using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Core.Persistence;

/// <summary>一个玩法独立维护的数据部分，使用稳定编号而非代码类型名称。</summary>
public abstract class SaveSection<TContext>
{
    /// <summary>长期保持稳定的部分编号。</summary>
    public string Id { get; }
    /// <summary>玩家可读的部分名称，本地化由登记方提供。</summary>
    public string Name { get; }
    /// <summary>当前数据版本，与模组发布版本独立。</summary>
    public int Version { get; }
    /// <summary>从正式版本起连续保留的数据转换。</summary>
    public IReadOnlyList<ISaveMigration> Migrations { get; }

    /// <summary>登记版本和转换；同一部分不维护旧版玩法实现。</summary>
    protected SaveSection(string id, string name, int version, IReadOnlyList<ISaveMigration> migrations)
    {
        Id = id;
        Name = name;
        Version = version;
        Migrations = migrations ?? Array.Empty<ISaveMigration>();
        SaveDataVersioning.Validate(id, version, Migrations);
    }

    /// <summary>采集稳定记录；没有内容时返回空。</summary>
    internal abstract JObject Capture(TContext context);
    /// <summary>在转换后检查记录，再恢复当前玩法状态。</summary>
    internal abstract void Restore(TContext context, JObject data);
}

/// <summary>为具体玩法绑定强类型数据，不保存委托或运行时上下文。</summary>
public sealed class SaveSection<TContext, TData> : SaveSection<TContext> where TData : class
{
    /// <summary>当前玩法的数据采集。</summary>
    private readonly Func<TContext, TData> capture;
    /// <summary>当前玩法的数据恢复。</summary>
    private readonly Action<TContext, TData> restore;
    /// <summary>在改动对象前检查外部存档数据。</summary>
    private readonly Action<TData> validate;
    /// <summary>每份记录使用独立的序列化器。</summary>
    private readonly JsonSerializerSettings settings;

    /// <summary>登记一个实际使用的数据部分；检查只在读取边界执行。</summary>
    public SaveSection(string id, string name, int version, Func<TContext, TData> capture,
        Action<TContext, TData> restore, Action<TData> validate = null,
        IReadOnlyList<ISaveMigration> migrations = null, JsonSerializerSettings settings = null)
        : base(id, name, version, migrations)
    {
        this.capture = capture;
        this.restore = restore;
        this.validate = validate;
        this.settings = settings ?? StableSaveJson.Settings;
    }

    /// <summary>仅采集明确的数据记录。</summary>
    internal override JObject Capture(TContext context)
    {
        TData data = capture(context);
        if (data == null) return null;
        validate?.Invoke(data);
        return JObject.FromObject(data, JsonSerializer.Create(settings));
    }

    /// <summary>反序列化及检查全部完成后才改写游戏对象。</summary>
    internal override void Restore(TContext context, JObject data)
    {
        TData record = data.ToObject<TData>(JsonSerializer.Create(settings));
        if (record == null) throw new InvalidDataException("资料为空");
        validate?.Invoke(record);
        restore(context, record);
    }
}

/// <summary>人物和世界共用的分部分保存读取流程；失败只影响对应部分。</summary>
public sealed class SaveSectionRegistry<TContext>
{
    /// <summary>外层格式只描述组织方式，不随每次发布变化。</summary>
    public const int FormatVersion = 1;
    /// <summary>按稳定编号查找已有部分。</summary>
    private readonly Dictionary<string, SaveSection<TContext>> sections = new(StringComparer.Ordinal);
    /// <summary>明确保留恢复顺序，不依赖不同运行环境中字典的遍历顺序。</summary>
    private readonly List<SaveSection<TContext>> orderedSections = new();

    /// <summary>注册稳定编号，同号重复是开发错误。</summary>
    public void Register(SaveSection<TContext> section)
    {
        sections.Add(section.Id, section);
        orderedSections.Add(section);
    }

    /// <summary>写入当前格式及各部分独立版本，不创建没有内容的部分。</summary>
    public string Capture(TContext context, string modVersion)
    {
        var payload = new JObject();
        foreach (SaveSection<TContext> section in orderedSections)
        {
            JObject data = section.Capture(context);
            if (data != null) payload.Add(section.Id, new JObject { ["version"] = section.Version, ["data"] = data });
        }
        return new JObject
        {
            ["formatVersion"] = FormatVersion, ["modVersion"] = modVersion, ["sections"] = payload
        }.ToString(Formatting.None);
    }

    /// <summary>先解析外层，再独立转换每一部分；输入原文始终保持不变。</summary>
    public void Restore(TContext context, string json, SaveLoadReport report, string owner)
    {
        if (string.IsNullOrEmpty(json)) return;
        JObject payload;
        try
        {
            JObject root = JObject.Parse(json);
            if (root["formatVersion"]?.Type != JTokenType.Integer ||
                root.Value<long>("formatVersion") != FormatVersion || root["sections"] is not JObject data)
                throw new InvalidDataException("不支持的存档格式或缺少内容");
            payload = data;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or FormatException or OverflowException)
        {
            report.Skip(owner, "存档格式", error.Message);
            return;
        }

        foreach (JProperty item in payload.Properties())
            if (!sections.ContainsKey(item.Name)) report.Skip(owner, item.Name, "对应玩法已删除或当前版本尚不支持");

        foreach (SaveSection<TContext> section in orderedSections)
        {
            if (!payload.TryGetValue(section.Id, out JToken token)) continue;
            try
            {
                if (token is not JObject part || part["data"] is not JObject data ||
                    part["version"]?.Type != JTokenType.Integer)
                    throw new InvalidDataException("资料缺少版本或内容");
                int version = part.Value<int>("version");
                JObject current = SaveDataVersioning.Upgrade(section.Id, section.Version, section.Migrations, data, version);
                section.Restore(context, current);
                if (version < section.Version) report.Upgraded++;
            }
            catch (Exception error) when (error is JsonException or InvalidDataException or
                                         SaveVersionTooNewException or FormatException or OverflowException)
            {
                report.Skip(owner, section.Name, error.Message);
                ModClass.LogWarning($"{owner} 的 {section.Name} 未恢复：{error}");
            }
        }
    }
}

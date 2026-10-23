using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Core.SkillLibV3.Effects;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>实际部位接触地块时调用的普通世界操作，不包含完整招式或额外范围。</summary>
public sealed class ConstructionWorldEffect
{
    /// <summary>已经登记的世界操作稳定编号。</summary>
    public string Action;

    /// <summary>针对同一地块的频率及次数；空值表示每块一次。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactPolicy Contact;

    /// <summary>本部位此操作最多接受的不同地块数，达到后不再接受新地块。</summary>
    public int MaximumTiles = 4096;
}

/// <summary>在主线程调用原版世界规则的已登记操作。</summary>
public sealed class ConstructionWorldAction
{
    /// <summary>稳定编号，不根据图形或材料猜测玩法。</summary>
    public string Id { get; }

    /// <summary>实际地块是否适用。</summary>
    internal readonly Func<WorldTile, bool> CanApply;

    /// <summary>实际提交结果，数值计算线程不调用。</summary>
    internal readonly Func<WorldTile, SkillEffectResult> Apply;

    /// <summary>登记一个实际操作，不登记整招。</summary>
    /// <param name="id">稳定编号。</param><param name="canApply">只读实际预检。</param>
    /// <param name="apply">主线程世界提交。</param>
    public ConstructionWorldAction(string id, Func<WorldTile, bool> canApply, Func<WorldTile, SkillEffectResult> apply)
    {
        if (string.IsNullOrWhiteSpace(id) || canApply == null || apply == null)
            throw new ArgumentException("世界操作需要编号、实际预检和提交");
        Id = id;
        CanApply = canApply;
        Apply = apply;
    }
}

/// <summary>世界操作在内容初始化时登记，构造入口解析实际规则。</summary>
public static class ConstructionWorldActions
{
    /// <summary>进程内的稳定操作目录，世界清理不删除规则。</summary>
    private static readonly Dictionary<string, ConstructionWorldAction> actions = new(StringComparer.Ordinal);

    /// <summary>新增一种原理供任意构造组合，不替换已经运行的规则。</summary>
    /// <param name="action">实际规则。</param>
    public static void Register(ConstructionWorldAction action)
    {
        if (action == null || actions.ContainsKey(action.Id)) throw new ArgumentException("世界操作为空或编号重复");
        actions.Add(action.Id, action);
    }

    /// <summary>在资料边界拒绝未知规则，保留具体错误位置。</summary>
    /// <param name="id">稳定编号。</param><param name="path">当前资料位置。</param>
    internal static ConstructionWorldAction Resolve(string id, string path) =>
        id != null && actions.TryGetValue(id, out var action)
            ? action
            : throw new InvalidDataException(path + "：尚未登记世界操作：" + id);
}

/// <summary>已经复制和解析的地块接触规则。</summary>
public sealed class CompiledConstructionWorldEffect
{
    /// <summary>已登记的实际世界操作。</summary>
    public ConstructionWorldAction Action { get; }

    /// <summary>不同地块的显式选择上限。</summary>
    public int MaximumTiles { get; }

    /// <summary>沿用共同接触规则的数值副本。</summary>
    internal readonly CompiledContactPolicy Contact;

    /// <summary>只在资料入口解析和检查。</summary>
    /// <param name="source">外部规则。</param><param name="path">资料位置。</param>
    internal CompiledConstructionWorldEffect(ConstructionWorldEffect source, string path)
    {
        if (source == null || source.MaximumTiles < 1 || source.MaximumTiles > 1048576)
            throw new InvalidDataException(path + "：世界接触需要1至1048576个明确地块上限");
        Action = ConstructionWorldActions.Resolve(source.Action, path);
        MaximumTiles = source.MaximumTiles;
        Contact = new CompiledContactPolicy(source.Contact, path + "/Contact");
    }
}

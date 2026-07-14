using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cultiway.Const;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Core.ActorFiltering;

/// <summary>角色筛选器下可选择的一个具体对象。</summary>
public readonly struct ActorFilterOption
{
    public ActorFilterOption(string id, string displayName, string iconPath = null,
        NanoObject metaObject = null)
    {
        Id = id;
        DisplayName = displayName;
        IconPath = iconPath;
        MetaObject = metaObject;
    }

    /// <summary>元对象 ID 或修炼体系资产 ID 的稳定字符串形式。</summary>
    public string Id { get; }

    /// <summary>配置窗口中展示的当前名称。</summary>
    public string DisplayName { get; }

    /// <summary>非元对象候选项使用的图标路径。</summary>
    public string IconPath { get; }

    /// <summary>元对象候选项的当前世界实例，用于生成原版 banner；其他候选项为 null。</summary>
    public NanoObject MetaObject { get; }
}

/// <summary>已经加入角色筛选表达式的一项条件。</summary>
public readonly struct ActorFilterEntry
{
    public ActorFilterEntry(string typeId, string valueId, string displayName)
    {
        TypeId = typeId;
        ValueId = valueId;
        DisplayName = displayName;
    }

    /// <summary>过滤类别的稳定标识。</summary>
    public string TypeId { get; }

    /// <summary>该类别下被选中对象的稳定标识。</summary>
    public string ValueId { get; }

    /// <summary>添加条件时记录的名称，用于对象消失后仍能说明原条件。</summary>
    public string DisplayName { get; }
}

/// <summary>一种可由角色筛选编辑器选择的过滤类别。</summary>
public sealed class ActorFilterDescriptor
{
    private readonly Func<IReadOnlyList<ActorFilterOption>> _getOptions;
    private readonly Func<Actor, string, bool> _matches;

    public ActorFilterDescriptor(string id, string nameKey, bool isCultisys,
        Func<IReadOnlyList<ActorFilterOption>> getOptions, Func<Actor, string, bool> matches,
        MetaType metaType = MetaType.None, string iconPath = null)
    {
        Id = id;
        NameKey = nameKey;
        IsCultisys = isCultisys;
        MetaType = metaType;
        IconPath = iconPath;
        _getOptions = getOptions ?? throw new ArgumentNullException(nameof(getOptions));
        _matches = matches;
    }

    /// <summary>过滤类别的稳定标识。</summary>
    public string Id { get; }

    /// <summary>过滤类别名称的本地化键。</summary>
    public string NameKey { get; }

    /// <summary>该类别是否表示修炼体系，并需要由调用方解释其匹配语义。</summary>
    public bool IsCultisys { get; }

    /// <summary>该类别对应的原版或扩展元对象类型；非元对象类别为 None。</summary>
    public MetaType MetaType { get; }

    /// <summary>过滤类别列表使用的图标路径。</summary>
    public string IconPath { get; }

    /// <summary>重新读取当前世界中可选择的对象。</summary>
    public IReadOnlyList<ActorFilterOption> GetOptions()
    {
        return _getOptions();
    }

    /// <summary>检查角色是否属于指定元对象；修炼体系类别不通过此入口判断。</summary>
    public bool Matches(Actor actor, string valueId)
    {
        return _matches != null && _matches(actor, valueId);
    }
}

/// <summary>角色逻辑筛选可使用的类别目录。</summary>
public static class ActorFilterCatalog
{
    public const string CultisysTypeId = "cultisys";

    private static readonly List<ActorFilterDescriptor> Descriptors = new();
    private static bool _initialized;

    /// <summary>按注册顺序提供给配置窗口的过滤类别。</summary>
    public static IReadOnlyList<ActorFilterDescriptor> Types => Descriptors;

    /// <summary>注册默认过滤类别；重复调用不会产生重复项。</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        Register(new ActorFilterDescriptor(CultisysTypeId,
            "Cultiway.ActorFilter.UI.FilterType.Cultisys", true, GetCultisysOptions, null,
            iconPath: "cultiway/icons/iconCultivation"));
        RegisterMeta(MetaType.Subspecies, "Cultiway.ActorFilter.UI.FilterType.Race", actor => actor.subspecies);
        RegisterMeta(MetaType.Family, "Cultiway.ActorFilter.UI.FilterType.Family", actor => actor.family);
        RegisterMeta(MetaType.Language, "Cultiway.ActorFilter.UI.FilterType.Language", actor => actor.language);
        RegisterMeta(MetaType.Culture, "Cultiway.ActorFilter.UI.FilterType.Culture", actor => actor.culture);
        RegisterMeta(MetaType.Religion, "Cultiway.ActorFilter.UI.FilterType.Religion", actor => actor.religion);
        RegisterMeta(MetaType.Clan, "Cultiway.ActorFilter.UI.FilterType.Clan", actor => actor.clan);
        RegisterMeta(MetaType.City, "Cultiway.ActorFilter.UI.FilterType.City", actor => actor.city);
        RegisterMeta(MetaType.Kingdom, "Cultiway.ActorFilter.UI.FilterType.Kingdom", actor => actor.kingdom);
        RegisterMeta(MetaType.Alliance, "Cultiway.ActorFilter.UI.FilterType.Alliance",
            actor => actor.kingdom == null ? null : actor.kingdom.getAlliance());
        RegisterMeta(MetaType.Army, "Cultiway.ActorFilter.UI.FilterType.Army", actor => actor.army);
        RegisterMeta(MetaTypeExtend.Sect.Back(), "Cultiway.ActorFilter.UI.FilterType.Sect",
            actor => actor.GetExtend().sect);
        Register(new ActorFilterDescriptor(WorldboxGame.MetaTypes.GeoRegion.id,
            "Cultiway.ActorFilter.UI.FilterType.GeoRegion", false,
            () => GetMetaOptions(MetaTypeExtend.GeoRegion.Back()), MatchesGeoRegion,
            MetaTypeExtend.GeoRegion.Back(), WorldboxGame.MetaTypes.GeoRegion.icon_single_path));
    }

    /// <summary>注册或替换过滤类别；替换同 ID 类别时保持原有显示顺序。</summary>
    public static void Register(ActorFilterDescriptor descriptor)
    {
        if (descriptor == null) return;
        for (var i = 0; i < Descriptors.Count; i++)
        {
            if (Descriptors[i].Id != descriptor.Id) continue;
            Descriptors[i] = descriptor;
            return;
        }
        Descriptors.Add(descriptor);
    }

    /// <summary>按稳定类别标识取得过滤定义；不存在时返回 null。</summary>
    public static ActorFilterDescriptor Get(string id)
    {
        for (var i = 0; i < Descriptors.Count; i++)
        {
            if (Descriptors[i].Id == id) return Descriptors[i];
        }
        return null;
    }

    private static void RegisterMeta(MetaType type, string nameKey, Func<Actor, NanoObject> resolve)
    {
        var asset = type.getAsset();
        Register(new ActorFilterDescriptor(asset.id, nameKey, false, () => GetMetaOptions(type),
            (actor, valueId) => MatchesSingleMeta(actor, valueId, resolve), type,
            !string.IsNullOrEmpty(asset.icon_single_path) ? asset.icon_single_path : asset.icon_list));
    }

    private static IReadOnlyList<ActorFilterOption> GetCultisysOptions()
    {
        return ProgressionService.RegisteredCultisyses
            .OrderBy(asset => asset.GetName(), StringComparer.Ordinal)
            .Select(asset => new ActorFilterOption(asset.id, asset.GetName(), asset.IconPath))
            .ToArray();
    }

    private static IReadOnlyList<ActorFilterOption> GetMetaOptions(MetaType type)
    {
        var asset = type.getAsset();
        if (asset?.get_list == null) return Array.Empty<ActorFilterOption>();

        var result = new List<ActorFilterOption>();
        var objects = asset.get_list();
        if (objects == null) return result;
        foreach (var item in objects)
        {
            if (item == null || !item.isAlive()) continue;
            var name = string.IsNullOrWhiteSpace(item.name) ? $"#{item.getID()}" : item.name;
            result.Add(new ActorFilterOption(
                item.getID().ToString(CultureInfo.InvariantCulture), name, metaObject: item));
        }
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.DisplayName, right.DisplayName));
        return result;
    }

    private static bool MatchesSingleMeta(Actor actor, string valueId, Func<Actor, NanoObject> resolve)
    {
        if (actor == null || !long.TryParse(valueId, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var targetId)) return false;
        var meta = resolve(actor);
        return meta != null && meta.isAlive() && meta.getID() == targetId;
    }

    private static bool MatchesGeoRegion(Actor actor, string valueId)
    {
        if (actor?.current_tile == null ||
            !long.TryParse(valueId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetId))
            return false;

        foreach (var region in actor.current_tile.GetExtend().GetGeoRegions())
        {
            if (region != null && region.isAlive() && region.getID() == targetId) return true;
        }
        return false;
    }
}

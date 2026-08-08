using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Libraries;

namespace Cultiway.Content.KnightCombat;

/// <summary>建立骑士战技的稳定领域索引。</summary>
[Dependency(typeof(KnightTechniques))]
public sealed class KnightTechniqueCatalog : ICanInit
{
    private static readonly List<KnightTechniqueAsset> all = new();
    private static readonly Dictionary<string, KnightTechniqueAsset> byTechniqueId = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<KnightTechniqueAsset>> byStyleId = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<KnightTechniqueAsset>> byWeaponGroup =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, KnightTechniqueAsset[]> styleViews =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, KnightTechniqueAsset[]> weaponGroupViews =
        new(StringComparer.Ordinal);

    /// <summary>按稳定顺序枚举全部战技。</summary>
    public static IReadOnlyList<KnightTechniqueAsset> All => all;

    /// <summary>构建不含运行时执行状态的战技只读索引。</summary>
    public void Init()
    {
        all.Clear();
        byTechniqueId.Clear();
        byStyleId.Clear();
        byWeaponGroup.Clear();
        styleViews.Clear();
        weaponGroupViews.Clear();

        var assets = new List<KnightTechniqueAsset>(Libraries.Manager.KnightTechniqueLibrary.list);
        assets.Sort(CompareAssets);
        for (int i = 0; i < assets.Count; i++)
        {
            KnightTechniqueAsset technique = assets[i];
            all.Add(technique);
            byTechniqueId.Add(technique.id, technique);
            AddStyleTechnique(technique.Style.id, technique);
            for (int j = 0; j < technique.Style.WeaponGroups.Length; j++)
            {
                AddWeaponTechnique(technique.Style.WeaponGroups[j], technique);
            }
        }

        foreach (KeyValuePair<string, List<KnightTechniqueAsset>> pair in byStyleId)
        {
            styleViews.Add(pair.Key, pair.Value.ToArray());
        }
        foreach (KeyValuePair<string, List<KnightTechniqueAsset>> pair in byWeaponGroup)
        {
            weaponGroupViews.Add(pair.Key, pair.Value.ToArray());
        }
    }

    /// <summary>按战技资产 ID 解析战技。</summary>
    public static KnightTechniqueAsset Get(string techniqueId)
    {
        return byTechniqueId[techniqueId];
    }

    /// <summary>返回流派下按等级和 ID 排序的战技。</summary>
    public static IReadOnlyList<KnightTechniqueAsset> GetByStyle(KnightStyleAsset style)
    {
        return styleViews[style.id];
    }

    /// <summary>返回兼容指定武器组的战技并集。</summary>
    public static IReadOnlyList<KnightTechniqueAsset> GetByWeaponGroup(string weaponGroup)
    {
        return weaponGroupViews.TryGetValue(weaponGroup, out KnightTechniqueAsset[] techniques)
            ? techniques
            : Array.Empty<KnightTechniqueAsset>();
    }

    private static void AddStyleTechnique(string styleId, KnightTechniqueAsset technique)
    {
        if (!byStyleId.TryGetValue(styleId, out List<KnightTechniqueAsset> techniques))
        {
            techniques = new List<KnightTechniqueAsset>();
            byStyleId.Add(styleId, techniques);
        }
        techniques.Add(technique);
    }

    private static void AddWeaponTechnique(string weaponGroup, KnightTechniqueAsset technique)
    {
        if (!byWeaponGroup.TryGetValue(weaponGroup, out List<KnightTechniqueAsset> techniques))
        {
            techniques = new List<KnightTechniqueAsset>();
            byWeaponGroup.Add(weaponGroup, techniques);
        }
        if (!techniques.Contains(technique)) techniques.Add(technique);
    }

    private static int CompareAssets(KnightTechniqueAsset left, KnightTechniqueAsset right)
    {
        int styleOrder = left.Style.SortOrder.CompareTo(right.Style.SortOrder);
        if (styleOrder != 0) return styleOrder;
        int level = left.MinimumKnightLevel.CompareTo(right.MinimumKnightLevel);
        return level != 0 ? level : string.CompareOrdinal(left.id, right.id);
    }
}

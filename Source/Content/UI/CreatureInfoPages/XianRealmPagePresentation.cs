using System;
using System.Collections.Generic;
using System.Globalization;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.UI.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.UI.CreatureInfoPages;

/// <summary>境界纹章固定造型与实例强调色组成的展示数据。</summary>
internal readonly struct RealmEmblemPresentation
{
    public readonly string BasePath;
    public readonly string PrimaryPath;
    public readonly string SecondaryPath;
    public readonly Color PrimaryColor;
    public readonly Color SecondaryColor;

    public RealmEmblemPresentation(string assetName, Color primaryColor, Color secondaryColor)
    {
        const string root = "cultiway/ui/realm_pages/";
        BasePath = root + assetName + "_base";
        PrimaryPath = root + assetName + "_primary";
        SecondaryPath = root + assetName + "_secondary";
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
    }
}

/// <summary>筑基详情页一次刷新所需的完整只读数据。</summary>
internal readonly struct FoundationPageModel
{
    public readonly ActorExtend Actor;
    public readonly XianBase Foundation;
    public readonly float[] ThreeFlowerValues;
    public readonly float[] FiveQiValues;
    public readonly int CompletedCount;
    public readonly RealmEmblemPresentation Emblem;
    public readonly bool IsCurrent;

    public FoundationPageModel(ActorExtend actor)
    {
        Actor = actor;
        XianBase foundation = actor.GetComponent<XianBase>();
        Foundation = foundation;
        ThreeFlowerValues = new[] { foundation.jing, foundation.qi, foundation.shen };
        FiveQiValues = new[] { foundation.iron, foundation.wood, foundation.water, foundation.fire, foundation.earth };
        CompletedCount = CountPositive(ThreeFlowerValues) + CountPositive(FiveQiValues);

        Color primary = XianRealmPagePresentation.FindDominantColor(
            FiveQiValues,
            XianRealmPagePresentation.FiveQiColors,
            XianRealmPagePresentation.FoundationPrimary);
        Color secondary = XianRealmPagePresentation.FindDominantColor(
            ThreeFlowerValues,
            XianRealmPagePresentation.ThreeFlowerColors,
            XianRealmPagePresentation.FoundationSecondary);
        Emblem = new RealmEmblemPresentation("foundation", primary, secondary);
        IsCurrent = actor.GetCultisys<Xian>().CurrLevel == Cultiway.Content.Const.XianLevels.XianBase;
    }

    private static int CountPositive(float[] values)
    {
        var count = 0;
        for (var i = 0; i < values.Length; i++)
            if (XianRealmPagePresentation.IsPositive(values[i]))
                count++;
        return count;
    }
}

/// <summary>命名真气、金丹或元婴详情页一次刷新所需的完整只读数据。</summary>
internal readonly struct CoreFormationPageModel
{
    public readonly ActorExtend Actor;
    public readonly CoreFormationRealm Realm;
    public readonly CoreFormationSnapshot Formation;
    public readonly string Name;
    public readonly float Strength;
    public readonly int Stage;
    public readonly string Lineage;

    /// <summary>当前境界需要替代通用标题摘要的运行状态；空值沿用通用摘要。</summary>
    public readonly string SummaryOverride;
    public readonly int NextEvolutionStage;
    public readonly RealmEmblemPresentation Emblem;
    public readonly bool IsCurrent;

    public CoreFormationPageModel(
        ActorExtend actor,
        CoreFormationRealm realm,
        CoreFormationSnapshot formation,
        string name,
        float strength,
        int stage,
        string lineage,
        int nextEvolutionStage,
        string summaryOverride = null)
    {
        Actor = actor;
        Realm = realm;
        Formation = formation;
        Name = name;
        Strength = strength;
        Stage = stage;
        Lineage = lineage;
        SummaryOverride = summaryOverride;
        NextEvolutionStage = nextEvolutionStage;

        Color fallback = realm switch
        {
            CoreFormationRealm.QiRefinement => XianRealmPagePresentation.QiRefinementPrimary,
            CoreFormationRealm.Foundation => XianRealmPagePresentation.FoundationPrimary,
            CoreFormationRealm.Jindan => XianRealmPagePresentation.JindanPrimary,
            CoreFormationRealm.Yuanying => XianRealmPagePresentation.YuanyingPrimary,
            CoreFormationRealm.Yuanshen => XianRealmPagePresentation.YuanshenPrimary,
            _ => throw new ArgumentOutOfRangeException(nameof(realm), realm, "未知核心形成境界。")
        };
        (Color primary, Color secondary) = formation.IsValid
            ? XianRealmPagePresentation.ResolveCompositionColors(formation.composition, fallback)
            : realm == CoreFormationRealm.QiRefinement
                ? (XianRealmPagePresentation.QiRefinementPrimary,
                    XianRealmPagePresentation.QiRefinementSecondary)
                : (fallback, Color.Lerp(fallback, Color.white, 0.45f));
        string emblem = realm switch
        {
            CoreFormationRealm.QiRefinement => "qi_refinement",
            CoreFormationRealm.Foundation => "foundation",
            CoreFormationRealm.Jindan => "jindan",
            CoreFormationRealm.Yuanying => "yuanying",
            CoreFormationRealm.Yuanshen => "yuanying",
            _ => throw new ArgumentOutOfRangeException(nameof(realm), realm, "未知核心形成境界。")
        };
        Emblem = new RealmEmblemPresentation(emblem, primary, secondary);
        int currentLevel = actor.GetCultisys<Xian>().CurrLevel;
        IsCurrent = realm switch
        {
            CoreFormationRealm.QiRefinement => currentLevel == Cultiway.Content.Const.XianLevels.QiRefinement,
            CoreFormationRealm.Foundation => currentLevel == Cultiway.Content.Const.XianLevels.XianBase,
            CoreFormationRealm.Jindan => currentLevel == Cultiway.Content.Const.XianLevels.Jindan,
            CoreFormationRealm.Yuanying => currentLevel == Cultiway.Content.Const.XianLevels.Yuanying,
            CoreFormationRealm.Yuanshen => currentLevel >= Cultiway.Content.Const.XianLevels.Huashen,
            _ => false
        };
    }
}

/// <summary>已显化形成原子的状态与资产定义。</summary>
internal readonly struct CoreFormationAtomPresentation
{
    public readonly CoreFormationAtomState State;
    public readonly CoreFormationAtomAsset Asset;

    public CoreFormationAtomPresentation(CoreFormationAtomState state, CoreFormationAtomAsset asset)
    {
        State = state;
        Asset = asset;
    }
}

/// <summary>集中提供境界页使用的颜色、图标、格式化和资产解析规则。</summary>
internal static class XianRealmPagePresentation
{
    public const float PageWidth = 246f;
    public const float PageHeight = 208f;
    public const float MetricCellWidth = 44f;
    public const float MetricCellHeight = 22f;

    public static readonly Color QiRefinementPrimary = new(0.24f, 0.76f, 0.96f, 1f);
    public static readonly Color QiRefinementSecondary = new(0.69f, 0.42f, 0.94f, 1f);
    public static readonly Color FoundationPrimary = new(0.53f, 0.81f, 0.92f, 1f);
    public static readonly Color FoundationSecondary = new(0.9f, 0.95f, 1f, 1f);
    public static readonly Color JindanPrimary = new(1f, 0.78f, 0.15f, 1f);
    public static readonly Color YuanyingPrimary = new(0.66f, 0.52f, 0.94f, 1f);
    public static readonly Color YuanshenPrimary = new(0.25f, 0.82f, 0.72f, 1f);

    public static readonly Color[] ThreeFlowerColors =
    {
        new(0.93f, 0.35f, 0.31f, 1f),
        new(0.28f, 0.82f, 0.74f, 1f),
        new(0.68f, 0.48f, 0.92f, 1f)
    };

    public static readonly Color[] FiveQiColors =
    {
        ElementRootDiagramStyles.GetColor(ElementIndex.Iron),
        ElementRootDiagramStyles.GetColor(ElementIndex.Wood),
        ElementRootDiagramStyles.GetColor(ElementIndex.Water),
        ElementRootDiagramStyles.GetColor(ElementIndex.Fire),
        ElementRootDiagramStyles.GetColor(ElementIndex.Earth)
    };

    public static readonly string[] ThreeFlowerIconPaths =
    {
        "ui/icons/iconHealth",
        "cultiway/icons/iconWakan",
        "ui/icons/actor_traits/iconIntelligence"
    };

    public static readonly string[] FiveQiIconPaths =
    {
        "cultiway/icons/element_root/iron",
        "cultiway/icons/element_root/wood",
        "cultiway/icons/element_root/water",
        "cultiway/icons/element_root/fire",
        "cultiway/icons/element_root/earth"
    };

    public static readonly string[] ElementIconPaths =
    {
        "cultiway/icons/element_root/iron",
        "cultiway/icons/element_root/wood",
        "cultiway/icons/element_root/water",
        "cultiway/icons/element_root/fire",
        "cultiway/icons/element_root/earth",
        "cultiway/icons/element_root/neg",
        "cultiway/icons/element_root/pos",
        "cultiway/icons/element_root/entropy"
    };

    public static readonly string[] ThreeFlowerNameKeys =
    {
        "Cultiway.RealmPage.Foundation.Jing",
        "Cultiway.RealmPage.Foundation.Qi",
        "Cultiway.RealmPage.Foundation.Shen"
    };

    /// <summary>返回八元素统一展示色。</summary>
    public static Color GetElementColor(int elementIndex)
    {
        return ElementRootDiagramStyles.GetColor(elementIndex);
    }

    /// <summary>返回指定成果境界的显化色。</summary>
    public static Color GetRealmColor(CoreFormationRealm realm)
    {
        return realm switch
        {
            CoreFormationRealm.QiRefinement => QiRefinementPrimary,
            CoreFormationRealm.Foundation => FoundationSecondary,
            CoreFormationRealm.Jindan => JindanPrimary,
            CoreFormationRealm.Yuanying => YuanyingPrimary,
            CoreFormationRealm.Yuanshen => YuanshenPrimary,
            _ => throw new ArgumentOutOfRangeException(nameof(realm), realm, "未知核心形成境界。")
        };
    }

    /// <summary>返回指定成果境界所继承的前序境界颜色。</summary>
    public static Color GetInheritedRealmColor(CoreFormationRealm realm)
    {
        return realm switch
        {
            CoreFormationRealm.Foundation => QiRefinementPrimary,
            CoreFormationRealm.Jindan => FoundationSecondary,
            CoreFormationRealm.Yuanying => JindanPrimary,
            CoreFormationRealm.Yuanshen => YuanyingPrimary,
            _ => FoundationPrimary
        };
    }

    /// <summary>把元素组成复制、归一化并清理非法值。</summary>
    public static float[] GetNormalizedComposition(ElementComposition composition)
    {
        composition.Normalize();
        float[] values = composition.AsArray();
        for (var i = 0; i < values.Length; i++)
            if (!IsPositive(values[i]))
                values[i] = 0f;
        return values;
    }

    /// <summary>从归一化元素组成中稳定选择主色和次色。</summary>
    public static (Color primary, Color secondary) ResolveCompositionColors(
        ElementComposition composition,
        Color fallback)
    {
        float[] values = GetNormalizedComposition(composition);
        var first = -1;
        var second = -1;
        for (var i = 0; i < values.Length; i++)
        {
            if (!IsPositive(values[i])) continue;
            if (first < 0 || values[i] > values[first])
            {
                second = first;
                first = i;
            }
            else if (second < 0 || values[i] > values[second])
            {
                second = i;
            }
        }

        if (first < 0)
            return (fallback, Color.Lerp(fallback, Color.white, 0.45f));

        Color primary = GetElementColor(first);
        Color secondary = second < 0
            ? Color.Lerp(fallback, Color.white, 0.45f)
            : GetElementColor(second);
        return (primary, secondary);
    }

    /// <summary>按最大正值稳定选择对应颜色；没有正值时返回默认色。</summary>
    public static Color FindDominantColor(float[] values, Color[] colors, Color fallback)
    {
        var selected = -1;
        for (var i = 0; i < values.Length && i < colors.Length; i++)
        {
            if (!IsPositive(values[i])) continue;
            if (selected < 0 || values[i] > values[selected]) selected = i;
        }
        return selected < 0 ? fallback : colors[selected];
    }

    /// <summary>返回当前阶段已经显化并仍有资产定义的形成原子。</summary>
    public static List<CoreFormationAtomPresentation> ResolveActiveAtoms(
        CoreFormationSnapshot formation,
        int stage)
    {
        var result = new List<CoreFormationAtomPresentation>(formation.atoms?.Length ?? 0);
        foreach (CoreFormationAtomState state in formation.atoms ?? Array.Empty<CoreFormationAtomState>())
        {
            if (!state.IsActive(stage)) continue;
            CoreFormationAtomAsset asset = Libraries.Manager.CoreFormationAtomLibrary.get(state.atom_id);
            if (asset != null) result.Add(new CoreFormationAtomPresentation(state, asset));
        }
        return result;
    }

    /// <summary>解析代表法术实体资产；ID 为空时返回 null。</summary>
    public static SkillEntityAsset ResolveRepresentativeSkill(string skillId)
    {
        return string.IsNullOrEmpty(skillId) ? null : ModClass.I.SkillV3.SkillLib.get(skillId);
    }

    /// <summary>取得法术实体的独立 UI 图标。</summary>
    public static Sprite ResolveSkillPreview(SkillEntityAsset asset)
    {
        return asset?.ResolveIcon(0);
    }

    /// <summary>取得代表法术的本地化名称。</summary>
    public static string ResolveSkillName(SkillEntityAsset asset)
    {
        if (asset == null) return "Cultiway.RealmPage.Skill.Empty".Localize();
        return LM.Has(asset.id) ? LM.Get(asset.id) : asset.id;
    }

    /// <summary>取得代表法术可用于悬停展示的编辑器说明。</summary>
    public static string ResolveSkillDescription(SkillEntityAsset asset)
    {
        if (asset == null || string.IsNullOrEmpty(asset.EditorDescriptionKey) ||
            !LM.Has(asset.EditorDescriptionKey))
            return string.Empty;
        return LM.Get(asset.EditorDescriptionKey);
    }

    /// <summary>把连续数值格式化为境界页使用的紧凑形式。</summary>
    public static string FormatNumber(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) value = 0f;
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>判断数值是否可作为有效正权重参与展示。</summary>
    public static bool IsPositive(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}

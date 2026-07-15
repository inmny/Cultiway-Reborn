using System;
using System.Linq;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>
/// 将法宝领域数据转换成界面文案和颜色。展示规则集中在此处，不污染 Content 中的模板定义。
/// </summary>
internal static class BaibaoPresentation
{
    public static string GetShapeName(ArtifactBlueprint blueprint)
    {
        return ModClass.L.ItemShapeLibrary.get(blueprint.ShapeId) is ArtifactShapeAsset shape
            ? GetShapeName(shape)
            : blueprint.ShapeId;
    }

    public static string GetShapeName(ArtifactShapeAsset shape)
    {
        return shape.ingredient_name_candidates.FirstOrDefault() ?? LocalizedOrFallback(shape.id, shape.id);
    }

    public static string GetAtomName(ArtifactAtomAsset atom)
    {
        string key = $"Cultiway.Baibao.Atom.{atom.id}";
        return LocalizedOrFallback(key, atom.name_stems.FirstOrDefault() ?? atom.tag ?? atom.id);
    }

    public static string GetAtomCategoryName(ArtifactAtomCategory category)
    {
        return $"Cultiway.Baibao.UI.AtomCategory.{category}".Localize();
    }

    public static string GetAbilityName(string abilityId)
    {
        ArtifactAbilityAsset ability = Content.Libraries.Manager.ArtifactAbilityLibrary.get(abilityId);
        return ability?.GetName() ?? abilityId;
    }

    public static string GetAbilityDescription(ArtifactAbilityInstance instance)
    {
        ArtifactAbilityAsset ability = Content.Libraries.Manager.ArtifactAbilityLibrary.get(instance.ability_id);
        return ability?.GetDescription(instance) ?? string.Empty;
    }

    public static string GetTemplateName(string key)
    {
        return AppearanceName("Template", key);
    }

    public static string GetModuleName(string key)
    {
        return AppearanceName("Module", key);
    }

    public static string GetVariantName(string module, string variant)
    {
        return AppearanceName("Variant", $"{module}.{variant}");
    }

    public static string GetColorSchemeName(string key)
    {
        return AppearanceName("Color", key);
    }

    public static Color GetColorSchemeSwatch(ArtifactAppearanceColorSchemeDef scheme)
    {
        string[] preferred = { "metal", "cloth", "top", "glass", "rim", "edge" };
        for (int i = 0; i < preferred.Length; i++)
        {
            if (scheme.Colors.TryGetValue(preferred[i], out string value) &&
                ColorUtility.TryParseHtmlString(value, out Color color))
            {
                return color;
            }
        }

        foreach (string value in scheme.Colors.Values)
        {
            if (ColorUtility.TryParseHtmlString(value, out Color color)) return color;
        }
        return Color.gray;
    }

    public static string GetOrigin(ArtifactBlueprint blueprint)
    {
        return blueprint.OriginKind == ArtifactBlueprintOriginKind.Forged
            ? "Cultiway.Baibao.UI.State.Forged".Localize()
            : string.Format("Cultiway.Baibao.UI.Format.ArchivedOrigin".Localize(), blueprint.SourceActorName);
    }

    private static string AppearanceName(string kind, string key)
    {
        return LocalizedOrFallback($"Cultiway.Baibao.Appearance.{kind}.{key}", Humanize(key));
    }

    private static string LocalizedOrFallback(string key, string fallback)
    {
        return LM.Has(key) ? LM.Get(key) : fallback;
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string tail = value.Contains('.') ? value.Substring(value.LastIndexOf('.') + 1) : value;
        return string.Join(" ", tail.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries));
    }
}

using System;
using System.Linq;
using System.Text;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.Baibao;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Semantics;
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
        return atom.GetName();
    }

    public static string GetAtomDescription(ArtifactAtomAsset atom)
    {
        return atom.GetDescription();
    }

    public static Sprite GetAtomIcon(ArtifactAtomAsset atom)
    {
        Sprite sprite = string.IsNullOrEmpty(atom.editor_icon_path)
            ? null
            : SpriteTextureLoader.getSprite(atom.editor_icon_path);
        return sprite ?? SpriteTextureLoader.getSprite(GetAtomCategoryIconPath(atom.category));
    }

    public static string GetAtomCategoryIconPath(ArtifactAtomCategory category)
    {
        return category switch
        {
            ArtifactAtomCategory.Shape => BaibaoUiIcons.Shape,
            ArtifactAtomCategory.Material => BaibaoUiIcons.Composition,
            ArtifactAtomCategory.Finish => BaibaoUiIcons.Appearance,
            _ => BaibaoUiIcons.Composition,
        };
    }

    public static string GetTraitName(string traitKey)
    {
        return ModClass.L.SemanticLibrary.TryResolve(traitKey, out SemanticAsset semantic)
            ? semantic.GetName()
            : traitKey;
    }

    public static string GetTraitFacetName(string traitKey)
    {
        return ModClass.L.SemanticLibrary.TryResolve(traitKey, out SemanticAsset semantic)
            ? semantic.Facet.GetName()
            : string.Empty;
    }

    public static string GetAtomTraitSummary(ArtifactAtomAsset atom, float strength, int limit = 3)
    {
        return string.Join(" · ", (atom.material_traits ?? [])
            .Where(trait => trait.value != 0f)
            .OrderByDescending(trait => Math.Abs(trait.value * strength))
            .Take(limit)
            .Select(trait => $"{GetTraitName(trait.key)} {FormatSigned(trait.value * strength)}"));
    }

    public static string GetAtomSearchText(ArtifactAtomAsset atom)
    {
        StringBuilder builder = new();
        builder.Append(GetAtomName(atom)).Append(' ')
            .Append(GetAtomDescription(atom)).Append(' ')
            .Append(GetAtomCategoryName(atom.category));
        string[] stems = atom.name_stems ?? [];
        for (int i = 0; i < stems.Length; i++) builder.Append(' ').Append(stems[i]);
        ArtifactMaterialTrait[] traits = atom.material_traits ?? [];
        for (int i = 0; i < traits.Length; i++)
        {
            builder.Append(' ').Append(GetTraitName(traits[i].key))
                .Append(' ').Append(GetTraitFacetName(traits[i].key));
        }
        return builder.ToString();
    }

    public static string GetAtomTooltipDetail(ArtifactAtomAsset atom, float strength)
    {
        string traits = GetAtomTraitSummary(atom, strength, 6);
        return string.IsNullOrEmpty(traits)
            ? GetAtomCategoryName(atom.category)
            : $"{GetAtomCategoryName(atom.category)}\n" +
              string.Format("Cultiway.Baibao.UI.Format.AtomTraits".Localize(), traits);
    }

    public static string GetAtomBiasSummary(ArtifactAtomAsset atom)
    {
        string variants = string.Join("、", (atom.variant_biases ?? []).Take(3)
            .Select(value => GetVariantName(value.Contains('.') ? value.Substring(0, value.IndexOf('.')) : string.Empty,
                value.Contains('.') ? value.Substring(value.IndexOf('.') + 1) : value)));
        string colors = string.Join("、", (atom.color_scheme_biases ?? []).Take(3).Select(GetColorSchemeName));
        if (string.IsNullOrEmpty(variants)) return colors;
        if (string.IsNullOrEmpty(colors)) return variants;
        return $"{variants}\n{colors}";
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

    public static Color GetColorSchemeSwatch(
        ArtifactAppearanceColorSchemeDef scheme,
        ArtifactAppearanceColorRoleDef role)
    {
        return scheme.Colors.TryGetValue(role.FallbackChannel, out string value) &&
               ColorUtility.TryParseHtmlString(value, out Color color)
            ? color
            : Color.gray;
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

    public static string FormatSigned(float value)
    {
        return value >= 0f ? $"+{value:0.##}" : value.ToString("0.##");
    }
}

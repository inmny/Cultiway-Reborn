using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Content.Components.Skill;

namespace Cultiway.Content;

public partial class SkillEntities
{
    /// <summary>永久抬升目标区域一个原版地形层级。</summary>
    public static SkillEntityAsset RaiseTerrain { get; private set; }

    /// <summary>永久降低目标区域一个原版地形层级。</summary>
    public static SkillEntityAsset LowerTerrain { get; private set; }

    /// <summary>向目标区域中原版允许蓄水的地块填充海水。</summary>
    public static SkillEntityAsset FillWater { get; private set; }

    /// <summary>排除目标区域内的普通海水。</summary>
    public static SkillEntityAsset DrainWater { get; private set; }

    /// <summary>维持八秒并周期催生当地植被的自然区域。</summary>
    public static SkillEntityAsset NatureGrowthField { get; private set; }

    /// <summary>维持六秒并周期清除地块污染的净土区域。</summary>
    public static SkillEntityAsset CleanLandField { get; private set; }

    /// <summary>在目标地块召唤一朵会按原版规则移动和降雨的雨云。</summary>
    public static SkillEntityAsset SummonRainCloud { get; private set; }

    /// <summary>把目标区域内尚未成熟的原版麦田直接催熟。</summary>
    public static SkillEntityAsset Fertilize { get; private set; }

    /// <summary>注册世界功能法术；注水和雨云仅供手动使用，其余可由组织工程按确定性需求调度。</summary>
    private static void ConfigureUtility()
    {
        ConfigureInstantUtility(
            RaiseTerrain,
            SkillModifiers.RaiseTerrain,
            SkillSemantics.Element.Earth,
            SkillSemantics.Effect.RaiseTerrain,
            SkillWorldVisualProfiles.RaiseTerrain,
            5f);
        ConfigureInstantUtility(
            LowerTerrain,
            SkillModifiers.LowerTerrain,
            SkillSemantics.Element.Earth,
            SkillSemantics.Effect.LowerTerrain,
            SkillWorldVisualProfiles.LowerTerrain,
            5f);
        ConfigureInstantUtility(
            FillWater,
            SkillModifiers.FillWater,
            SkillSemantics.Element.Water,
            SkillSemantics.Effect.FillWater,
            SkillWorldVisualProfiles.FillWater,
            4f);
        ConfigureInstantUtility(
            DrainWater,
            SkillModifiers.DrainWater,
            SkillSemantics.Element.Water,
            SkillSemantics.Effect.DrainWater,
            SkillWorldVisualProfiles.DrainWater,
            4f);

        Configure(
                NatureGrowthField,
                ElementComposition.Static.Wood,
                Anim(NatureGrowthField, 0, 0.055f, 20f),
                SkillTrajectories.FieldAtTarget,
                SkillImpactProfileLibrary.Field,
                SkillTrajectoryDomain.StationaryField,
                SkillUseProfileLibrary.WorldArea,
                SkillEntityType.Utility,
                true,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Wood,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Form.Sustain,
                SkillSemantics.Delivery.Field,
                SkillSemantics.Effect.Growth,
                SkillSemantics.Role.Utility)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(3f)
            .SetAreaCostBaseRadius(3f)
            .SetDealsBaseDamage(false)
            .TuneImpact(effectRadiusMultiplier: 3f / 2.5f, lifetimeMultiplier: 8f / 2.4f)
            .SetRuntimeLifetime(container => container.GetComponent<NatureGrowthModifier>().Duration)
            .RequireModifier(SkillModifiers.NatureGrowth)
            .SetIcon(GetIconResourcePath(NatureGrowthField))
            .SetWorldVisual(SkillWorldVisualProfiles.NatureGrowthField);

        Configure(
                CleanLandField,
                new ElementComposition(),
                Anim(CleanLandField, 0, 0.055f, 20f),
                SkillTrajectories.FieldAtTarget,
                SkillImpactProfileLibrary.Field,
                SkillTrajectoryDomain.StationaryField,
                SkillUseProfileLibrary.WorldArea,
                SkillEntityType.Utility,
                true,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Generic,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Form.Sustain,
                SkillSemantics.Delivery.Field,
                SkillSemantics.Effect.Cleanse,
                SkillSemantics.Role.Utility)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(3f)
            .SetAreaCostBaseRadius(3f)
            .SetDealsBaseDamage(false)
            .TuneImpact(effectRadiusMultiplier: 3f / 2.5f, lifetimeMultiplier: 6f / 2.4f)
            .SetRuntimeLifetime(container => container.GetComponent<CleanLandModifier>().Duration)
            .RequireModifier(SkillModifiers.CleanLand)
            .SetIcon(GetIconResourcePath(CleanLandField))
            .SetWorldVisual(SkillWorldVisualProfiles.CleanLandField);

        Configure(
                SummonRainCloud,
                ElementComposition.Static.Water,
                Anim(SummonRainCloud, 0, 20f),
                SkillTrajectories.AppearAtTarget,
                SkillImpactProfileLibrary.GroundManifest,
                SkillTrajectoryDomain.TargetManifest,
                SkillUseProfileLibrary.WorldArea,
                SkillEntityType.Utility,
                false,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Water,
                SkillSemantics.Form.Single,
                SkillSemantics.Delivery.Instant,
                SkillSemantics.Effect.Rain,
                SkillSemantics.Role.Utility)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(6f)
            .SetDealsBaseDamage(false)
            .TuneImpact(effectRadiusMultiplier: 0f)
            .RequireModifier(SkillModifiers.SummonRainCloud)
            .SetIcon("ui/icons/iconCloudRain")
            .SetWorldVisual(SkillWorldVisualProfiles.SummonRainCloud);

        Configure(
                Fertilize,
                ElementComposition.Static.WoodEarth,
                Anim(Fertilize, 0, 20f),
                SkillTrajectories.AppearAtTarget,
                SkillImpactProfileLibrary.GroundManifest,
                SkillTrajectoryDomain.TargetManifest,
                SkillUseProfileLibrary.WorldArea,
                SkillEntityType.Utility,
                false,
                VisualRotation.FixedUpright(),
                SkillSemantics.Element.Wood,
                SkillSemantics.Element.Earth,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Delivery.Instant,
                SkillSemantics.Effect.Fertilize,
                SkillSemantics.Role.Utility)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(4f)
            .SetAreaCostBaseRadius(3f)
            .SetDealsBaseDamage(false)
            .TuneImpact(effectRadiusMultiplier: 3f / 1.4f)
            .RequireModifier(SkillModifiers.Fertilize)
            .SetIcon("ui/icons/iconFertilizer")
            .SetWorldVisual(SkillWorldVisualProfiles.Fertilize);
    }

    /// <summary>以相同地表显现形态注册一种瞬时世界功能法术。</summary>
    private static void ConfigureInstantUtility(
        SkillEntityAsset asset,
        SkillModifierAsset modifier,
        SemanticAsset elementSemantic,
        SemanticAsset effectSemantic,
        Core.SkillLibV3.Visuals.SkillWorldVisualProfile visualProfile,
        float manaCost)
    {
        ElementComposition element = elementSemantic == SkillSemantics.Element.Earth
            ? ElementComposition.Static.Earth
            : ElementComposition.Static.Water;
        Configure(
                asset,
                element,
                Anim(asset, 0, 0.055f, 20f),
                SkillTrajectories.AppearAtTarget,
                SkillImpactProfileLibrary.GroundManifest,
                SkillTrajectoryDomain.TargetManifest,
                SkillUseProfileLibrary.WorldArea,
                SkillEntityType.Utility,
                false,
                VisualRotation.FixedUpright(),
                elementSemantic,
                SkillSemantics.Form.Aoe,
                SkillSemantics.Delivery.Instant,
                effectSemantic,
                SkillSemantics.Role.Utility)
            .RequireCastResource(SkillCastResources.Mana)
            .SetBaseCastDemand(manaCost)
            .SetAreaCostBaseRadius(2f)
            .SetDealsBaseDamage(false)
            .TuneImpact(effectRadiusMultiplier: 2f / 1.4f)
            .RequireModifier(modifier)
            .SetIcon(GetIconResourcePath(asset))
            .SetWorldVisual(visualProfile);
    }
}

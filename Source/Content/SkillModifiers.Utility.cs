using Cultiway.Content.Components.Skill;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

public partial class SkillModifiers
{
    private static readonly TerraformOptions CleanLandTerraform = new()
    {
        remove_fire = true,
        remove_frozen = true,
        remove_burned = true
    };

    public static SkillModifierAsset RaiseTerrain { get; private set; }
    public static SkillModifierAsset LowerTerrain { get; private set; }
    public static SkillModifierAsset FillWater { get; private set; }
    public static SkillModifierAsset DrainWater { get; private set; }
    public static SkillModifierAsset NatureGrowth { get; private set; }
    public static SkillModifierAsset CleanLand { get; private set; }

    /// <summary>注册功能法术的必选效果词条、地块预检、结算和抽象评级。</summary>
    private void ConfigureUtilityModifiers()
    {
        Setup<RaiseTerrainModifier>(RaiseTerrain, SkillModifierRarity.Common);
        SetEditorSkillIcon(RaiseTerrain, "raise_terrain");
        ConfigureFixedUtilityModifier<RaiseTerrainModifier>(
            RaiseTerrain,
            SkillSemantics.Effect.RaiseTerrain,
            "utility.raise_terrain",
            CanRaiseTerrain,
            ApplyRaiseTerrain,
            2f);

        Setup<LowerTerrainModifier>(LowerTerrain, SkillModifierRarity.Common);
        SetEditorSkillIcon(LowerTerrain, "lower_terrain");
        ConfigureFixedUtilityModifier<LowerTerrainModifier>(
            LowerTerrain,
            SkillSemantics.Effect.LowerTerrain,
            "utility.lower_terrain",
            CanLowerTerrain,
            ApplyLowerTerrain,
            2f);

        Setup<FillWaterModifier>(FillWater, SkillModifierRarity.Common);
        SetEditorSkillIcon(FillWater, "fill_water");
        ConfigureFixedUtilityModifier<FillWaterModifier>(
            FillWater,
            SkillSemantics.Effect.FillWater,
            "utility.fill_water",
            CanFillWater,
            ApplyFillWater,
            1.5f);

        Setup<DrainWaterModifier>(DrainWater, SkillModifierRarity.Common);
        SetEditorSkillIcon(DrainWater, "drain_water");
        ConfigureFixedUtilityModifier<DrainWaterModifier>(
            DrainWater,
            SkillSemantics.Effect.DrainWater,
            "utility.drain_water",
            CanDrainWater,
            ApplyDrainWater,
            1.5f);

        Setup<NatureGrowthModifier>(NatureGrowth, SkillModifierRarity.Common);
        SetEditorSkillIcon(NatureGrowth, "nature_growth_field");
        NatureGrowth.CastDemand = 0f;
        NatureGrowth.AddSemantics(
            SkillSemantics.Element.Wood,
            SkillSemantics.Effect.Growth,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Role.Utility);
        NatureGrowth.AddEffect(new SkillEffectDescriptor
        {
            Id = "utility.nature_growth",
            TargetRelation = SkillEffectTargetRelation.WorldTile,
            Trigger = SkillEffectTrigger.Periodic,
            Interval = 1f,
            CanApplyTile = CanGrowNature,
            ApplyTile = ApplyNatureGrowth
        });
        NatureGrowth.EvaluateLevel = EvaluateNatureGrowth;
        ConfigureEditor<NatureGrowthModifier>(NatureGrowth, "Utility",
            Float(nameof(NatureGrowthModifier.Duration), "Duration", 8f, 1f, 60f, 0.5f, "Seconds"));

        Setup<CleanLandModifier>(CleanLand, SkillModifierRarity.Common);
        SetEditorSkillIcon(CleanLand, "clean_land_field");
        CleanLand.CastDemand = 0f;
        CleanLand.AddSemantics(
            SkillSemantics.Effect.Cleanse,
            SkillSemantics.Form.Aoe,
            SkillSemantics.Form.Sustain,
            SkillSemantics.Role.Utility);
        CleanLand.AddEffect(new SkillEffectDescriptor
        {
            Id = "utility.clean_land",
            TargetRelation = SkillEffectTargetRelation.WorldTile,
            Trigger = SkillEffectTrigger.Periodic,
            Interval = 0.5f,
            CanApplyTile = CanCleanLand,
            ApplyTile = ApplyCleanLand
        });
        CleanLand.EvaluateLevel = EvaluateCleanLand;
        ConfigureEditor<CleanLandModifier>(CleanLand, "Utility",
            Float(nameof(CleanLandModifier.Duration), "Duration", 6f, 1f, 60f, 0.5f, "Seconds"));
    }

    /// <summary>配置没有数值编辑字段的瞬时地块功能词条。</summary>
    private static void ConfigureFixedUtilityModifier<TModifier>(
        SkillModifierAsset modifier,
        SemanticAsset effectSemantic,
        string effectId,
        SkillTileEffectApplicability applicability,
        SkillTileEffectAction apply,
        float utility)
        where TModifier : struct, IModifier
    {
        modifier.CastDemand = 0f;
        modifier.AddSemantics(effectSemantic, SkillSemantics.Form.Aoe, SkillSemantics.Role.Utility);
        modifier.AddEffect(new SkillEffectDescriptor
        {
            Id = effectId,
            TargetRelation = SkillEffectTargetRelation.WorldTile,
            Trigger = SkillEffectTrigger.Impact,
            CanApplyTile = applicability,
            ApplyTile = apply
        });
        modifier.EvaluateLevel = (Entity _, ref SkillEvaluationContext context) =>
        {
            context.MultiplyDirectPower(0f);
            context.AddUtility(utility);
        };
        ConfigureEditor<TModifier>(modifier, "Utility");
    }

    /// <summary>道路和建筑不参与永久地形抬升，其他地块必须存在原版上一级地形。</summary>
    private static bool CanRaiseTerrain(in SkillEffectEvaluationContext _, WorldTile tile)
    {
        return IsMutableTerrain(tile) && tile.main_type?.increase_to != null;
    }

    /// <summary>道路和建筑不参与永久地形降低，其他地块必须存在原版下一级地形。</summary>
    private static bool CanLowerTerrain(in SkillEffectEvaluationContext _, WorldTile tile)
    {
        return IsMutableTerrain(tile) && tile.main_type?.decrease_to != null;
    }

    /// <summary>把地块抬升一个原版层级，并把原有生物群系映射到新高度的地表。</summary>
    private static SkillEffectResult ApplyRaiseTerrain(in SkillEffectContext _, WorldTile tile)
    {
        if (tile?.main_type?.increase_to == null) return default;
        TileType targetType = tile.main_type.increase_to;
        ApplyTerrainLevel(tile, targetType);
        return tile.main_type == targetType
            ? new SkillEffectResult(SkillEffectOutcomeFlags.TerrainRaised)
            : default;
    }

    /// <summary>把地块降低一个原版层级，并把原有生物群系映射到新高度的地表。</summary>
    private static SkillEffectResult ApplyLowerTerrain(in SkillEffectContext _, WorldTile tile)
    {
        if (tile?.main_type?.decrease_to == null) return default;
        TileType targetType = tile.main_type.decrease_to;
        ApplyTerrainLevel(tile, targetType);
        return tile.main_type == targetType
            ? new SkillEffectResult(SkillEffectOutcomeFlags.TerrainLowered)
            : default;
    }

    /// <summary>按照原版地形映射把可蓄水地块填充为海水。</summary>
    private static bool CanFillWater(in SkillEffectEvaluationContext _, WorldTile tile)
    {
        return tile != null && !tile.hasBuilding() && !tile.Type.road && tile.Type.can_be_filled_with_ocean;
    }

    /// <summary>执行一次原版填水操作。</summary>
    private static SkillEffectResult ApplyFillWater(in SkillEffectContext _, WorldTile tile)
    {
        if (tile == null || !tile.Type.can_be_filled_with_ocean) return default;
        MapAction.setOcean(tile);
        return tile.Type.ocean
            ? new SkillEffectResult(SkillEffectOutcomeFlags.WaterFilled)
            : default;
    }

    /// <summary>只允许排除非熔岩的普通海水，并保护其上的建筑。</summary>
    private static bool CanDrainWater(in SkillEffectEvaluationContext _, WorldTile tile)
    {
        return tile != null && !tile.hasBuilding() && tile.Type.ocean && !tile.Type.lava;
    }

    /// <summary>通过原版排水入口转换为同层干燥地形。</summary>
    private static SkillEffectResult ApplyDrainWater(in SkillEffectContext _, WorldTile tile)
    {
        if (tile == null || !tile.Type.ocean || tile.Type.lava) return default;
        MapAction.removeLiquid(tile);
        return !tile.Type.ocean
            ? new SkillEffectResult(SkillEffectOutcomeFlags.WaterDrained)
            : default;
    }

    /// <summary>自然生长只处理无建筑、无道路、非液体且具有生物群系的地块。</summary>
    private static bool CanGrowNature(in SkillEffectEvaluationContext _, WorldTile tile)
    {
        return tile != null && !tile.hasBuilding() && !tile.Type.road && !tile.Type.liquid &&
               !tile.Type.lava && tile.getBiome() != null;
    }

    /// <summary>调用原版植被选择器尝试生成当地树木、植物或灌木。</summary>
    private static SkillEffectResult ApplyNatureGrowth(in SkillEffectContext _, WorldTile tile)
    {
        BiomeAsset biome = tile?.getBiome();
        if (biome == null || tile.hasBuilding()) return default;
        ActionLibrary.growRandomVegetation(tile, biome);
        return tile.hasBuilding()
            ? new SkillEffectResult(SkillEffectOutcomeFlags.FloraCreated)
            : default;
    }

    /// <summary>检查地块是否有净土法术能够移除且不涉及道路、建筑或熔岩的污染。</summary>
    private static bool CanCleanLand(in SkillEffectEvaluationContext _, WorldTile tile)
    {
        if (tile == null || tile.Type.lava) return false;
        bool transientPollution = tile.isOnFire() || tile.burned_stages > 0 ||
                                  tile.isTemporaryFrozen() || tile.heat > 0;
        bool removableWasteland = !tile.hasBuilding() && !tile.Type.road &&
                                  tile.top_type?.wasteland == true;
        return transientPollution || removableWasteland;
    }

    /// <summary>清除瞬时污染，并仅在不会破坏道路和建筑时移除荒地表层。</summary>
    private static SkillEffectResult ApplyCleanLand(in SkillEffectContext _, WorldTile tile)
    {
        if (tile == null || tile.Type.lava) return default;
        bool hadFire = tile.isOnFire();
        bool hadBurn = tile.burned_stages > 0;
        bool hadFrozen = tile.isTemporaryFrozen();
        bool hadHeat = tile.heat > 0;
        bool hadWasteland = !tile.hasBuilding() && !tile.Type.road && tile.top_type?.wasteland == true;
        MapAction.terraformTile(tile, tile.main_type, tile.top_type, CleanLandTerraform);
        tile.heat = 0;
        if (hadWasteland && tile.top_type?.wasteland == true)
        {
            MapAction.terraformTop(tile, null, TerraformLibrary.flash);
        }

        SkillEffectOutcomeFlags flags = SkillEffectOutcomeFlags.None;
        int count = 0;
        AddRemovedFlag(hadFire && !tile.isOnFire(), SkillEffectOutcomeFlags.FireRemoved, ref flags, ref count);
        AddRemovedFlag(hadBurn && tile.burned_stages <= 0, SkillEffectOutcomeFlags.BurnRemoved, ref flags, ref count);
        AddRemovedFlag(hadFrozen && !tile.isTemporaryFrozen(), SkillEffectOutcomeFlags.FrozenRemoved,
            ref flags, ref count);
        AddRemovedFlag(hadHeat && tile.heat <= 0, SkillEffectOutcomeFlags.HeatRemoved, ref flags, ref count);
        AddRemovedFlag(hadWasteland && tile.top_type?.wasteland != true,
            SkillEffectOutcomeFlags.WastelandRemoved, ref flags, ref count);
        return count > 0 ? new SkillEffectResult(flags, count) : default;
    }

    /// <summary>把一个已经确认被移除的地块状态写入聚合结果。</summary>
    private static void AddRemovedFlag(
        bool removed,
        SkillEffectOutcomeFlags flag,
        ref SkillEffectOutcomeFlags flags,
        ref int count)
    {
        if (!removed) return;
        flags |= flag;
        count++;
    }

    /// <summary>判断地块能否在不破坏道路和建筑的情况下调整主地形层。</summary>
    private static bool IsMutableTerrain(WorldTile tile)
    {
        return tile != null && !tile.hasBuilding() && !tile.Type.road && !tile.Type.lava;
    }

    /// <summary>改变主地形层，同时保留并重新映射原有生物群系外观。</summary>
    private static void ApplyTerrainLevel(WorldTile tile, TileType targetType)
    {
        BiomeAsset biome = tile.getBiome();
        TopTileType targetTop = tile.top_type == null || biome == null
            ? null
            : targetType.rank_type switch
            {
                TileRank.Low => biome.getTileLow(),
                TileRank.High => biome.getTileHigh(),
                _ => null
            };
        MapAction.terraformTile(tile, targetType, targetTop, TerraformLibrary.flash);
    }

    /// <summary>按持续时间和触发频率评价自然生长的功能强度。</summary>
    private static void EvaluateNatureGrowth(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        NatureGrowthModifier modifier = container.GetComponent<NatureGrowthModifier>();
        context.AddUtility(modifier.Duration * 0.2f);
    }

    /// <summary>按持续时间和触发频率评价净土区域的功能强度。</summary>
    private static void EvaluateCleanLand(Entity container, ref SkillEvaluationContext context)
    {
        context.MultiplyDirectPower(0f);
        CleanLandModifier modifier = container.GetComponent<CleanLandModifier>();
        context.AddUtility(modifier.Duration / 0.5f * 0.15f);
    }
}

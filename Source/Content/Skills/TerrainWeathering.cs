using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>有限地块接触推进原对象的枯萎或风化，世界本身照常运行。</summary>
internal static class TerrainWeathering
{
    /// <summary>可组合的植被枯萎、复生与石造建筑风化入口。</summary>
    internal const string WitherAction = "nature.wither", RenewAction = "nature.renew", StoneAction = "building.weather";

    /// <summary>只检查实际根部上的健康植被，农田沿原有作物规则处理。</summary>
    /// <param name="tile">被作用面实际覆盖的地块。</param>
    internal static bool CanWither(WorldTile tile)
    {
        Building plant = tile?.building;
        return plant != null && plant.current_tile == tile && plant.isUsable() && !plant.chopped &&
               plant.asset.flora && !plant.asset.wheat && plant.hasHealth();
    }

    /// <summary>每次扣除最多半份原生命，枯死后交给原版移除并腾出真实根部地块。</summary>
    /// <param name="context">已确认接触的地块、来源和世界时点。</param>
    internal static SkillEffectResult Wither(ConstructionWorldContext context)
    {
        Building plant = context.Tile.building;
        int before = plant.getHealth();
        plant.changeHealth(-HalfHealth(plant));
        int lost = before - plant.getHealth();
        if (lost <= 0) return SkillEffectResult.None;
        if (!plant.hasHealth()) plant.startDestroyBuilding();
        return new SkillEffectResult(SkillEffectOutcomeFlags.FloraWithered, magnitude: lost);
    }

    /// <summary>空地仍须满足原生植被的地形和群系条件。</summary>
    /// <param name="tile">已确认接触的地块。</param>
    internal static bool CanRenew(WorldTile tile)
        => tile != null && !tile.hasBuilding() && !tile.Type.road && !tile.Type.liquid && !tile.Type.lava &&
           tile.top_type != TopTileLibrary.field && tile.getBiome()?.grow_vegetation_auto == true;

    /// <summary>跳过等候自然发芽的随机时机，沿用原种群选择、占地和生长数量限制。</summary>
    /// <param name="context">已经确认的空地。</param>
    internal static SkillEffectResult Renew(ConstructionWorldContext context)
    {
        WorldTile tile = context.Tile;
        BuildingActions.tryGrowVegetationRandom(tile, VegetationType.Trees, pCheckRandom: false);
        if (!tile.hasBuilding()) BuildingActions.tryGrowVegetationRandom(tile, VegetationType.Plants, pCheckRandom: false);
        if (!tile.hasBuilding()) BuildingActions.tryGrowVegetationRandom(tile, VegetationType.Bushes, pCheckRandom: false);
        return tile.hasBuilding() ? new SkillEffectResult(SkillEffectOutcomeFlags.FloraCreated) : SkillEffectResult.None;
    }

    /// <summary>只推进已建成的石造建筑或其原废墟，副占用格不重复结算。</summary>
    /// <param name="tile">已确认接触的地块。</param>
    internal static bool CanWeatherStone(WorldTile tile)
    {
        Building building = tile?.building;
        return building != null && building.current_tile == tile && building.isAlive() && building.hasHealth() &&
               building.isAnimationState(BuildingAnimationState.Normal) && !building.isUnderConstruction() &&
               building.asset.city_building && building.asset.has_ruin_state && !building.asset.burnable &&
               building.asset.cost?.stone > 0;
    }

    /// <summary>沿原版毁坏流程进入废墟。废墟再失去生命后，只在释放的原址留下实际沙地。</summary>
    /// <param name="context">确实发生的世界接触。</param>
    internal static SkillEffectResult WeatherStone(ConstructionWorldContext context)
    {
        WorldTile tile = context.Tile;
        Building building = tile.building;
        bool ruin = building.isRuin();
        int before = building.getHealth();
        int damage = Mathf.Min(before, HalfHealth(building));
        building.getHit(damage, pFlash: false, pSkipIfShake: false);
        bool removed = ruin && building.isOnRemove() && !tile.hasBuilding();
        bool becameRuin = !ruin && building.isRuin();
        int lost = removed || becameRuin ? damage : before - building.getHealth();
        if (lost <= 0) return SkillEffectResult.None;

        var flags = SkillEffectOutcomeFlags.StoneWeathered;
        if (removed && tile.main_type == TileLibrary.soil_low && !tile.Type.road && tile.top_type != TopTileLibrary.field)
        {
            tile.setTileTypes(TileLibrary.sand, null, true);
            if (tile.main_type == TileLibrary.sand) flags |= SkillEffectOutcomeFlags.WeatheredSand;
        }
        return new SkillEffectResult(flags, magnitude: lost);
    }

    /// <summary>按原对象生命上限计算单次损耗，奇数生命也能在两次内耗尽。</summary>
    /// <param name="building">参与本次世界接触的原对象。</param>
    private static int HalfHealth(Building building) => Mathf.Max(1, Mathf.CeilToInt(building.getMaxHealth() * .5f));
}

using Cultiway.Core.Skills;

namespace Cultiway.Content;

/// <summary>新构造与原有世界法术共用原版地块操作，迁移期间不复制玩法结果。</summary>
public partial class SkillModifiers
{
    /// <summary>登记可自由组合的原理，旧入口与新接触仍调用同一实际实现。</summary>
    internal static void RegisterConstructionWorldEffects()
    {
        ConstructionWorldActions.Register(new ConstructionWorldAction("nature.grow",
            tile => CanGrowNature(default, tile), tile => ApplyNatureGrowth(default, tile)));
        ConstructionWorldActions.Register(new ConstructionWorldAction(Skills.LivingPlantRestoration.ActionId,
            Skills.LivingPlantRestoration.CanApply, Skills.LivingPlantRestoration.Apply, 0f));
        ConstructionWorldActions.Register(new ConstructionWorldAction("land.cleanse",
            tile => CanCleanLand(default, tile), tile => ApplyCleanLand(default, tile)));
        ConstructionWorldActions.Register(new ConstructionWorldAction("crop.mature",
            tile => CanFertilizeCrop(default, tile), tile => ApplyFertilizeCrop(default, tile)));
        ConstructionWorldActions.Register(new ConstructionWorldAction("terrain.raise",
            tile => CanRaiseTerrain(default, tile), tile => ApplyRaiseTerrain(default, tile)));
        ConstructionWorldActions.Register(new ConstructionWorldAction("terrain.lower",
            tile => CanLowerTerrain(default, tile), tile => ApplyLowerTerrain(default, tile)));
        ConstructionWorldActions.Register(new ConstructionWorldAction("water.fill",
            tile => CanFillWater(default, tile), tile => ApplyFillWater(default, tile)));
        ConstructionWorldActions.Register(new ConstructionWorldAction("weather.rain",
            tile => CanSummonRainCloud(default, tile),
            context => Skills.ConstructedRainClouds.Spawn(context.Tile, context.Duration, context.SourceId,
                context.AbilityId, context.WorldTime), 12f));
        ConstructionWorldActions.Register(new ConstructionWorldAction("water.drain",
            tile => CanDrainWater(default, tile), tile => ApplyDrainWater(default, tile)));
    }
}
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;

namespace Cultiway.Content.Visuals;

/// <summary>原技能的范围成长与专用材质限制。</summary>
internal static class OriginalSkillGrowthRules
{
    internal static readonly HashSet<string> ScaledSkills = new(System.StringComparer.Ordinal);

    /// <summary>内容库完成配置后逐项登记，所有旧变体编号及计时保留。</summary>
    internal static void Register()
    {
        Add(SkillEntities.RockProjectile, SkillEntities.FallingRock);
        Add(SkillEntities.StoneCone, SkillEntities.IceShard, SkillEntities.IceLance, SkillEntities.Icefall);
        Add(SkillEntities.GoldenNeedle, SkillEntities.PoisonNeedle, SkillEntities.WaterArrow, SkillEntities.LightningSpear);
        Add(SkillEntities.FlyingSword);
        Add(SkillEntities.SwordQi, SkillEntities.FireBlade, SkillEntities.WaterBlade, SkillEntities.WindBlade, SkillEntities.YinBlade);
        Add(SkillEntities.Fireball, SkillEntities.FlameMeteor, SkillEntities.YangBolt);
        Add(SkillEntities.WaterOrb);
        Add(SkillEntities.WindBullet);
        Add(SkillEntities.SpiritBolt, SkillEntities.YinBolt, SkillEntities.ChaosOrb, SkillEntities.ThunderOrb);
        Add(SkillEntities.SpiritBeam, SkillEntities.SolarBeam);
        Add(SkillEntities.ChainLightning);
        Add(SkillEntities.TidalWave, SkillEntities.EarthRidge);
        Add(SkillEntities.SpiritVine);
        Add(SkillEntities.WoodSpear, SkillEntities.FallingTimber);
        Add(SkillEntities.Tornado);
        Add(SkillEntities.PoisonMist, SkillEntities.PoisonPool, SkillEntities.YinField, SkillEntities.EntropyField,
            SkillEntities.RejuvenationField, SkillEntities.NatureGrowthField, SkillEntities.CleanLandField);
        Add(SkillEntities.EarthWall, SkillEntities.WoodWall, SkillEntities.FireWall, SkillEntities.IceWall,
            SkillEntities.WaterCurtain, SkillEntities.SwordCurtain, SkillEntities.WindWall);
        Add(SkillEntities.SpiritShield, SkillEntities.FlameShield, SkillEntities.LightningShield, SkillEntities.WindShield, SkillEntities.YangShield);
        Add(SkillEntities.Rift);
        Add(SkillEntities.HealingLight, SkillEntities.BattleBlessing, SkillEntities.GuardBlessing, SkillEntities.HasteBlessing);
        RestrictWorldChange(SkillEntities.PurificationWave, SkillEntities.RaiseTerrain, SkillEntities.LowerTerrain,
            SkillEntities.FillWater, SkillEntities.DrainWater, SkillEntities.SummonRainCloud, SkillEntities.Fertilize);
        // 专门制作的地形与屏障暂不靠换颜色冒充另一种材质。
        RestrictNature("earth", SkillEntities.RockProjectile, SkillEntities.FallingRock, SkillEntities.StoneCone,
            SkillEntities.EarthRidge, SkillEntities.GroundSpike, SkillEntities.EarthWall);
        RestrictNature("wood", SkillEntities.SpiritVine, SkillEntities.WoodSpear, SkillEntities.FallingTimber, SkillEntities.WoodWall);
        RestrictNature("water", SkillEntities.TidalWave, SkillEntities.WaterCurtain);
        RestrictNature("ice", SkillEntities.IceShard, SkillEntities.IceLance, SkillEntities.Icefall, SkillEntities.IceWall);
        RestrictNature("wind", SkillEntities.Tornado, SkillEntities.WindWall);
        RestrictNature("fire", SkillEntities.FireWall);
        RestrictNature("metal", SkillEntities.SwordCurtain);
        RestrictNature("chaos", SkillEntities.Rift);
        RestrictNature("lightning", SkillEntities.ChainLightning, SkillEntities.LightningStrike);
    }

    /// <summary>登记已经制作的专用材质，后续扩展可追加允许的性质编号。</summary>
    /// <param name="nature">与主体相符的性质。</param>
    /// <param name="assets">专用主体。</param>
    private static void RestrictNature(string nature, params SkillEntityAsset[] assets)
    {
        foreach (var asset in assets) asset.SupportedNatureIds = new HashSet<string>(System.StringComparer.Ordinal) { nature };
    }

    internal static void Add(params SkillEntityAsset[] assets)
    {
        foreach (SkillEntityAsset asset in assets)
            ScaledSkills.Add(asset.id);
    }

    private static void RestrictWorldChange(params SkillEntityAsset[] assets)
    {
        Add(assets);
        foreach (SkillEntityAsset asset in assets)
            asset.SupportedNatureIds = new HashSet<string>(System.StringComparer.Ordinal);
    }
}

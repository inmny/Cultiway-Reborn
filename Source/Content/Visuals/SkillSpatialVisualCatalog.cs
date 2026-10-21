using System.Collections.Generic;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.Components;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>实际法术的空间形式；元素颜色不能替代形式差别。</summary>
internal enum SkillSpatialForm
{
    /// <summary>飞行或下落的硬石。</summary>
    Rock,
    /// <summary>有尖端的棱体。</summary>
    Crystal,
    /// <summary>狭长针体。</summary>
    Needle,
    /// <summary>保留本体的飞剑。</summary>
    Sword,
    /// <summary>有刃缘的宽面。</summary>
    Blade,
    /// <summary>有流动火舌的火核。</summary>
    Fire,
    /// <summary>由流线包裹的水核。</summary>
    Water,
    /// <summary>有明确方向的风压。</summary>
    Wind,
    /// <summary>灵力团与阴阳团。</summary>
    Spirit,
    /// <summary>固定两端或跟随目标的通道。</summary>
    Beam,
    /// <summary>实际结算顺序建立的连锁。</summary>
    Chain,
    /// <summary>沿地面推进的宽前缘。</summary>
    Wave,
    /// <summary>贴地生长、缠绕的藤蔓。</summary>
    Vine,
    /// <summary>附着目标的毒种开绽，与沿地推进的藤蔓分开。</summary>
    PoisonBloom,
    /// <summary>保留轴向的木体。</summary>
    Timber,
    /// <summary>逐层旋转的风柱。</summary>
    Tornado,
    /// <summary>持续范围及内部稀疏流动。</summary>
    Field,
    /// <summary>有方向与厚度的屏障。</summary>
    Wall,
    /// <summary>围绕实际施法载体的护持。</summary>
    Shield,
    /// <summary>局部空间裂隙。</summary>
    Rift,
    /// <summary>向受益目标汇聚的辅助流动。</summary>
    Support,
    /// <summary>在实际接触点短促展开的冲击。</summary>
    Contact,
    /// <summary>由实际施法者外观构成的短时幻影。</summary>
    Image,
    /// <summary>沿用真实地形、云与作物变化，成功反馈另由结果事件生成。</summary>
    WorldChange
}

/// <summary>基础实体、法宝借用实体和器官借用实体共同使用的登记表。</summary>
internal static class SkillSpatialVisualCatalog
{
    /// <summary>按实际资产标识查找，不按名字猜测或退回通用光球。</summary>
    internal static readonly Dictionary<string, SkillSpatialForm> Forms = new(System.StringComparer.Ordinal);

    /// <summary>内容库完成配置后逐项登记，所有旧变体编号及计时保留。</summary>
    internal static void Register()
    {
        Add(SkillSpatialForm.Rock, SkillEntities.RockProjectile, SkillEntities.FallingRock);
        Add(SkillSpatialForm.Crystal, SkillEntities.StoneCone, SkillEntities.IceShard, SkillEntities.IceLance, SkillEntities.Icefall);
        Add(SkillSpatialForm.Needle, SkillEntities.GoldenNeedle, SkillEntities.PoisonNeedle, SkillEntities.WaterArrow, SkillEntities.LightningSpear);
        Add(SkillSpatialForm.Sword, SkillEntities.FlyingSword);
        Add(SkillSpatialForm.Blade, SkillEntities.SwordQi, SkillEntities.FireBlade, SkillEntities.WaterBlade, SkillEntities.WindBlade, SkillEntities.YinBlade);
        Add(SkillSpatialForm.Fire, SkillEntities.Fireball, SkillEntities.FlameMeteor, SkillEntities.YangBolt);
        Add(SkillSpatialForm.Water, SkillEntities.WaterOrb);
        Add(SkillSpatialForm.Wind, SkillEntities.WindBullet, SkillEntities.BalefulWindTribulationWave);
        Add(SkillSpatialForm.Spirit, SkillEntities.SpiritBolt, SkillEntities.YinBolt, SkillEntities.ChaosOrb, SkillEntities.ThunderOrb);
        Add(SkillSpatialForm.Beam, SkillEntities.SpiritBeam, SkillEntities.SolarBeam);
        Add(SkillSpatialForm.Chain, SkillEntities.ChainLightning);
        Add(SkillSpatialForm.Wave, SkillEntities.TidalWave, SkillEntities.EarthRidge);
        Add(SkillSpatialForm.Vine, SkillEntities.SpiritVine);
        Add(SkillSpatialForm.Timber, SkillEntities.WoodSpear, SkillEntities.FallingTimber);
        Add(SkillSpatialForm.Tornado, SkillEntities.Tornado, SkillEntities.BalefulWindTribulationCenter);
        Add(SkillSpatialForm.Field, SkillEntities.PoisonMist, SkillEntities.PoisonPool, SkillEntities.YinField, SkillEntities.EntropyField,
            SkillEntities.RejuvenationField, SkillEntities.NatureGrowthField, SkillEntities.CleanLandField);
        Add(SkillSpatialForm.Wall, SkillEntities.EarthWall, SkillEntities.WoodWall, SkillEntities.FireWall, SkillEntities.IceWall,
            SkillEntities.WaterCurtain, SkillEntities.SwordCurtain, SkillEntities.WindWall);
        Add(SkillSpatialForm.Shield, SkillEntities.SpiritShield, SkillEntities.FlameShield, SkillEntities.LightningShield, SkillEntities.WindShield, SkillEntities.YangShield);
        Add(SkillSpatialForm.Rift, SkillEntities.Rift);
        Add(SkillSpatialForm.Support, SkillEntities.HealingLight, SkillEntities.BattleBlessing, SkillEntities.GuardBlessing, SkillEntities.HasteBlessing);
        Add(SkillSpatialForm.WorldChange, SkillEntities.PurificationWave, SkillEntities.RaiseTerrain, SkillEntities.LowerTerrain,
            SkillEntities.FillWater, SkillEntities.DrainWater, SkillEntities.SummonRainCloud, SkillEntities.Fertilize);
        // 这两类有独立制作的空间主体，其余流程仍走同一个技能生命周期。
        AllVariants(SkillEntities.GroundSpike);
        AllVariants(SkillEntities.LightningStrike);
        // 专门制作的地形与屏障暂不靠换颜色冒充另一种材质。
        RestrictNature("earth", SkillEntities.RockProjectile, SkillEntities.FallingRock, SkillEntities.StoneCone,
            SkillEntities.EarthRidge, SkillEntities.GroundSpike, SkillEntities.EarthWall);
        RestrictNature("wood", SkillEntities.SpiritVine, SkillEntities.WoodSpear, SkillEntities.FallingTimber, SkillEntities.WoodWall);
        RestrictNature("water", SkillEntities.TidalWave, SkillEntities.WaterCurtain);
        RestrictNature("ice", SkillEntities.IceShard, SkillEntities.IceLance, SkillEntities.Icefall, SkillEntities.IceWall);
        RestrictNature("wind", SkillEntities.Tornado, SkillEntities.WindWall, SkillEntities.BalefulWindTribulationCenter);
        RestrictNature("fire", SkillEntities.FireWall);
        RestrictNature("metal", SkillEntities.SwordCurtain);
        RestrictNature("chaos", SkillEntities.Rift);
        RestrictNature("lightning", SkillEntities.ChainLightning, SkillEntities.LightningStrike);
        foreach (var pair in Forms)
            if (pair.Value == SkillSpatialForm.WorldChange)
                ModClass.I.SkillV3.SkillLib.get(pair.Key).SupportedNatureIds = new HashSet<string>(System.StringComparer.Ordinal);

    }

    /// <summary>登记已经制作的专用材质，后续扩展可追加允许的性质编号。</summary>
    /// <param name="nature">与主体相符的性质。</param>
    /// <param name="assets">专用主体。</param>
    private static void RestrictNature(string nature, params SkillEntityAsset[] assets)
    {
        foreach (var asset in assets) asset.SupportedNatureIds = new HashSet<string>(System.StringComparer.Ordinal) { nature };
    }

    /// <summary>登记实际形式并启用已有全部变体。</summary>
    /// <param name="form">实际空间形式。</param>
    /// <param name="assets">采用该形式的现有能力。</param>
    internal static void Add(SkillSpatialForm form, params SkillEntityAsset[] assets)
    {
        foreach (SkillEntityAsset asset in assets)
        {
            Forms[asset.id] = form; AllVariants(asset);

        }
    }

    /// <summary>只改绘制标记，不修改旧编号、阶段和玩法参数。</summary>
    /// <param name="asset">已完成配置的技能。</param>
    private static void AllVariants(SkillEntityAsset asset)
    {
        for (int i = 0; i < asset.Animations.Count; i++) asset.UseSpatialModel(i);
    }
}

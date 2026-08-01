using Cultiway.Core.SkillLibV3.Visuals;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>辅助与功能法术使用的声明式世界视觉配置。</summary>
internal static class SkillWorldVisualProfiles
{
    private const string PrimitiveRoot = "cultiway/effect/world_primitives";

    /// <summary>即时治疗的局部生命反馈。</summary>
    public static SkillWorldVisualProfile Healing { get; } = Local(
        SkillLocalEffectVisualKind.Healing,
        Color32(82, 222, 151),
        Color32(39, 142, 101),
        Color32(222, 255, 236));

    /// <summary>回春法阵及其沿种籽环传播的恢复脉冲。</summary>
    public static SkillWorldVisualProfile RejuvenationField { get; } = new()
    {
        Field = new SkillFieldVisualProfile
        {
            BoundaryColor = Color32(79, 211, 150, 205),
            PrimaryRing = Ring(
                new[] { $"{PrimitiveRoot}/seed_flow_a", $"{PrimitiveRoot}/seed_flow_b" },
                8,
                0.78f,
                -6f,
                Color32(61, 191, 129)),
            InscriptionRing = Ring(
                new[]
                {
                    $"{PrimitiveRoot}/inscription_sheng",
                    $"{PrimitiveRoot}/inscription_xi",
                    $"{PrimitiveRoot}/inscription_fu",
                    $"{PrimitiveRoot}/inscription_yuan"
                },
                4,
                0.58f,
                3f,
                Color32(188, 250, 218),
                0.12f,
                0.24f,
                0.36f)
        },
        LocalEffect = SkillLocalEffectVisualKind.Rejuvenation,
        PrimaryColor = Color32(73, 205, 142),
        SecondaryColor = Color32(30, 125, 87),
        GlowColor = Color32(220, 255, 235)
    };

    /// <summary>两层分段净化波和按距离延迟的目标反馈。</summary>
    public static SkillWorldVisualProfile PurificationWave { get; } = new()
    {
        AreaImpact = SkillAreaImpactVisualKind.PurificationWave,
        LocalEffect = SkillLocalEffectVisualKind.Purification,
        PrimaryColor = Color32(108, 224, 238),
        SecondaryColor = Color32(51, 145, 174),
        GlowColor = Color32(235, 255, 255)
    };

    /// <summary>战意祝福的火星反馈。</summary>
    public static SkillWorldVisualProfile BattleBlessing { get; } = Local(
        SkillLocalEffectVisualKind.BattleBlessing,
        Color32(239, 83, 51),
        Color32(153, 42, 35),
        Color32(255, 198, 83));

    /// <summary>守护祝福的金属环与菱片反馈。</summary>
    public static SkillWorldVisualProfile GuardBlessing { get; } = Local(
        SkillLocalEffectVisualKind.GuardBlessing,
        Color32(218, 184, 92),
        Color32(91, 113, 124),
        Color32(244, 239, 196));

    /// <summary>迅捷祝福的青色流线反馈。</summary>
    public static SkillWorldVisualProfile HasteBlessing { get; } = Local(
        SkillLocalEffectVisualKind.HasteBlessing,
        Color32(72, 211, 221),
        Color32(44, 118, 155),
        Color32(218, 255, 250));

    /// <summary>抬升地形时的尘土与碎石反馈。</summary>
    public static SkillWorldVisualProfile RaiseTerrain { get; } = Local(
        SkillLocalEffectVisualKind.RaiseTerrain,
        Color32(171, 126, 70),
        Color32(105, 79, 53),
        Color32(231, 191, 112));

    /// <summary>降低地形时的收拢尘土与碎石反馈。</summary>
    public static SkillWorldVisualProfile LowerTerrain { get; } = Local(
        SkillLocalEffectVisualKind.LowerTerrain,
        Color32(124, 91, 62),
        Color32(78, 61, 52),
        Color32(191, 158, 108));

    /// <summary>填水时的扩散水纹与气泡反馈。</summary>
    public static SkillWorldVisualProfile FillWater { get; } = Local(
        SkillLocalEffectVisualKind.FillWater,
        Color32(55, 169, 224),
        Color32(29, 96, 157),
        Color32(183, 239, 255));

    /// <summary>排水时的收缩漩涡与气泡反馈。</summary>
    public static SkillWorldVisualProfile DrainWater { get; } = Local(
        SkillLocalEffectVisualKind.DrainWater,
        Color32(42, 132, 191),
        Color32(23, 68, 124),
        Color32(151, 223, 247));

    /// <summary>自然生长法阵、枝蔓环、根纹环与萌芽反馈。</summary>
    public static SkillWorldVisualProfile NatureGrowthField { get; } = new()
    {
        Field = new SkillFieldVisualProfile
        {
            BoundaryColor = Color32(92, 181, 76, 205),
            InscriptionMinRadius = 2.8f,
            PrimaryRing = Ring(
                new[] { $"{PrimitiveRoot}/branch_vine_a", $"{PrimitiveRoot}/branch_vine_b" },
                10,
                0.8f,
                4f,
                Color32(80, 182, 70)),
            SecondaryRing = Ring(
                new[] { $"{PrimitiveRoot}/root_y" },
                5,
                0.55f,
                -2f,
                Color32(54, 115, 56),
                0.14f,
                0.25f,
                0.42f),
            InscriptionRing = Ring(
                new[]
                {
                    $"{PrimitiveRoot}/inscription_mu",
                    $"{PrimitiveRoot}/inscription_sheng",
                    $"{PrimitiveRoot}/inscription_fan",
                    $"{PrimitiveRoot}/inscription_rong"
                },
                4,
                0.67f,
                -2f,
                Color32(192, 238, 147),
                0.10f,
                0.22f,
                0.32f)
        },
        LocalEffect = SkillLocalEffectVisualKind.NatureGrowth,
        PrimaryColor = Color32(77, 181, 69),
        SecondaryColor = Color32(77, 106, 48),
        GlowColor = Color32(217, 250, 158)
    };

    /// <summary>净土法阵、过滤楔环、题字环与按污染类别区分的局部反馈。</summary>
    public static SkillWorldVisualProfile CleanLandField { get; } = new()
    {
        Field = new SkillFieldVisualProfile
        {
            BoundaryColor = Color32(189, 220, 216, 205),
            PrimaryRing = Ring(
                new[] { $"{PrimitiveRoot}/filter_wedge" },
                8,
                0.78f,
                -8f,
                Color32(151, 198, 194)),
            InscriptionRing = Ring(
                new[]
                {
                    $"{PrimitiveRoot}/inscription_jing",
                    $"{PrimitiveRoot}/inscription_chen",
                    $"{PrimitiveRoot}/inscription_di",
                    $"{PrimitiveRoot}/inscription_hui"
                },
                4,
                0.56f,
                4f,
                Color32(225, 244, 239),
                0.12f,
                0.24f,
                0.36f)
        },
        LocalEffect = SkillLocalEffectVisualKind.CleanLand,
        PrimaryColor = Color32(150, 205, 210),
        SecondaryColor = Color32(113, 92, 126),
        GlowColor = Color32(239, 255, 250)
    };

    /// <summary>召云本身直接由原版云实体表现，不叠加地面占位动画。</summary>
    public static SkillWorldVisualProfile SummonRainCloud { get; } = new()
    {
        PrimaryColor = Color32(104, 174, 226),
        SecondaryColor = Color32(72, 103, 137),
        GlowColor = Color32(210, 236, 250)
    };

    /// <summary>施肥成功时使用原版肥料颗粒和低矮生长脉冲。</summary>
    public static SkillWorldVisualProfile Fertilize { get; } = Local(
        SkillLocalEffectVisualKind.Fertilize,
        Color32(132, 183, 83),
        Color32(116, 78, 43),
        Color32(217, 232, 126));

    /// <summary>创建一个只包含局部成功反馈的视觉配置。</summary>
    private static SkillWorldVisualProfile Local(
        SkillLocalEffectVisualKind kind,
        Color primary,
        Color secondary,
        Color glow)
    {
        return new SkillWorldVisualProfile
        {
            LocalEffect = kind,
            PrimaryColor = primary,
            SecondaryColor = secondary,
            GlowColor = glow
        };
    }

    /// <summary>创建一个环形图元配置。</summary>
    private static SkillGlyphRingVisualProfile Ring(
        string[] glyphPaths,
        int count,
        float radiusRatio,
        float rotationSpeed,
        Color color,
        float sizeRadiusFactor = 0.18f,
        float minWorldSize = 0.32f,
        float maxWorldSize = 0.55f)
    {
        return new SkillGlyphRingVisualProfile
        {
            GlyphPaths = glyphPaths,
            Count = count,
            RadiusRatio = radiusRatio,
            RotationSpeed = rotationSpeed,
            Color = color,
            SizeRadiusFactor = sizeRadiusFactor,
            MinWorldSize = minWorldSize,
            MaxWorldSize = maxWorldSize
        };
    }

    /// <summary>以字节颜色构造 Unity 颜色，避免配置中散落难读的小数。</summary>
    private static Color Color32(byte r, byte g, byte b, byte a = 255)
    {
        return new UnityEngine.Color32(r, g, b, a);
    }
}

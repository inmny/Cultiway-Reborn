using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class ArtifactAtoms
{
    public static ArtifactAtomAsset CloudPattern { get; private set; }
    public static ArtifactAtomAsset ThunderPattern { get; private set; }
    public static ArtifactAtomAsset MountainPattern { get; private set; }
    public static ArtifactAtomAsset WaterMoonPattern { get; private set; }
    public static ArtifactAtomAsset LifePattern { get; private set; }
    public static ArtifactAtomAsset SpiritGatheringPattern { get; private set; }
    public static ArtifactAtomAsset SealingRunes { get; private set; }
    public static ArtifactAtomAsset PureYang { get; private set; }
    public static ArtifactAtomAsset ProfoundYin { get; private set; }
    public static ArtifactAtomAsset VoidMark { get; private set; }

    private static void ConfigureAdditionalFinishAtoms()
    {
        Set(CloudPattern, "cloud_pattern", ArtifactAtomCategory.Finish, ["云纹", "流霞", "御风"],
            ["robe_panel3d.wide_blue", "sword_guard3d.wing"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Feather, ItemShapes.Wing, ItemShapes.Silk, ItemShapes.Flower) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.55f),
            [
                Trait(ArtifactMaterialTraits.Mobility, 0.55f),
                Trait(ArtifactMaterialTraits.Flexibility, 0.3f),
                Trait(ArtifactMaterialTraits.Concealment, 0.3f),
            ]);

        Set(ThunderPattern, "thunder_pattern", ArtifactAtomCategory.Finish, ["雷纹", "霆痕", "电芒"],
            ["sword_blade3d.crystal", "ding3d.blue_fire"], ["cold_crystal", "black_gold"],
            r => Semantic(r, ArtifactMaterialTraits.Pos, 1.2f) +
                 Semantic(r, ArtifactMaterialTraits.Fire, 0.5f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Pos, 0.3f),
                Trait(ArtifactMaterialTraits.Mobility, 0.45f),
                Trait(ArtifactMaterialTraits.Volatility, 0.35f),
                Trait(ArtifactMaterialTraits.Amplification, 0.45f),
            ]);

        Set(MountainPattern, "mountain_pattern", ArtifactAtomCategory.Finish, ["岳纹", "山河", "坤脉"],
            ["seal_mountain3d.amber", "ding3d.copper_ember"], ["copper_ember", "black_gold"],
            r => Shape(r, ItemShapes.Stone, ItemShapes.Shell, ItemShapes.Root) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 1.15f),
            [
                Trait(ArtifactMaterialTraits.Earth, 0.3f),
                Trait(ArtifactMaterialTraits.Hardness, 0.45f),
                Trait(ArtifactMaterialTraits.Suppression, 0.4f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.35f),
                Trait(ArtifactMaterialTraits.Stability, 0.3f),
            ]);

        Set(WaterMoonPattern, "water_moon_pattern", ArtifactAtomCategory.Finish, ["水月", "镜花", "清辉"],
            ["mirror3d.jade_hex", "robe_panel3d.wide_blue"], ["cold_crystal"],
            r => Shape(r, ItemShapes.Liquid, ItemShapes.Lotus, ItemShapes.Eye) +
                 Semantic(r, ArtifactMaterialTraits.Water, 1f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.65f),
            [
                Trait(ArtifactMaterialTraits.Reflection, 0.45f),
                Trait(ArtifactMaterialTraits.Perception, 0.4f),
                Trait(ArtifactMaterialTraits.Insight, 0.4f),
                Trait(ArtifactMaterialTraits.Purification, 0.3f),
            ]);

        Set(LifePattern, "life_pattern", ArtifactAtomCategory.Finish, ["生纹", "回春", "不息"],
            ["robe_panel3d.split_green", "ding3d.copper_ember"], ["gold_jade", "copper_ember"],
            r => Shape(r, ItemShapes.Herb, ItemShapes.Root, ItemShapes.Vine, ItemShapes.Fruit) +
                 Semantic(r, ArtifactMaterialTraits.Wood, 0.9f) +
                 Semantic(r, ArtifactMaterialTraits.Vitality, 1f),
            [
                Trait(ArtifactMaterialTraits.Vitality, 0.35f),
                Trait(ArtifactMaterialTraits.Renewal, 0.55f),
                Trait(ArtifactMaterialTraits.Purification, 0.25f),
            ]);

        Set(SpiritGatheringPattern, "spirit_gathering_pattern", ArtifactAtomCategory.Finish,
            ["聚灵", "纳元", "归藏"],
            ["ding3d.blue_fire", "mirror3d.jade_hex"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Lotus, ItemShapes.Eye, ItemShapes.Root) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.35f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Spirituality, 0.4f),
                Trait(ArtifactMaterialTraits.Capacity, 0.45f),
                Trait(ArtifactMaterialTraits.Resonance, 0.35f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 0.6f),
            ]);

        Set(SealingRunes, "sealing_runes", ArtifactAtomCategory.Finish, ["禁纹", "锁灵", "镇符"],
            ["seal_mountain3d.amber", "robe_panel3d.split_green"], ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Root, ItemShapes.Vine, ItemShapes.Bone, ItemShapes.Talisman) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 0.8f) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 0.55f),
            [
                Trait(ArtifactMaterialTraits.Binding, 0.55f),
                Trait(ArtifactMaterialTraits.Suppression, 0.5f),
                Trait(ArtifactMaterialTraits.Ward, 0.25f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.45f),
            ]);

        Set(PureYang, "pure_yang", ArtifactAtomCategory.Finish, ["纯阳", "曜阳", "大日"],
            ["sword_guard3d.crescent", "ding3d.copper_ember"], ["copper_ember", "gold_jade"],
            r => Semantic(r, ArtifactMaterialTraits.Pos, 1.3f) +
                 Semantic(r, ArtifactMaterialTraits.Fire, 0.85f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Pos, 0.4f),
                Trait(ArtifactMaterialTraits.Fire, 0.3f),
                Trait(ArtifactMaterialTraits.Ward, 0.3f),
                Trait(ArtifactMaterialTraits.Purification, 0.45f),
                Trait(ArtifactMaterialTraits.Amplification, 0.3f),
            ]);

        Set(ProfoundYin, "profound_yin", ArtifactAtomCategory.Finish, ["玄阴", "幽月", "寒冥"],
            ["mirror3d.bronze_round", "robe_panel3d.wide_blue"], ["cold_crystal", "dark_steel"],
            r => Semantic(r, ArtifactMaterialTraits.Neg, 1.25f) +
                 Semantic(r, ArtifactMaterialTraits.Water, 0.75f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Neg, 0.4f),
                Trait(ArtifactMaterialTraits.Water, 0.25f),
                Trait(ArtifactMaterialTraits.Perception, 0.35f),
                Trait(ArtifactMaterialTraits.Concealment, 0.4f),
                Trait(ArtifactMaterialTraits.Insight, 0.3f),
            ]);

        Set(VoidMark, "void_mark", ArtifactAtomCategory.Finish, ["虚痕", "空纹", "无间"],
            ["sword_grip3d.ringed", "mirror3d.bronze_round", "seal_mountain3d.amber"],
            ["dark_steel", "black_gold"],
            r => Semantic(r, ArtifactMaterialTraits.Entropy, 1.4f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.45f) + Quality(r, 2),
            [
                Trait(ArtifactMaterialTraits.Entropy, 0.4f),
                Trait(ArtifactMaterialTraits.Mobility, 0.35f),
                Trait(ArtifactMaterialTraits.Concealment, 0.4f),
                Trait(ArtifactMaterialTraits.Devouring, 0.35f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.4f),
            ]);
    }
}

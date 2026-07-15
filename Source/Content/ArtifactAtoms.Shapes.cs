using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class ArtifactAtoms
{
    public static ArtifactAtomAsset SwordSwarm { get; private set; }
    public static ArtifactAtomAsset SwordBreaker { get; private set; }
    public static ArtifactAtomAsset PrisonSeal { get; private set; }
    public static ArtifactAtomAsset CommandSeal { get; private set; }
    public static ArtifactAtomAsset CloudRobe { get; private set; }
    public static ArtifactAtomAsset VitalityRobe { get; private set; }
    public static ArtifactAtomAsset SoulMirror { get; private set; }
    public static ArtifactAtomAsset VoidMirror { get; private set; }
    public static ArtifactAtomAsset SpiritDing { get; private set; }
    public static ArtifactAtomAsset VitalityDing { get; private set; }

    private static void ConfigureAdditionalShapeAtoms()
    {
        Set(SwordSwarm, "sword_swarm", ArtifactAtomCategory.Shape, ["流光", "千锋", "游龙"],
            ["sword_blade3d.crystal", "sword_guard3d.wing", "sword_grip3d.ringed"],
            ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Feather, ItemShapes.Wing, ItemShapes.Eye, ItemShapes.Crystal) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.2f),
            [
                Trait(ArtifactMaterialTraits.Edge, 0.65f),
                Trait(ArtifactMaterialTraits.Mobility, 1f),
                Trait(ArtifactMaterialTraits.Spirituality, 0.35f),
                Trait(ArtifactMaterialTraits.PiercingFlight, 1f),
                Trait(ArtifactMaterialTraits.Resonance, 0.35f),
            ],
            ItemShapes.Sword);

        Set(SwordBreaker, "sword_breaker", ArtifactAtomCategory.Shape, ["破军", "斩岳", "断金"],
            ["sword_blade3d.long_thorn", "sword_guard3d.bar", "sword_grip3d.wrapped"],
            ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Bone, ItemShapes.Horn, ItemShapes.Stone, ItemShapes.Shell) +
                 Semantic(r, ArtifactMaterialTraits.Iron, 1.1f),
            [
                Trait(ArtifactMaterialTraits.Edge, 1.15f),
                Trait(ArtifactMaterialTraits.Hardness, 0.75f),
                Trait(ArtifactMaterialTraits.PiercingFlight, 1f),
                Trait(ArtifactMaterialTraits.Binding, 0.25f),
            ],
            ItemShapes.Sword);

        Set(PrisonSeal, "prison_seal", ArtifactAtomCategory.Shape, ["禁灵", "锁岳", "封天"],
            ["seal_mountain3d.amber"], ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Root, ItemShapes.Vine, ItemShapes.Bone, ItemShapes.Horn) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 1.1f) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 0.7f),
            [
                Trait(ArtifactMaterialTraits.Suppression, 1.15f),
                Trait(ArtifactMaterialTraits.Binding, 0.9f),
                Trait(ArtifactMaterialTraits.FieldProjection, 1f),
                Trait(ArtifactMaterialTraits.Stability, 0.25f),
            ],
            ItemShapes.Seal);

        Set(CommandSeal, "command_seal", ArtifactAtomCategory.Shape, ["敕令", "天命", "玄诏"],
            ["seal_mountain3d.green"], ["gold_jade", "cold_crystal"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Crystal, ItemShapes.Lotus, ItemShapes.Feather) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 1f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.9f),
            [
                Trait(ArtifactMaterialTraits.Suppression, 0.7f),
                Trait(ArtifactMaterialTraits.Ward, 0.7f),
                Trait(ArtifactMaterialTraits.FieldProjection, 1f),
                Trait(ArtifactMaterialTraits.Amplification, 0.45f),
            ],
            ItemShapes.Seal);

        Set(CloudRobe, "cloud_robe", ArtifactAtomCategory.Shape, ["流云", "无尘", "踏虚"],
            ["robe_panel3d.wide_blue"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Feather, ItemShapes.Wing, ItemShapes.Silk, ItemShapes.Flower) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.65f),
            [
                Trait(ArtifactMaterialTraits.Flexibility, 1.1f),
                Trait(ArtifactMaterialTraits.Mobility, 0.8f),
                Trait(ArtifactMaterialTraits.Ward, 0.65f),
                Trait(ArtifactMaterialTraits.Concealment, 0.55f),
                Trait(ArtifactMaterialTraits.GuardianWard, 1f),
            ],
            ItemShapes.Robe);

        Set(VitalityRobe, "vitality_robe", ArtifactAtomCategory.Shape, ["长生", "青木", "回春"],
            ["robe_panel3d.split_green"], ["gold_jade", "copper_ember"],
            r => Shape(r, ItemShapes.Herb, ItemShapes.Root, ItemShapes.Vine, ItemShapes.Fur) +
                 Semantic(r, ArtifactMaterialTraits.Wood, 1f) +
                 Semantic(r, ArtifactMaterialTraits.Vitality, 1f),
            [
                Trait(ArtifactMaterialTraits.Flexibility, 0.9f),
                Trait(ArtifactMaterialTraits.Ward, 0.55f),
                Trait(ArtifactMaterialTraits.Renewal, 1f),
                Trait(ArtifactMaterialTraits.GuardianWard, 0.65f),
                Trait(ArtifactMaterialTraits.Purification, 0.35f),
            ],
            ItemShapes.Robe);

        Set(SoulMirror, "soul_mirror", ArtifactAtomCategory.Shape, ["照魂", "问心", "观微"],
            ["mirror3d.jade_hex"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Blood, ItemShapes.Crystal) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.25f),
            [
                Trait(ArtifactMaterialTraits.Perception, 1.2f),
                Trait(ArtifactMaterialTraits.Reflection, 0.65f),
                Trait(ArtifactMaterialTraits.Insight, 1f),
                Trait(ArtifactMaterialTraits.Resonance, 0.55f),
            ],
            ItemShapes.Mirror);

        Set(VoidMirror, "void_mirror", ArtifactAtomCategory.Shape, ["太虚", "幽鉴", "无相"],
            ["mirror3d.bronze_round"], ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Liquid, ItemShapes.Stone) +
                 Semantic(r, ArtifactMaterialTraits.Entropy, 1.2f) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 0.8f),
            [
                Trait(ArtifactMaterialTraits.Perception, 0.85f),
                Trait(ArtifactMaterialTraits.Reflection, 0.8f),
                Trait(ArtifactMaterialTraits.Suppression, 0.55f),
                Trait(ArtifactMaterialTraits.Insight, 1f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.65f),
            ],
            ItemShapes.Mirror);

        Set(SpiritDing, "spirit_ding", ArtifactAtomCategory.Shape, ["纳灵", "归元", "聚气"],
            ["ding3d.blue_fire"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Lotus, ItemShapes.Root, ItemShapes.Eye) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.2f),
            [
                Trait(ArtifactMaterialTraits.Capacity, 1.15f),
                Trait(ArtifactMaterialTraits.Alchemy, 0.75f),
                Trait(ArtifactMaterialTraits.AlchemyVessel, 1f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 1f),
                Trait(ArtifactMaterialTraits.Resonance, 0.4f),
            ],
            ItemShapes.Ding);

        Set(VitalityDing, "vitality_ding", ArtifactAtomCategory.Shape, ["万生", "百草", "青华"],
            ["ding3d.copper_ember"], ["gold_jade", "copper_ember"],
            r => Shape(r, ItemShapes.Herb, ItemShapes.Mushroom, ItemShapes.Fruit, ItemShapes.Blood) +
                 Semantic(r, ArtifactMaterialTraits.Wood, 0.8f) +
                 Semantic(r, ArtifactMaterialTraits.Vitality, 1.2f),
            [
                Trait(ArtifactMaterialTraits.Capacity, 0.9f),
                Trait(ArtifactMaterialTraits.Alchemy, 0.85f),
                Trait(ArtifactMaterialTraits.AlchemyVessel, 1f),
                Trait(ArtifactMaterialTraits.Renewal, 1f),
                Trait(ArtifactMaterialTraits.Purification, 0.45f),
            ],
            ItemShapes.Ding);
    }
}

using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class ArtifactAtoms
{
    public static ArtifactAtomAsset SpiritWood { get; private set; }
    public static ArtifactAtomAsset FrostJade { get; private set; }
    public static ArtifactAtomAsset StarSilver { get; private set; }
    public static ArtifactAtomAsset EarthCore { get; private set; }
    public static ArtifactAtomAsset SoulCrystal { get; private set; }
    public static ArtifactAtomAsset BloodGold { get; private set; }
    public static ArtifactAtomAsset VoidStone { get; private set; }
    public static ArtifactAtomAsset CelestialSilk { get; private set; }
    public static ArtifactAtomAsset ThunderStone { get; private set; }
    public static ArtifactAtomAsset MoonWater { get; private set; }

    private static void ConfigureAdditionalMaterialAtoms()
    {
        Set(SpiritWood, "spirit_wood", ArtifactAtomCategory.Material, ["养灵", "青木", "生生"],
            ["sword_grip3d.wrapped", "robe_panel3d.split_green"], ["gold_jade"],
            r => Shape(r, ItemShapes.Wood, ItemShapes.Root, ItemShapes.Vine, ItemShapes.Herb) +
                 Semantic(r, ArtifactMaterialTraits.Wood, 1.4f) +
                 Semantic(r, ArtifactMaterialTraits.Vitality, 0.8f),
            [
                Trait(ArtifactMaterialTraits.Wood, 0.45f),
                Trait(ArtifactMaterialTraits.Flexibility, 0.45f),
                Trait(ArtifactMaterialTraits.Vitality, 0.4f),
                Trait(ArtifactMaterialTraits.Renewal, 0.7f),
            ]);

        Set(FrostJade, "frost_jade", ArtifactAtomCategory.Material, ["寒玉", "霜华", "冰魄"],
            ["sword_blade3d.jade", "mirror3d.jade_hex"], ["cold_crystal"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Lotus, ItemShapes.Liquid, ItemShapes.Shell) +
                 Semantic(r, ArtifactMaterialTraits.Water, 1.35f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Water, 0.4f),
                Trait(ArtifactMaterialTraits.Ward, 0.45f),
                Trait(ArtifactMaterialTraits.Stability, 0.5f),
                Trait(ArtifactMaterialTraits.Purification, 0.55f),
            ]);

        Set(StarSilver, "star_silver", ArtifactAtomCategory.Material, ["星银", "天银", "流辉"],
            ["sword_blade3d.crystal", "sword_guard3d.crescent", "mirror3d.bronze_round"],
            ["cold_crystal", "black_gold"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Eye, ItemShapes.Feather, ItemShapes.Stone) +
                 Semantic(r, ArtifactMaterialTraits.Iron, 0.8f) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 1f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Iron, 0.3f),
                Trait(ArtifactMaterialTraits.Edge, 0.45f),
                Trait(ArtifactMaterialTraits.Reflection, 0.55f),
                Trait(ArtifactMaterialTraits.Perception, 0.35f),
                Trait(ArtifactMaterialTraits.Insight, 0.45f),
            ]);

        Set(EarthCore, "earth_core", ArtifactAtomCategory.Material, ["地髓", "山心", "厚土"],
            ["seal_mountain3d.amber", "ding3d.copper_ember"], ["copper_ember", "black_gold"],
            r => Shape(r, ItemShapes.Stone, ItemShapes.Shell, ItemShapes.Bone, ItemShapes.Root) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 1.45f),
            [
                Trait(ArtifactMaterialTraits.Earth, 0.45f),
                Trait(ArtifactMaterialTraits.Hardness, 0.75f),
                Trait(ArtifactMaterialTraits.Suppression, 0.45f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.55f),
                Trait(ArtifactMaterialTraits.Stability, 0.45f),
            ]);

        Set(SoulCrystal, "soul_crystal", ArtifactAtomCategory.Material, ["魂晶", "神晶", "心魄"],
            ["mirror3d.jade_hex", "ding3d.blue_fire"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Crystal, ItemShapes.Blood, ItemShapes.Lotus) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.7f),
            [
                Trait(ArtifactMaterialTraits.Spirituality, 0.7f),
                Trait(ArtifactMaterialTraits.Perception, 0.5f),
                Trait(ArtifactMaterialTraits.Resonance, 0.55f),
                Trait(ArtifactMaterialTraits.Insight, 0.45f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 0.65f),
            ]);

        Set(BloodGold, "blood_gold", ArtifactAtomCategory.Material, ["血金", "赤金", "龙血"],
            ["sword_blade3d.long_thorn", "ding3d.copper_ember"], ["copper_ember", "black_gold"],
            r => Shape(r, ItemShapes.Blood, ItemShapes.Horn, ItemShapes.Claw, ItemShapes.Tooth) +
                 Semantic(r, ArtifactMaterialTraits.Fire, 0.8f) +
                 Semantic(r, ArtifactMaterialTraits.Vitality, 1.15f),
            [
                Trait(ArtifactMaterialTraits.Vitality, 0.55f),
                Trait(ArtifactMaterialTraits.Edge, 0.5f),
                Trait(ArtifactMaterialTraits.Amplification, 0.55f),
                Trait(ArtifactMaterialTraits.Volatility, 0.4f),
            ]);

        Set(VoidStone, "void_stone", ArtifactAtomCategory.Material, ["虚石", "幽岩", "空冥"],
            ["seal_mountain3d.amber", "mirror3d.bronze_round"], ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Stone, ItemShapes.Eye, ItemShapes.Liquid, ItemShapes.Shell) +
                 Semantic(r, ArtifactMaterialTraits.Entropy, 1.5f) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 0.9f),
            [
                Trait(ArtifactMaterialTraits.Entropy, 0.45f),
                Trait(ArtifactMaterialTraits.Concealment, 0.6f),
                Trait(ArtifactMaterialTraits.Devouring, 0.45f),
                Trait(ArtifactMaterialTraits.Suppression, 0.4f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.4f),
            ]);

        Set(CelestialSilk, "celestial_silk", ArtifactAtomCategory.Material, ["天蚕", "云锦", "玄绡"],
            ["robe_panel3d.wide_blue", "robe_panel3d.split_green"], ["cold_crystal", "gold_jade"],
            r => Shape(r, ItemShapes.Silk, ItemShapes.Fur, ItemShapes.Feather, ItemShapes.Wing) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.75f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Flexibility, 0.8f),
                Trait(ArtifactMaterialTraits.Ward, 0.55f),
                Trait(ArtifactMaterialTraits.Mobility, 0.45f),
                Trait(ArtifactMaterialTraits.Concealment, 0.35f),
                Trait(ArtifactMaterialTraits.GuardianWard, 0.55f),
            ]);

        Set(ThunderStone, "thunder_stone", ArtifactAtomCategory.Material, ["雷石", "霆晶", "电母"],
            ["sword_blade3d.crystal", "sword_guard3d.wing", "ding3d.blue_fire"],
            ["cold_crystal", "black_gold"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Stone, ItemShapes.Horn, ItemShapes.Eye) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 1.25f) +
                 Semantic(r, ArtifactMaterialTraits.Fire, 0.45f),
            [
                Trait(ArtifactMaterialTraits.Pos, 0.4f),
                Trait(ArtifactMaterialTraits.Mobility, 0.65f),
                Trait(ArtifactMaterialTraits.Volatility, 0.5f),
                Trait(ArtifactMaterialTraits.Amplification, 0.45f),
                Trait(ArtifactMaterialTraits.PiercingFlight, 0.45f),
            ]);

        Set(MoonWater, "moon_water", ArtifactAtomCategory.Material, ["月华", "玄水", "镜泉"],
            ["mirror3d.jade_hex", "robe_panel3d.wide_blue"], ["cold_crystal"],
            r => Shape(r, ItemShapes.Liquid, ItemShapes.Lotus, ItemShapes.Eye, ItemShapes.Flower) +
                 Semantic(r, ArtifactMaterialTraits.Water, 1.2f) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 0.45f),
            [
                Trait(ArtifactMaterialTraits.Water, 0.4f),
                Trait(ArtifactMaterialTraits.Reflection, 0.5f),
                Trait(ArtifactMaterialTraits.Perception, 0.45f),
                Trait(ArtifactMaterialTraits.Insight, 0.45f),
                Trait(ArtifactMaterialTraits.Purification, 0.35f),
            ]);
    }
}

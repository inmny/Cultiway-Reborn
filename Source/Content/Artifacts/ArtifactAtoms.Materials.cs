using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class ArtifactAtoms
{
    /// <summary>灵木材料 Atom；强化木行、柔韧、生机和续生语义。</summary>
    public static ArtifactAtomAsset SpiritWood { get; private set; }
    /// <summary>寒玉材料 Atom；强化水行、守护、稳定和净化语义。</summary>
    public static ArtifactAtomAsset FrostJade { get; private set; }
    /// <summary>星银材料 Atom；强化金性、锋锐、反射、感知和洞察语义。</summary>
    public static ArtifactAtomAsset StarSilver { get; private set; }
    /// <summary>地心材料 Atom；强化土行、坚硬、镇压、法域和稳定语义。</summary>
    public static ArtifactAtomAsset EarthCore { get; private set; }
    /// <summary>魂晶材料 Atom；强化灵性、感知、共鸣、洞察和蓄灵语义。</summary>
    public static ArtifactAtomAsset SoulCrystal { get; private set; }
    /// <summary>血金材料 Atom；强化生机、锋锐、增幅和易变语义。</summary>
    public static ArtifactAtomAsset BloodGold { get; private set; }
    /// <summary>虚空石材料 Atom；强化熵、隐匿、吞噬、镇压和法域语义。</summary>
    public static ArtifactAtomAsset VoidStone { get; private set; }
    /// <summary>天蚕丝材料 Atom；强化柔韧、守护、机动、隐匿和护主语义。</summary>
    public static ArtifactAtomAsset CelestialSilk { get; private set; }
    /// <summary>雷石材料 Atom；强化阳性、机动、易变、增幅和御剑穿刺语义。</summary>
    public static ArtifactAtomAsset ThunderStone { get; private set; }
    /// <summary>月华水材料 Atom；强化水行、反射、感知、洞察和净化语义。</summary>
    public static ArtifactAtomAsset MoonWater { get; private set; }
    /// <summary>共鸣铜材料 Atom；强化音律、共鸣、冲击、坚硬和稳定语义。</summary>
    public static ArtifactAtomAsset ResonantBronze { get; private set; }
    /// <summary>空间晶体材料 Atom；强化空间、储存、投射、容量和稳定语义。</summary>
    public static ArtifactAtomAsset SpatialCrystal { get; private set; }
    /// <summary>魂玉材料 Atom；强化魂魄、灵性、封印、感知和蓄灵语义。</summary>
    public static ArtifactAtomAsset SoulJade { get; private set; }
    /// <summary>虚空丝材料 Atom；强化柔韧、法域、隐匿、魂魄和投射语义。</summary>
    public static ArtifactAtomAsset VoidSilk { get; private set; }
    /// <summary>磁元铁材料 Atom；强化束缚、冲击、投射、坚硬和镇压语义。</summary>
    public static ArtifactAtomAsset MagneticIron { get; private set; }
    /// <summary>世界木材料 Atom；强化持续、储存、生机、容量和续生语义。</summary>
    public static ArtifactAtomAsset WorldWood { get; private set; }
    /// <summary>日火琉璃材料 Atom；强化火行、投射、增幅、易变和变化语义。</summary>
    public static ArtifactAtomAsset SunfireGlass { get; private set; }
    /// <summary>星髓材料 Atom；强化共鸣、魂魄、空间、蓄灵和投射语义。</summary>
    public static ArtifactAtomAsset StarlightMarrow { get; private set; }
    /// <summary>虚舟核心材料 Atom；提供载具、空间、容量和稳定语义，支撑御器载运。</summary>
    public static ArtifactAtomAsset VoidSailCore { get; private set; }
    /// <summary>气运地脉核心 Atom；提供宗门守护、稳定、坚硬和护持语义。</summary>
    public static ArtifactAtomAsset FortuneVeinCore { get; private set; }
    /// <summary>魂核材料 Atom；提供器灵、魂魄、持续和容量语义，支撑器灵成长与显化。</summary>
    public static ArtifactAtomAsset SoulCore { get; private set; }

    private static void ConfigureMaterialAtoms()
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
            ["moon_silver", "cold_crystal"],
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
            ["sword_blade3d.long_thorn", "ding3d.copper_ember"], ["blood_jade", "copper_ember"],
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
            ["seal_mountain3d.amber", "mirror3d.bronze_round"], ["void_obsidian", "dark_steel"],
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
            ["robe_panel3d.wide_blue", "robe_panel3d.split_green"], ["cloud_silk", "moon_silver"],
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
            ["mirror3d.jade_hex", "robe_panel3d.wide_blue"], ["moon_silver", "cold_crystal"],
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

        Set(ResonantBronze, "resonant_bronze", ArtifactAtomCategory.Material, ["鸣铜", "梵金", "回音"],
                    ["bell_body3d.ancient_bronze", "bell_mouth3d.eight_tone_ring", "banner_finial3d.beast_crown"],
                    ["imperial_bronze", "copper_ember"],
                    r => Shape(r, ItemShapes.Shell, ItemShapes.Bone, ItemShapes.Horn, ItemShapes.Stone) +
                         Semantic(r, ArtifactMaterialTraits.Iron, 0.8f) + Quality(r, 1),
                    [
                        Trait(ArtifactMaterialTraits.Sound, 0.75f),
                Trait(ArtifactMaterialTraits.Resonance, 0.65f),
                Trait(ArtifactMaterialTraits.Impact, 0.45f),
                Trait(ArtifactMaterialTraits.Hardness, 0.4f),
                Trait(ArtifactMaterialTraits.Stability, 0.45f),
                    ]);

        Set(SpatialCrystal, "spatial_crystal", ArtifactAtomCategory.Material, ["空晶", "界石", "须弥"],
            ["gourd_mouth3d.spatial_rim", "tower_level3d.crystal_prison_level", "pearl_shell3d.void_lattice_shell"],
            ["void_obsidian", "cold_crystal"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Eye, ItemShapes.Stone, ItemShapes.Liquid) +
                 Semantic(r, ArtifactMaterialTraits.Entropy, 1f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.65f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Space, 0.8f),
                Trait(ArtifactMaterialTraits.Storage, 0.65f),
                Trait(ArtifactMaterialTraits.Projection, 0.55f),
                Trait(ArtifactMaterialTraits.Capacity, 0.45f),
                Trait(ArtifactMaterialTraits.Stability, 0.3f),
            ]);

        Set(SoulJade, "soul_jade", ArtifactAtomCategory.Material, ["魂玉", "心璧", "神珀"],
            ["bell_clapper3d.jade_tongue", "gourd_stopper3d.spirit_bead_seal", "pearl_core3d.moonwater_pearl"],
            ["gold_jade", "moon_silver"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Crystal, ItemShapes.Lotus, ItemShapes.Blood) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.45f),
            [
                Trait(ArtifactMaterialTraits.Soul, 0.75f),
                Trait(ArtifactMaterialTraits.Spirituality, 0.55f),
                Trait(ArtifactMaterialTraits.Sealing, 0.45f),
                Trait(ArtifactMaterialTraits.Perception, 0.4f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 0.4f),
            ]);

        Set(VoidSilk, "void_silk", ArtifactAtomCategory.Material, ["虚绡", "幽锦", "无影"],
            ["banner_cloth3d.spirit_script_streamer", "fan_leaf3d.cloud_silk_leaf", "fan_pendant3d.red_double_tassel"],
            ["void_obsidian", "cloud_silk"],
            r => Shape(r, ItemShapes.Silk, ItemShapes.Feather, ItemShapes.Wing, ItemShapes.Fur) +
                 Semantic(r, ArtifactMaterialTraits.Entropy, 0.8f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Flexibility, 0.7f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.5f),
                Trait(ArtifactMaterialTraits.Concealment, 0.55f),
                Trait(ArtifactMaterialTraits.Soul, 0.35f),
                Trait(ArtifactMaterialTraits.Projection, 0.35f),
            ]);

        Set(MagneticIron, "magnetic_iron", ArtifactAtomCategory.Material, ["元磁", "玄极", "摄铁"],
            ["fan_leaf3d.black_iron_leaf", "tower_base3d.octagonal_stone_base", "tower_eaves3d.broad_square_eaves"],
            ["dark_steel", "black_gold"],
            r => Shape(r, ItemShapes.Stone, ItemShapes.Crystal, ItemShapes.Shell, ItemShapes.Horn) +
                 Semantic(r, ArtifactMaterialTraits.Iron, 1.2f) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 0.55f),
            [
                Trait(ArtifactMaterialTraits.Binding, 0.65f),
                Trait(ArtifactMaterialTraits.Impact, 0.55f),
                Trait(ArtifactMaterialTraits.Projection, 0.4f),
                Trait(ArtifactMaterialTraits.Hardness, 0.55f),
                Trait(ArtifactMaterialTraits.Suppression, 0.35f),
            ]);

        Set(WorldWood, "world_wood", ArtifactAtomCategory.Material, ["界木", "建木", "天根"],
            ["banner_pole3d.jade_bamboo_staff", "gourd_tie3d.vine_binding", "fan_handle3d.jade_spine_handle"],
            ["gold_jade", "white_bone"],
            r => Shape(r, ItemShapes.Wood, ItemShapes.Root, ItemShapes.Vine, ItemShapes.Bamboo) +
                 Semantic(r, ArtifactMaterialTraits.Wood, 1.3f) +
                 Semantic(r, ArtifactMaterialTraits.Vitality, 0.6f),
            [
                Trait(ArtifactMaterialTraits.Sustain, 0.65f),
                Trait(ArtifactMaterialTraits.Storage, 0.45f),
                Trait(ArtifactMaterialTraits.Vitality, 0.4f),
                Trait(ArtifactMaterialTraits.Capacity, 0.4f),
                Trait(ArtifactMaterialTraits.Renewal, 0.45f),
            ]);

        Set(SunfireGlass, "sunfire_glass", ArtifactAtomCategory.Material, ["曜璃", "火晶", "日精"],
            ["fan_leaf3d.black_iron_leaf", "pearl_core3d.five_element_prism_pearl", "pearl_halo3d.solar_glyph_halo"],
            ["blood_jade", "copper_ember"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Blood, ItemShapes.Eye, ItemShapes.Liquid) +
                 Semantic(r, ArtifactMaterialTraits.Fire, 1.25f) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.75f),
            [
                Trait(ArtifactMaterialTraits.Fire, 0.6f),
                Trait(ArtifactMaterialTraits.Projection, 0.55f),
                Trait(ArtifactMaterialTraits.Amplification, 0.6f),
                Trait(ArtifactMaterialTraits.Volatility, 0.4f),
                Trait(ArtifactMaterialTraits.Transformation, 0.3f),
            ]);

        Set(StarlightMarrow, "starlight_marrow", ArtifactAtomCategory.Material, ["星髓", "天华", "辰砂"],
            ["pearl_companions3d.seven_star_companions", "tower_finial3d.seven_pearl_spire", "bell_crown3d.heavenly_arch"],
            ["moon_silver", "azure_ceramic"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Bone, ItemShapes.Eye, ItemShapes.Ball) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.8f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1f) + Quality(r, 2),
            [
                Trait(ArtifactMaterialTraits.Resonance, 0.6f),
                Trait(ArtifactMaterialTraits.Soul, 0.45f),
                Trait(ArtifactMaterialTraits.Space, 0.45f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 0.55f),
                Trait(ArtifactMaterialTraits.Projection, 0.35f),
            ]);

        Set(VoidSailCore, "void_sail_core", ArtifactAtomCategory.Material, ["虚舟", "空梭", "云台"],
                    ["tower_base3d.floating_cloud_base", "pearl_shell3d.void_lattice_shell", "gourd_mouth3d.spatial_rim"],
                    ["void_obsidian", "cold_crystal"],
                    r => Shape(r, ItemShapes.Crystal, ItemShapes.Wood, ItemShapes.Ball, ItemShapes.Shell) +
                         Semantic(r, ArtifactMaterialTraits.Space, 0.9f) +
                         Semantic(r, ArtifactMaterialTraits.Capacity, 0.7f),
                    [
                        Trait(ArtifactMaterialTraits.Vehicle, 0.7f),
                Trait(ArtifactMaterialTraits.Space, 0.55f),
                Trait(ArtifactMaterialTraits.Capacity, 0.5f),
                Trait(ArtifactMaterialTraits.Stability, 0.35f),
                    ]);

        Set(FortuneVeinCore, "fortune_vein_core", ArtifactAtomCategory.Material, ["地脉", "宗运", "祖庭"],
                    ["tower_base3d.octagonal_stone_base", "seal_base3d.lotus_plinth", "ding_core3d.golden_elixir"],
                    ["gold_jade", "azure_ceramic"],
                    r => Shape(r, ItemShapes.Stone, ItemShapes.Crystal, ItemShapes.Root, ItemShapes.Lotus) +
                         Semantic(r, ArtifactMaterialTraits.Earth, 0.8f) +
                         Semantic(r, ArtifactMaterialTraits.Stability, 0.8f),
                    [
                        Trait(ArtifactMaterialTraits.SectGuardian, 0.7f),
                Trait(ArtifactMaterialTraits.Stability, 0.6f),
                Trait(ArtifactMaterialTraits.Hardness, 0.4f),
                Trait(ArtifactMaterialTraits.Ward, 0.35f),
                    ]);

        Set(SoulCore, "soul_core", ArtifactAtomCategory.Material, ["魂核", "灵胎", "心珀"],
                    ["pearl_core3d.moonwater_pearl", "ding_core3d.void_vortex", "gourd_stopper3d.spirit_bead_seal"],
                    ["blood_jade", "gold_jade"],
                    r => Shape(r, ItemShapes.Crystal, ItemShapes.Eye, ItemShapes.Bone, ItemShapes.Blood) +
                         Semantic(r, ArtifactMaterialTraits.Spirituality, 1.2f) + Quality(r, 1),
                    [
                        Trait(ArtifactMaterialTraits.ArtifactSpirit, 0.72f),
                Trait(ArtifactMaterialTraits.Soul, 0.58f),
                Trait(ArtifactMaterialTraits.Sustain, 0.42f),
                Trait(ArtifactMaterialTraits.Capacity, 0.3f),
                    ]);
    }
}

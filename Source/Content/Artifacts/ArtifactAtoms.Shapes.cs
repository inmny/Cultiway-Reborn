using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class ArtifactAtoms
{
    /// <summary>分光剑器形 Atom；强化机动、共鸣和御剑穿刺，倾向多剑阵列能力。</summary>
    public static ArtifactAtomAsset SwordSwarm { get; private set; }
    /// <summary>破军剑器形 Atom；强化锋锐、坚硬、穿刺和束缚，倾向重击破甲能力。</summary>
    public static ArtifactAtomAsset SwordBreaker { get; private set; }
    /// <summary>镇狱印器形 Atom；强化镇压、束缚、法域和稳定，倾向囚禁封禁能力。</summary>
    public static ArtifactAtomAsset PrisonSeal { get; private set; }
    /// <summary>敕令印器形 Atom；兼顾镇压、守护、法域和增幅，倾向号令型法印。</summary>
    public static ArtifactAtomAsset CommandSeal { get; private set; }
    /// <summary>流云袍器形 Atom；强化柔韧、机动、守护、隐匿和护主能力。</summary>
    public static ArtifactAtomAsset CloudRobe { get; private set; }
    /// <summary>生机袍器形 Atom；强化续生、守护和净化，倾向恢复型法袍。</summary>
    public static ArtifactAtomAsset VitalityRobe { get; private set; }
    /// <summary>照魂镜器形 Atom；强化感知、反射、洞察和共鸣，倾向识魂破妄能力。</summary>
    public static ArtifactAtomAsset SoulMirror { get; private set; }
    /// <summary>太虚镜器形 Atom；强化反射、洞察、镇压和法域，倾向虚空控制能力。</summary>
    public static ArtifactAtomAsset VoidMirror { get; private set; }
    /// <summary>聚灵鼎器形 Atom；强化容量、炼制、蓄灵和共鸣，倾向灵力储炼能力。</summary>
    public static ArtifactAtomAsset SpiritDing { get; private set; }
    /// <summary>百草鼎器形 Atom；强化炼制、续生和净化，倾向丹药与恢复辅助。</summary>
    public static ArtifactAtomAsset VitalityDing { get; private set; }
    /// <summary>号令旗器形 Atom；强化法域、投射、增幅、音律和持续，倾向群体统御能力。</summary>
    public static ArtifactAtomAsset CommandBanner { get; private set; }
    /// <summary>招魂幡器形 Atom；强化魂魄、储存、吞噬、封印和法域，倾向魂体操控。</summary>
    public static ArtifactAtomAsset SoulBanner { get; private set; }
    /// <summary>震魂钟器形 Atom；强化音律、共鸣、魂魄、冲击和投射，倾向范围震荡。</summary>
    public static ArtifactAtomAsset ResonanceBell { get; private set; }
    /// <summary>清音钟器形 Atom；强化音律、净化、守护、法域和持续，倾向群体净化。</summary>
    public static ArtifactAtomAsset PurifyingBell { get; private set; }
    /// <summary>蕴灵葫芦器形 Atom；强化储存、容量、空间、蓄灵和持续，倾向灵力缓冲。</summary>
    public static ArtifactAtomAsset SpiritGourd { get; private set; }
    /// <summary>吞天葫芦器形 Atom；强化吞噬、储存、空间、束缚和投射，倾向吸纳释放。</summary>
    public static ArtifactAtomAsset DevouringGourd { get; private set; }
    /// <summary>罡风扇器形 Atom；强化投射、机动、冲击、增幅和柔韧，倾向风系横扫。</summary>
    public static ArtifactAtomAsset TempestFan { get; private set; }
    /// <summary>离火扇器形 Atom；强化火行、投射、增幅、易变和持续，倾向火焰扇击。</summary>
    public static ArtifactAtomAsset FlameFan { get; private set; }
    /// <summary>镇狱塔器形 Atom；强化镇压、封印、束缚、空间和持续法域。</summary>
    public static ArtifactAtomAsset PrisonTower { get; private set; }
    /// <summary>九重塔器形 Atom；强化投射、法域、守护、容量、空间和持续。</summary>
    public static ArtifactAtomAsset RealmTower { get; private set; }
    /// <summary>周天珠器形 Atom；强化机动、共鸣、守护、投射和护主，倾向环绕防御。</summary>
    public static ArtifactAtomAsset OrbitPearl { get; private set; }
    /// <summary>五行珠器形 Atom；强化共鸣、增幅、投射、蓄灵和变化，倾向元素联动。</summary>
    public static ArtifactAtomAsset ElementPearl { get; private set; }

    private static void ConfigureShapeAtoms()
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
            ["robe_panel3d.wide_blue"], ["cloud_silk", "moon_silver"],
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
            ["mirror3d.bronze_round"], ["void_obsidian", "dark_steel"],
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

        Set(CommandBanner, "command_banner", ArtifactAtomCategory.Shape, ["号令", "玄旗", "天军"],
                    ["banner_cloth3d.cloud_war_banner", "banner_pole3d.black_iron_spine", "banner_finial3d.spearhead"],
                    ["imperial_bronze", "black_gold"],
                    r => Shape(r, ItemShapes.Silk, ItemShapes.Feather, ItemShapes.Talisman, ItemShapes.Wood) +
                         Semantic(r, ArtifactMaterialTraits.Pos, 0.8f) +
                         Semantic(r, ArtifactMaterialTraits.Amplification, 0.8f),
                    [
                        Trait(ArtifactMaterialTraits.FieldProjection, 1f),
                Trait(ArtifactMaterialTraits.Projection, 0.75f),
                Trait(ArtifactMaterialTraits.Amplification, 0.8f),
                Trait(ArtifactMaterialTraits.Sound, 0.35f),
                Trait(ArtifactMaterialTraits.Sustain, 0.55f),
                    ],
                    ItemShapes.Banner);

        Set(SoulBanner, "soul_banner", ArtifactAtomCategory.Shape, ["招魂", "摄魄", "幽幡"],
            ["banner_cloth3d.spirit_script_streamer", "banner_pole3d.carved_bone_standard", "banner_tassel3d.spirit_bells"],
            ["void_obsidian", "white_bone"],
            r => Shape(r, ItemShapes.Bone, ItemShapes.Blood, ItemShapes.Eye, ItemShapes.Talisman) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 1f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.8f),
            [
                Trait(ArtifactMaterialTraits.Soul, 1.1f),
                Trait(ArtifactMaterialTraits.Storage, 0.65f),
                Trait(ArtifactMaterialTraits.Devouring, 0.55f),
                Trait(ArtifactMaterialTraits.Sealing, 0.55f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.8f),
            ],
            ItemShapes.Banner);

        Set(ResonanceBell, "resonance_bell", ArtifactAtomCategory.Shape, ["震魂", "雷音", "梵钟"],
            ["bell_body3d.ancient_bronze", "bell_mouth3d.eight_tone_ring", "bell_clapper3d.thunder_seed"],
            ["imperial_bronze", "black_gold"],
            r => Shape(r, ItemShapes.Shell, ItemShapes.Bone, ItemShapes.Horn, ItemShapes.Crystal) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.55f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.7f),
            [
                Trait(ArtifactMaterialTraits.Sound, 1.15f),
                Trait(ArtifactMaterialTraits.Resonance, 0.9f),
                Trait(ArtifactMaterialTraits.Soul, 0.65f),
                Trait(ArtifactMaterialTraits.Impact, 0.75f),
                Trait(ArtifactMaterialTraits.Projection, 0.55f),
            ],
            ItemShapes.Bell);

        Set(PurifyingBell, "purifying_bell", ArtifactAtomCategory.Shape, ["清音", "涤尘", "护心"],
            ["bell_body3d.jade_temple_bell", "bell_mouth3d.lotus_lip", "bell_crown3d.lotus_hook"],
            ["moon_silver", "gold_jade"],
            r => Shape(r, ItemShapes.Lotus, ItemShapes.Crystal, ItemShapes.Liquid, ItemShapes.Flower) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.9f) +
                 Semantic(r, ArtifactMaterialTraits.Water, 0.45f),
            [
                Trait(ArtifactMaterialTraits.Sound, 0.85f),
                Trait(ArtifactMaterialTraits.Purification, 1f),
                Trait(ArtifactMaterialTraits.Ward, 0.8f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.55f),
                Trait(ArtifactMaterialTraits.Sustain, 0.45f),
            ],
            ItemShapes.Bell);

        Set(SpiritGourd, "spirit_gourd", ArtifactAtomCategory.Shape, ["蕴灵", "归藏", "纳元"],
            ["gourd_body3d.jade_spirit_gourd", "gourd_mouth3d.lotus_mouth", "gourd_stopper3d.spirit_bead_seal"],
            ["gold_jade", "azure_ceramic"],
            r => Shape(r, ItemShapes.Fruit, ItemShapes.Liquid, ItemShapes.Lotus, ItemShapes.Root) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 1.1f) +
                 Semantic(r, ArtifactMaterialTraits.Wood, 0.5f),
            [
                Trait(ArtifactMaterialTraits.Storage, 1.1f),
                Trait(ArtifactMaterialTraits.Capacity, 0.9f),
                Trait(ArtifactMaterialTraits.Space, 0.55f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 0.85f),
                Trait(ArtifactMaterialTraits.Sustain, 0.5f),
            ],
            ItemShapes.Gourd);

        Set(DevouringGourd, "devouring_gourd", ArtifactAtomCategory.Shape, ["吞天", "摄物", "化虚"],
            ["gourd_body3d.void_swallowing_gourd", "gourd_mouth3d.spatial_rim", "gourd_tie3d.chain_talisman"],
            ["void_obsidian", "dark_steel"],
            r => Shape(r, ItemShapes.Eye, ItemShapes.Liquid, ItemShapes.Stone, ItemShapes.Blood) +
                 Semantic(r, ArtifactMaterialTraits.Entropy, 1.1f) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 0.75f),
            [
                Trait(ArtifactMaterialTraits.Devouring, 1f),
                Trait(ArtifactMaterialTraits.Storage, 0.8f),
                Trait(ArtifactMaterialTraits.Space, 0.8f),
                Trait(ArtifactMaterialTraits.Binding, 0.55f),
                Trait(ArtifactMaterialTraits.Projection, 0.55f),
            ],
            ItemShapes.Gourd);

        Set(TempestFan, "tempest_fan", ArtifactAtomCategory.Shape, ["罡风", "扶摇", "天翎"],
            ["fan_leaf3d.crane_feather_leaf", "fan_ribs3d.jade_seven_ribs", "fan_pendant3d.moon_ring_pendant"],
            ["cloud_silk", "moon_silver"],
            r => Shape(r, ItemShapes.Feather, ItemShapes.Wing, ItemShapes.Silk, ItemShapes.Bamboo) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.45f) +
                 Semantic(r, ArtifactMaterialTraits.Mobility, 0.8f),
            [
                Trait(ArtifactMaterialTraits.Projection, 1f),
                Trait(ArtifactMaterialTraits.Mobility, 0.9f),
                Trait(ArtifactMaterialTraits.Impact, 0.7f),
                Trait(ArtifactMaterialTraits.Amplification, 0.55f),
                Trait(ArtifactMaterialTraits.Flexibility, 0.45f),
            ],
            ItemShapes.Fan);

        Set(FlameFan, "flame_fan", ArtifactAtomCategory.Shape, ["离火", "赤羽", "焚天"],
            ["fan_leaf3d.black_iron_leaf", "fan_ribs3d.gilded_six_ribs", "fan_handle3d.bronze_cloud_handle"],
            ["copper_ember", "blood_jade"],
            r => Shape(r, ItemShapes.Feather, ItemShapes.Blood, ItemShapes.Horn, ItemShapes.Wood) +
                 Semantic(r, ArtifactMaterialTraits.Fire, 1.2f),
            [
                Trait(ArtifactMaterialTraits.Fire, 1f),
                Trait(ArtifactMaterialTraits.Projection, 0.9f),
                Trait(ArtifactMaterialTraits.Amplification, 0.65f),
                Trait(ArtifactMaterialTraits.Volatility, 0.55f),
                Trait(ArtifactMaterialTraits.Sustain, 0.35f),
            ],
            ItemShapes.Fan);

        Set(PrisonTower, "prison_tower", ArtifactAtomCategory.Shape, ["镇狱", "锁界", "封天"],
            ["tower_level3d.crystal_prison_level", "tower_eaves3d.suspended_bell_eaves", "tower_base3d.octagonal_stone_base"],
            ["void_obsidian", "dark_steel"],
            r => Shape(r, ItemShapes.Stone, ItemShapes.Crystal, ItemShapes.Bone, ItemShapes.Talisman) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 0.8f) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 0.8f),
            [
                Trait(ArtifactMaterialTraits.Suppression, 1.05f),
                Trait(ArtifactMaterialTraits.Sealing, 1f),
                Trait(ArtifactMaterialTraits.Binding, 0.85f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.9f),
                Trait(ArtifactMaterialTraits.Space, 0.55f),
                Trait(ArtifactMaterialTraits.Sustain, 0.55f),
            ],
            ItemShapes.Tower);

        Set(RealmTower, "realm_tower", ArtifactAtomCategory.Shape, ["层界", "九重", "天阙"],
            ["tower_level3d.golden_scripture_chamber", "tower_eaves3d.octagonal_lotus_eaves", "tower_finial3d.seven_pearl_spire"],
            ["imperial_bronze", "azure_ceramic"],
            r => Shape(r, ItemShapes.Crystal, ItemShapes.Lotus, ItemShapes.Shell, ItemShapes.Stone) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.8f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Projection, 0.85f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.9f),
                Trait(ArtifactMaterialTraits.Ward, 0.75f),
                Trait(ArtifactMaterialTraits.Capacity, 0.65f),
                Trait(ArtifactMaterialTraits.Space, 0.7f),
                Trait(ArtifactMaterialTraits.Sustain, 0.65f),
            ],
            ItemShapes.Tower);

        Set(OrbitPearl, "orbit_pearl", ArtifactAtomCategory.Shape, ["周天", "连珠", "护辰"],
            ["pearl_core3d.moonwater_pearl", "pearl_shell3d.dragon_armillary_shell", "pearl_companions3d.seven_star_companions"],
            ["moon_silver", "cold_crystal"],
            r => Shape(r, ItemShapes.Ball, ItemShapes.Crystal, ItemShapes.Eye, ItemShapes.Lotus) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.85f) +
                 Semantic(r, ArtifactMaterialTraits.Water, 0.45f),
            [
                Trait(ArtifactMaterialTraits.Mobility, 0.75f),
                Trait(ArtifactMaterialTraits.Resonance, 0.85f),
                Trait(ArtifactMaterialTraits.Ward, 0.75f),
                Trait(ArtifactMaterialTraits.Projection, 0.75f),
                Trait(ArtifactMaterialTraits.GuardianWard, 0.75f),
                Trait(ArtifactMaterialTraits.Sustain, 0.55f),
            ],
            ItemShapes.Pearl);

        Set(ElementPearl, "element_pearl", ArtifactAtomCategory.Shape, ["五蕴", "混元", "曜灵"],
            ["pearl_core3d.five_element_prism_pearl", "pearl_halo3d.solar_glyph_halo", "pearl_companions3d.three_talent_companions"],
            ["azure_ceramic", "blood_jade"],
            r => Shape(r, ItemShapes.Ball, ItemShapes.Crystal, ItemShapes.Liquid, ItemShapes.Blood) +
                 Semantic(r, ArtifactMaterialTraits.Amplification, 0.8f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Resonance, 0.8f),
                Trait(ArtifactMaterialTraits.Amplification, 0.8f),
                Trait(ArtifactMaterialTraits.Projection, 0.9f),
                Trait(ArtifactMaterialTraits.SpiritReservoir, 0.55f),
                Trait(ArtifactMaterialTraits.Transformation, 0.45f),
            ],
            ItemShapes.Pearl);
    }
}

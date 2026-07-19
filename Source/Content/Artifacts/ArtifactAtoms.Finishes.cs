using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Libraries;

namespace Cultiway.Content;

public partial class ArtifactAtoms
{
    /// <summary>云纹纹饰 Atom；强化机动、柔韧和隐匿语义，倾向轻灵移动效果。</summary>
    public static ArtifactAtomAsset CloudPattern { get; private set; }
    /// <summary>雷纹纹饰 Atom；强化阳性、机动、易变和增幅语义，倾向高速爆发效果。</summary>
    public static ArtifactAtomAsset ThunderPattern { get; private set; }
    /// <summary>山岳纹饰 Atom；强化土行、坚硬、镇压、法域和稳定语义。</summary>
    public static ArtifactAtomAsset MountainPattern { get; private set; }
    /// <summary>水月纹饰 Atom；强化反射、感知、洞察和净化语义。</summary>
    public static ArtifactAtomAsset WaterMoonPattern { get; private set; }
    /// <summary>生机纹饰 Atom；强化生机、续生和净化语义，倾向恢复效果。</summary>
    public static ArtifactAtomAsset LifePattern { get; private set; }
    /// <summary>聚灵纹饰 Atom；强化灵性、容量、共鸣和蓄灵语义。</summary>
    public static ArtifactAtomAsset SpiritGatheringPattern { get; private set; }
    /// <summary>封禁符纹 Atom；强化束缚、镇压、守护和法域投射语义。</summary>
    public static ArtifactAtomAsset SealingRunes { get; private set; }
    /// <summary>纯阳纹饰 Atom；强化阳性、火行、守护、净化和增幅语义。</summary>
    public static ArtifactAtomAsset PureYang { get; private set; }
    /// <summary>玄阴纹饰 Atom；强化阴性、水行、感知、隐匿和洞察语义。</summary>
    public static ArtifactAtomAsset ProfoundYin { get; private set; }
    /// <summary>虚空纹饰 Atom；强化熵、机动、隐匿、吞噬和法域语义。</summary>
    public static ArtifactAtomAsset VoidMark { get; private set; }
    /// <summary>号令符纹 Atom；强化增幅、音律、法域投射和持续语义，倾向群体指挥效果。</summary>
    public static ArtifactAtomAsset CommandRunes { get; private set; }
    /// <summary>缚魂符纹 Atom；强化魂魄、封印、束缚和储存语义。</summary>
    public static ArtifactAtomAsset SoulBindingScript { get; private set; }
    /// <summary>叠空纹饰 Atom；强化空间、储存、投射和隐匿语义。</summary>
    public static ArtifactAtomAsset SpatialFold { get; private set; }
    /// <summary>共鸣环纹 Atom；强化音律、共鸣、增幅和投射语义。</summary>
    public static ArtifactAtomAsset ResonanceRings { get; private set; }
    /// <summary>吞噬涡纹 Atom；强化吞噬、储存、空间和束缚语义。</summary>
    public static ArtifactAtomAsset DevouringVortex { get; private set; }
    /// <summary>护持光轮 Atom；强化守护、投射、护主和持续语义。</summary>
    public static ArtifactAtomAsset GuardianHalo { get; private set; }
    /// <summary>层界法纹 Atom；强化法域投射、封印、空间和镇压语义。</summary>
    public static ArtifactAtomAsset LayeredRealmMark { get; private set; }
    /// <summary>化形纹饰 Atom；强化变化、投射、机动和易变语义。</summary>
    public static ArtifactAtomAsset TransformationPattern { get; private set; }
    /// <summary>御空符纹 Atom；提供载具、机动、投射和持续语义，倾向御器载运能力。</summary>
    public static ArtifactAtomAsset CloudRidingScript { get; private set; }
    /// <summary>镇宗誓纹 Atom；提供宗门守护、护持、法域和持续语义。</summary>
    public static ArtifactAtomAsset AncestralGuardianVow { get; private set; }
    /// <summary>点灵符纹 Atom；提供器灵、魂魄、灵性和感知语义，倾向唤醒持久器灵。</summary>
    public static ArtifactAtomAsset SpiritAwakeningScript { get; private set; }

    private static void ConfigureFinishAtoms()
    {
        Set(CloudPattern, "cloud_pattern", ArtifactAtomCategory.Finish, ["云纹", "流霞", "御风"],
                    ["robe_panel3d.wide_blue", "sword_guard3d.wing"], ["cloud_silk", "moon_silver"],
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
            ["mirror3d.jade_hex", "robe_panel3d.wide_blue"], ["moon_silver", "cold_crystal"],
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
            ["void_obsidian", "dark_steel"],
            r => Semantic(r, ArtifactMaterialTraits.Entropy, 1.4f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.45f) + Quality(r, 2),
            [
                Trait(ArtifactMaterialTraits.Entropy, 0.4f),
                Trait(ArtifactMaterialTraits.Mobility, 0.35f),
                Trait(ArtifactMaterialTraits.Concealment, 0.4f),
                Trait(ArtifactMaterialTraits.Devouring, 0.35f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.4f),
            ]);

        Set(CommandRunes, "command_runes", ArtifactAtomCategory.Finish, ["号令", "天诏", "军势"],
                    ["banner_cloth3d.cloud_war_banner", "tower_level3d.golden_scripture_chamber"],
                    ["imperial_bronze", "black_gold"],
                    r => Shape(r, ItemShapes.Talisman, ItemShapes.Silk, ItemShapes.Eye, ItemShapes.Feather) +
                         Semantic(r, ArtifactMaterialTraits.Pos, 0.9f) + Quality(r, 1),
                    [
                        Trait(ArtifactMaterialTraits.Amplification, 0.5f),
                Trait(ArtifactMaterialTraits.Sound, 0.35f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.55f),
                Trait(ArtifactMaterialTraits.Sustain, 0.4f),
                    ]);

        Set(SoulBindingScript, "soul_binding_script", ArtifactAtomCategory.Finish, ["缚魂", "摄魄", "役灵"],
            ["banner_cloth3d.spirit_script_streamer", "bell_clapper3d.thunder_seed", "tower_level3d.crystal_prison_level"],
            ["void_obsidian", "white_bone"],
            r => Shape(r, ItemShapes.Talisman, ItemShapes.Bone, ItemShapes.Blood, ItemShapes.Eye) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 1f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.6f),
            [
                Trait(ArtifactMaterialTraits.Soul, 0.55f),
                Trait(ArtifactMaterialTraits.Sealing, 0.55f),
                Trait(ArtifactMaterialTraits.Binding, 0.5f),
                Trait(ArtifactMaterialTraits.Storage, 0.35f),
            ]);

        Set(SpatialFold, "spatial_fold", ArtifactAtomCategory.Finish, ["叠空", "须弥", "界褶"],
            ["gourd_mouth3d.spatial_rim", "tower_level3d.crystal_prison_level", "pearl_shell3d.void_lattice_shell"],
            ["void_obsidian", "cold_crystal"],
            r => Semantic(r, ArtifactMaterialTraits.Entropy, 1.25f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.55f) + Quality(r, 2),
            [
                Trait(ArtifactMaterialTraits.Space, 0.6f),
                Trait(ArtifactMaterialTraits.Storage, 0.5f),
                Trait(ArtifactMaterialTraits.Projection, 0.45f),
                Trait(ArtifactMaterialTraits.Concealment, 0.3f),
            ]);

        Set(ResonanceRings, "resonance_rings", ArtifactAtomCategory.Finish, ["回响", "同律", "鸣环"],
            ["bell_mouth3d.eight_tone_ring", "pearl_halo3d.water_ripple_halo", "gourd_tie3d.chain_talisman"],
            ["moon_silver", "imperial_bronze"],
            r => Shape(r, ItemShapes.Shell, ItemShapes.Crystal, ItemShapes.Ball, ItemShapes.Liquid) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.9f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Sound, 0.5f),
                Trait(ArtifactMaterialTraits.Resonance, 0.6f),
                Trait(ArtifactMaterialTraits.Amplification, 0.35f),
                Trait(ArtifactMaterialTraits.Projection, 0.35f),
            ]);

        Set(DevouringVortex, "devouring_vortex", ArtifactAtomCategory.Finish, ["吞漩", "归墟", "化物"],
            ["gourd_body3d.void_swallowing_gourd", "pearl_shell3d.void_lattice_shell", "tower_base3d.floating_cloud_base"],
            ["void_obsidian", "dark_steel"],
            r => Semantic(r, ArtifactMaterialTraits.Entropy, 1.35f) +
                 Semantic(r, ArtifactMaterialTraits.Neg, 0.65f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Devouring, 0.6f),
                Trait(ArtifactMaterialTraits.Storage, 0.4f),
                Trait(ArtifactMaterialTraits.Space, 0.45f),
                Trait(ArtifactMaterialTraits.Binding, 0.35f),
            ]);

        Set(GuardianHalo, "guardian_halo", ArtifactAtomCategory.Finish, ["护轮", "周天", "守一"],
            ["pearl_halo3d.solar_glyph_halo", "bell_crown3d.lotus_hook", "tower_eaves3d.octagonal_lotus_eaves"],
            ["moon_silver", "gold_jade"],
            r => Shape(r, ItemShapes.Lotus, ItemShapes.Crystal, ItemShapes.Shell, ItemShapes.Eye) +
                 Semantic(r, ArtifactMaterialTraits.Pos, 0.9f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Ward, 0.55f),
                Trait(ArtifactMaterialTraits.Projection, 0.4f),
                Trait(ArtifactMaterialTraits.GuardianWard, 0.55f),
                Trait(ArtifactMaterialTraits.Sustain, 0.4f),
            ]);

        Set(LayeredRealmMark, "layered_realm_mark", ArtifactAtomCategory.Finish, ["层界", "重天", "镇域"],
            ["tower_level3d.golden_scripture_chamber", "tower_eaves3d.suspended_bell_eaves", "banner_cloth3d.spirit_script_streamer"],
            ["azure_ceramic", "black_gold"],
            r => Shape(r, ItemShapes.Talisman, ItemShapes.Stone, ItemShapes.Crystal, ItemShapes.Root) +
                 Semantic(r, ArtifactMaterialTraits.Earth, 0.65f) +
                 Semantic(r, ArtifactMaterialTraits.Spirituality, 0.65f) + Quality(r, 2),
            [
                Trait(ArtifactMaterialTraits.FieldProjection, 0.65f),
                Trait(ArtifactMaterialTraits.Sealing, 0.45f),
                Trait(ArtifactMaterialTraits.Space, 0.4f),
                Trait(ArtifactMaterialTraits.Suppression, 0.45f),
            ]);

        Set(TransformationPattern, "transformation_pattern", ArtifactAtomCategory.Finish, ["化形", "幻变", "流转"],
            ["fan_leaf3d.crane_feather_leaf", "gourd_tie3d.vine_binding", "pearl_core3d.five_element_prism_pearl"],
            ["cloud_silk", "blood_jade"],
            r => Shape(r, ItemShapes.Liquid, ItemShapes.Vine, ItemShapes.Feather, ItemShapes.Blood) +
                 Semantic(r, ArtifactMaterialTraits.Entropy, 0.65f) +
                 Semantic(r, ArtifactMaterialTraits.Vitality, 0.55f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Transformation, 0.65f),
                Trait(ArtifactMaterialTraits.Projection, 0.35f),
                Trait(ArtifactMaterialTraits.Mobility, 0.35f),
                Trait(ArtifactMaterialTraits.Volatility, 0.25f),
            ]);

        Set(CloudRidingScript, "cloud_riding_script", ArtifactAtomCategory.Finish, ["御空", "乘云", "凌霄"],
            ["tower_base3d.floating_cloud_base", "fan_leaf3d.crane_feather_leaf", "banner_tassel3d.twin_silk"],
            ["cloud_silk", "moon_silver"],
            r => Shape(r, ItemShapes.Feather, ItemShapes.Wing, ItemShapes.Silk, ItemShapes.Wood, ItemShapes.Ball) +
                 Semantic(r, ArtifactMaterialTraits.Mobility, 0.9f) +
                 Semantic(r, ArtifactMaterialTraits.Space, 0.5f) + Quality(r, 1),
            [
                Trait(ArtifactMaterialTraits.Vehicle, 0.65f),
                Trait(ArtifactMaterialTraits.Mobility, 0.55f),
                Trait(ArtifactMaterialTraits.Projection, 0.4f),
                Trait(ArtifactMaterialTraits.Sustain, 0.35f),
            ]);

        Set(AncestralGuardianVow, "ancestral_guardian_vow", ArtifactAtomCategory.Finish,
                    ["镇宗", "祖誓", "山门"],
                    ["banner_cloth3d.cloud_war_banner", "tower_level3d.golden_scripture_chamber", "seal_crown3d.dragon_loop"],
                    ["imperial_bronze", "black_gold"],
                    r => Shape(r, ItemShapes.Talisman, ItemShapes.Stone, ItemShapes.Root, ItemShapes.Silk) +
                         Semantic(r, ArtifactMaterialTraits.Ward, 0.9f) + Quality(r, 2),
                    [
                        Trait(ArtifactMaterialTraits.SectGuardian, 0.85f),
                Trait(ArtifactMaterialTraits.Ward, 0.55f),
                Trait(ArtifactMaterialTraits.FieldProjection, 0.5f),
                Trait(ArtifactMaterialTraits.Sustain, 0.4f),
                    ]);

        Set(SpiritAwakeningScript, "spirit_awakening_script", ArtifactAtomCategory.Finish,
                    ["点灵", "启慧", "通幽"],
                    ["banner_tassel3d.spirit_bells", "pearl_halo3d.solar_glyph_halo", "bell_crown3d.heavenly_arch"],
                    ["moon_silver", "void_obsidian"],
                    r => Shape(r, ItemShapes.Talisman, ItemShapes.Eye, ItemShapes.Blood, ItemShapes.Lotus) +
                         Semantic(r, ArtifactMaterialTraits.Soul, 1.1f) +
                         Semantic(r, ArtifactMaterialTraits.Spirituality, 0.9f) + Quality(r, 2),
                    [
                        Trait(ArtifactMaterialTraits.ArtifactSpirit, 0.9f),
                Trait(ArtifactMaterialTraits.Soul, 0.65f),
                Trait(ArtifactMaterialTraits.Spirituality, 0.45f),
                Trait(ArtifactMaterialTraits.Perception, 0.3f),
                    ]);
    }
}

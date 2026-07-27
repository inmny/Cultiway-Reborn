using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using strings;

namespace Cultiway.Content;

/// <summary>金丹与元婴共享的可组合规则原子。</summary>
[Dependency(
    typeof(BaseStatses),
    typeof(CultivationSemantics),
    typeof(CoreFormationSkills),
    typeof(SkillCastResources),
    typeof(StatusEffects))]
public sealed class CoreFormationAtoms : ExtendLibrary<CoreFormationAtomAsset, CoreFormationAtoms>
{
    private const float StructureMinimumScore = 4f;

    /// <summary>由金元素占比决定权重的金行主相原子。</summary>
    public static CoreFormationAtomAsset ElementIron { get; private set; }

    /// <summary>由木元素占比决定权重的木行主相原子。</summary>
    public static CoreFormationAtomAsset ElementWood { get; private set; }

    /// <summary>由水元素占比决定权重的水行主相原子。</summary>
    public static CoreFormationAtomAsset ElementWater { get; private set; }

    /// <summary>由火元素占比决定权重的火行主相原子。</summary>
    public static CoreFormationAtomAsset ElementFire { get; private set; }

    /// <summary>由土元素占比决定权重的土行主相原子。</summary>
    public static CoreFormationAtomAsset ElementEarth { get; private set; }

    /// <summary>由阴元素占比决定权重的阴行主相原子。</summary>
    public static CoreFormationAtomAsset ElementYin { get; private set; }

    /// <summary>由阳元素占比决定权重的阳行主相原子。</summary>
    public static CoreFormationAtomAsset ElementYang { get; private set; }

    /// <summary>由混沌元素占比决定权重的混沌主相原子。</summary>
    public static CoreFormationAtomAsset ElementChaos { get; private set; }

    /// <summary>表达三花与诸气均衡共鸣的混元结构原子。</summary>
    public static CoreFormationAtomAsset StructureBalanced { get; private set; }

    /// <summary>表达以气凝聚灵力储量的凝元结构原子。</summary>
    public static CoreFormationAtomAsset StructureCondensed { get; private set; }

    /// <summary>表达以精强化体魄与防护的精元结构原子。</summary>
    public static CoreFormationAtomAsset StructureVital { get; private set; }

    /// <summary>表达以神强化感知与灵力运转的灵台结构原子。</summary>
    public static CoreFormationAtomAsset StructureSpiritual { get; private set; }

    /// <summary>由剑道语义凝成并强化攻伐的剑道烙印原子。</summary>
    public static CoreFormationAtomAsset PathSword { get; private set; }

    /// <summary>由炼体语义与精元根基凝成的炼体烙印原子。</summary>
    public static CoreFormationAtomAsset PathBody { get; private set; }

    /// <summary>由幻术语义凝成并偏向虚实隐匿的幻道烙印原子。</summary>
    public static CoreFormationAtomAsset PathIllusion { get; private set; }

    /// <summary>由蓄灵语义凝成并强化资源储备的灵渊烙印原子。</summary>
    public static CoreFormationAtomAsset PathReservoir { get; private set; }

    /// <summary>由龙族来源或固有龙性凝成的龙脉主题原子。</summary>
    public static CoreFormationAtomAsset ThemeDragon { get; private set; }

    /// <summary>结婴时形成通用稳定灵胎的基础显化原子。</summary>
    public static CoreFormationAtomAsset ManifestInfant { get; private set; }

    /// <summary>结婴时把剑道根基显化为剑胎的显化原子。</summary>
    public static CoreFormationAtomAsset ManifestSwordEmbryo { get; private set; }

    /// <summary>结婴时把龙性根基显化为龙相的显化原子。</summary>
    public static CoreFormationAtomAsset ManifestDragonAspect { get; private set; }

    /// <summary>结婴时把强盛神识显化为灵台的显化原子。</summary>
    public static CoreFormationAtomAsset ManifestSpiritPlatform { get; private set; }

    /// <summary>结婴时把雄厚精元显化为真身的显化原子。</summary>
    public static CoreFormationAtomAsset ManifestPrimalBody { get; private set; }

    /// <summary>均衡五气在结婴时形成完整循环的五相蜕变原子。</summary>
    public static CoreFormationAtomAsset TransformFivePhase { get; private set; }

    /// <summary>阳气达到显化条件后形成纯阳元神的蜕变原子。</summary>
    public static CoreFormationAtomAsset TransformPureYang { get; private set; }

    /// <summary>阴气达到显化条件后形成玄阴元神的蜕变原子。</summary>
    public static CoreFormationAtomAsset TransformMysteriousYin { get; private set; }

    /// <summary>混沌之气达到显化条件后引发归墟再生的蜕变原子。</summary>
    public static CoreFormationAtomAsset TransformChaos { get; private set; }

    /// <summary>允许基类从本类的静态资产属性自动创建并注册原子。</summary>
    protected override bool AutoRegisterAssets() => true;

    /// <summary>返回所有组合原子共享的稳定资产 ID 前缀。</summary>
    protected override string Prefix() => "Cultiway.CoreFormationAtom";

    /// <summary>为自动注册的原子配置图标、分类、评分、语义、属性和规则命名词干。</summary>
    protected override void OnInit()
    {
        SetElement(ElementIron, "iron", "cultiway/icons/element_root/iron",
            ["庚金", "玄锋", "锐金"], SkillSemantics.Element.Iron, 0);
        SetElement(ElementWood, "wood", "cultiway/icons/element_root/wood",
            ["青木", "长青", "苍灵"], SkillSemantics.Element.Wood, 1);
        SetElement(ElementWater, "water", "cultiway/icons/element_root/water",
            ["玄水", "沧溟", "寒泉"], SkillSemantics.Element.Water, 2);
        SetElement(ElementFire, "fire", "cultiway/icons/element_root/fire",
            ["离火", "赤炎", "焚阳"], SkillSemantics.Element.Fire, 3);
        SetElement(ElementEarth, "earth", "cultiway/icons/element_root/earth",
            ["坤岳", "厚土", "镇山"], SkillSemantics.Element.Earth, 4);
        SetElement(ElementYin, "yin", "cultiway/icons/element_root/neg",
            ["玄阴", "太阴", "幽玄"], SkillSemantics.Element.Neg, 5);
        SetElement(ElementYang, "yang", "cultiway/icons/element_root/pos",
            ["纯阳", "曜灵", "明光"], SkillSemantics.Element.Pos, 6);
        SetElement(ElementChaos, "chaos", "cultiway/icons/element_root/entropy",
            ["混沌", "归墟", "浊玄"], SkillSemantics.Element.Entropy, 7);

        Set(StructureBalanced, "balanced", "cultiway/icons/artifact_atoms/resonance_rings",
            CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["混元", "归一", "浑成"],
            ScoreBalancedStructure,
            Descriptor(CultivationSemantics.Material.Stability, CultivationSemantics.Effect.Resonance),
            Stats((S.multiplier_health, 0.08f), (S.multiplier_damage, 0.08f)), StructureMinimumScore);
        Set(StructureCondensed, "condensed", "cultiway/icons/artifact_atoms/spirit_gourd",
            CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["凝元", "抱一", "玄凝"],
            context => 1f + context.QiRatio * 5f + context.SemanticScore(CultivationSemantics.Resource.Reserve) * 2f,
            Descriptor(CultivationSemantics.Resource.Reserve, CultivationSemantics.Effect.Storage),
            Stats((BaseStatses.MaxWakan.id, 12f), (S.multiplier_damage, 0.05f)), StructureMinimumScore);
        Set(StructureVital, "vital", "cultiway/icons/artifact_atoms/life_pattern",
            CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["精元", "血魄", "真形"],
            context => 1f + context.JingRatio * 5f + context.SemanticScore(CultivationSemantics.Form.Body) * 2f,
            Descriptor(CultivationSemantics.Resource.Vitality, CultivationSemantics.Form.Body),
            Stats((S.multiplier_health, 0.16f), (S.armor, 1.5f)), StructureMinimumScore);
        Set(StructureSpiritual, "spiritual", "cultiway/icons/artifact_atoms/spirit_gathering_pattern",
            CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["灵台", "神凝", "照神"],
            context => 1f + context.ShenRatio * 5f + context.SemanticScore(CultivationSemantics.Resource.Spirituality) * 2f,
            Descriptor(CultivationSemantics.Resource.Spirituality, CultivationSemantics.Effect.Perception),
            Stats((BaseStatses.MaxWakan.id, 8f), (S.multiplier_crit, 0.06f)), StructureMinimumScore);

        Set(PathSword, "sword", "cultiway/icons/artifact_atoms/sword_swarm",
            CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["剑心", "剑魄", "玄剑"], context => context.SemanticScore(CultivationSemantics.Path.Sword) * 7f,
            Descriptor(CultivationSemantics.Path.Sword, CultivationSemantics.Form.Blade),
            Stats((S.multiplier_damage, 0.16f), (S.critical_chance, 0.05f)), 1f);
        Set(PathBody, "body", "cultiway/icons/artifact_atoms/vitality_robe",
            CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["真形", "道体", "玄躯"],
            context => context.SemanticScore(CultivationSemantics.Form.Body) * 5f + context.JingRatio,
            Descriptor(CultivationSemantics.Form.Body, CultivationSemantics.Resource.Vitality),
            Stats((S.multiplier_health, 0.18f), (S.armor, 2f)), 1f);
        Set(PathIllusion, "illusion", "cultiway/icons/artifact_atoms/void_mirror",
            CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["幻真", "蜃影", "虚灵"], context => context.SemanticScore(CultivationSemantics.Theme.Illusion) * 7f,
            Descriptor(CultivationSemantics.Theme.Illusion, CultivationSemantics.Effect.Concealment),
            Stats((S.multiplier_speed, 0.12f), (S.multiplier_attack_speed, 0.05f)), 1f);
        Set(PathReservoir, "reservoir", "cultiway/icons/artifact_atoms/spirit_ding",
            CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["元海", "灵渊", "纳元"],
            context => context.SemanticScore(CultivationSemantics.Craft.SpiritReservoir) * 6f + context.QiRatio * 0.5f,
            Descriptor(CultivationSemantics.Craft.SpiritReservoir, CultivationSemantics.Resource.Reserve),
            Stats((BaseStatses.MaxWakan.id, 18f)), 1f);
        Set(ThemeDragon, "dragon", "ui/icons/iconDragon",
            CoreFormationAtomCategory.Theme, CoreFormationRealmMask.All,
            ["龙脉", "龙魂", "苍龙"], context => context.IsDragonSource ? 8f : 0f,
            Descriptor(CultivationSemantics.Theme.Dragon, CultivationSemantics.Form.Body),
            Stats((S.multiplier_health, 0.12f), (S.multiplier_damage, 0.12f)), 2f);

        Set(ManifestInfant, "infant", "cultiway/ui/realm_pages/yuanying_base",
            CoreFormationAtomCategory.Manifestation, CoreFormationRealmMask.Yuanying,
            ["灵胎", "玄胎", "道胎"], _ => 1f,
            Descriptor(CultivationSemantics.Theme.Spirit, CultivationSemantics.Realm.Yuanying), []);
        Set(ManifestSwordEmbryo, "sword_embryo", "cultiway/icons/artifact_atoms/sword_edge",
            CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["剑胎", "剑魂", "剑魄"],
            context => context.SemanticScore(CultivationSemantics.Path.Sword) * 8f,
            Descriptor(CultivationSemantics.Path.Sword, CultivationSemantics.Form.Blade),
            Stats((S.multiplier_damage, 0.18f)), 1f);
        Set(ManifestDragonAspect, "dragon_aspect", "ui/icons/iconDragon",
            CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["龙相", "龙魂", "苍龙"], context => context.IsDragonSource ? 9f : 0f,
            Descriptor(CultivationSemantics.Theme.Dragon, CultivationSemantics.Effect.Transformation),
            Stats((S.multiplier_health, 0.15f), (S.armor, 2f)), 1f);
        Set(ManifestSpiritPlatform, "spirit_platform",
            "cultiway/icons/artifact_atoms/spirit_awakening_script",
            CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["灵台", "神魂", "天心"],
            context => 1f + context.ShenRatio * 4f +
                       context.SemanticScore(CultivationSemantics.Resource.Spirituality) * 3f,
            Descriptor(CultivationSemantics.Theme.Soul, CultivationSemantics.Resource.Spirituality),
            Stats((BaseStatses.MaxWakan.id, 16f), (S.multiplier_crit, 0.08f)), 3f);
        Set(ManifestPrimalBody, "primal_body", "cultiway/icons/artifact_atoms/vitality_robe",
            CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["真身", "法身", "道躯"],
            context => 1f + context.JingRatio * 4f + context.SemanticScore(CultivationSemantics.Form.Body) * 3f,
            Descriptor(CultivationSemantics.Form.Body, CultivationSemantics.Effect.Transformation),
            Stats((S.multiplier_health, 0.2f), (S.armor, 2.5f)), 3f);

        Set(TransformFivePhase, "five_phase", "cultiway/icons/artifact_atoms/element_pearl",
            CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["五气", "混元", "五相"],
            context => context.FivePhaseBalance * 6f,
            Descriptor(CultivationSemantics.Theme.Elemental, CultivationSemantics.Effect.Resonance), [], 2.2f);
        Set(TransformPureYang, "pure_yang", "cultiway/icons/element_root/pos",
            CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["阳神", "曜魂", "纯阳"], context => context.Composition.pos * 8f,
            Descriptor(SkillSemantics.Element.Pos, CultivationSemantics.Theme.Soul), [], 2.2f);
        Set(TransformMysteriousYin, "mysterious_yin", "cultiway/icons/element_root/neg",
            CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["阴神", "玄魂", "太阴"], context => context.Composition.neg * 8f,
            Descriptor(SkillSemantics.Element.Neg, CultivationSemantics.Theme.Soul), [], 2.2f);
        Set(TransformChaos, "chaos_rebirth", "cultiway/icons/element_root/entropy",
            CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["混沌", "归墟", "玄变"], context => context.Composition.entropy * 8f,
            Descriptor(SkillSemantics.Element.Entropy, CultivationSemantics.Effect.Transformation), [], 2.2f);

        ConfigureEffects();
    }

    /// <summary>把全部 26 个形成原子绑定到可合并、可触发的实际机制。</summary>
    private static void ConfigureEffects()
    {
        SetEffects(ElementIron, Effect(ElementIron, CoreFormationEffectFamilies.Iron, "iron", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.22f, 0.35f, 2.5f, CoreFormationEffectHandlers.Iron,
            CoreFormationSkills.IronSeverance));
        SetEffects(ElementWood, Effect(ElementWood, CoreFormationEffectFamilies.Wood, "wood", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.Kill,
            0.18f, 0.3f, 2.5f, CoreFormationEffectHandlers.Wood,
            CoreFormationSkills.WoodVenomBloom));
        SetEffects(ElementWater, Effect(ElementWater, CoreFormationEffectFamilies.Water, "water", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.2f, 0.32f, 3f, CoreFormationEffectHandlers.Water,
            CoreFormationSkills.WaterFrostBind));
        SetEffects(ElementFire, Effect(ElementFire, CoreFormationEffectFamilies.Fire, "fire", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.2f, 0.32f, 2f, CoreFormationEffectHandlers.Fire,
            CoreFormationSkills.FireBrand));
        SetEffects(ElementEarth, Effect(ElementEarth, CoreFormationEffectFamilies.Earth, "earth", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.FinalDamageIncoming,
            0.25f, 0.4f, 1f, CoreFormationEffectHandlers.Earth,
            CoreFormationSkills.EarthWard,
            statusAnimationPath: "cultiway/effect/core_formation/earth_ward_loop",
            statusAnimationScale: 0.09f,
            finalStage: FinalDamageStage.Shield));
        SetEffects(ElementYin, Effect(ElementYin, CoreFormationEffectFamilies.Yin, "yin", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.18f, 0.3f, 3f, CoreFormationEffectHandlers.Yin,
            CoreFormationSkills.YinDrain));
        SetEffects(ElementYang, Effect(ElementYang, CoreFormationEffectFamilies.Yang, "yang", 1,
            CoreFormationEffectTrigger.SkillCastCompleted, 0.25f, 0.4f, 5f, CoreFormationEffectHandlers.Yang,
            CoreFormationSkills.YangCleanse));
        SetEffects(ElementChaos, Effect(ElementChaos, CoreFormationEffectFamilies.Chaos, "chaos", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.15f, 0.25f, 3f, CoreFormationEffectHandlers.Chaos,
            CoreFormationSkills.ChaosEcho));

        SetEffects(StructureBalanced, Effect(StructureBalanced, CoreFormationEffectFamilies.Balanced, "balanced", 1,
            CoreFormationEffectTrigger.FinalDamageIncoming, 0.25f, 0.4f, 1f,
            CoreFormationEffectHandlers.Balanced,
            CoreFormationSkills.BalancedAdaptation,
            finalStage: FinalDamageStage.Adaptation));
        SetEffects(StructureCondensed, Effect(StructureCondensed, CoreFormationEffectFamilies.Condensed,
            "condensed", 1, CoreFormationEffectTrigger.SkillCastCompleted | CoreFormationEffectTrigger.DamageDealt,
            0.25f, 0.4f, 3f, CoreFormationEffectHandlers.Condensed,
            CoreFormationSkills.CondensedRelease,
            statusAnimationPath: "cultiway/effect/core_formation/reservoir_orb_loop",
            statusAnimationScale: 0.075f));
        SetEffects(StructureVital, Effect(StructureVital, CoreFormationEffectFamilies.Vital, "vital", 1,
            CoreFormationEffectTrigger.DamageTaken | CoreFormationEffectTrigger.Tick,
            1f, 1f, 0f, CoreFormationEffectHandlers.Vital));
        SetEffects(StructureSpiritual, Effect(StructureSpiritual, CoreFormationEffectFamilies.Spiritual,
            "spiritual", 1, CoreFormationEffectTrigger.SkillCastCompleted,
            0.2f, 0.35f, 5f, CoreFormationEffectHandlers.Spiritual,
            CoreFormationSkills.SpiritEcho));

        SetEffects(PathSword, Effect(PathSword, CoreFormationEffectFamilies.Sword, "sword", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.2f, 0.35f, 2f, CoreFormationEffectHandlers.Sword,
            CoreFormationSkills.SwordChase));
        SetEffects(PathBody, Effect(PathBody, CoreFormationEffectFamilies.Body, "body", 1,
            CoreFormationEffectTrigger.DamageTaken, 0.25f, 0.4f, 4f, CoreFormationEffectHandlers.Body,
            CoreFormationSkills.BodyCounter));
        SetEffects(PathIllusion, Effect(PathIllusion, CoreFormationEffectFamilies.Illusion, "illusion", 1,
            CoreFormationEffectTrigger.FinalDamageIncoming, 0.2f, 0.3f, 8f,
            CoreFormationEffectHandlers.Illusion,
            CoreFormationSkills.IllusionDecoy,
            finalStage: FinalDamageStage.Avoidance));
        SetEffects(PathReservoir, Effect(PathReservoir, CoreFormationEffectFamilies.Reservoir, "reservoir", 1,
            CoreFormationEffectTrigger.Tick, 1f, 1f, 0f, CoreFormationEffectHandlers.Reservoir,
            statusAnimationPath: "cultiway/effect/core_formation/reservoir_orb_loop",
            statusAnimationScale: 0.075f));
        SetEffects(ThemeDragon, Effect(ThemeDragon, CoreFormationEffectFamilies.Dragon, "dragon", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.DamageTaken,
            0.3f, 0.45f, 8f, CoreFormationEffectHandlers.Dragon,
            CoreFormationSkills.DragonMight));

        SetEffects(ManifestInfant, Effect(ManifestInfant, CoreFormationEffectFamilies.Survival, "infant", 1,
            CoreFormationEffectTrigger.FinalDamageIncoming, 1f, 1f, 60f,
            CoreFormationEffectHandlers.Survival,
            CoreFormationSkills.InfantGuard,
            finalStage: FinalDamageStage.Survival));
        SetEffects(ManifestSwordEmbryo, Effect(ManifestSwordEmbryo, CoreFormationEffectFamilies.Sword,
            "sword_embryo", 2, CoreFormationEffectTrigger.DamageDealt,
            0.2f, 0.35f, 2f, CoreFormationEffectHandlers.Sword,
            CoreFormationSkills.SwordEmbryoStrike,
            statusAnimationPath: "cultiway/effect/core_formation/sword_embryo_aura_loop",
            statusAnimationScale: 0.1f,
            active: Active("sword_embryo", "cultiway/icons/element_root/iron", 6f, 15f,
                0f, 0f, ActiveAbilityTargetMode.Self, 28, CoreFormationSkills.SwordEmbryoAura,
                CoreFormationEffectHandlers.PrepareCombatBuff)));
        SetEffects(ManifestDragonAspect, Effect(ManifestDragonAspect, CoreFormationEffectFamilies.Dragon,
            "dragon_aspect", 2,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.DamageTaken,
            0.3f, 0.45f, 8f, CoreFormationEffectHandlers.Dragon,
            CoreFormationSkills.DragonAspectMight,
            active: Active("dragon_aspect", "cultiway/icons/element_root/earth", 0f, 15f,
                12f, 4f, ActiveAbilityTargetMode.Area, 32, CoreFormationSkills.DragonAspectBurst,
                CoreFormationEffectHandlers.PrepareCombatBuff)));
        SetEffects(ManifestSpiritPlatform, Effect(ManifestSpiritPlatform, CoreFormationEffectFamilies.Spiritual,
            "spirit_platform", 2, CoreFormationEffectTrigger.SkillCastCompleted,
            0.2f, 0.35f, 5f, CoreFormationEffectHandlers.Spiritual,
            CoreFormationSkills.SpiritEcho,
            statusAnimationPath: "cultiway/effect/core_formation/spirit_platform_loop",
            statusAnimationScale: 0.11f,
            active: Active("spirit_platform", "cultiway/icons/iconWakan", 8f, 20f,
                0f, 0f, ActiveAbilityTargetMode.Self, 26, CoreFormationSkills.SpiritPlatform,
                CoreFormationEffectHandlers.PrepareCombatBuff)));
        SetEffects(ManifestPrimalBody, Effect(ManifestPrimalBody, CoreFormationEffectFamilies.Body,
            "primal_body", 2,
            CoreFormationEffectTrigger.DamageTaken | CoreFormationEffectTrigger.FinalDamageIncoming,
            0.25f, 0.4f, 4f, CoreFormationEffectHandlers.Body,
            CoreFormationSkills.PrimalBodyCounter,
            statusAnimationPath: "cultiway/effect/core_formation/primal_body_loop",
            statusAnimationScale: 0.1f,
            finalStage: FinalDamageStage.Cap,
            active: Active("primal_body", "cultiway/icons/element_root/earth", 8f, 20f,
                0f, 0f, ActiveAbilityTargetMode.Self, 30, CoreFormationSkills.PrimalBody,
                CoreFormationEffectHandlers.PrepareCombatBuff)));

        SetEffects(TransformFivePhase, Effect(TransformFivePhase, CoreFormationEffectFamilies.FivePhase,
            "five_phase", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.FinalDamageIncoming |
            CoreFormationEffectTrigger.Tick,
            1f, 1f, 0f, CoreFormationEffectHandlers.FivePhase,
            CoreFormationSkills.FivePhaseStrike,
            statusAnimationPath: "cultiway/effect/core_formation/five_phase_loop",
            statusAnimationScale: 0.12f,
            finalStage: FinalDamageStage.Adaptation,
            active: Active("five_phase", "cultiway/icons/element_root/entropy", 10f, 18f,
                0f, 0f, ActiveAbilityTargetMode.Self, 30, CoreFormationSkills.FivePhase,
                CoreFormationEffectHandlers.PrepareCombatBuff)));
        SetEffects(TransformPureYang, Effect(TransformPureYang, CoreFormationEffectFamilies.Yang,
            "pure_yang", 2, CoreFormationEffectTrigger.SkillCastCompleted,
            0.25f, 0.4f, 5f, CoreFormationEffectHandlers.Yang,
            CoreFormationSkills.PureYangCleanse,
            active: Active("pure_yang", "cultiway/icons/element_root/pos", 0f, 18f,
                10f, 5f, ActiveAbilityTargetMode.Area, 34, CoreFormationSkills.PureYangDomain,
                CoreFormationEffectHandlers.PrepareCombatBuff)));
        SetEffects(TransformMysteriousYin, Effect(TransformMysteriousYin, CoreFormationEffectFamilies.Yin,
            "mysterious_yin", 2, CoreFormationEffectTrigger.DamageDealt,
            0.18f, 0.3f, 3f, CoreFormationEffectHandlers.Yin,
            CoreFormationSkills.MysteriousYinDrain,
            active: Active("mysterious_yin", "cultiway/icons/element_root/neg", 0f, 18f,
                12f, 5f, ActiveAbilityTargetMode.Area, 34, CoreFormationSkills.MysteriousYinDomain,
                CoreFormationEffectHandlers.PrepareCombatBuff)));
        SetEffects(TransformChaos,
            Effect(TransformChaos, CoreFormationEffectFamilies.Chaos, "chaos_rebirth", 2,
                CoreFormationEffectTrigger.DamageDealt, 0.22f, 0.35f, 2f,
                CoreFormationEffectHandlers.Chaos,
                CoreFormationSkills.ChaosRebirthEcho),
            Effect(TransformChaos, CoreFormationEffectFamilies.Survival, "chaos_rebirth_survival", 2,
                CoreFormationEffectTrigger.FinalDamageIncoming, 1f, 1f, 120f,
                CoreFormationEffectHandlers.Survival,
                CoreFormationSkills.ChaosRebirth,
                finalStage: FinalDamageStage.Survival));
    }

    /// <summary>把一组机制定义写入指定形成原子。</summary>
    private static void SetEffects(CoreFormationAtomAsset atom, params CoreFormationEffectDefinition[] effects)
    {
        atom.effects = effects ?? [];
    }

    /// <summary>构造一项完整效果定义，并按原子分类写入倍率参考权重。</summary>
    private static CoreFormationEffectDefinition Effect(
        CoreFormationAtomAsset atom,
        string familyId,
        string key,
        int rank,
        CoreFormationEffectTrigger triggers,
        float baseChance,
        float maxChance,
        float cooldown,
        CoreFormationEffectHandler handler,
        Entity triggerSkill = default,
        Entity cooldownSkill = default,
        string statusAnimationPath = null,
        float statusAnimationScale = 0.1f,
        FinalDamageStage finalStage = FinalDamageStage.Adaptation,
        CoreFormationActiveProfile active = null)
    {
        var definition = new CoreFormationEffectDefinition
        {
            family_id = familyId,
            rank = rank,
            triggers = triggers,
            base_chance = baseChance,
            max_chance = maxChance,
            cooldown = cooldown,
            reference_weight = ReferenceWeight(atom.category),
            name_key = $"Cultiway.CoreFormationEffect.{key}.Name",
            description_key = $"Cultiway.CoreFormationEffect.{key}.Description",
            final_damage_stage = finalStage,
            active = active,
            TriggerSkill = triggerSkill,
            CooldownSkill = cooldownSkill.IsNull ? triggerSkill : cooldownSkill,
            Handle = handler,
        };
        definition.StateStatus = CoreFormationStatusFactory.Build(
            key,
            definition,
            active,
            statusAnimationPath,
            statusAnimationScale);
        return definition;
    }

    /// <summary>构造一个使用固定灵气消耗的主动能力配置。</summary>
    private static CoreFormationActiveProfile Active(
        string key,
        string iconPath,
        float duration,
        float cooldown,
        float range,
        float radius,
        ActiveAbilityTargetMode targetMode,
        int aiWeight,
        Entity skillContainer,
        CoreFormationActivePrepareAction prepare)
    {
        return new CoreFormationActiveProfile
        {
            name_key = $"Cultiway.CoreFormationEffect.{key}.Active.Name",
            icon_path = iconPath,
            duration = duration,
            cooldown = cooldown,
            range = range,
            radius = radius,
            target_mode = targetMode,
            ai_weight = aiWeight,
            CanPrepare = prepare,
            SkillContainer = skillContainer,
        };
    }

    /// <summary>返回不同原子分类参与效果倍率计算时的基准权重。</summary>
    private static float ReferenceWeight(CoreFormationAtomCategory category)
    {
        return category switch
        {
            CoreFormationAtomCategory.Element => 5f,
            CoreFormationAtomCategory.Structure => 8f,
            CoreFormationAtomCategory.Path => 7f,
            CoreFormationAtomCategory.Theme => 8f,
            CoreFormationAtomCategory.Manifestation => 8f,
            CoreFormationAtomCategory.Transformation => 6f,
            _ => 1f,
        };
    }

    /// <summary>仅在元素与三花同时均衡时提高混元结构评分，避免任一维度单独补偿另一维度。</summary>
    private static float ScoreBalancedStructure(CoreFormationContext context)
    {
        float jointBalance = context.ElementBalance * context.ThreeHuaBalance;
        return 1f + jointBalance * jointBalance * 10f;
    }

    /// <summary>按指定图标与元素槽位配置一个由元素占比直接评分的元素原子。</summary>
    private static void SetElement(CoreFormationAtomAsset atom, string key, string iconPath,
                                   string[] stems, SemanticAsset semantic, int elementIndex)
    {
        Set(atom, key, iconPath, CoreFormationAtomCategory.Element, CoreFormationRealmMask.All, stems,
            context => context.Composition[elementIndex] * 10f,
            Descriptor(semantic, CultivationSemantics.Theme.Elemental), [], 0.01f);
    }

    /// <summary>把显式图标和一套完整的选择规则、派生效果写入指定组合原子资产。</summary>
    private static void Set(CoreFormationAtomAsset atom, string key, string iconPath,
                            CoreFormationAtomCategory category, CoreFormationRealmMask realms,
                            string[] stems, Func<CoreFormationContext, float> score,
                            SemanticDescriptor semantics, CoreFormationStatValue[] stats, float minimumScore = 0f)
    {
        atom.category = category;
        atom.realms = realms;
        atom.name_key = $"Cultiway.CoreFormationAtom.{key}.Name";
        atom.description_key = $"Cultiway.CoreFormationAtom.{key}.Description";
        atom.icon_path = iconPath;
        atom.name_stems = stems;
        atom.ScoreContext = score;
        atom.semantics = semantics;
        atom.stats = stats;
        atom.minimum_score = minimumScore;
        atom.priority = 100;
    }

    /// <summary>把语义资产数组转换为原子可直接保存的规范语义描述。</summary>
    private static SemanticDescriptor Descriptor(params SemanticAsset[] semantics)
    {
        return SemanticDescriptor.Of(semantics);
    }

    /// <summary>把便于声明的属性元组转换为组合快照使用的属性系数数组。</summary>
    private static CoreFormationStatValue[] Stats(params (string id, float value)[] values)
    {
        var result = new CoreFormationStatValue[values.Length];
        for (var i = 0; i < values.Length; i++)
            result[i] = new CoreFormationStatValue(values[i].id, values[i].value);
        return result;
    }
}

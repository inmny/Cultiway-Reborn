using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Semantics;
using strings;

namespace Cultiway.Content;

/// <summary>金丹与元婴共享的可组合规则原子。</summary>
[Dependency(typeof(BaseStatses), typeof(CultivationSemantics))]
public sealed class CoreFormationAtoms : ExtendLibrary<CoreFormationAtomAsset, CoreFormationAtoms>
{
    private const float StructureMinimumScore = 4f;
    private const float VisualFrameInterval = 0.05f;
    private const float VisualOneShotLifeTime = 0.65f;

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

    /// <summary>为自动注册的原子配置分类、评分、语义、属性和规则命名词干。</summary>
    protected override void OnInit()
    {
        SetElement(ElementIron, "iron", ["庚金", "玄锋", "锐金"], SkillSemantics.Element.Iron, 0);
        SetElement(ElementWood, "wood", ["青木", "长青", "苍灵"], SkillSemantics.Element.Wood, 1);
        SetElement(ElementWater, "water", ["玄水", "沧溟", "寒泉"], SkillSemantics.Element.Water, 2);
        SetElement(ElementFire, "fire", ["离火", "赤炎", "焚阳"], SkillSemantics.Element.Fire, 3);
        SetElement(ElementEarth, "earth", ["坤岳", "厚土", "镇山"], SkillSemantics.Element.Earth, 4);
        SetElement(ElementYin, "yin", ["玄阴", "太阴", "幽玄"], SkillSemantics.Element.Neg, 5);
        SetElement(ElementYang, "yang", ["纯阳", "曜灵", "明光"], SkillSemantics.Element.Pos, 6);
        SetElement(ElementChaos, "chaos", ["混沌", "归墟", "浊玄"], SkillSemantics.Element.Entropy, 7);

        Set(StructureBalanced, "balanced", CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["混元", "归一", "浑成"],
            ScoreBalancedStructure,
            Descriptor(CultivationSemantics.Material.Stability, CultivationSemantics.Effect.Resonance),
            Stats((S.multiplier_health, 0.08f), (S.multiplier_damage, 0.08f)), StructureMinimumScore);
        Set(StructureCondensed, "condensed", CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["凝元", "抱一", "玄凝"],
            context => 1f + context.QiRatio * 5f + context.SemanticScore(CultivationSemantics.Resource.Reserve) * 2f,
            Descriptor(CultivationSemantics.Resource.Reserve, CultivationSemantics.Effect.Storage),
            Stats((BaseStatses.MaxWakan.id, 12f), (S.multiplier_damage, 0.05f)), StructureMinimumScore);
        Set(StructureVital, "vital", CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["精元", "血魄", "真形"],
            context => 1f + context.JingRatio * 5f + context.SemanticScore(CultivationSemantics.Form.Body) * 2f,
            Descriptor(CultivationSemantics.Resource.Vitality, CultivationSemantics.Form.Body),
            Stats((S.multiplier_health, 0.16f), (S.armor, 1.5f)), StructureMinimumScore);
        Set(StructureSpiritual, "spiritual", CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["灵台", "神凝", "照神"],
            context => 1f + context.ShenRatio * 5f + context.SemanticScore(CultivationSemantics.Resource.Spirituality) * 2f,
            Descriptor(CultivationSemantics.Resource.Spirituality, CultivationSemantics.Effect.Perception),
            Stats((BaseStatses.MaxWakan.id, 8f), (S.multiplier_crit, 0.06f)), StructureMinimumScore);

        Set(PathSword, "sword", CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["剑心", "剑魄", "玄剑"], context => context.SemanticScore(CultivationSemantics.Path.Sword) * 7f,
            Descriptor(CultivationSemantics.Path.Sword, CultivationSemantics.Form.Blade),
            Stats((S.multiplier_damage, 0.16f), (S.critical_chance, 0.05f)), 1f);
        Set(PathBody, "body", CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["真形", "道体", "玄躯"],
            context => context.SemanticScore(CultivationSemantics.Form.Body) * 5f + context.JingRatio,
            Descriptor(CultivationSemantics.Form.Body, CultivationSemantics.Resource.Vitality),
            Stats((S.multiplier_health, 0.18f), (S.armor, 2f)), 1f);
        Set(PathIllusion, "illusion", CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["幻真", "蜃影", "虚灵"], context => context.SemanticScore(CultivationSemantics.Theme.Illusion) * 7f,
            Descriptor(CultivationSemantics.Theme.Illusion, CultivationSemantics.Effect.Concealment),
            Stats((S.multiplier_speed, 0.12f), (S.multiplier_attack_speed, 0.05f)), 1f);
        Set(PathReservoir, "reservoir", CoreFormationAtomCategory.Path, CoreFormationRealmMask.All,
            ["元海", "灵渊", "纳元"],
            context => context.SemanticScore(CultivationSemantics.Craft.SpiritReservoir) * 6f + context.QiRatio * 0.5f,
            Descriptor(CultivationSemantics.Craft.SpiritReservoir, CultivationSemantics.Resource.Reserve),
            Stats((BaseStatses.MaxWakan.id, 18f)), 1f);
        Set(ThemeDragon, "dragon", CoreFormationAtomCategory.Theme, CoreFormationRealmMask.All,
            ["龙脉", "龙魂", "苍龙"], context => context.IsDragonSource ? 8f : 0f,
            Descriptor(CultivationSemantics.Theme.Dragon, CultivationSemantics.Form.Body),
            Stats((S.multiplier_health, 0.12f), (S.multiplier_damage, 0.12f)), 2f);

        Set(ManifestInfant, "infant", CoreFormationAtomCategory.Manifestation, CoreFormationRealmMask.Yuanying,
            ["灵胎", "玄胎", "道胎"], _ => 1f,
            Descriptor(CultivationSemantics.Theme.Spirit, CultivationSemantics.Realm.Yuanying), []);
        Set(ManifestSwordEmbryo, "sword_embryo", CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["剑胎", "剑魂", "剑魄"],
            context => context.SemanticScore(CultivationSemantics.Path.Sword) * 8f,
            Descriptor(CultivationSemantics.Path.Sword, CultivationSemantics.Form.Blade),
            Stats((S.multiplier_damage, 0.18f)), 1f);
        Set(ManifestDragonAspect, "dragon_aspect", CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["龙相", "龙魂", "苍龙"], context => context.IsDragonSource ? 9f : 0f,
            Descriptor(CultivationSemantics.Theme.Dragon, CultivationSemantics.Effect.Transformation),
            Stats((S.multiplier_health, 0.15f), (S.armor, 2f)), 1f);
        Set(ManifestSpiritPlatform, "spirit_platform", CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["灵台", "神魂", "天心"],
            context => 1f + context.ShenRatio * 4f +
                       context.SemanticScore(CultivationSemantics.Resource.Spirituality) * 3f,
            Descriptor(CultivationSemantics.Theme.Soul, CultivationSemantics.Resource.Spirituality),
            Stats((BaseStatses.MaxWakan.id, 16f), (S.multiplier_crit, 0.08f)), 3f);
        Set(ManifestPrimalBody, "primal_body", CoreFormationAtomCategory.Manifestation,
            CoreFormationRealmMask.Yuanying, ["真身", "法身", "道躯"],
            context => 1f + context.JingRatio * 4f + context.SemanticScore(CultivationSemantics.Form.Body) * 3f,
            Descriptor(CultivationSemantics.Form.Body, CultivationSemantics.Effect.Transformation),
            Stats((S.multiplier_health, 0.2f), (S.armor, 2.5f)), 3f);

        Set(TransformFivePhase, "five_phase", CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["五气", "混元", "五相"],
            context => context.FivePhaseBalance * 6f,
            Descriptor(CultivationSemantics.Theme.Elemental, CultivationSemantics.Effect.Resonance), [], 2.2f);
        Set(TransformPureYang, "pure_yang", CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["阳神", "曜魂", "纯阳"], context => context.Composition.pos * 8f,
            Descriptor(SkillSemantics.Element.Pos, CultivationSemantics.Theme.Soul), [], 2.2f);
        Set(TransformMysteriousYin, "mysterious_yin", CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["阴神", "玄魂", "太阴"], context => context.Composition.neg * 8f,
            Descriptor(SkillSemantics.Element.Neg, CultivationSemantics.Theme.Soul), [], 2.2f);
        Set(TransformChaos, "chaos_rebirth", CoreFormationAtomCategory.Transformation,
            CoreFormationRealmMask.Yuanying, ["混沌", "归墟", "玄变"], context => context.Composition.entropy * 8f,
            Descriptor(SkillSemantics.Element.Entropy, CultivationSemantics.Effect.Transformation), [], 2.2f);

        ConfigureEffects();
    }

    /// <summary>把全部 26 个形成原子绑定到可合并、可触发的实际机制。</summary>
    private static void ConfigureEffects()
    {
        SetEffects(ElementIron, Effect(ElementIron, CoreFormationEffectFamilies.Iron, "iron", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.22f, 0.35f, 2.5f, CoreFormationEffectHandlers.Iron,
            Visual(hit: Cue("cultiway/effect/core_formation/iron_severance", 0.09f,
                fixedUpright: false))));
        SetEffects(ElementWood, Effect(ElementWood, CoreFormationEffectFamilies.Wood, "wood", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.Kill,
            0.18f, 0.3f, 2.5f, CoreFormationEffectHandlers.Wood,
            Visual(trigger: Cue("cultiway/effect/core_formation/wood_life_return", 0.08f),
                hit: Cue("cultiway/effect/core_formation/wood_venom_bloom", 0.09f))));
        SetEffects(ElementWater, Effect(ElementWater, CoreFormationEffectFamilies.Water, "water", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.2f, 0.32f, 3f, CoreFormationEffectHandlers.Water,
            Visual(hit: Cue("cultiway/effect/core_formation/water_frost_bind", 0.09f))));
        SetEffects(ElementFire, Effect(ElementFire, CoreFormationEffectFamilies.Fire, "fire", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.2f, 0.32f, 2f, CoreFormationEffectHandlers.Fire,
            Visual(apply: Cue("cultiway/effect/core_formation/fire_brand", 0.075f),
                hit: Cue("cultiway/effect/core_formation/fire_ember_burst", 0.1f))));
        SetEffects(ElementEarth, Effect(ElementEarth, CoreFormationEffectFamilies.Earth, "earth", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.FinalDamageIncoming,
            0.25f, 0.4f, 1f, CoreFormationEffectHandlers.Earth,
            Visual(hit: Cue("cultiway/effect/core_formation/earth_ward_impact", 0.085f),
                charge: Cue("cultiway/effect/core_formation/earth_ward", 0.09f),
                loop: Cue("cultiway/effect/core_formation/earth_ward_loop", 0.09f, loop: true,
                    lifeTime: 2f)),
            FinalDamageStage.Shield));
        SetEffects(ElementYin, Effect(ElementYin, CoreFormationEffectFamilies.Yin, "yin", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.18f, 0.3f, 3f, CoreFormationEffectHandlers.Yin,
            Visual(hit: Cue("cultiway/effect/core_formation/yin_drain", 0.1f, fixedUpright: false,
                motion: CoreFormationVisualMotion.Linear))));
        SetEffects(ElementYang, Effect(ElementYang, CoreFormationEffectFamilies.Yang, "yang", 1,
            CoreFormationEffectTrigger.SkillCastCompleted, 0.25f, 0.4f, 5f, CoreFormationEffectHandlers.Yang,
            Visual(trigger: Cue("cultiway/effect/core_formation/yang_cleanse", 0.1f))));
        SetEffects(ElementChaos, Effect(ElementChaos, CoreFormationEffectFamilies.Chaos, "chaos", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.15f, 0.25f, 3f, CoreFormationEffectHandlers.Chaos,
            Visual(hit: Cue("cultiway/effect/core_formation/chaos_echo", 0.1f))));

        SetEffects(StructureBalanced, Effect(StructureBalanced, CoreFormationEffectFamilies.Balanced, "balanced", 1,
            CoreFormationEffectTrigger.FinalDamageIncoming, 0.25f, 0.4f, 1f,
            CoreFormationEffectHandlers.Balanced,
            Visual(charge: Cue("cultiway/effect/core_formation/balanced_adaptation", 0.09f)),
            FinalDamageStage.Adaptation));
        SetEffects(StructureCondensed, Effect(StructureCondensed, CoreFormationEffectFamilies.Condensed,
            "condensed", 1, CoreFormationEffectTrigger.SkillCastCompleted | CoreFormationEffectTrigger.DamageDealt,
            0.25f, 0.4f, 3f, CoreFormationEffectHandlers.Condensed,
            Visual(charge: Cue("cultiway/effect/core_formation/reservoir_orb", 0.08f),
                hit: Cue("cultiway/effect/core_formation/condensed_release", 0.09f),
                loop: Cue("cultiway/effect/core_formation/reservoir_orb_loop", 0.075f, loop: true,
                    lifeTime: 2f))));
        SetEffects(StructureVital, Effect(StructureVital, CoreFormationEffectFamilies.Vital, "vital", 1,
            CoreFormationEffectTrigger.DamageTaken | CoreFormationEffectTrigger.Tick,
            1f, 1f, 0f, CoreFormationEffectHandlers.Vital));
        SetEffects(StructureSpiritual, Effect(StructureSpiritual, CoreFormationEffectFamilies.Spiritual,
            "spiritual", 1, CoreFormationEffectTrigger.SkillCastCompleted,
            0.2f, 0.35f, 5f, CoreFormationEffectHandlers.Spiritual,
            Visual(trigger: Cue("cultiway/effect/core_formation/spirit_echo", 0.08f))));

        SetEffects(PathSword, Effect(PathSword, CoreFormationEffectFamilies.Sword, "sword", 1,
            CoreFormationEffectTrigger.DamageDealt, 0.2f, 0.35f, 2f, CoreFormationEffectHandlers.Sword,
            Visual(hit: Cue("cultiway/effect/core_formation/sword_chase", 0.09f,
                fixedUpright: false, motion: CoreFormationVisualMotion.Linear))));
        SetEffects(PathBody, Effect(PathBody, CoreFormationEffectFamilies.Body, "body", 1,
            CoreFormationEffectTrigger.DamageTaken, 0.25f, 0.4f, 4f, CoreFormationEffectHandlers.Body,
            Visual(hit: Cue("cultiway/effect/core_formation/body_counter", 0.09f))));
        SetEffects(PathIllusion, Effect(PathIllusion, CoreFormationEffectFamilies.Illusion, "illusion", 1,
            CoreFormationEffectTrigger.FinalDamageIncoming, 0.2f, 0.3f, 8f,
            CoreFormationEffectHandlers.Illusion,
            Visual(trigger: Cue("cultiway/effect/core_formation/illusion_decoy", 0.1f)),
            FinalDamageStage.Avoidance));
        SetEffects(PathReservoir, Effect(PathReservoir, CoreFormationEffectFamilies.Reservoir, "reservoir", 1,
            CoreFormationEffectTrigger.Tick, 1f, 1f, 0f, CoreFormationEffectHandlers.Reservoir,
            Visual(loop: Cue("cultiway/effect/core_formation/reservoir_orb_loop", 0.075f, loop: true,
                lifeTime: 2f))));
        SetEffects(ThemeDragon, Effect(ThemeDragon, CoreFormationEffectFamilies.Dragon, "dragon", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.DamageTaken,
            0.3f, 0.45f, 8f, CoreFormationEffectHandlers.Dragon,
            Visual(trigger: Cue("cultiway/effect/core_formation/dragon_might", 0.11f))));

        SetEffects(ManifestInfant, Effect(ManifestInfant, CoreFormationEffectFamilies.Survival, "infant", 1,
            CoreFormationEffectTrigger.FinalDamageIncoming, 1f, 1f, 60f,
            CoreFormationEffectHandlers.Survival,
            Visual(rebirth: Cue("cultiway/effect/core_formation/infant_guard", 0.1f)),
            FinalDamageStage.Survival));
        SetEffects(ManifestSwordEmbryo, Effect(ManifestSwordEmbryo, CoreFormationEffectFamilies.Sword,
            "sword_embryo", 2, CoreFormationEffectTrigger.DamageDealt,
            0.2f, 0.35f, 2f, CoreFormationEffectHandlers.Sword,
            Visual(hit: Cue("cultiway/effect/core_formation/sword_embryo_strike", 0.11f,
                fixedUpright: false, motion: CoreFormationVisualMotion.Linear),
                activate: Cue("cultiway/effect/core_formation/sword_embryo_aura", 0.1f),
                loop: Cue("cultiway/effect/core_formation/sword_embryo_aura_loop", 0.1f, loop: true,
                    lifeTime: 2f)),
            active: Active("sword_embryo", "cultiway/icons/element_root/iron", 32f, 6f, 15f,
                0f, 0f, ActiveAbilityTargetMode.Self, 28,
                CoreFormationEffectHandlers.PrepareCombatBuff, CoreFormationEffectHandlers.ActivateSwordEmbryo)));
        SetEffects(ManifestDragonAspect, Effect(ManifestDragonAspect, CoreFormationEffectFamilies.Dragon,
            "dragon_aspect", 2,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.DamageTaken,
            0.3f, 0.45f, 8f, CoreFormationEffectHandlers.Dragon,
            Visual(trigger: Cue("cultiway/effect/core_formation/dragon_might", 0.13f),
                activate: Cue("cultiway/effect/core_formation/dragon_aspect_burst", 0.16f)),
            active: Active("dragon_aspect", "cultiway/icons/element_root/earth", 40f, 0f, 15f,
                12f, 4f, ActiveAbilityTargetMode.Area, 32,
                CoreFormationEffectHandlers.PrepareCombatBuff, CoreFormationEffectHandlers.ActivateDragonAspect)));
        SetEffects(ManifestSpiritPlatform, Effect(ManifestSpiritPlatform, CoreFormationEffectFamilies.Spiritual,
            "spirit_platform", 2, CoreFormationEffectTrigger.SkillCastCompleted,
            0.2f, 0.35f, 5f, CoreFormationEffectHandlers.Spiritual,
            Visual(trigger: Cue("cultiway/effect/core_formation/spirit_echo", 0.1f),
                activate: Cue("cultiway/effect/core_formation/spirit_platform", 0.13f),
                loop: Cue("cultiway/effect/core_formation/spirit_platform_loop", 0.11f, loop: true,
                    lifeTime: 2f)),
            active: Active("spirit_platform", "cultiway/icons/iconWakan", 48f, 8f, 20f,
                0f, 0f, ActiveAbilityTargetMode.Self, 26,
                CoreFormationEffectHandlers.PrepareCombatBuff, CoreFormationEffectHandlers.ActivateSpiritPlatform)));
        SetEffects(ManifestPrimalBody, Effect(ManifestPrimalBody, CoreFormationEffectFamilies.Body,
            "primal_body", 2,
            CoreFormationEffectTrigger.DamageTaken | CoreFormationEffectTrigger.FinalDamageIncoming,
            0.25f, 0.4f, 4f, CoreFormationEffectHandlers.Body,
            Visual(hit: Cue("cultiway/effect/core_formation/primal_body_counter", 0.11f),
                activate: Cue("cultiway/effect/core_formation/primal_body", 0.12f),
                loop: Cue("cultiway/effect/core_formation/primal_body_loop", 0.1f, loop: true,
                    lifeTime: 2f)),
            FinalDamageStage.Cap,
            Active("primal_body", "cultiway/icons/element_root/earth", 40f, 8f, 20f,
                0f, 0f, ActiveAbilityTargetMode.Self, 30,
                CoreFormationEffectHandlers.PrepareCombatBuff, CoreFormationEffectHandlers.ActivatePrimalBody)));

        SetEffects(TransformFivePhase, Effect(TransformFivePhase, CoreFormationEffectFamilies.FivePhase,
            "five_phase", 1,
            CoreFormationEffectTrigger.DamageDealt | CoreFormationEffectTrigger.FinalDamageIncoming |
            CoreFormationEffectTrigger.Tick,
            1f, 1f, 0f, CoreFormationEffectHandlers.FivePhase,
            Visual(hit: Cue("cultiway/effect/core_formation/five_phase_strike", 0.09f),
                activate: Cue("cultiway/effect/core_formation/five_phase", 0.14f),
                loop: Cue("cultiway/effect/core_formation/five_phase_loop", 0.12f, loop: true,
                    lifeTime: 2f)),
            FinalDamageStage.Adaptation,
            Active("five_phase", "cultiway/icons/element_root/entropy", 48f, 10f, 18f,
                0f, 0f, ActiveAbilityTargetMode.Self, 30,
                CoreFormationEffectHandlers.PrepareCombatBuff, CoreFormationEffectHandlers.ActivateFivePhase)));
        SetEffects(TransformPureYang, Effect(TransformPureYang, CoreFormationEffectFamilies.Yang,
            "pure_yang", 2, CoreFormationEffectTrigger.SkillCastCompleted,
            0.25f, 0.4f, 5f, CoreFormationEffectHandlers.Yang,
            Visual(trigger: Cue("cultiway/effect/core_formation/yang_cleanse", 0.12f),
                activate: Cue("cultiway/effect/core_formation/pure_yang_domain", 0.17f)),
            active: Active("pure_yang", "cultiway/icons/element_root/pos", 56f, 0f, 18f,
                10f, 5f, ActiveAbilityTargetMode.Area, 34,
                CoreFormationEffectHandlers.PrepareCombatBuff, CoreFormationEffectHandlers.ActivatePureYang)));
        SetEffects(TransformMysteriousYin, Effect(TransformMysteriousYin, CoreFormationEffectFamilies.Yin,
            "mysterious_yin", 2, CoreFormationEffectTrigger.DamageDealt,
            0.18f, 0.3f, 3f, CoreFormationEffectHandlers.Yin,
            Visual(hit: Cue("cultiway/effect/core_formation/yin_drain", 0.12f, fixedUpright: false,
                    motion: CoreFormationVisualMotion.Linear),
                activate: Cue("cultiway/effect/core_formation/mysterious_yin_domain", 0.17f)),
            active: Active("mysterious_yin", "cultiway/icons/element_root/neg", 56f, 0f, 18f,
                12f, 5f, ActiveAbilityTargetMode.Area, 34,
                CoreFormationEffectHandlers.PrepareCombatBuff, CoreFormationEffectHandlers.ActivateMysteriousYin)));
        SetEffects(TransformChaos,
            Effect(TransformChaos, CoreFormationEffectFamilies.Chaos, "chaos_rebirth", 2,
                CoreFormationEffectTrigger.DamageDealt, 0.22f, 0.35f, 2f,
                CoreFormationEffectHandlers.Chaos,
                Visual(hit: Cue("cultiway/effect/core_formation/chaos_echo", 0.13f))),
            Effect(TransformChaos, CoreFormationEffectFamilies.Survival, "chaos_rebirth_survival", 2,
                CoreFormationEffectTrigger.FinalDamageIncoming, 1f, 1f, 120f,
                CoreFormationEffectHandlers.Survival,
                Visual(rebirth: Cue("cultiway/effect/core_formation/chaos_rebirth", 0.16f)),
                FinalDamageStage.Survival));
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
        CoreFormationEffectVisualProfile visual = null,
        FinalDamageStage finalStage = FinalDamageStage.Adaptation,
        CoreFormationActiveProfile active = null)
    {
        return new CoreFormationEffectDefinition
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
            visual = visual,
            active = active,
            Handle = handler,
        };
    }

    /// <summary>构造一个使用固定灵气消耗的主动能力配置。</summary>
    private static CoreFormationActiveProfile Active(
        string key,
        string iconPath,
        float cost,
        float duration,
        float cooldown,
        float range,
        float radius,
        ActiveAbilityTargetMode targetMode,
        int aiWeight,
        CoreFormationActivePrepareAction prepare,
        CoreFormationActiveUseAction use)
    {
        return new CoreFormationActiveProfile
        {
            name_key = $"Cultiway.CoreFormationEffect.{key}.Active.Name",
            icon_path = iconPath,
            wakan_cost = cost,
            duration = duration,
            cooldown = cooldown,
            range = range,
            radius = radius,
            target_mode = targetMode,
            ai_weight = aiWeight,
            CanPrepare = prepare,
            Use = use,
        };
    }

    /// <summary>构造一个只填入实际使用阶段的视觉配置。</summary>
    private static CoreFormationEffectVisualProfile Visual(
        CoreFormationEffectVisualCue trigger = null,
        CoreFormationEffectVisualCue apply = null,
        CoreFormationEffectVisualCue hit = null,
        CoreFormationEffectVisualCue charge = null,
        CoreFormationEffectVisualCue activate = null,
        CoreFormationEffectVisualCue loop = null,
        CoreFormationEffectVisualCue end = null,
        CoreFormationEffectVisualCue rebirth = null)
    {
        return new CoreFormationEffectVisualProfile
        {
            trigger = trigger,
            apply = apply,
            hit = hit,
            charge = charge,
            activate = activate,
            loop = loop,
            end = end,
            rebirth = rebirth,
        };
    }

    /// <summary>构造一个帧动画播放配置。</summary>
    private static CoreFormationEffectVisualCue Cue(
        string path,
        float scale,
        bool fixedUpright = true,
        bool loop = false,
        float lifeTime = VisualOneShotLifeTime,
        CoreFormationVisualMotion? motion = null)
    {
        return new CoreFormationEffectVisualCue
        {
            path = path,
            scale = scale,
            frame_interval = VisualFrameInterval,
            life_time = lifeTime,
            motion = motion ?? (loop
                ? CoreFormationVisualMotion.FollowOwner
                : CoreFormationVisualMotion.Stationary),
            loop = loop,
            fixed_upright = fixedUpright,
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

    /// <summary>按指定元素槽位配置一个由元素占比直接评分的元素原子。</summary>
    private static void SetElement(CoreFormationAtomAsset atom, string key, string[] stems,
                                   SemanticAsset semantic, int elementIndex)
    {
        Set(atom, key, CoreFormationAtomCategory.Element, CoreFormationRealmMask.All, stems,
            context => context.Composition[elementIndex] * 10f,
            Descriptor(semantic, CultivationSemantics.Theme.Elemental), [], 0.01f);
    }

    /// <summary>把一套完整的选择规则和派生效果写入指定组合原子资产。</summary>
    private static void Set(CoreFormationAtomAsset atom, string key, CoreFormationAtomCategory category,
                            CoreFormationRealmMask realms, string[] stems, Func<CoreFormationContext, float> score,
                            SemanticDescriptor semantics, CoreFormationStatValue[] stats,
                            float minimumScore = 0f)
    {
        atom.category = category;
        atom.realms = realms;
        atom.name_key = $"Cultiway.CoreFormationAtom.{key}.Name";
        atom.description_key = $"Cultiway.CoreFormationAtom.{key}.Description";
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

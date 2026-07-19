using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Semantics;
using strings;

namespace Cultiway.Content;

/// <summary>金丹与元婴共享的可组合规则原子。</summary>
[Dependency(typeof(BaseStatses), typeof(CultivationSemantics))]
public sealed class CoreFormationAtoms : ExtendLibrary<CoreFormationAtomAsset, CoreFormationAtoms>
{
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
            context => 2f + context.ElementBalance * 5f + context.ThreeHuaBalance * 4f,
            Descriptor(CultivationSemantics.Material.Stability, CultivationSemantics.Effect.Resonance),
            Stats((S.multiplier_health, 0.08f), (S.multiplier_damage, 0.08f)));
        Set(StructureCondensed, "condensed", CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["凝元", "抱一", "玄凝"],
            context => 1f + context.QiRatio * 5f + context.SemanticScore(CultivationSemantics.Resource.Reserve) * 2f,
            Descriptor(CultivationSemantics.Resource.Reserve, CultivationSemantics.Effect.Storage),
            Stats((BaseStatses.MaxWakan.id, 12f), (S.multiplier_damage, 0.05f)));
        Set(StructureVital, "vital", CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["精元", "血魄", "真形"],
            context => 1f + context.JingRatio * 5f + context.SemanticScore(CultivationSemantics.Form.Body) * 2f,
            Descriptor(CultivationSemantics.Resource.Vitality, CultivationSemantics.Form.Body),
            Stats((S.multiplier_health, 0.16f), (S.armor, 1.5f)));
        Set(StructureSpiritual, "spiritual", CoreFormationAtomCategory.Structure, CoreFormationRealmMask.All,
            ["灵台", "神凝", "照神"],
            context => 1f + context.ShenRatio * 5f + context.SemanticScore(CultivationSemantics.Resource.Spirituality) * 2f,
            Descriptor(CultivationSemantics.Resource.Spirituality, CultivationSemantics.Effect.Perception),
            Stats((BaseStatses.MaxWakan.id, 8f), (S.multiplier_crit, 0.06f)));

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

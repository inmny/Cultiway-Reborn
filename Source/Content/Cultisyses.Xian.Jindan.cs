using Cultiway.Const;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>仙道金丹境界的结丹、淬炼、技能奖励、同步与传承规则。</summary>
public partial class Cultisyses
{
    /// <summary>授予结婴前必须逐项结算到的最低金丹淬炼层数。</summary>
    private const int YuanyingRequiredJindanStage = 9;

    /// <summary>将金丹淬炼层数映射到金丹境界的细分排序区间。</summary>
    [Hotfixable]
    private static float GetJindanDetailedLevel(ActorExtend actor)
    {
        if (!actor.TryGetComponent(out Jindan jindan)) return 0f;
        return 0.01f + 0.9f * (1 - 1f / (jindan.stage + 1));
    }

    /// <summary>要求角色持有金丹组件；缺失时拒绝淬炼或结婴。</summary>
    private static ProgressionGateResult RequireJindan(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                        ref Xian component)
    {
        return actor.HasComponent<Jindan>()
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.Blocked("xian.jindan_missing");
    }

    /// <summary>无副作用地选择金丹境界的展示候选，不执行普通结婴概率抽取。</summary>
    private static ProgressionTransitionAsset<Xian> SelectJindanTransitionForQuery(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.Jindan);
        if (!actor.TryGetComponent(out Jindan jindan)) return realm.GetMinorTransition();
        return jindan.stage >= YuanyingRequiredJindanStage || MustAttemptYuanyingForLifespan(actor.Base)
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>直接授予大境界时先把金丹逐次淬炼到九转，再允许提交结婴过渡。</summary>
    private static ProgressionTransitionAsset<Xian> SelectJindanTransitionForGrant(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.Jindan);
        return actor.TryGetComponent(out Jindan jindan) && jindan.stage >= YuanyingRequiredJindanStage
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>自然尝试时按金丹淬炼层数、性格概率和寿命压力决定淬炼或结婴。</summary>
    private static ProgressionTransitionAsset<Xian> SelectJindanTransitionForAttempt(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.Jindan);
        if (!actor.TryGetComponent(out Jindan jindan)) return realm.GetMinorTransition();

        bool shouldFormYuanying = jindan.stage >= YuanyingRequiredJindanStage
                                  && Randy.randomChance(actor.Base.hasTrait(WorldboxGame.ActorTraits.Ambitious.id)
                                      ? 0.13f
                                      : 0.5f);
        return shouldFormYuanying || MustAttemptYuanyingForLifespan(actor.Base)
            ? realm.GetMajorTransition()
            : realm.GetMinorTransition();
    }

    /// <summary>非长生角色寿命消耗超过九成时强制尝试结婴。</summary>
    private static bool MustAttemptYuanyingForLifespan(Actor actor)
    {
        if (actor.hasTrait(ActorTraits.Immortal.id)) return false;
        float lifespan = actor.stats[S.lifespan];
        return lifespan > 0f && actor.data.getAge() / lifespan > 0.9f;
    }

    /// <summary>根据三花五气总强度判定自然结丹，并组合角色独有的金丹快照。</summary>
    private static ProgressionResolution ResolveJindan(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                        ref Xian component)
    {
        XianBase xianBase = actor.GetComponent<XianBase>();
        float strength = xianBase.GetStrength();
        if (RdUtils.NextNormal_0_6() > strength) return ProgressionResolution.Failure();

        CoreFormationSnapshot formation = CoreFormationComposer.ComposeJindan(actor, xianBase, strength);
        return ProgressionResolution.Success(new JindanPayload(xianBase, formation, strength));
    }

    /// <summary>直接结丹时先补齐筑基，再按同一组合规则生成金丹快照。</summary>
    private static ProgressionResolution ResolveGrantedJindan(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                               ref Xian component)
    {
        XianBase xianBase = CompleteFoundationForGrant(actor);
        float strength = xianBase.GetStrength();
        CoreFormationSnapshot formation = CoreFormationComposer.ComposeJindan(actor, xianBase, strength);
        return ProgressionResolution.Success(new JindanPayload(xianBase, formation, strength));
    }

    /// <summary>提交结丹所需的筑基数据，并创建或替换角色金丹组件。</summary>
    private static void ApplyJindanTransformation(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                   ref Xian component, object payload)
    {
        var data = (JindanPayload)payload;
        ref XianBase xianBase = ref actor.GetOrAddComponent<XianBase>();
        xianBase = data.XianBase;
        ref Jindan jindan = ref actor.GetOrAddComponent<Jindan>();
        jindan = new Jindan(data.Formation, data.Strength);
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>结丹后处理植物命名，并学习与组合快照最匹配的代表法术。</summary>
    private static void ApplyJindanReward(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                          ref Xian component, object payload)
    {
        var data = (JindanPayload)payload;
        if (actor.Base.asset == Actors.Plant)
        {
            string rootName = actor.HasElementRoot() ? actor.GetElementRoot().Type.GetName() : null;
            PlantNameGenerator.Instance.NewNameGenerateRequest(
                GetPlantNameParams(actor, cultisys.GetLevelName(XianLevels.Jindan), rootName,
                    data.Formation.canonical_name),
                actor.Base);
        }

        GrantRepresentativeSkill(actor, data.Formation);
    }

    /// <summary>按智力和当前淬炼层数判定自然淬炼，并生成成功后的强度倍率。</summary>
    private static ProgressionResolution ResolveJindanRefinement(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        Jindan jindan = actor.GetComponent<Jindan>();
        if (jindan.stage >= 10000) return ProgressionResolution.NoProgress();

        float intelligence = actor.GetStat(S.intelligence);
        if (Mathf.Abs(RdUtils.NextNormal_0_6()) * (jindan.stage + 1) >= intelligence)
            return ProgressionResolution.Failure();
        return ProgressionResolution.Success(new JindanRefinementPayload(
            1f + 0.2f * Randy.randomFloat(intelligence / (10f + intelligence), 1f)));
    }

    /// <summary>直接授予一次固定 1.2 倍的金丹淬炼；达到一万层后返回无进展。</summary>
    private static ProgressionResolution ResolveGrantedJindanRefinement(ActorExtend actor,
        CultisysAsset<Xian> cultisys, ref Xian component)
    {
        if (!actor.TryGetComponent(out Jindan jindan))
            return ProgressionResolution.Failure(reason: "xian.jindan_missing");
        return jindan.stage < 10000
            ? ProgressionResolution.Success(new JindanRefinementPayload(1.2f))
            : ProgressionResolution.NoProgress(reason: "xian.jindan_refinement_capped");
    }

    /// <summary>自然淬炼成功后保留当前灵气的八成。</summary>
    private static void ApplyJindanRefinementCost(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                  ref Xian component, object payload)
    {
        WakanResourceService.Set(actor, ref component, component.wakan * 0.8f);
    }

    /// <summary>金丹淬炼层数加一，并在三、六、九转推进预定的组合演化。</summary>
    private static void ApplyJindanRefinement(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                              ref Xian component, object payload)
    {
        var data = (JindanRefinementPayload)payload;
        ref Jindan jindan = ref actor.GetComponent<Jindan>();
        int previousStage = jindan.stage;
        jindan.stage++;
        jindan.strength *= data.StrengthMultiplier;
        data.FormationEvolved = CoreFormationComposer.EvolveJindan(ref jindan.formation, previousStage, jindan.stage);
        actor.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(actor);
    }

    /// <summary>普通淬炼强化法术；觉醒节点优先补充当前组合的代表法术。</summary>
    private static void ApplyJindanRefinementReward(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                    ref Xian component, object payload)
    {
        var data = (JindanRefinementPayload)payload;
        ref Jindan jindan = ref actor.GetComponent<Jindan>();
        if (!data.FormationEvolved || !GrantRepresentativeSkill(actor, jindan.formation))
            actor.EnhanceSkillRandomly(SkillEnhanceSources.SmallUpgradeSuccess);
    }

    /// <summary>金丹淬炼达到上限却仍尝试时保留当前灵气的六成。</summary>
    private static void ApplyJindanRefinementCapCost(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                                     ref Xian component, object payload)
    {
        WakanResourceService.Set(actor, ref component, component.wakan * 0.6f);
    }

    /// <summary>学习组合快照指定的代表法术；已经掌握相同本体时不重复创建。</summary>
    private static bool GrantRepresentativeSkill(ActorExtend actor, CoreFormationSnapshot formation)
    {
        if (!GeneralSettings.EnableSkillSystems || string.IsNullOrEmpty(formation.representative_skill_id))
            return false;
        foreach (var learned in actor.GetLearnedSkillsInOrder())
        {
            if (learned.IsNull || !learned.HasComponent<SkillContainer>()) continue;
            if (learned.GetComponent<SkillContainer>().SkillEntityAssetID == formation.representative_skill_id)
                return false;
        }

        var asset = ModClass.I.SkillV3.SkillLib.get(formation.representative_skill_id);
        if (asset == null || !asset.CanBeLearned) return false;
        actor.LearnSkillV3(new SkillContainerBuilder(asset).Build());
        return true;
    }

    /// <summary>修复金丹境界结构：补齐筑基、补建组合金丹，并移除越级残留的元婴。</summary>
    private static void NormalizeJindanRealm(ActorExtend actor, CultisysAsset<Xian> cultisys,
                                              ref Xian component, object payload)
    {
        XianBase xianBaseValue = CompleteFoundationForGrant(actor);
        ref XianBase xianBase = ref actor.GetOrAddComponent<XianBase>();
        xianBase = xianBaseValue;
        if (actor.HasComponent<Yuanying>())
        {
            actor.E.RemoveComponent<Yuanying>();
            actor.MarkSemanticProfileDirty();
        }

        if (!actor.HasComponent<Jindan>())
        {
            float strength = xianBaseValue.GetStrength();
            actor.AddComponent(new Jindan(CoreFormationComposer.ComposeJindan(actor, xianBaseValue, strength),
                strength));
        }
        actor.MarkSemanticProfileDirty();
    }

    /// <summary>将来源金丹及其数组快照深拷贝给目标；来源没有金丹时同步移除目标金丹。</summary>
    private static void TransferJindan(ActorExtend source, ActorExtend target)
    {
        if (source.HasComponent<Jindan>())
        {
            Jindan component = source.GetComponent<Jindan>();
            component.formation = component.formation.DeepClone();
            ref Jindan targetComponent = ref target.GetOrAddComponent<Jindan>();
            targetComponent = component;
        }
        else if (target.HasComponent<Jindan>())
        {
            target.E.RemoveComponent<Jindan>();
        }
    }

    /// <summary>结丹判定后传给结构变换和奖励阶段的不可变数据。</summary>
    private sealed class JindanPayload
    {
        /// <summary>固化结丹后的筑基数据、组合快照和初始强度。</summary>
        public JindanPayload(XianBase xianBase, CoreFormationSnapshot formation, float strength)
        {
            XianBase = xianBase;
            Formation = formation;
            Strength = strength;
        }

        /// <summary>金丹匹配过程中可能完成或调整后的筑基数据。</summary>
        public XianBase XianBase { get; }

        /// <summary>根据筑基、灵根、功法与语义组合出的金丹快照。</summary>
        public CoreFormationSnapshot Formation { get; }

        /// <summary>由三花强度与五气强度相乘得到的初始金丹强度。</summary>
        public float Strength { get; }
    }

    /// <summary>一次金丹淬炼成功后在结构变换与奖励阶段间共享的结算数据。</summary>
    private sealed class JindanRefinementPayload
    {
        /// <summary>创建一次淬炼的强度倍率载荷，组合演化结果由应用阶段回填。</summary>
        public JindanRefinementPayload(float strengthMultiplier)
        {
            StrengthMultiplier = strengthMultiplier;
        }

        /// <summary>本次淬炼乘到当前金丹强度上的倍率。</summary>
        public float StrengthMultiplier { get; }

        /// <summary>本次淬炼是否跨越三、六、九转组合节点。</summary>
        public bool FormationEvolved { get; set; }
    }
}

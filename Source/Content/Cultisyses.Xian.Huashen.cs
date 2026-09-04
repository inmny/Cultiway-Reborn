using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>仙道化神境界的九转门槛、煞风劫与状态修复规则。</summary>
public partial class Cultisyses
{
    /// <summary>元神蕴养的最高层数。</summary>
    internal const int MaximumYuanshenStage = 9;

    /// <summary>将元神蕴养层数映射到化神境界的细分排序区间。</summary>
    private static float GetHuashenDetailedLevel(ActorExtend actor)
    {
        return actor.TryGetComponent(out Yuanshen yuanshen)
            ? 0.01f + 0.89f * Mathf.Clamp01((float)yuanshen.stage / MaximumYuanshenStage)
            : 0f;
    }

    private static ProgressionTransitionAsset<Xian> SelectHuashenTransition(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.Huashen);
        return realm?.GetMinorTransition();
    }

    /// <summary>元神蕴养只在层数未满且人物接近突破时进入人工智能候选。</summary>
    private static bool IsYuanshenRefinementApproaching(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        return actor.TryGetComponent(out Yuanshen yuanshen) &&
               yuanshen.stage < MaximumYuanshenStage &&
               IsXianApproachingBreakthrough(actor, cultisys, ref component);
    }

    /// <summary>要求人物持有有效元神成果。</summary>
    private static ProgressionGateResult RequireYuanshen(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        return actor.TryGetComponent(out Yuanshen yuanshen) && yuanshen.formation.IsValid
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.Blocked("xian.yuanshen_missing");
    }

    /// <summary>自然蕴养以人物智慧和当前层数进行一次明确判定。</summary>
    private static ProgressionResolution ResolveYuanshenRefinement(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        Yuanshen yuanshen = actor.GetComponent<Yuanshen>();
        if (yuanshen.stage >= MaximumYuanshenStage) return ProgressionResolution.NoProgress();
        float intelligence = Mathf.Max(1f, actor.GetStat(S.intelligence));
        if (Mathf.Abs(RdUtils.NextNormal_0_6()) * (yuanshen.stage + 1) >= intelligence)
            return ProgressionResolution.Failure();
        return ProgressionResolution.Success();
    }

    /// <summary>直接授予蕴养固定成功一层，不调用自然随机判定。</summary>
    private static ProgressionResolution ResolveGrantedYuanshenRefinement(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        if (!actor.TryGetComponent(out Yuanshen yuanshen))
            return ProgressionResolution.Failure(reason: "xian.yuanshen_missing");
        return yuanshen.stage < MaximumYuanshenStage
            ? ProgressionResolution.Success()
            : ProgressionResolution.NoProgress(reason: "xian.yuanshen_refinement_capped");
    }

    /// <summary>成功蕴养后保留当前灵气的四分之一。</summary>
    private static void ApplyYuanshenRefinementCost(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        WakanResourceService.Set(actor, ref component, component.wakan * 0.25f);
    }

    /// <summary>失败蕴养保留一半灵气并造成短时神魂创伤。</summary>
    private static void ApplyYuanshenRefinementFailure(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        WakanResourceService.Set(actor, ref component, component.wakan * 0.5f);
        CombatStatusEffects.ApplyStatus(
            actor.Base,
            StatusEffects.SoulTrauma,
            TimeScales.SecPerMonth,
            actor.Base);
    }

    /// <summary>确定性提高元神一层并重建全部派生效果。</summary>
    private static void ApplyYuanshenRefinement(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        ref Yuanshen yuanshen = ref actor.GetComponent<Yuanshen>();
        int previous = yuanshen.stage;
        int current = Mathf.Min(MaximumYuanshenStage, previous + 1);
        CoreFormationComposer.EvolveYuanshen(ref yuanshen.formation, previous, current);
        actor.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(actor);
        YuanshenTravelService.NotifyMindStateChanged(actor);
    }

    private static ProgressionTransitionAsset<Xian> SelectYuanyingTransition(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        RealmProgressionAsset<Xian> realm = cultisys.Progression.GetRealm(XianLevels.Yuanying);
        if (realm == null) return null;
        return actor.TryGetComponent(out Yuanying yuanying) && yuanying.stage < MaximumYuanyingStage
            ? realm.GetMinorTransition()
            : realm.GetMajorTransition();
    }

    private static ProgressionGateResult RequireNinefoldYuanying(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component)
    {
        return actor.TryGetComponent(out Yuanying yuanying) && yuanying.stage >= MaximumYuanyingStage
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.NotReady("xian.yuanying_not_ninefold");
    }

    private static void ApplyHuashenCost(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        WakanResourceService.Clear(actor, ref component);
    }

    private static void ApplyHuashenTransformation(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        CompleteNinefoldYuanying(actor, cultisys, ref component, payload);
        EnsureYuanshenFormation(actor);
        BalefulWindTribulationSkillService.Cleanup(actor);
        actor.MarkSemanticProfileDirty();
    }

    private static void CompleteNinefoldYuanying(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        if (!actor.HasComponent<Yuanying>())
            NormalizeYuanyingRealm(actor, cultisys, ref component, payload);

        ref Yuanying yuanying = ref actor.GetComponent<Yuanying>();
        int previousStage = Mathf.Max(0, yuanying.stage);
        if (previousStage >= MaximumYuanyingStage) return;

        CoreFormationComposer.EvolveYuanying(ref yuanying.formation, previousStage, MaximumYuanyingStage);
        yuanying.stage = MaximumYuanyingStage;
    }

    /// <summary>从九转元婴补齐唯一元神成果，已有有效元神保持原样。</summary>
    /// <param name="actor">需要检查元神成果的人物。</param>
    private static void EnsureYuanshenFormation(ActorExtend actor)
    {
        if (actor.TryGetComponent(out Yuanshen current) &&
            current.formation.IsValid && current.formation.realm == CoreFormationRealm.Yuanshen)
        {
            YuanshenTravelService.EnsureMindLedger(actor);
            return;
        }
        if (!actor.TryGetComponent(out Yuanying yuanying) ||
            !yuanying.formation.IsFinalized || yuanying.stage < MaximumYuanyingStage)
            throw new System.InvalidOperationException("化神境界缺少可以形成元神的九转元婴。");

        if (!PhysicalBodyService.TryCapture(actor.Base, out PhysicalBodySnapshot originalBody))
            throw new System.InvalidOperationException("化神境界缺少可以固定为本相的完整肉身与灵根。");
        CoreFormationSnapshot formation = CoreFormationComposer.ComposeYuanshen(yuanying.formation);
        ref Yuanshen result = ref actor.GetOrAddComponent<Yuanshen>();
        result = new Yuanshen(formation, originalBody);
        YuanshenTravelService.EnsureMindLedger(actor);
    }

    /// <summary>将来源元神的内部数组深拷贝给目标；来源没有元神时移除目标旧成果。</summary>
    /// <param name="source">提供元神成果的人物。</param>
    /// <param name="target">接受元神成果的人物。</param>
    private static void TransferYuanshen(ActorExtend source, ActorExtend target)
    {
        if (source.TryGetComponent(out Yuanshen sourceYuanshen))
        {
            PhysicalBodySnapshot targetOriginal;
            if (target.TryGetComponent(out Yuanshen existing) && existing.original_body.IsValid)
                targetOriginal = existing.original_body.DeepClone();
            else if (!PhysicalBodyService.TryCapture(target.Base, out targetOriginal))
                throw new System.InvalidOperationException("元神传承目标缺少自己的本相肉身印记。");
            ref Yuanshen targetYuanshen = ref target.GetOrAddComponent<Yuanshen>();
            targetYuanshen = sourceYuanshen.DeepClone();
            targetYuanshen.original_body = targetOriginal;
            YuanshenTravelService.EnsureMindLedger(target);
        }
        else if (target.HasComponent<Yuanshen>())
        {
            target.E.RemoveComponent<Yuanshen>();
        }
    }

    /// <summary>同步、授予或传承化神境界时补齐九转元婴和元神，并清理不再需要的渡劫过程。</summary>
    private static void NormalizeHuashenRealm(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        NormalizeYuanyingRealm(actor, cultisys, ref component, payload);
        CompleteNinefoldYuanying(actor, cultisys, ref component, payload);
        EnsureYuanshenFormation(actor);
        BalefulWindTribulationSkillService.Cleanup(actor);
        actor.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(actor);
    }
}

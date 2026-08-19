using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>仙道化神境界的九转门槛、煞风劫与状态修复规则。</summary>
public partial class Cultisyses
{
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
        BalefulWindTribulationSkillService.Cleanup(actor);
        if (actor.HasComponent<BalefulWindTribulation>())
            actor.E.RemoveComponent<BalefulWindTribulation>();
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

    /// <summary>同步、授予或传承化神境界时补齐九转元婴，并清理不再需要的渡劫过程。</summary>
    private static void NormalizeHuashenRealm(
        ActorExtend actor,
        CultisysAsset<Xian> cultisys,
        ref Xian component,
        object payload)
    {
        NormalizeYuanyingRealm(actor, cultisys, ref component, payload);
        CompleteNinefoldYuanying(actor, cultisys, ref component, payload);
        BalefulWindTribulationSkillService.Cleanup(actor);
        if (actor.HasComponent<BalefulWindTribulation>())
            actor.E.RemoveComponent<BalefulWindTribulation>();
        actor.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(actor);
    }
}

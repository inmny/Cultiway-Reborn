using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Progression;
using Cultiway.Utils;

namespace Cultiway.Content;

/// <summary>把化神前的九重煞风劫接入通用进阶关卡。</summary>
internal sealed class BalefulWindTribulationStage : IProgressionStage
{
    public const string TransitionId = "xian.ascend_huashen";

    public ProgressionGateResult Evaluate(ProgressionStageContext context)
    {
        ActorExtend actor = context.Actor;
        if (actor?.Base == null || !actor.Base.isAlive())
            return ProgressionGateResult.Blocked("xian.baleful_wind_actor_invalid");
        if (context.TransitionId != TransitionId)
            return ProgressionGateResult.Blocked("xian.baleful_wind_transition_invalid");
        if (!IsEligibleRealm(actor))
            return ProgressionGateResult.Blocked("xian.baleful_wind_actor_invalid");

        string workOrderId = GetWorkOrderId(actor);
        if (!actor.TryGetComponent(out BalefulWindTribulation tribulation))
            return ProgressionGateResult.NeedsStart("xian.baleful_wind_not_started", workOrderId);
        return tribulation.IsPassed
            ? ProgressionGateResult.Satisfied
            : ProgressionGateResult.InProgress("xian.baleful_wind_in_progress", workOrderId);
    }

    public void Start(ProgressionStageContext context)
    {
        ActorExtend actor = context.Actor;
        if (actor?.Base == null || !actor.Base.isAlive() ||
            actor.HasComponent<BalefulWindTribulation>()) return;
        if (!IsEligibleRealm(actor)) return;

        double now = World.world.getCurWorldTime();
        actor.AddComponent(new BalefulWindTribulation
        {
            waves_survived = 0,
            active_wave = 0,
            started_at = now,
            next_wave_at = now + BalefulWindTribulation.InitialDelay,
            outcome = BalefulWindTribulationOutcome.InProgress
        });
        WorldLogUtils.LogBalefulWindTribulationStarted(actor.Base);
        BalefulWindTribulationSkillService.SpawnCenter(actor.Base);
    }

    private static bool IsEligibleRealm(ActorExtend actor)
    {
        return actor.TryGetComponent(out Xian xian)
               && xian.CurrLevel == XianLevels.Yuanying
               && actor.TryGetComponent(out Yuanying yuanying)
               && yuanying.stage >= Cultisyses.MaximumYuanyingStage;
    }

    private static string GetWorkOrderId(ActorExtend actor)
    {
        return $"xian.baleful_wind:{actor.Base.data.id}";
    }
}

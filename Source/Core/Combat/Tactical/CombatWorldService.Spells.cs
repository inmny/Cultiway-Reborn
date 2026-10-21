using Cultiway.Utils.Extension;

namespace Cultiway.Core.Combat.Tactical;

public static partial class CombatWorldService
{
    /// <summary>沿用当前可用动作，每轮轮换释放数种瞬发法术；逐次验证目标与支付，不等待普攻恢复。</summary>
    /// <param name="actor">自主战斗中的人物。</param>
    /// <param name="runtime">当前世界的战斗状态。</param>
    /// <param name="now">实际世界时间。</param>
    private static void TryCastSpellVolley(Actor actor, CombatActorRuntime runtime, double now)
    {
        CombatPlan plan = runtime.Plan;
        if (now < runtime.NextSpellVolleyAt || plan == null || !plan.HasEnemy ||
            plan.ActionSequence.Length == 0 || !CanEngageTarget(actor, plan.PrimaryEnemy.Object)) return;

        // 每轮只遍历一次现有候选；下一轮从上次结束处继续，让已掌握的法术轮流出手。
        runtime.NextSpellVolleyAt = now + TacticalCombatSettings.SpellVolleyInterval;
        int count = plan.ActionSequence.Length;
        int start = runtime.SpellVolleyOffset % count;
        int cast = 0;
        for (int inspected = 0; inspected < count; inspected++)
        {
            int index = (start + inspected) % count;
            CombatPlannedAction action = plan.ActionSequence[index];
            CombatActionCandidate candidate = action.Candidate;
            runtime.SpellVolleyOffset = (index + 1) % count;
            if (!candidate.Profile.CanCastConcurrently) continue;

            BaseSimObject target = ResolveActionTarget(actor, plan, action);
            if (target.isRekt() || !IsWithinPlannedActionRange(actor, plan, action, target)) continue;
            var context = new CombatActionExecutionContext(actor.GetExtend(), plan.PrimaryEnemy.Object,
                target, action.Use, target.GetSimPos());

            // 继续走同一能力入口；前一法术已经支付，后一法术必须按剩余资源重新检查。
            CombatExecutionStatus result = CombatActionService.Execute(candidate, context, shareAttackRecovery: false);
            CombatDiagnostics.RecordExecution(result);
            if (result != CombatExecutionStatus.Started) continue;
            runtime.LastProgressAt = now;
            runtime.ActiveActionUse = action.Use;
            runtime.ActionPresentationUntil = now + TacticalCombatSettings.ActionPresentationDuration;
            if (++cast >= TacticalCombatSettings.SpellVolleyLimit) break;
        }
    }
}

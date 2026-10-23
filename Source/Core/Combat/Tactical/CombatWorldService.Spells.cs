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

        // 先收集本轮候选，再按真实成功时间尝试。列表重排不应重置法术的等待次序。
        runtime.NextSpellVolleyAt = now + TacticalCombatSettings.SpellVolleyInterval;
        using var choices = new ListPool<CombatPlannedAction>();
        for (int i = 0; i < plan.ActionSequence.Length; i++)
        {
            CombatPlannedAction action = plan.ActionSequence[i];
            if (action.Candidate.Profile.CanCastConcurrently) choices.Add(action);
        }

        int cast = 0;
        while (choices.Count > 0)
        {
            int next = 0;
            double oldest = double.MaxValue;
            for (int i = 0; i < choices.Count; i++)
            {
                double last = runtime.SpellLastStartedAt.TryGetValue(choices[i].Candidate.Key, out double value)
                    ? value
                    : double.MinValue;
                if (last >= oldest) continue;
                oldest = last;
                next = i;
            }

            CombatPlannedAction action = choices[next];
            choices.RemoveAt(next);
            CombatActionCandidate candidate = action.Candidate;
            BaseSimObject target = ResolveActionTarget(actor, plan, action);
            if (target.isRekt() || !IsWithinPlannedActionRange(actor, plan, action, target)) continue;
            var context = new CombatActionExecutionContext(actor.GetExtend(), plan.PrimaryEnemy.Object,
                target, action.Use, target.GetSimPos());

            // 继续走同一能力入口；前一法术已经支付，后一法术必须按剩余资源重新检查。
            CombatExecutionStatus result = CombatActionService.Execute(candidate, context, shareAttackRecovery: false);
            CombatDiagnostics.RecordExecution(result);
            if (result != CombatExecutionStatus.Started) continue;
            runtime.SpellLastStartedAt[candidate.Key] = now;
            runtime.LastProgressAt = now;
            runtime.ActiveActionUse = action.Use;
            runtime.ActionPresentationUntil = now + TacticalCombatSettings.ActionPresentationDuration;
            if (++cast >= TacticalCombatSettings.SpellVolleyLimit) break;
        }
    }
}
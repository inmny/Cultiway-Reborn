using ai.behaviours;
using Cultiway.Core.Combat.Tactical;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 战术战斗任务的空转行为。目标、移动和动作均由 b2/b3 战斗层维护，任务本身不追向敌人。
/// </summary>
public sealed class BehMaintainTacticalCombat : BehaviourActionActor
{
    public override BehResult execute(Actor actor)
    {
        if (!TacticalCombatSettings.Enabled || !CombatWorldService.IsEngaged(actor))
            return BehResult.Stop;
        return BehResult.RepeatStep;
    }
}

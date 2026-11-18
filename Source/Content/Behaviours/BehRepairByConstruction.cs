using ai.behaviours;
using Cultiway.Content.Skills;

namespace Cultiway.Content.Behaviours;

/// <summary>原维修任务先尝试法术，再由原步骤检查仍需普通修理的装备。</summary>
internal sealed class BehRepairByConstruction : BehaviourActionActor
{
    public override BehResult execute(Actor pActor)
    {
        if (ConstructedAbilityService.TryOwnEquipmentRepair(pActor)) pActor.timer_action = .35f;
        return BehResult.Continue;
    }
}

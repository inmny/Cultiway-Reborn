using ai.behaviours;
using Cultiway.Content.Skills;

namespace Cultiway.Content.Behaviours;

/// <summary>只在原出行任务已经选好目的地时尝试缩短路程。</summary>
internal sealed class BehTravelByConstruction : BehaviourActionActor
{
    public override BehResult execute(Actor pActor)
    {
        if (ConstructedAbilityService.TryTravelShortcut(pActor, pActor.beh_tile_target))
            pActor.timer_action = ConstructedAbilityService.TravelPreparation;
        return BehResult.Continue;
    }
}

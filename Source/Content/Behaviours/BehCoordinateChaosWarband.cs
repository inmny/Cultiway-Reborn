using ai.behaviours;
using Cultiway.Core.Coordination;

namespace Cultiway.Content.Behaviours;

/// <summary>维持混沌战帮的协调行动。</summary>
public sealed class BehCoordinateChaosWarband : BehaviourActionActor
{
    public override BehResult execute(Actor actor)
    {
        CoordinationParticipantResult result = ChaosWarbandService.TickActor(actor);
        return result == CoordinationParticipantResult.Leave
            ? BehResult.Continue
            : BehResult.RepeatStep;
    }
}

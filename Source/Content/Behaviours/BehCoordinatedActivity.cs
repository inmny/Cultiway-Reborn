using ai.behaviours;
using Cultiway.Core.Coordination;

namespace Cultiway.Content.Behaviours;

/// <summary>让角色持续执行已经分配的协调行动，直到行动或该角色席位结束。</summary>
public sealed class BehCoordinatedActivity : BehaviourActionActor
{
    /// <inheritdoc />
    public override BehResult execute(Actor actor)
    {
        CoordinationParticipantResult result = CoordinatedActivityService.TickParticipant(actor);
        return result == CoordinationParticipantResult.Continue
            ? BehResult.RepeatStep
            : BehResult.Continue;
    }
}

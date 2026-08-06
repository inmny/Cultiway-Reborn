using ai.behaviours;
using Cultiway.Core.Coordination;

namespace Cultiway.Content.Behaviours;

/// <summary>维持鼠人所属小队的协调行动；具体路径与战斗仍由角色自身系统执行。</summary>
public sealed class BehCoordinateSkavenPack : BehaviourActionActor
{
    /// <inheritdoc />
    public override BehResult execute(Actor actor)
    {
        CoordinationParticipantResult result = SkavenPackService.TickActor(actor);
        return result == CoordinationParticipantResult.Leave
            ? BehResult.Continue
            : BehResult.RepeatStep;
    }
}

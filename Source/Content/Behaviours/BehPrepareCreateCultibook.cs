using ai.behaviours;
using Cultiway.Content.AIGC;

namespace Cultiway.Content.Behaviours;

public sealed class BehPrepareCreateCultibook : BehCityActor
{
    public override BehResult execute(Actor actor)
    {
        CultibookRequestService.CancelActorRequests(actor.getID());
        return BehResult.Continue;
    }
}

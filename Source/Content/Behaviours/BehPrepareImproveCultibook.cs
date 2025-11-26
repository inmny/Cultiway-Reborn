using ai.behaviours;
using Cultiway.Content.Const;

namespace Cultiway.Content.Behaviours;

public class BehPrepareImproveCultibook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        pObject.data.set(ContentActorDataKeys.WaitingForCultibookImprovement_int, -1);
        return BehResult.Continue;
    }
}


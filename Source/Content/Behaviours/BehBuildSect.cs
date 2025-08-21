using ai.behaviours;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehBuildSect : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        if (pObject.GetExtend().sect != null)
        {
            return BehResult.Stop;
        }

        WorldboxGame.I.Sects.BuildSect(pObject);
        return BehResult.Continue;
    }
}
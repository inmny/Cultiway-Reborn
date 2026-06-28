using ai.behaviours;
using Cultiway.Content;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehBuildSect : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        if (!SectRules.CanFoundSect(pObject))
        {
            return BehResult.Stop;
        }

        return WorldboxGame.I.Sects.BuildSect(pObject) != null ? BehResult.Continue : BehResult.Stop;
    }
}

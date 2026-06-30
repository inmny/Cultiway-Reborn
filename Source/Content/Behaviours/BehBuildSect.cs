using ai.behaviours;
using Cultiway.Content;
using Cultiway.Debug;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehBuildSect : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        if (!SectRules.CanFoundSect(pObject))
        {
            SectVerifyLog.Log("BuildSectTask", $"actor={SectVerifyLog.Actor(pObject)} result=false");
            return BehResult.Stop;
        }

        bool result = WorldboxGame.I.Sects.BuildSect(pObject) != null;
        SectVerifyLog.Log("BuildSectTask", $"actor={SectVerifyLog.Actor(pObject)} result={result}");
        return result ? BehResult.Continue : BehResult.Stop;
    }
}

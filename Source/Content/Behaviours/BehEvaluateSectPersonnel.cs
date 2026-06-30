using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehEvaluateSectPersonnel : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        if (!SectPersonnelEvaluator.CanManageSectPersonnel(pObject))
        {
            SectVerifyLog.Log("EvaluateTask", $"actor={SectVerifyLog.Actor(pObject)} result=false reason=no_permission");
            return BehResult.Stop;
        }

        Sect sect = pObject.GetExtend().sect;
        bool result = sect.EvaluateAllMemberRoles(pObject);
        SectVerifyLog.Log("EvaluateTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pObject)} result={result}");
        return result ? BehResult.Continue : BehResult.Stop;
    }
}

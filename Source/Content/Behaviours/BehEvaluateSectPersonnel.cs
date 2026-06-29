using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
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
            return BehResult.Stop;
        }

        Sect sect = pObject.GetExtend().sect;
        sect.EvaluateAllMemberRoles();
        return BehResult.Continue;
    }
}

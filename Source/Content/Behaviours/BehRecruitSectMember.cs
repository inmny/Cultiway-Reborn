using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehRecruitSectMember : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        if (!SectPersonnelEvaluator.CanRecruitExternalMember(pObject))
        {
            return BehResult.Stop;
        }

        Actor candidate = SectPersonnelEvaluator.FindExternalRecruitCandidate(pObject);
        if (candidate == null)
        {
            return BehResult.Stop;
        }

        if (!pObject.isInAttackRange(candidate))
        {
            pObject.goTo(candidate.current_tile);
            return BehResult.RepeatStep;
        }

        Sect sect = pObject.GetExtend().sect;
        if (sect.TryRecruitExternalMember(pObject, candidate))
        {
            ModClass.LogInfo($"[{pObject.getName()}] 招揽散修入宗: {candidate.getName()} -> {sect.name}({candidate.GetSectRank()})");
            return BehResult.Continue;
        }

        return BehResult.Stop;
    }
}

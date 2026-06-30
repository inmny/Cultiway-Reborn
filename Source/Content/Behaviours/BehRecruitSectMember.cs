using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Debug;
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
            SectVerifyLog.Log("RecruitTask", $"actor={SectVerifyLog.Actor(pObject)} result=false reason=no_candidate_or_permission");
            return BehResult.Stop;
        }

        Actor candidate = SectPersonnelEvaluator.FindExternalRecruitCandidate(pObject);
        if (candidate == null)
        {
            SectVerifyLog.Log("RecruitTask", $"actor={SectVerifyLog.Actor(pObject)} result=false reason=no_candidate");
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
            SectVerifyLog.Log("RecruitTask", $"sect={SectVerifyLog.Sect(sect)} recruiter={SectVerifyLog.Actor(pObject)} candidate={SectVerifyLog.Actor(candidate)} result=true roles={candidate.GetSectRoleSummary()}");
            return BehResult.Continue;
        }

        SectVerifyLog.Log("RecruitTask", $"sect={SectVerifyLog.Sect(sect)} recruiter={SectVerifyLog.Actor(pObject)} candidate={SectVerifyLog.Actor(candidate)} result=false");
        return BehResult.Stop;
    }
}

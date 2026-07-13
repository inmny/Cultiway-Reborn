using Cultiway.Debug;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Sects;

public static class SectScriptureStudyService
{
    [Hotfixable]
    public static bool TryStudy(Actor actor)
    {
        if (!SectScriptureStudyPlanner.TryCreatePlan(actor, out SectScriptureStudyPlan plan))
        {
            SectVerifyLog.Log("StudyScriptureTask", $"actor={SectVerifyLog.Actor(actor)} result=false reason=no_plan");
            return false;
        }

        if (!actor.TrySpendSectContribution(plan.Cost))
        {
            SectVerifyLog.Log("StudyScriptureTask", $"actor={SectVerifyLog.Actor(actor)} book={SectVerifyLog.Book(plan.Book)} cost={plan.Cost} available={actor.GetAvailableSectContribution()} result=false reason=not_enough_contribution");
            return false;
        }

        plan.Book.readIt();
        actor.beh_book_target = plan.Book;
        Sect sect = actor.GetExtend().sect;
        SectVerifyLog.Log("StudyScriptureTask", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(actor)} book={SectVerifyLog.Book(plan.Book)} candidates={plan.CandidateCount} score={plan.Score:F1} cost={plan.Cost} available={actor.GetAvailableSectContribution()} result=true");
        return true;
    }
}

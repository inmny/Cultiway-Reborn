using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Sects;

namespace Cultiway.Content.Behaviours;

public class BehWriteSkillbook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        if (!SectScriptureContributionPlanner.TryPickSkillbookTarget(pObject, out var target, out var skill_to_share))
        {
            return BehResult.Continue;
        }

        var raw_cultibook = World.world.books.WriteSkillbookBook(pObject, skill_to_share);
        if (raw_cultibook == null)
        {
            return BehResult.Stop;
        }

        target.StoreBook(pObject, raw_cultibook);
        pObject.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        return BehResult.Continue;
    }
}

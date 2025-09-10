using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehCreateSkillbook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        if (ae.all_skills.Count == 0) return BehResult.Stop;
        var raw_cultibook = World.world.books.CreateNewSkillbook(pObject, ae.all_skills.GetRandom());
        if (raw_cultibook == null)
        {
            return BehResult.Stop;
        }
        pObject.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        return BehResult.Continue;
    }
}
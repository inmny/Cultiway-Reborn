using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehCreateCultibook : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var raw_cultibook = World.world.books.CreateNewCultibook(pObject);
        if (raw_cultibook == null)
        {
            return BehResult.Stop;
        }
        pObject.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        var ae = pObject.GetExtend();
        ae.SetMainCultibook(raw_cultibook.GetExtend().GetComponent<Cultibook>().Asset);
        ae.AddMainCultibookMastery(100);
        return BehResult.Continue;
    }
}
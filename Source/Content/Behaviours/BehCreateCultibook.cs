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
        var ae = pObject.GetExtend();
        var raw_cultibook = World.world.books.GenerateNewBook(pObject, BookTypes.Cultibook);
        if (raw_cultibook == null)
        {
            return BehResult.Stop;
        }
        var be = raw_cultibook.GetExtend();
        be.AddComponent(new Cultibook()
        {
            
        });
        be.AddComponent(new ItemLevel()
        {
            
        });
        ae.SetCultibookMasterRelation(be.E, 100);
        pObject.timer_action = Randy.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        return BehResult.Continue;
    }
}
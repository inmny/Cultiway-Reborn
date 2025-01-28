using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehCreateCultibook : BehCity
{
    public override BehResult execute(Actor pObject)
    {
        var ae = pObject.GetExtend();
        var new_cultibook = Libraries.Manager.CultibookLibrary.NewCultibook(pObject.getName());
        ae.SetCultibookMasterRelation(ref new_cultibook.CultibookEntity, 100);
        pObject.timer_action = Toolbox.randomFloat(TimeScales.SecPerYear, TimeScales.SecPerYear * 3);
        return BehResult.Continue;
    }
}
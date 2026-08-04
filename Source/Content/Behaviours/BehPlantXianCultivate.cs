using ai.behaviours;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehPlantXianCultivate : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        ActorExtend actor_extend = pObject.GetExtend();
        var context = new Libraries.CultivationTriggerContext(
            actor_extend,
            Libraries.CultivationTriggerKind.ActiveTick,
            Libraries.CultivationActivityKind.PlantPurification,
            1f);
        CultivateMethods.TryDispatch(in context);
        BehOutdoorCultivationWait.ClearCultivationTimers(pObject);
        return BehResult.Continue;
    }
}

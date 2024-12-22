using ai.behaviours;
using Cultiway.Content.Components;
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
        ref Xian xian = ref actor_extend.GetCultisys<Xian>();
        Cultisyses.TakeWakanAndCultivate(actor_extend, ref xian);
        return BehResult.Continue;
    }
}
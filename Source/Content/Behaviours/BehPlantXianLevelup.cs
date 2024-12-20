using ai.behaviours;
using Cultiway.Content.CultisysComponents;
using Cultiway.Core;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehPlantXianLevelup : BehaviourActionActor
{
    public override BehResult execute(Actor pObject)
    {
        ActorExtend actor_extend = pObject.GetExtend();
        ref Xian xian = ref actor_extend.GetCultisys<Xian>();

        // 获取修炼收益
        Cultisyses.TakeWakanAndCultivate(actor_extend, ref xian);

        if (Cultisyses.Xian.AllowUpgrade(actor_extend)) Cultisyses.Xian.TryPerformUpgrade(actor_extend);

        return BehResult.Continue;
    }
}
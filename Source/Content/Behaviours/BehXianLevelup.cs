using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

public class BehXianLevelup : BehCityActor
{
    public override BehResult execute(Actor pObject)
    {
        var actor_extend = pObject.GetExtend();
        ref var xian = ref actor_extend.GetCultisys<Xian>();

        // 获取修炼收益
        Cultisyses.TakeWakanAndCultivate(actor_extend, ref xian);

        if (Cultisyses.Xian.AllowUpgrade(actor_extend))
        {
            pObject.changeHappiness(HappinessAssets.LevelUp.id);
            Cultisyses.Xian.TryPerformUpgrade(actor_extend);
            return BehResult.Continue;
        }

        return !Cultisyses.Xian.PreCheckUpgrade(actor_extend) ? BehResult.Continue : BehResult.RepeatStep;
    }
}
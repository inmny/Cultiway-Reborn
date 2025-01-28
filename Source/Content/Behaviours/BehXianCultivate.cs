using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

public class BehXianCultivate : BehCity
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        pObject.data.get(ContentActorDataKeys.CultivateTime_float, out var time, -TimeScales.SecPerMonth);

        var actor_extend = pObject.GetExtend();
        ref var xian = ref actor_extend.GetCultisys<Xian>();
        if (time <= -TimeScales.SecPerMonth)
        {
            time = (xian.CurrLevel + 1) * TimeScales.SecPerMonth;
        }

        if (time > 0)
        {
            time -= pObject.timer_action;

            // 获取修炼收益
            // TODO: 考虑天赋以及其他因素的影响
            Cultisyses.TakeWakanAndCultivate(actor_extend, ref xian);
            if (xian.wakan < pObject.stats[BaseStatses.MaxWakan.id])
            {
                pObject.data.set(ContentActorDataKeys.CultivateTime_float, time);
                return BehResult.RepeatStep;
            }
        }

        pObject.data.set(ContentActorDataKeys.CultivateTime_float, -TimeScales.SecPerMonth);
        return BehResult.Continue;
    }
}
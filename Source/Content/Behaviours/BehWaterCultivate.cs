using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 水中修炼行为
/// </summary>
public class BehWaterCultivate : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        // 如果不在水中，先移动到水域
        if (!pObject.current_tile.IsWater())
        {
            return BehResult.Stop; // 由寻找水域行为处理
        }

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

            // 水中修炼：从地块吸收灵气（效率提升）
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


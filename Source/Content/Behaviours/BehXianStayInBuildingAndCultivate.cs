using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Const;
using Cultiway.Content.CultisysComponents;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehXianStayInBuildingAndCultivate : BehCity
{
    public override void create()
    {
        base.create();
        special_inside_object = true;
        check_building_target_non_usable = true;
        null_check_building_target = true;
    }

    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        pObject.timer_action = TimeScales.SecPerMonth;
        pObject.stayInBuilding(pObject.beh_building_target);

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
            var tile_pos = pObject.currentTile.pos;
            var to_take = Mathf.Log10(WakanMap.I.map[tile_pos.x, tile_pos.y] + 1);

            var max_wakan = pObject.stats[BaseStatses.MaxWakan.id];
            xian.wakan = Mathf.Min(xian.wakan + to_take * actor_extend.GetElementRoot().GetStrength(), max_wakan);
            WakanMap.I.map[tile_pos.x, tile_pos.y] -= to_take;
            if (xian.wakan < max_wakan)
            {
                pObject.data.set(ContentActorDataKeys.CultivateTime_float, time);
                return BehResult.RepeatStep;
            }
        }

        pObject.data.set(ContentActorDataKeys.CultivateTime_float, -TimeScales.SecPerMonth);
        return BehResult.Continue;
    }
}
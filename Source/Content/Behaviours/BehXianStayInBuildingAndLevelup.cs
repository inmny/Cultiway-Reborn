using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.CultisysComponents;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Behaviours;

public class BehXianStayInBuildingAndLevelup : BehCity
{
    public override void create()
    {
        base.create();
        special_inside_object = true;
        check_building_target_non_usable = true;
        null_check_building_target = true;
    }

    public override BehResult execute(Actor pObject)
    {
        pObject.timer_action = TimeScales.SecPerMonth;
        pObject.stayInBuilding(pObject.beh_building_target);

        var actor_extend = pObject.GetExtend();
        ref var xian = ref actor_extend.GetCultisys<Xian>();

        // 获取修炼收益
        var tile_pos = pObject.currentTile.pos;
        var to_take = Mathf.Log10(WakanMap.I.map[tile_pos.x, tile_pos.y] + 1);
        xian.wakan = Mathf.Min(xian.wakan + to_take * actor_extend.GetElementRoot().GetStrength(),
            pObject.stats[BaseStatses.MaxWakan.id]);
        WakanMap.I.map[tile_pos.x, tile_pos.y] -= to_take;

        if (Cultisyses.Xian.AllowUpgrade(actor_extend))
        {
            Cultisyses.Xian.TryPerformUpgrade(actor_extend);
            return BehResult.Continue;
        }

        return !Cultisyses.Xian.PreCheckUpgrade(actor_extend) ? BehResult.Continue : BehResult.RepeatStep;
    }
}
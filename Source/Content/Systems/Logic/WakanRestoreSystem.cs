using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.Components;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

public class WakanRestoreSystem : QuerySystem<Xian, ActorBinder>
{
    private float _restore_timer = TimeScales.SecPerMonth;
    protected override void OnUpdate()
    {
        _restore_timer -= Tick.deltaTime;
        if (_restore_timer > 0) return;
        _restore_timer = TimeScales.SecPerMonth;
        Query.ForEachComponents(((ref Xian xian, ref ActorBinder binder) =>
        {
            var a = binder.Actor;
            if (a == null) return;
            var max_wakan = a.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit;
            if (xian.wakan >= max_wakan) return;
            Vector2Int tile_pos = a.currentTile.pos;
            var to_take = Mathf.Log10(WakanMap.I.map[tile_pos.x, tile_pos.y] + 1);
            to_take = Mathf.Min(max_wakan - (xian.wakan + a.stats[BaseStatses.WakanRegen.id] * to_take), WakanMap.I.map[tile_pos.x, tile_pos.y]);
            WakanMap.I.map[tile_pos.x, tile_pos.y] -= to_take;
            xian.wakan += to_take;
        }));
    }
}
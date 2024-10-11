using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content;

public class WorldBehs : ExtendLibrary<WorldBehaviourAsset, WorldBehs>
{
    public static WorldBehaviourAsset WakanTide   { get; private set; }
    public static WorldBehaviourAsset WakanSpread { get; private set; }

    protected override void OnInit()
    {
        WakanTide = Add(new WorldBehaviourAsset()
        {
            id = nameof(WakanTide),
            interval = 0.1f,
            interval_random = 0,
            action = UpdateWakanTide
        });
        WakanSpread = Add(new WorldBehaviourAsset()
        {
            id = nameof(WakanSpread),
            interval = 0.05f,
            interval_random = 0,
            action = WorldBehaviourActionWakanSpread.Update,
            action_clear = WorldBehaviourActionWakanSpread.Clear
        });
    }

    protected override WorldBehaviourAsset Add(WorldBehaviourAsset asset)
    {
        var res = base.Add(asset);
        res.manager = new WorldBehaviour(res);
        return res;
    }

    [Hotfixable]
    private static void UpdateWakanTide()
    {
        var record_e = ModClass.I.WorldRecord.E;
        if (!record_e.HasComponent<WakanTideStatus>())
        {
            var positions = new List<Vector2Int>();
            var len = Toolbox.randomInt(1, (int)Mathf.Sqrt(Config.ZONE_AMOUNT_X * Config.ZONE_AMOUNT_Y));
            for (int i = 0; i < len; i++)
            {
                positions.Add(new Vector2Int(Toolbox.randomInt(0, MapBox.width), Toolbox.randomInt(0, MapBox.height)));
            }

            record_e.AddComponent(new WakanTideStatus()
            {
                switch_timer = 500 * 12 * TimeScales.SecPerMonth,
                rise = true,
                action_positions = positions
            });
        }

        ref var status = ref record_e.GetComponent<WakanTideStatus>();
        status.switch_timer -= WakanTide.interval;
        if (status.switch_timer < 0)
        {
            status.switch_timer += 500 * 12 * TimeScales.SecPerMonth;
            status.rise = !status.rise;
            if (status.rise)
            {
                var positions = status.action_positions;
                positions.Clear();
                var len = Toolbox.randomInt(1, (int)Mathf.Sqrt(Config.ZONE_AMOUNT_X * Config.ZONE_AMOUNT_Y));
                for (int i = 0; i < len; i++)
                {
                    positions.Add(new Vector2Int(Toolbox.randomInt(0, MapBox.width),
                        Toolbox.randomInt(0,                          MapBox.height)));
                }
            }
            else
            {
                status.next_zone_id = 0;
            }
        }

        if (status.rise)
        {
            var positions = status.action_positions;
            for (int i = 0; i < positions.Count; i++)
            {
                var pos = positions[i];
                WakanMap.I.map[pos.x, pos.y] = Mathf.Clamp(WakanMap.I.map[pos.x, pos.y] * 1.01f + 10, 0, 1e8f);
            }
        }
        else
        {
            var zone = World.world.zoneCalculator.zones[status.next_zone_id];
            for (int i = 0; i < zone.tiles.Count; i++)
            {
                var pos = zone.tiles[i].pos;
                WakanMap.I.map[pos.x, pos.y] = Mathf.Clamp(WakanMap.I.map[pos.x, pos.y] * 0.99f - 1, 0, 1e8f);
            }

            status.next_zone_id = (status.next_zone_id + 1) % (Config.ZONE_AMOUNT_X * Config.ZONE_AMOUNT_Y);
        }
    }

    struct WakanTideStatus : IComponent
    {
        public float            switch_timer;
        public bool             rise;
        public int              next_zone_id;
        public List<Vector2Int> action_positions;
    }
}
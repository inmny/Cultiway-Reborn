using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.Components.Position;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicTriggerTileCollisionSystem : QuerySystem<TileCollisionTrigger, TileCollisionContext>
{
    private readonly ArchetypeQuery<ColliderBox, TileCollisionTrigger, TileCollisionContext>    box_query;
    private readonly ArchetypeQuery<ColliderSphere, TileCollisionTrigger, TileCollisionContext> sphere_query;

    public LogicTriggerTileCollisionSystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());

        box_query = world.Query<ColliderBox, TileCollisionTrigger, TileCollisionContext>(Filter);
        sphere_query = world.Query<ColliderSphere, TileCollisionTrigger, TileCollisionContext>(Filter);
    }
    [Hotfixable]
    protected override void OnUpdate()
    {
        var world_min = new Vector2Int(0,            0);
        var world_max = new Vector2Int(MapBox.width-1, MapBox.height-1);
        box_query.ForEachEntity((ref ColliderBox         collider, ref TileCollisionTrigger trigger,
                                 ref TileCollisionContext context,  Entity                  trigger_entity) =>
        {
            if (!trigger.Enabled) return;
            Entity skill_entity = trigger_entity.Parent;
            var pos = skill_entity.GetComponent<Position>();
            Vector2 lb = pos.v2 - collider.v2;
            Vector2 rt = pos.v2 + collider.v2;

            Vector2Int lb_fixed = Vector2Int.FloorToInt(lb);
            Vector2Int rt_fixed = Vector2Int.CeilToInt(rt);

            lb_fixed.Clamp(world_min, world_max);
            rt_fixed.Clamp(world_min, world_max);

            var triggered = false;

            for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
            for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
            {
                if (!ShapeUtils.InRect(new(x, y), lb, rt)) continue;
                CheckTile(World.world.GetTileSimple(x, y), ref trigger, ref context);
            }

            return;
            void CheckTile(WorldTile obj, ref TileCollisionTrigger trigger, ref TileCollisionContext context)
            {
                context.Tile = obj;
                context.JustTriggered = !triggered;

                trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                triggered = true;
            }
        });
        
        sphere_query.ForEachEntity([Hotfixable](ref ColliderSphere      collider, ref TileCollisionTrigger trigger,
                                    ref TileCollisionContext context,  Entity                  trigger_entity) =>
        {
            if (!trigger.Enabled) return;
            Entity skill_entity = trigger_entity.Parent;
            var pos = skill_entity.GetComponent<Position>();
            var radius = collider.radius;
            Vector2 lb = pos.v2 - Vector2.one * radius;
            Vector2 rt = pos.v2 + Vector2.one * radius;

            Vector2Int lb_fixed = Vector2Int.FloorToInt(lb);
            Vector2Int rt_fixed = Vector2Int.CeilToInt(rt);

            lb_fixed.Clamp(world_min, world_max);
            rt_fixed.Clamp(world_min, world_max);
            
            var triggered = false;

            var pos_x = pos.x;
            var pos_y = pos.y;

                for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
                for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
                {
                    if (((pos_x - x)*(pos_x-x) + (pos_y-y)*(pos_y-y)) >= (radius + 1)*(radius+1)) continue;
                    CheckTile(World.world.GetTileSimple(x, y), ref trigger, ref context);
                }

            return;

            [Hotfixable]
            void CheckTile(WorldTile obj, ref TileCollisionTrigger trigger, ref TileCollisionContext context)
            {
                context.Tile = obj;
                context.JustTriggered = !triggered;

                trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                triggered = true;
            }
        });
    }
}
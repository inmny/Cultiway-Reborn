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

public class LogicTriggerObjCollisionSystem : QuerySystem<ObjCollisionTrigger, ObjCollisionContext>
{
    private readonly ArchetypeQuery<ColliderBox, ObjCollisionTrigger, ObjCollisionContext>    box_query;
    private readonly ArchetypeQuery<ColliderSphere, ObjCollisionTrigger, ObjCollisionContext> sphere_query;

    public LogicTriggerObjCollisionSystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());

        box_query = world.Query<ColliderBox, ObjCollisionTrigger, ObjCollisionContext>(Filter);
        sphere_query = world.Query<ColliderSphere, ObjCollisionTrigger, ObjCollisionContext>(Filter);
    }

    protected override void OnUpdate()
    {
        var world_min = new Vector2Int(0,            0);
        var world_max = new Vector2Int(MapBox.width, MapBox.height);
        box_query.ForEachEntity((ref ColliderBox         collider, ref ObjCollisionTrigger trigger,
                                 ref ObjCollisionContext context,  Entity                  trigger_entity) =>
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

            Kingdom caster_kingdom = skill_entity.GetComponent<SkillCaster>().AsActor.kingdom;
            var triggered = false;

            if (trigger.actor)
                for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
                for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
                {
                    WorldTile tile = World.world.GetTileSimple(x, y);
                    for (var i = 0; i < tile._units.Count; i++)
                    {
                        Actor obj = tile._units[i];
                        check_obj(obj, ref trigger, ref context);
                    }
                }

            if (trigger.building)
                for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
                for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
                {
                    WorldTile tile = World.world.GetTileSimple(x, y);
                    Building obj = tile.building;
                    if (obj == null) continue;
                    check_obj(obj, ref trigger, ref context);
                }

            return;
            [Hotfixable]
            void check_obj(BaseSimObject obj, ref ObjCollisionTrigger trigger, ref ObjCollisionContext context)
            {
                if (!ShapeUtils.InRect(obj.currentPosition, lb, rt)) return;
                if (!obj.isAlive()) return;

                var enemy = caster_kingdom.isEnemy(obj.kingdom);
                if (enemy && trigger.enemy)
                {
                    context.obj = obj;
                    context.dist = Toolbox.DistVec2Float(pos.v2, obj.currentPosition);
                    context.JustTriggered = !triggered;

                    trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                    triggered = true;
                }
                else if (!enemy && trigger.friend)
                {
                    context.obj = obj;
                    context.dist = Toolbox.DistVec2Float(pos.v2, obj.currentPosition);
                    context.JustTriggered = !triggered;

                    trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                    triggered = true;
                }
            }
        });
        
        sphere_query.ForEachEntity([Hotfixable](ref ColliderSphere      collider, ref ObjCollisionTrigger trigger,
                                    ref ObjCollisionContext context,  Entity                  trigger_entity) =>
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
            if (skill_entity.GetComponent<SkillCaster>().value.E.IsNull)
            {
                ModClass.LogInfo($"{World.world.getCurWorldTime()}, null skill caster: {skill_entity}, {trigger_entity.Id}, {skill_entity.GetComponent<AnimData>().CurrentFrame.name}");
            }
            Kingdom caster_kingdom = skill_entity.GetComponent<SkillCaster>().AsActor.kingdom;
            var triggered = false;

            if (trigger.actor)
                for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
                for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
                {
                    WorldTile tile = World.world.GetTileSimple(x, y);
                    if (Vector2.Distance(pos.v2, tile.pos) >= radius + 1) continue;
                    for (var i = 0; i < tile._units.Count; i++)
                    {
                        Actor obj = tile._units[i];
                        check_obj(obj, ref trigger, ref context);
                    }
                }

            if (trigger.building)
                for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
                for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
                {
                    WorldTile tile = World.world.GetTileSimple(x, y);
                    if (Vector2.Distance(pos.v2, tile.pos) >= radius + 1) continue;
                    Building obj = tile.building;
                    if (obj == null) continue;
                    check_obj(obj, ref trigger, ref context);
                }

            return;

            void check_obj(BaseSimObject obj, ref ObjCollisionTrigger trigger, ref ObjCollisionContext context)
            {
                if (!ShapeUtils.InRect(obj.currentPosition, lb, rt)) return;

                var enemy = caster_kingdom.isEnemy(obj.kingdom);
                if (enemy && trigger.enemy)
                {
                    context.obj = obj;
                    context.dist = Toolbox.DistVec2Float(pos.v2, obj.currentPosition);
                    context.JustTriggered = !triggered;

                    trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                    triggered = true;
                }
                else if (!enemy && trigger.friend)
                {
                    context.obj = obj;
                    context.dist = Toolbox.DistVec2Float(pos.v2, obj.currentPosition);
                    context.JustTriggered = !triggered;

                    trigger.TriggerActionMeta.Invoke(ref trigger, ref context, trigger_entity);

                    triggered = true;
                }
            }
        });
    }
}
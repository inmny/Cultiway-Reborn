using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicActorCollisionSystem : QuerySystem<SkillContext, SkillEntity, ColliderSphere, ColliderConfig, Position>
{
    public LogicActorCollisionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
    }
    
    protected override void OnUpdate()
    {
        var world_min = new Vector2Int(0,            0);
        var world_max = new Vector2Int(MapBox.width-1, MapBox.height-1);
        Query.ForEachEntity(((ref SkillContext context, ref SkillEntity skill_entity, ref ColliderSphere collider,
            ref ColliderConfig config, ref Position pos, Entity entity) =>
        {
            if (!config.Enabled || !config.Actor) return;
            
            var radius = collider.Radius;
            var lb = pos.v2 - radius * Vector2.one;
            var rt = pos.v2 + radius * Vector2.one;
            
            Vector2Int lb_fixed = Vector2Int.FloorToInt(lb);
            Vector2Int rt_fixed = Vector2Int.CeilToInt(rt);

            lb_fixed.Clamp(world_min, world_max);
            rt_fixed.Clamp(world_min, world_max);

            var caster_kingdom = context.SourceObj?.kingdom;

            var pos_x = pos.x;
            var pos_y = pos.y;

            var action = skill_entity.Asset.OnObjCollision;
            for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
            for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
            {
                if (((pos_x - x)*(pos_x-x) + (pos_y-y)*(pos_y-y)) >= (radius + 1)*(radius+1)) continue;
                WorldTile tile = World.world.GetTileSimple(x, y);
                for (var i = 0; i < tile._units.Count; i++)
                {
                    Actor obj = tile._units[i];
                    
                    
                    var enemy = caster_kingdom?.isEnemy(obj.kingdom) ?? true;
                    if ((!enemy || !config.Enemy) && (enemy || !config.Alias)) continue;
                    if (!action(ref context, skill_entity.SkillContainer, entity, obj)) return;
                }
            }
        }));
    }
}
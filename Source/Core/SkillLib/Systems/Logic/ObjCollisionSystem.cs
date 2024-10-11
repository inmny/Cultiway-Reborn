using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Components.Triggers;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.SkillLib.Components.Position;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class ObjCollisionSystem : QuerySystem<Position, SkillInfo, ObjCollisionTrigger>
{
    protected override void OnUpdate()
    {
        Query.WithoutAllTags(Tags.Get<PrefabTag>());
        var tiles = World.world.tilesMap;
        var width = MapBox.width;
        var height = MapBox.height;
        Query.ForEachEntity([Hotfixable](ref Position pos, ref SkillInfo skill_info, ref ObjCollisionTrigger trigger,
                                         Entity       skill_entity) =>
        {
            if (!trigger.Enabled) return;
            var action_entity = trigger.ActionContainer;
            var action =
                action_entity.GetComponent<ObjCollisionActionContainerInfo>().Meta.action;

            bool enemy_flag = (trigger.collision_flag  & ObjCollisionFlag.Enemy)  != 0;
            bool friend_flag = (trigger.collision_flag & ObjCollisionFlag.Friend) != 0;
            using var list = ShapeUtils.CircleOffsets(new Vector2Int((int)pos.x, (int)pos.y), trigger.radius);
            foreach (var p in list)
            {
                if (p is not { x: >= 0, y: >= 0 } || p.x >= width || p.y >= height) continue;
                var tile = tiles[p.x, p.y];

                if ((trigger.collision_flag & ObjCollisionFlag.Building) != 0 && tile.building != null)
                {
                    trigger.Val = tile.building;
                    action(ref trigger, ref skill_entity, ref action_entity);
                }

                if ((trigger.collision_flag & ObjCollisionFlag.Actor) != 0 && tile.hasUnits())
                {
                    foreach (var u in tile._units)
                    {
                        if (Toolbox.DistVec2Float(u.currentPosition, pos.as_v2) > trigger.radius) continue;
                        bool is_enemy = u.kingdom?.isEnemy(skill_info.user?.Base?.kingdom) ?? true;
                        if (enemy_flag && is_enemy)
                        {
                            trigger.Val = u;
                            action(ref trigger, ref skill_entity, ref action_entity);
                        }

                        if (friend_flag && !is_enemy)
                        {
                            trigger.Val = u;
                            action(ref trigger, ref skill_entity, ref action_entity);
                        }
                    }
                }
            }
        });
    }
}
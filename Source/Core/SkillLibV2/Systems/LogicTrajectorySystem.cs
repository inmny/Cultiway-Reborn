using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.Components.Position;
using Rotation = Cultiway.Core.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Systems;

public class LogicTrajectorySystem : QuerySystem<Position, Trajectory>
{
    private readonly ArchetypeQuery<Position, Trajectory, Rotation> with_rot_query;

    public LogicTrajectorySystem(EntityStore world)
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        Filter.WithoutAnyComponents(ComponentTypes.Get<Rotation>());

        var filter = new QueryFilter();
        filter.WithoutAnyTags(Tags.Get<TagPrefab>());
        with_rot_query = world.Query<Position, Trajectory, Rotation>(filter);
    }

    protected override void OnUpdate()
    {
        var dt = Tick.deltaTime;
        Query.ForEachEntity([Hotfixable](ref Position pos, ref Trajectory traj, Entity e) =>
        {
            pos.value += traj.meta.get_delta_position(dt, ref pos, ref traj, e);
        });
        with_rot_query.ForEachEntity([Hotfixable](ref Position pos, ref Trajectory traj, ref Rotation rot, Entity e) =>
        {
            Vector3 dp = traj.meta.get_delta_position(dt, ref pos, ref traj, e);
            pos.value += dp;
            if (traj.meta.towards_velocity)
                rot.value = dp;
            if (traj.meta.get_delta_rotation != null)
                rot.value += traj.meta.get_delta_rotation(dt, ref rot, ref traj, e);
        });
    }
}
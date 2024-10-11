using Cultiway.Core.SkillLib.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Position = Cultiway.Core.SkillLib.Components.Position;

namespace Cultiway.Core.SkillLib.Systems.Logic;

public class TrajectorySystem : QuerySystem<TrajectoryInfo, Position, Velocity, AliveTimer, SkillInfo>
{
    protected override void OnUpdate()
    {
        Query.WithoutAllTags(Tags.Get<PrefabTag>());

        var dt = Tick.deltaTime;
        Query.ForEachEntity((ref TrajectoryInfo traj,       ref Position pos, ref Velocity velo, ref AliveTimer timer,
                             ref SkillInfo      skill_info, Entity       e) =>
        {
            var new_pos_vec = traj.asset.update_trajectory_action(timer.alive_time, dt, ref pos.value, ref velo.scale,
                skill_info.target, skill_info.target_tile.posV3, ref e);
            pos.x = new_pos_vec.x;
            pos.y = new_pos_vec.y;
            pos.z = new_pos_vec.z;
        });
    }
}
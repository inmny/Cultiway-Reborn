using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Friflo.Engine.ECS;
using UnityEngine;
using Position = Cultiway.Core.SkillLibV2.Components.Position;
using Rotation = Cultiway.Core.SkillLibV2.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Predefined;

public static class Trajectories
{
    public static TrajectoryMeta GoForward { get; private set; }

    internal static void Init()
    {
        GoForward = new TrajectoryMeta
        {
            towards_velocity = true,
            calc = go_forward
        };
    }

    private static Vector3 go_forward(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        var dir = data.Get<Rotation>();
        var vel = data.Get<Velocity>();
        return Vector3.Scale(dir.value.normalized * dt, vel.scale);
    }
}
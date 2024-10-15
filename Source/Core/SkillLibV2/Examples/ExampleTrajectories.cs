using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Friflo.Engine.ECS;
using UnityEngine;
using Position = Cultiway.Core.SkillLibV2.Components.Position;
using Rotation = Cultiway.Core.SkillLibV2.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Examples;

public static class ExampleTrajectories
{
    public static TrajectoryMeta OnlyTowards { get; private set; }

    public static void Init()
    {
        OnlyTowards = new TrajectoryMeta();
        OnlyTowards.towards_velocity = true;
        OnlyTowards.calc = only_towards_calc;
    }

    private static Vector3 only_towards_calc(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        var dir = data.Get<Rotation>();
        var vel = data.Get<Velocity>();

        Vector3 dir_vec = dir.value.normalized;
        dir_vec.Scale(vel.scale);
        return dir_vec * dt;
    }
}
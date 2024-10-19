using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.SkillLibV2.Components.Position;
using Rotation = Cultiway.Core.SkillLibV2.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Predefined;

public static class Trajectories
{
    public static TrajectoryMeta GoForward                      { get; private set; }
    public static TrajectoryMeta GoTowardsTargetPosWithRotation { get; private set; }

    internal static void Init()
    {
        GoForward = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = go_forward
        };
        GoTowardsTargetPosWithRotation = new TrajectoryMeta
        {
            get_delta_position = go_towards_target_pos,
            get_delta_rotation = self_rotate
        };
    }

    [Hotfixable]
    private static Vector3 self_rotate(float dt, ref Rotation rot, ref Trajectory traj, Entity skill_entity)
    {
        var angle = Vector2.SignedAngle(Vector2.right, rot.in_plane) +
                    skill_entity.GetComponent<AngleVelocity>().value * dt;
        var magnitude = rot.in_plane.magnitude;
        if (magnitude >= 0.5f) return magnitude * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) - rot.in_plane;

        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    private static Vector3 go_towards_target_pos(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        Vector3 dir = data.Get<SkillTargetPos>().v3 - pos.value;
        var vel = data.Get<Velocity>();
        return Vector3.Scale(dir.normalized * dt, vel.scale);
    }

    private static Vector3 go_forward(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        var dir = data.Get<Rotation>();
        var vel = data.Get<Velocity>();
        return Vector3.Scale(dir.value.normalized * dt, vel.scale);
    }
}
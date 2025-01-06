using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV2.Components;
using Cultiway.Core.SkillLibV2.Components.TrajectoryParams;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;
using Position = Cultiway.Core.Components.Position;
using Rotation = Cultiway.Core.Components.Rotation;

namespace Cultiway.Core.SkillLibV2.Predefined;

public static class Trajectories
{
    public static TrajectoryMeta GoForward                      { get; private set; }
    public static TrajectoryMeta GoTowardsTargetObj { get; private set; }
    public static TrajectoryMeta GoTowardsTargetPos { get; private set; }
    public static TrajectoryMeta GoTowardsTargetPosWithRotation { get; private set; }
    public static TrajectoryMeta FallToGround { get; private set; }

    internal static void Init()
    {
        GoForward = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = go_forward
        };
        GoTowardsTargetPos = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = go_towards_target_pos
        };
        GoTowardsTargetObj = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = go_towards_target_obj
        };
        GoTowardsTargetPosWithRotation = new TrajectoryMeta
        {
            get_delta_position = go_towards_target_pos,
            get_delta_rotation = self_rotate
        };
        FallToGround = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = fall_to_ground
        };
    }

    public static TrajectoryMeta.GetDeltaScale GetLinearScale(Vector3 k, Vector3 final_scale = default)
    {
        return (float dt, ref Scale scale, ref Trajectory traj, Entity entity) =>
        {
            if (final_scale == default)
                return k * dt;
            var ds = (final_scale - scale.value);
            ds.Scale(k);
            return ds * dt;
        };
    }

    private static Vector3 fall_to_ground(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        var vel = skill_entity.Data.Get<Velocity>();
        return Vector3.Scale(Vector3.back * dt, vel.scale);
    }

    private static Vector3 go_towards_target_obj(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        BaseSimObject obj = data.Get<SkillTargetObj>().value;
        var vel = data.Get<Velocity>();
        if (obj == null) return Vector3.Scale(data.Get<Rotation>().value.normalized * dt, vel.scale);

        Vector3 dir = data.Get<SkillTargetObj>().value.curTransformPosition - pos.value;
        return Vector3.Scale(dir.normalized * dt, vel.scale);
    }

    [Hotfixable]
    private static Vector3 self_rotate(float dt, ref Rotation rot, ref Trajectory traj, Entity skill_entity)
    {
        var angle = Vector2.SignedAngle(Vector2.right, rot.in_plane) +
                    skill_entity.GetComponent<AngleVelocity>().value * dt;
        angle *= Mathf.Deg2Rad;
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
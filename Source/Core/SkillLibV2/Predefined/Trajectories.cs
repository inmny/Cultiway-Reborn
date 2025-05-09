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
    public static TrajectoryMeta GoTowardsTargetObjWithRotation { get; private set; }
    public static TrajectoryMeta FallToGround { get; private set; }
    public static TrajectoryMeta SelfSurround { get; private set; }
    public static TrajectoryMeta OutSurround { get; private set; }

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
        GoTowardsTargetObjWithRotation = new TrajectoryMeta
        {
            get_delta_position = go_towards_target_obj,
            get_delta_rotation = self_rotate
        };
        FallToGround = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = fall_to_ground
        };
        SelfSurround = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = self_surround
        };
        OutSurround = new TrajectoryMeta
        {
            towards_velocity = true,
            get_delta_position = out_surround
        };
    }
    [Hotfixable]
    private static Vector3 out_surround(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        var data = skill_entity.Data;
        var center = data.Get<SkillCaster>().AsActor.current_position;
        var curr_angle_deg = Vector2.SignedAngle(Vector2.right, pos.v2 - center);
        
        var curr_angle_rad = curr_angle_deg * Mathf.Deg2Rad;

        var old_radius = (pos.v2 - center).magnitude;
        var radius = old_radius + data.Get<OutVelocity>().value * dt;
        //ModClass.LogInfo($"[{skill_entity.Id}] outvelo: {data.Get<OutVelocity>().value}, radius: {(pos.v2 - center).magnitude}->{radius}");
        var velocity = data.Get<Velocity>();
        velocity.scale *= Mathf.Sqrt(old_radius);
        
        var target_angle_rad = curr_angle_rad + velocity.scale2.magnitude * dt / radius;
        var target_pos = new Vector3(Mathf.Cos(target_angle_rad) * radius + center.x, Mathf.Sin(target_angle_rad) * radius + center.y);
        Vector3 dir = target_pos - pos.value;
        var dp = Vector3.Scale(dir.normalized * dt, velocity.scale);
        
        if (dp.sqrMagnitude >= dir.sqrMagnitude)
            return dir;
        return dp;
    }

    [Hotfixable]
    private static Vector3 self_surround(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        var data = skill_entity.Data;
        var center = data.Get<SkillCaster>().AsActor.current_position;
        var curr_angle_deg = Vector2.SignedAngle(Vector2.right, pos.v2 - center);
        
        var curr_angle_rad = curr_angle_deg * Mathf.Deg2Rad;
        
        var radius = data.Get<SurroundRadius>().value;
        var velocity = data.Get<Velocity>();
        
        var target_angle_rad = curr_angle_rad + velocity.scale2.magnitude * dt / radius;
        var target_pos = new Vector3(Mathf.Cos(target_angle_rad) * radius + center.x, Mathf.Sin(target_angle_rad) * radius + center.y);
        Vector3 dir = target_pos - pos.value;
        var dp = Vector3.Scale(dir.normalized * dt, velocity.scale);
        if (dp.sqrMagnitude >= dir.sqrMagnitude)
            return dir;
        return dp;
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
    [Hotfixable]
    private static Vector3 fall_to_ground(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        var data = skill_entity.Data;
        ref var vel = ref data.Get<Velocity>();
        var dir = new Vector3(pos.x, pos.y, 0) - pos.value;
        
        var dp = Vector3.Scale(dir.normalized * dt, vel.scale);
        if (dp.sqrMagnitude >= dir.sqrMagnitude)
            return dir;
        return dp;
    }
    [Hotfixable]
    private static Vector3 go_towards_target_obj(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        BaseSimObject obj = data.Get<SkillTargetObj>().value;
        var vel = data.Get<Velocity>();
        if (obj == null) return vel.scale * dt;

        Vector3 dir = data.Get<SkillTargetObj>().value.cur_transform_position - pos.value;
        
        var dp = Vector3.Scale(dir.normalized * dt, vel.scale);
        if (dp.sqrMagnitude >= dir.sqrMagnitude)
            return dir;
        return dp;
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
    [Hotfixable]
    private static Vector3 go_towards_target_pos(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        Vector3 dir = data.Get<SkillTargetPos>().v3 - pos.value;
        var vel = data.Get<Velocity>();
        
        var dp = Vector3.Scale(dir.normalized * dt, vel.scale);
        if (dp.sqrMagnitude >= dir.sqrMagnitude)
            return dir;
        return dp;
    }

    private static Vector3 go_forward(float dt, ref Position pos, ref Trajectory traj, Entity skill_entity)
    {
        EntityData data = skill_entity.Data;
        var dir = data.Get<Rotation>();
        var vel = data.Get<Velocity>();
        return Vector3.Scale(dir.value.normalized * dt, vel.scale);
    }
}
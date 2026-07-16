using System;
using Cultiway.Content.Components;
using Cultiway.Content.Utils;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 法器在跟随驾驭者时的世界表现方案。新器形通过注册新的方案扩展，不需要修改渲染系统。
/// </summary>
public class ArtifactPresentationAsset : Asset
{
    /// <summary>
    /// 法器本体在标准角色缩放下的碰撞半径。
    /// </summary>
    public float body_radius = 0.12f;

    /// <summary>
    /// 法器施展能力时的像素倍率。1 表示法器单像素与驾驭者当前贴图单像素等大。
    /// </summary>
    public float active_pixel_scale = 1f;

    /// <summary>
    /// 根据驾驭者、调度状态和布局位置计算当前世界姿态。
    /// </summary>
    public Func<ArtifactPresentationContext, ArtifactPresentationPose> ResolvePose;

    /// <summary>未激活时的通用运动档案。</summary>
    public ArtifactMotionProfile idle_motion;

    /// <summary>Ready、Operating 和 Overloaded 状态使用的通用运动档案。</summary>
    public ArtifactMotionProfile active_motion;

    /// <summary>法器作为真实载具承托驾驭者时的世界姿态。</summary>
    public ArtifactMotionProfile vehicle_motion;

    public ArtifactPresentationPose Resolve(ArtifactPresentationContext context)
    {
        if (ResolvePose != null) return ResolvePose(context);
        ArtifactMotionProfile profile = context.control_state == ArtifactControlState.Cold
            ? idle_motion
            : active_motion ?? idle_motion;
        return ArtifactPresentationMotion.Resolve(profile, context);
    }

    public ArtifactPresentationPose ResolveVehicle(ArtifactPresentationContext context)
    {
        return ArtifactPresentationMotion.Resolve(vehicle_motion ?? active_motion ?? idle_motion, context);
    }
}

/// <summary>法器围绕驾驭者的通用布局方式。</summary>
public enum ArtifactMotionLayout
{
    /// <summary>按索引在驾驭者周围线性排开。</summary>
    Linear,

    /// <summary>固定在驾驭者朝向一侧，多件沿外侧和纵向展开。</summary>
    FacingSide,

    /// <summary>依次分布到驾驭者左右两侧。</summary>
    AlternatingSides,

    /// <summary>按容量分环绕驾驭者运行。</summary>
    Orbit,
}

/// <summary>
/// 可复用的法器世界运动档案。器形只配置布局、尺寸、摆动和轨道参数，不再各自实现逐帧函数。
/// </summary>
public sealed class ArtifactMotionProfile
{
    public ArtifactMotionLayout layout;
    public Vector2 offset;
    public Vector2 spacing;
    public float world_size = 0.6f;
    public float base_rotation;
    public float spread_rotation;
    public float side_rotation;
    public float bob_amplitude;
    public float bob_activity;
    public float sway_amplitude;
    public float sway_activity;
    public float speed = 1f;
    public float speed_activity;
    public float phase_step = 0.73f;
    public float activity_height;
    public bool flip_with_actor;

    public int orbit_capacity = 6;
    public float orbit_radius = 0.4f;
    public float orbit_radius_activity;
    public float orbit_ring_spacing = 0.16f;
    public float orbit_ring_height = 0.08f;
    public float orbit_vertical_amplitude = 0.2f;
    public bool reverse_odd_orbit_rings = true;
}

internal static class ArtifactPresentationMotion
{
    internal static ArtifactPresentationPose Resolve(
        ArtifactMotionProfile profile,
        ArtifactPresentationContext context)
    {
        float activity = ResolveActivity(context.control_state);
        float phase = context.time * (profile.speed + activity * profile.speed_activity) +
                      context.index * profile.phase_step;
        float spread = context.index - (context.count - 1) * 0.5f;
        float side = ResolveSide(profile.layout, context);
        Vector3 local = profile.layout switch
        {
            ArtifactMotionLayout.Linear => ResolveLinear(profile, spread),
            ArtifactMotionLayout.FacingSide => ResolveFacingSide(profile, spread, side),
            ArtifactMotionLayout.AlternatingSides => ResolveAlternatingSide(profile, context, side),
            ArtifactMotionLayout.Orbit => ResolveOrbit(profile, context, activity),
            _ => throw new ArgumentOutOfRangeException(),
        };
        if (profile.layout != ArtifactMotionLayout.Orbit)
        {
            local.y += activity * profile.activity_height +
                       Mathf.Sin(phase) * (profile.bob_amplitude + activity * profile.bob_activity);
        }

        float rotation = profile.base_rotation + spread * profile.spread_rotation + side * profile.side_rotation;
        rotation += Mathf.Sin(phase * 0.72f) *
                    (profile.sway_amplitude + activity * profile.sway_activity);
        return new ArtifactPresentationPose
        {
            position = context.actor.cur_transform_position + local * context.actor_scale,
            rotation = rotation,
            world_size = context.actor_scale * profile.world_size * context.control_state.GetStateScale(),
            flip_x = profile.flip_with_actor && context.actor.flip,
        };
    }

    private static Vector3 ResolveLinear(ArtifactMotionProfile profile, float spread)
    {
        return new Vector3(
            profile.offset.x + spread * profile.spacing.x,
            profile.offset.y + spread * profile.spacing.y,
            0f);
    }

    private static Vector3 ResolveFacingSide(ArtifactMotionProfile profile, float spread, float side)
    {
        return new Vector3(
            side * (profile.offset.x + Mathf.Abs(spread) * profile.spacing.x),
            profile.offset.y + spread * profile.spacing.y,
            0f);
    }

    private static Vector3 ResolveAlternatingSide(
        ArtifactMotionProfile profile,
        ArtifactPresentationContext context,
        float side)
    {
        int lane = context.index / 2;
        return new Vector3(
            side * (profile.offset.x + lane * profile.spacing.x),
            profile.offset.y + lane * profile.spacing.y,
            0f);
    }

    private static Vector3 ResolveOrbit(
        ArtifactMotionProfile profile,
        ArtifactPresentationContext context,
        float activity)
    {
        int capacity = Mathf.Max(1, profile.orbit_capacity);
        int ring = context.index / capacity;
        int indexInRing = context.index % capacity;
        int ringCount = Mathf.Min(capacity, context.count - ring * capacity);
        float direction = profile.reverse_odd_orbit_rings && ring % 2 != 0 ? -1f : 1f;
        float angle = context.time * direction * (profile.speed + activity * profile.speed_activity + ring * 0.08f) +
                      indexInRing * Mathf.PI * 2f / ringCount;
        float radius = profile.orbit_radius + activity * profile.orbit_radius_activity +
                       ring * profile.orbit_ring_spacing;
        return new Vector3(
            profile.offset.x + Mathf.Cos(angle) * radius,
            profile.offset.y + activity * profile.activity_height + ring * profile.orbit_ring_height +
            Mathf.Sin(angle) * profile.orbit_vertical_amplitude,
            0f);
    }

    private static float ResolveSide(ArtifactMotionLayout layout, ArtifactPresentationContext context)
    {
        return layout switch
        {
            ArtifactMotionLayout.FacingSide => context.actor.flip ? -1f : 1f,
            ArtifactMotionLayout.AlternatingSides => context.index % 2 == 0 ? 1f : -1f,
            _ => 0f,
        };
    }

    private static float ResolveActivity(ArtifactControlState state)
    {
        return state switch
        {
            ArtifactControlState.Cold => 0.25f,
            ArtifactControlState.Ready => 0.65f,
            ArtifactControlState.Operating => 1f,
            ArtifactControlState.Overloaded => 1.15f,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
    }
}

/// <summary>
/// 计算一个已装备法器世界姿态所需的只读上下文。
/// </summary>
public readonly struct ArtifactPresentationContext
{
    public readonly Actor actor;
    public readonly ArtifactControlState control_state;
    public readonly int index;
    public readonly int count;
    public readonly float actor_scale;
    public readonly float time;

    public ArtifactPresentationContext(
        Actor actor,
        ArtifactControlState controlState,
        int index,
        int count,
        float actorScale,
        float time)
    {
        this.actor = actor;
        control_state = controlState;
        this.index = index;
        this.count = count;
        actor_scale = actorScale;
        this.time = time;
    }
}

/// <summary>
/// 世界表现方案计算出的绝对位置与视觉姿态。
/// </summary>
public struct ArtifactPresentationPose
{
    public Vector3 position;
    public float rotation;
    public float world_size;
    public bool flip_x;
}

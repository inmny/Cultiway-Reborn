using System;
using Cultiway.Content.Components;
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
    /// 法器被能力释放到世界中时，其可见最长边相对标准角色缩放的比例。
    /// </summary>
    public float active_world_size = 1f;

    /// <summary>
    /// 根据驾驭者、调度状态和布局位置计算当前世界姿态。
    /// </summary>
    public Func<ArtifactPresentationContext, ArtifactPresentationPose> ResolvePose;
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
    public int sorting_order;
}

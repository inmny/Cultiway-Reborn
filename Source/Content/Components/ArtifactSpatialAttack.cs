using System.Collections.Generic;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Components;

/// <summary>
/// 飞剑持续空间攻击当前所处的运动阶段。
/// </summary>
public enum ArtifactSpatialAttackPhase
{
    Pursuing,
    Piercing,
    Cruising,
    Returning,
}

/// <summary>
/// 飞剑主动能力执行会话的私有运动状态。目标与施法者存放在通用 SkillContext 中，命中由 SkillV3 碰撞系统处理。
/// </summary>
public struct ArtifactSpatialAttackMotion : IComponent
{
    public Vector2 direction;
    /// <summary>品质参数给出的巡航基准速度。</summary>
    public float speed;
    /// <summary>经过朝向和运动阶段修正后的实际速度。</summary>
    public float current_speed;
    public float turn_rate;
    public float control_range;
    public float pierce_distance;
    public float repeat_cooldown;
    public float pierce_remaining;
    public float reacquire_in;
    public float repeat_ready_at;
    public float orbit_sign;
    public long last_target_key;
    public bool has_last_target;
    public HashSet<long> hit_target_keys;
    public ArtifactSpatialAttackPhase phase;
}

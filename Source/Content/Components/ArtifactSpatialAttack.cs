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
    public float speed;
    public float turn_rate;
    public float damage_multiplier;
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

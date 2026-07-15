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
/// 一件飞剑正在执行的持续空间攻击。运动、扫掠命中和世界渲染都作用于法器本体。
/// </summary>
public struct ArtifactSpatialAttackMotion : IComponent
{
    public long owner_actor_id;
    public long target_id;
    public bool target_is_actor;
    public Vector2 direction;
    public float speed;
    public float turn_rate;
    public float damage_multiplier;
    public float control_range;
    public float hit_radius;
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

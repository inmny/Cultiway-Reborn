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
/// 法宝本体飞行攻击完成一次穿刺后的后续行为。
/// </summary>
public enum ArtifactSpatialAttackMode
{
    /// <summary>持续寻找目标并反复穿刺，直到外部生命周期要求归位。</summary>
    ContinuousHunt,

    /// <summary>只追踪指定目标，完成穿刺或失去目标后立即归位。</summary>
    StrikeAndReturn,
}

/// <summary>
/// 法宝本体空间攻击执行会话的运动状态。目标与施法者存放在通用 SkillContext 中，命中由 SkillV3 碰撞系统处理。
/// </summary>
public struct ArtifactSpatialAttackMotion : IComponent
{
    public ArtifactSpatialAttackMode mode;
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
    /// <summary>单次出击最迟开始归位的世界时间；持续寻敌模式不使用。</summary>
    public float return_at;
    /// <summary>剑体实际碰撞后附加的冲击力。</summary>
    public float impact_force;
    public float orbit_sign;
    public long last_target_key;
    public bool has_last_target;
    public HashSet<long> hit_target_keys;
    public ArtifactSpatialAttackPhase phase;
}

using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Components;

/// <summary>分光剑阵中单道剑影当前所处的动作阶段。</summary>
public enum ArtifactSwordArrayBladePhase
{
    Forming,
    Arrayed,
    Launching,
    Piercing,
    Returning,
}

/// <summary>单道剑影的瞬时轨迹状态。剑影只是一份表现与命中载体，不是独立法器实体。</summary>
public struct ArtifactSwordArrayBladeState
{
    public int slot_index;
    public int return_slot_index;
    public ArtifactSwordArrayBladePhase phase;
    public Vector2 position;
    public Vector2 previous_position;
    public Vector2 direction;
    public Vector2 phase_origin;
    public Vector2 phase_control;
    public Vector2 phase_destination;
    public Actor target;
    public float phase_started_at;
    public float phase_duration;
    public float hit_at;
}

/// <summary>
/// 一次分光剑阵 SkillExecution 的完整运行状态。所有剑影共用一个执行实体，由轨迹系统逐帧推进。
/// </summary>
public struct ArtifactSwordArrayExecutionState : IComponent
{
    public ArtifactSwordArrayBladeState[] blades = [];
    public int sortie_sequence;
    public int max_simultaneous;
    public float started_at;
    public float duration;
    public float formation_duration;
    public float collapse_duration;
    public float launch_interval;
    public float next_launch_at;
    public float attack_range;
    public float ring_angle;
    public float angular_speed;

    public ArtifactSwordArrayExecutionState()
    {
    }
}

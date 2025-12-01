using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components.TrajParams;

/// <summary>
/// 轨迹转向速度参数，用于控制平滑转向的速度（每秒转向角度，单位：度/秒）
/// </summary>
public struct TurnRate : IComponent
{
    public float Value;
}


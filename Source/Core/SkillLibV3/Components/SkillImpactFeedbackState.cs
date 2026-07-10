using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 限制持续法术的命中音效频率，避免同一实体连续碰撞时堆叠声音。
/// </summary>
public struct SkillImpactFeedbackState : IComponent
{
    public float NextAllowedTime;
}

using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>记录持久技能下一次调度时间，以及各周期效果共同使用的上次结算边界。</summary>
public struct SkillPeriodicEffectState : IComponent
{
    public float NextTick;
    public float Interval;
    public float LastResolvedTime;
}

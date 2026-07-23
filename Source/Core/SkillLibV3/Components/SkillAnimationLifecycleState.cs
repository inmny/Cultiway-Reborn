using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

public enum SkillAnimationPhase : byte
{
    Appearance,
    Runtime,
    Dissipation,
    RecycleReady,
}

/// <summary>
/// 法术实体当前动画阶段及切换阶段时需要恢复的播放基准。
/// </summary>
public struct SkillAnimationLifecycleState : IComponent
{
    public SkillEntityAnimation Animation;
    public SkillAnimationPhase Phase;
    public float BaseFrameInterval;
    public bool BaseLoop;
    public bool HasRuntimeLoopOverride;
    public bool RuntimeLoopOverride;
}

/// <summary>当前动画阶段不允许运行轨迹。</summary>
public struct TagSkillAnimationNoMovement : ITag
{
}

/// <summary>当前动画阶段不允许检测技能碰撞。</summary>
public struct TagSkillAnimationNoCollision : ITag
{
}

/// <summary>当前动画阶段不允许触发掠地效果和 OnTravel 回调。</summary>
public struct TagSkillAnimationNoTravelEffects : ITag
{
}

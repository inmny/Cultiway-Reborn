using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>保存一个通用状态动画实例的可变表现参数。</summary>
public struct StatusAnimationState : IComponent
{
    /// <summary>相对状态资产基础尺寸的实例缩放倍率。</summary>
    public float ScaleMultiplier;
}

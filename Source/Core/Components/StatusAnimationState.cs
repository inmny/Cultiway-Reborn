using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>保存状态空间标记的缩放；类型和存档编号保留，以读取正式第一版资料。</summary>
public struct StatusAnimationState : IComponent
{
    /// <summary>相对状态资产基础尺寸的实例缩放倍率。</summary>
    public float ScaleMultiplier;
}

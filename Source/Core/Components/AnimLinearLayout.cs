using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>线性动画沿自身横轴呈现时采用的布局方式。</summary>
public enum AnimLinearLayoutMode : byte
{
    /// <summary>将单张动画帧缩放到指定世界长度，适合具有连续起止端点的光束。</summary>
    Stretch,

    /// <summary>保持动画帧自身比例并沿横轴重复，适合由连续段构成的墙体。</summary>
    Tile
}

/// <summary>记录线性动画的布局方式与当前需要覆盖的世界长度。</summary>
public struct AnimLinearLayout : IComponent
{
    public AnimLinearLayoutMode Mode;
    public float WorldLength;
}

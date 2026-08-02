using Cultiway.Core.Visuals;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using UnityEngine;

namespace Cultiway.Core.Components;

/// <summary>运动历史采用的几何表现。</summary>
public enum MotionRibbonShape
{
    /// <summary>围绕运动中心线生成固定宽度路径带。</summary>
    Path,

    /// <summary>在每个采样点的来源中心与运动点之间生成径向扫掠扇面。</summary>
    RadialSweep,

    /// <summary>从来源中心到运动点生成两端收束的轴向突刺枪芒。</summary>
    AxialThrust,
}

/// <summary>声明一个跟随实体真实运动历史绘制的双层带状拖尾。</summary>
public struct MotionRibbonTrail : IComponent
{
    /// <summary>是否记录并显示当前拖尾。</summary>
    public bool Enabled;

    /// <summary>选择固定宽度路径带或径向扫掠扇面。</summary>
    public MotionRibbonShape Shape;

    /// <summary>历史轨迹保留和离体淡出的秒数。</summary>
    public float HistorySeconds;

    /// <summary>相邻固定采样点之间的最小世界距离。</summary>
    public float MinSampleDistance;

    /// <summary>单条轨迹允许保留的最大采样点数。</summary>
    public int MaxPoints;

    /// <summary>中心实体带的世界宽度。</summary>
    public float CoreWidth;

    /// <summary>外围柔光带的世界宽度。</summary>
    public float GlowWidth;

    /// <summary>中心实体带采用的语义颜色。</summary>
    public Color CoreColor;

    /// <summary>外围柔光带采用的语义颜色。</summary>
    public Color GlowColor;

    /// <summary>需要来源锚点的几何形态每帧使用的世界中心。</summary>
    [Ignore]
    public Vector3 SourceOrigin;

    /// <summary>扫掠扇面内缘相对武器运动半径的比例。</summary>
    public float SweepInnerRadiusRatio;

    /// <summary>扫掠扇面实体层超过武器中心轨迹的外缘距离。</summary>
    public float SweepOuterExtension;

    /// <summary>扫掠扇面柔光层在实体层之外继续扩展的距离。</summary>
    public float SweepGlowExpansion;

    /// <summary>轴向枪芒起点沿攻击方向离开来源中心的距离。</summary>
    public float ThrustStartOffset;

    /// <summary>轴向枪芒尖端超过武器运动点的距离。</summary>
    public float ThrustTipExtension;

    /// <summary>中心实体带的不透明度。</summary>
    public float CoreAlpha;

    /// <summary>外围柔光带的不透明度。</summary>
    public float GlowAlpha;

    /// <summary>纹理沿路径重复一次所需的世界长度。</summary>
    public float TileLength;

    /// <summary>纹理沿路径流动的每秒循环数。</summary>
    public float FlowSpeed;
}

/// <summary>将 ECS 轨迹实体绑定到池化 Unity 视图，不参与存档。</summary>
internal struct MotionRibbonTrailBinder : IComponent
{
    [Ignore]
    public MotionRibbonTrailView Value;
}

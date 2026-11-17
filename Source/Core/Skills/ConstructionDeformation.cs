using System;

namespace Cultiway.Core.Skills;

/// <summary>相对模型绑定姿态的结构目标，空改动用于恢复展开。</summary>
public sealed class ConstructionDeformation
{
    /// <summary>三个模型方向的控制间距倍率，改变局部控制位置。</summary>
    public float[] Spacing = { 1f, 1f, 1f };

    /// <summary>实际命名控制的额外偏移和旋转。</summary>
    public ConstructionControlTarget[] Controls = Array.Empty<ConstructionControlTarget>();
}

/// <summary>一个真实控制相对绑定位置和方向的改动。</summary>
public sealed class ConstructionControlTarget
{
    /// <summary>资源中已有的控制名称。</summary>
    public string Control;

    /// <summary>在控制父空间叠加的三个位置数值。</summary>
    public float[] Offset = new float[3];

    /// <summary>相对绑定方向叠加的三个角度。</summary>
    public float[] Rotation = new float[3];
}

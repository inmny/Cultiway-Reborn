using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>同一部位从固定形体进入有限的浅层流散，不另建一次施放。</summary>
public sealed class ConstructionFlow
{
    /// <summary>每边格数，允许16至64。</summary>
    public int Cells = 48;
    /// <summary>未缩放的最小计算边长；高压或高速时扩大格宽，保留体积。</summary>
    public float Size = 24f;
    /// <summary>实际地形与水深驱动的重力系数。</summary>
    public float Gravity = 10f;
    /// <summary>每世界秒的动量阻力。</summary>
    public float Drag = .8f;
    /// <summary>每世界秒实际蒸散的体积比例。</summary>
    public float Evaporation = .03f;
    /// <summary>开放边界允许水流出，并记录去向；关闭时反射。</summary>
    public bool OpenBoundary = true;
}

/// <summary>输入边界核对一次的数值流散参数。</summary>
internal sealed class CompiledFlow
{
    /// <summary>固定网格边长。</summary>
    internal readonly int Cells;
    /// <summary>区域、重力、阻力及蒸散。</summary>
    internal readonly float Size, Gravity, Drag, Evaporation;
    /// <summary>是否记录越过区域的外流。</summary>
    internal readonly bool OpenBoundary;

    /// <summary>拒绝无效参数及无界网格；不在每个计算步重复核对。</summary>
    /// <param name="source">实际外部资料。</param>
    /// <param name="path">错误对应的部位。</param>
    internal CompiledFlow(ConstructionFlow source, string path)
    {
        if (source == null || source.Cells < 16 || source.Cells > 64 || !Finite(source.Size) || source.Size <= 0 || source.Size > 256 ||
            !Finite(source.Gravity) || source.Gravity < 0 || source.Gravity > 100 || !Finite(source.Drag) || source.Drag < 0 || source.Drag > 100 ||
            !Finite(source.Evaporation) || source.Evaporation < 0 || source.Evaporation > 10)
            throw new InvalidDataException(path + "：流散需要16至64格、正数区域及有效重力、阻力和蒸散");
        Cells = source.Cells; Size = source.Size; Gravity = source.Gravity; Drag = source.Drag; Evaporation = source.Evaporation; OpenBoundary = source.OpenBoundary;
    }

    /// <summary>外部数字须有限。</summary>
    /// <param name="value">输入值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

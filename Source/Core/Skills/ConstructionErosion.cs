using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>按明确材料耐受推进的侵蚀份额。</summary>
public sealed class ConstructionErosion
{
    /// <summary>整段投入份额，乘当前材料系数后得到实际损耗比例。</summary>
    public float Fraction = .75f;
}

/// <summary>入口核对后的有限侵蚀过程。</summary>
internal sealed class CompiledErosion
{
    /// <summary>沿共同世界时间推进的历时。</summary>
    internal readonly float Seconds;
    /// <summary>未乘材料耐受的投入份额。</summary>
    internal readonly float Fraction;
    /// <summary>解析实际参数，非法声明停在资料边界。</summary>
    /// <param name="source">原始操作。</param><param name="lifetime">整次上限。</param><param name="path">资料位置。</param>
    internal CompiledErosion(ConstructionOperation source, float lifetime, string path)
    {
        Seconds = source.Seconds; Fraction = source.Erosion?.Fraction ?? .75f;
        if (float.IsNaN(Seconds) || float.IsInfinity(Seconds) || Seconds <= 0 || Seconds > lifetime ||
            float.IsNaN(Fraction) || float.IsInfinity(Fraction) || Fraction <= 0 || Fraction > 1)
            throw new InvalidDataException(path + "：侵蚀需要有效历时及大于零且不超过一的份额");
    }
}

/// <summary>由实际累计推进得到的材料阶段。</summary>
public enum MaterialErosionStage
{
    /// <summary>没有接受侵蚀。</summary>
    Intact,
    /// <summary>已发生表面及实体损耗。</summary>
    Weathered,
    /// <summary>累计侵蚀达到脆弱阶段。</summary>
    Weakened,
    /// <summary>累计侵蚀已耗尽实体。</summary>
    Spent
}

/// <summary>已发生的侵蚀事实，体积和支付去向分别维护。</summary>
public struct AbilityErosion
{
    /// <summary>累计侵蚀占比，按实际保留比例叠加，最大为一。</summary>
    public float Exposure;
    /// <summary>已经耗蚀退出实体的真实体积。</summary>
    public double LostVolume;
    /// <summary>随实际损耗退出的原支付份额，不记为回能。</summary>
    public double SpentResource;
    /// <summary>从真实累计值读取当前阶段。</summary>
    public MaterialErosionStage Stage => Exposure >= .99999f ? MaterialErosionStage.Spent :
        Exposure >= .5f ? MaterialErosionStage.Weakened : Exposure > 0 ? MaterialErosionStage.Weathered : MaterialErosionStage.Intact;
}

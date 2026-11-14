using System;
using System.IO;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>从真实来源发出的有限射线，长度和转向都使用共同世界时间。</summary>
public sealed class ConstructionRay
{
    /// <summary>制作坐标中的整体长度上限，包含两端厚度；跟随普通尺寸缩放。</summary>
    public float Length = 8f;
    /// <summary>启动后是否继续读取本次目标的当前位置。</summary>
    public bool FollowTarget = true;
    /// <summary>每世界秒允许改变指向的角度，启动时直接采用实际目标方向。</summary>
    public float TurnRate = 360f;
}

/// <summary>创建边界复制的射线与制作控制，运行只读取有限数值。</summary>
internal sealed class CompiledRay
{
    /// <summary>实际制作控制与端部厚度。</summary>
    internal readonly FormSpan Span;
    /// <summary>本次长度、角速度和目标更新规则。</summary>
    internal readonly float Length, TurnRate;
    /// <summary>是否继续更新目标。</summary>
    internal readonly bool FollowTarget;

    /// <summary>在构造入口一次核对资源能力和数值。</summary>
    /// <param name="source">外部参数。</param><param name="resource">真实模型。</param><param name="path">资料位置。</param>
    internal CompiledRay(ConstructionRay source, FormResource resource, string path)
    {
        Span = resource.Span;
        if (source == null || Span == null) throw new InvalidDataException(path + "：射线需要明确参数及支持伸展的实际模型");
        if (float.IsNaN(source.Length) || float.IsInfinity(source.Length) || source.Length <= Span.Radius * 2f || source.Length > 10000f ||
            float.IsNaN(source.TurnRate) || float.IsInfinity(source.TurnRate) || source.TurnRate <= 0f || source.TurnRate > 7200f)
            throw new InvalidDataException(path + "：射线长度须容纳实际两端且不超过一万，角速度须在0至7200之间");
        Length = source.Length; FollowTarget = source.FollowTarget; TurnRate = source.TurnRate;
    }
}

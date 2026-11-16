using System;
using System.IO;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Core.Skills;

/// <summary>以所属部位为中心的有限水平范围运动，独立于接触伤害和形体尺寸。</summary>
public sealed class ConstructionRadialMotion
{
    /// <summary>世界球形范围半径；边缘速度为零，不随父体缩放重复放大。</summary>
    public float Radius = 2f;

    /// <summary>中心处每世界秒地图格数；负值向内牵引，正值向外推动。</summary>
    public float Speed = -.5f;

    /// <summary>按整次施放时钟重新确定运动的间隔；每段运动最多维持同样时长。</summary>
    public float Interval = .1f;

    /// <summary>实际作用的人物关系；范围运动只作用于可移动且站在地面的人物。</summary>
    public SkillUseTargetRelation Targets;
}

/// <summary>资料边界检查并独立复制后的范围运动；不保留旧词条或旅行回调。</summary>
public sealed class CompiledConstructionRadialMotion
{
    /// <summary>世界作用半径。</summary>
    public float Radius { get; }

    /// <summary>中心处有方向的水平速度。</summary>
    public float Speed { get; }

    /// <summary>固定世界时间间隔。</summary>
    public float Interval { get; }

    /// <summary>已经确定的目标关系。</summary>
    public SkillUseTargetRelation Targets { get; }

    /// <summary>创建、编辑和恢复共用一次输入检查。</summary>
    /// <param name="source">可编辑资料。</param><param name="path">资料位置。</param>
    internal CompiledConstructionRadialMotion(ConstructionRadialMotion source, string path)
    {
        Radius = source.Radius; Speed = source.Speed; Interval = source.Interval; Targets = source.Targets;
        if (float.IsNaN(Radius) || float.IsInfinity(Radius) || Radius <= 0f || Radius > 1000f ||
            float.IsNaN(Speed) || float.IsInfinity(Speed) || Math.Abs(Speed) > 1000f ||
            float.IsNaN(Interval) || float.IsInfinity(Interval) || Interval < 1f / 120f || Interval > 1f ||
            Targets is not (SkillUseTargetRelation.Hostile or SkillUseTargetRelation.Friendly or SkillUseTargetRelation.Self))
            throw new InvalidDataException(path + "：范围运动需要0至1000以内的正半径、正负1000以内速度、1/120至1秒间隔及有效人物关系");
    }
}

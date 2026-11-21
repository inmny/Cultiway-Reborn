using System;
using System.IO;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json;
using UnityEngine;

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

    /// <summary>开始范围作用的最早部位时点，允许形体先出现。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float StartAt;

    /// <summary>实际作用的人物关系；范围运动只作用于可移动且站在地面的人物。</summary>
    public SkillUseTargetRelation Targets;

    /// <summary>每次确认最多卷起多少件无主持物；零只作用于人物。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int GroundItemLimit;

    /// <summary>正值沿部位朝向形成风槽，单位为世界格。半径改为入口半宽。零保留原来的圆形推拉。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float ChannelLength;
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

    /// <summary>已确认的部位本地开始时点。</summary>
    internal float StartAt { get; }

    /// <summary>已经确定的目标关系。</summary>
    public SkillUseTargetRelation Targets { get; }

    /// <summary>实际携物上限，保持原物件、归属和落地规则。</summary>
    public int GroundItemLimit { get; }

    /// <summary>风槽的世界长度；零时保持圆形。</summary>
    internal float ChannelLength { get; }

    /// <summary>入口到出口的平均宽度与长度决定风槽的计价面积。</summary>
    internal double Area => ChannelLength > 0f
        ? Radius * ChannelLength * (1d + ConstructionWindChannel.OutletWidth)
        : Math.PI * Radius * Radius;

    /// <summary>创建、编辑和恢复共用一次输入检查。</summary>
    /// <param name="source">可编辑资料。</param><param name="path">资料位置。</param>
    internal CompiledConstructionRadialMotion(ConstructionRadialMotion source, string path)
    {
        Radius = source.Radius; Speed = source.Speed; Interval = source.Interval; Targets = source.Targets;
        GroundItemLimit = source.GroundItemLimit; ChannelLength = source.ChannelLength; StartAt = source.StartAt;
        if (float.IsNaN(Radius) || float.IsInfinity(Radius) || Radius <= 0f || Radius > 1000f ||
            float.IsNaN(Speed) || float.IsInfinity(Speed) || Math.Abs(Speed) > 1000f ||
            float.IsNaN(Interval) || float.IsInfinity(Interval) || Interval < 1f / 120f || Interval > 1f ||
            Targets is not (SkillUseTargetRelation.Hostile or SkillUseTargetRelation.Friendly or SkillUseTargetRelation.Self))
            throw new InvalidDataException(path + "：范围运动需要0至1000以内的正半径、正负1000以内速度、1/120至1秒间隔及有效人物关系");
        if (float.IsNaN(StartAt) || StartAt < 0f || StartAt > 3600f)
            throw new InvalidDataException(path + "：范围运动开始时点须在0至3600秒内");
        if (GroundItemLimit is < 0 or > 8)
            throw new InvalidDataException(path + "：每次范围运动可卷起零至八件无主持物");
        if (float.IsNaN(ChannelLength) || ChannelLength < 0f || ChannelLength > 1000f ||
            ChannelLength > 0f && (ChannelLength < 2f || Speed <= 0f))
            throw new InvalidDataException(path + "：风槽长需为零或2至1000格，启用时速度须为正值");
    }
}

/// <summary>真实入口、侧壁和出口共用的世界位置。数值取样不依赖网格或显示回调。</summary>
internal readonly struct ConstructionWindChannel
{
    internal const float OutletWidth = .38f;
    internal const float ReleaseAt = .88f;
    internal readonly Vector3 Centre;
    internal readonly Vector2 Forward, Side;
    internal readonly float Length, Width, Height, Progress;

    internal ConstructionWindChannel(in Matrix4x4 transform, CompiledConstructionRadialMotion rule, float progress)
    {
        Centre = transform.GetColumn(3);
        Vector2 direction = (Vector3)transform.GetColumn(2);
        if (direction.sqrMagnitude < .000001f) direction = (Vector3)transform.GetColumn(1);
        Forward = direction.normalized;
        Side = new Vector2(-Forward.y, Forward.x);
        Progress = progress;
        Length = rule.ChannelLength;
        Width = rule.Radius * Mathf.Lerp(1f, .69f, Mathf.Sin(Mathf.Clamp01((progress - .13f) / .84f) * Mathf.PI));
        Height = Mathf.Clamp(rule.Radius * .67f, 1f, 7f);
    }

    internal bool Pressing => Progress >= .13f && Progress < .86f;
    internal bool CanGather => Progress < .64f;
    internal float Opacity => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Progress / .16f)) *
                              Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - Progress) / .18f));

    internal Bounds Bounds
    {
        get
        {
            float x = Mathf.Abs(Forward.x) * Length + Mathf.Abs(Side.x) * Width * 2f;
            float y = Mathf.Abs(Forward.y) * Length + Mathf.Abs(Side.y) * Width * 2f;
            return new Bounds(Centre, new Vector3(x, y, Height * 2f));
        }
    }

    internal static float WidthAt(float along) => Mathf.Lerp(1f, OutletWidth, along);

    /// <summary>用足底或原物的实际位置取样。开口两端及两侧之外都没有受力。</summary>
    internal bool Sample(Vector3 point, out float along, out float lateral)
    {
        Vector3 offset = point - Centre;
        along = .5f + Vector2.Dot(offset, Forward) / Length;
        lateral = Vector2.Dot(offset, Side) / (Width * WidthAt(along));
        return along >= 0f && along <= 1f && Mathf.Abs(lateral) < 1f && Mathf.Abs(offset.z) < Height;
    }

    internal Vector2 Velocity(float speed, float along, float lateral)
    {
        float fade = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((Mathf.Abs(lateral) - .66f) / .34f));
        return speed * fade * (Forward * Mathf.Lerp(.58f, 1f, along) - Side * (.62f * lateral));
    }
}

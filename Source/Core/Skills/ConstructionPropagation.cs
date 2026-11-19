using System;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>沿真实连通地面的有限波面，距离与速度均按地图格计算。</summary>
public sealed class ConstructionPropagation
{
    /// <summary>沿实际地面路径的最远距离。</summary>
    public float Range = 64f;
    /// <summary>每世界秒经过的地面距离。</summary>
    public float Speed = 20f;
    /// <summary>一个位置从隆起到回落的世界秒数。</summary>
    public float Seconds = .6f;
    /// <summary>入口处波面的最高厚度。</summary>
    public float Height = 1f;
    /// <summary>入口周围只传力而不形成作用波面的半径，零保留原规则。</summary>
    public float InnerRadius;
    /// <summary>起点正下方允许入地的最大距离；零仍要求直接触地。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float GroundReach;
    /// <summary>前向扇面的总夹角；360表示向四周传开。</summary>
    public float Angle = 360f;
    /// <summary>每格路程的强度衰减率，强度按指数连续下降。</summary>
    public float Attenuation = .01f;
    /// <summary>相邻地块允许的最大实际高度差。</summary>
    public float MaximumStep = 2f;
    /// <summary>每到达一格实际消耗的作用量。</summary>
    public float CellCost = .001f;
    /// <summary>本过程最多到达的不同地块数。</summary>
    public int MaximumCells = 65536;
}

/// <summary>只在编译边界核对并复制，不持有可编辑输入。</summary>
internal sealed class CompiledPropagation
{
    /// <summary>已确定的距离、速度、时长和厚度。</summary>
    internal readonly float Range, Speed, Seconds, Height;
    /// <summary>入口安静半径及向下触地的最大距离。</summary>
    internal readonly float InnerRadius, GroundReach;
    /// <summary>方向边界、衰减、地形边界及每格消耗。</summary>
    internal readonly float MinimumDot, Attenuation, MaximumStep, CellCost;
    /// <summary>实际处理数量上限。</summary>
    internal readonly int MaximumCells;

    /// <summary>从外部资料创建独立参数。</summary>
    /// <param name="source">明确的传播资料。</param><param name="path">资料位置。</param>
    internal CompiledPropagation(ConstructionPropagation source, string path)
    {
        bool Valid(float v, float min, float max) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= min && v <= max;
        if (source == null || !Valid(source.Range, .01f, 4096f) || !Valid(source.Speed, .01f, 10000f) ||
            !Valid(source.Seconds, 1f / 60f, 3600f) || !Valid(source.Height, .01f, 100f) ||
            !Valid(source.InnerRadius, 0f, source.Range) || source.InnerRadius >= source.Range || !Valid(source.GroundReach, 0f, 100f) ||
            !Valid(source.Angle, 1f, 360f) || !Valid(source.Attenuation, 0f, 10f) ||
            !Valid(source.MaximumStep, 0f, 100f) || !Valid(source.CellCost, .000001f, 1000000f) ||
            source.MaximumCells < 1 || source.MaximumCells > 262144)
            throw new InvalidDataException(path + "：地表传播需要有效的距离、速度、波面、方向、消耗及1至262144格上限");
        Range = source.Range; Speed = source.Speed; Seconds = source.Seconds; Height = source.Height;
        InnerRadius = source.InnerRadius; GroundReach = source.GroundReach;
        MinimumDot = (float)Math.Cos(source.Angle * .5d * Math.PI / 180d);
        Attenuation = source.Attenuation; MaximumStep = source.MaximumStep; CellCost = source.CellCost;
        MaximumCells = source.MaximumCells;
    }
}

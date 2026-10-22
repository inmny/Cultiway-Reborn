using System;
using UnityEngine;
using Cultiway.Core.Skills;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>已经发布的浅层曲面数值；显示不改变水深，也不持有工作数组。</summary>
public sealed class FormLayerFrame
{
    /// <summary>同一过程的显示缓存标识。</summary>
    internal readonly object Owner;
    /// <summary>固定网格的边长。</summary>
    public readonly int Cells;
    /// <summary>实际格宽和地图左下边缘。</summary>
    public readonly float Cell;
    /// <summary>地图左下边缘。</summary>
    public readonly Vector2 Origin;
    /// <summary>不可变水深、动量及地面。</summary>
    internal readonly Vector3[] Water;
    /// <summary>发布时相同地面。</summary>
    internal readonly float[] Ground;
    /// <summary>发布时实际总量及去向。</summary>
    public readonly double Volume, Escaped, Evaporated, Recovered;
    /// <summary>用于跳过不可见曲面的完整保守范围。</summary>
    public readonly Bounds Bounds;
    /// <summary>实际显示的最低水深，与薄层作用区共用。</summary>
    public float DryDepth => Cell * AbilityFlow.DryFraction;

    /// <summary>只在完整发布时复制，回退和下一步不修改本帧。</summary>
    /// <param name="flow">已完成数值推进的实际水层。</param>
    internal FormLayerFrame(AbilityFlow flow)
    {
        Owner = flow.VisualOwner; Cells = flow.Settings.Cells; Cell = flow.Cell; Origin = flow.Origin;
        Water = (Vector3[])flow.Water.Clone(); Ground = (float[])flow.Ground.Clone();
        Volume = flow.Volume; Escaped = flow.Escaped; Evaporated = flow.Evaporated; Recovered = flow.Recovered;
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int i = 0; i < Water.Length; i++) { min = Mathf.Min(min, Ground[i]); max = Mathf.Max(max, Ground[i] + Water[i].x); }
        Bounds = new Bounds(new Vector3(Origin.x + Cell * Cells * .5f, Origin.y + Cell * Cells * .5f, (min + max) * .5f),
            new Vector3(Cell * (Cells + 1), Cell * (Cells + 1), Mathf.Max(.00001f, max - min)));
    }

    /// <summary>查询本次发布的实际水深，不暴露可变数组。</summary>
    /// <param name="x">横向格。</param><param name="y">纵向格。</param>
    public float Depth(int x, int y) => Water[y * Cells + x].x;
}

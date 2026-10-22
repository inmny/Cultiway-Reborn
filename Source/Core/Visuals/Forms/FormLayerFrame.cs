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

    /// <summary>本次完整水面里仍有实际水量的来源数。</summary>
    public readonly int Sources;

    /// <summary>同一格里至少两份来源实际交汇的格数。</summary>
    public readonly int MixedCells;

    /// <summary>用于跳过不可见曲面的完整保守范围。</summary>
    public readonly Bounds Bounds;

    /// <summary>共同格网发布时的相邻一格，避免区域缝隙和法向断开。</summary>
    private readonly Vector3[] borderWater;

    /// <summary>相邻格使用同一地表。</summary>
    private readonly float[] borderGround;

    /// <summary>本次不可变的外观分区；数组由发布者建立后不再修改。</summary>
    internal readonly float[][] AppearanceMasks;

    /// <summary>当前外观在同一次分区里的序号，负数表示不分区。</summary>
    internal readonly int AppearanceIndex;

    /// <summary>共同格网由左下区域唯一拥有相邻中心之间的面。</summary>
    internal bool SharedGrid => borderWater != null;
    /// <summary>实际显示的最低水深，与薄层作用区共用。</summary>
    public float DryDepth => Cell * AbilityFlow.DryFraction;

    /// <summary>只在完整发布时复制，回退和下一步不修改本帧。</summary>
    /// <param name="flow">已完成数值推进的实际水层。</param>
    /// <param name="owner">稳定显示缓存标识。</param><param name="border">不可变邻区水柱。</param>
    /// <param name="bed">不可变邻区地表。</param><param name="masks">同次发布的不可变外观分区。</param><param name="appearance">本外观序号。</param>
    internal FormLayerFrame(AbilityFlow flow, object owner = null, Vector3[] border = null, float[] bed = null,
        float[][] masks = null, int appearance = -1, int sources = 1, int mixedCells = 0)
    {
        Sources = sources;
        MixedCells = mixedCells;
        borderWater = border;
        borderGround = bed;
        AppearanceMasks = masks;
        AppearanceIndex = appearance;
        Owner = owner ?? flow.VisualOwner; Cells = flow.Settings.Cells; Cell = flow.Cell; Origin = flow.Origin;
        Water = (Vector3[])flow.Water.Clone(); Ground = (float[])flow.Ground.Clone();
        Volume = flow.Volume; Escaped = flow.Escaped; Evaporated = flow.Evaporated; Recovered = flow.Recovered;
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int i = 0; i < Water.Length; i++) { min = Mathf.Min(min, Ground[i]); max = Mathf.Max(max, Ground[i] + Water[i].x); }

        if (borderWater != null)
            for (int i = 0; i < borderWater.Length; i++)
            {
                min = Mathf.Min(min, borderGround[i]);
                max = Mathf.Max(max, borderGround[i] + borderWater[i].x);
            }

        Bounds = new Bounds(new Vector3(Origin.x + Cell * Cells * .5f, Origin.y + Cell * Cells * .5f, (min + max) * .5f),
            new Vector3(Cell * (Cells + 1), Cell * (Cells + 1), Mathf.Max(.00001f, max - min)));
    }

    /// <summary>共同水面读取相邻格；独立水层区域外仍为干燥。</summary>
    /// <param name="x">相对横格。</param><param name="y">相对纵格。</param>
    internal Vector3 WaterAt(int x, int y) => borderWater != null
        ? borderWater[(Mathf.Clamp(y, -1, Cells) + 1) * (Cells + 2) + Mathf.Clamp(x, -1, Cells) + 1]
        : x < 0 || y < 0 || x >= Cells || y >= Cells
            ? Vector3.zero
            : Water[y * Cells + x];

    /// <summary>与实际水柱相同的地面取样。</summary>
    /// <param name="x">相对横格。</param><param name="y">相对纵格。</param>
    internal float GroundAt(int x, int y) => borderGround != null
        ? borderGround[(Mathf.Clamp(y, -1, Cells) + 1) * (Cells + 2) + Mathf.Clamp(x, -1, Cells) + 1]
        : Ground[Mathf.Clamp(y, 0, Cells - 1) * Cells + Mathf.Clamp(x, 0, Cells - 1)];

    /// <summary>外观边界与水面一起插值，多个表面不会互相覆盖或留下三角孔。</summary>
    /// <param name="appearance">外观序号。</param><param name="x">相对横格。</param><param name="y">相对纵格。</param>
    internal float MaskAt(int appearance, int x, int y) =>
        AppearanceMasks[appearance][(Mathf.Clamp(y, -1, Cells) + 1) * (Cells + 2) + Mathf.Clamp(x, -1, Cells) + 1];

    /// <summary>查询本次发布的实际水深，不暴露可变数组。</summary>
    /// <param name="x">横向格。</param><param name="y">纵向格。</param>
    public float Depth(int x, int y) => Water[y * Cells + x].x;
}
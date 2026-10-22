using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实水格或原版零厚水面快照，传播不读取游戏对象或显示。</summary>
internal readonly struct ConductingWater
{
    /// <summary>共同格网坐标。</summary>
    internal readonly int X, Y;

    /// <summary>真实底部及水深。</summary>
    internal readonly float Bottom, Depth;

    /// <summary>独立水格的实际平面范围；零尺寸表示原共同格网。</summary>
    internal readonly Rect Area;

    /// <summary>零为共同水柱，负一为原版水面，正数为世界材料，更小负数为活动施放。</summary>
    internal readonly long Source;

    /// <summary>世界材料内的水格编号；活动施放由部位和水格共同编码。</summary>
    internal readonly int Index;

    /// <summary>原版二维水面没有体积资料，零厚面不能冒充可回收材料。</summary>
    internal bool Surface => Source == -1 && Depth == 0;

    /// <summary>保存已经聚合的共同水柱。</summary>
    /// <param name="x">横格。</param><param name="y">纵格。</param><param name="bottom">真实底部。</param><param name="depth">共同水深。</param>
    internal ConductingWater(int x, int y, float bottom, float depth)
    {
        X = x;
        Y = y;
        Bottom = bottom;
        Depth = depth;
        Area = default;
        Source = 0;
        Index = 0;
    }

    /// <summary>保留独立水格的实际边缘和来源，不重采样成更大的导电桥。</summary>
    /// <param name="x">显示横格。</param><param name="y">显示纵格。</param><param name="bottom">真实底部。</param>
    /// <param name="depth">真实深度；原版二维水面为零。</param><param name="area">真实平面边缘。</param>
    /// <param name="source">原版水面或独立材料编号。</param><param name="index">来源内水格编号。</param>
    internal ConductingWater(int x, int y, float bottom, float depth, Rect area, long source, int index)
    {
        X = x;
        Y = y;
        Bottom = bottom;
        Depth = depth;
        Area = area;
        Source = source;
        Index = index;
    }

    /// <summary>活动及世界水层采用相同边缘公式，避免小数格宽产生假干隙。</summary>
    /// <param name="flow">真实水层。</param><param name="index">实际水格。</param>
    internal static Rect FlowArea(AbilityFlow flow, int index)
    {
        int x = index % flow.Settings.Cells, y = index / flow.Settings.Cells;
        return Rect.MinMaxRect(flow.Origin.x + x * flow.Cell, flow.Origin.y + y * flow.Cell,
            flow.Origin.x + (x + 1) * flow.Cell, flow.Origin.y + (y + 1) * flow.Cell);
    }

    /// <summary>取得共同或独立水格的实际边缘。</summary>
    /// <param name="cell">共同格宽。</param>
    internal Rect Footprint(float cell) => Area.width > 0 ? Area : new Rect(X * cell, Y * cell, cell, cell);

    /// <summary>水柱必须实际相接；只有原版二维面允许零厚的水面边缘相接。</summary>
    /// <param name="other">候选相邻水柱。</param><param name="cell">共同格宽。</param>
    internal bool Touches(in ConductingWater other, float cell)
    {
        Rect a = Footprint(cell), b = other.Footprint(cell);
        float x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin),
            y = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin),
            z = Mathf.Min(Bottom + Depth, other.Bottom + other.Depth) - Mathf.Max(Bottom, other.Bottom);
        if (x < 0 || y < 0 || z < 0) return false;
        if ((Surface || other.Surface) && z == 0) return x > 0 || y > 0;
        return (x > 0 && y > 0) || (x > 0 && z > 0) || (y > 0 && z > 0);
    }

    /// <summary>真实水柱接触体，不用平面半径扩大高度。</summary>
    /// <param name="cell">共同格宽。</param>
    internal AbilityCapsule Shape(float cell)
    {
        Rect area = Footprint(cell);
        var p = new Vector3(area.center.x, area.center.y, Bottom + Depth * .5f);
        return new AbilityCapsule(0, p, p, 0, "water", halfExtents: new Vector3(area.width, area.height, Depth) * .5f);
    }
}

/// <summary>已经到达的一次人物作用，不含游戏对象。</summary>
internal readonly struct ConductionHit
{
    /// <summary>输入人物快照及水路格的序号。</summary>
    internal readonly int Target, Cell;

    /// <summary>此次作用前已经消耗的力量，包含本次完整人物消耗。</summary>
    internal readonly float Spent;

    /// <summary>保存真实到达。</summary>
    /// <param name="target">人物快照序号。</param><param name="cell">水路格序号。</param><param name="spent">累计真实消耗。</param>
    internal ConductionHit(int target, int cell, float spent)
    {
        Target = target;
        Cell = cell;
        Spent = spent;
    }
}

/// <summary>一次即时传播的纯数值结果；沿水路耗力，每个人物最多作用一次。</summary>
internal sealed class AbilityConduction
{
    /// <summary>按稳定到达顺序的真实水路。</summary>
    internal readonly List<ConductingWater> Route = new();

    /// <summary>按到达顺序的人物作用。</summary>
    internal readonly List<ConductionHit> Hits = new();

    /// <summary>各到达格的真实上游；入口为负一，用于回调改变水路后的分支作废。</summary>
    internal readonly List<int> Parents = new();

    /// <summary>沿途和完整人物作用合计消耗。</summary>
    internal float Spent { get; private set; }

    /// <summary>以真实入口建立队列，沿实际相接的水格按水路距离传播并去重。</summary>
    /// <param name="water">已经聚合的真实液态水柱。</param><param name="seeds">实际触水入口。</param>
    /// <param name="targets">按稳定人物编号排列的实际身体快照。</param><param name="settings">已经检查的参数。</param>
    /// <param name="cell">共同格宽。</param><param name="force">本部位现有余力。</param>
    internal AbilityConduction(IReadOnlyList<ConductingWater> water, IReadOnlyList<AbilityCapsule> seeds,
        IReadOnlyList<AbilityCapsule> targets, CompiledConduction settings, float cell, float force)
    {
        // 先按真实边缘建立索引，再按实际水路距离推进；不同分辨率和上下水层不合成假水柱。
        var eligible = new List<int>();
        for (int i = 0; i < water.Count; i++)
            if (water[i].Depth >= settings.MinimumDepth || water[i].Surface)
                eligible.Add(i);
        eligible.Sort((a, b) => Compare(water[a], water[b], cell));
        var waterIndex = new AbilityContactIndex();
        foreach (int i in eligible)
            waterIndex.Add(AbilityContactIndex.Swept(water[i].Shape(cell), water[i].Shape(cell)));
        var nearby = new List<int>();
        var distances = new float[eligible.Count];
        var parents = new int[eligible.Count];
        for (int i = 0; i < distances.Length; i++) distances[i] = float.PositiveInfinity;
        var queue = new SortedSet<(float Distance, int Index)>();
        foreach (var seed in seeds)
        {
            waterIndex.Query(AbilityContactIndex.Swept(seed, seed), nearby);
            foreach (int i in nearby)
            {
                var column = water[eligible[i]].Shape(cell);
                if (!AbilityPairSweep.Find(seed, seed, column, column, out _, out _, out _)) continue;
                distances[i] = 0;
                parents[i] = -1;
                queue.Add((0, i));
            }
        }

        var targetIndex = new AbilityContactIndex();
        for (int i = 0; i < targets.Count; i++) targetIndex.Add(AbilityContactIndex.Swept(targets[i], targets[i]));
        var visited = new HashSet<int>();
        var affected = new HashSet<int>();
        var adjacent = new List<int>();
        while (queue.Count > 0 && Route.Count < settings.MaximumCells)
        {
            var next = queue.Min;
            queue.Remove(next);
            if (!visited.Add(next.Index)) continue;
            var current = water[eligible[next.Index]];
            Rect area = current.Footprint(cell);
            float travel = settings.TravelCost * Mathf.Max(area.width, area.height);
            if (force - Spent < travel) continue;
            int routeIndex = Route.Count;
            Route.Add(current);
            Parents.Add(parents[next.Index]);
            Spent += travel;
            var shape = current.Shape(cell);
            targetIndex.Query(AbilityContactIndex.Swept(shape, shape), nearby);
            nearby.Sort();
            foreach (int target in nearby)
            {
                if (affected.Count >= settings.MaximumTargets || force - Spent < settings.DamagePerTarget) break;
                if (affected.Contains(target) || !AbilityPairSweep.Find(shape, shape, targets[target], targets[target],
                        out _, out _, out _)) continue;
                affected.Add(target);
                Spent += settings.DamagePerTarget;
                Hits.Add(new ConductionHit(target, routeIndex, Spent));
            }

            waterIndex.Query(AbilityContactIndex.Swept(shape, shape), adjacent);
            adjacent.Sort();
            foreach (int other in adjacent)
            {
                if (visited.Contains(other)) continue;
                var candidate = water[eligible[other]];
                if (!current.Touches(candidate, cell)) continue;
                Vector2 delta = candidate.Footprint(cell).center - area.center;
                float distance = next.Distance + Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
                if (distance > settings.Range || distance >= distances[other]) continue;
                queue.Remove((distances[other], other));
                distances[other] = distance;
                parents[other] = routeIndex;
                queue.Add((distance, other));
            }
        }
    }

    /// <summary>以实际位置和来源建立稳定顺序，输入容器顺序不决定传播。</summary>
    /// <param name="a">第一水格。</param><param name="b">第二水格。</param><param name="cell">共同格宽。</param>
    private static int Compare(in ConductingWater a, in ConductingWater b, float cell)
    {
        Rect left = a.Footprint(cell), right = b.Footprint(cell);
        int order = left.y.CompareTo(right.y);
        if (order == 0) order = left.x.CompareTo(right.x);
        if (order == 0) order = a.Bottom.CompareTo(b.Bottom);
        if (order == 0) order = a.Source.CompareTo(b.Source);
        if (order == 0) order = a.Index.CompareTo(b.Index);
        return order;
    }
}
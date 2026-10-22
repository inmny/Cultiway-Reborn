using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实水柱快照，传播不读取人物、世界对象或显示。</summary>
internal readonly struct ConductingWater
{
    /// <summary>共同格网坐标。</summary>
    internal readonly int X, Y;

    /// <summary>真实底部及水深。</summary>
    internal readonly float Bottom, Depth;

    /// <summary>保存已经聚合的共同水柱。</summary>
    /// <param name="x">横格。</param><param name="y">纵格。</param><param name="bottom">真实底部。</param><param name="depth">共同水深。</param>
    internal ConductingWater(int x, int y, float bottom, float depth)
    {
        X = x;
        Y = y;
        Bottom = bottom;
        Depth = depth;
    }

    /// <summary>真实水柱接触体，不用平面半径扩大高度。</summary>
    /// <param name="cell">共同格宽。</param>
    internal AbilityCapsule Shape(float cell)
    {
        var p = new Vector3((X + .5f) * cell, (Y + .5f) * cell, Bottom + Depth * .5f);
        return new AbilityCapsule(0, p, p, 0, "water", halfExtents: new Vector3(cell, cell, Depth) * .5f);
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

    /// <summary>以真实入口建立队列，再沿高度相交的四邻水柱传播并去重。</summary>
    /// <param name="water">已经聚合的真实液态水柱。</param><param name="seeds">实际触水入口。</param>
    /// <param name="targets">按稳定人物编号排列的实际身体快照。</param><param name="settings">已经检查的参数。</param>
    /// <param name="cell">共同格宽。</param><param name="force">本部位现有余力。</param>
    internal AbilityConduction(IReadOnlyList<ConductingWater> water, IReadOnlyList<AbilityCapsule> seeds,
        IReadOnlyList<AbilityCapsule> targets, CompiledConduction settings, float cell, float force)
    {
        // 先建空间索引，入口和人物都使用共同接触体；随后按等格宽的最短水路推进。
        var index = new Dictionary<(int, int), int>();
        var waterIndex = new AbilityContactIndex();
        for (int i = 0; i < water.Count; i++)
        {
            if (water[i].Depth < settings.MinimumDepth) continue;
            index.Add((water[i].X, water[i].Y), i);
        }

        var eligible = new List<int>(index.Values);
        eligible.Sort((a, b) =>
            water[a].Y != water[b].Y ? water[a].Y.CompareTo(water[b].Y) : water[a].X.CompareTo(water[b].X));
        foreach (int i in eligible)
            waterIndex.Add(AbilityContactIndex.Swept(water[i].Shape(cell), water[i].Shape(cell)));
        var nearby = new List<int>();
        var entries = new SortedSet<int>(Comparer<int>.Create((a, b) =>
            water[a].Y != water[b].Y ? water[a].Y.CompareTo(water[b].Y) : water[a].X.CompareTo(water[b].X)));
        foreach (var seed in seeds)
        {
            waterIndex.Query(AbilityContactIndex.Swept(seed, seed), nearby);
            foreach (int n in nearby)
            {
                int i = eligible[n];
                var column = water[i].Shape(cell);
                if (AbilityPairSweep.Find(seed, seed, column, column, out _, out _, out _)) entries.Add(i);
            }
        }

        var targetIndex = new AbilityContactIndex();
        for (int i = 0; i < targets.Count; i++) targetIndex.Add(AbilityContactIndex.Swept(targets[i], targets[i]));
        var visited = new HashSet<int>();
        var affected = new HashSet<int>();
        var queue = new Queue<(int Index, int Distance, int Parent)>();
        foreach (int i in entries)
            if (visited.Add(i))
                queue.Enqueue((i, 0, -1));
        float travel = settings.TravelCost * cell;
        while (queue.Count > 0 && Route.Count < settings.MaximumCells && force - Spent >= travel)
        {
            var next = queue.Dequeue();
            var current = water[next.Index];
            int routeIndex = Route.Count;
            Route.Add(current);
            Parents.Add(next.Parent);
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

            if ((next.Distance + 1) * cell > settings.Range) continue;
            Add(current.X - 1, current.Y);
            Add(current.X, current.Y - 1);
            Add(current.X + 1, current.Y);
            Add(current.X, current.Y + 1);

            void Add(int x, int y)
            {
                if (!index.TryGetValue((x, y), out int other) || visited.Contains(other)) return;
                var adjacent = water[other];
                if (Mathf.Min(current.Bottom + current.Depth, adjacent.Bottom + adjacent.Depth) <=
                    Mathf.Max(current.Bottom, adjacent.Bottom)) return;
                visited.Add(other);
                queue.Enqueue((other, next.Distance + 1, routeIndex));
            }
        }
    }
}
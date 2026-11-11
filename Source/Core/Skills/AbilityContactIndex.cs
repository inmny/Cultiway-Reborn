using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>按实际扫过的空间筛选形状；巨大区域回查全体，避免按格展开产生无界开销。</summary>
internal sealed class AbilityContactIndex
{
    /// <summary>普通空间格的边长。</summary>
    private readonly float cellSize;
    /// <summary>单个覆盖允许展开的格数，超过时进入大形状集合。</summary>
    private const int MaxCells = 4096;
    /// <summary>普通格与实际条目索引。</summary>
    private readonly Dictionary<(int, int, int), List<int>> cells = new();
    /// <summary>回收普通格列表，步间不反复分配。</summary>
    private readonly Stack<List<int>> pool = new();
    /// <summary>每个条目的实际包围范围。</summary>
    private readonly List<Bounds> bounds = new();
    /// <summary>覆盖过大的条目，不插入数百万普通格。</summary>
    private readonly List<int> large = new();
    /// <summary>查询去重标记，避免每次分配集合。</summary>
    private int[] visited = Array.Empty<int>();
    /// <summary>当前查询编号。</summary>
    private int stamp;

    /// <summary>人物形状沿用八格，模型三角面采用更细的共同索引。</summary>
    /// <param name="cellSize">实际格边长。</param>
    internal AbilityContactIndex(float cellSize = 8f) { this.cellSize = cellSize; }

    /// <summary>清除本轮索引并复用分配，不保留人物或形体对象。</summary>
    internal void Clear()
    {
        foreach (List<int> list in cells.Values) { list.Clear(); pool.Push(list); }
        cells.Clear(); bounds.Clear(); large.Clear();
    }

    /// <summary>插入实际扫掠范围；返回值与本轮插入顺序一致。</summary>
    /// <param name="value">真实空间包围范围。</param>
    internal int Add(Bounds value)
    {
        int id = bounds.Count; bounds.Add(value);
        Insert(id, value); return id;
    }

    /// <summary>更新本轮已登记对象，新范围立即可查；旧格引用按当前范围过滤并在轮末回收。</summary>
    /// <param name="id">本轮稳定的条目。</param>
    /// <param name="value">改变后的实际范围。</param>
    internal void Update(int id, Bounds value)
    { bounds[id] = value; Insert(id, value); }

    /// <summary>将当前范围写入覆盖格，查询去重不会因同一轮多次更新而重复返回。</summary>
    /// <param name="id">稳定条目。</param>
    /// <param name="value">当前范围。</param>
    private void Insert(int id, Bounds value)
    {
        if (!Cells(value, out Vector3Int min, out Vector3Int max)) { large.Add(id); return; }
        for (int x = min.x; x <= max.x; x++) for (int y = min.y; y <= max.y; y++) for (int z = min.z; z <= max.z; z++)
                {
                    var key = (x, y, z);
                    if (!cells.TryGetValue(key, out List<int> list)) cells.Add(key, list = pool.Count > 0 ? pool.Pop() : new List<int>());
                    list.Add(id);
                }
    }

    /// <summary>取得实际范围相交的候选，每个条目只返回一次。</summary>
    /// <param name="value">查询范围。</param>
    /// <param name="output">由调用方复用的结果集合。</param>
    internal void Query(Bounds value, List<int> output)
    {
        output.Clear();
        if (visited.Length < bounds.Count) Array.Resize(ref visited, Math.Max(bounds.Count, visited.Length * 2));
        if (stamp == int.MaxValue) { Array.Clear(visited, 0, visited.Length); stamp = 0; }
        stamp++;
        if (!Cells(value, out Vector3Int min, out Vector3Int max))
        { for (int id = 0; id < bounds.Count; id++) Include(id); return; }
        for (int x = min.x; x <= max.x; x++) for (int y = min.y; y <= max.y; y++) for (int z = min.z; z <= max.z; z++)
                    if (cells.TryGetValue((x, y, z), out List<int> list)) foreach (int id in list) Include(id);
        foreach (int id in large) Include(id);

        void Include(int id)
        {
            if (visited[id] == stamp) return;
            visited[id] = stamp;
            if (bounds[id].Intersects(value)) output.Add(id);
        }
    }

    /// <summary>实际双端胶囊扫过的包围范围，保留接触距离计算的微小数值容差。</summary>
    /// <param name="from">前态。</param>
    /// <param name="to">后态。</param>
    internal static Bounds Swept(in AbilityCapsule from, in AbilityCapsule to)
    {
        Vector3 radius = Vector3.one * (Mathf.Max(from.Radius, to.Radius) + .00002f);
        radius += Vector3.Max(from.HalfExtents, to.HalfExtents);
        var value = new Bounds();
        value.SetMinMax(Vector3.Min(Vector3.Min(from.A, from.B), Vector3.Min(to.A, to.B)) - radius,
            Vector3.Max(Vector3.Max(from.A, from.B), Vector3.Max(to.A, to.B)) + radius);
        return value;
    }

    /// <summary>计算有限格范围，巨大形体使用明确的大范围查询路径。</summary>
    /// <param name="value">实际空间范围。</param>
    /// <param name="min">最低格。</param>
    /// <param name="max">最高格。</param>
    private bool Cells(Bounds value, out Vector3Int min, out Vector3Int max)
    {
        min = Vector3Int.FloorToInt(value.min / cellSize); max = Vector3Int.FloorToInt(value.max / cellSize);
        return ((double)max.x - min.x + 1) * ((double)max.y - min.y + 1) * ((double)max.z - min.z + 1) <= MaxCells;
    }
}

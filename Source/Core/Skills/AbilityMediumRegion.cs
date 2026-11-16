using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只包含与真实入口相连的介质形状；空间索引筛选后仍以三维接触确认。</summary>
internal sealed class AbilityMediumRegion
{
    /// <summary>同一时点的实际形状，不读取游戏对象。</summary>
    private readonly IReadOnlyList<AbilityCapsule> shapes;
    /// <summary>仅用于筛选邻近形状。</summary>
    private readonly AbilityContactIndex index = new();
    /// <summary>入口实际连通的形状编号。</summary>
    private readonly HashSet<int> connected = new();
    /// <summary>连通形状的共同范围。</summary>
    internal Bounds Bounds { get; private set; }
    /// <summary>真实入口是否接触此介质。</summary>
    internal bool IsConnected => connected.Count > 0;

    /// <summary>从入口寻找有限连通部分，不跨过未接触的空隙。</summary>
    /// <param name="shapes">一个活动部位的真实作用形状。</param><param name="entry">实际触发人物的身体。</param>
    internal AbilityMediumRegion(IReadOnlyList<AbilityCapsule> shapes, in AbilityCapsule entry)
    {
        this.shapes = shapes;
        foreach (var shape in shapes) index.Add(AbilityContactIndex.Swept(shape, shape));
        var pending = new Queue<int>(); var nearby = new List<int>();
        index.Query(AbilityContactIndex.Swept(entry, entry), nearby);
        foreach (int id in nearby)
            if (Touches(shapes[id], entry)) { connected.Add(id); pending.Enqueue(id); }
        bool first = true;
        while (pending.Count > 0)
        {
            int id = pending.Dequeue(); var shape = shapes[id]; Bounds bounds = Bounds;
            if (first) { bounds = AbilityContactIndex.Swept(shape, shape); first = false; }
            else bounds.Encapsulate(AbilityContactIndex.Swept(shape, shape));
            Bounds = bounds;
            index.Query(AbilityContactIndex.Swept(shape, shape), nearby);
            foreach (int next in nearby)
                if (!connected.Contains(next) && Touches(shape, shapes[next])) { connected.Add(next); pending.Enqueue(next); }
        }
    }

    /// <summary>人物或建筑必须实际接触入口所在的同一连通部分。</summary>
    /// <param name="body">当前真实接收形状。</param>
    internal bool Contains(in AbilityCapsule body)
    {
        var nearby = new List<int>(); index.Query(AbilityContactIndex.Swept(body, body), nearby);
        foreach (int id in nearby) if (connected.Contains(id) && Touches(shapes[id], body)) return true;
        return false;
    }

    /// <summary>胶囊及水层薄箱共用已有的真实接触计算。</summary>
    /// <param name="left">介质形状。</param><param name="right">邻近形状。</param>
    private static bool Touches(in AbilityCapsule left, in AbilityCapsule right) =>
        AbilityPairSweep.Find(left, left, right, right, out _, out _, out _);
}

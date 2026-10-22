using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>共同水层内的一份来源；身份由内容维护，数值线程只认识稳定编号。</summary>
internal readonly struct AbilityWaterLayer
{
    /// <summary>当前记录及原来源链。</summary>
    internal readonly long Id, Source;

    /// <summary>本来源在一个世界区域里的实际水量、动量和逐格支付余量。</summary>
    internal readonly AbilityFlow Flow;

    /// <summary>只绑定已接纳的共同格网层。</summary>
    /// <param name="id">记录编号。</param><param name="source">原来源链。</param><param name="flow">独占数值层。</param>
    internal AbilityWaterLayer(long id, long source, AbilityFlow flow)
    {
        Id = id;
        Source = source;
        Flow = flow;
    }
}

/// <summary>相邻区共享旧水面及界面通量，按区并行，来源份额随局部质量流动。</summary>
internal sealed class AbilityWorldWater
{
    /// <summary>共同区域每边16格。</summary>
    internal const int Side = 16;

    /// <summary>相邻区域和逐来源层，在主线程建立后整个子步不改拓扑。</summary>
    private readonly Dictionary<(int, int), Tile> grid = new();

    /// <summary>固定顺序供现有工作池分发。</summary>
    private readonly Tile[] tiles;

    /// <summary>共同格宽和真实地图边界。</summary>
    private readonly float cell;

    /// <summary>材料越过地图边界后才算世界外流。</summary>
    private readonly Vector2 worldSize;

    /// <summary>当前共同稳定步长。</summary>
    internal float Seconds { get; private set; }

    /// <summary>可分给工作池的实际区域数量。</summary>
    internal int Count => tiles.Length;

    /// <summary>先聚合共同旧状态；后续计算不访问人物、网格或Unity资源。</summary>
    /// <param name="layers">当前同一材料的全部来源层。</param><param name="cell">地图共同格宽。</param>
    /// <param name="worldSize">真实地图大小。</param><param name="seconds">本轮尚未推进的世界秒数。</param>
    internal AbilityWorldWater(IEnumerable<AbilityWaterLayer> layers, float cell, Vector2 worldSize, float seconds)
    {
        this.cell = cell;
        this.worldSize = worldSize;
        foreach (var group in layers.OrderBy(l => l.Id).GroupBy(l => ((int)Math.Round(l.Flow.Origin.x / (Side * cell)),
                     (int)Math.Round(l.Flow.Origin.y / (Side * cell)))))
            grid.Add(group.Key, new Tile(group.Key, group.ToArray()));
        tiles = grid.Values.OrderBy(t => t.Position.Item2).ThenBy(t => t.Position.Item1).ToArray();
        float speed = .0001f;
        foreach (Tile tile in tiles)
        {
            for (int i = 0; i < Side * Side; i++)
            {
                Vector3 water = Vector3.zero;
                float gravity = 0;
                foreach (var layer in tile.Layers)
                {
                    Vector3 u = layer.Flow.Water[i];
                    water += u;
                    gravity += u.x * layer.Flow.Settings.Gravity;
                }

                tile.Water[i] = water;
                tile.Ground[i] = tile.Layers[0].Flow.Ground[i];
                tile.Gravity[i] = water.x > 0 ? gravity / water.x : 0;
                if (water.x > 1e-7f)
                    speed = Mathf.Max(speed,
                        Mathf.Max(Mathf.Abs(water.y), Mathf.Abs(water.z)) / water.x +
                        Mathf.Sqrt(tile.Gravity[i] * water.x));
            }
        }

        Seconds = Mathf.Min(seconds, cell * .2f / speed);
    }

    /// <summary>一个区域只写自己的差值，邻区读取共同旧值；可并行调用。</summary>
    /// <param name="index">区域序号。</param>
    internal void Compute(int index)
    {
        Tile tile = tiles[index];
        float factor = Seconds / cell;
        for (int y = 0; y < Side; y++)
        for (int x = 0; x < Side; x++)
        {
            int i = y * Side + x;
            Vector3 change = Vector3.zero;
            for (int direction = 0; direction < 4; direction++)
            {
                bool vertical = direction >= 2, negative = (direction & 1) == 0;
                int nx = x + (vertical ? 0 : negative ? -1 : 1), ny = y + (vertical ? negative ? -1 : 1 : 0);
                Tile neighbour = Neighbour(tile, ref nx, ref ny);
                int j = ny * Side + nx;
                Vector3 centre = tile.Water[i], other = neighbour?.Water[j] ?? Vector3.zero;
                float ground = tile.Ground[i], otherGround = neighbour?.Ground[j] ?? ground;
                bool outside = Outside(tile.Position.Item1 * Side + x + (vertical ? 0 : negative ? -1 : 1),
                    tile.Position.Item2 * Side + y + (vertical ? negative ? -1 : 1 : 0));
                Vector3 a = negative ? other : centre, b = negative ? centre : other;
                float za = negative ? otherGround : ground, zb = negative ? ground : otherGround;
                float gravity = neighbour == null || other.x <= 0 ? tile.Gravity[i] :
                    centre.x <= 0 ? neighbour.Gravity[j] : (tile.Gravity[i] + neighbour.Gravity[j]) * .5f;
                if (centre.x <= 0 && other.x <= 0) continue;
                Vector3 flux = Flux(a, b, za, zb, gravity, vertical, out Vector3 left, out Vector3 right);
                bool toNeighbour = negative ? flux.x < 0 : flux.x > 0;
                if (!outside && (neighbour == null || !HasDestinations(tile, i, neighbour, j, toNeighbour)))
                {
                    other = centre;
                    if (vertical) other.z = -other.z;
                    else other.y = -other.y;
                    a = negative ? other : centre;
                    b = negative ? centre : other;
                    za = zb = ground;
                    flux = Flux(a, b, za, zb, tile.Gravity[i], vertical, out left, out right);
                }

                if (outside && !toNeighbour)
                {
                    flux = left = right = Vector3.zero;
                }

                Vector3 delta = (negative ? right : -left) * factor;
                change += delta;
                Tile upstream = toNeighbour ? tile : neighbour;
                int at = toNeighbour ? i : j;
                if (upstream == null || upstream.Water[at].x <= 0) continue;
                double h = upstream.Water[at].x;
                float signed = negative ? flux.x : -flux.x;
                for (int source = 0; source < tile.Layers.Length; source++)
                {
                    var layer = tile.Layers[source];
                    int from = upstream.SourceIndex.TryGetValue(layer.Source, out int k) ? k : -1;
                    if (from < 0) continue;
                    AbilityFlow input = upstream.Layers[from].Flow;
                    float fraction = (float)(input.Water[at].x / h);
                    tile.Depth[source][i] += signed * factor * fraction;
                    tile.Budget[source][i] += signed * factor * (input.Budget[at] / h);
                    if (outside && toNeighbour)
                        tile.Escaped[source] += Math.Abs(flux.x) * factor * fraction * cell * cell;
                }
            }

            tile.Momentum[i] = new Vector2(tile.Water[i].y + change.y, tile.Water[i].z + change.z);
        }
    }

    /// <summary>全部区域计算完成后才提交新水深和动量，各区域仍可并行。</summary>
    /// <param name="index">区域序号。</param>
    internal void Commit(int index)
    {
        Tile tile = tiles[index];
        float area = cell * cell;
        for (int i = 0; i < Side * Side; i++)
        {
            float total = 0;
            for (int k = 0; k < tile.Layers.Length; k++)
                total += Mathf.Max(0, tile.Layers[k].Flow.Water[i].x + tile.Depth[k][i]);
            for (int k = 0; k < tile.Layers.Length; k++)
            {
                AbilityFlow flow = tile.Layers[k].Flow;
                float depth = Mathf.Max(0, flow.Water[i].x + tile.Depth[k][i]);
                float retained = Mathf.Exp(-flow.Settings.Evaporation * Seconds),
                    friction = Mathf.Exp(-flow.Settings.Drag * Seconds);
                flow.Evaporated += depth * (1d - retained) * area;
                float h = depth * retained;
                Vector2 q = total > 0 ? tile.Momentum[i] * (depth / total * retained * friction) : Vector2.zero;
                flow.Water[i] = new Vector3(h, q.x, q.y);
                flow.Budget[i] = Math.Max(0, flow.Budget[i] + tile.Budget[k][i]) * retained;
            }
        }

        for (int k = 0; k < tile.Layers.Length; k++)
        {
            AbilityFlow flow = tile.Layers[k].Flow;
            flow.Escaped += tile.Escaped[k];
            flow.RefreshTotals();
        }
    }

    /// <summary>只有上游实际有水的来源需要相应接收层，预算不足时整面回压。</summary>
    /// <param name="here">当前区域。</param><param name="i">当前格。</param><param name="other">相邻区域。</param>
    /// <param name="j">相邻格。</param><param name="outgoing">流动是否离开当前格。</param>
    private static bool HasDestinations(Tile here, int i, Tile other, int j, bool outgoing)
    {
        Tile from = outgoing ? here : other, to = outgoing ? other : here;
        int at = outgoing ? i : j;
        foreach (var layer in from.Layers)
            if (layer.Flow.Water[at].x > 0 && !to.SourceIndex.ContainsKey(layer.Source))
                return false;
        return true;
    }

    /// <summary>跨区格索引只改坐标，不复制相邻水量。</summary>
    /// <param name="tile">当前区域。</param><param name="x">横向格索引。</param><param name="y">纵向格索引。</param>
    private Tile Neighbour(Tile tile, ref int x, ref int y)
    {
        int tx = tile.Position.Item1, ty = tile.Position.Item2;
        if (x < 0)
        {
            tx--;
            x += Side;
        }
        else if (x >= Side)
        {
            tx++;
            x -= Side;
        }

        if (y < 0)
        {
            ty--;
            y += Side;
        }
        else if (y >= Side)
        {
            ty++;
            y -= Side;
        }

        return grid.TryGetValue((tx, ty), out Tile value) ? value : null;
    }

    /// <summary>真实地图边缘，区别于尚未分配的内区。</summary>
    /// <param name="x">全局横格。</param><param name="y">全局纵格。</param>
    private bool Outside(int x, int y) => x < 0 || y < 0 || x >= Mathf.CeilToInt(worldSize.x / cell) ||
                                          y >= Mathf.CeilToInt(worldSize.y / cell);

    /// <summary>沿用有限水层的静水重构，界面两边始终读取同一顺序和系数。</summary>
    /// <param name="a">左或下格。</param><param name="b">右或上格。</param><param name="za">左地面。</param>
    /// <param name="zb">右地面。</param><param name="g">共同界面重力。</param><param name="vertical">是否沿纵向。</param>
    /// <param name="left">左侧含地形源项的通量。</param><param name="right">右侧含地形源项的通量。</param>
    private static Vector3 Flux(Vector3 a, Vector3 b, float za, float zb, float g, bool vertical, out Vector3 left,
        out Vector3 right)
    {
        if (vertical)
        {
            a = new Vector3(a.x, a.z, a.y);
            b = new Vector3(b.x, b.z, b.y);
        }

        float ua = a.x > 1e-7f ? a.y / a.x : 0,
            va = a.x > 1e-7f ? a.z / a.x : 0,
            ub = b.x > 1e-7f ? b.y / b.x : 0,
            vb = b.x > 1e-7f ? b.z / b.x : 0;
        float bed = Mathf.Max(za, zb), ha = Mathf.Max(0, a.x + za - bed), hb = Mathf.Max(0, b.x + zb - bed);
        Vector3 l = new(ha, ha * ua, ha * va), r = new(hb, hb * ub, hb * vb);
        float speed = Mathf.Max(Mathf.Abs(ua) + Mathf.Sqrt(g * ha), Mathf.Abs(ub) + Mathf.Sqrt(g * hb));
        Vector3 flux = .5f * (new Vector3(l.y, l.y * ua + .5f * g * ha * ha, l.y * va) +
            new Vector3(r.y, r.y * ub + .5f * g * hb * hb, r.y * vb) - speed * (r - l));
        left = flux + Vector3.up * (.5f * g * (a.x * a.x - ha * ha));
        right = flux + Vector3.up * (.5f * g * (b.x * b.x - hb * hb));
        if (vertical)
        {
            left = new Vector3(left.x, left.z, left.y);
            right = new Vector3(right.x, right.z, right.y);
        }

        return flux;
    }

    /// <summary>一个区域的共同旧状态及各来源独占差值，不在工作阶段修改来源表。</summary>
    private sealed class Tile
    {
        /// <summary>共同格网的区域坐标。</summary>
        internal readonly (int, int) Position;

        /// <summary>稳定顺序的来源与查询。</summary>
        internal readonly AbilityWaterLayer[] Layers;

        /// <summary>原来源链到本区层的对应。</summary>
        internal readonly Dictionary<long, int> SourceIndex = new();

        /// <summary>共同旧水面、地表和有效重力。</summary>
        internal readonly Vector3[] Water = new Vector3[Side * Side];

        /// <summary>同一界面的静水承托。</summary>
        internal readonly float[] Ground = new float[Side * Side], Gravity = new float[Side * Side];

        /// <summary>完整通量后待分配的共同动量。</summary>
        internal readonly Vector2[] Momentum = new Vector2[Side * Side];

        /// <summary>本区各来源水深及支付差值。</summary>
        internal readonly float[][] Depth;

        /// <summary>按实际通量移动的支付份额。</summary>
        internal readonly double[][] Budget;

        /// <summary>各来源本步真正离开地图的水量。</summary>
        internal readonly double[] Escaped;

        /// <summary>仅在主线程组织一次，子步计算互不修改。</summary>
        /// <param name="position">区域坐标。</param><param name="layers">真实来源层。</param>
        internal Tile((int, int) position, AbilityWaterLayer[] layers)
        {
            Position = position;
            Layers = layers;
            Depth = new float[layers.Length][];
            Budget = new double[layers.Length][];
            Escaped = new double[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                SourceIndex.Add(layers[i].Source, i);
                Depth[i] = new float[Side * Side];
                Budget[i] = new double[Side * Side];
            }
        }
    }
}
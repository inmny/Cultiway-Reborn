using System;
using System.Collections.Generic;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 依据城市规模生成城墙边界 tile 列表（方形或圆形），支持内/外墙半径与墙体宽度，并预留出口。
/// 城墙本身使用原版 TopTileType（<see cref="TopTileLibrary.wall_order"/> / <see cref="TopTileLibrary.wall_wild"/> 等），
/// 由调用方通过 <see cref="MapAction.terraformTop(WorldTile, TopTileType, bool)"/> 放置。
/// </summary>
public static class WallShapeHelper
{
    // 每个 zone 的等效 tile 面积（经验值，用于由 zone 数推算半径）
    private const float TILES_PER_ZONE = 8f;
    private const int RADIUS_MIN = 3;
    private const int RADIUS_MAX = 60;   // 外墙 = 3×内墙，需要较大上限
    // 每个出口的宽度（tile 数，含两端）
    private const int EXIT_WIDTH_MIN = 2;
    private const int EXIT_WIDTH_MAX = 2; // 出入口固定两格宽，确保单位能正常通行

    /// <summary>内墙半径：包围城市全部领土（πr² ≈ zones×TILES_PER_ZONE）。</summary>
    public static int InnerRadius(City city)
    {
        if (city == null || city.zones.Count == 0) return RADIUS_MIN;
        int r = Mathf.RoundToInt(Mathf.Sqrt(city.zones.Count * TILES_PER_ZONE / Mathf.PI));
        return Mathf.Clamp(r, RADIUS_MIN, RADIUS_MAX);
    }

    /// <summary>由内墙半径推算外墙半径：与内墙的距离 = 内墙直径(2r) → r_outer = 3 × r_inner。</summary>
    public static int OuterRadiusFromInner(int innerRadius)
    {
        return Mathf.Clamp(innerRadius * 3, RADIUS_MIN, RADIUS_MAX);
    }

    /// <summary>外墙半径（按当前规模估算）。实际使用应以建墙时记录的半径为准。</summary>
    public static int OuterRadius(City city)
    {
        return OuterRadiusFromInner(InnerRadius(city));
    }

    /// <summary>
    /// 生成指定半径、宽度（同心圈数）的城墙 tile 列表。
    /// 出口数量与方位均<b>基于城市 id 确定性</b>生成（非全局随机），同一城市的内/外墙与多层同心圈缺口始终对齐。
    /// 路径上的<b>水域 tile 会贴着水岸绕行</b>（替换为最背离圆心的陆地邻居），相邻位置之间用<b>避水</b>的 4 连通路径补全。
    /// 城墙<b>只生成在陆地（沿水岸）</b>，四周皆水的深海段不放置（以水为天然屏障），可能在该处断开。
    /// </summary>
    public static List<WorldTile> ComputeWallRing(City city, bool circle, int radius, int width)
    {
        var result = new List<WorldTile>();
        if (city == null || city.zones.Count == 0 || radius <= 0 || width <= 0) return result;

        var center = city.getTile();
        if (center == null) return result;
        int cx = center.x, cy = center.y;

        long seed = city.data?.id ?? 0;
        int exitCount = 1 + HashInt(seed, 7, 0, 4); // 1~4 个出口
        var exits = ComputeExitAngles(exitCount, radius, seed);

        var placed = new HashSet<long>();
        for (int w = 0; w < width; w++)
        {
            int r = radius + w;
            var actual = BuildActualRing(cx, cy, circle, r, exits, out bool hasLandExit);
            int n = actual.Count;
            if (n == 0) continue;
            // 兜底：确保至少一条陆地通道——若无任何出口落在陆地上（如临海城市出口全开在水里），
            // 在最长陆地（连续非空）段中部强制开 2 格缺口
            if (!hasLandExit) ForceLandGap(actual);
            // 闭环连接相邻非空 tile；遇 null（出口 / 深海断点）则跳过该连接，保留出入口通道、不入水
            for (int i = 0; i < n; i++)
            {
                var cur = actual[i];
                var next = actual[(i + 1) % n];
                if (cur == null || next == null) continue;          // 断点：不连接
                AddUnique(cur, result, placed);
                Connect4Land(cur.x, cur.y, next.x, next.y, result, placed);
            }
        }
        return result;
    }

    /// <summary>
    /// 生成单圈实际边界序列（顺时针有序）：水域 tile 替换为最背离圆心的陆地邻居（贴岸绕行）。
    /// <b>出口方位与四周皆水的深海 tile 记为 null（断点）</b>——调用方据此在断点处断开，
    /// 既保留出入口通道，又不在水里生成。
    /// </summary>
    /// <param name="hasLandExit">输出：是否存在落在<b>陆地</b>上的出口断点（真正可通行的通道）。</param>
    private static List<WorldTile> BuildActualRing(int cx, int cy, bool circle, int r, List<ExitRange> exits, out bool hasLandExit)
    {
        hasLandExit = false;
        var ring = new List<WorldTile>();
        var ringSeen = new HashSet<long>();
        if (circle) FillCircleEdge(cx, cy, r, ring, ringSeen);
        else FillSquareEdge(cx, cy, r, ring, ringSeen);

        var actual = new List<WorldTile>();
        foreach (var t in ring)
        {
            if (t == null) continue;
            float ang = NormalizeAngle(Mathf.Atan2(t.y - cy, t.x - cx));
            if (InAnyExit(ang, exits))
            {
                actual.Add(null); // 出口断点
                if (!t.IsWater()) hasLandExit = true; // 出口落在陆地 → 可通行通道
                continue;
            }
            WorldTile a = t.IsWater() ? FindLandNeighbor(t, cx, cy) : t;
            actual.Add(a); // a==null（深海）同为断点
        }
        return actual;
    }

    /// <summary>
    /// 在 actual 中<b>最长的连续非空（陆地）段</b>中部强制开 2 格缺口，
    /// 用于"出口全部落在水上"时保证至少一条陆地通道。
    /// </summary>
    private static void ForceLandGap(List<WorldTile> actual)
    {
        int n = actual.Count;
        if (n == 0) return;
        // 闭环找最长连续非空段
        int bestStart = 0, bestLen = 0;
        int curStart = 0, curLen = 0;
        for (int i = 0; i < 2 * n && curLen < n; i++)
        {
            int idx = i % n;
            if (actual[idx] != null)
            {
                if (curLen == 0) curStart = idx;
                curLen++;
                if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }
            }
            else curLen = 0;
        }
        if (bestLen > n) bestLen = n;
        if (bestLen >= 4) // 足够长的陆地城墙段：开 2 格缺口
        {
            int mid = (bestStart + bestLen / 2) % n;
            actual[mid] = null;
            actual[(mid + 1) % n] = null;
        }
        else if (bestLen >= 2) actual[(bestStart + bestLen / 2) % n] = null;
    }

    /// <summary>取水域 tile 四邻接中<b>最背离圆心</b>的陆地邻居（贴着水岸向外绕行）；四周皆水则返回 null。</summary>
    private static WorldTile FindLandNeighbor(WorldTile water, int cx, int cy)
    {
        if (water == null) return null;
        WorldTile best = null;
        int bestDist = -1;
        foreach (var n in water.neighbours)
        {
            if (n == null || n.IsWater()) continue;
            int d = (n.x - cx) * (n.x - cx) + (n.y - cy) * (n.y - cy);
            if (d > bestDist) { bestDist = d; best = n; }
        }
        return best;
    }

    /// <summary>
    /// 返回该圈每个出口方位的代表 tile（缺口中心），用于在出入口处放置守门建筑（如箭塔）。
    /// 方位基于城市 id 确定性，与 <see cref="ComputeWallRing"/> 的开口完全对齐。
    /// </summary>
    public static List<WorldTile> ComputeExitTiles(City city, bool circle, int radius)
    {
        var result = new List<WorldTile>();
        if (city == null || city.zones.Count == 0 || radius <= 0) return result;

        var center = city.getTile();
        if (center == null) return result;
        int cx = center.x, cy = center.y;

        long seed = city.data?.id ?? 0;
        int exitCount = 1 + HashInt(seed, 7, 0, 4);
        var exits = ComputeExitAngles(exitCount, radius, seed);
        foreach (var er in exits)
        {
            float c = Mathf.Cos(er.center);
            float s = Mathf.Sin(er.center);
            int x, y;
            if (circle)
            {
                x = Mathf.RoundToInt(cx + radius * c);
                y = Mathf.RoundToInt(cy + radius * s);
            }
            else
            {
                // 方形：取射线与方形边的交点 = 圆点除以 max(|cos|,|sin|)
                float m = Mathf.Max(Mathf.Abs(c), Mathf.Abs(s));
                if (m < 1e-4f) { x = cx; y = cy; }
                else
                {
                    x = Mathf.RoundToInt(cx + radius * c / m);
                    y = Mathf.RoundToInt(cy + radius * s / m);
                }
            }
            if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) continue;
            var t = World.world.GetTileSimple(x, y);
            if (t == null) continue;
            if (t.IsWater()) t = FindLandNeighbor(t, cx, cy); // 出口落在水上则贴岸放塔
            if (t != null) result.Add(t);
        }
        return result;
    }

    /// <summary>指定半径、宽度的现存城墙比例（实际边界——含水岸贴行位置——中已是墙的比例，0~1）。用于判断是否被摧毁。</summary>
    public static float ExistingWallRatio(City city, bool circle, int radius, int width)
    {
        if (city == null || city.zones.Count == 0 || radius <= 0 || width <= 0) return 0f;
        var center = city.getTile();
        if (center == null) return 0f;
        int cx = center.x, cy = center.y;

        long seed = city.data?.id ?? 0;
        int exitCount = 1 + HashInt(seed, 7, 0, 4);
        var exits = ComputeExitAngles(exitCount, radius, seed);

        int total = 0;
        int existing = 0;
        for (int w = 0; w < width; w++)
        {
            int r = radius + w;
            foreach (var t in BuildActualRing(cx, cy, circle, r, exits, out _))
            {
                if (t == null) continue; // 断点（出口 / 深海）不计入
                total++;
                if (IsWallTop(t)) existing++;
            }
        }
        return total == 0 ? 0f : (float)existing / total;
    }

    private static bool IsWallTop(WorldTile t)
        => t.top_type != null && t.top_type.wall;

    private static void AddTile(int x, int y, List<WorldTile> list, HashSet<long> seen)
    {
        if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) return;
        var tile = World.world.GetTileSimple(x, y);
        if (tile == null) return;
        long key = (long)y * MapBox.width + x;
        if (!seen.Add(key)) return; // 已加入则跳过
        list.Add(tile);
    }

    /// <summary>去重加入一个已有 tile（复用 AddTile 的越界/去重逻辑）。</summary>
    private static void AddUnique(WorldTile t, List<WorldTile> list, HashSet<long> seen)
    {
        if (t != null) AddTile(t.x, t.y, list, seen);
    }

    private static void FillSquareEdge(int cx, int cy, int r, List<WorldTile> list, HashSet<long> seen)
    {
        // 顺时针：上(→) → 右(↓) → 下(←) → 左(↑)，沿边逐格，天然 4 连通
        for (int x = cx - r; x <= cx + r; x++) AddTile(x, cy + r, list, seen);
        for (int y = cy + r - 1; y >= cy - r; y--) AddTile(cx + r, y, list, seen);
        for (int x = cx + r - 1; x >= cx - r; x--) AddTile(x, cy - r, list, seen);
        for (int y = cy - r + 1; y < cy + r; y++) AddTile(cx - r, y, list, seen);
    }

    /// <summary>
    /// 圆形边界：密集角度采样后，相邻采样点之间用 4 连通路径补全，
    /// 避免出现仅对角相连的墙段（会导致视觉断裂 / 缝隙可被穿越）。
    /// </summary>
    private static void FillCircleEdge(int cx, int cy, int r, List<WorldTile> list, HashSet<long> seen)
    {
        int steps = Mathf.Max(16, r * 7); // 周长 ≈ 2πr，每步弧长 &lt; 1 tile
        int px = Mathf.RoundToInt(cx + r * Mathf.Cos(0f));
        int py = Mathf.RoundToInt(cy + r * Mathf.Sin(0f));
        AddTile(px, py, list, seen);
        for (int i = 1; i <= steps; i++)
        {
            float a = Mathf.PI * 2f * i / steps;
            int nx = Mathf.RoundToInt(cx + r * Mathf.Cos(a));
            int ny = Mathf.RoundToInt(cy + r * Mathf.Sin(a));
            Connect4(px, py, nx, ny, list, seen);
            px = nx;
            py = ny;
        }
    }

    /// <summary>
    /// 生成 (ax,ay) -> (bx,by) 的 4 连通路径（不含起点、含终点）：
    /// 先沿 x 逐格、再沿 y 逐格，保证每步共边相邻，杜绝任何对角相连。
    /// </summary>
    private static void Connect4(int ax, int ay, int bx, int by, List<WorldTile> list, HashSet<long> seen)
    {
        int x = ax, y = ay;
        int sx = Math.Sign(bx - ax);
        int sy = Math.Sign(by - ay);
        while (x != bx) { x += sx; AddTile(x, y, list, seen); }
        while (y != by) { y += sy; AddTile(x, y, list, seen); }
    }

    /// <summary>
    /// 生成 (ax,ay) -> (bx,by) 的 4 连通路径（不含起点、含终点），<b>只在陆地放置</b>（水 tile 跳过）。
    /// 用于沿水岸连接相邻边界点，确保城墙不在水里生成；若两点间必须穿水，则该段断开。
    /// </summary>
    private static void Connect4Land(int ax, int ay, int bx, int by, List<WorldTile> list, HashSet<long> seen)
    {
        int x = ax, y = ay;
        int sx = Math.Sign(bx - ax);
        int sy = Math.Sign(by - ay);
        while (x != bx) { x += sx; AddTileIfLand(x, y, list, seen); }
        while (y != by) { y += sy; AddTileIfLand(x, y, list, seen); }
    }

    /// <summary>只在陆地（非水）去重加入 tile。</summary>
    private static void AddTileIfLand(int x, int y, List<WorldTile> list, HashSet<long> seen)
    {
        if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) return;
        var tile = World.world.GetTileSimple(x, y);
        if (tile == null || tile.IsWater()) return; // 水/越界 → 跳过，不在水里生成
        long key = (long)y * MapBox.width + x;
        if (!seen.Add(key)) return;
        list.Add(tile);
    }

    private struct ExitRange
    {
        public float center;
        public float halfWidth;
    }

    /// <summary>
    /// 基于 <paramref name="seed"/>（城市 id）确定性生成 exitCount 个出口的角度方位（均匀分布 + 段内确定性偏移）。
    /// 角度宽度基于该半径换算，保证开口至少 EXIT_WIDTH 格。
    /// </summary>
    private static List<ExitRange> ComputeExitAngles(int exitCount, int radius, long seed)
    {
        var list = new List<ExitRange>();
        if (exitCount <= 0) return list;
        int widthTiles = EXIT_WIDTH_MIN + HashInt(seed, 0, 0, EXIT_WIDTH_MAX - EXIT_WIDTH_MIN + 1); // 2~3
        float halfWidth = widthTiles * 0.5f / Mathf.Max(1, radius); // 弧度半宽
        float seg = Mathf.PI * 2f / exitCount;
        for (int e = 0; e < exitCount; e++)
        {
            float jitter = (HashFloat(seed, e + 1) - 0.5f) * seg * 0.8f;
            float center = seg * (e + 0.5f) + jitter;
            list.Add(new ExitRange { center = center, halfWidth = halfWidth });
        }
        return list;
    }

    private static float NormalizeAngle(float a)
    {
        a %= Mathf.PI * 2f;
        if (a < 0) a += Mathf.PI * 2f;
        return a;
    }

    /// <summary>判断角度是否落入任一出口方位（按环绕最短角距判断，自动处理跨 0/2π 边界）。</summary>
    private static bool InAnyExit(float angle, List<ExitRange> exits)
    {
        const float TAU = Mathf.PI * 2f;
        foreach (var er in exits)
        {
            float d = Mathf.Abs(angle - er.center);
            d = Mathf.Min(d, TAU - d); // 环绕最短角距
            if (d <= er.halfWidth) return true;
        }
        return false;
    }

    /// <summary>基于种子的确定性整数散列，落在 [min, maxExclusive)。</summary>
    private static int HashInt(long seed, int salt, int min, int maxExclusive)
    {
        long h = (seed ^ ((long)salt * 1099511628211L)) & 0x7FFFFFFFL;
        h = (h * 2654435761L) & 0x7FFFFFFFL;
        return min + (int)(h % (maxExclusive - min));
    }

    /// <summary>基于种子的确定性浮点散列，落在 [0, 1)。</summary>
    private static float HashFloat(long seed, int salt)
    {
        long h = (seed ^ ((long)salt * 1099511628211L)) & 0x7FFFFFFFL;
        h = (h * 2654435761L) & 0x7FFFFFFFL;
        return h / (float)0x7FFFFFFFL;
    }
}

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
    private const int EXIT_WIDTH_MAX = 3;

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
    /// 生成指定半径、宽度（同心圈数）的城墙 tile 列表，剔除水域与出口方位。
    /// 出口数量与方位均<b>基于城市 id 确定性</b>生成（非全局随机），因此同一城市的内/外墙与
    /// 多层同心圈的缺口始终对齐，且重建后位置一致。
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

        var seen = new HashSet<long>();
        for (int w = 0; w < width; w++)
        {
            int r = radius + w;
            var ring = new List<WorldTile>();
            if (circle) FillCircleEdge(cx, cy, r, ring, seen);
            else FillSquareEdge(cx, cy, r, ring, seen);
            foreach (var t in ring)
            {
                if (t == null || t.IsWater()) continue;            // 跳过水域
                float ang = NormalizeAngle(Mathf.Atan2(t.y - cy, t.x - cx));
                if (InAnyExit(ang, exits)) continue;               // 出口方位，跳过
                result.Add(t);
            }
        }
        return result;
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
            if (t != null) result.Add(t);
        }
        return result;
    }

    /// <summary>指定半径、宽度的现存城墙比例（陆地边界中已是墙的比例，0~1）。用于判断是否被摧毁。</summary>
    public static float ExistingWallRatio(City city, bool circle, int radius, int width)
    {
        if (city == null || city.zones.Count == 0 || radius <= 0 || width <= 0) return 0f;
        var center = city.getTile();
        if (center == null) return 0f;
        int cx = center.x, cy = center.y;

        var seen = new HashSet<long>();
        int total = 0;
        int existing = 0;
        for (int w = 0; w < width; w++)
        {
            int r = radius + w;
            var ring = new List<WorldTile>();
            if (circle) FillCircleEdge(cx, cy, r, ring, seen);
            else FillSquareEdge(cx, cy, r, ring, seen);
            foreach (var t in ring)
            {
                if (t == null || t.IsWater()) continue;
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

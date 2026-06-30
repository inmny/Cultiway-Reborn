using System;
using System.Collections.Generic;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 依据城市建筑包围盒生成<b>矩形</b>城墙边界 tile 列表，支持墙体宽度（多圈）与 4 边中点出入口。
/// 城墙使用原版 TopTileType（<see cref="TopTileLibrary.wall_order"/> / <see cref="TopTileLibrary.wall_wild"/> 等），
/// 由调用方通过 <see cref="WorldTile.setTopTileType"/> 放置。
/// </summary>
public static class WallShapeHelper
{
    private const int RADIUS_MIN = 3;
    private const int RADIUS_MAX = 60;
    private const int EXIT_HALF = 1; // 出口在每条边中点附近 ±EXIT_HALF 格（共 3 格通道）

    /// <summary>矩形包围盒：中心 (cx,cy) + 半宽 hx + 半高 hy。</summary>
    public struct Bounds
    {
        public int cx, cy, hx, hy;
    }

    /// <summary>城市所有建筑的包围盒（中心 + 半宽/半高）。半宽/半高至少 <see cref="RADIUS_MIN"/>。无建筑返回 null。</summary>
    public static Bounds? GetBuildingsBounds(City city)
    {
        if (city == null || city.buildings.Count == 0) return null;
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        int count = 0;
        foreach (var b in city.buildings)
        {
            var t = b.current_tile;
            if (t == null) continue;
            if (t.x < minX) minX = t.x;
            if (t.x > maxX) maxX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.y > maxY) maxY = t.y;
            count++;
        }
        if (count == 0) return null;
        return new Bounds
        {
            cx = (minX + maxX) / 2,
            cy = (minY + maxY) / 2,
            hx = Mathf.Clamp((maxX - minX) / 2, RADIUS_MIN, RADIUS_MAX),
            hy = Mathf.Clamp((maxY - minY) / 2, RADIUS_MIN, RADIUS_MAX),
        };
    }

    /// <summary>
    /// 生成矩形城墙 tile 列表：使用给定 <paramref name="b"/>（中心 + 半宽/半高，已含余量），<paramref name="width"/> 圈同心矩形。
    /// 4 条边中点附近留出入口（断点）；水域贴岸；深海不入水；确保至少一条陆地通道。
    /// </summary>
    public static List<WorldTile> ComputeWallRing(Bounds b, int width)
    {
        var result = new List<WorldTile>();
        int cx = b.cx, cy = b.cy;

        var placed = new HashSet<long>();
        for (int w = 0; w < width; w++)
        {
            int hx = b.hx + w, hy = b.hy + w;
            var actual = BuildActualRing(cx, cy, hx, hy, out bool hasLandExit);
            int n = actual.Count;
            if (n == 0) continue;
            // 兜底：无陆地出口（如临海城市出口全在水里）时，在最长陆地段强制开缺口
            if (!hasLandExit) ForceLandGap(actual);
            // 闭环连接相邻非空 tile；遇 null（出口 / 深海断点）则跳过，保留通道、不入水
            for (int i = 0; i < n; i++)
            {
                var cur = actual[i];
                var next = actual[(i + 1) % n];
                if (cur == null || next == null) continue;
                AddUnique(cur, result, placed);
                Connect4Land(cur.x, cur.y, next.x, next.y, result, placed);
            }
        }
        return result;
    }

    /// <summary>指定 bounds/宽度的现存城墙比例（实际边界中已是墙的比例，0~1）。用于判断是否被摧毁。</summary>
    public static float ExistingWallRatio(Bounds b, int width)
    {
        int cx = b.cx, cy = b.cy;
        int total = 0;
        int existing = 0;
        for (int w = 0; w < width; w++)
        {
            int hx = b.hx + w, hy = b.hy + w;
            foreach (var t in BuildActualRing(cx, cy, hx, hy, out _))
            {
                if (t == null) continue; // 断点不计入
                total++;
                if (IsWallTop(t)) existing++;
            }
        }
        return total == 0 ? 0f : (float)existing / total;
    }

    /// <summary>
    /// 生成单圈矩形实际边界序列（顺时针有序）：4 边中点附近为出口（null 断点）；
    /// 水域 tile 替换为最背离圆心的陆地邻居（贴岸），四周皆水时记 null（不入水）。
    /// </summary>
    /// <param name="hasLandExit">输出：是否存在落在陆地上的出口断点（可通行通道）。</param>
    private static List<WorldTile> BuildActualRing(int cx, int cy, int hx, int hy, out bool hasLandExit)
    {
        hasLandExit = false;
        var ring = new List<WorldTile>();
        var ringSeen = new HashSet<long>();
        FillRectEdge(cx, cy, hx, hy, ring, ringSeen);

        var actual = new List<WorldTile>();
        foreach (var t in ring)
        {
            if (t == null) continue;
            if (IsExit(t.x, t.y, cx, cy, hx, hy))
            {
                actual.Add(null); // 出口断点
                if (!t.IsWater()) hasLandExit = true;
                continue;
            }
            WorldTile a = t.IsWater() ? FindLandNeighbor(t, cx, cy) : t;
            actual.Add(a); // a==null（深海）同为断点
        }
        return actual;
    }

    /// <summary>4 条边中点附近为出口：上下边(|dy|==hy 且 |dx|&lt;=EXIT_HALF)、左右边(|dx|==hx 且 |dy|&lt;=EXIT_HALF)。</summary>
    private static bool IsExit(int tx, int ty, int cx, int cy, int hx, int hy)
    {
        int dx = Math.Abs(tx - cx), dy = Math.Abs(ty - cy);
        if (dy == hy && dx <= EXIT_HALF) return true; // 上/下边中点
        if (dx == hx && dy <= EXIT_HALF) return true; // 左/右边中点
        return false;
    }

    /// <summary>矩形四条边顺时针、沿边逐格（天然 4 连通）：上(→) → 右(↓) → 下(←) → 左(↑)。</summary>
    private static void FillRectEdge(int cx, int cy, int hx, int hy, List<WorldTile> list, HashSet<long> seen)
    {
        for (int x = cx - hx; x <= cx + hx; x++) AddTile(x, cy + hy, list, seen);
        for (int y = cy + hy - 1; y >= cy - hy; y--) AddTile(cx + hx, y, list, seen);
        for (int x = cx + hx - 1; x >= cx - hx; x--) AddTile(x, cy - hy, list, seen);
        for (int y = cy - hy + 1; y < cy + hy; y++) AddTile(cx - hx, y, list, seen);
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

    /// <summary>
    /// 生成 (ax,ay) -> (bx,by) 的 4 连通路径（不含起点、含终点），<b>只在陆地放置</b>（水 tile 跳过）。
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
        if (tile == null || tile.IsWater()) return; // 水/越界 → 跳过
        long key = (long)y * MapBox.width + x;
        if (!seen.Add(key)) return;
        list.Add(tile);
    }

    /// <summary>
    /// 在 actual 中<b>最长的连续非空（陆地）段</b>中部强制开 2 格缺口，
    /// 用于"出口全部落在水上"时保证至少一条陆地通道。
    /// </summary>
    private static void ForceLandGap(List<WorldTile> actual)
    {
        int n = actual.Count;
        if (n == 0) return;
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
        if (bestLen >= 4)
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
}

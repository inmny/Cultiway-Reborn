using System;
using System.Collections.Generic;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 依据城市建筑范围生成城墙边界 tile 列表，支持领土内闭合、墙体宽度（多圈）与四向出入口。
/// 城墙使用原版 TopTileType（<see cref="TopTileLibrary.wall_order"/> / <see cref="TopTileLibrary.wall_wild"/> 等），
/// 由调用方通过 <see cref="WorldTile.setTopTileType"/> 放置。
/// </summary>
public static class WallShapeHelper
{
    private const int RADIUS_MIN = 3;
    private const int RADIUS_MAX = 60;
    private const int REMOTE_UTILITY_DISTANCE = 16;
    private const int EXIT_HALF = 1; // 出口在每条边中点附近 ±EXIT_HALF 格（共 3 格通道）

    /// <summary>矩形包围盒：中心 (cx,cy) + 半宽 hx + 半高 hy。</summary>
    public struct Bounds
    {
        public int cx, cy, hx, hy;
    }

    /// <summary>城市所有建筑的包围盒（中心 + 半宽/半高）。半宽/半高至少 <see cref="RADIUS_MIN"/>。无建筑返回 null。</summary>
    public static Bounds? GetBuildingsBounds(City city, bool ignoreRemoteUtilities = false)
    {
        if (city == null || city.buildings.Count == 0) return null;
        WorldTile center = null;
        if (ignoreRemoteUtilities)
            center = city.getBuildingOfType("type_hall")?.current_tile
                     ?? city.getBuildingOfType("type_bonfire")?.current_tile
                     ?? city.getTile();
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        int count = 0;
        foreach (var b in city.buildings)
        {
            var t = b.current_tile;
            if (t == null) continue;
            if (ignoreRemoteUtilities && IsRemoteUtility(b, center)) continue;
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

    private static bool IsRemoteUtility(Building building, WorldTile center)
    {
        if (center == null || building?.asset == null || building.current_tile == null) return false;
        string type = building.asset.type;
        if (type != "type_windmill" && type != "type_mine" && type != "type_crops") return false;
        return Math.Max(Math.Abs(building.current_tile.x - center.x), Math.Abs(building.current_tile.y - center.y))
               > REMOTE_UTILITY_DISTANCE;
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

    /// <summary>
    /// 在城市领土内生成闭合城墙。墙体沿「目标矩形与城市陆地的交集」内缘布置，
    /// 遇到水域时沿岸边陆地延伸；高山和山顶参与轮廓计算，但由放置阶段跳过。
    /// </summary>
    public static List<WorldTile> ComputeWallRing(Bounds b, int width, City city, bool carvePassages = true)
    {
        if (city == null) return ComputeWallRing(b, width);

        var territoryLand = GetCityLand(city);
        var coreLand = GetCoreLand(territoryLand, city.getTile());
        int minX = Math.Max(0, b.cx - b.hx);
        int maxX = Math.Min(MapBox.width - 1, b.cx + b.hx);
        int minY = Math.Max(0, b.cy - b.hy);
        int maxY = Math.Min(MapBox.height - 1, b.cy + b.hy);
        var remaining = new HashSet<long>();
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                long key = TileKey(x, y);
                if (coreLand.Contains(key)) remaining.Add(key);
            }
        }

        var result = new List<WorldTile>();
        var exterior = FloodExterior(remaining, minX, maxX, minY, maxY);
        for (int layer = 0; layer < width && remaining.Count > 0; layer++)
        {
            var boundary = new List<WorldTile>();
            foreach (long key in remaining)
            {
                int x = (int)(key % MapBox.width);
                int y = (int)(key / MapBox.width);
                if (IsOuterBoundaryTile(x, y, exterior, minX, maxX, minY, maxY))
                    boundary.Add(World.world.GetTileSimple(x, y));
            }
            var sealedBoundary = SealDiagonalGaps(boundary, coreLand, minX, maxX, minY, maxY, b);
            foreach (var tile in boundary)
            {
                long key = TileKey(tile.x, tile.y);
                remaining.Remove(key);
                exterior.Add(key);
            }
            foreach (var tile in sealedBoundary)
            {
                long key = TileKey(tile.x, tile.y);
                remaining.Remove(key);
                exterior.Add(key);
                result.Add(tile);
            }
        }

        if (carvePassages)
        {
            CarveLandGates(result, b, city);
            CarveDockPassages(result, city, width);
        }
        return result;
    }

    /// <summary>补齐仅对角相接的墙段，避免单位从两个墙角之间斜向穿过。</summary>
    private static List<WorldTile> SealDiagonalGaps(List<WorldTile> ring, HashSet<long> coreLand,
                                                    int minX, int maxX, int minY, int maxY, Bounds b)
    {
        var result = new List<WorldTile>(ring);
        var walls = new HashSet<long>();
        foreach (var tile in ring) walls.Add(TileKey(tile.x, tile.y));

        var additions = new List<WorldTile>();
        foreach (var tile in ring)
        {
            TrySealDiagonal(tile, 1, 1, walls, coreLand, additions, minX, maxX, minY, maxY, b);
            TrySealDiagonal(tile, 1, -1, walls, coreLand, additions, minX, maxX, minY, maxY, b);
        }
        foreach (var tile in additions)
        {
            if (walls.Add(TileKey(tile.x, tile.y))) result.Add(tile);
        }
        return result;
    }

    private static void TrySealDiagonal(WorldTile tile, int dx, int dy, HashSet<long> walls,
                                        HashSet<long> coreLand, List<WorldTile> additions,
                                        int minX, int maxX, int minY, int maxY, Bounds b)
    {
        int diagonalX = tile.x + dx;
        int diagonalY = tile.y + dy;
        if (!walls.Contains(TileKey(diagonalX, diagonalY))) return;

        var horizontal = GetLandTile(tile.x + dx, tile.y, coreLand, minX, maxX, minY, maxY);
        var vertical = GetLandTile(tile.x, tile.y + dy, coreLand, minX, maxX, minY, maxY);
        bool horizontalWall = horizontal != null && walls.Contains(TileKey(horizontal.x, horizontal.y));
        bool verticalWall = vertical != null && walls.Contains(TileKey(vertical.x, vertical.y));
        if (horizontalWall || verticalWall) return;

        WorldTile bridge;
        if (horizontal == null) bridge = vertical;
        else if (vertical == null) bridge = horizontal;
        else
        {
            int horizontalDistance = Math.Abs(horizontal.x - b.cx) + Math.Abs(horizontal.y - b.cy);
            int verticalDistance = Math.Abs(vertical.x - b.cx) + Math.Abs(vertical.y - b.cy);
            bridge = horizontalDistance >= verticalDistance ? horizontal : vertical;
        }
        if (bridge != null && !IsBlockingTerrain(bridge)) additions.Add(bridge);
    }

    private static WorldTile GetLandTile(int x, int y, HashSet<long> coreLand,
                                         int minX, int maxX, int minY, int maxY)
    {
        if (x < minX || x > maxX || y < minY || y > maxY || !coreLand.Contains(TileKey(x, y))) return null;
        return World.world.GetTileSimple(x, y);
    }

    /// <summary>指定 bounds/宽度的现存城墙比例（实际边界中已是墙的比例，0~1）。用于判断是否被摧毁。
    /// 传入 <paramref name="city"/> 时使用当前城市领土内的闭合边界。</summary>
    public static float ExistingWallRatio(Bounds b, int width, City city = null)
    {
        if (city != null)
        {
            var ring = ComputeWallRing(b, width, city);
            if (ring.Count == 0) return 0f;
            int existingWalls = 0;
            foreach (var tile in ring)
            {
                if (IsWallTop(tile)) existingWalls++;
            }
            return (float)existingWalls / ring.Count;
        }

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

    private static HashSet<long> GetCoreLand(HashSet<long> territoryLand, WorldTile cityCenter)
    {
        var result = new HashSet<long>();
        if (territoryLand.Count == 0) return result;

        long seed = 0;
        bool hasSeed = cityCenter != null && territoryLand.Contains(TileKey(cityCenter.x, cityCenter.y));
        if (hasSeed)
        {
            seed = TileKey(cityCenter.x, cityCenter.y);
        }
        else
        {
            int bestDistance = int.MaxValue;
            foreach (long key in territoryLand)
            {
                if (cityCenter == null)
                {
                    seed = key;
                    hasSeed = true;
                    break;
                }
                int x = (int)(key % MapBox.width);
                int y = (int)(key / MapBox.width);
                int dx = x - cityCenter.x;
                int dy = y - cityCenter.y;
                int distance = dx * dx + dy * dy;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                seed = key;
                hasSeed = true;
            }
        }
        if (!hasSeed) return result;

        var queue = new Queue<long>();
        queue.Enqueue(seed);
        result.Add(seed);
        while (queue.Count > 0)
        {
            long key = queue.Dequeue();
            int x = (int)(key % MapBox.width);
            int y = (int)(key / MapBox.width);
            AddConnectedLand(x - 1, y, territoryLand, result, queue);
            AddConnectedLand(x + 1, y, territoryLand, result, queue);
            AddConnectedLand(x, y - 1, territoryLand, result, queue);
            AddConnectedLand(x, y + 1, territoryLand, result, queue);
        }
        return result;
    }

    private static HashSet<long> GetCityLand(City city)
    {
        var result = new HashSet<long>();
        foreach (var zone in city.zones)
        {
            if (zone == null || zone.city != city) continue;
            foreach (var tile in zone.tiles)
            {
                if (tile != null && !tile.IsWater()) result.Add(TileKey(tile.x, tile.y));
            }
        }
        return result;
    }

    private static void AddConnectedLand(int x, int y, HashSet<long> territoryLand,
                                         HashSet<long> connected, Queue<long> queue)
    {
        if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) return;
        long key = TileKey(x, y);
        if (!territoryLand.Contains(key) || !connected.Add(key)) return;
        queue.Enqueue(key);
    }

    private static HashSet<long> FloodExterior(HashSet<long> land, int minX, int maxX, int minY, int maxY)
    {
        var exterior = new HashSet<long>();
        var queue = new Queue<long>();
        for (int x = minX; x <= maxX; x++)
        {
            AddExterior(x, minY, land, exterior, queue, minX, maxX, minY, maxY);
            AddExterior(x, maxY, land, exterior, queue, minX, maxX, minY, maxY);
        }
        for (int y = minY + 1; y < maxY; y++)
        {
            AddExterior(minX, y, land, exterior, queue, minX, maxX, minY, maxY);
            AddExterior(maxX, y, land, exterior, queue, minX, maxX, minY, maxY);
        }

        while (queue.Count > 0)
        {
            long key = queue.Dequeue();
            int x = (int)(key % MapBox.width);
            int y = (int)(key / MapBox.width);
            AddExterior(x - 1, y, land, exterior, queue, minX, maxX, minY, maxY);
            AddExterior(x + 1, y, land, exterior, queue, minX, maxX, minY, maxY);
            AddExterior(x, y - 1, land, exterior, queue, minX, maxX, minY, maxY);
            AddExterior(x, y + 1, land, exterior, queue, minX, maxX, minY, maxY);
        }
        return exterior;
    }

    private static void AddExterior(int x, int y, HashSet<long> land, HashSet<long> exterior, Queue<long> queue,
                                    int minX, int maxX, int minY, int maxY)
    {
        if (x < minX || x > maxX || y < minY || y > maxY) return;
        long key = TileKey(x, y);
        if (land.Contains(key) || !exterior.Add(key)) return;
        queue.Enqueue(key);
    }

    private static bool IsOuterBoundaryTile(int x, int y, HashSet<long> exterior,
                                            int minX, int maxX, int minY, int maxY)
    {
        return x == minX || x == maxX || y == minY || y == maxY
               || exterior.Contains(TileKey(x - 1, y))
               || exterior.Contains(TileKey(x + 1, y))
               || exterior.Contains(TileKey(x, y - 1))
               || exterior.Contains(TileKey(x, y + 1));
    }

    private static long TileKey(int x, int y)
    {
        return (long)y * MapBox.width + x;
    }

    private static void CarveLandGates(List<WorldTile> ring, Bounds b, City city)
    {
        if (ring.Count == 0) return;
        int radius = EXIT_HALF;
        var removed = new HashSet<long>();
        bool north = CarveLandGate(ring, b, radius, 0, 1, city, removed);
        bool east = CarveLandGate(ring, b, radius, 1, 0, city, removed);
        bool south = CarveLandGate(ring, b, radius, 0, -1, city, removed);
        bool west = CarveLandGate(ring, b, radius, -1, 0, city, removed);
        if (!north && !east) CarveLandGate(ring, b, radius, 1, 1, city, removed);
        if (!east && !south) CarveLandGate(ring, b, radius, 1, -1, city, removed);
        if (!south && !west) CarveLandGate(ring, b, radius, -1, -1, city, removed);
        if (!west && !north) CarveLandGate(ring, b, radius, -1, 1, city, removed);
        ring.RemoveAll(tile => removed.Contains(TileKey(tile.x, tile.y)));
    }

    private static bool CarveLandGate(List<WorldTile> ring, Bounds b, int radius,
                                      int directionX, int directionY, City city, HashSet<long> removed)
    {
        if (CarveRoadGate(ring, b, radius, directionX, directionY, city, removed)) return true;

        WorldTile gate = null;
        int bestLateral = int.MaxValue;
        int bestProjection = int.MinValue;
        foreach (var tile in ring)
        {
            if (!IsInDirection(tile, b, directionX, directionY, out int lateral, out int projection)
                || IsBlockingTerrain(tile)) continue;
            if (!HasPassableOutside(tile, directionX, directionY)) continue;
            if (lateral > bestLateral || lateral == bestLateral && projection <= bestProjection) continue;
            bestLateral = lateral;
            bestProjection = projection;
            gate = tile;
        }
        if (gate == null) return false;
        MarkPassage(ring, gate, radius, removed);
        return true;
    }

    private static bool CarveRoadGate(List<WorldTile> ring, Bounds b, int radius,
                                      int directionX, int directionY, City city, HashSet<long> removed)
    {
        WorldTile gate = null;
        int bestLateral = int.MaxValue;
        int bestProjection = int.MinValue;
        foreach (var tile in ring)
        {
            if (!IsInDirection(tile, b, directionX, directionY, out int lateral, out int projection)
                || IsBlockingTerrain(tile) || !HasCityRoadNearby(tile, city, 6)) continue;
            if (!HasPassableOutside(tile, directionX, directionY)) continue;
            if (lateral > bestLateral || lateral == bestLateral && projection <= bestProjection) continue;
            bestLateral = lateral;
            bestProjection = projection;
            gate = tile;
        }
        if (gate == null) return false;

        MarkPassage(ring, gate, radius, removed);
        return true;
    }

    private static bool IsInDirection(WorldTile tile, Bounds b, int directionX, int directionY,
                                      out int lateral, out int projection)
    {
        int dx = tile.x - b.cx;
        int dy = tile.y - b.cy;
        projection = dx * directionX + dy * directionY;
        lateral = Math.Abs(dx * directionY - dy * directionX);
        return projection > 0 && lateral <= projection;
    }

    private static bool HasCityRoadNearby(WorldTile center, City city, int radius)
    {
        int radiusSquared = radius * radius;
        for (int y = center.y - radius; y <= center.y + radius; y++)
        {
            for (int x = center.x - radius; x <= center.x + radius; x++)
            {
                if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) continue;
                int dx = x - center.x;
                int dy = y - center.y;
                if (dx * dx + dy * dy > radiusSquared) continue;
                var tile = World.world.GetTileSimple(x, y);
                if (tile?.zone?.city == city && tile.Type?.road == true) return true;
            }
        }
        return false;
    }

    private static WorldTile GetNeighbour(WorldTile tile, int directionX, int directionY)
    {
        int x = tile.x + directionX;
        int y = tile.y + directionY;
        if (x < 0 || y < 0 || x >= MapBox.width || y >= MapBox.height) return null;
        return World.world.GetTileSimple(x, y);
    }

    private static bool HasPassableOutside(WorldTile tile, int directionX, int directionY)
    {
        if (directionX != 0 && directionY != 0)
            return IsPassableLand(GetNeighbour(tile, directionX, 0))
                   || IsPassableLand(GetNeighbour(tile, 0, directionY));
        return IsPassableLand(GetNeighbour(tile, directionX, directionY));
    }

    private static bool IsPassableLand(WorldTile tile)
    {
        return tile != null && !tile.IsWater() && !IsBlockingTerrain(tile);
    }

    private static void MarkPassage(List<WorldTile> ring, WorldTile passage, int radius, HashSet<long> removed)
    {
        foreach (var tile in ring)
        {
            if (Math.Abs(tile.x - passage.x) <= radius && Math.Abs(tile.y - passage.y) <= radius)
                removed.Add(TileKey(tile.x, tile.y));
        }
    }

    /// <summary>港口实际占地靠近墙线时，在最近的岸上墙段预留贯穿全部墙层的通道。</summary>
    private static bool CarveDockPassages(List<WorldTile> ring, City city, int width)
    {
        if (ring.Count == 0 || city == null) return false;

        int radius = EXIT_HALF;
        int maxDistance = 8;
        int maxDistanceSquared = maxDistance * maxDistance;
        var removed = new HashSet<long>();
        foreach (var building in city.buildings)
        {
            if (building?.asset == null || !building.asset.docks) continue;

            WorldTile passage = null;
            int bestDistance = int.MaxValue;
            foreach (var wallTile in ring)
            {
                foreach (var buildingTile in building.tiles)
                {
                    if (buildingTile == null) continue;
                    int dx = wallTile.x - buildingTile.x;
                    int dy = wallTile.y - buildingTile.y;
                    int distance = dx * dx + dy * dy;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    passage = wallTile;
                }
            }
            if (passage == null || bestDistance > maxDistanceSquared) continue;

            MarkPassage(ring, passage, radius, removed);
        }
        ring.RemoveAll(tile => removed.Contains(TileKey(tile.x, tile.y)));
        return removed.Count > 0;
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
                if (!t.IsWater() && !IsBlockingTerrain(t)) hasLandExit = true; // 出口落在可通行陆地
                continue;
            }
            if (t.IsWater()) { actual.Add(FindLandNeighbor(t, cx, cy)); continue; } // 水域贴岸
            if (IsBlockingTerrain(t)) { actual.Add(null); continue; }               // 山峰/雪顶等不可通行 → 断点
            actual.Add(t);
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

    /// <summary>地块是否限制单位通行（山峰/雪顶等高山地形）——城墙在此跳过，以高山为天然屏障。</summary>
    private static bool IsBlockingTerrain(WorldTile t)
    {
        var type = t.Type;
        return type != null && (type.mountains || type.summit);
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

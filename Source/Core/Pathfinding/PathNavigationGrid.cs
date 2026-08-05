using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Core.Performance;

namespace Cultiway.Core.Pathfinding;

[Flags]
internal enum PathTileFlags : ushort
{
    None = 0,
    Exists = 1 << 0,
    HasType = 1 << 1,
    Block = 1 << 2,
    Lava = 1 << 3,
    Ocean = 1 << 4,
    Liquid = 1 << 5,
    DamageUnits = 1 << 6,
    Fire = 1 << 7
}

/// <summary>
/// 工作线程使用的单格导航数据，不持有 Unity 或 WorldBox 运行时对象。
/// </summary>
internal readonly struct PathTileSnapshot
{
    internal PathTileSnapshot(PathTileFlags flags, float damage, float walkMultiplier, int regionId)
    {
        Flags = flags;
        Damage = damage;
        WalkMultiplier = walkMultiplier;
        RegionId = regionId;
    }

    internal PathTileFlags Flags { get; }
    internal float Damage { get; }
    internal float WalkMultiplier { get; }
    internal int RegionId { get; }
    internal bool Exists => (Flags & PathTileFlags.Exists) != 0;
    internal bool HasType => (Flags & PathTileFlags.HasType) != 0;
    internal bool Block => (Flags & PathTileFlags.Block) != 0;
    internal bool Lava => (Flags & PathTileFlags.Lava) != 0;
    internal bool Ocean => (Flags & PathTileFlags.Ocean) != 0;
    internal bool Liquid => (Flags & PathTileFlags.Liquid) != 0;
    internal bool DamageUnits => (Flags & PathTileFlags.DamageUnits) != 0;
    internal bool IsOnFire => (Flags & PathTileFlags.Fire) != 0;

    /// <summary>在模拟线程上提取一个地块的寻路语义。</summary>
    internal static PathTileSnapshot Capture(WorldTile tile)
    {
        if (tile?.data == null)
        {
            return default;
        }

        PathTileFlags flags = PathTileFlags.Exists;
        TileTypeBase type = tile.Type;
        if (type != null)
        {
            flags |= PathTileFlags.HasType;
            if (type.block) flags |= PathTileFlags.Block;
            if (type.lava) flags |= PathTileFlags.Lava;
            if (type.ocean) flags |= PathTileFlags.Ocean;
            if (type.liquid) flags |= PathTileFlags.Liquid;
            if (type.damage_units) flags |= PathTileFlags.DamageUnits;
        }

        try
        {
            if (tile.isOnFire()) flags |= PathTileFlags.Fire;
        }
        catch
        {
            // 世界生成和清理边界上火焰数组可能尚未就绪，此时按无火处理。
        }

        return new PathTileSnapshot(
            flags,
            type?.damage ?? 0f,
            type?.walk_multiplier ?? 1f,
            tile.region?.id ?? -1);
    }
}

internal sealed class PathRegionSnapshot
{
    internal PathRegionSnapshot(int id, int centerTileId, int[] neighbours)
    {
        Id = id;
        CenterTileId = centerTileId;
        Neighbours = neighbours ?? Array.Empty<int>();
    }

    internal int Id { get; }
    internal int CenterTileId { get; }
    internal int[] Neighbours { get; }
}

/// <summary>
/// 区域级路径拓扑。原版重算区域后整体替换，工作线程持有的旧版本始终只读。
/// </summary>
internal sealed class PathRegionTopology
{
    private readonly Dictionary<int, PathRegionSnapshot> regions;

    private PathRegionTopology(int generation, int revision, Dictionary<int, PathRegionSnapshot> regions)
    {
        Generation = generation;
        Revision = revision;
        this.regions = regions ?? new Dictionary<int, PathRegionSnapshot>();
    }

    internal int Generation { get; }
    internal int Revision { get; }

    internal bool TryGetRegion(int regionId, out PathRegionSnapshot region)
    {
        return regions.TryGetValue(regionId, out region);
    }

    internal static PathRegionTopology Capture(MapBox world, WorldTile[] tiles, int generation, int revision)
    {
        MapChunk[] chunks = world?.map_chunk_manager?.chunks;
        if (chunks != null && chunks.Length > 0)
        {
            var chunkRegions = new Dictionary<int, PathRegionSnapshot>();
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                MapChunk chunk = chunks[chunkIndex];
                if (chunk?.regions == null) continue;
                for (int regionIndex = 0; regionIndex < chunk.regions.Count; regionIndex++)
                {
                    MapRegion region = chunk.regions[regionIndex];
                    if (region == null || chunkRegions.ContainsKey(region.id)) continue;
                    chunkRegions.Add(region.id, CaptureRegion(region));
                }
            }

            if (chunkRegions.Count > 0)
            {
                return new PathRegionTopology(generation, revision, chunkRegions);
            }
        }

        var liveRegions = new Dictionary<int, MapRegion>();
        if (tiles != null)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                MapRegion region = tiles[i]?.region;
                if (region != null && !liveRegions.ContainsKey(region.id))
                {
                    liveRegions.Add(region.id, region);
                }
            }
        }

        var result = new Dictionary<int, PathRegionSnapshot>(liveRegions.Count);
        foreach (KeyValuePair<int, MapRegion> pair in liveRegions)
        {
            result.Add(pair.Key, CaptureRegion(pair.Value));
        }

        return new PathRegionTopology(generation, revision, result);
    }

    private static PathRegionSnapshot CaptureRegion(MapRegion region)
    {
        var neighbours = new List<int>(region.neighbours?.Count ?? 0);
        if (region.neighbours != null)
        {
            for (int i = 0; i < region.neighbours.Count; i++)
            {
                MapRegion neighbour = region.neighbours[i];
                if (neighbour != null) neighbours.Add(neighbour.id);
            }
        }

        return new PathRegionSnapshot(region.id, ResolveRegionCenterTile(region), neighbours.ToArray());
    }

    private static int ResolveRegionCenterTile(MapRegion region)
    {
        if (region?.tiles == null || region.tiles.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < region.tiles.Count; i++)
        {
            WorldTile tile = region.tiles[i];
            if (tile?.data != null) return tile.data.tile_id;
        }

        return -1;
    }
}

/// <summary>
/// 世界级紧凑导航缓存。地块字段由模拟线程原子发布，寻路线程只读取标量数组。
/// </summary>
internal sealed class PathNavigationGrid
{
    private static int nextIdentity;
    private readonly int[] flags;
    private readonly float[] damage;
    private readonly float[] walkMultipliers;
    private readonly int[] regionIds;
    private PathRegionTopology topology;
    private int topologyRevision;

    private PathNavigationGrid(int generation, int width, int height, int tileCount)
    {
        Identity = Interlocked.Increment(ref nextIdentity);
        Generation = generation;
        Width = width;
        Height = height;
        TileCount = tileCount;
        flags = new int[tileCount];
        damage = new float[tileCount];
        walkMultipliers = new float[tileCount];
        regionIds = new int[tileCount];
    }

    internal int Identity { get; }
    internal int Generation { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal int TileCount { get; }
    internal PathRegionTopology Topology => Volatile.Read(ref topology);

    internal bool MatchesCurrentWorld(WorldTile[] tiles)
    {
        return tiles != null && tiles.Length == TileCount && Width == MapBox.width && Height == MapBox.height &&
               Generation == SimulationTime.Generation;
    }

    internal bool TryGetTile(int tileId, out PathTileSnapshot tile)
    {
        if ((uint)tileId >= (uint)TileCount)
        {
            tile = default;
            return false;
        }

        PathTileFlags currentFlags = (PathTileFlags)Volatile.Read(ref flags[tileId]);
        if ((currentFlags & PathTileFlags.Exists) == 0)
        {
            tile = default;
            return false;
        }

        tile = new PathTileSnapshot(
            currentFlags,
            Volatile.Read(ref damage[tileId]),
            Volatile.Read(ref walkMultipliers[tileId]),
            Volatile.Read(ref regionIds[tileId]));
        return true;
    }

    internal bool TryGetTileAt(int x, int y, out int tileId, out PathTileSnapshot tile)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            tileId = -1;
            tile = default;
            return false;
        }

        tileId = x + y * Width;
        return TryGetTile(tileId, out tile);
    }

    internal int XOf(int tileId)
    {
        return tileId % Width;
    }

    internal int YOf(int tileId)
    {
        return tileId / Width;
    }

    internal int ManhattanDistance(int firstTileId, int secondTileId)
    {
        return Math.Abs(XOf(firstTileId) - XOf(secondTileId)) +
               Math.Abs(YOf(firstTileId) - YOf(secondTileId));
    }

    internal static PathNavigationGrid Capture(MapBox world, int generation)
    {
        WorldTile[] tiles = world?.tiles_list;
        int width = MapBox.width;
        int height = MapBox.height;
        if (tiles == null || tiles.Length == 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        var grid = new PathNavigationGrid(generation, width, height, tiles.Length);
        for (int i = 0; i < tiles.Length; i++)
        {
            grid.WriteTile(i, PathTileSnapshot.Capture(tiles[i]));
        }

        grid.topologyRevision = 1;
        grid.topology = PathRegionTopology.Capture(world, tiles, generation, grid.topologyRevision);
        return grid;
    }

    internal void UpdateTiles(WorldTile[] worldTiles, IReadOnlyList<int> dirtyTileIds)
    {
        if (worldTiles == null || worldTiles.Length != TileCount || dirtyTileIds == null || dirtyTileIds.Count == 0)
        {
            return;
        }

        for (int i = 0; i < dirtyTileIds.Count; i++)
        {
            int tileId = dirtyTileIds[i];
            if ((uint)tileId >= (uint)TileCount) continue;
            WriteTile(tileId, PathTileSnapshot.Capture(worldTiles[tileId]));
        }

    }

    internal void ReplaceTopology(MapBox world)
    {
        int revision = Interlocked.Increment(ref topologyRevision);
        PathRegionTopology next = PathRegionTopology.Capture(world, world?.tiles_list, Generation, revision);
        Volatile.Write(ref topology, next);
    }

    private void WriteTile(int tileId, PathTileSnapshot tile)
    {
        // flags 最后发布；读到新 flags 的线程也能看到此前写入的成本和区域数据。
        Volatile.Write(ref damage[tileId], tile.Damage);
        Volatile.Write(ref walkMultipliers[tileId], tile.WalkMultiplier);
        Volatile.Write(ref regionIds[tileId], tile.RegionId);
        Volatile.Write(ref flags[tileId], (int)tile.Flags);
    }
}

/// <summary>
/// 在模拟线程维护导航缓存；工作线程不会接触实时 WorldTile、MapRegion 或火焰数组。
/// </summary>
internal static class PathNavigationGridService
{
    private static readonly object DirtySync = new();
    private static readonly HashSet<int> DirtyTiles = new();
    private static PathNavigationGrid current;
    private static bool topologyDirty;

    internal static PathNavigationGrid Current => Volatile.Read(ref current);

    internal static void BuildForCurrentWorld()
    {
        MapBox world = World.world;
        if (world?.tiles_list == null || world.tiles_list.Length == 0)
        {
            Clear();
            return;
        }

        PathNavigationGrid grid = PathNavigationGrid.Capture(world, SimulationTime.Generation);
        Volatile.Write(ref current, grid);
        lock (DirtySync)
        {
            DirtyTiles.Clear();
            topologyDirty = false;
        }
    }

    internal static void MarkDirty(WorldTile tile)
    {
        int tileId = tile?.data?.tile_id ?? -1;
        if (tileId < 0) return;
        lock (DirtySync)
        {
            DirtyTiles.Add(tileId);
        }
    }

    internal static void MarkTopologyDirty(IEnumerable<MapChunk> chunks)
    {
        if (chunks == null) return;
        lock (DirtySync)
        {
            foreach (MapChunk chunk in chunks)
            {
                if (chunk?.tiles == null) continue;
                for (int i = 0; i < chunk.tiles.Length; i++)
                {
                    int tileId = chunk.tiles[i]?.data?.tile_id ?? -1;
                    if (tileId >= 0) DirtyTiles.Add(tileId);
                }
            }

            topologyDirty = true;
        }
    }

    internal static void FlushDirty()
    {
        PathNavigationGrid grid = Current;
        MapBox world = World.world;
        WorldTile[] worldTiles = world?.tiles_list;
        if (grid == null || !grid.MatchesCurrentWorld(worldTiles))
        {
            BuildForCurrentWorld();
            return;
        }

        List<int> dirty;
        bool refreshTopology;
        lock (DirtySync)
        {
            if (DirtyTiles.Count == 0 && !topologyDirty) return;
            dirty = new List<int>(DirtyTiles);
            DirtyTiles.Clear();
            refreshTopology = topologyDirty;
            topologyDirty = false;
        }

        grid.UpdateTiles(worldTiles, dirty);
        if (refreshTopology)
        {
            grid.ReplaceTopology(world);
        }
    }

    internal static void Clear()
    {
        Volatile.Write(ref current, null);
        lock (DirtySync)
        {
            DirtyTiles.Clear();
            topologyDirty = false;
        }
    }
}

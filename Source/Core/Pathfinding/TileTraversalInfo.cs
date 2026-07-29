using System;
using System.Globalization;
using System.Threading;

namespace Cultiway.Core.Pathfinding;

internal readonly struct TileTraversalInfo
{
    private static readonly object CacheGate = new();
    private static CacheState cache;
    private static long cacheWorlds;
    private static long cacheEntries;
    private static long cacheRefreshes;
    private static long cacheReadRetries;

    private TileTraversalInfo(int tileId, int x, int y, bool hasType, bool ground, bool block, bool lava, bool ocean,
        bool liquid, bool damageUnits, float damage, float walkMultiplier, string typeId, bool isOnFire)
    {
        TileId = tileId;
        X = x;
        Y = y;
        HasType = hasType;
        Ground = ground;
        Block = block;
        Lava = lava;
        Ocean = ocean;
        Liquid = liquid;
        DamageUnits = damageUnits;
        Damage = damage;
        WalkMultiplier = walkMultiplier;
        TypeId = typeId ?? "null";
        IsOnFire = isOnFire;
        Exists = true;
    }

    public bool Exists { get; }
    public int TileId { get; }
    public int X { get; }
    public int Y { get; }
    public bool HasType { get; }
    public bool Ground { get; }
    public bool Block { get; }
    public bool Lava { get; }
    public bool Ocean { get; }
    public bool Liquid { get; }
    public bool DamageUnits { get; }
    public float Damage { get; }
    public float WalkMultiplier { get; }
    public string TypeId { get; }
    public bool IsOnFire { get; }

    public static int TileIdOf(WorldTile tile)
    {
        return tile?.data?.tile_id ?? -1;
    }

    public static bool TryGet(int tileId, out TileTraversalInfo info)
    {
        CacheState state = GetCacheState();
        if (state == null ||
            tileId < 0 ||
            tileId >= state.Tiles.Length)
        {
            info = default;
            return false;
        }

        StaticTileInfo cached = ReadStaticInfo(
            state,
            tileId);
        if (!cached.Exists)
        {
            info = default;
            return false;
        }

        bool isOnFire =
            tileId < state.Fires.Length &&
            state.Fires[tileId];
        info = cached.ToTraversalInfo(isOnFire);
        return true;
    }

    public static bool TryGetAt(int x, int y, out TileTraversalInfo info)
    {
        var width = MapBox.width;
        var height = MapBox.height;
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            info = default;
            return false;
        }

        return TryGet(x + y * width, out info);
    }

    public static WorldTile ResolveTile(int tileId)
    {
        var tiles = World.world?.tiles_list;
        if (tiles == null || tileId < 0 || tileId >= tiles.Length)
        {
            return null;
        }

        return tiles[tileId];
    }

    public static bool TryCreate(WorldTile tile, out TileTraversalInfo info)
    {
        if (!TryCreateStatic(
                tile,
                out StaticTileInfo cached))
        {
            info = default;
            return false;
        }

        info = cached.ToTraversalInfo(
            SafeIsOnFire(tile));
        return info.Exists;
    }

    internal static void Refresh(WorldTile tile)
    {
        int tileId = TileIdOf(tile);
        CacheState state =
            Volatile.Read(ref cache);
        if (state == null ||
            tileId < 0 ||
            tileId >= state.Tiles.Length ||
            !ReferenceEquals(
                state.Tiles[tileId],
                tile))
        {
            return;
        }

        int writingVersion =
            BeginWrite(
                state.Versions,
                tileId);
        state.Infos[tileId] =
            CreateStatic(tile);
        Volatile.Write(
            ref state.Versions[tileId],
            writingVersion + 1);
        Interlocked.Increment(
            ref cacheRefreshes);
    }

    internal static void ClearCache()
    {
        lock (CacheGate)
        {
            Volatile.Write(
                ref cache,
                null);
        }
    }

    internal static string GetCacheDiagnostics()
    {
        CacheState state =
            Volatile.Read(ref cache);
        return string.Format(
            CultureInfo.InvariantCulture,
            "tiles={0} worlds={1} entries={2} refreshes={3} retries={4}",
            state?.Tiles.Length ?? 0,
            Interlocked.Read(ref cacheWorlds),
            Interlocked.Read(ref cacheEntries),
            Interlocked.Read(ref cacheRefreshes),
            Interlocked.Read(ref cacheReadRetries));
    }

    private static CacheState GetCacheState()
    {
        MapBox world = World.world;
        WorldTile[] tiles =
            world?.tiles_list;
        bool[] fires =
            world?.tile_manager?.fires;
        if (tiles == null ||
            fires == null)
        {
            return null;
        }

        CacheState current =
            Volatile.Read(ref cache);
        if (current != null &&
            ReferenceEquals(
                current.Tiles,
                tiles) &&
            ReferenceEquals(
                current.Fires,
                fires))
        {
            return current;
        }

        lock (CacheGate)
        {
            current =
                Volatile.Read(ref cache);
            if (current != null &&
                ReferenceEquals(
                    current.Tiles,
                    tiles) &&
                ReferenceEquals(
                    current.Fires,
                    fires))
            {
                return current;
            }

            current =
                new CacheState(
                    tiles,
                    fires);
            Volatile.Write(
                ref cache,
                current);
            Interlocked.Increment(
                ref cacheWorlds);
            return current;
        }
    }

    private static StaticTileInfo ReadStaticInfo(
        CacheState state,
        int tileId)
    {
        SpinWait spin = default;
        while (true)
        {
            int version =
                Volatile.Read(
                    ref state.Versions[tileId]);
            if (version == 0)
            {
                if (Interlocked.CompareExchange(
                        ref state.Versions[tileId],
                        1,
                        0) == 0)
                {
                    StaticTileInfo created =
                        CreateStatic(
                            state.Tiles[tileId]);
                    state.Infos[tileId] =
                        created;
                    Volatile.Write(
                        ref state.Versions[tileId],
                        2);
                    Interlocked.Increment(
                        ref cacheEntries);
                    return created;
                }

                continue;
            }

            if ((version & 1) != 0)
            {
                spin.SpinOnce();
                continue;
            }

            StaticTileInfo info =
                state.Infos[tileId];
            if (version ==
                Volatile.Read(
                    ref state.Versions[tileId]))
            {
                return info;
            }

            Interlocked.Increment(
                ref cacheReadRetries);
        }
    }

    private static int BeginWrite(
        int[] versions,
        int tileId)
    {
        SpinWait spin = default;
        while (true)
        {
            int version =
                Volatile.Read(
                    ref versions[tileId]);
            if ((version & 1) != 0)
            {
                spin.SpinOnce();
                continue;
            }

            int writingVersion =
                version == 0 ||
                version >= int.MaxValue - 1
                    ? 1
                    : version + 1;
            if (Interlocked.CompareExchange(
                    ref versions[tileId],
                    writingVersion,
                    version) == version)
            {
                return writingVersion;
            }
        }
    }

    private static bool TryCreateStatic(
        WorldTile tile,
        out StaticTileInfo info)
    {
        info = CreateStatic(tile);
        return info.Exists;
    }

    private static StaticTileInfo CreateStatic(
        WorldTile tile)
    {
        int tileId = TileIdOf(tile);
        if (tile == null ||
            tileId < 0)
        {
            return default;
        }

        TileTypeBase type =
            tile.Type;
        return new StaticTileInfo(
            tileId,
            tile.x,
            tile.y,
            type != null,
            type?.ground ?? false,
            type?.block ?? false,
            type?.lava ?? false,
            type?.ocean ?? false,
            type?.liquid ?? false,
            type?.damage_units ?? false,
            type?.damage ?? 0f,
            type?.walk_multiplier ?? 1f,
            type?.id ?? "null");
    }

    private static bool SafeIsOnFire(WorldTile tile)
    {
        try
        {
            return tile != null && tile.isOnFire();
        }
        catch
        {
            return false;
        }
    }

    private sealed class CacheState
    {
        internal CacheState(
            WorldTile[] tiles,
            bool[] fires)
        {
            Tiles = tiles;
            Fires = fires;
            Infos =
                new StaticTileInfo[tiles.Length];
            Versions =
                new int[tiles.Length];
        }

        internal WorldTile[] Tiles { get; }
        internal bool[] Fires { get; }
        internal StaticTileInfo[] Infos { get; }
        internal int[] Versions { get; }
    }

    private readonly struct StaticTileInfo
    {
        internal StaticTileInfo(
            int tileId,
            int x,
            int y,
            bool hasType,
            bool ground,
            bool block,
            bool lava,
            bool ocean,
            bool liquid,
            bool damageUnits,
            float damage,
            float walkMultiplier,
            string typeId)
        {
            TileId = tileId;
            X = x;
            Y = y;
            HasType = hasType;
            Ground = ground;
            Block = block;
            Lava = lava;
            Ocean = ocean;
            Liquid = liquid;
            DamageUnits = damageUnits;
            Damage = damage;
            WalkMultiplier = walkMultiplier;
            TypeId = typeId;
            Exists = true;
        }

        internal bool Exists { get; }
        private int TileId { get; }
        private int X { get; }
        private int Y { get; }
        private bool HasType { get; }
        private bool Ground { get; }
        private bool Block { get; }
        private bool Lava { get; }
        private bool Ocean { get; }
        private bool Liquid { get; }
        private bool DamageUnits { get; }
        private float Damage { get; }
        private float WalkMultiplier { get; }
        private string TypeId { get; }

        internal TileTraversalInfo ToTraversalInfo(
            bool isOnFire)
        {
            return new TileTraversalInfo(
                TileId,
                X,
                Y,
                HasType,
                Ground,
                Block,
                Lava,
                Ocean,
                Liquid,
                DamageUnits,
                Damage,
                WalkMultiplier,
                TypeId,
                isOnFire);
        }
    }
}

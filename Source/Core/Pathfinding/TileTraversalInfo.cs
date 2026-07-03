using System;

namespace Cultiway.Core.Pathfinding;

internal readonly struct TileTraversalInfo
{
    private TileTraversalInfo(int tileId, int x, int y, bool hasType, bool block, bool lava, bool ocean,
        bool liquid, bool damageUnits, float damage, float walkMultiplier, string typeId, bool isOnFire)
    {
        TileId = tileId;
        X = x;
        Y = y;
        HasType = hasType;
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
        var tile = ResolveTile(tileId);
        return TryCreate(tile, out info);
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
        var tileId = TileIdOf(tile);
        if (tile == null || tileId < 0)
        {
            info = default;
            return false;
        }

        var type = tile.Type;
        info = new TileTraversalInfo(
            tileId,
            tile.x,
            tile.y,
            type != null,
            type?.block ?? false,
            type?.lava ?? false,
            type?.ocean ?? false,
            type?.liquid ?? false,
            type?.damage_units ?? false,
            type?.damage ?? 0f,
            type?.walk_multiplier ?? 1f,
            type?.id ?? "null",
            SafeIsOnFire(tile));
        return info.Exists;
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
}

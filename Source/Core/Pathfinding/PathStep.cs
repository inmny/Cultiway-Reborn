using System;

namespace Cultiway.Core.Pathfinding;

public readonly struct PathStep
{
    private readonly WorldTile _tile;

    public PathStep(WorldTile tile, MovementMethod method, TraversalEstimate estimate = default,
        PortalDefinition entry = null, PortalDefinition exit = null)
    {
        _tile = tile ?? throw new ArgumentNullException(nameof(tile));
        TileId = tile.data?.tile_id ?? -1;
        Method = method;
        Estimate = estimate;
        Entry = entry;
        Exit = exit;
    }

    internal PathStep(int tileId, MovementMethod method, TraversalEstimate estimate = default,
        PortalDefinition entry = null, PortalDefinition exit = null)
    {
        _tile = null;
        TileId = tileId;
        Method = method;
        Estimate = estimate;
        Entry = entry;
        Exit = exit;
    }

    public int TileId { get; }
    public bool HasTile => _tile != null || TileId >= 0;
    public WorldTile Tile => _tile ?? ResolveTile(TileId);
    public MovementMethod Method { get; }
    public TraversalEstimate Estimate { get; }
    public HazardFlags Hazards => Estimate.Hazards;
    public PortalDefinition Entry {get;}
    public PortalDefinition Exit {get;}

    private static WorldTile ResolveTile(int tileId)
    {
        var tiles = World.world?.tiles_list;
        if (tiles == null || tileId < 0 || tileId >= tiles.Length)
        {
            return null;
        }

        return tiles[tileId];
    }
}

[Flags]
public enum HazardFlags
{
    None = 0,
    Block = 1 << 0,
    Lava = 1 << 1,
    Ocean = 1 << 2,
    Fire = 1 << 3,
    TerrainDamage = 1 << 4,
    StaminaDrain = 1 << 5,
    Drowning = 1 << 6,
    LowHealth = 1 << 7,
    Direct = 1 << 8,
    Portal = 1 << 9
}

public readonly struct TraversalEstimate
{
    public TraversalEstimate(float timeSeconds, float staminaCost, float healthCost, float riskCost,
        HazardFlags hazards)
    {
        TimeSeconds = timeSeconds;
        StaminaCost = staminaCost;
        HealthCost = healthCost;
        RiskCost = riskCost;
        Hazards = hazards;
    }

    public float TimeSeconds { get; }
    public float StaminaCost { get; }
    public float HealthCost { get; }
    public float RiskCost { get; }
    public HazardFlags Hazards { get; }

    public static TraversalEstimate Direct => new(0f, 0f, 0f, 0f, HazardFlags.Direct);

    public static TraversalEstimate Portal(float timeSeconds)
    {
        return new TraversalEstimate(timeSeconds, 0f, 0f, 0f, HazardFlags.Portal);
    }
}

using System;

namespace Cultiway.Core.Pathfinding;

/// <summary>一段路径中的纯 tile index 步骤，不隐式解析任何世界对象。</summary>
public readonly struct PathStep
{
    public PathStep(WorldTile tile, MovementMethod method, TraversalEstimate estimate = default,
        PortalDefinition entry = null, PortalDefinition exit = null)
    {
        if (tile == null) throw new ArgumentNullException(nameof(tile));
        TileId = tile.data?.tile_id ?? -1;
        Method = method;
        Estimate = estimate;
        Entry = entry;
        Exit = exit;
        PlannedTileFlags = PathTileSnapshot.Capture(tile).Flags;
    }

    internal PathStep(int tileId, MovementMethod method, TraversalEstimate estimate = default,
        PortalDefinition entry = null, PortalDefinition exit = null, PathTileFlags plannedTileFlags = default)
    {
        TileId = tileId;
        Method = method;
        Estimate = estimate;
        Entry = entry;
        Exit = exit;
        PlannedTileFlags = plannedTileFlags;
    }

    public int TileId { get; }
    public bool HasTile => TileId >= 0;
    public MovementMethod Method { get; }
    public TraversalEstimate Estimate { get; }
    public HazardFlags Hazards => Estimate.Hazards;
    public PortalDefinition Entry { get; }
    public PortalDefinition Exit { get; }
    internal PathTileFlags PlannedTileFlags { get; }
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

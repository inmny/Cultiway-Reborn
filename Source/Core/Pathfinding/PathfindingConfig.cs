namespace Cultiway.Core.Pathfinding;

public class PathfindingConfig
{
    public static PathfindingConfig Default { get; } = new();

    public int ShortRangeTiles { get; set; } = 24;
    public int LongRangeTiles { get; set; } = 96;
    public int MaxNodesShort { get; set; } = 3000;
    public int MaxNodesLong { get; set; } = 12000;
    public int MaxNodesLongFallback { get; set; } = 60000;
    public int FallbackCorridorMinDetour { get; set; } = 32;
    public float FallbackCorridorDetourScale { get; set; } = 0.75f;
    public int PortalCandidates { get; set; } = 2;
    public int PortalSearchRadius { get; set; } = 64;
    public float WalkSpeedScale { get; set; } = 0.4f;
    public float SwimSpeedScale { get; set; } = 0.25f;
    public float SailSpeedScale { get; set; } = 0.6f;
    public int MaxLabelsPerTile { get; set; } = 4;
    public float StaminaCostWeight { get; set; } = 0.08f;
    public float HealthCostWeight { get; set; } = 4f;
    public float LowHealthRiskCost { get; set; } = 160f;
    public float DeathRiskCost { get; set; } = 100000f;
    public float BlockRiskCost { get; set; } = 12f;
    public float FireRiskCost { get; set; } = 30f;
    public float OceanRiskCost { get; set; } = 4f;
    public float LavaRiskCost { get; set; } = 120f;
    public float TerrainDamageRiskCost { get; set; } = 60f;
    public float WaterStaminaDrainPerSecond { get; set; } = 10f;
    public float DrowningDamagePerSecond { get; set; } = 2f;
    public float DamageUnitsTicksPerSecond { get; set; } = 3.333f;
    public float ExhaustedSwimSpeedScale { get; set; } = 0.4f;
    public float XianEnvironmentalDamageFloor { get; set; } = 0.01f;
}

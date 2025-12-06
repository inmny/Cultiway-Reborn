namespace Cultiway.Core.Pathfinding;

public class PathfindingConfig
{
    public static PathfindingConfig Default { get; } = new();

    public int ShortRangeTiles { get; set; } = 24;
    public int LongRangeTiles { get; set; } = 96;
    public int MaxNodesShort { get; set; } = 3000;
    public int MaxNodesLong { get; set; } = 12000;
    public int MaxSwimWidth { get; set; } = 12;
    public int PortalCandidates { get; set; } = 2;
    public int PortalSearchRadius { get; set; } = 64;
    public float WalkSpeedScale { get; set; } = 0.4f;
    public float SwimSpeedScale { get; set; } = 0.25f;
    public float SailSpeedScale { get; set; } = 0.6f;
    public float LongSwimPenalty { get; set; } = 2.5f;
}

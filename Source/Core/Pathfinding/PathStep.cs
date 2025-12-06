using System;

namespace Cultiway.Core.Pathfinding;

public readonly struct PathStep
{
    public PathStep(WorldTile tile, MovementMethod method, StepPenalty penalty = StepPenalty.None)
    {
        Tile = tile ?? throw new ArgumentNullException(nameof(tile));
        Method = method;
        Penalty = penalty;
    }

    public WorldTile Tile { get; }
    public MovementMethod Method { get; }
    public StepPenalty Penalty { get; }
}

[Flags]
public enum StepPenalty
{
    None = 0,
    Block = 1 << 0,
    Lava = 1 << 1,
    Ocean = 1 << 2
}

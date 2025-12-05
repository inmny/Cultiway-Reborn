using System;

namespace Cultiway.Core.Pathfinding;

public readonly struct PathStep
{
    public PathStep(WorldTile tile, MovementMethod method)
    {
        Tile = tile ?? throw new ArgumentNullException(nameof(tile));
        Method = method;
    }

    public WorldTile Tile { get; }
    public MovementMethod Method { get; }
}

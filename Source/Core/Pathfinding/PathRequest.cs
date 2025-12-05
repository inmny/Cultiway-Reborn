namespace Cultiway.Core.Pathfinding;

public sealed class PathRequest
{
    public PathRequest(Actor actor, WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int regionLimit)
    {
        Actor = actor;
        Start = actor?.current_tile;
        Target = target;
        PathOnWater = pathOnWater;
        WalkOnBlocks = walkOnBlocks;
        WalkOnLava = walkOnLava;
        RegionLimit = regionLimit;
    }

    public Actor Actor { get; }
    public WorldTile Start { get; }
    public WorldTile Target { get; }
    public bool PathOnWater { get; }
    public bool WalkOnBlocks { get; }
    public bool WalkOnLava { get; }
    public int RegionLimit { get; }
}

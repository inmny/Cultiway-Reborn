using System.Collections.Generic;

namespace Cultiway.Core.Pathfinding;

public sealed class PortalSnapshot
{
    public long Id { get; set; }
    public WorldTile Tile { get; set; }
    public MapRegion Region { get; set; }
    public float WaitTime { get; set; }
    public float TransferTime { get; set; }
    public IReadOnlyList<PortalConnection> Connections { get; set; }
}

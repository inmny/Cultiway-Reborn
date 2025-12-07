using System.Collections.Generic;
using Cultiway.Core.BuildingComponents;

namespace Cultiway.Core.Pathfinding;

public sealed class PortalSnapshot
{
    public long Id { get; set; }
    public WorldTile Tile { get; set; }
    public MapRegion Region { get; set; }
    public Portal Portal { get; set; }
    public float WaitTime { get; set; }
    public float TransferTime { get; set; }
    public IReadOnlyList<PortalConnection> Connections { get; set; }
}

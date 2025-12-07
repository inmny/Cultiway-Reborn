using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.BuildingComponents;

namespace Cultiway.Core.Pathfinding;

public sealed class PortalDefinition
{
    public PortalDefinition(Portal portal, long id, WorldTile tile, float waitTime, float transferTime,
        IEnumerable<PortalConnection> connections)
    {
        Portal = portal;
        Id = id;
        Tile = tile;
        WaitTime = waitTime;
        TransferTime = transferTime;
        Connections = connections?.ToList() ?? new List<PortalConnection>();
    }
    public Portal Portal {get;}
    public long Id { get; }
    public WorldTile Tile { get; }
    public float WaitTime { get; }
    public float TransferTime { get; }
    public IReadOnlyList<PortalConnection> Connections { get; }
}

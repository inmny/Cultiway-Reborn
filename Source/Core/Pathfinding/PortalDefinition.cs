using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Pathfinding;

public sealed class PortalDefinition
{
    public PortalDefinition(long id, WorldTile tile, float waitTime, float transferTime,
        IEnumerable<PortalConnection> connections)
    {
        Id = id;
        Tile = tile;
        WaitTime = waitTime;
        TransferTime = transferTime;
        Connections = connections?.ToList() ?? new List<PortalConnection>();
    }

    public long Id { get; }
    public WorldTile Tile { get; }
    public float WaitTime { get; }
    public float TransferTime { get; }
    public IReadOnlyList<PortalConnection> Connections { get; }
}

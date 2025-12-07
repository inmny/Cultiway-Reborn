using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Pathfinding;

public sealed class PortalDefinition
{
    public PortalDefinition(string type, long id, WorldTile tile, float waitTime, float transferTime,
        IEnumerable<PortalConnection> connections)
    {
        Type = type;
        Id = id;
        Tile = tile;
        WaitTime = waitTime;
        TransferTime = transferTime;
        Connections = connections?.ToList() ?? new List<PortalConnection>();
    }
    public string Type {get;}
    public long Id { get; }
    public WorldTile Tile { get; }
    public float WaitTime { get; }
    public float TransferTime { get; }
    public IReadOnlyList<PortalConnection> Connections { get; }
}

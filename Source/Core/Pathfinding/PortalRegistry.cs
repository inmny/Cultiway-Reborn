using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Pathfinding;

public sealed class PortalRegistry
{
    public static PortalRegistry Instance { get; } = new();

    private readonly ConcurrentDictionary<long, PortalDefinition> _portals = new();

    public void RegisterOrUpdate(PortalDefinition portal)
    {
        if (portal == null || portal.Id == 0 || portal.Tile == null)
        {
            return;
        }

        _portals[portal.Id] = portal;
    }

    public bool Remove(long id)
    {
        return id != 0 && _portals.TryRemove(id, out _);
    }

    public void Clear()
    {
        _portals.Clear();
    }

    public IReadOnlyList<PortalSnapshot> Snapshot()
    {
        if (_portals.IsEmpty)
        {
            return System.Array.Empty<PortalSnapshot>();
        }

        return _portals.Values
            .Where(p => p.Tile != null)
            .Select(p => new PortalSnapshot
            {
                Id = p.Id,
                Tile = p.Tile,
                Region = p.Tile.region,
                WaitTime = p.WaitTime,
                TransferTime = p.TransferTime,
                Connections = p.Connections.ToList()
            })
            .ToList();
    }
}

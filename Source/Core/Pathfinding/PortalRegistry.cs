using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Libraries;

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

    public IEnumerable<PortalDefinition> Enumerate(PortalAsset type = null)
    {
        foreach (var pair in _portals)
        {
            var portal = pair.Value;
            if (portal?.Tile == null || (type != null && portal.Portal.Asset != type))
            {
                continue;
            }

            yield return portal;
        }
    }

    public bool TryGet(long id, out PortalDefinition portal)
    {
        if (id == 0 || !_portals.TryGetValue(id, out portal) || portal?.Tile == null)
        {
            portal = null;
            return false;
        }

        return true;
    }

    public IReadOnlyList<PortalSnapshot> Snapshot(PortalAsset type = null)
    {
        if (_portals.IsEmpty)
        {
            return System.Array.Empty<PortalSnapshot>();
        }

        return _portals.Values
            .Where(p => p.Tile != null && (type == null || p.Portal.Asset == type))
            .Select(p =>
            {
                var tile = p.Tile;
                return new PortalSnapshot
                {
                    Id = p.Id,
                    Portal = p.Portal,
                    Tile = tile,
                    TileId = tile.data?.tile_id ?? -1,
                    X = tile.x,
                    Y = tile.y,
                    Region = tile.region,
                    WaitTime = p.WaitTime,
                    TransferTime = p.TransferTime,
                    Connections = p.Connections.ToList()
                };
            })
            .ToList();
    }
}

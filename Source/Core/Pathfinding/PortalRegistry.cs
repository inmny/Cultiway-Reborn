using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.Pathfinding;

public sealed class PortalRegistry
{
    public static PortalRegistry Instance { get; } = new();

    private readonly ConcurrentDictionary<long, PortalDefinition> _portals = new();
    private readonly ConcurrentDictionary<long, PathPortalSnapshot> _pathSnapshots = new();
    private readonly object _pathSnapshotSync = new();
    private PathPortalSnapshot[] _pathSnapshotCache = System.Array.Empty<PathPortalSnapshot>();
    private int _pathSnapshotRevision;
    private int _cachedPathSnapshotRevision = -1;

    public void RegisterOrUpdate(PortalDefinition portal)
    {
        if (portal == null || portal.Id == 0 || portal.Tile == null)
        {
            return;
        }

        _portals[portal.Id] = portal;
        _pathSnapshots[portal.Id] = PathPortalSnapshot.Capture(portal);
        Interlocked.Increment(ref _pathSnapshotRevision);
    }

    public bool Remove(long id)
    {
        if (id == 0 || !_portals.TryRemove(id, out _))
        {
            return false;
        }

        _pathSnapshots.TryRemove(id, out _);
        Interlocked.Increment(ref _pathSnapshotRevision);
        return true;
    }

    public void Clear()
    {
        _portals.Clear();
        _pathSnapshots.Clear();
        Interlocked.Increment(ref _pathSnapshotRevision);
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

    /// <summary>
    /// 返回供寻路线程读取的标量传送数据；运行时入口句柄只随结果透传，不在后台解引用。
    /// </summary>
    internal PathPortalSnapshot[] CapturePathSnapshot()
    {
        int revision = Volatile.Read(ref _pathSnapshotRevision);
        if (Volatile.Read(ref _cachedPathSnapshotRevision) == revision)
        {
            return Volatile.Read(ref _pathSnapshotCache);
        }

        lock (_pathSnapshotSync)
        {
            revision = Volatile.Read(ref _pathSnapshotRevision);
            if (_cachedPathSnapshotRevision != revision)
            {
                PathPortalSnapshot[] snapshot = _pathSnapshots.IsEmpty
                    ? System.Array.Empty<PathPortalSnapshot>()
                    : _pathSnapshots.Values.ToArray();
                Volatile.Write(ref _pathSnapshotCache, snapshot);
                Volatile.Write(ref _cachedPathSnapshotRevision, revision);
            }

            return _pathSnapshotCache;
        }
    }
}

internal readonly struct PathPortalSnapshot
{
    private PathPortalSnapshot(PortalDefinition definition, long id, int tileId, int x, int y,
        float waitTime, float transferTime, PortalConnection[] connections)
    {
        Definition = definition;
        Id = id;
        TileId = tileId;
        X = x;
        Y = y;
        WaitTime = waitTime;
        TransferTime = transferTime;
        Connections = connections;
    }

    internal PortalDefinition Definition { get; }
    internal long Id { get; }
    internal int TileId { get; }
    internal int X { get; }
    internal int Y { get; }
    internal float WaitTime { get; }
    internal float TransferTime { get; }
    internal PortalConnection[] Connections { get; }

    internal static PathPortalSnapshot Capture(PortalDefinition definition)
    {
        WorldTile tile = definition.Tile;
        var connections = new PortalConnection[definition.Connections.Count];
        for (int i = 0; i < connections.Length; i++)
        {
            connections[i] = definition.Connections[i];
        }

        return new PathPortalSnapshot(definition, definition.Id, tile?.data?.tile_id ?? -1,
            tile?.x ?? 0, tile?.y ?? 0, definition.WaitTime, definition.TransferTime, connections);
    }
}

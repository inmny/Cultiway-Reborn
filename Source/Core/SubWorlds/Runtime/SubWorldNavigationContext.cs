using System;
using System.Threading;
using Cultiway.Core.Pathfinding;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>将 SubWorldGrid 发布成共享 PathFinder 使用的不可变导航快照。</summary>
internal sealed class SubWorldNavigationContext
{
    private static int nextGeneration;

    private readonly SubWorldGrid grid;
    private long navigationRevision;

    internal SubWorldNavigationContext(long instanceId, SubWorldGrid grid)
    {
        this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        int generation = Interlocked.Increment(ref nextGeneration);
        WorldKey = PathWorldKey.SubWorld(instanceId, generation);
        navigationRevision = 1;
        RebuildSnapshot();
    }

    internal PathWorldKey WorldKey { get; }
    internal PathNavigationGrid CurrentGrid { get; private set; }
    internal long NavigationRevision => navigationRevision;

    internal void PublishTerrainChange()
    {
        navigationRevision++;
        RebuildSnapshot();
    }

    internal bool IsTerrainPassable(int tileIndex)
    {
        return CurrentGrid.TryGetTile(tileIndex, out PathTileSnapshot tile) && tile.Exists && tile.HasType &&
               !tile.Block;
    }

    internal float GetWalkMultiplier(int tileIndex)
    {
        return CurrentGrid.TryGetTile(tileIndex, out PathTileSnapshot tile)
            ? Math.Max(0.05f, tile.WalkMultiplier)
            : 0.05f;
    }

    private void RebuildSnapshot()
    {
        var tiles = new PathTileSnapshot[grid.TileCount];
        for (int index = 0; index < tiles.Length; index++)
        {
            tiles[index] = PathTileSnapshot.Capture(grid.GetTerrainType(index));
        }

        CurrentGrid = PathNavigationGrid.Create(WorldKey, grid.Width, grid.Height, tiles, navigationRevision);
    }
}

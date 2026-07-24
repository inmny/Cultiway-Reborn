using System;
using Cultiway.Abstract;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class TileExtendManager : ExtendComponentManager<TileExtend>
{
    // GeoRegion 等低数量运行时实体继续共用该 Store；这里不再存放逐 tile 实体。
    public readonly EntityStore World;
    private WorldTile[] currentTiles;

    internal bool IsWorldInitializationPending { get; private set; }

    internal TileExtendManager()
    {
        World = new EntityStore();
    }

    public TileExtend Get(int tileId)
    {
        GetTile(tileId);
        return new TileExtend(tileId);
    }

    internal WorldTile GetTile(int tileId)
    {
        if (!Ready())
        {
            throw new InvalidOperationException("TileExtend 尚未绑定当前世界");
        }

        if ((uint)tileId >= (uint)currentTiles.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId), tileId, $"tile id 超出范围: count={currentTiles.Length}");
        }

        return currentTiles[tileId];
    }

    public bool Ready()
    {
        return currentTiles != null &&
               global::World.world != null &&
               ReferenceEquals(currentTiles, global::World.world.tiles_list);
    }

    internal void BeginFitNewWorld(int worldSeedId, int width, int height)
    {
        WorldTile[] tiles = global::World.world?.tiles_list ??
                            throw new InvalidOperationException("当前世界没有 tiles_list");
        if (ReferenceEquals(currentTiles, tiles) &&
            (IsWorldInitializationPending ||
             WorldboxGame.I?.GeoRegions?.IsMembershipReady == true))
        {
            return;
        }

        currentTiles = tiles;
        IsWorldInitializationPending = true;
        WorldboxGame.I?.GeoRegions?.ClearMembership();

        EventSystemHub.Publish(new WorldGeneratedEvent
        {
            WorldSeedId = worldSeedId,
            Width = width,
            Height = height
        });

        ModClass.LogInfo(
            $"[FramePriority] TileExtend 已绑定当前世界: tiles={currentTiles.Length}");
    }

    internal void CancelFitNewWorld()
    {
        currentTiles = null;
        IsWorldInitializationPending = false;
        WorldboxGame.I?.GeoRegions?.ClearMembership();
    }

    internal void CompleteWorldInitialization(WorldTile[] expectedTiles)
    {
        if (ReferenceEquals(currentTiles, expectedTiles) &&
            ReferenceEquals(global::World.world?.tiles_list, expectedTiles))
        {
            IsWorldInitializationPending = false;
        }
    }

    internal void FailWorldInitialization(WorldTile[] expectedTiles)
    {
        if (ReferenceEquals(currentTiles, expectedTiles))
        {
            WorldboxGame.I?.GeoRegions?.ClearMembership();
            IsWorldInitializationPending = false;
        }
    }
}

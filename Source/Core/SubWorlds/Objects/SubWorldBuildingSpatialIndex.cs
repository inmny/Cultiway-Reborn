using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Runtime;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SubWorlds.Objects;

/// <summary>缓存由 Position 与 BuildingAsset.fundament 派生的 Tile 到 Building 索引。</summary>
internal sealed class SubWorldBuildingSpatialIndex
{
    private readonly SubWorldRuntime runtime;
    private readonly LocalObjectId[] buildingByTile;
    private readonly Dictionary<LocalObjectId, IndexedBuilding> buildings = new();

    internal SubWorldBuildingSpatialIndex(SubWorldRuntime runtime, int tileCount)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        if (tileCount <= 0) throw new ArgumentOutOfRangeException(nameof(tileCount));
        buildingByTile = new LocalObjectId[tileCount];
    }

    internal int Count => buildings.Count;

    internal void Register(Entity entity)
    {
        if (entity.IsNull || entity.Store != runtime.EntityStore)
            throw new InvalidOperationException("Building Entity 不属于目标 Runtime");
        if (!entity.HasComponent<SubWorldBuilding>() || !entity.HasComponent<Position>())
            throw new InvalidOperationException("Building Entity 缺少类别或 Position 组件");

        SubWorldBuilding building = entity.GetComponent<SubWorldBuilding>();
        LocalObjectId localObjectId = building.LocalObjectId;
        if (!localObjectId.IsValid) throw new InvalidOperationException("Building LocalObjectId 无效");
        if (buildings.ContainsKey(localObjectId))
            throw new InvalidOperationException($"Building LocalObjectId 重复: {localObjectId}");

        BuildingAsset asset = AssetManager.buildings.get(building.BuildingAssetId);
        Position position = entity.GetComponent<Position>();
        SubWorldBuildingBounds bounds = SubWorldBuildingGeometry.GetBounds(runtime.Grid, position, asset);
        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        for (int x = bounds.MinX; x <= bounds.MaxX; x++)
        {
            int tileIndex = runtime.Grid.GetIndex(x, y);
            if (buildingByTile[tileIndex].IsValid)
                throw new InvalidOperationException(
                    $"Building footprint 重叠: object={localObjectId}, other={buildingByTile[tileIndex]}, tile={tileIndex}");
        }

        buildings.Add(localObjectId, new IndexedBuilding(entity, bounds));
        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        for (int x = bounds.MinX; x <= bounds.MaxX; x++)
            buildingByTile[runtime.Grid.GetIndex(x, y)] = localObjectId;
        runtime.LocalObjectIds.Reserve(localObjectId);
    }

    internal bool TryGet(LocalObjectId localObjectId, out Entity entity)
    {
        if (buildings.TryGetValue(localObjectId, out IndexedBuilding indexed) && !indexed.Entity.IsNull)
        {
            entity = indexed.Entity;
            return true;
        }
        entity = default;
        return false;
    }

    internal bool TryGetAtTile(int tileIndex, out Entity entity)
    {
        if ((uint)tileIndex >= (uint)buildingByTile.Length)
        {
            entity = default;
            return false;
        }

        LocalObjectId localObjectId = buildingByTile[tileIndex];
        if (!localObjectId.IsValid)
        {
            entity = default;
            return false;
        }
        return TryGet(localObjectId, out entity);
    }

    internal bool Unregister(LocalObjectId localObjectId)
    {
        if (!buildings.TryGetValue(localObjectId, out IndexedBuilding indexed)) return false;
        SubWorldBuildingBounds bounds = indexed.Bounds;
        for (int y = bounds.MinY; y <= bounds.MaxY; y++)
        for (int x = bounds.MinX; x <= bounds.MaxX; x++)
        {
            int tileIndex = runtime.Grid.GetIndex(x, y);
            if (buildingByTile[tileIndex] == localObjectId) buildingByTile[tileIndex] = default;
        }
        buildings.Remove(localObjectId);
        return true;
    }

    internal void Clear()
    {
        buildings.Clear();
        Array.Clear(buildingByTile, 0, buildingByTile.Length);
    }

    private readonly struct IndexedBuilding
    {
        internal IndexedBuilding(Entity entity, SubWorldBuildingBounds bounds)
        {
            Entity = entity;
            Bounds = bounds;
        }

        internal Entity Entity { get; }
        internal SubWorldBuildingBounds Bounds { get; }
    }
}

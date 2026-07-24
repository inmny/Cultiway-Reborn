using System;
using System.Collections.Generic;

namespace Cultiway.Core;

/// <summary>
/// GeoRegion 的运行时双向索引。
/// 每个 tile 在同一层最多归属一个地区，正向查询和手动编辑均为 O(1)。
/// </summary>
internal sealed class GeoRegionMembershipIndex
{
    internal const int LayerCount = (int)GeoRegionLayer.Archipelago + 1;

    private readonly WorldTile[] tiles;
    private readonly int[] regionSlotByTileLayer;
    private readonly int[] positionInRegionByTileLayer;
    private readonly List<GeoRegionMembershipEntry> regions;
    private readonly Dictionary<long, int> slotByRegionId;

    internal GeoRegionMembershipIndex(
        WorldTile[] tiles,
        int[] regionSlotByTileLayer,
        int[] positionInRegionByTileLayer,
        List<GeoRegionMembershipEntry> regions)
    {
        this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        this.regionSlotByTileLayer =
            regionSlotByTileLayer ?? throw new ArgumentNullException(nameof(regionSlotByTileLayer));
        this.positionInRegionByTileLayer =
            positionInRegionByTileLayer ?? throw new ArgumentNullException(nameof(positionInRegionByTileLayer));
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));

        int expectedLength = checked(tiles.Length * LayerCount);
        if (regionSlotByTileLayer.Length != expectedLength ||
            positionInRegionByTileLayer.Length != expectedLength)
        {
            throw new InvalidOperationException(
                $"GeoRegion 索引尺寸不匹配: tiles={tiles.Length}, slots={regionSlotByTileLayer.Length}, positions={positionInRegionByTileLayer.Length}");
        }

        slotByRegionId = new Dictionary<long, int>(regions.Count);
        for (int i = 0; i < regions.Count; i++)
        {
            GeoRegionMembershipEntry entry = regions[i];
            if (entry?.Region == null)
            {
                throw new InvalidOperationException($"GeoRegion 索引包含空地区: slot={i}");
            }

            long regionId = entry.Region.getID();
            if (slotByRegionId.ContainsKey(regionId))
            {
                throw new InvalidOperationException($"GeoRegion 索引包含重复地区: id={regionId}");
            }

            slotByRegionId.Add(regionId, i);
            entry.Region.data.TileCount = entry.TileIds.Count;
        }
    }

    internal bool Matches(WorldTile[] currentTiles)
    {
        return ReferenceEquals(tiles, currentTiles);
    }

    internal GeoRegion GetRegion(int tileId, GeoRegionLayer layer)
    {
        int flatIndex = GetFlatIndex(tileId, layer);
        int slot = regionSlotByTileLayer[flatIndex];
        return slot >= 0 && slot < regions.Count ? regions[slot].Region : null;
    }

    internal IEnumerable<GeoRegion> EnumerateRegions(int tileId)
    {
        ValidateTileId(tileId);
        int offset = tileId * LayerCount;
        for (int layer = 0; layer < LayerCount; layer++)
        {
            int slot = regionSlotByTileLayer[offset + layer];
            if (slot < 0 || slot >= regions.Count) continue;

            GeoRegion region = regions[slot].Region;
            if (region != null)
            {
                yield return region;
            }
        }
    }

    internal IReadOnlyList<int> GetTileIds(GeoRegion region)
    {
        return TryGetSlot(region, out int slot)
            ? regions[slot].TileIds
            : Array.Empty<int>();
    }

    internal int GetTileCount(GeoRegion region)
    {
        return TryGetSlot(region, out int slot) ? regions[slot].TileIds.Count : 0;
    }

    internal bool AssignTile(int tileId, GeoRegionLayer layer, GeoRegion region)
    {
        if (region == null) throw new ArgumentNullException(nameof(region));
        ValidateTileId(tileId);
        ValidateLayer(layer);

        int flatIndex = tileId * LayerCount + (int)layer;
        int targetSlot = GetOrCreateSlot(region, layer);
        int currentSlot = regionSlotByTileLayer[flatIndex];
        if (currentSlot == targetSlot)
        {
            return false;
        }

        if (currentSlot >= 0)
        {
            RemoveAt(flatIndex, currentSlot);
        }

        GeoRegionMembershipEntry target = regions[targetSlot];
        positionInRegionByTileLayer[flatIndex] = target.TileIds.Count;
        regionSlotByTileLayer[flatIndex] = targetSlot;
        target.TileIds.Add(tileId);
        target.Region.data.TileCount = target.TileIds.Count;
        return true;
    }

    internal bool RemoveTile(int tileId, GeoRegionLayer layer)
    {
        int flatIndex = GetFlatIndex(tileId, layer);
        int currentSlot = regionSlotByTileLayer[flatIndex];
        if (currentSlot < 0)
        {
            return false;
        }

        RemoveAt(flatIndex, currentSlot);
        return true;
    }

    private int GetOrCreateSlot(GeoRegion region, GeoRegionLayer layer)
    {
        long regionId = region.getID();
        if (slotByRegionId.TryGetValue(regionId, out int slot))
        {
            GeoRegionMembershipEntry existing = regions[slot];
            if (existing.Layer != layer)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 层级不一致: id={regionId}, existing={existing.Layer}, requested={layer}");
            }

            return slot;
        }

        if (region.data == null)
        {
            throw new InvalidOperationException($"GeoRegion 数据为空: id={regionId}");
        }

        if (region.data.Layer != layer)
        {
            throw new InvalidOperationException(
                $"GeoRegion 数据层级不一致: id={regionId}, data={region.data.Layer}, requested={layer}");
        }

        slot = regions.Count;
        regions.Add(new GeoRegionMembershipEntry(region, layer, new List<int>(4)));
        slotByRegionId.Add(regionId, slot);
        region.data.TileCount = 0;
        return slot;
    }

    private void RemoveAt(int flatIndex, int slot)
    {
        GeoRegionMembershipEntry entry = regions[slot];
        int position = positionInRegionByTileLayer[flatIndex];
        if (position < 0 || position >= entry.TileIds.Count)
        {
            throw new InvalidOperationException(
                $"GeoRegion 反向索引损坏: slot={slot}, position={position}, count={entry.TileIds.Count}");
        }

        int lastPosition = entry.TileIds.Count - 1;
        int movedTileId = entry.TileIds[lastPosition];
        if (position != lastPosition)
        {
            entry.TileIds[position] = movedTileId;
            int movedFlatIndex = movedTileId * LayerCount + (int)entry.Layer;
            positionInRegionByTileLayer[movedFlatIndex] = position;
        }

        entry.TileIds.RemoveAt(lastPosition);
        regionSlotByTileLayer[flatIndex] = -1;
        positionInRegionByTileLayer[flatIndex] = -1;
        entry.Region.data.TileCount = entry.TileIds.Count;
    }

    private bool TryGetSlot(GeoRegion region, out int slot)
    {
        slot = -1;
        return region != null && slotByRegionId.TryGetValue(region.getID(), out slot);
    }

    private int GetFlatIndex(int tileId, GeoRegionLayer layer)
    {
        ValidateTileId(tileId);
        ValidateLayer(layer);
        return tileId * LayerCount + (int)layer;
    }

    private void ValidateTileId(int tileId)
    {
        if ((uint)tileId >= (uint)tiles.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId), tileId, $"tile id 超出范围: count={tiles.Length}");
        }
    }

    private static void ValidateLayer(GeoRegionLayer layer)
    {
        int value = (int)layer;
        if ((uint)value >= LayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知 GeoRegionLayer");
        }
    }
}

internal sealed class GeoRegionMembershipEntry
{
    internal GeoRegionMembershipEntry(GeoRegion region, GeoRegionLayer layer, List<int> tileIds)
    {
        Region = region ?? throw new ArgumentNullException(nameof(region));
        Layer = layer;
        TileIds = tileIds ?? throw new ArgumentNullException(nameof(tileIds));
    }

    internal GeoRegion Region { get; }
    internal GeoRegionLayer Layer { get; }
    internal List<int> TileIds { get; }
}

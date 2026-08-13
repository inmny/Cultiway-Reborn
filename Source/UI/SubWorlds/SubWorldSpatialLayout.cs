using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.UI.SubWorlds;

/// <summary>小世界在主地图周边占用的稳定空间槽位。</summary>
internal sealed class SubWorldSpatialSlot
{
    internal SubWorldSpatialSlot(int slotId, Rect cellBounds, int mapWidth, int mapHeight)
    {
        SlotId = slotId;
        CellBounds = cellBounds;
        WorldOrigin = new Vector2(
            cellBounds.center.x - mapWidth * 0.5f,
            cellBounds.center.y - mapHeight * 0.5f);
        WorldBounds = new Rect(WorldOrigin.x, WorldOrigin.y, mapWidth, mapHeight);
    }

    internal int SlotId { get; }
    internal Vector2 WorldOrigin { get; }
    internal Rect WorldBounds { get; }
    internal Rect CellBounds { get; }

    internal Vector2 ToWorld(Vector2 localPosition)
    {
        return WorldOrigin + localPosition;
    }

    internal Vector2 ToLocal(Vector2 worldPosition)
    {
        return worldPosition - WorldOrigin;
    }
}

/// <summary>按矩形环枚举、分配并回收主地图周边的固定槽位。</summary>
internal sealed class SubWorldSpatialLayout
{
    internal const int MaxTemplateSize = 128;
    internal const int SlotGap = 32;
    internal const int Pitch = MaxTemplateSize + SlotGap;

    private readonly Dictionary<long, SubWorldSpatialSlot> occupiedSlots = new();
    private readonly SortedSet<int> freeSlotIds = new();
    private readonly List<Vector2Int> slotCells = new();
    private int centralColumns;
    private int centralRows;
    private int generatedRing;
    private int nextSlotId;
    private Rect mainWorldBounds;

    internal Rect CameraBounds { get; private set; }
    internal bool HasOccupiedSlots => occupiedSlots.Count != 0;

    internal SubWorldSpatialSlot Allocate(long instanceId, int mapWidth, int mapHeight)
    {
        if (mapWidth > MaxTemplateSize || mapHeight > MaxTemplateSize)
        {
            throw new InvalidOperationException(
                $"SubWorld 模板尺寸超过槽位上限: {mapWidth}x{mapHeight}, max={MaxTemplateSize}");
        }

        EnsureConfigured();
        int slotId;
        if (freeSlotIds.Count > 0)
        {
            slotId = freeSlotIds.Min;
            freeSlotIds.Remove(slotId);
        }
        else
        {
            slotId = nextSlotId++;
            EnsureSlotCell(slotId);
        }

        Vector2Int cell = slotCells[slotId];
        var cellBounds = new Rect(cell.x * Pitch, cell.y * Pitch, Pitch, Pitch);
        var slot = new SubWorldSpatialSlot(slotId, cellBounds, mapWidth, mapHeight);
        occupiedSlots.Add(instanceId, slot);
        RecalculateCameraBounds();
        return slot;
    }

    internal SubWorldSpatialSlot Get(long instanceId)
    {
        if (!occupiedSlots.TryGetValue(instanceId, out SubWorldSpatialSlot slot))
            throw new KeyNotFoundException($"SubWorld 空间槽位不存在: instance={instanceId}");
        return slot;
    }

    internal bool Release(long instanceId)
    {
        if (!occupiedSlots.TryGetValue(instanceId, out SubWorldSpatialSlot slot)) return false;
        occupiedSlots.Remove(instanceId);
        freeSlotIds.Add(slot.SlotId);
        RecalculateCameraBounds();
        return true;
    }

    internal bool ContainsMainWorld(Vector2 worldPosition)
    {
        return mainWorldBounds.Contains(worldPosition);
    }

    internal void Clear()
    {
        occupiedSlots.Clear();
        freeSlotIds.Clear();
        slotCells.Clear();
        centralColumns = 0;
        centralRows = 0;
        generatedRing = 0;
        nextSlotId = 0;
        mainWorldBounds = default;
        CameraBounds = default;
    }

    private void EnsureConfigured()
    {
        if (centralColumns != 0) return;
        mainWorldBounds = new Rect(0f, 0f, MapBox.width, MapBox.height);
        centralColumns = Mathf.CeilToInt(MapBox.width / (float)Pitch);
        centralRows = Mathf.CeilToInt(MapBox.height / (float)Pitch);
        CameraBounds = mainWorldBounds;
    }

    private void EnsureSlotCell(int slotId)
    {
        while (slotCells.Count <= slotId) GenerateNextRing();
    }

    private void GenerateNextRing()
    {
        int ring = ++generatedRing;
        int minX = -ring;
        int maxX = centralColumns - 1 + ring;
        int minY = -ring;
        int maxY = centralRows - 1 + ring;

        for (int x = minX; x <= maxX; x++) slotCells.Add(new Vector2Int(x, maxY));
        for (int y = maxY - 1; y >= minY; y--) slotCells.Add(new Vector2Int(maxX, y));
        for (int x = maxX - 1; x >= minX; x--) slotCells.Add(new Vector2Int(x, minY));
        for (int y = minY + 1; y < maxY; y++) slotCells.Add(new Vector2Int(minX, y));
    }

    private void RecalculateCameraBounds()
    {
        float xMin = mainWorldBounds.xMin;
        float yMin = mainWorldBounds.yMin;
        float xMax = mainWorldBounds.xMax;
        float yMax = mainWorldBounds.yMax;
        foreach (SubWorldSpatialSlot slot in occupiedSlots.Values)
        {
            xMin = Mathf.Min(xMin, slot.CellBounds.xMin);
            yMin = Mathf.Min(yMin, slot.CellBounds.yMin);
            xMax = Mathf.Max(xMax, slot.CellBounds.xMax);
            yMax = Mathf.Max(yMax, slot.CellBounds.yMax);
        }
        CameraBounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }
}

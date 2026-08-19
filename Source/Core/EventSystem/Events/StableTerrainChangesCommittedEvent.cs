using System;

namespace Cultiway.Core.EventSystem.Events;

/// <summary>地区系统已经提交一批稳定地形变化，内容系统可以按格子重新检查自己的运行数据。</summary>
public struct StableTerrainChangesCommittedEvent
{
    public int WorldSeedId;
    public int Width;
    public int Height;
    public int TerrainRevision;
    public int TopologyGeneration;
    public bool TopologyChanged;
    public int[] ChangedTileIds;

    public bool HasChanges => ChangedTileIds != null && ChangedTileIds.Length > 0;

    public StableTerrainChangesCommittedEvent(
        int worldSeedId,
        int width,
        int height,
        int terrainRevision,
        int topologyGeneration,
        bool topologyChanged,
        int[] changedTileIds)
    {
        WorldSeedId = worldSeedId;
        Width = width;
        Height = height;
        TerrainRevision = terrainRevision;
        TopologyGeneration = topologyGeneration;
        TopologyChanged = topologyChanged;
        ChangedTileIds = changedTileIds ?? Array.Empty<int>();
    }
}

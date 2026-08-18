using System;
using Cultiway.Core.GeoRegions.Partitioning;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 在主线程分批读取整张地图的格子地形，全部读完后生成一份不会再变化的地形数据。
/// 分批处理可以避免一次读取整张地图造成明显卡顿。
/// </summary>
internal sealed class GeoRegionTerrainCaptureSession
{
    // 当前世界和本次读取的地图尺寸、数据版本，用于确保整批数据来自同一张地图。
    private readonly int worldSeedId;
    private readonly int width;
    private readonly int height;
    private readonly int revision;

    // 原始游戏格子与本次读取所使用的地区划分规则。
    private readonly WorldTile[] tiles;
    private readonly GeoRegionRuleSnapshot rules;

    // 按格子编号保存可供后台计算使用的纯地形数据及分项观察结果。
    private readonly GeoRegionTerrainCell[] cells;
    private readonly GeoRegionTerrainObservation[] observations;

    // 下一格的编号也是当前完成数量；completed 防止重复交付同一份结果。
    private int nextTileId;
    private bool completed;

    /// <summary>
    /// 创建一次整图读取过程，并校验地图、规则和数据版本彼此一致。
    /// </summary>
    internal GeoRegionTerrainCaptureSession(
        int worldSeedId,
        int width,
        int height,
        int revision,
        WorldTile[] tiles,
        GeoRegionRuleSnapshot rules)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
        this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));

        int expectedCount = checked(width * height);
        if (tiles.Length != expectedCount)
        {
            throw new InvalidOperationException(
                $"GeoRegion 捕获尺寸与 tile 数不匹配: width={width}, height={height}, tiles={tiles.Length}");
        }

        if (rules.WorldSeedId != worldSeedId || rules.Width != width || rules.Height != height || rules.Revision != revision)
        {
            throw new InvalidOperationException("GeoRegion 捕获器与规则快照身份不一致");
        }

        this.worldSeedId = worldSeedId;
        this.width = width;
        this.height = height;
        this.revision = revision;
        cells = new GeoRegionTerrainCell[expectedCount];
        observations = new GeoRegionTerrainObservation[expectedCount];
    }

    /// <summary>已经读取的格子数量。</summary>
    internal int CapturedCount => nextTileId;

    /// <summary>本次需要读取的格子总数。</summary>
    internal int TotalCount => cells.Length;

    /// <summary>是否已经读完整张地图。</summary>
    internal bool IsComplete => nextTileId == cells.Length;

    /// <summary>
    /// 从上次停下的位置继续读取，最多处理 <paramref name="maxCells"/> 个格子。
    /// 返回本次处理后是否已经读完整张地图。
    /// </summary>
    internal bool CaptureNext(int maxCells)
    {
        if (completed) throw new InvalidOperationException("GeoRegion 地形捕获已经完成");
        if (maxCells <= 0) throw new ArgumentOutOfRangeException(nameof(maxCells));

        int end = Math.Min(cells.Length, checked(nextTileId + maxCells));
        while (nextTileId < end)
        {
            CaptureCell(nextTileId);
            nextTileId++;
        }

        return IsComplete;
    }

    /// <summary>
    /// 在全部格子读取完成后交付地形数据；同一次读取过程只能交付一次。
    /// </summary>
    internal GeoRegionTerrainSnapshot Complete()
    {
        if (completed) throw new InvalidOperationException("GeoRegion 地形快照不能重复完成");
        if (!IsComplete)
        {
            throw new InvalidOperationException(
                $"GeoRegion 地形捕获尚未完成: captured={nextTileId}, total={cells.Length}");
        }

        completed = true;
        return new GeoRegionTerrainSnapshot(worldSeedId, width, height, revision, cells, observations);
    }

    /// <summary>
    /// 读取一个游戏格子，同时保存分项观察结果和供地区划分直接使用的合成结果。
    /// </summary>
    private void CaptureCell(int tileId)
    {
        GeoRegionTerrainObservation observation = GeoRegionTerrainCellCapture.CaptureObservation(
            tiles[tileId],
            tileId,
            width,
            rules);
        observations[tileId] = observation;
        cells[tileId] = observation.Compose();
    }
}

using System;
using System.Collections.Generic;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 某格当前认定的地面情况，只保存分区需要的简单数值，不引用游戏中的地块或资源对象。
/// </summary>
internal readonly struct GeoRegionTerrainCell : IEquatable<GeoRegionTerrainCell>
{
    /// <summary>创建某格当前用于分区的地面数据。</summary>
    internal GeoRegionTerrainCell(
        GeoRegionTerrainLayer layer,
        GeoRegionTerrainKind terrainKind,
        GeoRegionPrimaryCategoryCode primaryBiomeCode,
        string tileTypeId,
        string biomeId,
        bool isOceanMaterial,
        bool isBeachMaterial,
        bool isFillablePit,
        bool isLava,
        bool isGoo,
        bool isMountain)
    {
        Layer = layer;
        TerrainKind = terrainKind;
        PrimaryBiomeCode = primaryBiomeCode;
        TileTypeId = tileTypeId ?? string.Empty;
        BiomeId = biomeId ?? string.Empty;
        IsOceanMaterial = isOceanMaterial;
        IsBeachMaterial = isBeachMaterial;
        IsFillablePit = isFillablePit;
        IsLava = isLava;
        IsGoo = isGoo;
        IsMountain = isMountain;
    }

    /// <summary>游戏中该格原本所在的地面层。</summary>
    internal GeoRegionTerrainLayer Layer { get; }
    /// <summary>分区时认定的地面种类。</summary>
    internal GeoRegionTerrainKind TerrainKind { get; }
    /// <summary>该格归入的主要地表类别。</summary>
    internal GeoRegionPrimaryCategoryCode PrimaryBiomeCode { get; }
    /// <summary>该格地块材质的标识。</summary>
    internal string TileTypeId { get; }
    /// <summary>该格生物群系的标识。</summary>
    internal string BiomeId { get; }
    /// <summary>该格材质是否属于海洋。</summary>
    internal bool IsOceanMaterial { get; }
    /// <summary>该格材质是否属于海滩。</summary>
    internal bool IsBeachMaterial { get; }
    /// <summary>该格是否是可填平的坑。</summary>
    internal bool IsFillablePit { get; }
    /// <summary>该格是否属于熔岩。</summary>
    internal bool IsLava { get; }
    /// <summary>该格是否属于黏液。</summary>
    internal bool IsGoo { get; }
    /// <summary>该格是否属于山体。</summary>
    internal bool IsMountain { get; }

    /// <summary>判断两格用于分区的地面数据是否完全相同。</summary>
    public bool Equals(GeoRegionTerrainCell other)
    {
        return Layer == other.Layer &&
               TerrainKind == other.TerrainKind &&
               PrimaryBiomeCode == other.PrimaryBiomeCode &&
               string.Equals(TileTypeId, other.TileTypeId, StringComparison.Ordinal) &&
               string.Equals(BiomeId, other.BiomeId, StringComparison.Ordinal) &&
               IsOceanMaterial == other.IsOceanMaterial &&
               IsBeachMaterial == other.IsBeachMaterial &&
               IsFillablePit == other.IsFillablePit &&
               IsLava == other.IsLava &&
               IsGoo == other.IsGoo &&
               IsMountain == other.IsMountain;
    }

    /// <summary>判断传入对象是否是内容相同的单格地面数据。</summary>
    public override bool Equals(object obj)
    {
        return obj is GeoRegionTerrainCell other && Equals(other);
    }

    /// <summary>根据该格各项地面数据生成用于比较和查表的哈希值。</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Layer;
            hash = hash * 397 ^ (int)TerrainKind;
            hash = hash * 397 ^ (int)PrimaryBiomeCode;
            hash = hash * 397 ^ (TileTypeId?.GetHashCode() ?? 0);
            hash = hash * 397 ^ (BiomeId?.GetHashCode() ?? 0);
            hash = hash * 397 ^ (IsOceanMaterial ? 1 : 0);
            hash = hash * 397 ^ (IsBeachMaterial ? 1 : 0);
            hash = hash * 397 ^ (IsFillablePit ? 1 : 0);
            hash = hash * 397 ^ (IsLava ? 1 : 0);
            hash = hash * 397 ^ (IsGoo ? 1 : 0);
            return hash * 397 ^ (IsMountain ? 1 : 0);
        }
    }
}

/// <summary>
/// 某次分区计算看到的整张地图地形。创建后内容不变，可安全交给后台计算。
/// </summary>
internal sealed class GeoRegionTerrainSnapshot
{
    /// <summary>每页保存的地图格数量；更新少量格子时只复制它们所在的页。</summary>
    private const int PageSize = 1024;
    /// <summary>按页保存每格当前用于分区的地面数据。</summary>
    private readonly GeoRegionTerrainCell[][] cellPages;
    /// <summary>按页保存每格尚未整理的完整观测记录。</summary>
    private readonly GeoRegionTerrainObservation[][] observationPages;
    /// <summary>地图中的格子总数，等于宽度乘以高度。</summary>
    private readonly int cellCount;

    /// <summary>从整张地图的连续数组创建地形快照，并复制为分页存储。</summary>
    internal GeoRegionTerrainSnapshot(
        int worldSeedId,
        int width,
        int height,
        int revision,
        GeoRegionTerrainCell[] cells,
        GeoRegionTerrainObservation[] observations)
    {
        ValidateIdentity(width, height, revision);
        int expectedCount = checked(width * height);
        if (cells == null || cells.Length != expectedCount ||
            observations == null || observations.Length != expectedCount)
        {
            throw new InvalidOperationException(
                $"GeoRegion 地形快照数组尺寸不匹配: width={width}, height={height}, " +
                $"cells={cells?.Length ?? 0}, observations={observations?.Length ?? 0}");
        }

        WorldSeedId = worldSeedId;
        Width = width;
        Height = height;
        Revision = revision;
        cellCount = expectedCount;
        cellPages = CreatePages(cells);
        observationPages = CreatePages(observations);
    }

    /// <summary>复用未变化的页面创建新版本快照，供少量格子更新时使用。</summary>
    private GeoRegionTerrainSnapshot(
        int worldSeedId,
        int width,
        int height,
        int revision,
        int cellCount,
        GeoRegionTerrainCell[][] cellPages,
        GeoRegionTerrainObservation[][] observationPages)
    {
        ValidateIdentity(width, height, revision);
        if (cellCount != checked(width * height))
        {
            throw new InvalidOperationException("GeoRegion 分页快照尺寸不匹配");
        }

        WorldSeedId = worldSeedId;
        Width = width;
        Height = height;
        Revision = revision;
        this.cellCount = cellCount;
        this.cellPages = cellPages ?? throw new ArgumentNullException(nameof(cellPages));
        this.observationPages = observationPages ?? throw new ArgumentNullException(nameof(observationPages));
    }

    /// <summary>这份地图所属世界的种子标识。</summary>
    internal int WorldSeedId { get; }
    /// <summary>地图横向格子数。</summary>
    internal int Width { get; }
    /// <summary>地图纵向格子数。</summary>
    internal int Height { get; }
    /// <summary>地形数据版本，用于阻止不同批次的数据混用。</summary>
    internal int Revision { get; }
    /// <summary>地图格子总数。</summary>
    internal int CellCount => cellCount;

    /// <summary>按格子编号读取当前真正用于分区的地面数据。</summary>
    internal GeoRegionTerrainCell GetCell(int tileId)
    {
        ValidateTileId(tileId);
        return cellPages[tileId / PageSize][tileId % PageSize];
    }

    /// <summary>按格子编号读取完整观测记录。</summary>
    internal GeoRegionTerrainObservation GetObservation(int tileId)
    {
        ValidateTileId(tileId);
        return observationPages[tileId / PageSize][tileId % PageSize];
    }

    /// <summary>仅替换指定格子的观测记录，保留当前已整理地面数据和版本号。</summary>
    internal GeoRegionTerrainSnapshot WithAppliedObservations(
        int[] dirtyTileIds,
        GeoRegionTerrainObservation[] dirtyObservations)
    {
        ValidateDirtyInput(dirtyTileIds, dirtyObservations, "applied");
        GeoRegionTerrainObservation[][] nextObservationPages =
            (GeoRegionTerrainObservation[][])observationPages.Clone();
        for (int i = 0; i < dirtyTileIds.Length; i++)
        {
            int tileId = dirtyTileIds[i];
            ValidateTileId(tileId);
            int pageIndex = tileId / PageSize;
            if (ReferenceEquals(nextObservationPages[pageIndex], observationPages[pageIndex]))
            {
                nextObservationPages[pageIndex] =
                    (GeoRegionTerrainObservation[])observationPages[pageIndex].Clone();
            }
            nextObservationPages[pageIndex][tileId % PageSize] = dirtyObservations[i];
        }

        return new GeoRegionTerrainSnapshot(
            WorldSeedId,
            Width,
            Height,
            Revision,
            cellCount,
            cellPages,
            nextObservationPages);
    }

    /// <summary>重新整理指定格子的观测，返回新版本快照，并报告真正改变分区输入的格子数。</summary>
    internal GeoRegionTerrainSnapshot WithDirtyObservations(
        int revision,
        int[] dirtyTileIds,
        GeoRegionTerrainObservation[] dirtyObservations,
        out int changedCellCount)
    {
        if (revision <= 0 || revision == Revision) throw new ArgumentOutOfRangeException(nameof(revision));
        ValidateDirtyInput(dirtyTileIds, dirtyObservations, "dirty");

        GeoRegionTerrainCell[][] nextCellPages = (GeoRegionTerrainCell[][])cellPages.Clone();
        GeoRegionTerrainObservation[][] nextObservationPages =
            (GeoRegionTerrainObservation[][])observationPages.Clone();
        changedCellCount = 0;
        for (int i = 0; i < dirtyTileIds.Length; i++)
        {
            int tileId = dirtyTileIds[i];
            ValidateTileId(tileId);
            int pageIndex = tileId / PageSize;
            int pageOffset = tileId % PageSize;
            if (ReferenceEquals(nextObservationPages[pageIndex], observationPages[pageIndex]))
            {
                nextObservationPages[pageIndex] =
                    (GeoRegionTerrainObservation[])observationPages[pageIndex].Clone();
            }
            nextObservationPages[pageIndex][pageOffset] = dirtyObservations[i];

            GeoRegionTerrainCell nextCell = dirtyObservations[i].Compose();
            if (cellPages[pageIndex][pageOffset].Equals(nextCell)) continue;
            if (ReferenceEquals(nextCellPages[pageIndex], cellPages[pageIndex]))
            {
                nextCellPages[pageIndex] = (GeoRegionTerrainCell[])cellPages[pageIndex].Clone();
            }
            nextCellPages[pageIndex][pageOffset] = nextCell;
            changedCellCount++;
        }

        return new GeoRegionTerrainSnapshot(
            WorldSeedId,
            Width,
            Height,
            revision,
            cellCount,
            nextCellPages,
            nextObservationPages);
    }

    /// <summary>把连续数组按固定大小拆成多页，便于更新时复用没有变化的页。</summary>
    private static T[][] CreatePages<T>(T[] source)
    {
        int pageCount = (source.Length + PageSize - 1) / PageSize;
        var pages = new T[pageCount][];
        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            int sourceOffset = pageIndex * PageSize;
            int length = Math.Min(PageSize, source.Length - sourceOffset);
            var page = new T[length];
            Array.Copy(source, sourceOffset, page, 0, length);
            pages[pageIndex] = page;
        }
        return pages;
    }

    /// <summary>确认地图尺寸和数据版本都是有效正数。</summary>
    private static void ValidateIdentity(int width, int height, int revision)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "GeoRegion 地形快照尺寸必须为正数");
        }
        if (revision <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision), "GeoRegion 地形快照 revision 必须为正数");
        }
    }

    /// <summary>确认待更新格子与观测一一对应、编号有效且没有重复。</summary>
    private void ValidateDirtyInput(
        int[] dirtyTileIds,
        GeoRegionTerrainObservation[] dirtyObservations,
        string operation)
    {
        if (dirtyTileIds == null) throw new ArgumentNullException(nameof(dirtyTileIds));
        if (dirtyObservations == null || dirtyObservations.Length != dirtyTileIds.Length)
        {
            throw new InvalidOperationException(
                $"GeoRegion {operation} tile 与 observation 数量不一致");
        }
        var seen = new HashSet<int>();
        for (int i = 0; i < dirtyTileIds.Length; i++)
        {
            ValidateTileId(dirtyTileIds[i]);
            if (!seen.Add(dirtyTileIds[i]))
            {
                throw new InvalidOperationException(
                    $"GeoRegion {operation} 输入包含重复 tile: tile={dirtyTileIds[i]}");
            }
        }
    }

    /// <summary>确认格子编号落在当前地图范围内。</summary>
    private void ValidateTileId(int tileId)
    {
        if ((uint)tileId >= (uint)cellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId));
        }
    }
}

/// <summary>
/// 启动一次分区计算所需的地形和规则，两者必须属于同一个世界、尺寸和版本。
/// </summary>
internal sealed class GeoRegionPartitionInput
{
    /// <summary>创建分区输入，并确认地形与规则来自同一份地图数据。</summary>
    internal GeoRegionPartitionInput(
        GeoRegionTerrainSnapshot terrain,
        GeoRegionRuleSnapshot rules)
    {
        Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));

        if (terrain.WorldSeedId != rules.WorldSeedId ||
            terrain.Width != rules.Width ||
            terrain.Height != rules.Height ||
            terrain.Revision != rules.Revision)
        {
            throw new InvalidOperationException(
                $"GeoRegion 地形与规则快照身份不一致: " +
                $"terrain={terrain.WorldSeedId}/{terrain.Width}x{terrain.Height}/r{terrain.Revision}, " +
                $"rules={rules.WorldSeedId}/{rules.Width}x{rules.Height}/r{rules.Revision}");
        }
    }

    /// <summary>本次计算使用的整张地图地形。</summary>
    internal GeoRegionTerrainSnapshot Terrain { get; }
    /// <summary>本次计算使用的地区分类规则。</summary>
    internal GeoRegionRuleSnapshot Rules { get; }
    /// <summary>本次计算所属世界的种子标识。</summary>
    internal int WorldSeedId => Terrain.WorldSeedId;
    /// <summary>本次计算的地图宽度。</summary>
    internal int Width => Terrain.Width;
    /// <summary>本次计算的地图高度。</summary>
    internal int Height => Terrain.Height;
    /// <summary>本次计算使用的数据版本。</summary>
    internal int Revision => Terrain.Revision;
}

using System;
using System.Collections.Generic;
using System.Threading;

namespace Cultiway.Core;

/// <summary>
/// 一份创建后不再修改的地块归属表，同时保存“地块属于哪个地区”和“地区包含哪些地块”。
/// 每个地块在同一层最多属于一个地区，两种方向的查询都可直接定位，无需遍历全图。
/// </summary>
internal sealed class GeoRegionMembershipSnapshot
{
    /// <summary>地区层级总数，用于把“地块加层级”换算成一维数组位置。</summary>
    internal const int LayerCount = (int)GeoRegionLayer.Archipelago + 1;

    // 记录这份归属表对应的世界地块数组，也用于判断切换世界后数据是否仍有效。
    private readonly WorldTile[] tiles;
    // 按“地块加层级”保存地区在下方地区列表中的位置，供地块查地区。
    private readonly int[] regionSlotByTileLayer;
    // 保存地块在对应地区地块列表中的位置，用于核对两个方向的数据是否一致。
    private readonly int[] positionInRegionByTileLayer;
    // 保存每个地块各层归属上次变化的编号，增量重算用它识别被改动的格子。
    private readonly int[] assignmentStampByTileLayer;
    // 每个地区及其层级、地块编号列表，供地区反查地块。
    private readonly List<GeoRegionMembershipEntry> regions;
    // 从地区唯一编号直接找到它在地区列表中的位置。
    private readonly Dictionary<long, int> slotByRegionId;
    // 当前仍固定读取这份数据的操作数量，归零前不能释放相关旧地区。
    private int readerCount;

    /// <summary>
    /// 复制并接管一整套地块归属数据，同时检查数组尺寸、地区层级和双向对应关系。
    /// </summary>
    internal GeoRegionMembershipSnapshot(
        int revision,
        WorldTile[] tiles,
        int[] regionSlotByTileLayer,
        int[] positionInRegionByTileLayer,
        IList<GeoRegionMembershipEntry> regions,
        int[] assignmentStampByTileLayer = null)
    {
        if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
        this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        if (regionSlotByTileLayer == null) throw new ArgumentNullException(nameof(regionSlotByTileLayer));
        if (positionInRegionByTileLayer == null) throw new ArgumentNullException(nameof(positionInRegionByTileLayer));
        if (regions == null) throw new ArgumentNullException(nameof(regions));

        Revision = revision;
        this.regionSlotByTileLayer = (int[])regionSlotByTileLayer.Clone();
        this.positionInRegionByTileLayer = (int[])positionInRegionByTileLayer.Clone();
        this.regions = new List<GeoRegionMembershipEntry>(regions.Count);

        int expectedLength = checked(tiles.Length * LayerCount);
        if (regionSlotByTileLayer.Length != expectedLength ||
            positionInRegionByTileLayer.Length != expectedLength ||
            (assignmentStampByTileLayer != null && assignmentStampByTileLayer.Length != expectedLength))
        {
            throw new InvalidOperationException(
                $"GeoRegion 索引尺寸不匹配: tiles={tiles.Length}, slots={regionSlotByTileLayer.Length}, " +
                $"positions={positionInRegionByTileLayer.Length}, stamps={assignmentStampByTileLayer?.Length ?? expectedLength}");
        }

        if (assignmentStampByTileLayer == null)
        {
            this.assignmentStampByTileLayer = new int[expectedLength];
            for (int i = 0; i < this.assignmentStampByTileLayer.Length; i++)
            {
                this.assignmentStampByTileLayer[i] = 1;
            }
        }
        else
        {
            this.assignmentStampByTileLayer = (int[])assignmentStampByTileLayer.Clone();
        }

        slotByRegionId = new Dictionary<long, int>(regions.Count);
        for (int i = 0; i < regions.Count; i++)
        {
            GeoRegionMembershipEntry entry = regions[i] ??
                                             throw new InvalidOperationException($"GeoRegion 索引包含空地区: slot={i}");
            entry = entry.Clone();
            this.regions.Add(entry);

            GeoRegionData data = entry.Region.data ??
                                 throw new InvalidOperationException($"GeoRegion 索引地区缺少数据: slot={i}");
            if (data.Layer != entry.Layer)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 索引层级不一致: slot={i}, data={data.Layer}, entry={entry.Layer}");
            }

            long regionId = entry.Region.getID();
            if (slotByRegionId.ContainsKey(regionId))
            {
                throw new InvalidOperationException($"GeoRegion 索引包含重复地区: id={regionId}");
            }

            slotByRegionId.Add(regionId, i);
            data.TileCount = entry.TileIds.Count;
        }

        ValidateBidirectionalIndex();
    }

    /// <summary>整份归属数据的变化编号，用于确认增量结果基于正确的上一版。</summary>
    internal int Revision { get; }
    /// <summary>这份归属数据中登记的地区数量。</summary>
    internal int RegionCount => regions.Count;
    /// <summary>当前仍在读取这份数据的操作数量。</summary>
    internal int ReaderCount => Volatile.Read(ref readerCount);

    /// <summary>检查这份归属数据是否由指定的世界地块数组构建。</summary>
    internal bool Matches(WorldTile[] currentTiles)
    {
        return ReferenceEquals(tiles, currentTiles);
    }

    /// <summary>直接查找某地块在指定层所属的地区，没有归属时返回空。</summary>
    internal GeoRegion GetRegion(int tileId, GeoRegionLayer layer)
    {
        int flatIndex = GetFlatIndex(tileId, layer);
        int slot = regionSlotByTileLayer[flatIndex];
        return slot >= 0 && slot < regions.Count ? regions[slot].Region : null;
    }

    /// <summary>取得某地块指定层归属的变化编号，供增量重算比较前后状态。</summary>
    internal int GetAssignmentStamp(int tileId, GeoRegionLayer layer)
    {
        return assignmentStampByTileLayer[GetFlatIndex(tileId, layer)];
    }

    /// <summary>复制全部地块归属变化编号，供下一轮重算在独立数组上修改。</summary>
    internal int[] CopyAssignmentStamps()
    {
        return (int[])assignmentStampByTileLayer.Clone();
    }

    /// <summary>依次返回某地块在所有层所属的地区。</summary>
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

    /// <summary>返回地区包含的地块编号只读列表；地区不存在时返回空列表。</summary>
    internal IReadOnlyList<int> GetTileIds(GeoRegion region)
    {
        return TryGetSlot(region, out int slot)
            ? regions[slot].TileIds
            : Array.Empty<int>();
    }

    /// <summary>返回地区包含的地块数量；地区不存在时返回 0。</summary>
    internal int GetTileCount(GeoRegion region)
    {
        return TryGetSlot(region, out int slot) ? regions[slot].TileIds.Count : 0;
    }

    /// <summary>按内部列表位置取得地区，重算流程遍历全部地区时使用。</summary>
    internal GeoRegion GetRegionBySlot(int slot)
    {
        if ((uint)slot >= (uint)regions.Count) throw new ArgumentOutOfRangeException(nameof(slot));
        return regions[slot].Region;
    }

    /// <summary>登记一个开始固定读取这份数据的操作。</summary>
    internal void AddReader()
    {
        Interlocked.Increment(ref readerCount);
    }

    /// <summary>登记一个固定读取已结束；计数失衡时恢复为 0 并报错。</summary>
    internal void RemoveReader()
    {
        int remaining = Interlocked.Decrement(ref readerCount);
        if (remaining < 0)
        {
            Interlocked.Exchange(ref readerCount, 0);
            throw new InvalidOperationException("GeoRegion membership read lease 计数失衡");
        }
    }

    /// <summary>逐项核对地块到地区、地区到地块两种记录完全一致。</summary>
    private void ValidateBidirectionalIndex()
    {
        for (int slot = 0; slot < regions.Count; slot++)
        {
            GeoRegionMembershipEntry entry = regions[slot];
            int layer = (int)entry.Layer;
            for (int position = 0; position < entry.TileIds.Count; position++)
            {
                int tileId = entry.TileIds[position];
                ValidateTileId(tileId);
                int flatIndex = tileId * LayerCount + layer;
                if (regionSlotByTileLayer[flatIndex] != slot ||
                    positionInRegionByTileLayer[flatIndex] != position)
                {
                    throw new InvalidOperationException(
                        $"GeoRegion 反向索引不一致: slot={slot}, tile={tileId}, layer={entry.Layer}, position={position}");
                }
            }
        }

        for (int flatIndex = 0; flatIndex < regionSlotByTileLayer.Length; flatIndex++)
        {
            int slot = regionSlotByTileLayer[flatIndex];
            int position = positionInRegionByTileLayer[flatIndex];
            if (slot < 0)
            {
                if (position >= 0)
                {
                    throw new InvalidOperationException(
                        $"GeoRegion 空正向索引包含反向位置: index={flatIndex}, position={position}");
                }
                continue;
            }

            if ((uint)slot >= (uint)regions.Count)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 正向索引 slot 越界: index={flatIndex}, slot={slot}, regions={regions.Count}");
            }

            int tileId = flatIndex / LayerCount;
            int layer = flatIndex % LayerCount;
            GeoRegionMembershipEntry entry = regions[slot];
            if ((int)entry.Layer != layer ||
                (uint)position >= (uint)entry.TileIds.Count ||
                entry.TileIds[position] != tileId)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 正向索引不一致: tile={tileId}, layer={(GeoRegionLayer)layer}, slot={slot}, position={position}");
            }
        }
    }

    /// <summary>按地区唯一编号查找其内部列表位置。</summary>
    private bool TryGetSlot(GeoRegion region, out int slot)
    {
        slot = -1;
        return region != null && slotByRegionId.TryGetValue(region.getID(), out slot);
    }

    /// <summary>验证输入后，把地块编号和层级换算成数组位置。</summary>
    private int GetFlatIndex(int tileId, GeoRegionLayer layer)
    {
        ValidateTileId(tileId);
        ValidateLayer(layer);
        return tileId * LayerCount + (int)layer;
    }

    /// <summary>确认地块编号位于当前世界地块数组范围内。</summary>
    private void ValidateTileId(int tileId)
    {
        if ((uint)tileId >= (uint)tiles.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId), tileId, $"tile id 超出范围: count={tiles.Length}");
        }
    }

    /// <summary>确认层级是已定义的地理地区层。</summary>
    private static void ValidateLayer(GeoRegionLayer layer)
    {
        int value = (int)layer;
        if ((uint)value >= LayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知 GeoRegionLayer");
        }
    }
}

/// <summary>
/// 保存一个地区、它所在的层级和所含地块编号，是“地区反查地块”的一条完整记录。
/// </summary>
internal sealed class GeoRegionMembershipEntry
{
    // 创建时复制地块编号，之后只读，避免外部列表变化破坏归属关系。
    private readonly int[] tileIds;

    /// <summary>复制传入的地块编号并建立一条地区归属记录。</summary>
    internal GeoRegionMembershipEntry(GeoRegion region, GeoRegionLayer layer, IList<int> tileIds)
    {
        Region = region ?? throw new ArgumentNullException(nameof(region));
        Layer = layer;
        if (tileIds == null) throw new ArgumentNullException(nameof(tileIds));

        this.tileIds = new int[tileIds.Count];
        for (int i = 0; i < tileIds.Count; i++)
        {
            this.tileIds[i] = tileIds[i];
        }
    }

    /// <summary>这条记录对应的地区。</summary>
    internal GeoRegion Region { get; }
    /// <summary>该地区所属的地图分类层。</summary>
    internal GeoRegionLayer Layer { get; }
    /// <summary>地区包含的全部地块编号。</summary>
    internal IReadOnlyList<int> TileIds => tileIds;

    /// <summary>创建内容相同且拥有独立地块编号数组的新记录。</summary>
    internal GeoRegionMembershipEntry Clone()
    {
        return new GeoRegionMembershipEntry(Region, Layer, tileIds);
    }
}

/// <summary>
/// 在一段连续读取期间固定使用同一份地块归属数据，避免查询中途被新版数据替换。
/// 调用方使用完毕后必须释放此对象，旧地区才能在无人读取时安全删除。
/// </summary>
internal sealed class GeoRegionMembershipReadLease : IDisposable
{
    // 保存负责释放读取计数的管理器、当前固定数据，以及进入前线程原本固定的数据。
    private GeoRegionManager manager;
    private GeoRegionMembershipSnapshot snapshot;
    private readonly GeoRegionMembershipSnapshot previousSnapshot;

    /// <summary>固定当前归属数据并登记一个读取者，同时记住嵌套读取前的数据。</summary>
    internal GeoRegionMembershipReadLease(
        GeoRegionManager manager,
        GeoRegionMembershipSnapshot snapshot,
        GeoRegionMembershipSnapshot previousSnapshot)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        this.previousSnapshot = previousSnapshot;
        snapshot.AddReader();
    }

    /// <summary>尚未释放且仍持有固定归属数据时为真。</summary>
    internal bool IsValid => snapshot != null;

    /// <summary>结束固定读取，恢复线程先前的数据并减少读取者计数；重复调用不会产生影响。</summary>
    public void Dispose()
    {
        GeoRegionMembershipSnapshot current = snapshot;
        if (current == null) return;

        snapshot = null;
        GeoRegionManager owner = manager;
        manager = null;
        owner.ReleaseReadLease(current, previousSnapshot);
    }
}

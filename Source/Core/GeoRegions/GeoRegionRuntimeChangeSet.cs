using System;
using System.Collections.Generic;

namespace Cultiway.Core.GeoRegions;

/// <summary>标记地区哪一类运行中内容发生了变化，可组合多项以便只刷新受影响结果。</summary>
[Flags]
internal enum GeoRegionRuntimeChangeKind
{
    /// <summary>没有需要刷新的内容。</summary>
    None = 0,
    /// <summary>名称、颜色等直接显示信息发生变化。</summary>
    Presentation = 1 << 0,
    /// <summary>地区包含的地块或边界形状发生变化。</summary>
    Geometry = 1 << 1,
    /// <summary>同层边界相邻关系发生变化。</summary>
    Adjacency = 1 << 2,
    /// <summary>不同层之间的重叠或包含关系发生变化。</summary>
    CrossLayer = 1 << 3,
    /// <summary>地区内城市、王国等组成统计发生变化。</summary>
    Composition = 1 << 4
}

/// <summary>
/// 汇总一次地块归属重算对游戏运行状态造成的改动。
/// 提交新版归属数据后，管理器依据这里的记录只刷新真正受影响的地区、单位、图标和地图格。
/// </summary>
internal sealed class GeoRegionRuntimeChangeSet
{
    // 分别保存每个地区的变化种类、需重算单位的地区、需重画图标的地区和需刷新显示的地块。
    private readonly Dictionary<GeoRegion, GeoRegionRuntimeChangeKind> regionChanges = new();
    private readonly HashSet<GeoRegion> unitDirtyRegions = new();
    private readonly HashSet<GeoRegion> shapeDirtyRegions = new();
    private readonly HashSet<int> mapDirtyTileIds = new();

    /// <summary>各地区需要刷新的内容种类。</summary>
    internal IReadOnlyDictionary<GeoRegion, GeoRegionRuntimeChangeKind> RegionChanges => regionChanges;
    /// <summary>需要重新收集所含单位的地区。</summary>
    internal IReadOnlyCollection<GeoRegion> UnitDirtyRegions => unitDirtyRegions;
    /// <summary>边界图标需要重新绘制的地区。</summary>
    internal IReadOnlyCollection<GeoRegion> ShapeDirtyRegions => shapeDirtyRegions;
    /// <summary>地图模式需要重新着色的地块编号。</summary>
    internal IReadOnlyCollection<int> MapDirtyTileIds => mapDirtyTileIds;
    /// <summary>本次重算中实际改变地区归属的“地块加层级”数量。</summary>
    internal int ChangedAssignmentCount { get; private set; }

    /// <summary>合并登记某地区的变化种类，同一地区的多次登记不会丢失已有标记。</summary>
    internal void AddRegionChange(GeoRegion region, GeoRegionRuntimeChangeKind change)
    {
        if (region == null || change == GeoRegionRuntimeChangeKind.None) return;
        regionChanges.TryGetValue(region, out GeoRegionRuntimeChangeKind current);
        regionChanges[region] = current | change;
    }

    /// <summary>登记一个需要重新统计单位的地区，并自动去重。</summary>
    internal void AddUnitDirtyRegion(GeoRegion region)
    {
        if (region != null) unitDirtyRegions.Add(region);
    }

    /// <summary>检查地区是否已经登记为需要重新统计单位。</summary>
    internal bool IsUnitDirty(GeoRegion region)
    {
        return region != null && unitDirtyRegions.Contains(region);
    }

    /// <summary>登记一个需要重画边界图标的地区，并自动去重。</summary>
    internal void AddShapeDirtyRegion(GeoRegion region)
    {
        if (region != null) shapeDirtyRegions.Add(region);
    }

    /// <summary>登记一个需要刷新地图显示的有效地块编号，并自动去重。</summary>
    internal void AddMapDirtyTile(int tileId)
    {
        if (tileId >= 0) mapDirtyTileIds.Add(tileId);
    }

    /// <summary>记录又有一个“地块加层级”的归属发生了实际改变。</summary>
    internal void CountChangedAssignment()
    {
        ChangedAssignmentCount++;
    }
}

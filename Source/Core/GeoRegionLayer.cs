namespace Cultiway.Core;

/// <summary>
/// GeoRegion 的大类分层。
/// </summary>
public enum GeoRegionLayer
{
    /// <summary>
    /// 主层：默认展示层，主要用于地表主分类（群系/水域/特殊地块）。
    /// </summary>
    Primary,
    /// <summary>
    /// 地貌层：平原、山地、峡谷、盆地等。
    /// </summary>
    Landform,
    /// <summary>
    /// 陆块层：大陆/岛屿等连通陆地分量。
    /// </summary>
    Landmass,
    /// <summary>
    /// 半岛层：细长伸向水域的陆地形态。
    /// </summary>
    Peninsula,
    /// <summary>
    /// 海峡层：狭长水道形态。
    /// </summary>
    Strait,
    /// <summary>
    /// 群岛层：由多个小岛聚类形成，可非连通。
    /// </summary>
    Archipelago
}

namespace Cultiway.Core;

/// <summary>
/// 地理地区的分类层级。同一地块可在每一层分别归属一个地区，查询时用层级区分用途。
/// </summary>
public enum GeoRegionLayer
{
    /// <summary>
    /// 主层：默认展示层，主要区分生物群系、水域和特殊地块。
    /// </summary>
    Primary,
    /// <summary>
    /// 地貌层：区分平原、山地、峡谷、盆地等地形。
    /// </summary>
    Landform,
    /// <summary>
    /// 陆块层：把互相连通的陆地划分为岛、洲或大陆。
    /// </summary>
    Landmass,
    /// <summary>
    /// 半岛层：标记细长伸向水域的陆地。
    /// </summary>
    Peninsula,
    /// <summary>
    /// 海峡层：标记连接水域的狭长水道。
    /// </summary>
    Strait,
    /// <summary>
    /// 群岛层：把距离较近的多个小岛归为一组，地块之间可以不相连。
    /// </summary>
    Archipelago
}

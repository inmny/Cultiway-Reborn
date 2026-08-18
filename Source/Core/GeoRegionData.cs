namespace Cultiway.Core;

/// <summary>
/// 游戏里一个地区当前使用的数据，记录显示样式、地图位置、分类结果和地区组成统计。
/// 地区重新划分后会更新由地块计算出的字段，界面展示时直接读取这些结果。
/// </summary>
public class GeoRegionData : MetaObjectData
{
    /// <summary>旗帜背景在图片库中的编号。</summary>
    public int BannerBackgroundIndex;
    /// <summary>旗帜图案在图片库中的编号。</summary>
    public int BannerIconIndex;

    /// <summary>地区所在的地图分类层；同一地块可在不同层各属于一个地区。</summary>
    public GeoRegionLayer Layer;
    /// <summary>地区分类配置的编号，用于取得名称、图标和识别规则。</summary>
    public string CategoryId;
    /// <summary>地区中心地块的横向坐标。</summary>
    public int CenterX;
    /// <summary>地区中心地块的纵向坐标。</summary>
    public int CenterY;
    /// <summary>地区包含的全部地块数。</summary>
    public int TileCount;
    /// <summary>地区中决定其主要类别的核心地块数。</summary>
    public int CoreTileCount;
    /// <summary>地区是否由多种成分混合组成。</summary>
    public bool IsMixed;
    /// <summary>是否因周围没有足够大的同类地区，只能保留为一个低于通常面积要求的小地区。</summary>
    public bool TopologyExempt;
    /// <summary>地区中占比最高的主要地表类别编号。</summary>
    public int DominantPrimaryCode;
    /// <summary>地区中占比最高的地貌类别编号。</summary>
    public int DominantLandformCode;
    /// <summary>核心部分中数量最多的生物群系编号。</summary>
    public string CoreBiomeId;
    /// <summary>整个地区中数量最多的生物群系编号。</summary>
    public string DominantBiomeId;
    /// <summary>地区内参与统计的生物群系种类数。</summary>
    public int BiomeCompositionCount;
    /// <summary>地区合并前包含的原始地表种类数。</summary>
    public int RawCompositionCount;
    /// <summary>主要成分所占比例；越接近 1，地区组成越单一。</summary>
    public float Purity;
}

using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 一种地理地区分类的配置，保存所属层、识别条件、显示名称、图标和形状限制。
/// 世界生成时用这些条件划分地区，界面展示时读取名称和图标。
/// </summary>
public class GeoRegionAsset : Asset
{
    /// <summary>分类未提供图标或图标加载失败时使用的默认图片路径。</summary>
    public const string DefaultIconPath = "cultiway/icons/iconGeoRegion";

    /// <summary>
    /// 该分类所属的地图层，决定它与哪些地区规则一起使用。
    /// </summary>
    public GeoRegionLayer Layer;
    /// <summary>
    /// 同层识别顺序，值越大越先尝试此分类。
    /// </summary>
    public int Priority;
    /// <summary>
    /// 地区允许的最少地块数，常用于过滤零散小区域。
    /// </summary>
    public int MinTiles;
    /// <summary>
    /// 地区允许的最多地块数；为 0 时通常表示不限制。
    /// </summary>
    public int MaxTiles;

    /// <summary>
    /// 分类的默认显示名称，本地化文本不存在时使用。
    /// </summary>
    public string DisplayName;
    /// <summary>
    /// 分类图标的资源路径，不带 png 后缀。
    /// </summary>
    public string IconPath;

    /// <summary>加载分类图标；路径为空或资源不存在时返回通用地区图标。</summary>
    public Sprite GetSpriteIcon()
    {
        var sprite = string.IsNullOrEmpty(IconPath) ? null : SpriteTextureLoader.getSprite(IconPath);
        return sprite != null ? sprite : SpriteTextureLoader.getSprite(DefaultIconPath);
    }

    /// <summary>取得本地化后的分类名称；没有对应翻译时使用配置中的默认名称。</summary>
    public string GetDisplayName()
    {
        return LMTools.GetOrFallback(id, DisplayName);
    }

    /// <summary>
    /// 允许的生物群系编号列表；为空表示不限制。
    /// </summary>
    public string[] BiomeIds;
    /// <summary>
    /// 允许的地块类型编号列表；为空表示不限制。
    /// </summary>
    public string[] TileTypeIds;
    /// <summary>
    /// 允许的地块层类型列表；为空表示不限制。
    /// </summary>
    public TileLayerType[] LayerTypes;

    /// <summary>
    /// 是否要求地块带有海洋标记；为空表示不检查。
    /// </summary>
    public bool? RequireOceanFlag;
    /// <summary>
    /// 是否要求地块可被海水填充；为空表示不检查。
    /// </summary>
    public bool? RequireFillableWaterFlag;
    /// <summary>
    /// 是否要求地块带有熔岩标记；为空表示不检查。
    /// </summary>
    public bool? RequireLavaFlag;
    /// <summary>
    /// 是否要求地块带有菌泥标记；为空表示不检查。
    /// </summary>
    public bool? RequireGooFlag;
    /// <summary>
    /// 是否要求地块带有山体标记；为空表示不检查。
    /// </summary>
    public bool? RequireMountainFlag;

    /// <summary>
    /// 上下左右相邻格中要求的最少水体数量，常用于识别贴水的海滩。
    /// </summary>
    public int MinNeighborWater;
    /// <summary>
    /// 到最近水体的最大地块距离，用于控制海滩宽度；-1 表示不限制。
    /// </summary>
    public int MaxDistanceToWater = -1;
    /// <summary>
    /// 上下左右相邻格中要求的最少阻挡地块数量。
    /// </summary>
    public int MinNeighborBlock;
    /// <summary>
    /// 上下左右相邻格中要求的最少坑地数量。
    /// </summary>
    public int MinNeighborPit;
    /// <summary>
    /// 是否要求左右或上下两侧同时存在山体，用于识别夹在山间的地形。
    /// </summary>
    public bool RequireOppositeBlockPair;

    /// <summary>
    /// 半岛允许的最大地块厚度。
    /// </summary>
    public int MaxThickness;
    /// <summary>
    /// 半岛最小海岸占比。
    /// </summary>
    public float MinCoastRatio;
    /// <summary>
    /// 半岛最大颈部占比。
    /// </summary>
    public float MaxNeckRatio;

    /// <summary>
    /// 海峡半宽上限。
    /// </summary>
    public int MaxHalfWidth;
    /// <summary>
    /// 海峡最小出口数。
    /// </summary>
    public int MinExits;
    /// <summary>
    /// 海峡最小外接矩形长宽比，用于要求整体形状足够狭长。
    /// </summary>
    public float MinAspectRatio;

    /// <summary>
    /// 组成群岛的单个岛屿最多可含多少地块。
    /// </summary>
    public int IslandMaxTiles;
    /// <summary>
    /// 两座岛仍可归入同一群岛的最大间隔。
    /// </summary>
    public int MaxGap;
    /// <summary>
    /// 群岛最小岛屿数。
    /// </summary>
    public int MinIslands;
    /// <summary>
    /// 群岛所有岛屿合计至少需要多少地块。
    /// </summary>
    public int MinTotalTiles;
}

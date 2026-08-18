using System;
using Cultiway.Core;

namespace Cultiway.Core.Libraries;

/// <summary>
/// 集中保存游戏内置的全部地理地区分类配置。世界划分地区时按属性引用这些固定分类，
/// 地区详情和地图图例也从同一处取得名称与图标，避免各系统重复创建配置。
/// </summary>
public class GeoRegionLibrary : AssetLibrary<GeoRegionAsset>
{
    /// <summary>
    /// 主要分类 - 海洋，接触地图边缘的大面积水体
    /// </summary>
    public GeoRegionAsset PrimarySea { get; private set; }
    /// <summary>
    /// 主要分类 - 湖泊，被陆地包围的内陆水体
    /// </summary>
    public GeoRegionAsset PrimaryLake { get; private set; }
    /// <summary>
    /// 主要分类 - 河流，狭长的流动水体
    /// </summary>
    public GeoRegionAsset PrimaryRiver { get; private set; }
    /// <summary>
    /// 主要分类 - 熔岩，岩浆地块
    /// </summary>
    public GeoRegionAsset PrimaryLava { get; private set; }
    /// <summary>
    /// 主要分类 - 菌泥，腐蚀性物质地块
    /// </summary>
    public GeoRegionAsset PrimaryGoo { get; private set; }
    /// <summary>
    /// 主要分类 - 山脉，不可通行的岩石地块
    /// </summary>
    public GeoRegionAsset PrimaryMountains { get; private set; }

    /// <summary>
    /// 主要分类 - 草原，温带草地生物群系
    /// </summary>
    public GeoRegionAsset PrimaryGrassland { get; private set; }
    /// <summary>
    /// 主要分类 - 森林，温带森林生物群系
    /// </summary>
    public GeoRegionAsset PrimaryForest { get; private set; }
    /// <summary>
    /// 主要分类 - 丛林，热带雨林生物群系
    /// </summary>
    public GeoRegionAsset PrimaryJungle { get; private set; }
    /// <summary>
    /// 主要分类 - 沼泽，湿地生物群系
    /// </summary>
    public GeoRegionAsset PrimarySwamp { get; private set; }
    /// <summary>
    /// 主要分类 - 沙漠，干旱沙漠生物群系
    /// </summary>
    public GeoRegionAsset PrimaryDesert { get; private set; }
    /// <summary>
    /// 主要分类 - 海滩，邻近水体的沙滩地块
    /// </summary>
    public GeoRegionAsset PrimaryBeach { get; private set; }
    /// <summary>
    /// 主要分类 - 冻原，寒冷苔原生物群系
    /// </summary>
    public GeoRegionAsset PrimaryTundra { get; private set; }
    /// <summary>
    /// 主要分类 - 高地，海拔较高的高原地区
    /// </summary>
    public GeoRegionAsset PrimaryHighlands { get; private set; }
    /// <summary>
    /// 主要分类 - 荒原，被腐蚀或破坏的荒芜地区
    /// </summary>
    public GeoRegionAsset PrimaryWasteland { get; private set; }
    /// <summary>
    /// 主要分类 - 特殊，无法归入其他分类的特殊地块
    /// </summary>
    public GeoRegionAsset PrimarySpecial { get; private set; }

    /// <summary>
    /// 地貌分类 - 平原，平坦开阔的地形
    /// </summary>
    public GeoRegionAsset LandformPlain { get; private set; }
    /// <summary>
    /// 地貌分类 - 山地，隆起的山地地形
    /// </summary>
    public GeoRegionAsset LandformMountain { get; private set; }
    /// <summary>
    /// 地貌分类 - 峡谷，两侧高中间低的狭长地形
    /// </summary>
    public GeoRegionAsset LandformCanyon { get; private set; }
    /// <summary>
    /// 地貌分类 - 盆地，四周高中间低的凹陷地形
    /// </summary>
    public GeoRegionAsset LandformBasin { get; private set; }

    /// <summary>
    /// 陆块分类 - 岛，面积为 21～3000 格的连通陆地。
    /// </summary>
    public GeoRegionAsset LandmassIsland { get; private set; }
    /// <summary>
    /// 陆块分类 - 洲，面积为 3001～10000 格的连通陆地。
    /// </summary>
    public GeoRegionAsset LandmassContinent { get; private set; }
    /// <summary>
    /// 陆块分类 - 大陆，面积至少为 10001 格的连通陆地。
    /// </summary>
    public GeoRegionAsset LandmassMainland { get; private set; }

    /// <summary>
    /// 形态分类 - 半岛，三面环水的狭长陆地
    /// </summary>
    public GeoRegionAsset Peninsula { get; private set; }
    /// <summary>
    /// 形态分类 - 海峡，连接两片水域的狭窄通道
    /// </summary>
    public GeoRegionAsset Strait { get; private set; }
    /// <summary>
    /// 形态分类 - 群岛，密集分布的岛屿群
    /// </summary>
    public GeoRegionAsset Archipelago { get; private set; }

    /// <summary>初始化分类库，并依次登记主要地表、地貌、陆块和特殊形态分类。</summary>
    public override void init()
    {
        base.init();
        InitPrimary();
        InitLandform();
        InitLandmass();
        InitMorphology();
    }

    /// <summary>登记分类配置；未指定图标路径时先按分类编号生成约定路径。</summary>
    public override GeoRegionAsset add(GeoRegionAsset pAsset)
    {
        if (string.IsNullOrEmpty(pAsset.IconPath))
        {
            pAsset.IconPath = GetDefaultIconPath(pAsset);
        }

        return base.add(pAsset);
    }

    /// <summary>
    /// 把分类编号转换为默认图标资源路径，例如去掉公共前缀并把分隔点改为下划线。
    /// </summary>
    private static string GetDefaultIconPath(GeoRegionAsset asset)
    {
        const string prefix = "Cultiway.GeoRegion.";
        var iconId = asset?.id ?? string.Empty;
        if (iconId.StartsWith(prefix, StringComparison.Ordinal))
        {
            iconId = iconId.Substring(prefix.Length);
        }

        if (string.IsNullOrEmpty(iconId))
        {
            iconId = "unknown";
        }

        return "cultiway/icons/geo_regions/" + iconId.Replace('.', '_').ToLowerInvariant();
    }

    /// <summary>登记水域、特殊地块和各类生物群系使用的默认主层分类。</summary>
    private void InitPrimary()
    {
        PrimarySea = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Sea",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "海",
            MinTiles = 32
        });
        PrimaryLake = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Lake",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "湖",
            MinTiles = 32
        });
        PrimaryRiver = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.River",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "河",
            MinTiles = 16,
            MaxTiles = 2048,
            MinAspectRatio = 3.0f
        });
        PrimaryLava = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Lava",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "熔岩地带",
            MinTiles = 32
        });
        PrimaryGoo = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Goo",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "灰疫之地",
            MinTiles = 32
        });
        PrimaryMountains = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Mountains",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "山脉",
            MinTiles = 64
        });

        PrimaryGrassland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Grassland",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "草原",
            MinTiles = 64,
            BiomeIds = new[] { "biome_grass", "biome_savanna", "biome_clover", "biome_flower" }
        });
        PrimaryForest = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Forest",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "森林",
            MinTiles = 64,
            BiomeIds = new[] { "biome_birch", "biome_maple" }
        });
        PrimaryJungle = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Jungle",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "丛林",
            MinTiles = 64,
            BiomeIds = new[] { "biome_jungle" }
        });
        PrimarySwamp = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Swamp",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "沼泽",
            MinTiles = 64,
            BiomeIds = new[] { "biome_swamp" }
        });
        PrimaryDesert = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Desert",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "沙漠",
            MinTiles = 64,
            BiomeIds = new[] { "biome_desert", "biome_sand" }
        });
        PrimaryBeach = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Beach",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "海滩",
            MinTiles = 32,
            BiomeIds = new[] { "biome_sand" },
            TileTypeIds = new[] { "sand", "snow_sand" },
            MinNeighborWater = 0,
            MaxDistanceToWater = 6
        });
        PrimaryTundra = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Tundra",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "雪原",
            MinTiles = 64,
            BiomeIds = new[] { "biome_permafrost" }
        });
        PrimaryHighlands = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Highlands",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "高地",
            MinTiles = 64,
            BiomeIds = new[] { "biome_hill", "biome_rocklands" }
        });
        PrimaryWasteland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Wasteland",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "荒原",
            MinTiles = 64,
            BiomeIds = new[] { "biome_wasteland" }
        });
        PrimarySpecial = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Special",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "奇境",
            MinTiles = 64
        });
    }

    /// <summary>登记平原、山地、峡谷和盆地等地貌分类及其识别限制。</summary>
    private void InitLandform()
    {
        LandformPlain = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Plain",
            Layer = GeoRegionLayer.Landform,
            Priority = 0,
            DisplayName = "平原",
            MinTiles = 128
        });
        LandformMountain = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Mountain",
            Layer = GeoRegionLayer.Landform,
            Priority = 300,
            DisplayName = "山地",
            MinTiles = 128,
            RequireMountainFlag = true
        });
        LandformCanyon = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Canyon",
            Layer = GeoRegionLayer.Landform,
            Priority = 260,
            DisplayName = "峡谷",
            MinTiles = 64,
            RequireOceanFlag = false,
            RequireFillableWaterFlag = false,
            MinNeighborBlock = 2,
            RequireOppositeBlockPair = true
        });
        LandformBasin = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Basin",
            Layer = GeoRegionLayer.Landform,
            Priority = 200,
            DisplayName = "盆地",
            MinTiles = 64,
            RequireFillableWaterFlag = true,
            RequireOceanFlag = false
        });
    }

    /// <summary>按连通陆地面积范围登记岛、洲和大陆分类。</summary>
    private void InitLandmass()
    {
        LandmassIsland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landmass.Island",
            Layer = GeoRegionLayer.Landmass,
            DisplayName = "岛",
            MinTiles = 21,
            MaxTiles = 3000
        });
        LandmassContinent = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landmass.Continent",
            Layer = GeoRegionLayer.Landmass,
            DisplayName = "洲",
            MinTiles = 3001,
            MaxTiles = 10000
        });
        LandmassMainland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landmass.Mainland",
            Layer = GeoRegionLayer.Landmass,
            DisplayName = "大陆",
            MinTiles = 10001
        });
    }

    /// <summary>登记半岛、海峡和群岛分类，并设置各自的形状与规模条件。</summary>
    private void InitMorphology()
    {
        Peninsula = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Morphology.Peninsula",
            Layer = GeoRegionLayer.Peninsula,
            DisplayName = "半岛",
            MinTiles = 128,
            MaxTiles = 8192,
            MaxThickness = 2,
            MinCoastRatio = 0.40f,
            MaxNeckRatio = 0.05f
        });
        Strait = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Morphology.Strait",
            Layer = GeoRegionLayer.Strait,
            DisplayName = "海峡",
            MinTiles = 24,
            MaxTiles = 4096,
            MaxHalfWidth = 1,
            MinExits = 2,
            MinAspectRatio = 2.0f
        });
        Archipelago = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Morphology.Archipelago",
            Layer = GeoRegionLayer.Archipelago,
            DisplayName = "群岛",
            MinIslands = 3,
            MinTotalTiles = 512,
            IslandMaxTiles = 2048,
            MaxGap = 8
        });
    }
}

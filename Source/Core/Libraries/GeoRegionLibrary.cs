using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;

namespace Cultiway.Core.Libraries;

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
    /// 陆块分类 - 岛屿，不接触地图边缘的独立陆块
    /// </summary>
    public GeoRegionAsset LandmassIsland { get; private set; }
    /// <summary>
    /// 陆块分类 - 大陆，接触地图边缘的主要陆块
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

    /// <summary>
    /// 生物群系ID到主要分类资产的映射字典
    /// </summary>
    private readonly Dictionary<string, GeoRegionAsset> _biomeIdToPrimaryClass = new();
    /// <summary>
    /// 地貌规则资产数组，用于按优先级匹配地貌分类
    /// </summary>
    private GeoRegionAsset[] _landformRules;

    public override void init()
    {
        base.init();
        InitPrimary();
        InitLandform();
        InitLandmass();
        InitMorphology();
        BuildBiomeMapping();
    }

    public bool TryGetPrimaryClassByBiome(string biomeId, out GeoRegionAsset category)
    {
        if (string.IsNullOrEmpty(biomeId))
        {
            category = PrimarySpecial;
            return false;
        }
        return _biomeIdToPrimaryClass.TryGetValue(biomeId, out category);
    }

    /// <summary>
    /// 根据 Primary 层水体细分类获取对应资产。
    /// </summary>
    public GeoRegionAsset ResolvePrimaryWater(PrimaryWaterKind waterKind)
    {
        return waterKind switch
        {
            PrimaryWaterKind.Sea => PrimarySea,
            PrimaryWaterKind.River => PrimaryRiver,
            PrimaryWaterKind.Lake => PrimaryLake,
            _ => PrimaryLake
        };
    }

    /// <summary>
    /// 根据水域连通块形态判定 Sea/Lake/River。
    /// </summary>
    public PrimaryWaterKind ResolvePrimaryWaterKind(bool touchesEdge, int tileCount, int bboxWidth, int bboxHeight)
    {
        if (touchesEdge) return PrimaryWaterKind.Sea;

        var river = PrimaryRiver;
        if (river != null)
        {
            var minTiles = Math.Max(1, river.MinTiles);
            var maxTiles = river.MaxTiles;
            var minAspectRatio = river.MinAspectRatio > 0f ? river.MinAspectRatio : 3f;
            var aspectRatio = Math.Max(bboxWidth, bboxHeight) / (float)Math.Max(1, Math.Min(bboxWidth, bboxHeight));

            if (tileCount >= minTiles &&
                (maxTiles <= 0 || tileCount <= maxTiles) &&
                aspectRatio >= minAspectRatio)
            {
                return PrimaryWaterKind.River;
            }
        }

        return PrimaryWaterKind.Lake;
    }

    public GeoRegionAsset ResolvePrimaryLandByBiome(string biomeId)
    {
        return _biomeIdToPrimaryClass.TryGetValue(biomeId ?? string.Empty, out var category) ? category : PrimarySpecial;
    }

    /// <summary>
    /// 基于 tile type / biome / 邻接统计解析 Primary 地表分类（包含海滩覆盖规则）。
    /// </summary>
    public GeoRegionAsset ResolvePrimaryLandByContext(in GeoRegionTileRuleContext context)
    {
        if (PrimaryBeach != null && MatchPrimaryBeachRule(PrimaryBeach, context))
        {
            return PrimaryBeach;
        }

        return ResolvePrimaryLandByBiome(context.BiomeId);
    }

    /// <summary>
    /// 依据 tile 层级与标记解析 Primary 特殊地块分类。
    /// </summary>
    public GeoRegionAsset ResolvePrimarySpecial(TileLayerType layerType, bool isLavaFlag = false, bool isGooFlag = false, bool isMountainFlag = false)
    {
        if (layerType == TileLayerType.Lava || isLavaFlag)
        {
            return PrimaryLava;
        }

        if (layerType == TileLayerType.Goo || isGooFlag)
        {
            return PrimaryGoo;
        }

        if (layerType == TileLayerType.Block || isMountainFlag)
        {
            return PrimaryMountains;
        }

        return PrimarySpecial;
    }

    /// <summary>
    /// 基于 tile type / biome / 邻接统计解析地貌分类。
    /// </summary>
    public GeoRegionAsset ResolveLandform(in GeoRegionTileRuleContext context)
    {
        foreach (var rule in _landformRules)
        {
            if (rule == null) continue;
            if (MatchLandformRule(rule, context))
            {
                return rule;
            }
        }
        return LandformPlain;
    }

    public GeoRegionAsset ResolveLandmass(bool touchesEdge)
    {
        return touchesEdge ? LandmassMainland : LandmassIsland;
    }

    private static bool MatchLandformRule(GeoRegionAsset rule, in GeoRegionTileRuleContext context)
    {
        if (!MatchString(rule.TileTypeIds, context.TileTypeId))
        {
            return false;
        }

        if (!MatchLayer(rule.LayerTypes, context.LayerType))
        {
            return false;
        }

        if (!MatchString(rule.BiomeIds, context.BiomeId))
        {
            return false;
        }

        if (rule.RequireOceanFlag.HasValue && context.IsOceanFlag != rule.RequireOceanFlag.Value)
        {
            return false;
        }

        if (rule.RequireFillableWaterFlag.HasValue && context.IsFillableWaterFlag != rule.RequireFillableWaterFlag.Value)
        {
            return false;
        }

        if (rule.RequireLavaFlag.HasValue && context.IsLavaFlag != rule.RequireLavaFlag.Value)
        {
            return false;
        }

        if (rule.RequireGooFlag.HasValue && context.IsGooFlag != rule.RequireGooFlag.Value)
        {
            return false;
        }

        if (rule.RequireMountainFlag.HasValue && context.IsMountainFlag != rule.RequireMountainFlag.Value)
        {
            return false;
        }

        if (rule.MinNeighborWater > 0 && context.NeighborWaterCount < rule.MinNeighborWater)
        {
            return false;
        }

        if (rule.MinNeighborBlock > 0 && context.NeighborBlockCount < rule.MinNeighborBlock)
        {
            return false;
        }

        if (rule.MinNeighborPit > 0 && context.NeighborPitCount < rule.MinNeighborPit)
        {
            return false;
        }

        if (rule.RequireOppositeBlockPair && !context.HasOppositeBlockPair)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 海滩规则：支持“贴海宽度 + 对角邻水补偿”，避免海岸线漏判与窄判。
    /// </summary>
    private static bool MatchPrimaryBeachRule(GeoRegionAsset rule, in GeoRegionTileRuleContext context)
    {
        if (!MatchLayer(rule.LayerTypes, context.LayerType))
        {
            return false;
        }

        var hasBiomeRestriction = rule.BiomeIds != null && rule.BiomeIds.Length > 0;
        var hasTileTypeRestriction = rule.TileTypeIds != null && rule.TileTypeIds.Length > 0;
        if (hasBiomeRestriction || hasTileTypeRestriction)
        {
            var matchBiome = hasBiomeRestriction && MatchString(rule.BiomeIds, context.BiomeId);
            var matchTileType = hasTileTypeRestriction && MatchString(rule.TileTypeIds, context.TileTypeId);
            if (!matchBiome && !matchTileType)
            {
                return false;
            }
        }

        if (rule.RequireOceanFlag.HasValue && context.IsOceanFlag != rule.RequireOceanFlag.Value)
        {
            return false;
        }

        if (rule.RequireFillableWaterFlag.HasValue && context.IsFillableWaterFlag != rule.RequireFillableWaterFlag.Value)
        {
            return false;
        }

        if (rule.RequireLavaFlag.HasValue && context.IsLavaFlag != rule.RequireLavaFlag.Value)
        {
            return false;
        }

        if (rule.RequireGooFlag.HasValue && context.IsGooFlag != rule.RequireGooFlag.Value)
        {
            return false;
        }

        if (rule.RequireMountainFlag.HasValue && context.IsMountainFlag != rule.RequireMountainFlag.Value)
        {
            return false;
        }

        if (rule.MaxDistanceToWater >= 0)
        {
            if (context.DistanceToWater < 0 || context.DistanceToWater > rule.MaxDistanceToWater)
            {
                return false;
            }
        }

        if (rule.MinNeighborWater > 0)
        {
            var orthogonalWater = context.NeighborWaterCount;
            if (orthogonalWater < rule.MinNeighborWater)
            {
                var diagonalWater = Math.Max(0, context.NeighborWater8Count - orthogonalWater);
                var compensatedWater = orthogonalWater + (diagonalWater >= 2 ? 1 : 0);
                if (compensatedWater < rule.MinNeighborWater)
                {
                    return false;
                }
            }
        }

        if (rule.MinNeighborBlock > 0 && context.NeighborBlockCount < rule.MinNeighborBlock)
        {
            return false;
        }

        if (rule.MinNeighborPit > 0 && context.NeighborPitCount < rule.MinNeighborPit)
        {
            return false;
        }

        if (rule.RequireOppositeBlockPair && !context.HasOppositeBlockPair)
        {
            return false;
        }

        return true;
    }

    private static bool MatchString(string[] candidates, string value)
    {
        if (candidates == null || candidates.Length == 0) return true;
        if (string.IsNullOrEmpty(value)) return false;

        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate)) continue;
            if (string.Equals(candidate, value, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchLayer(TileLayerType[] candidates, TileLayerType value)
    {
        if (candidates == null || candidates.Length == 0) return true;
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] == value) return true;
        }
        return false;
    }

    private void InitPrimary()
    {
        PrimarySea = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Sea",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "海",
            Naming = new GeoRegionNamingRule { Template = "{Dir}海" },
            MinTiles = 32
        });
        PrimaryLake = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Lake",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "湖",
            Naming = new GeoRegionNamingRule { Template = "{Dir}湖" },
            MinTiles = 32
        });
        PrimaryRiver = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.River",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "河",
            Naming = new GeoRegionNamingRule { Template = "{Dir}河" },
            MinTiles = 16,
            MaxTiles = 2048,
            MinAspectRatio = 3.0f
        });
        PrimaryLava = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Lava",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "熔岩地带",
            Naming = new GeoRegionNamingRule { Template = "{Dir}熔岩地带" },
            MinTiles = 32
        });
        PrimaryGoo = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Goo",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "灰疫之地",
            Naming = new GeoRegionNamingRule { Template = "{Dir}灰疫之地" },
            MinTiles = 32
        });
        PrimaryMountains = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Mountains",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "山地",
            Naming = new GeoRegionNamingRule { Template = "{Dir}山地" },
            MinTiles = 64
        });

        PrimaryGrassland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Grassland",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "草原",
            Naming = new GeoRegionNamingRule { Template = "{Dir}草原" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_grass", "biome_savanna", "biome_clover", "biome_flower" }
        });
        PrimaryForest = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Forest",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "森林",
            Naming = new GeoRegionNamingRule { Template = "{Dir}森林" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_birch", "biome_maple" }
        });
        PrimaryJungle = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Jungle",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "丛林",
            Naming = new GeoRegionNamingRule { Template = "{Dir}丛林" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_jungle" }
        });
        PrimarySwamp = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Swamp",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "沼泽",
            Naming = new GeoRegionNamingRule { Template = "{Dir}沼泽" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_swamp" }
        });
        PrimaryDesert = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Desert",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "沙漠",
            Naming = new GeoRegionNamingRule { Template = "{Dir}沙漠" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_desert", "biome_sand" }
        });
        PrimaryBeach = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Beach",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "海滩",
            Naming = new GeoRegionNamingRule { Template = "{Dir}海滩" },
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
            Naming = new GeoRegionNamingRule { Template = "{Dir}雪原" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_permafrost" }
        });
        PrimaryHighlands = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Highlands",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "高地",
            Naming = new GeoRegionNamingRule { Template = "{Dir}高地" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_hill", "biome_rocklands" }
        });
        PrimaryWasteland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Wasteland",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "荒原",
            Naming = new GeoRegionNamingRule { Template = "{Dir}荒原" },
            MinTiles = 64,
            BiomeIds = new[] { "biome_wasteland" }
        });
        PrimarySpecial = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Primary.Special",
            Layer = GeoRegionLayer.Primary,
            DisplayName = "奇境",
            Naming = new GeoRegionNamingRule { Template = "{Dir}奇境" },
            MinTiles = 64
        });
    }

    private void InitLandform()
    {
        LandformPlain = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Plain",
            Layer = GeoRegionLayer.Landform,
            Priority = 0,
            DisplayName = "平原",
            Naming = new GeoRegionNamingRule { Template = "{Dir}平原" },
            MinTiles = 128
        });
        LandformMountain = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Mountain",
            Layer = GeoRegionLayer.Landform,
            Priority = 300,
            DisplayName = "山地",
            Naming = new GeoRegionNamingRule { Template = "{Dir}山地" },
            MinTiles = 128,
            RequireMountainFlag = true
        });
        LandformCanyon = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Canyon",
            Layer = GeoRegionLayer.Landform,
            Priority = 260,
            DisplayName = "峡谷",
            Naming = new GeoRegionNamingRule { Template = "{Dir}峡谷" },
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
            Naming = new GeoRegionNamingRule { Template = "{Dir}盆地" },
            MinTiles = 64,
            RequireFillableWaterFlag = true,
            RequireOceanFlag = false
        });

        _landformRules = list.Where(a => a.Layer == GeoRegionLayer.Landform)
            .OrderByDescending(a => a.Priority)
            .ToArray();
    }

    private void InitLandmass()
    {
        LandmassIsland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landmass.Island",
            Layer = GeoRegionLayer.Landmass,
            DisplayName = "岛",
            Naming = new GeoRegionNamingRule { Template = "{Dir}{Biome}岛" },
            MinTiles = 128
        });
        LandmassMainland = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landmass.Mainland",
            Layer = GeoRegionLayer.Landmass,
            DisplayName = "大陆",
            Naming = new GeoRegionNamingRule { Template = "{Dir}{Biome}大陆" },
            MinTiles = 512
        });
    }

    private void InitMorphology()
    {
        Peninsula = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Morphology.Peninsula",
            Layer = GeoRegionLayer.Peninsula,
            DisplayName = "半岛",
            Naming = new GeoRegionNamingRule { Template = "{Dir}{Biome}半岛" },
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
            Naming = new GeoRegionNamingRule { Template = "{Dir}海峡" },
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
            Naming = new GeoRegionNamingRule { Template = "{Dir}{Biome}群岛" },
            MinIslands = 3,
            MinTotalTiles = 512,
            IslandMaxTiles = 2048,
            MaxGap = 8
        });
    }

    private void BuildBiomeMapping()
    {
        _biomeIdToPrimaryClass.Clear();
        var primaryClasses = new List<GeoRegionAsset>
        {
            PrimaryGrassland,
            PrimaryForest,
            PrimaryJungle,
            PrimarySwamp,
            PrimaryDesert,
            PrimaryTundra,
            PrimaryHighlands,
            PrimaryWasteland
        };

        foreach (var category in primaryClasses)
        {
            if (category?.BiomeIds == null) continue;
            foreach (var biomeId in category.BiomeIds)
            {
                if (string.IsNullOrEmpty(biomeId)) continue;
                _biomeIdToPrimaryClass[biomeId] = category;
            }
        }

        foreach (var biome in AssetManager.biome_library.list)
        {
            if (biome == null || string.IsNullOrEmpty(biome.id)) continue;
            if (_biomeIdToPrimaryClass.ContainsKey(biome.id)) continue;
            _biomeIdToPrimaryClass[biome.id] = PrimarySpecial;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;

namespace Cultiway.Core.Libraries;

public class GeoRegionLibrary : AssetLibrary<GeoRegionAsset>
{
    public GeoRegionAsset PrimarySea { get; private set; }
    public GeoRegionAsset PrimaryLake { get; private set; }
    public GeoRegionAsset PrimaryLava { get; private set; }
    public GeoRegionAsset PrimaryGoo { get; private set; }
    public GeoRegionAsset PrimaryMountains { get; private set; }

    public GeoRegionAsset PrimaryGrassland { get; private set; }
    public GeoRegionAsset PrimaryForest { get; private set; }
    public GeoRegionAsset PrimaryJungle { get; private set; }
    public GeoRegionAsset PrimarySwamp { get; private set; }
    public GeoRegionAsset PrimaryDesert { get; private set; }
    public GeoRegionAsset PrimaryTundra { get; private set; }
    public GeoRegionAsset PrimaryHighlands { get; private set; }
    public GeoRegionAsset PrimaryWasteland { get; private set; }
    public GeoRegionAsset PrimarySpecial { get; private set; }

    public GeoRegionAsset LandformPlain { get; private set; }
    public GeoRegionAsset LandformMountain { get; private set; }
    public GeoRegionAsset LandformCanyon { get; private set; }
    public GeoRegionAsset LandformBasin { get; private set; }

    public GeoRegionAsset LandmassIsland { get; private set; }
    public GeoRegionAsset LandmassMainland { get; private set; }

    public GeoRegionAsset Peninsula { get; private set; }
    public GeoRegionAsset Strait { get; private set; }
    public GeoRegionAsset Archipelago { get; private set; }

    private readonly Dictionary<string, GeoRegionAsset> _biomeIdToPrimaryClass = new();
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

    public GeoRegionAsset ResolvePrimaryWater(bool touchesEdge)
    {
        return touchesEdge ? PrimarySea : PrimaryLake;
    }

    public GeoRegionAsset ResolvePrimaryLandByBiome(string biomeId)
    {
        return _biomeIdToPrimaryClass.TryGetValue(biomeId ?? string.Empty, out var category) ? category : PrimarySpecial;
    }

    public GeoRegionAsset ResolvePrimarySpecial(TileLayerType layerType)
    {
        return layerType switch
        {
            TileLayerType.Lava => PrimaryLava,
            TileLayerType.Goo => PrimaryGoo,
            TileLayerType.Block => PrimaryMountains,
            _ => PrimarySpecial
        };
    }

    public GeoRegionAsset ResolveLandform(int height, int slope, int delta)
    {
        foreach (var rule in _landformRules)
        {
            if (rule == null) continue;
            if (MatchLandformRule(rule, height, slope, delta))
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

    private static bool MatchLandformRule(GeoRegionAsset rule, int height, int slope, int delta)
    {
        bool heightOk = rule.MinHeight < 0 || height >= rule.MinHeight;
        heightOk &= rule.MaxHeight < 0 || height <= rule.MaxHeight;

        bool slopeOk = rule.MinSlope < 0 || slope >= rule.MinSlope;
        slopeOk &= rule.MaxSlope < 0 || slope <= rule.MaxSlope;

        bool deltaOk = rule.MinDelta < 0 || delta >= rule.MinDelta;
        deltaOk &= rule.MaxDelta < 0 || delta <= rule.MaxDelta;

        if (rule.HeightOrSlope && rule.MinHeight >= 0 && rule.MinSlope >= 0)
        {
            return (heightOk || slopeOk) && deltaOk;
        }

        return heightOk && slopeOk && deltaOk;
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
            Priority = 200,
            DisplayName = "山地",
            Naming = new GeoRegionNamingRule { Template = "{Dir}山地" },
            MinTiles = 128,
            MinHeight = 170,
            MinSlope = 18,
            HeightOrSlope = true
        });
        LandformCanyon = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Canyon",
            Layer = GeoRegionLayer.Landform,
            Priority = 300,
            DisplayName = "峡谷",
            Naming = new GeoRegionNamingRule { Template = "{Dir}峡谷" },
            MinTiles = 64,
            MaxDelta = -12,
            MinSlope = 20
        });
        LandformBasin = add(new GeoRegionAsset
        {
            id = "Cultiway.GeoRegion.Landform.Basin",
            Layer = GeoRegionLayer.Landform,
            Priority = 100,
            DisplayName = "盆地",
            Naming = new GeoRegionNamingRule { Template = "{Dir}盆地" },
            MinTiles = 64,
            MaxDelta = -8,
            MaxSlope = 12
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

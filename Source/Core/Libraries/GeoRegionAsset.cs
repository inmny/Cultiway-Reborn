using Cultiway.Core;

namespace Cultiway.Core.Libraries;

public class GeoRegionNamingRule
{
    public string Template;
}

public class GeoRegionAsset : Asset
{
    public GeoRegionLayer Layer;
    public int Priority;
    public int MinTiles;
    public int MaxTiles;

    public string DisplayName;
    public GeoRegionNamingRule Naming = new();

    public string[] BiomeIds;

    public int MinHeight = -1;
    public int MaxHeight = -1;
    public int MinSlope = -1;
    public int MaxSlope = -1;
    public int MinDelta = -1;
    public int MaxDelta = -1;
    public bool HeightOrSlope;

    public int MaxThickness;
    public float MinCoastRatio;
    public float MaxNeckRatio;

    public int MaxHalfWidth;
    public int MinExits;
    public float MinAspectRatio;

    public int IslandMaxTiles;
    public int MaxGap;
    public int MinIslands;
    public int MinTotalTiles;
}

using Cultiway.Core;

namespace Cultiway.Core.EventSystem.Events;

public struct GeoRegionGeneratedEvent
{
    public int WorldSeedId;
    public int Width;
    public int Height;

    public GeoRegionLayer Layer;
    public long RegionId;

    public TileLayerType BaseLayerType;
    public PrimaryWaterKind WaterKind;
    public bool TouchesEdge;

    public int CenterX;
    public int CenterY;
    public int TileCount;

    public string BiomeDominantCategoryId;
    public string LandformDominantCategoryId;
}

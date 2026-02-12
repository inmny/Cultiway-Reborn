using Cultiway.Abstract;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class TileExtend : ExtendComponent<WorldTile>
{
    private readonly Entity e;

    public TileExtend(Entity e)
    {
        this.e = e;
    }
    public override Entity E => e;
    public override WorldTile Base => e.GetComponent<TileBinder>().Tile;

    public GeoRegion GetGeoRegion()
    {
        var rels = e.GetRelations<BelongToRelation>();
        foreach (var rel in rels)
        {
            if (rel.entity.HasComponent<GeoRegionBinder>())
            {
                return rel.entity.GetComponent<GeoRegionBinder>().GeoRegion;
            }
        }
        return null;
    }

    public bool HasGeoRegion()
    {
        return GetGeoRegion() != null;
    }
}
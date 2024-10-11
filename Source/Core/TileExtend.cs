using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class TileExtend : ExtendComponent<WorldTile>
{
    private readonly Entity e;

    public TileExtend(Entity e)
    {
        this.e = e;
    }

    public override WorldTile Base => e.GetComponent<TileBinder>().Tile;
}
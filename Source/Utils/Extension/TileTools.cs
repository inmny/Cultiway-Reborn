using Cultiway.Core;

namespace Cultiway.Utils.Extension;

public static class TileTools
{
    public static TileExtend GetExtend(this WorldTile tile)
    {
        return ModClass.I.TileExtendManager.Get(tile.data.tile_id);
    }
}
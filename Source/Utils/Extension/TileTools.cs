using Cultiway.Core;
using strings;

namespace Cultiway.Utils.Extension;

public static class TileTools
{
    public static TileExtend GetExtend(this WorldTile tile)
    {
        return ModClass.I.TileExtendManager.Get(tile.data.tile_id);
    }
    
    /// <summary>
    /// 检查地块是否是水域
    /// </summary>
    public static bool IsWater(this WorldTile tile)
    {
        var terrainId = tile.main_type.id;
        return terrainId == ST.deep_ocean || 
               terrainId == ST.close_ocean || 
               terrainId == ST.shallow_waters;
    }
}
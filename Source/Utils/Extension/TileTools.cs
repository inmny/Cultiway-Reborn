using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Utils;
using strings;
using UnityEngine;

namespace Cultiway.Utils.Extension;

public static class TileTools
{
    public static TileExtend GetExtend(this WorldTile tile)
    {
        return ModClass.I.TileExtendManager.Get(tile.data.tile_id);
    }

    public static TileTypeBase GetCurrentType(this WorldTile tile)
    {
        if (tile == null)
        {
            throw new InvalidOperationException("地块为空");
        }

        TileTypeBase type = tile.Type;
        if (type != null)
        {
            return type;
        }

        throw new InvalidOperationException($"地块当前类型为空 x={tile.x}, y={tile.y}");
    }

    public static Color32 GetCurrentColor(this WorldTile tile, byte alpha = 255)
    {
        tile.GetCurrentType();
        Color32 color = tile.getColor();
        color.a = alpha;
        return color;
    }

    public static Color32 GetAverageCurrentColor(this IReadOnlyList<WorldTile> tiles, byte alpha = 255)
    {
        if (tiles == null)
        {
            throw new InvalidOperationException("计算地块平均颜色失败：地块列表为空");
        }

        long red = 0;
        long green = 0;
        long blue = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            Color32 color = tiles[i].GetCurrentColor(alpha);
            red += color.r;
            green += color.g;
            blue += color.b;
        }

        return ColorUtils.AverageRgb(red, green, blue, tiles.Count, alpha);
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

using System;
using Cultiway.Core.SubWorlds.Model;

namespace Cultiway.Core.SubWorlds.Runtime;

internal sealed class SubWorldGrid
{
    private readonly SubWorldMapData mapData;
    private readonly TileType[] mainTypes;
    private readonly TopTileType[] topTypes;

    internal SubWorldGrid(SubWorldMapData mapData)
    {
        if (mapData == null) throw new ArgumentNullException(nameof(mapData));
        if (mapData.Width <= 0) throw new ArgumentOutOfRangeException(nameof(mapData.Width));
        if (mapData.Height <= 0) throw new ArgumentOutOfRangeException(nameof(mapData.Height));

        int tileCount = checked(mapData.Width * mapData.Height);
        if (mapData.Tiles == null || mapData.Tiles.Length != tileCount)
        {
            throw new InvalidOperationException(
                $"SubWorld 地图格子数量错误: width={mapData.Width}, height={mapData.Height}, tiles={mapData.Tiles?.Length ?? 0}");
        }

        ValidateTileIndices(mapData.EntryTileIndices, nameof(mapData.EntryTileIndices), tileCount);
        ValidateTileIndices(mapData.ExitTileIndices, nameof(mapData.ExitTileIndices), tileCount);

        this.mapData = mapData;
        Width = mapData.Width;
        Height = mapData.Height;
        TileCount = tileCount;
        mainTypes = new TileType[tileCount];
        topTypes = new TopTileType[tileCount];

        for (int index = 0; index < tileCount; index++)
        {
            ResolveTile(mapData.Tiles[index], index, out mainTypes[index], out topTypes[index]);
        }
    }

    internal SubWorldMapData MapData => mapData;
    internal int Width { get; }
    internal int Height { get; }
    internal int TileCount { get; }

    internal bool Contains(int x, int y)
    {
        return (uint)x < (uint)Width && (uint)y < (uint)Height;
    }

    internal int GetIndex(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"SubWorld 格子坐标超出地图范围: ({x}, {y})");
        }
        return y * Width + x;
    }

    internal int GetX(int index)
    {
        ValidateIndex(index);
        return index % Width;
    }

    internal int GetY(int index)
    {
        ValidateIndex(index);
        return index / Width;
    }

    internal SubWorldTile GetTile(int index)
    {
        ValidateIndex(index);
        return mapData.Tiles[index];
    }

    internal TileType GetMainType(int index)
    {
        ValidateIndex(index);
        return mainTypes[index];
    }

    internal TopTileType GetTopType(int index)
    {
        ValidateIndex(index);
        return topTypes[index];
    }

    internal TileTypeBase GetTerrainType(int index)
    {
        ValidateIndex(index);
        TopTileType topType = topTypes[index];
        return topType != null ? topType : mainTypes[index];
    }

    internal void SetTile(int index, SubWorldTile tile)
    {
        ValidateIndex(index);
        ResolveTile(tile, index, out TileType mainType, out TopTileType topType);
        mapData.Tiles[index] = tile;
        mainTypes[index] = mainType;
        topTypes[index] = topType;
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)TileCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "SubWorld 格子索引超出地图范围");
        }
    }

    private static void ResolveTile(SubWorldTile tile, int index, out TileType mainType,
        out TopTileType topType)
    {
        if (string.IsNullOrEmpty(tile.MainAssetId))
        {
            throw new InvalidOperationException($"SubWorld 格子缺少 Main terrain: index={index}");
        }

        mainType = AssetManager.tiles.get(tile.MainAssetId);
        if (mainType == null)
        {
            throw new InvalidOperationException(
                $"SubWorld 格子引用了未注册的 Main terrain: index={index}, asset={tile.MainAssetId}");
        }

        topType = null;
        if (!string.IsNullOrEmpty(tile.TopAssetId))
        {
            topType = AssetManager.top_tiles.get(tile.TopAssetId);
            if (topType == null)
            {
                throw new InvalidOperationException(
                    $"SubWorld 格子引用了未注册的 Top terrain: index={index}, asset={tile.TopAssetId}");
            }
        }
    }

    private static void ValidateTileIndices(int[] indices, string fieldName, int tileCount)
    {
        if (indices == null) throw new InvalidOperationException($"SubWorld 地图缺少 {fieldName}");
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            if ((uint)index >= (uint)tileCount)
            {
                throw new InvalidOperationException(
                    $"SubWorld 地图的 {fieldName}[{i}] 超出范围: index={index}, count={tileCount}");
            }
        }
    }
}

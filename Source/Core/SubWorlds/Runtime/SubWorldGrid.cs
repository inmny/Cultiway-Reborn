using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SubWorlds.Model;
using UnityEngine;

namespace Cultiway.Core.SubWorlds.Runtime;

/// <summary>
/// 管理小世界格子坐标，并把地图中的 terrain Asset ID 解析为原版 Asset 直接引用。
/// </summary>
internal sealed class SubWorldGrid
{
    private readonly SubWorldMapData mapData;
    private readonly TileType[] mainTypes;
    private readonly TopTileType[] topTypes;
    private readonly HashSet<int> dirtyTiles = new();
    private bool allTilesDirty = true;

    /// <summary>
    /// 验证地图形状并解析所有格子的 Main 和 Top terrain。
    /// </summary>
    /// <param name="mapData">由当前 Runtime 独占的地图数据。</param>
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

    /// <summary>当前 Grid 管理的地图数据。</summary>
    internal SubWorldMapData MapData => mapData;

    /// <summary>地图宽度，单位为格。</summary>
    internal int Width { get; }

    /// <summary>地图高度，单位为格。</summary>
    internal int Height { get; }

    /// <summary>地图格子总数。</summary>
    internal int TileCount { get; }

    /// <summary>
    /// 判断格子坐标是否位于地图范围内。
    /// </summary>
    /// <param name="x">格子横坐标。</param>
    /// <param name="y">格子纵坐标。</param>
    /// <returns>坐标有效时为 <see langword="true"/>。</returns>
    internal bool Contains(int x, int y)
    {
        return (uint)x < (uint)Width && (uint)y < (uint)Height;
    }

    /// <summary>
    /// 将格子坐标转换为 row-major 索引。
    /// </summary>
    /// <param name="x">格子横坐标。</param>
    /// <param name="y">格子纵坐标。</param>
    /// <returns><c>y * Width + x</c>。</returns>
    internal int GetIndex(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"SubWorld 格子坐标超出地图范围: ({x}, {y})");
        }
        return y * Width + x;
    }

    /// <summary>取得 Position 所在格子的 row-major 索引。</summary>
    /// <param name="position">地图局部坐标。</param>
    /// <returns>Position 所在格子的索引。</returns>
    internal int GetAnchorTileIndex(in Position position)
    {
        return GetIndex(
            Mathf.FloorToInt(position.value.x),
            Mathf.FloorToInt(position.value.y));
    }

    /// <summary>
    /// 取得 row-major 索引对应的格子横坐标。
    /// </summary>
    /// <param name="index">格子索引。</param>
    /// <returns>格子横坐标。</returns>
    internal int GetX(int index)
    {
        ValidateIndex(index);
        return index % Width;
    }

    /// <summary>
    /// 取得 row-major 索引对应的格子纵坐标。
    /// </summary>
    /// <param name="index">格子索引。</param>
    /// <returns>格子纵坐标。</returns>
    internal int GetY(int index)
    {
        ValidateIndex(index);
        return index / Width;
    }

    /// <summary>
    /// 取得指定格子的 map-local Asset ID 数据。
    /// </summary>
    /// <param name="index">格子索引。</param>
    /// <returns>格子数据。</returns>
    internal SubWorldTile GetTile(int index)
    {
        ValidateIndex(index);
        return mapData.Tiles[index];
    }

    /// <summary>
    /// 取得指定格子解析后的原版 Main terrain Asset。
    /// </summary>
    /// <param name="index">格子索引。</param>
    /// <returns>Main terrain Asset。</returns>
    internal TileType GetMainType(int index)
    {
        ValidateIndex(index);
        return mainTypes[index];
    }

    /// <summary>
    /// 取得指定格子解析后的原版 Top terrain Asset。
    /// </summary>
    /// <param name="index">格子索引。</param>
    /// <returns>Top terrain Asset；该格没有 Top 时为 <see langword="null"/>。</returns>
    internal TopTileType GetTopType(int index)
    {
        ValidateIndex(index);
        return topTypes[index];
    }

    /// <summary>
    /// 取得 gameplay 使用的有效 terrain，即 <c>Top ?? Main</c>。
    /// </summary>
    /// <param name="index">格子索引。</param>
    /// <returns>该格的有效原版 terrain Asset。</returns>
    internal TileTypeBase GetTerrainType(int index)
    {
        ValidateIndex(index);
        TopTileType topType = topTypes[index];
        return topType != null ? topType : mainTypes[index];
    }

    /// <summary>
    /// 修改一个格子的 Asset ID，并同步刷新其原版 Asset 直接引用。
    /// </summary>
    /// <param name="index">格子索引。</param>
    /// <param name="tile">新的格子数据。</param>
    internal void SetTile(int index, SubWorldTile tile)
    {
        ValidateIndex(index);
        ResolveTile(tile, index, out TileType mainType, out TopTileType topType);
        mapData.Tiles[index] = tile;
        mainTypes[index] = mainType;
        topTypes[index] = topType;
        if (!allTilesDirty) MarkTileAndNeighboursDirty(index);
    }

    /// <summary>
    /// 取出自上次视觉同步后发生变化的格子；首次同步返回整张地图。
    /// </summary>
    /// <param name="target">接收 dirty tile index 的列表。</param>
    internal void ConsumeDirtyTiles(List<int> target)
    {
        target.Clear();
        if (allTilesDirty)
        {
            for (int index = 0; index < TileCount; index++) target.Add(index);
            allTilesDirty = false;
        }
        else
        {
            target.AddRange(dirtyTiles);
        }
        dirtyTiles.Clear();
    }

    private void ValidateIndex(int index)
    {
        if ((uint)index >= (uint)TileCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "SubWorld 格子索引超出地图范围");
        }
    }

    private void MarkTileAndNeighboursDirty(int index)
    {
        dirtyTiles.Add(index);
        int x = index % Width;
        int y = index / Width;
        if (x > 0) dirtyTiles.Add(index - 1);
        if (x + 1 < Width) dirtyTiles.Add(index + 1);
        if (y > 0) dirtyTiles.Add(index - Width);
        if (y + 1 < Height) dirtyTiles.Add(index + Width);
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

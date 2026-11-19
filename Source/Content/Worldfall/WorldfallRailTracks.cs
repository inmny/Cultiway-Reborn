#if S3COND2_WORLDFALL
using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Core;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Content.Worldfall;

/// <summary>把实际铁路显示为钢轨和木枕，只缓存镜头到过的小片区域。</summary>
internal static class WorldfallRailTracks
{
    /// <summary>车轮底与钢轨顶面共用的高度。</summary>
    internal const float RailTop = .14f;
    /// <summary>每片十六格，铺轨或拆轨只重查受影响的小片。</summary>
    private const int ChunkSide = 16;
    /// <summary>按邻接方向和距离档共用的静态轨道网格。</summary>
    private static readonly MeshModel[,] Models = new MeshModel[16, 3];
    /// <summary>已经看过的区域及其显示实例。</summary>
    private static readonly Dictionary<(int X, int Y), TrackChunk> Chunks = new();
    /// <summary>缓存所属的地图数组，换世界时清空。</summary>
    private static WorldTile[,] Tiles;
    /// <summary>原生场景的网格列表。</summary>
    private static FieldInfo SceneMeshes;
    /// <summary>采样器共用的地表缓存。</summary>
    private static FieldInfo SamplerTerrain;
    /// <summary>上帝视角当前观察范围。</summary>
    private static FieldInfo SkyReach;
    /// <summary>上帝视角落在地面的观察中心。</summary>
    private static FieldInfo SkyFocus;

    /// <summary>一片区域只在初次看见或地块改变后重建轨道索引。</summary>
    private sealed class TrackChunk
    {
        /// <summary>下一次显示前需要重新读取地块。</summary>
        internal bool Dirty = true;
        /// <summary>仍保留在这一片内的轨道显示记录。</summary>
        internal readonly List<TrackCell> Cells = new();
    }

    /// <summary>一个实际铁轨地块与复用的显示实例。</summary>
    private sealed class TrackCell
    {
        /// <summary>对应的实际地块。</summary>
        internal WorldTile Tile;
        /// <summary>上、右、下、左四方向组成的邻接位。</summary>
        internal int Mask;
        /// <summary>只有索引重建时才新建实例，逐帧不生成网格。</summary>
        internal readonly MeshInstance Mesh = new();
    }

    /// <summary>模型读入后安装显示和地块变更入口。</summary>
    /// <param name="collector">已核对的场景收集类型。</param>
    /// <param name="harmony">本模组的外观补丁组。</param>
    /// <param name="models">已经读入的正式网格。</param>
    internal static void Install(Type collector, Harmony harmony, Dictionary<string, MeshModel> models)
    {
        string[] suffixes = ["", "_m", "_f"];
        for (int mask = 0; mask < 16; mask++)
            for (int level = 0; level < suffixes.Length; level++)
                Models[mask, level] = models["cultiway_train_track_" + mask + suffixes[level]];
        SceneMeshes = AccessTools.Field(collector, "Meshes");
        SamplerTerrain = AccessTools.Field(collector.Assembly.GetType("FirstPerson.WorldSampler"), "Cache");
        SkyReach = AccessTools.Field(collector, "SkyReach");
        SkyFocus = AccessTools.Field(collector, "SkyFocus");
        harmony.Patch(AccessTools.Method(collector, "Collect"),
            postfix: new HarmonyMethod(typeof(WorldfallRailTracks), nameof(CollectTracks)));
        var before = new HarmonyMethod(typeof(WorldfallRailTracks), nameof(BeforeTileChanged));
        var changed = new HarmonyMethod(typeof(WorldfallRailTracks), nameof(TileChanged));
        harmony.Patch(AccessTools.Method(typeof(WorldTile), nameof(WorldTile.setTopTileType)), prefix: before, postfix: changed);
        harmony.Patch(AccessTools.Method(typeof(WorldTile), nameof(WorldTile.setTileType), [typeof(TileType), typeof(bool)]),
            prefix: before, postfix: changed);
        var stationChanged = new HarmonyMethod(typeof(WorldfallRailTracks), nameof(StationChanged));
        harmony.Patch(AccessTools.Method(typeof(Building), nameof(Building.fillTiles)), postfix: stationChanged);
        harmony.Patch(AccessTools.Method(typeof(Building), nameof(Building.clearTiles)), prefix: stationChanged);
    }

    /// <summary>记录变化前是否是铁轨，普通群系更新不使轨道缓存失效。</summary>
    /// <param name="__instance">正在变化的地块。</param>
    /// <param name="__state">变化前的铁轨标记。</param>
    private static void BeforeTileChanged(WorldTile __instance, out bool __state) =>
        __state = __instance.top_type == TopTileTypes.TrainTrack;

    /// <summary>铺设、拆除或地形变化时连同邻格更新，弯道与断头同步改变。</summary>
    /// <param name="__instance">刚刚改变的实际地块。</param>
    /// <param name="__state">变化前是否有铁轨。</param>
    private static void TileChanged(WorldTile __instance, bool __state)
    {
        if (__state || __instance.top_type == TopTileTypes.TrainTrack) DirtyAround(__instance);
    }

    /// <summary>车站占地生成或移除时同步更新相邻的接轨口。</summary>
    /// <param name="__instance">占地变化的建筑。</param>
    private static void StationChanged(Building __instance)
    {
        if (__instance.asset != Buildings.TrainStation) return;
        foreach (WorldTile tile in __instance.tiles) DirtyAround(tile);
    }

    /// <summary>邻居位于另一片区域时，也要刷新接头。</summary>
    /// <param name="tile">发生变化的格子。</param>
    private static void DirtyAround(WorldTile tile)
    {
        DirtyAt(tile);
        DirtyAt(tile.tile_up); DirtyAt(tile.tile_right);
        DirtyAt(tile.tile_down); DirtyAt(tile.tile_left);
    }

    /// <summary>未看过的区域无需创建缓存。</summary>
    /// <param name="tile">变化涉及的格子。</param>
    private static void DirtyAt(WorldTile tile)
    {
        if (tile != null && Chunks.TryGetValue((tile.x / ChunkSide, tile.y / ChunkSide), out var chunk))
            chunk.Dirty = true;
    }

    /// <summary>只把邻接铁路或车站入口视为连接方向。</summary>
    /// <param name="from">当前铁轨格。</param>
    /// <param name="tile">相邻格子。</param>
    /// <returns>轨道能否连向这里。</returns>
    private static bool Connected(WorldTile from, WorldTile tile) => Railways.RailwayStationService.ConnectsTrack(from, tile);

    /// <summary>在首次看见或发生变化时重建这一小片，普通帧只读缓存。</summary>
    /// <param name="chunk">本片缓存。</param>
    /// <param name="cx">区域横坐标。</param>
    /// <param name="cy">区域纵坐标。</param>
    private static void Rebuild(TrackChunk chunk, int cx, int cy)
    {
        chunk.Cells.Clear();
        int x1 = Math.Min((cx + 1) * ChunkSide, Tiles.GetLength(0));
        int y1 = Math.Min((cy + 1) * ChunkSide, Tiles.GetLength(1));
        for (int y = cy * ChunkSide; y < y1; y++)
            for (int x = cx * ChunkSide; x < x1; x++)
            {
                WorldTile tile = Tiles[x, y];
                if (tile.top_type != TopTileTypes.TrainTrack) continue;
                int mask = (Connected(tile, tile.tile_up) ? 1 : 0) | (Connected(tile, tile.tile_right) ? 2 : 0) |
                    (Connected(tile, tile.tile_down) ? 4 : 0) | (Connected(tile, tile.tile_left) ? 8 : 0);
                chunk.Cells.Add(new TrackCell { Tile = tile, Mask = mask });
            }
        chunk.Dirty = false;
    }

    /// <summary>从本帧镜头附近取缓存，按地表放置轨道，并选择对应距离档。</summary>
    /// <param name="__instance">实际场景收集器。</param>
    /// <param name="view">实际镜头。</param>
    /// <param name="range">原生场景观察距离。</param>
    /// <param name="sampler">当前世界的原生地形采样器。</param>
    private static void CollectTracks(object __instance, ViewState view, float range, object sampler)
    {
        if (World.world == null || World.world.tiles_map == null) return;
        if (!ReferenceEquals(Tiles, World.world.tiles_map))
        {
            Tiles = World.world.tiles_map;
            Chunks.Clear();
        }
        TerrainCache terrain = (TerrainCache)SamplerTerrain.GetValue(sampler);
        var meshes = (List<MeshInstance>)SceneMeshes.GetValue(__instance);
        float skyReach = (float)SkyReach.GetValue(__instance);
        Vector2 center = skyReach > 0f ? (Vector2)SkyFocus.GetValue(__instance) : new Vector2(view.X, view.Y);
        float reach = Math.Max(range, skyReach) + 2f;
        int x0 = Math.Max(0, Mathf.FloorToInt(center.x - reach) / ChunkSide);
        int y0 = Math.Max(0, Mathf.FloorToInt(center.y - reach) / ChunkSide);
        int x1 = Math.Min((Tiles.GetLength(0) - 1) / ChunkSide, Mathf.CeilToInt(center.x + reach) / ChunkSide);
        int y1 = Math.Min((Tiles.GetLength(1) - 1) / ChunkSide, Mathf.CeilToInt(center.y + reach) / ChunkSide);
        for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
            {
                if (!Chunks.TryGetValue((cx, cy), out var chunk))
                    Chunks.Add((cx, cy), chunk = new TrackChunk());
                if (chunk.Dirty) Rebuild(chunk, cx, cy);
                foreach (TrackCell cell in chunk.Cells)
                {
                    float dx = cell.Tile.x - center.x, dy = cell.Tile.y - center.y;
                    float distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared > reach * reach) continue;
                    int level = distanceSquared < 900f ? 0 : distanceSquared < 6400f ? 1 : 2;
                    MeshInstance mesh = cell.Mesh;
                    mesh.Model = Models[cell.Mask, level];
                    mesh.EnsurePose();
                    mesh.Position = new Vec3(cell.Tile.x, cell.Tile.y, terrain.SampleHeight(cell.Tile.x, cell.Tile.y));
                    mesh.Basis = Mat3.Identity;
                    mesh.NoShadow = false; mesh.KeepSmall = true; mesh.Static = true;
                    mesh.SortDepth = mesh.Position.Z;
                    meshes.Add(mesh);
                }
            }
    }
}
#endif

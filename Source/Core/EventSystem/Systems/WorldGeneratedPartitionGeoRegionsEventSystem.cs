using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.GeoLib.Components;
using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// 世界生成完成后，按层自动划分 GeoRegion（允许 tile 跨层多归属）。
/// 主要流程：
/// 1) 预计算 tile 基础属性（陆/水/坑/山、Primary 编码、Landform 编码、海滩距离场）。
/// 2) 依次生成 Primary / Landform / Landmass 三个“基础层”。
/// 3) 基于基础层和形态启发式生成 Peninsula / Strait / Archipelago 三个“叠加层”。
/// 4) 每创建一个 GeoRegion 都发布 GeoRegionGeneratedEvent，由后续系统自动分类命名。
/// </summary>
public class WorldGeneratedPartitionGeoRegionsEventSystem : GenericEventSystem<WorldGeneratedEvent>
{
    protected override int MaxEventsPerUpdate => 4;

    private int _lastWorldSeedId;
    private int _lastWidth;
    private int _lastHeight;

    /// <summary>
    /// WorldGeneratedEvent 主入口：完成一次完整的多层 GeoRegion 划分。
    /// </summary>
    protected override void HandleEvent(WorldGeneratedEvent evt)
    {
        if (evt.Width <= 0 || evt.Height <= 0) return;

        // 避免重复触发（有些情况下 finishMakingWorld 可能被调用多次）
        if (evt.WorldSeedId == _lastWorldSeedId && evt.Width == _lastWidth && evt.Height == _lastHeight)
        {
            return;
        }

        _lastWorldSeedId = evt.WorldSeedId;
        _lastWidth = evt.Width;
        _lastHeight = evt.Height;

        if (ModClass.I?.TileExtendManager == null || !ModClass.I.TileExtendManager.Ready())
        {
            // 理论上这里已经 FitNewWorld，但为了稳定性加一道保险
            return;
        }

        var tiles = World.world.tiles_list;
        if (tiles == null || tiles.Length == 0)
        {
            return;
        }

        var width = evt.Width;
        var height = evt.Height;
        if (width * height != tiles.Length)
        {
            width = MapBox.width;
            height = MapBox.height;
        }

        var geoRegionLib = ModClass.L.GeoRegionLibrary;

        // 旧世界切换到新世界时，先清理历史关系实体，避免残留关系污染本次划分。
        CleanupOldGeoRegionBinders();

        // 基础数组：后续所有层级都依赖这些预计算结果。
        var total = tiles.Length;
        var isLand = new bool[total];
        var isWater = new bool[total];
        var primaryCategoryCode = new byte[total];
        var primarySignature = new byte[total];
        var landformCode = new byte[total];

        // 统一做一遍 tile 级预计算（材料、邻接、签名、海滩距离）。
        BuildBaseArrays(tiles, width, height, geoRegionLib, isLand, isWater, primaryCategoryCode, primarySignature, landformCode);

        var queue = new int[total];

        // 基础层：Primary（地表主分类 + 水体细分）。
        GeneratePrimary(evt, tiles, width, height, geoRegionLib, primarySignature, landformCode, isLand, isWater, queue);
        // 基础层：Landform（平原/山地/峡谷/盆地）。
        GenerateLandform(evt, tiles, width, height, geoRegionLib, landformCode, primaryCategoryCode, queue);

        // 基础层：Landmass（大陆/岛屿），并产出后续群岛层候选。
        var islandCandidates = new List<IslandInfo>(64);
        GenerateLandmass(evt, tiles, width, height, geoRegionLib, isLand, primaryCategoryCode, landformCode, queue, islandCandidates);

        // 叠加形态层：半岛、海峡、群岛。
        GeneratePeninsula(evt, tiles, width, height, geoRegionLib, isLand, isWater, primaryCategoryCode, landformCode, queue);
        GenerateStrait(evt, tiles, width, height, geoRegionLib, isLand, isWater, queue);
        GenerateArchipelago(evt, tiles, width, height, geoRegionLib, primaryCategoryCode, landformCode, islandCandidates);
    }

    /// <summary>
    /// 清理旧世界遗留的 GeoRegion 关系绑定实体。
    /// </summary>
    private static void CleanupOldGeoRegionBinders()
    {
        var ecsWorld = ModClass.I.TileExtendManager.World;
        ecsWorld.Query<GeoRegionBinder>().ForEachEntity((ref GeoRegionBinder _, Entity e) => e.DeleteEntity());
    }

    /// <summary>
    /// 预计算基础数组：
    /// - isLand / isWater：后续 flood fill 与形态识别的底图。
    /// - primarySignature / primaryCategoryCode：Primary 划分签名与主类编码。
    /// - landformCode：Landform 划分签名。
    /// 同时计算沙地到水体的距离场，用于“宽海滩”判定。
    /// </summary>
    private static void BuildBaseArrays(
        WorldTile[] tiles,
        int width,
        int height,
        GeoRegionLibrary geoRegionLib,
        bool[] isLand,
        bool[] isWater,
        byte[] primaryCategoryCode,
        byte[] primarySignature,
        byte[] landformCode)
    {
        var isBlock = new bool[tiles.Length];
        var isPit = new bool[tiles.Length];
        var isBeachMaterial = new bool[tiles.Length];
        var beachDistance = new int[tiles.Length];

        // 第一遍：识别 tile 基础属性，并给非 Ground 类型写入固定 Primary 签名。
        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            var tileType = tile.Type;
            var layerType = tileType.layer_type;
            var biomeId = tile.getBiome()?.id;

            var isLava = layerType == TileLayerType.Lava || tileType.lava;
            var isGoo = layerType == TileLayerType.Goo || tileType.grey_goo;
            var isWaterTile = (layerType == TileLayerType.Ocean || tileType.ocean) && !isLava && !isGoo;
            var isBlockTile = layerType == TileLayerType.Block || tileType.block;
            var isGroundTile = layerType == TileLayerType.Ground;
            var isPitTile = tileType.can_be_filled_with_ocean;

            isWater[i] = isWaterTile;
            isBlock[i] = isBlockTile;
            isPit[i] = isPitTile;
            isBeachMaterial[i] = IsBeachMaterialTile(tileType, biomeId);
            beachDistance[i] = -1;

            if (isLava)
            {
                primarySignature[i] = 2;
                continue;
            }

            if (isGoo)
            {
                primarySignature[i] = 3;
                continue;
            }

            if (isWaterTile)
            {
                primarySignature[i] = 1;
                continue;
            }

            if (isBlockTile)
            {
                isLand[i] = true;
                primaryCategoryCode[i] = 11; // 山地（用于跨层命名）
                primarySignature[i] = 4;
                continue;
            }

            if (isGroundTile)
            {
                isLand[i] = true;
                continue;
            }

            primarySignature[i] = 0;
        }

        // 预计算“沙地连通到水体”的距离，用于支持宽海滩识别。
        var beachQueue = new int[tiles.Length];
        var beachHead = 0;
        var beachTail = 0;

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isLand[i] || !isBeachMaterial[i]) continue;
            if (!HasWaterNeighbor8(tiles, i, width, height, isWater)) continue;
            beachDistance[i] = 0;
            beachQueue[beachTail++] = i;
        }

        while (beachHead < beachTail)
        {
            var idx = beachQueue[beachHead++];
            var nextDistance = beachDistance[idx] + 1;
            var tile = tiles[idx];
            var x = tile.x;
            var y = tile.y;

            if (x > 0) TryExpandBeachDistance(idx - 1, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
            if (x < width - 1) TryExpandBeachDistance(idx + 1, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
            if (y > 0) TryExpandBeachDistance(idx - width, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
            if (y < height - 1) TryExpandBeachDistance(idx + width, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
        }

        // 第二遍：仅在陆地上计算邻接统计，得到 Landform 与 Ground 的 Primary 分类。
        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isLand[i]) continue;

            var tile = tiles[i];
            var tileType = tile.Type;

            var x = tile.x;
            var y = tile.y;
            var left = x > 0 ? i - 1 : -1;
            var right = x < width - 1 ? i + 1 : -1;
            var down = y > 0 ? i - width : -1;
            var up = y < height - 1 ? i + width : -1;

            var neighborWaterCount = 0;
            var neighborWater8Count = 0;
            var neighborBlockCount = 0;
            var neighborPitCount = 0;

            if (left >= 0)
            {
                if (isWater[left])
                {
                    neighborWaterCount++;
                    neighborWater8Count++;
                }
                if (isBlock[left]) neighborBlockCount++;
                if (isPit[left]) neighborPitCount++;
            }
            if (right >= 0)
            {
                if (isWater[right])
                {
                    neighborWaterCount++;
                    neighborWater8Count++;
                }
                if (isBlock[right]) neighborBlockCount++;
                if (isPit[right]) neighborPitCount++;
            }
            if (down >= 0)
            {
                if (isWater[down])
                {
                    neighborWaterCount++;
                    neighborWater8Count++;
                }
                if (isBlock[down]) neighborBlockCount++;
                if (isPit[down]) neighborPitCount++;
            }
            if (up >= 0)
            {
                if (isWater[up])
                {
                    neighborWaterCount++;
                    neighborWater8Count++;
                }
                if (isBlock[up]) neighborBlockCount++;
                if (isPit[up]) neighborPitCount++;
            }

            if (x > 0 && y > 0 && isWater[i - width - 1]) neighborWater8Count++;
            if (x < width - 1 && y > 0 && isWater[i - width + 1]) neighborWater8Count++;
            if (x > 0 && y < height - 1 && isWater[i + width - 1]) neighborWater8Count++;
            if (x < width - 1 && y < height - 1 && isWater[i + width + 1]) neighborWater8Count++;

            var leftBlock = left >= 0 && isBlock[left];
            var rightBlock = right >= 0 && isBlock[right];
            var downBlock = down >= 0 && isBlock[down];
            var upBlock = up >= 0 && isBlock[up];
            var hasOppositeBlockPair = (leftBlock && rightBlock) || (downBlock && upBlock);

            var layerType = tileType.layer_type;
            var isLava = layerType == TileLayerType.Lava || tileType.lava;
            var isGoo = layerType == TileLayerType.Goo || tileType.grey_goo;
            var isMountain = layerType == TileLayerType.Block || tileType.mountains || tileType.edge_mountains;
            var biomeId = tile.getBiome()?.id;

            var context = new GeoRegionTileRuleContext(
                tileType.id,
                layerType,
                biomeId,
                tileType.ocean,
                isPit[i],
                isLava,
                isGoo,
                isMountain,
                neighborWaterCount,
                neighborWater8Count,
                beachDistance[i],
                neighborBlockCount,
                neighborPitCount,
                hasOppositeBlockPair);

            var landformAsset = geoRegionLib.ResolveLandform(context);
            landformCode[i] = ResolveLandformCode(geoRegionLib, landformAsset);

            if (layerType == TileLayerType.Ground)
            {
                var primaryAsset = geoRegionLib.ResolvePrimaryLandByContext(context);
                var primaryCode = ResolvePrimaryCategoryCode(geoRegionLib, primaryAsset);
                primaryCategoryCode[i] = primaryCode;
                primarySignature[i] = (byte)(10 + primaryCode);
            }
        }
    }

    /// <summary>
    /// 判断地块是否可视为“沙滩材质”。
    /// </summary>
    private static bool IsBeachMaterialTile(TileTypeBase tileType, string biomeId)
    {
        if (tileType == null) return false;
        if (tileType.sand) return true;

        var tileTypeId = tileType.id;
        if (string.Equals(tileTypeId, "sand", StringComparison.Ordinal) ||
            string.Equals(tileTypeId, "snow_sand", StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(biomeId, "biome_sand", StringComparison.Ordinal);
    }

    /// <summary>
    /// 判断地块 8 邻接是否存在水体。
    /// </summary>
    private static bool HasWaterNeighbor8(WorldTile[] tiles, int tileId, int width, int height, bool[] isWater)
    {
        var tile = tiles[tileId];
        var x = tile.x;
        var y = tile.y;

        if (x > 0 && isWater[tileId - 1]) return true;
        if (x < width - 1 && isWater[tileId + 1]) return true;
        if (y > 0 && isWater[tileId - width]) return true;
        if (y < height - 1 && isWater[tileId + width]) return true;

        if (x > 0 && y > 0 && isWater[tileId - width - 1]) return true;
        if (x < width - 1 && y > 0 && isWater[tileId - width + 1]) return true;
        if (x > 0 && y < height - 1 && isWater[tileId + width - 1]) return true;
        if (x < width - 1 && y < height - 1 && isWater[tileId + width + 1]) return true;

        return false;
    }

    /// <summary>
    /// 广度扩展沙滩距离场。
    /// </summary>
    private static void TryExpandBeachDistance(
        int index,
        int distance,
        bool[] isLand,
        bool[] isBeachMaterial,
        int[] beachDistance,
        int[] queue,
        ref int tail)
    {
        if (!isLand[index] || !isBeachMaterial[index]) return;
        if (beachDistance[index] >= 0) return;

        beachDistance[index] = distance;
        queue[tail++] = index;
    }

    /// <summary>
    /// 生成 Primary 层：
    /// - 非水体直接按 signature 连通分量划分；
    /// - 水体先识别河流，再对其余水体做海/湖分块；
    /// - 对碎片执行同 signature 合并；
    /// - 创建 Region、写入关系并发布事件。
    /// </summary>
    private static void GeneratePrimary(
        WorldGeneratedEvent evt,
        WorldTile[] tiles,
        int width,
        int height,
        GeoRegionLibrary geoRegionLib,
        byte[] primarySignature,
        byte[] landformCode,
        bool[] isLand,
        bool[] isWater,
        int[] queue)
    {
        var components = new List<MutableRegionComponent>(256);
        var componentOfTile = new int[tiles.Length];
        for (var i = 0; i < componentOfTile.Length; i++) componentOfTile[i] = -1;

        CollectPrimaryNonWaterComponents(tiles, width, height, primarySignature, queue, components, componentOfTile);
        CollectPrimaryWaterComponents(tiles, width, height, evt.WorldSeedId, geoRegionLib, isLand, isWater, queue, components, componentOfTile);

        MergeOrDropTinyComponents(tiles, width, height, components, componentOfTile,
            sig => ResolvePrimaryMinTilesBySignature(geoRegionLib, sig));

        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component.Removed || component.TileIds.Count <= 0) continue;

            var count = component.TileIds.Count;
            var centerX = (component.SumX + count / 2) / count;
            var centerY = (component.SumY + count / 2) / count;

            var waterKind = SignatureToWaterKind(component.Signature);
            var baseLayerType = waterKind == PrimaryWaterKind.None
                ? SigToBaseLayerType((byte)component.Signature)
                : TileLayerType.Ocean;

            string biomeDominantCategoryId = null;
            if (baseLayerType == TileLayerType.Ground)
            {
                var biomeCode = (byte)(component.Signature - 10);
                biomeDominantCategoryId = PrimaryCategoryIdFromCode(geoRegionLib, biomeCode);
            }
            else if (baseLayerType == TileLayerType.Block)
            {
                biomeDominantCategoryId = geoRegionLib.PrimaryMountains?.id;
            }

            string landformDominantCategoryId = null;
            if (baseLayerType is TileLayerType.Ground or TileLayerType.Block)
            {
                landformDominantCategoryId = ResolveDominantLandformCategoryId(geoRegionLib, landformCode, component.TileIds);
            }

            var region = WorldboxGame.I.GeoRegions.BuildGeoRegion(null);
            for (var k = 0; k < component.TileIds.Count; k++)
            {
                var tileId = component.TileIds[k];
                var tileEntity = ModClass.I.TileExtendManager.Get(tileId).E;
                tileEntity.AddRelation(new BelongToRelation
                {
                    entity = region.E,
                    layer = GeoRegionLayer.Primary
                });
            }

            EventSystemHub.Publish(new GeoRegionGeneratedEvent
            {
                WorldSeedId = evt.WorldSeedId,
                Width = width,
                Height = height,
                Layer = GeoRegionLayer.Primary,
                RegionId = region.getID(),
                BaseLayerType = baseLayerType,
                WaterKind = waterKind,
                TouchesEdge = component.TouchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                BiomeDominantCategoryId = biomeDominantCategoryId,
                LandformDominantCategoryId = landformDominantCategoryId
            });
        }
    }

    /// <summary>
    /// 收集 Primary 非水体连通块（熔岩/灰疫/山地/地表分类）。
    /// </summary>
    private static void CollectPrimaryNonWaterComponents(
        WorldTile[] tiles,
        int width,
        int height,
        byte[] primarySignature,
        int[] queue,
        List<MutableRegionComponent> components,
        int[] componentOfTile)
    {
        var visited = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            var sig = primarySignature[i];
            if (sig == 0 || sig == 1 || visited[i]) continue;

            var count = FloodFillBySignature(tiles, width, height, i, sig, primarySignature, visited, queue,
                out var sumX, out var sumY, out var touchesEdge);
            if (count <= 0) continue;

            var tileIds = new List<int>(count);
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                tileIds.Add(tileId);
                componentOfTile[tileId] = components.Count;
            }

            components.Add(new MutableRegionComponent(sig, tileIds, sumX, sumY, touchesEdge));
        }
    }

    /// <summary>
    /// 收集 Primary 水体连通块：
    /// 1) 先按“狭长水道”掩码提取河流；
    /// 2) 剩余水体按封闭性与规模判定海/湖；
    /// 3) 大连通水体按 splitCount 随机生长拆分为多个子海域/子湖域。
    /// </summary>
    private static void CollectPrimaryWaterComponents(
        WorldTile[] tiles,
        int width,
        int height,
        int worldSeedId,
        GeoRegionLibrary geoRegionLib,
        bool[] isLand,
        bool[] isWater,
        int[] queue,
        List<MutableRegionComponent> components,
        int[] componentOfTile)
    {
        var maxHalfWidth = Math.Max(1, geoRegionLib.Strait?.MaxHalfWidth ?? 1);
        var channelMask = BuildWaterChannelMask(tiles, width, height, isLand, isWater, maxHalfWidth);
        var isRiver = new bool[tiles.Length];
        var riverVisited = new bool[tiles.Length];

        var riverMinTiles = Math.Max(1, geoRegionLib.PrimaryRiver?.MinTiles ?? 12);
        var riverMaxTiles = geoRegionLib.PrimaryRiver?.MaxTiles ?? 2048;
        var riverMinAspectRatio = geoRegionLib.PrimaryRiver?.MinAspectRatio > 0f
            ? geoRegionLib.PrimaryRiver.MinAspectRatio
            : 3.0f;

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isWater[i] || !channelMask[i] || riverVisited[i]) continue;

            var count = FloodFillMask(tiles, width, height, i, channelMask, riverVisited, queue,
                out var sumX, out var sumY, out var touchesEdge, out var minX, out var minY, out var maxX, out var maxY);
            if (count <= 0) continue;

            var bboxW = maxX - minX + 1;
            var bboxH = maxY - minY + 1;
            var aspect = Math.Max(bboxW, bboxH) / (float)Math.Max(1, Math.Min(bboxW, bboxH));
            var isRiverComponent = count >= riverMinTiles &&
                                   (riverMaxTiles <= 0 || count <= riverMaxTiles) &&
                                   aspect >= riverMinAspectRatio;
            if (!isRiverComponent) continue;

            var tileIds = new List<int>(count);
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                if (!isWater[tileId]) continue;
                isRiver[tileId] = true;
                tileIds.Add(tileId);
                componentOfTile[tileId] = components.Count;
            }

            if (tileIds.Count > 0)
            {
                components.Add(new MutableRegionComponent((int)PrimaryWaterSignature.River, tileIds, sumX, sumY, touchesEdge));
            }
        }

        var nonRiverMask = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            nonRiverMask[i] = isWater[i] && !isRiver[i];
        }

        var visitedWater = new bool[tiles.Length];
        var owner = new int[tiles.Length];
        var membershipMark = new int[tiles.Length];
        var marksToken = 1;
        for (var i = 0; i < owner.Length; i++)
        {
            owner[i] = -1;
        }

        var componentOrdinal = 0;
        var lakeMinTiles = Math.Max(1, geoRegionLib.PrimaryLake?.MinTiles ?? 24);
        var closedDirectMaxTiles = Math.Max(64, lakeMinTiles * 6);

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!nonRiverMask[i] || visitedWater[i]) continue;

            var count = FloodFillMask(tiles, width, height, i, nonRiverMask, visitedWater, queue,
                out var sumX, out var sumY, out var touchesEdge);
            if (count <= 0) continue;

            componentOrdinal++;
            var boundarySideMask = ComputeBoundarySideMask(tiles, queue, count, width, height);
            var boundarySideCount = CountBoundarySides(boundarySideMask);
            var isLargeConnectedWater = boundarySideCount >= 3;
            var isClosedWater = !isLargeConnectedWater;

            var tileIds = new List<int>(count);
            for (var k = 0; k < count; k++)
            {
                tileIds.Add(queue[k]);
            }

            if (isClosedWater && count <= closedDirectMaxTiles)
            {
                var componentIndex = components.Count;
                components.Add(new MutableRegionComponent((int)PrimaryWaterSignature.Lake, tileIds, sumX, sumY, false));
                for (var k = 0; k < tileIds.Count; k++)
                {
                    componentOfTile[tileIds[k]] = componentIndex;
                }
                continue;
            }

            var signature = isLargeConnectedWater ? (int)PrimaryWaterSignature.Sea : (int)PrimaryWaterSignature.Lake;
            var componentSeed = ComputeWaterSplitSeed(worldSeedId, componentOrdinal, count, boundarySideMask);
            var splitCount = ResolveWaterSplitCount(geoRegionLib, count, isLargeConnectedWater, componentSeed);
            if (splitCount <= 1)
            {
                var componentIndex = components.Count;
                components.Add(new MutableRegionComponent(signature, tileIds, sumX, sumY, touchesEdge));
                for (var k = 0; k < tileIds.Count; k++)
                {
                    componentOfTile[tileIds[k]] = componentIndex;
                }
                continue;
            }

            marksToken++;
            if (marksToken == int.MaxValue)
            {
                Array.Clear(membershipMark, 0, membershipMark.Length);
                marksToken = 1;
            }

            for (var k = 0; k < tileIds.Count; k++)
            {
                var tileId = tileIds[k];
                membershipMark[tileId] = marksToken;
                owner[tileId] = -1;
            }

            splitCount = Math.Min(splitCount, tileIds.Count);
            var seeds = PickDistinctSeedTiles(tileIds, splitCount, componentSeed);
            GrowWaterClustersRandomly(tileIds, seeds, owner, membershipMark, marksToken, tiles, width, height, componentSeed);

            var clusterTiles = new List<int>[splitCount];
            var clusterSumX = new int[splitCount];
            var clusterSumY = new int[splitCount];
            var clusterTouchesEdge = new bool[splitCount];

            for (var k = 0; k < tileIds.Count; k++)
            {
                var tileId = tileIds[k];
                var clusterId = owner[tileId];
                if (clusterId < 0) clusterId = 0;

                clusterTiles[clusterId] ??= new List<int>(tileIds.Count / splitCount + 1);
                clusterTiles[clusterId].Add(tileId);

                var tile = tiles[tileId];
                clusterSumX[clusterId] += tile.x;
                clusterSumY[clusterId] += tile.y;
                if (tile.x == 0 || tile.y == 0 || tile.x == width - 1 || tile.y == height - 1) clusterTouchesEdge[clusterId] = true;
            }

            for (var clusterId = 0; clusterId < splitCount; clusterId++)
            {
                var cluster = clusterTiles[clusterId];
                if (cluster == null || cluster.Count == 0) continue;

                var componentIndex = components.Count;
                components.Add(new MutableRegionComponent(signature, cluster, clusterSumX[clusterId], clusterSumY[clusterId], clusterTouchesEdge[clusterId]));
                for (var k = 0; k < cluster.Count; k++)
                {
                    componentOfTile[cluster[k]] = componentIndex;
                }
            }

            for (var k = 0; k < tileIds.Count; k++)
            {
                owner[tileIds[k]] = -1;
            }
        }
    }

    /// <summary>
    /// 构建“狭长水道”掩码：用于河流候选提取与海峡候选提取。
    /// </summary>
    private static bool[] BuildWaterChannelMask(
        WorldTile[] tiles,
        int width,
        int height,
        bool[] isLand,
        bool[] isWater,
        int maxHalfWidth)
    {
        var channel = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isWater[i]) continue;

            var tile = tiles[i];
            var x = tile.x;
            var y = tile.y;

            var leftLand = x > 0 && isLand[i - 1];
            var rightLand = x < width - 1 && isLand[i + 1];
            var downLand = y > 0 && isLand[i - width];
            var upLand = y < height - 1 && isLand[i + width];

            var landAdjCount = 0;
            if (leftLand) landAdjCount++;
            if (rightLand) landAdjCount++;
            if (downLand) landAdjCount++;
            if (upLand) landAdjCount++;

            var narrowH = HasLandWithin(x, y, -1, 0, maxHalfWidth, isLand, isWater, width, height) &&
                          HasLandWithin(x, y, 1, 0, maxHalfWidth, isLand, isWater, width, height);
            var narrowV = HasLandWithin(x, y, 0, -1, maxHalfWidth, isLand, isWater, width, height) &&
                          HasLandWithin(x, y, 0, 1, maxHalfWidth, isLand, isWater, width, height);

            channel[i] = narrowH || narrowV || landAdjCount >= 3;
        }

        return channel;
    }

    /// <summary>
    /// 根据连通水体规模估算拆分数：
    /// - 基于 sqrt(size) 与 MinTiles 双约束；
    /// - 大洋类连通水体再压缩数量；
    /// - 加入少量随机抖动，避免每次形态过于固定。
    /// </summary>
    private static int ResolveWaterSplitCount(GeoRegionLibrary geoRegionLib, int size, bool isLargeConnectedWater, int randomSeed)
    {
        if (size <= 0) return 1;

        var minTiles = isLargeConnectedWater
            ? Math.Max(1, geoRegionLib.PrimarySea?.MinTiles ?? 64)
            : Math.Max(1, geoRegionLib.PrimaryLake?.MinTiles ?? 24);

        var sqrtScale = isLargeConnectedWater ? 7.0 : 8.0;
        var bySqrt = Math.Max(1, (int)Math.Round(Math.Sqrt(size) / sqrtScale));
        var byMinTiles = Math.Max(1, size / minTiles);
        var split = Math.Min(bySqrt, byMinTiles);

        if (isLargeConnectedWater)
        {
            split = Math.Max(1, (split + 3) / 4);
        }

        if (split > 1)
        {
            var jitter = Math.Abs(MixInt(randomSeed ^ 0x51F15E)) % 3 - 1;
            split = Math.Max(1, split + jitter);
        }

        if (isLargeConnectedWater && size >= minTiles * 12)
        {
            split = Math.Max(2, split);
        }

        return Math.Max(1, split);
    }

    /// <summary>
    /// 组合世界种子与连通块局部特征，得到稳定随机种子。
    /// </summary>
    private static int ComputeWaterSplitSeed(int worldSeedId, int componentOrdinal, int componentSize, int boundarySideMask)
    {
        unchecked
        {
            var hash = worldSeedId;
            hash = hash * 16777619 ^ componentOrdinal;
            hash = hash * 16777619 ^ componentSize;
            hash = hash * 16777619 ^ boundarySideMask;
            return hash;
        }
    }

    /// <summary>
    /// 统计连通块触达世界边界的方向掩码（左/右/下/上）。
    /// </summary>
    private static int ComputeBoundarySideMask(WorldTile[] tiles, int[] indices, int count, int width, int height)
    {
        var mask = 0;
        for (var i = 0; i < count; i++)
        {
            var tile = tiles[indices[i]];
            if (tile.x == 0) mask |= 1;
            if (tile.x == width - 1) mask |= 2;
            if (tile.y == 0) mask |= 4;
            if (tile.y == height - 1) mask |= 8;
            if (mask == 15) break;
        }

        return mask;
    }

    /// <summary>
    /// 统计边界方向掩码中的置位数量。
    /// </summary>
    private static int CountBoundarySides(int mask)
    {
        var count = 0;
        if ((mask & 1) != 0) count++;
        if ((mask & 2) != 0) count++;
        if ((mask & 4) != 0) count++;
        if ((mask & 8) != 0) count++;
        return count;
    }

    /// <summary>
    /// 从连通块内随机挑选互不重复的 seed tile。
    /// </summary>
    private static int[] PickDistinctSeedTiles(List<int> tileIds, int count, int seed)
    {
        if (count <= 0 || tileIds == null || tileIds.Count == 0)
        {
            return Array.Empty<int>();
        }

        count = Math.Min(count, tileIds.Count);
        var rng = new Random(seed);
        var picked = new HashSet<int>();
        var result = new int[count];
        var filled = 0;

        while (filled < count)
        {
            var idx = rng.Next(tileIds.Count);
            var tileId = tileIds[idx];
            if (!picked.Add(tileId)) continue;
            result[filled++] = tileId;
        }

        return result;
    }

    /// <summary>
    /// 在连通水体内部执行“随机前沿生长”：
    /// - 每个 seed 对应一个簇；
    /// - 每轮随机选择簇与前沿点扩张；
    /// - 最终得到形态不规则、边界更自然的分区结果。
    /// </summary>
    private static void GrowWaterClustersRandomly(
        List<int> tileIds,
        int[] seeds,
        int[] owner,
        int[] membershipMark,
        int marksToken,
        WorldTile[] tiles,
        int width,
        int height,
        int seed)
    {
        if (tileIds == null || tileIds.Count == 0 || seeds == null || seeds.Length == 0) return;

        var rng = new Random(seed ^ unchecked((int)0x9E3779B9));
        var frontiers = new List<int>[seeds.Length];
        var activeClusters = new List<int>(seeds.Length);

        for (var i = 0; i < seeds.Length; i++)
        {
            var seedTileId = seeds[i];
            owner[seedTileId] = i;
            frontiers[i] = new List<int>(32) { seedTileId };
            activeClusters.Add(i);
        }

        while (activeClusters.Count > 0)
        {
            var totalFrontier = 0;
            for (var i = 0; i < activeClusters.Count; i++)
            {
                var clusterId = activeClusters[i];
                totalFrontier += frontiers[clusterId].Count;
            }
            if (totalFrontier <= 0) break;

            var pick = rng.Next(totalFrontier);
            var activeIndex = 0;
            for (; activeIndex < activeClusters.Count; activeIndex++)
            {
                var clusterId = activeClusters[activeIndex];
                var frontierCount = frontiers[clusterId].Count;
                if (pick < frontierCount) break;
                pick -= frontierCount;
            }

            if (activeIndex >= activeClusters.Count) activeIndex = activeClusters.Count - 1;
            var ownerId = activeClusters[activeIndex];
            var frontier = frontiers[ownerId];
            if (frontier.Count <= 0)
            {
                activeClusters.RemoveAt(activeIndex);
                continue;
            }

            var frontierPick = rng.Next(frontier.Count);
            var tileId = frontier[frontierPick];
            var tile = tiles[tileId];
            var x = tile.x;
            var y = tile.y;

            var c0 = -1;
            var c1 = -1;
            var c2 = -1;
            var c3 = -1;
            var candidateCount = 0;

            if (x > 0)
            {
                var n = tileId - 1;
                if (membershipMark[n] == marksToken && owner[n] < 0)
                {
                    if (candidateCount == 0) c0 = n;
                    else if (candidateCount == 1) c1 = n;
                    else if (candidateCount == 2) c2 = n;
                    else c3 = n;
                    candidateCount++;
                }
            }
            if (x < width - 1)
            {
                var n = tileId + 1;
                if (membershipMark[n] == marksToken && owner[n] < 0)
                {
                    if (candidateCount == 0) c0 = n;
                    else if (candidateCount == 1) c1 = n;
                    else if (candidateCount == 2) c2 = n;
                    else c3 = n;
                    candidateCount++;
                }
            }
            if (y > 0)
            {
                var n = tileId - width;
                if (membershipMark[n] == marksToken && owner[n] < 0)
                {
                    if (candidateCount == 0) c0 = n;
                    else if (candidateCount == 1) c1 = n;
                    else if (candidateCount == 2) c2 = n;
                    else c3 = n;
                    candidateCount++;
                }
            }
            if (y < height - 1)
            {
                var n = tileId + width;
                if (membershipMark[n] == marksToken && owner[n] < 0)
                {
                    if (candidateCount == 0) c0 = n;
                    else if (candidateCount == 1) c1 = n;
                    else if (candidateCount == 2) c2 = n;
                    else c3 = n;
                    candidateCount++;
                }
            }

            if (candidateCount <= 0)
            {
                frontier[frontierPick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (frontier.Count == 0)
                {
                    activeClusters.RemoveAt(activeIndex);
                }
                continue;
            }

            var selected = rng.Next(candidateCount);
            var nextTileId = selected switch
            {
                0 => c0,
                1 => c1,
                2 => c2,
                _ => c3
            };

            owner[nextTileId] = ownerId;
            frontier.Add(nextTileId);
        }

        for (var i = 0; i < tileIds.Count; i++)
        {
            var tileId = tileIds[i];
            if (owner[tileId] >= 0) continue;

            var tile = tiles[tileId];
            var x = tile.x;
            var y = tile.y;
            var fallbackOwner = -1;

            if (x > 0)
            {
                var n = tileId - 1;
                if (membershipMark[n] == marksToken && owner[n] >= 0) fallbackOwner = owner[n];
            }
            if (fallbackOwner < 0 && x < width - 1)
            {
                var n = tileId + 1;
                if (membershipMark[n] == marksToken && owner[n] >= 0) fallbackOwner = owner[n];
            }
            if (fallbackOwner < 0 && y > 0)
            {
                var n = tileId - width;
                if (membershipMark[n] == marksToken && owner[n] >= 0) fallbackOwner = owner[n];
            }
            if (fallbackOwner < 0 && y < height - 1)
            {
                var n = tileId + width;
                if (membershipMark[n] == marksToken && owner[n] >= 0) fallbackOwner = owner[n];
            }

            owner[tileId] = fallbackOwner >= 0 ? fallbackOwner : 0;
        }
    }

    private static int MixInt(int value)
    {
        unchecked
        {
            var v = value;
            v ^= v >> 16;
            v *= unchecked((int)0x7FEB352D);
            v ^= v >> 15;
            v *= unchecked((int)0x846CA68B);
            v ^= v >> 16;
            return v;
        }
    }

    /// <summary>
    /// 按 Primary 签名返回对应类别的最小区域面积阈值。
    /// </summary>
    private static int ResolvePrimaryMinTilesBySignature(GeoRegionLibrary geoRegionLib, int signature)
    {
        return signature switch
        {
            (int)PrimaryWaterSignature.Sea => Math.Max(1, geoRegionLib.PrimarySea?.MinTiles ?? 64),
            (int)PrimaryWaterSignature.Lake => Math.Max(1, geoRegionLib.PrimaryLake?.MinTiles ?? 24),
            (int)PrimaryWaterSignature.River => Math.Max(1, geoRegionLib.PrimaryRiver?.MinTiles ?? 12),
            2 => Math.Max(1, geoRegionLib.PrimaryLava?.MinTiles ?? 16),
            3 => Math.Max(1, geoRegionLib.PrimaryGoo?.MinTiles ?? 16),
            4 => Math.Max(1, geoRegionLib.PrimaryMountains?.MinTiles ?? 32),
            >= 10 => Math.Max(1, PrimaryCategoryAssetFromCode(geoRegionLib, (byte)(signature - 10))?.MinTiles ?? 32),
            _ => 1
        };
    }

    /// <summary>
    /// 将 Primary 内部水体签名映射为事件中的水体细类。
    /// </summary>
    private static PrimaryWaterKind SignatureToWaterKind(int signature)
    {
        return signature switch
        {
            (int)PrimaryWaterSignature.Sea => PrimaryWaterKind.Sea,
            (int)PrimaryWaterSignature.Lake => PrimaryWaterKind.Lake,
            (int)PrimaryWaterSignature.River => PrimaryWaterKind.River,
            _ => PrimaryWaterKind.None
        };
    }

    /// <summary>
    /// 生成 Landform 层：按 landformCode 连通分量划分并合并碎片。
    /// </summary>
    private static void GenerateLandform(
        WorldGeneratedEvent evt,
        WorldTile[] tiles,
        int width,
        int height,
        GeoRegionLibrary geoRegionLib,
        byte[] landformCode,
        byte[] primaryCategoryCode,
        int[] queue)
    {
        var visited = new bool[tiles.Length];
        var components = new List<MutableRegionComponent>(256);
        var componentOfTile = new int[tiles.Length];
        for (var i = 0; i < componentOfTile.Length; i++) componentOfTile[i] = -1;

        for (var i = 0; i < tiles.Length; i++)
        {
            var sig = landformCode[i];
            if (sig == 0 || visited[i]) continue;

            var count = FloodFillBySignature(tiles, width, height, i, sig, landformCode, visited, queue,
                out var sumX, out var sumY, out var touchesEdge);
            if (count <= 0) continue;

            var tileIds = new List<int>(count);
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                tileIds.Add(tileId);
                componentOfTile[tileId] = components.Count;
            }

            components.Add(new MutableRegionComponent(sig, tileIds, sumX, sumY, touchesEdge));
        }

        MergeOrDropTinyComponents(tiles, width, height, components, componentOfTile,
            sig => ResolveLandformMinTilesBySignature(geoRegionLib, sig));

        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component.Removed || component.TileIds.Count <= 0) continue;

            var count = component.TileIds.Count;
            var centerX = (component.SumX + count / 2) / count;
            var centerY = (component.SumY + count / 2) / count;

            var biomeDominantCategoryId = ResolveDominantPrimaryCategoryId(geoRegionLib, primaryCategoryCode, component.TileIds);
            var landformDominantCategoryId = LandformCategoryIdFromCode(geoRegionLib, (byte)component.Signature);

            var region = WorldboxGame.I.GeoRegions.BuildGeoRegion(null);
            for (var k = 0; k < component.TileIds.Count; k++)
            {
                var tileId = component.TileIds[k];
                var tileEntity = ModClass.I.TileExtendManager.Get(tileId).E;
                tileEntity.AddRelation(new BelongToRelation
                {
                    entity = region.E,
                    layer = GeoRegionLayer.Landform
                });
            }

            EventSystemHub.Publish(new GeoRegionGeneratedEvent
            {
                WorldSeedId = evt.WorldSeedId,
                Width = width,
                Height = height,
                Layer = GeoRegionLayer.Landform,
                RegionId = region.getID(),
                BaseLayerType = TileLayerType.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = component.TouchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                BiomeDominantCategoryId = biomeDominantCategoryId,
                LandformDominantCategoryId = landformDominantCategoryId
            });
        }
    }

    /// <summary>
    /// 生成 Landmass 层：按陆地连通分量划分大陆/岛屿，并收集群岛候选岛屿。
    /// </summary>
    private static void GenerateLandmass(
        WorldGeneratedEvent evt,
        WorldTile[] tiles,
        int width,
        int height,
        GeoRegionLibrary geoRegionLib,
        bool[] isLand,
        byte[] primaryCategoryCode,
        byte[] landformCode,
        int[] queue,
        List<IslandInfo> islandCandidates)
    {
        var visited = new bool[tiles.Length];
        var islandMaxTiles = Math.Max(0, geoRegionLib.Archipelago?.IslandMaxTiles ?? 0);

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isLand[i] || visited[i]) continue;

            var count = FloodFillLand(tiles, width, height, i, isLand, visited, queue,
                out var sumX, out var sumY, out var touchesEdge, out var minX, out var minY, out var maxX, out var maxY);

            if (count <= 0) continue;
            var minTiles = touchesEdge
                ? Math.Max(1, geoRegionLib.LandmassMainland?.MinTiles ?? 64)
                : Math.Max(1, geoRegionLib.LandmassIsland?.MinTiles ?? 64);
            if (count < minTiles) continue;

            var centerX = (sumX + count / 2) / count;
            var centerY = (sumY + count / 2) / count;

            var biomeDominantCategoryId = ResolveDominantPrimaryCategoryId(geoRegionLib, primaryCategoryCode, queue, count);
            var landformDominantCategoryId = ResolveDominantLandformCategoryId(geoRegionLib, landformCode, queue, count);

            var region = WorldboxGame.I.GeoRegions.BuildGeoRegion(null);
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                var tileEntity = ModClass.I.TileExtendManager.Get(tileId).E;
                tileEntity.AddRelation(new BelongToRelation
                {
                    entity = region.E,
                    layer = GeoRegionLayer.Landmass
                });
            }

            EventSystemHub.Publish(new GeoRegionGeneratedEvent
            {
                WorldSeedId = evt.WorldSeedId,
                Width = width,
                Height = height,
                Layer = GeoRegionLayer.Landmass,
                RegionId = region.getID(),
                BaseLayerType = TileLayerType.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = touchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                BiomeDominantCategoryId = biomeDominantCategoryId,
                LandformDominantCategoryId = landformDominantCategoryId
            });

            if (!touchesEdge && islandMaxTiles > 0 && count <= islandMaxTiles)
            {
                var indices = new int[count];
                Array.Copy(queue, 0, indices, 0, count);
                islandCandidates.Add(new IslandInfo(indices, count, sumX, sumY, minX, minY, maxX, maxY));
            }
        }
    }

    /// <summary>
    /// 生成 Peninsula 层：
    /// 先在“近海薄陆地”中找连通块，再按海岸占比与颈部占比过滤。
    /// </summary>
    private static void GeneratePeninsula(
        WorldGeneratedEvent evt,
        WorldTile[] tiles,
        int width,
        int height,
        GeoRegionLibrary geoRegionLib,
        bool[] isLand,
        bool[] isWater,
        byte[] primaryCategoryCode,
        byte[] landformCode,
        int[] queue)
    {
        var asset = geoRegionLib.Peninsula;
        if (asset == null) return;

        var maxThickness = asset.MaxThickness;
        if (maxThickness <= 0) return;

        var dist = new byte[tiles.Length];
        var qh = 0;
        var qt = 0;

        // 多源 BFS：从临海陆地开始，向内扩展到 MaxThickness
        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isLand[i]) continue;
            if (!HasWaterNeighbor(tiles, i, width, height, isWater)) continue;
            dist[i] = 1;
            queue[qt++] = i;
        }

        while (qh < qt)
        {
            var idx = queue[qh++];
            var d = dist[idx];
            if (d >= maxThickness) continue;

            var tile = tiles[idx];
            var x = tile.x;
            var y = tile.y;

            var next = (byte)(d + 1);
            TryEnqueueLand(x - 1, y, width, height, isLand, dist, next, maxThickness, queue, ref qt);
            TryEnqueueLand(x + 1, y, width, height, isLand, dist, next, maxThickness, queue, ref qt);
            TryEnqueueLand(x, y - 1, width, height, isLand, dist, next, maxThickness, queue, ref qt);
            TryEnqueueLand(x, y + 1, width, height, isLand, dist, next, maxThickness, queue, ref qt);
        }

        var thin = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            thin[i] = isLand[i] && dist[i] > 0 && dist[i] <= maxThickness;
        }

        var visited = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            if (!thin[i] || visited[i]) continue;

            var count = FloodFillMask(tiles, width, height, i, thin, visited, queue,
                out var sumX, out var sumY, out var touchesEdge);

            if (count <= 0) continue;
            if (asset.MinTiles > 0 && count < asset.MinTiles) continue;
            if (asset.MaxTiles > 0 && count > asset.MaxTiles) continue;

            var coastTiles = 0;
            var neckEdges = 0;

            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                if (HasWaterNeighbor(tiles, tileId, width, height, isWater))
                {
                    coastTiles++;
                }

                var tile = tiles[tileId];
                var x = tile.x;
                var y = tile.y;

                // neckEdges：与“更厚的陆地”（非 thin）相连的边数
                if (x > 0 && isLand[tileId - 1] && !thin[tileId - 1]) neckEdges++;
                if (x < width - 1 && isLand[tileId + 1] && !thin[tileId + 1]) neckEdges++;
                if (y > 0 && isLand[tileId - width] && !thin[tileId - width]) neckEdges++;
                if (y < height - 1 && isLand[tileId + width] && !thin[tileId + width]) neckEdges++;
            }

            // 需要存在“颈部”连接，否则容易把小岛误判为半岛
            if (neckEdges <= 0) continue;

            var coastRatio = coastTiles / (float)count;
            var neckRatio = neckEdges / (float)count;
            if (coastRatio < asset.MinCoastRatio) continue;
            if (neckRatio > asset.MaxNeckRatio) continue;

            var centerX = (sumX + count / 2) / count;
            var centerY = (sumY + count / 2) / count;

            var biomeDominantCategoryId = ResolveDominantPrimaryCategoryId(geoRegionLib, primaryCategoryCode, queue, count);
            var landformDominantCategoryId = ResolveDominantLandformCategoryId(geoRegionLib, landformCode, queue, count);

            var region = WorldboxGame.I.GeoRegions.BuildGeoRegion(null);
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                var tileEntity = ModClass.I.TileExtendManager.Get(tileId).E;
                tileEntity.AddRelation(new BelongToRelation
                {
                    entity = region.E,
                    layer = GeoRegionLayer.Peninsula
                });
            }

            EventSystemHub.Publish(new GeoRegionGeneratedEvent
            {
                WorldSeedId = evt.WorldSeedId,
                Width = width,
                Height = height,
                Layer = GeoRegionLayer.Peninsula,
                RegionId = region.getID(),
                BaseLayerType = TileLayerType.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = touchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                BiomeDominantCategoryId = biomeDominantCategoryId,
                LandformDominantCategoryId = landformDominantCategoryId
            });
        }
    }

    /// <summary>
    /// 生成 Strait 层：
    /// 在狭水道掩码上找连通块，再按长宽比与出口数过滤。
    /// </summary>
    private static void GenerateStrait(
        WorldGeneratedEvent evt,
        WorldTile[] tiles,
        int width,
        int height,
        GeoRegionLibrary geoRegionLib,
        bool[] isLand,
        bool[] isWater,
        int[] queue)
    {
        var asset = geoRegionLib.Strait;
        if (asset == null) return;

        var maxHalfWidth = Math.Max(1, asset.MaxHalfWidth);
        var channel = new bool[tiles.Length];

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isWater[i]) continue;

            var tile = tiles[i];
            var x = tile.x;
            var y = tile.y;

            bool leftLand = x > 0 && isLand[i - 1];
            bool rightLand = x < width - 1 && isLand[i + 1];
            bool downLand = y > 0 && isLand[i - width];
            bool upLand = y < height - 1 && isLand[i + width];

            var landAdj = 0;
            if (leftLand) landAdj++;
            if (rightLand) landAdj++;
            if (downLand) landAdj++;
            if (upLand) landAdj++;

            var narrowH = HasLandWithin(x, y, -1, 0, maxHalfWidth, isLand, isWater, width, height) &&
                          HasLandWithin(x, y, 1, 0, maxHalfWidth, isLand, isWater, width, height);
            var narrowV = HasLandWithin(x, y, 0, -1, maxHalfWidth, isLand, isWater, width, height) &&
                          HasLandWithin(x, y, 0, 1, maxHalfWidth, isLand, isWater, width, height);

            channel[i] = narrowH || narrowV || landAdj >= 3;
        }

        var openWaterId = BuildOpenWaterComponents(tiles, width, height, isWater, channel, queue);

        var visited = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            if (!channel[i] || visited[i]) continue;

            var count = FloodFillMask(tiles, width, height, i, channel, visited, queue,
                out var sumX, out var sumY, out var touchesEdge, out var minX, out var minY, out var maxX, out var maxY);

            if (count <= 0) continue;
            if (asset.MinTiles > 0 && count < asset.MinTiles) continue;
            if (asset.MaxTiles > 0 && count > asset.MaxTiles) continue;

            var bboxW = maxX - minX + 1;
            var bboxH = maxY - minY + 1;
            var aspect = Math.Max(bboxW, bboxH) / (float)Math.Max(1, Math.Min(bboxW, bboxH));
            if (aspect < asset.MinAspectRatio) continue;

            var exits = new HashSet<int>();
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                var tile = tiles[tileId];
                var x = tile.x;
                var y = tile.y;

                if (x > 0 && openWaterId[tileId - 1] > 0) exits.Add(openWaterId[tileId - 1]);
                if (x < width - 1 && openWaterId[tileId + 1] > 0) exits.Add(openWaterId[tileId + 1]);
                if (y > 0 && openWaterId[tileId - width] > 0) exits.Add(openWaterId[tileId - width]);
                if (y < height - 1 && openWaterId[tileId + width] > 0) exits.Add(openWaterId[tileId + width]);
            }

            if (exits.Count < asset.MinExits) continue;

            var centerX = (sumX + count / 2) / count;
            var centerY = (sumY + count / 2) / count;

            var region = WorldboxGame.I.GeoRegions.BuildGeoRegion(null);
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                var tileEntity = ModClass.I.TileExtendManager.Get(tileId).E;
                tileEntity.AddRelation(new BelongToRelation
                {
                    entity = region.E,
                    layer = GeoRegionLayer.Strait
                });
            }

            EventSystemHub.Publish(new GeoRegionGeneratedEvent
            {
                WorldSeedId = evt.WorldSeedId,
                Width = width,
                Height = height,
                Layer = GeoRegionLayer.Strait,
                RegionId = region.getID(),
                BaseLayerType = TileLayerType.Ocean,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = touchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                BiomeDominantCategoryId = null,
                LandformDominantCategoryId = null
            });
        }
    }

    /// <summary>
    /// 生成 Archipelago 层：
    /// 对候选小岛做空间哈希聚类，簇满足阈值后生成一个可非连通群岛区域。
    /// </summary>
    private static void GenerateArchipelago(
        WorldGeneratedEvent evt,
        WorldTile[] tiles,
        int width,
        int height,
        GeoRegionLibrary geoRegionLib,
        byte[] primaryCategoryCode,
        byte[] landformCode,
        List<IslandInfo> islandCandidates)
    {
        var asset = geoRegionLib.Archipelago;
        if (asset == null) return;
        if (islandCandidates == null || islandCandidates.Count == 0) return;

        var maxGap = Math.Max(0, asset.MaxGap);
        var minIslands = Math.Max(1, asset.MinIslands);
        var minTotalTiles = Math.Max(1, asset.MinTotalTiles);

        var cellSize = Math.Max(1, maxGap + 1);
        var cellMap = new Dictionary<long, List<int>>(256);

        // 将每个岛按其 bbox 覆盖的 cell 进行索引
        for (var i = 0; i < islandCandidates.Count; i++)
        {
            var island = islandCandidates[i];
            var minCellX = FloorDiv(island.MinX, cellSize);
            var maxCellX = FloorDiv(island.MaxX, cellSize);
            var minCellY = FloorDiv(island.MinY, cellSize);
            var maxCellY = FloorDiv(island.MaxY, cellSize);

            for (var cx = minCellX; cx <= maxCellX; cx++)
            {
                for (var cy = minCellY; cy <= maxCellY; cy++)
                {
                    var key = PackCell(cx, cy);
                    if (!cellMap.TryGetValue(key, out var list))
                    {
                        list = new List<int>(4);
                        cellMap[key] = list;
                    }
                    list.Add(i);
                }
            }
        }

        var parent = new int[islandCandidates.Count];
        for (var i = 0; i < parent.Length; i++) parent[i] = i;

        for (var i = 0; i < islandCandidates.Count; i++)
        {
            var island = islandCandidates[i];
            var minCellX = FloorDiv(island.MinX - maxGap, cellSize);
            var maxCellX = FloorDiv(island.MaxX + maxGap, cellSize);
            var minCellY = FloorDiv(island.MinY - maxGap, cellSize);
            var maxCellY = FloorDiv(island.MaxY + maxGap, cellSize);

            for (var cx = minCellX; cx <= maxCellX; cx++)
            {
                for (var cy = minCellY; cy <= maxCellY; cy++)
                {
                    var key = PackCell(cx, cy);
                    if (!cellMap.TryGetValue(key, out var list)) continue;

                    foreach (var j in list)
                    {
                        if (j <= i) continue;
                        if (!IsWithinGap(islandCandidates[i], islandCandidates[j], maxGap)) continue;
                        Union(parent, i, j);
                    }
                }
            }
        }

        var clusters = new Dictionary<int, List<int>>(64);
        for (var i = 0; i < islandCandidates.Count; i++)
        {
            var root = Find(parent, i);
            if (!clusters.TryGetValue(root, out var list))
            {
                list = new List<int>(4);
                clusters[root] = list;
            }
            list.Add(i);
        }

        foreach (var cluster in clusters.Values)
        {
            if (cluster.Count < minIslands) continue;

            var totalTiles = 0;
            var sumX = 0;
            var sumY = 0;

            for (var i = 0; i < cluster.Count; i++)
            {
                var island = islandCandidates[cluster[i]];
                totalTiles += island.TileCount;
                sumX += island.SumX;
                sumY += island.SumY;
            }

            if (totalTiles < minTotalTiles) continue;

            // 创建一个可非连通的群岛 GeoRegion
            var region = WorldboxGame.I.GeoRegions.BuildGeoRegion(null);

            var primaryCounts = new int[12];
            var landformCounts = new int[5];

            for (var i = 0; i < cluster.Count; i++)
            {
                var island = islandCandidates[cluster[i]];
                for (var k = 0; k < island.TileIndices.Length; k++)
                {
                    var tileId = island.TileIndices[k];
                    var tileEntity = ModClass.I.TileExtendManager.Get(tileId).E;
                    tileEntity.AddRelation(new BelongToRelation
                    {
                        entity = region.E,
                        layer = GeoRegionLayer.Archipelago
                    });

                    var pc = primaryCategoryCode[tileId];
                    if (pc > 0 && pc < primaryCounts.Length) primaryCounts[pc]++;
                    var lc = landformCode[tileId];
                    if (lc > 0 && lc < landformCounts.Length) landformCounts[lc]++;
                }
            }

            var centerX = (sumX + totalTiles / 2) / totalTiles;
            var centerY = (sumY + totalTiles / 2) / totalTiles;

            var biomeDominantCategoryId = PrimaryCategoryIdFromCode(geoRegionLib, (byte)ArgMax(primaryCounts));
            var landformDominantCategoryId = LandformCategoryIdFromCode(geoRegionLib, (byte)ArgMax(landformCounts));

            EventSystemHub.Publish(new GeoRegionGeneratedEvent
            {
                WorldSeedId = evt.WorldSeedId,
                Width = width,
                Height = height,
                Layer = GeoRegionLayer.Archipelago,
                RegionId = region.getID(),
                BaseLayerType = TileLayerType.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = false,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = totalTiles,
                BiomeDominantCategoryId = biomeDominantCategoryId,
                LandformDominantCategoryId = landformDominantCategoryId
            });
        }
    }

    /// <summary>
    /// 按 Landform 签名返回最小面积阈值。
    /// </summary>
    private static int ResolveLandformMinTilesBySignature(GeoRegionLibrary geoRegionLib, int signature)
    {
        return signature switch
        {
            1 => Math.Max(1, geoRegionLib.LandformPlain?.MinTiles ?? 64),
            2 => Math.Max(1, geoRegionLib.LandformMountain?.MinTiles ?? 64),
            3 => Math.Max(1, geoRegionLib.LandformCanyon?.MinTiles ?? 64),
            4 => Math.Max(1, geoRegionLib.LandformBasin?.MinTiles ?? 64),
            _ => 1
        };
    }

    /// <summary>
    /// 小区域后处理：
    /// - 仅允许合并到同 signature 邻区；
    /// - 若找不到可合并目标则直接丢弃该碎片。
    /// </summary>
    private static void MergeOrDropTinyComponents(
        WorldTile[] tiles,
        int width,
        int height,
        List<MutableRegionComponent> components,
        int[] componentOfTile,
        Func<int, int> resolveMinTiles)
    {
        if (components == null || components.Count == 0) return;

        var neighborContact = new Dictionary<int, int>(16);
        var order = new List<int>(components.Count);

        var changed = true;
        while (changed)
        {
            changed = false;
            order.Clear();
            for (var i = 0; i < components.Count; i++)
            {
                if (components[i].Removed || components[i].TileIds.Count <= 0) continue;
                order.Add(i);
            }

            order.Sort((a, b) => components[a].TileIds.Count.CompareTo(components[b].TileIds.Count));

            for (var oi = 0; oi < order.Count; oi++)
            {
                var index = order[oi];
                var component = components[index];
                if (component.Removed || component.TileIds.Count <= 0) continue;

                var minTiles = Math.Max(1, resolveMinTiles(component.Signature));
                if (component.TileIds.Count >= minTiles) continue;

                neighborContact.Clear();
                for (var t = 0; t < component.TileIds.Count; t++)
                {
                    var tileId = component.TileIds[t];
                    var tile = tiles[tileId];
                    var x = tile.x;
                    var y = tile.y;

                    if (x > 0)
                    {
                        TryAccumulateNeighbor(index, tileId - 1, component.Signature, components, componentOfTile, neighborContact);
                    }
                    if (x < width - 1)
                    {
                        TryAccumulateNeighbor(index, tileId + 1, component.Signature, components, componentOfTile, neighborContact);
                    }
                    if (y > 0)
                    {
                        TryAccumulateNeighbor(index, tileId - width, component.Signature, components, componentOfTile, neighborContact);
                    }
                    if (y < height - 1)
                    {
                        TryAccumulateNeighbor(index, tileId + width, component.Signature, components, componentOfTile, neighborContact);
                    }
                }

                var bestTarget = -1;
                var bestContact = -1;
                var bestSize = -1;
                foreach (var pair in neighborContact)
                {
                    var target = pair.Key;
                    var contact = pair.Value;
                    var targetComponent = components[target];
                    var targetSize = targetComponent.TileIds.Count;

                    if (contact > bestContact || (contact == bestContact && targetSize > bestSize))
                    {
                        bestTarget = target;
                        bestContact = contact;
                        bestSize = targetSize;
                    }
                }

                if (bestTarget >= 0)
                {
                    var targetComponent = components[bestTarget];
                    targetComponent.SumX += component.SumX;
                    targetComponent.SumY += component.SumY;
                    targetComponent.TouchesEdge |= component.TouchesEdge;
                    for (var t = 0; t < component.TileIds.Count; t++)
                    {
                        var tileId = component.TileIds[t];
                        targetComponent.TileIds.Add(tileId);
                        componentOfTile[tileId] = bestTarget;
                    }
                }
                else
                {
                    for (var t = 0; t < component.TileIds.Count; t++)
                    {
                        componentOfTile[component.TileIds[t]] = -1;
                    }
                }

                component.TileIds.Clear();
                component.Removed = true;
                changed = true;
            }
        }
    }

    /// <summary>
    /// 统计候选目标邻区的接触边数量（用于挑选最佳合并目标）。
    /// </summary>
    private static void TryAccumulateNeighbor(
        int selfIndex,
        int neighborTileId,
        int signature,
        List<MutableRegionComponent> components,
        int[] componentOfTile,
        Dictionary<int, int> neighborContact)
    {
        var target = componentOfTile[neighborTileId];
        if (target < 0 || target == selfIndex) return;

        var targetComponent = components[target];
        if (targetComponent.Removed || targetComponent.TileIds.Count <= 0) return;
        if (targetComponent.Signature != signature) return;

        if (neighborContact.TryGetValue(target, out var val))
        {
            neighborContact[target] = val + 1;
        }
        else
        {
            neighborContact[target] = 1;
        }
    }

    /// <summary>
    /// 将 Primary 分类资产映射为内部编码（用于 signature 和多数投票）。
    /// </summary>
    private static byte ResolvePrimaryCategoryCode(GeoRegionLibrary geoRegionLib, GeoRegionAsset category)
    {
        if (category == null) return 10;
        if (category == geoRegionLib.PrimaryGrassland) return 1;
        if (category == geoRegionLib.PrimaryForest) return 2;
        if (category == geoRegionLib.PrimaryJungle) return 3;
        if (category == geoRegionLib.PrimarySwamp) return 4;
        if (category == geoRegionLib.PrimaryDesert) return 5;
        if (category == geoRegionLib.PrimaryTundra) return 6;
        if (category == geoRegionLib.PrimaryHighlands) return 7;
        if (category == geoRegionLib.PrimaryWasteland) return 8;
        if (category == geoRegionLib.PrimaryBeach) return 9;
        if (category == geoRegionLib.PrimarySpecial) return 10;
        if (category == geoRegionLib.PrimaryMountains) return 11;
        return 10;
    }

    /// <summary>
    /// 将 Landform 分类资产映射为内部编码。
    /// </summary>
    private static byte ResolveLandformCode(GeoRegionLibrary geoRegionLib, GeoRegionAsset landformAsset)
    {
        if (landformAsset == null) return 1;
        if (landformAsset == geoRegionLib.LandformPlain) return 1;
        if (landformAsset == geoRegionLib.LandformMountain) return 2;
        if (landformAsset == geoRegionLib.LandformCanyon) return 3;
        if (landformAsset == geoRegionLib.LandformBasin) return 4;
        return 1;
    }

    /// <summary>
    /// 将 Primary 签名转换为底层 tile layer 类型。
    /// </summary>
    private static TileLayerType SigToBaseLayerType(byte sig)
    {
        return sig switch
        {
            1 => TileLayerType.Ocean,
            2 => TileLayerType.Lava,
            3 => TileLayerType.Goo,
            4 => TileLayerType.Block,
            _ => TileLayerType.Ground
        };
    }

    /// <summary>
    /// 将 Primary 编码转回分类资产 id。
    /// </summary>
    private static string PrimaryCategoryIdFromCode(GeoRegionLibrary geoRegionLib, byte code)
    {
        return PrimaryCategoryAssetFromCode(geoRegionLib, code)?.id ?? geoRegionLib.PrimarySpecial?.id;
    }

    /// <summary>
    /// 将 Primary 编码转回分类资产对象。
    /// </summary>
    private static GeoRegionAsset PrimaryCategoryAssetFromCode(GeoRegionLibrary geoRegionLib, byte code)
    {
        return code switch
        {
            1 => geoRegionLib.PrimaryGrassland,
            2 => geoRegionLib.PrimaryForest,
            3 => geoRegionLib.PrimaryJungle,
            4 => geoRegionLib.PrimarySwamp,
            5 => geoRegionLib.PrimaryDesert,
            6 => geoRegionLib.PrimaryTundra,
            7 => geoRegionLib.PrimaryHighlands,
            8 => geoRegionLib.PrimaryWasteland,
            9 => geoRegionLib.PrimaryBeach,
            10 => geoRegionLib.PrimarySpecial,
            11 => geoRegionLib.PrimaryMountains,
            _ => geoRegionLib.PrimarySpecial
        };
    }

    /// <summary>
    /// 将 Landform 编码转回分类资产 id。
    /// </summary>
    private static string LandformCategoryIdFromCode(GeoRegionLibrary geoRegionLib, byte code)
    {
        return code switch
        {
            1 => geoRegionLib.LandformPlain?.id,
            2 => geoRegionLib.LandformMountain?.id,
            3 => geoRegionLib.LandformCanyon?.id,
            4 => geoRegionLib.LandformBasin?.id,
            _ => geoRegionLib.LandformPlain?.id
        };
    }

    /// <summary>
    /// 统计给定 tile 集合内的 Primary 主导类别（数组版本）。
    /// </summary>
    private static string ResolveDominantPrimaryCategoryId(GeoRegionLibrary geoRegionLib, byte[] primaryCategoryCode, int[] indices, int count)
    {
        var counts = new int[12];
        for (var i = 0; i < count; i++)
        {
            var code = primaryCategoryCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }
        var winner = (byte)ArgMax(counts);
        return PrimaryCategoryIdFromCode(geoRegionLib, winner);
    }

    /// <summary>
    /// 统计给定 tile 集合内的 Primary 主导类别（List 版本）。
    /// </summary>
    private static string ResolveDominantPrimaryCategoryId(GeoRegionLibrary geoRegionLib, byte[] primaryCategoryCode, List<int> indices)
    {
        var counts = new int[12];
        for (var i = 0; i < indices.Count; i++)
        {
            var code = primaryCategoryCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }
        var winner = (byte)ArgMax(counts);
        return PrimaryCategoryIdFromCode(geoRegionLib, winner);
    }

    /// <summary>
    /// 统计给定 tile 集合内的 Landform 主导类别（数组版本）。
    /// </summary>
    private static string ResolveDominantLandformCategoryId(GeoRegionLibrary geoRegionLib, byte[] landformCode, int[] indices, int count)
    {
        var counts = new int[5];
        for (var i = 0; i < count; i++)
        {
            var code = landformCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }
        var winner = (byte)ArgMax(counts);
        return LandformCategoryIdFromCode(geoRegionLib, winner);
    }

    /// <summary>
    /// 统计给定 tile 集合内的 Landform 主导类别（List 版本）。
    /// </summary>
    private static string ResolveDominantLandformCategoryId(GeoRegionLibrary geoRegionLib, byte[] landformCode, List<int> indices)
    {
        var counts = new int[5];
        for (var i = 0; i < indices.Count; i++)
        {
            var code = landformCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }
        var winner = (byte)ArgMax(counts);
        return LandformCategoryIdFromCode(geoRegionLib, winner);
    }

    /// <summary>
    /// 返回计数数组中值最大的下标。
    /// </summary>
    private static int ArgMax(int[] counts)
    {
        var bestIdx = 0;
        var bestVal = -1;
        for (var i = 0; i < counts.Length; i++)
        {
            var v = counts[i];
            if (v > bestVal)
            {
                bestVal = v;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    /// <summary>
    /// 按 signature 在 4 邻接网格上做 flood fill。
    /// </summary>
    private static int FloodFillBySignature(
        WorldTile[] tiles,
        int width,
        int height,
        int startIdx,
        byte sig,
        byte[] sigArray,
        bool[] visited,
        int[] queue,
        out int sumX,
        out int sumY,
        out bool touchesEdge)
    {
        sumX = 0;
        sumY = 0;
        touchesEdge = false;

        var head = 0;
        var tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;

        while (head < tail)
        {
            var idx = queue[head++];
            var tile = tiles[idx];

            sumX += tile.x;
            sumY += tile.y;
            if (tile.x == 0 || tile.y == 0 || tile.x == width - 1 || tile.y == height - 1) touchesEdge = true;

            var x = tile.x;
            var y = tile.y;

            if (x > 0)
            {
                var n = idx - 1;
                if (!visited[n] && sigArray[n] == sig) { visited[n] = true; queue[tail++] = n; }
            }
            if (x < width - 1)
            {
                var n = idx + 1;
                if (!visited[n] && sigArray[n] == sig) { visited[n] = true; queue[tail++] = n; }
            }
            if (y > 0)
            {
                var n = idx - width;
                if (!visited[n] && sigArray[n] == sig) { visited[n] = true; queue[tail++] = n; }
            }
            if (y < height - 1)
            {
                var n = idx + width;
                if (!visited[n] && sigArray[n] == sig) { visited[n] = true; queue[tail++] = n; }
            }
        }

        return tail;
    }

    /// <summary>
    /// 在陆地掩码上做 flood fill，并返回 bbox 与触边信息。
    /// </summary>
    private static int FloodFillLand(
        WorldTile[] tiles,
        int width,
        int height,
        int startIdx,
        bool[] isLand,
        bool[] visited,
        int[] queue,
        out int sumX,
        out int sumY,
        out bool touchesEdge,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        sumX = 0;
        sumY = 0;
        touchesEdge = false;

        var tile0 = tiles[startIdx];
        minX = maxX = tile0.x;
        minY = maxY = tile0.y;

        var head = 0;
        var tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;

        while (head < tail)
        {
            var idx = queue[head++];
            var tile = tiles[idx];
            var x = tile.x;
            var y = tile.y;

            sumX += x;
            sumY += y;
            if (x == 0 || y == 0 || x == width - 1 || y == height - 1) touchesEdge = true;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);

            if (x > 0)
            {
                var n = idx - 1;
                if (!visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (x < width - 1)
            {
                var n = idx + 1;
                if (!visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (y > 0)
            {
                var n = idx - width;
                if (!visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (y < height - 1)
            {
                var n = idx + width;
                if (!visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
        }

        return tail;
    }

    /// <summary>
    /// 在任意布尔掩码上做 flood fill（简化版本）。
    /// </summary>
    private static int FloodFillMask(
        WorldTile[] tiles,
        int width,
        int height,
        int startIdx,
        bool[] mask,
        bool[] visited,
        int[] queue,
        out int sumX,
        out int sumY,
        out bool touchesEdge)
    {
        return FloodFillMask(tiles, width, height, startIdx, mask, visited, queue, out sumX, out sumY, out touchesEdge,
            out _, out _, out _, out _);
    }

    /// <summary>
    /// 在任意布尔掩码上做 flood fill（完整版本，含 bbox 输出）。
    /// </summary>
    private static int FloodFillMask(
        WorldTile[] tiles,
        int width,
        int height,
        int startIdx,
        bool[] mask,
        bool[] visited,
        int[] queue,
        out int sumX,
        out int sumY,
        out bool touchesEdge,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        sumX = 0;
        sumY = 0;
        touchesEdge = false;

        var tile0 = tiles[startIdx];
        minX = maxX = tile0.x;
        minY = maxY = tile0.y;

        var head = 0;
        var tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;

        while (head < tail)
        {
            var idx = queue[head++];
            var tile = tiles[idx];
            var x = tile.x;
            var y = tile.y;

            sumX += x;
            sumY += y;
            if (x == 0 || y == 0 || x == width - 1 || y == height - 1) touchesEdge = true;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);

            if (x > 0)
            {
                var n = idx - 1;
                if (!visited[n] && mask[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (x < width - 1)
            {
                var n = idx + 1;
                if (!visited[n] && mask[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (y > 0)
            {
                var n = idx - width;
                if (!visited[n] && mask[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (y < height - 1)
            {
                var n = idx + width;
                if (!visited[n] && mask[n]) { visited[n] = true; queue[tail++] = n; }
            }
        }

        return tail;
    }

    /// <summary>
    /// 判断 tile 的 4 邻接是否至少有一个水体。
    /// </summary>
    private static bool HasWaterNeighbor(WorldTile[] tiles, int idx, int width, int height, bool[] isWater)
    {
        var tile = tiles[idx];
        var x = tile.x;
        var y = tile.y;

        if (x > 0 && isWater[idx - 1]) return true;
        if (x < width - 1 && isWater[idx + 1]) return true;
        if (y > 0 && isWater[idx - width]) return true;
        if (y < height - 1 && isWater[idx + width]) return true;

        return false;
    }

    /// <summary>
    /// 半岛距离场扩展时尝试将陆地邻居入队。
    /// </summary>
    private static void TryEnqueueLand(
        int x,
        int y,
        int width,
        int height,
        bool[] isLand,
        byte[] dist,
        byte nextDist,
        int maxThickness,
        int[] queue,
        ref int qt)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (nextDist > maxThickness) return;

        var idx = x + y * width;
        if (!isLand[idx]) return;
        if (dist[idx] != 0) return;

        dist[idx] = nextDist;
        queue[qt++] = idx;
    }

    /// <summary>
    /// 从某个水 tile 沿指定方向向外探测，判断给定步数内是否能碰到陆地。
    /// </summary>
    private static bool HasLandWithin(
        int x,
        int y,
        int dx,
        int dy,
        int maxSteps,
        bool[] isLand,
        bool[] isWater,
        int width,
        int height)
    {
        for (var step = 1; step <= maxSteps; step++)
        {
            var nx = x + dx * step;
            var ny = y + dy * step;
            if (nx < 0 || nx >= width || ny < 0 || ny >= height) return false;

            var nIdx = nx + ny * width;
            if (isLand[nIdx]) return true;
            if (!isWater[nIdx]) return false;
        }
        return false;
    }

    /// <summary>
    /// 对“非 channel”的开放水面分连通分量，给每块开放水面分配一个 id。
    /// </summary>
    private static int[] BuildOpenWaterComponents(
        WorldTile[] tiles,
        int width,
        int height,
        bool[] isWater,
        bool[] channel,
        int[] queue)
    {
        var openId = new int[tiles.Length];
        var visited = new bool[tiles.Length];
        var nextId = 1;

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!isWater[i] || channel[i] || visited[i]) continue;

            var head = 0;
            var tail = 0;
            queue[tail++] = i;
            visited[i] = true;
            openId[i] = nextId;

            while (head < tail)
            {
                var idx = queue[head++];
                var tile = tiles[idx];
                var x = tile.x;
                var y = tile.y;

                if (x > 0)
                {
                    var n = idx - 1;
                    if (!visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
                if (x < width - 1)
                {
                    var n = idx + 1;
                    if (!visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
                if (y > 0)
                {
                    var n = idx - width;
                    if (!visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
                if (y < height - 1)
                {
                    var n = idx + width;
                    if (!visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
            }

            nextId++;
        }

        return openId;
    }

    /// <summary>
    /// 判断两个岛屿的 bbox 间距是否不超过 maxGap（用于群岛聚类）。
    /// </summary>
    private static bool IsWithinGap(IslandInfo a, IslandInfo b, int maxGap)
    {
        var dx = 0;
        if (a.MaxX < b.MinX) dx = b.MinX - a.MaxX - 1;
        else if (b.MaxX < a.MinX) dx = a.MinX - b.MaxX - 1;

        var dy = 0;
        if (a.MaxY < b.MinY) dy = b.MinY - a.MaxY - 1;
        else if (b.MaxY < a.MinY) dy = a.MinY - b.MaxY - 1;

        var gap = Math.Max(dx, dy);
        return gap <= maxGap;
    }

    /// <summary>
    /// 并查集查找（带路径压缩）。
    /// </summary>
    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    /// <summary>
    /// 并查集合并。
    /// </summary>
    private static void Union(int[] parent, int a, int b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra == rb) return;
        parent[rb] = ra;
    }

    /// <summary>
    /// 将二维网格坐标打包成字典键。
    /// </summary>
    private static long PackCell(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }

    /// <summary>
    /// 向下取整除法（支持负数）。
    /// </summary>
    private static int FloorDiv(int value, int divisor)
    {
        if (divisor <= 0) return 0;
        if (value >= 0) return value / divisor;
        return -((-value + divisor - 1) / divisor);
    }

    /// <summary>
    /// Primary 内部水体签名编码（用于分区阶段）。
    /// </summary>
    private enum PrimaryWaterSignature
    {
        Sea = 101,
        Lake = 102,
        River = 103
    }

    /// <summary>
    /// 可变连通块数据结构：用于生成期聚合、合并、剔除。
    /// </summary>
    private sealed class MutableRegionComponent
    {
        public int Signature;
        public List<int> TileIds;
        public int SumX;
        public int SumY;
        public bool TouchesEdge;
        public bool Removed;

        public MutableRegionComponent(int signature, List<int> tileIds, int sumX, int sumY, bool touchesEdge)
        {
            Signature = signature;
            TileIds = tileIds ?? new List<int>(4);
            SumX = sumX;
            SumY = sumY;
            TouchesEdge = touchesEdge;
            Removed = false;
        }
    }

    /// <summary>
    /// 群岛候选岛屿快照（从 Landmass 层提取）。
    /// </summary>
    private readonly struct IslandInfo
    {
        public readonly int[] TileIndices;
        public readonly int TileCount;
        public readonly int SumX;
        public readonly int SumY;
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public IslandInfo(int[] tileIndices, int tileCount, int sumX, int sumY, int minX, int minY, int maxX, int maxY)
        {
            TileIndices = tileIndices ?? Array.Empty<int>();
            TileCount = tileCount;
            SumX = sumX;
            SumY = sumY;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }
    }
}

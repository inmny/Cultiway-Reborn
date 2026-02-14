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
/// </summary>
public class WorldGeneratedPartitionGeoRegionsEventSystem : GenericEventSystem<WorldGeneratedEvent>
{
    protected override int MaxEventsPerUpdate => 4;

    private int _lastWorldSeedId;
    private int _lastWidth;
    private int _lastHeight;

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

        CleanupOldGeoRegionBinders();

        var total = tiles.Length;
        var isLand = new bool[total];
        var isWater = new bool[total];
        var primaryCategoryCode = new byte[total];
        var primarySignature = new byte[total];
        var landformCode = new byte[total];

        BuildBaseArrays(tiles, width, height, geoRegionLib, isLand, isWater, primaryCategoryCode, primarySignature, landformCode);

        var queue = new int[total];

        GeneratePrimary(evt, tiles, width, height, geoRegionLib, primarySignature, landformCode, isLand, isWater, queue);
        GenerateLandform(evt, tiles, width, height, geoRegionLib, landformCode, primaryCategoryCode, queue);

        var islandCandidates = new List<IslandInfo>(64);
        GenerateLandmass(evt, tiles, width, height, geoRegionLib, isLand, primaryCategoryCode, landformCode, queue, islandCandidates);

        GeneratePeninsula(evt, tiles, width, height, geoRegionLib, isLand, isWater, primaryCategoryCode, landformCode, queue);
        GenerateStrait(evt, tiles, width, height, geoRegionLib, isLand, isWater, queue);
        GenerateArchipelago(evt, tiles, width, height, geoRegionLib, primaryCategoryCode, landformCode, islandCandidates);
    }

    private static void CleanupOldGeoRegionBinders()
    {
        var ecsWorld = ModClass.I.TileExtendManager.World;
        ecsWorld.Query<GeoRegionBinder>().ForEachEntity((ref GeoRegionBinder _, Entity e) => e.DeleteEntity());
    }

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

        // 预计算 Landform（只针对陆地，规则仅依赖 tile type/biome/邻接统计）
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

    private static int CountBoundarySides(int mask)
    {
        var count = 0;
        if ((mask & 1) != 0) count++;
        if ((mask & 2) != 0) count++;
        if ((mask & 4) != 0) count++;
        if ((mask & 8) != 0) count++;
        return count;
    }

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

    private static byte ResolveLandformCode(GeoRegionLibrary geoRegionLib, GeoRegionAsset landformAsset)
    {
        if (landformAsset == null) return 1;
        if (landformAsset == geoRegionLib.LandformPlain) return 1;
        if (landformAsset == geoRegionLib.LandformMountain) return 2;
        if (landformAsset == geoRegionLib.LandformCanyon) return 3;
        if (landformAsset == geoRegionLib.LandformBasin) return 4;
        return 1;
    }

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

    private static string PrimaryCategoryIdFromCode(GeoRegionLibrary geoRegionLib, byte code)
    {
        return PrimaryCategoryAssetFromCode(geoRegionLib, code)?.id ?? geoRegionLib.PrimarySpecial?.id;
    }

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

    private static int Find(int[] parent, int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }
        return x;
    }

    private static void Union(int[] parent, int a, int b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra == rb) return;
        parent[rb] = ra;
    }

    private static long PackCell(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }

    private static int FloorDiv(int value, int divisor)
    {
        if (divisor <= 0) return 0;
        if (value >= 0) return value / divisor;
        return -((-value + divisor - 1) / divisor);
    }

    private enum PrimaryWaterSignature
    {
        Sea = 101,
        Lake = 102,
        River = 103
    }

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

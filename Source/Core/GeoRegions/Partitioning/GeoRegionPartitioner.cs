using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Cultiway.Core.GeoRegions.Partitioning;

/// <summary>
/// 指定本次需要重新生成哪些区域层；未选中的层可沿用旧结果。
/// </summary>
[Flags]
internal enum GeoRegionGeneratedLayerMask : byte
{
    None = 0,
    Primary = 1 << (int)GeoRegionLayer.Primary,
    Landform = 1 << (int)GeoRegionLayer.Landform,
    Landmass = 1 << (int)GeoRegionLayer.Landmass,
    Peninsula = 1 << (int)GeoRegionLayer.Peninsula,
    Strait = 1 << (int)GeoRegionLayer.Strait,
    Archipelago = 1 << (int)GeoRegionLayer.Archipelago,
    Classification = Primary | Landform,
    All = Primary | Landform | Landmass | Peninsula | Strait | Archipelago
}

/// <summary>
/// 可按页共享内容的定长数组。复制数组时先复用原页，写入时只复制被改动的页，
/// 用于降低局部更新一代数据时的内存和复制开销。
/// </summary>
internal sealed class GeoRegionPagedArray<T>
{
    // 页大小固定为 2 的幂，便于通过位运算定位页和页内位置。
    private const int PageSize = 1024;
    // 数据页保存实际内容；独占标记说明当前实例可直接改写哪些页。
    private readonly T[][] pages;
    private readonly bool[] ownedPages;

    /// <summary>
    /// 创建指定长度的空数组，初始页全部由当前实例独占。
    /// </summary>
    internal GeoRegionPagedArray(int length)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        Length = length;
        int pageCount = (length + PageSize - 1) / PageSize;
        pages = new T[pageCount][];
        ownedPages = new bool[pageCount];
        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            int pageLength = Math.Min(PageSize, length - pageIndex * PageSize);
            pages[pageIndex] = new T[pageLength];
            ownedPages[pageIndex] = true;
        }
    }

    private GeoRegionPagedArray(T[][] pages, int length)
    {
        this.pages = pages;
        ownedPages = new bool[pages.Length];
        Length = length;
    }

    /// <summary>数组一共可以按格子编号读取多少项。</summary>
    internal int Length { get; }

    /// <summary>读取指定格子的值；写入共享页时会先只复制这一页，避免复制整张地图。</summary>
    internal T this[int index]
    {
        get => pages[index >> 10][index & (PageSize - 1)];
        set
        {
            int pageIndex = index >> 10;
            if (!ownedPages[pageIndex])
            {
                pages[pageIndex] = (T[])pages[pageIndex].Clone();
                ownedPages[pageIndex] = true;
            }
            pages[pageIndex][index & (PageSize - 1)] = value;
        }
    }

    /// <summary>
    /// 创建共享现有页的新数组；后续任一实例写入时只复制对应页。
    /// </summary>
    internal GeoRegionPagedArray<T> Clone()
    {
        var sharedPages = (T[][])pages.Clone();
        return new GeoRegionPagedArray<T>(sharedPages, Length);
    }
}

/// <summary>
/// 保存每个格子的陆水状态、主分类、生态和地貌等预计算结果。
/// 它随分区结果一起保留，使下一次局部更新能复用旧值并只复制被改动的页。
/// </summary>
internal sealed class GeoRegionPartitionBaseArrays
{
    /// <summary>
    /// 为指定格子数创建一组空的基础分类数组。
    /// </summary>
    internal GeoRegionPartitionBaseArrays(int tileCount)
    {
        if (tileCount <= 0) throw new ArgumentOutOfRangeException(nameof(tileCount));
        IsLand = new GeoRegionPagedArray<bool>(tileCount);
        IsWater = new GeoRegionPagedArray<bool>(tileCount);
        PrimaryCategoryCode = new GeoRegionPagedArray<byte>(tileCount);
        PrimarySignature = new GeoRegionPagedArray<int>(tileCount);
        BiomeIdentityCode = new GeoRegionPagedArray<int>(tileCount);
        LandformCode = new GeoRegionPagedArray<byte>(tileCount);
    }

    private GeoRegionPartitionBaseArrays(
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        GeoRegionPagedArray<int> primarySignature,
        GeoRegionPagedArray<int> biomeIdentityCode,
        GeoRegionPagedArray<byte> landformCode)
    {
        IsLand = isLand;
        IsWater = isWater;
        PrimaryCategoryCode = primaryCategoryCode;
        PrimarySignature = primarySignature;
        BiomeIdentityCode = biomeIdentityCode;
        LandformCode = landformCode;
    }

    /// <summary>
    /// 复制整组数组的视图并共享未修改页，供下一代结果做局部更新。
    /// </summary>
    internal GeoRegionPartitionBaseArrays Clone()
    {
        return new GeoRegionPartitionBaseArrays(
            IsLand.Clone(),
            IsWater.Clone(),
            PrimaryCategoryCode.Clone(),
            PrimarySignature.Clone(),
            BiomeIdentityCode.Clone(),
            LandformCode.Clone());
    }

    /// <summary>地图包含的格子总数。</summary>
    internal int TileCount => IsLand.Length;
    /// <summary>逐格记录这里是否属于陆地。</summary>
    internal GeoRegionPagedArray<bool> IsLand { get; private set; }
    /// <summary>逐格记录这里是否属于水域。</summary>
    internal GeoRegionPagedArray<bool> IsWater { get; private set; }
    /// <summary>逐格记录主要地表类别，例如草原、森林或沙漠。</summary>
    internal GeoRegionPagedArray<byte> PrimaryCategoryCode { get; private set; }
    /// <summary>逐格记录哪些格子可以连成同一片主要地区。</summary>
    internal GeoRegionPagedArray<int> PrimarySignature { get; private set; }
    /// <summary>逐格记录具体生物群系的稳定编号，用来发现同类中的群系变化。</summary>
    internal GeoRegionPagedArray<int> BiomeIdentityCode { get; private set; }
    /// <summary>逐格记录平原、山地、峡谷或盆地等地貌类别。</summary>
    internal GeoRegionPagedArray<byte> LandformCode { get; private set; }
}

/// <summary>
/// 根据地形和规则快照生成六层区域数据，不读取游戏运行时对象。
/// 输入是不可变快照，产出包含区域描述、格子归属索引和可供下次复用的基础数组。
/// </summary>
internal static class GeoRegionPartitioner
{
    /// <summary>
    /// 对输入世界执行完整分区，生成所有层并检查每个格子的覆盖情况。
    /// </summary>
    internal static GeoRegionPartitionResult BuildFull(
        GeoRegionPartitionInput input,
        CancellationToken cancellationToken)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var includedMask = new bool[input.Terrain.CellCount];
        for (int tileId = 0; tileId < includedMask.Length; tileId++) includedMask[tileId] = true;
        return BuildCore(
            input,
            includedMask,
            GeoRegionGeneratedLayerMask.All,
            Array.Empty<GeoRegionDescriptor>(),
            true,
            null,
            cancellationToken);
    }

    /// <summary>
    /// 按指定格子范围和区域层生成结果，并合入范围外保留的旧区域。
    /// 完整与局部分区共用此入口，以保证生成、排序和索引规则一致。
    /// </summary>
    internal static GeoRegionPartitionResult BuildCore(
        GeoRegionPartitionInput input,
        bool[] includedMask,
        GeoRegionGeneratedLayerMask generatedLayers,
        IList<GeoRegionDescriptor> retainedDescriptors,
        bool validateCoverage,
        GeoRegionPartitionBaseArrays precomputedBaseArrays,
        CancellationToken cancellationToken)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (includedMask == null || includedMask.Length != input.Terrain.CellCount)
        {
            throw new InvalidOperationException("GeoRegion included mask 尺寸不匹配");
        }
        if (retainedDescriptors == null) throw new ArgumentNullException(nameof(retainedDescriptors));
        cancellationToken.ThrowIfCancellationRequested();

        GeoRegionTerrainSnapshot terrain = input.Terrain;
        GeoRegionRuleSnapshot rules = input.Rules;
        var tiles = new GeoRegionGrid(input.Width, input.Height);
        var state = new PartitionBuildState();
        var totalTimer = Stopwatch.StartNew();
        var stageTimer = Stopwatch.StartNew();
        int total = tiles.Length;
        GeoRegionPartitionBaseArrays baseArrays = precomputedBaseArrays ?? new GeoRegionPartitionBaseArrays(total);
        if (baseArrays.TileCount != total)
        {
            throw new InvalidOperationException(
                $"GeoRegion 预计算基础数组尺寸不匹配: expected={total}, actual={baseArrays.TileCount}");
        }
        GeoRegionPagedArray<bool> isLand = baseArrays.IsLand;
        GeoRegionPagedArray<bool> isWater = baseArrays.IsWater;
        GeoRegionPagedArray<byte> primaryCategoryCode = baseArrays.PrimaryCategoryCode;
        GeoRegionPagedArray<int> primarySignature = baseArrays.PrimarySignature;
        GeoRegionPagedArray<int> biomeIdentityCode = baseArrays.BiomeIdentityCode;
        GeoRegionPagedArray<byte> landformCode = baseArrays.LandformCode;

        if (precomputedBaseArrays == null)
        {
            BuildBaseArrays(
                terrain,
                tiles,
                rules,
                isLand,
                isWater,
                primaryCategoryCode,
                primarySignature,
                biomeIdentityCode,
                landformCode);
        }
        state.BaseArrays = baseArrays;
        cancellationToken.ThrowIfCancellationRequested();
        state.BaseArraysMilliseconds = stageTimer.Elapsed.TotalMilliseconds;

        var queue = new int[total];
        stageTimer.Restart();
        if ((generatedLayers & GeoRegionGeneratedLayerMask.Primary) != 0)
        {
            GeneratePrimary(
                input,
                tiles,
                input.Width,
                input.Height,
                rules,
                primarySignature,
                primaryCategoryCode,
                landformCode,
                isLand,
                isWater,
                includedMask,
                queue,
                state.Regions);
        }
        cancellationToken.ThrowIfCancellationRequested();
        state.PrimaryMilliseconds = stageTimer.Elapsed.TotalMilliseconds;

        stageTimer.Restart();
        if ((generatedLayers & GeoRegionGeneratedLayerMask.Landform) != 0)
        {
            GenerateLandform(
                input,
                tiles,
                input.Width,
                input.Height,
                rules,
                landformCode,
                primaryCategoryCode,
                includedMask,
                queue,
                state.Regions);
        }
        cancellationToken.ThrowIfCancellationRequested();
        state.LandformMilliseconds = stageTimer.Elapsed.TotalMilliseconds;

        var islandCandidates = new List<IslandInfo>(64);
        stageTimer.Restart();
        if ((generatedLayers & (GeoRegionGeneratedLayerMask.Landmass | GeoRegionGeneratedLayerMask.Archipelago)) != 0)
        {
            GenerateLandmass(
                input,
                tiles,
                input.Width,
                input.Height,
                rules,
                isLand,
                primaryCategoryCode,
                landformCode,
                includedMask,
                queue,
                islandCandidates,
                (generatedLayers & GeoRegionGeneratedLayerMask.Landmass) != 0 ? state.Regions : null);
        }
        cancellationToken.ThrowIfCancellationRequested();
        state.LandmassMilliseconds = stageTimer.Elapsed.TotalMilliseconds;

        stageTimer.Restart();
        if ((generatedLayers & GeoRegionGeneratedLayerMask.Peninsula) != 0)
        {
            GeneratePeninsula(
                input,
                tiles,
                input.Width,
                input.Height,
                rules,
                isLand,
                isWater,
                primaryCategoryCode,
                landformCode,
                includedMask,
                queue,
                state.Regions);
        }
        cancellationToken.ThrowIfCancellationRequested();
        state.PeninsulaMilliseconds = stageTimer.Elapsed.TotalMilliseconds;

        stageTimer.Restart();
        if ((generatedLayers & GeoRegionGeneratedLayerMask.Strait) != 0)
        {
            GenerateStrait(
                input,
                tiles,
                input.Width,
                input.Height,
                rules,
                isLand,
                isWater,
                includedMask,
                queue,
                state.Regions);
        }
        cancellationToken.ThrowIfCancellationRequested();
        state.StraitMilliseconds = stageTimer.Elapsed.TotalMilliseconds;

        stageTimer.Restart();
        if ((generatedLayers & GeoRegionGeneratedLayerMask.Archipelago) != 0)
        {
            GenerateArchipelago(
                input,
                tiles,
                input.Width,
                input.Height,
                rules,
                primaryCategoryCode,
                landformCode,
                islandCandidates,
                state.Regions);
        }
        cancellationToken.ThrowIfCancellationRequested();
        state.ArchipelagoMilliseconds = stageTimer.Elapsed.TotalMilliseconds;

        AddRetainedDescriptors(state, retainedDescriptors, includedMask, generatedLayers);
        state.Regions.Sort(ComparePendingRegions);
        stageTimer.Restart();
        BuildMembershipArrays(state, total, cancellationToken);
        state.IndexMilliseconds = stageTimer.Elapsed.TotalMilliseconds;
        state.TotalMilliseconds = totalTimer.Elapsed.TotalMilliseconds;
        GeoRegionPartitionResult result = state.Complete(input, validateCoverage);
        ValidateRegularizedRegions(result, rules);
        return result;
    }

    /// <summary>
    /// 从完整地形快照计算每格基础分类，供各区域层重复使用。
    /// </summary>
    internal static GeoRegionPartitionBaseArrays CalculateBaseArrays(
        GeoRegionTerrainSnapshot terrain,
        GeoRegionRuleSnapshot rules)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (terrain.WorldSeedId != rules.WorldSeedId ||
            terrain.Width != rules.Width ||
            terrain.Height != rules.Height)
        {
            throw new InvalidOperationException("GeoRegion 基础数组的 terrain/rules 世界身份不一致");
        }

        var arrays = new GeoRegionPartitionBaseArrays(terrain.CellCount);
        BuildBaseArrays(
            terrain,
            new GeoRegionGrid(terrain.Width, terrain.Height),
            rules,
            arrays.IsLand,
            arrays.IsWater,
            arrays.PrimaryCategoryCode,
            arrays.PrimarySignature,
            arrays.BiomeIdentityCode,
            arrays.LandformCode);
        return arrays;
    }

    /// <summary>
    /// 以旧基础数组和脏格列表为输入，只重算可能受影响的邻域。
    /// 返回新一代数组，并分别给出分类变化格和陆水连接关系变化格。
    /// </summary>
    internal static GeoRegionPartitionBaseArrays CalculateBaseArraysIncremental(
        GeoRegionTerrainSnapshot terrain,
        GeoRegionRuleSnapshot rules,
        GeoRegionPartitionBaseArrays baseline,
        IList<int> dirtyTileIds,
        out int[] changedBaseTileIds,
        out int[] topologyChangedTileIds)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (baseline == null) throw new ArgumentNullException(nameof(baseline));
        if (dirtyTileIds == null) throw new ArgumentNullException(nameof(dirtyTileIds));
        if (terrain.WorldSeedId != rules.WorldSeedId || terrain.Width != rules.Width ||
            terrain.Height != rules.Height || baseline.TileCount != terrain.CellCount)
        {
            throw new InvalidOperationException("GeoRegion 局部基础数组输入身份不一致");
        }

        int beachDistance = rules.PrimaryBeach.MaxDistanceToWater;
        if (beachDistance < 0)
        {
            GeoRegionPartitionBaseArrays full = CalculateBaseArrays(terrain, rules);
            CollectBaseArrayDelta(baseline, full, out changedBaseTileIds, out topologyChangedTileIds);
            return full;
        }

        int dependencyRadius = Math.Max(1, beachDistance + 1);
        var affected = new HashSet<int>();
        for (int i = 0; i < dirtyTileIds.Count; i++)
        {
            int tileId = dirtyTileIds[i];
            if ((uint)tileId >= (uint)terrain.CellCount)
            {
                throw new ArgumentOutOfRangeException(nameof(dirtyTileIds), tileId, "GeoRegion dirty tile 越界");
            }

            int centerX = tileId % terrain.Width;
            int centerY = tileId / terrain.Width;
            int minX = Math.Max(0, centerX - dependencyRadius);
            int maxX = Math.Min(terrain.Width - 1, centerX + dependencyRadius);
            int minY = Math.Max(0, centerY - dependencyRadius);
            int maxY = Math.Min(terrain.Height - 1, centerY + dependencyRadius);
            for (int y = minY; y <= maxY; y++)
            {
                int rowOffset = y * terrain.Width;
                for (int x = minX; x <= maxX; x++) affected.Add(rowOffset + x);
            }
        }

        var orderedAffected = new List<int>(affected);
        orderedAffected.Sort();
        GeoRegionPartitionBaseArrays next = baseline.Clone();
        var beachScratch = new BeachDistanceScratch(beachDistance);
        var changed = new List<int>();
        var topologyChanged = new List<int>();
        for (int i = 0; i < orderedAffected.Count; i++)
        {
            int tileId = orderedAffected[i];
            RecalculateBaseCell(terrain, rules, next, tileId, beachDistance, beachScratch);
            bool landChanged = baseline.IsLand[tileId] != next.IsLand[tileId];
            bool waterChanged = baseline.IsWater[tileId] != next.IsWater[tileId];
            bool baseChanged = landChanged || waterChanged ||
                               baseline.PrimaryCategoryCode[tileId] != next.PrimaryCategoryCode[tileId] ||
                               baseline.PrimarySignature[tileId] != next.PrimarySignature[tileId] ||
                               baseline.BiomeIdentityCode[tileId] != next.BiomeIdentityCode[tileId] ||
                               baseline.LandformCode[tileId] != next.LandformCode[tileId];
            if (baseChanged) changed.Add(tileId);
            bool topologyChangedCell = landChanged || waterChanged ||
                                        ResolveBaseTopologyCode(baseline.PrimarySignature[tileId]) !=
                                        ResolveBaseTopologyCode(next.PrimarySignature[tileId]);
            if (topologyChangedCell) topologyChanged.Add(tileId);
        }

        changedBaseTileIds = changed.ToArray();
        topologyChangedTileIds = topologyChanged.ToArray();
        return next;
    }

    /// <summary>
    /// 在无法局部推导时比较新旧整组基础数组，产出实际变化格及连接关系变化格。
    /// </summary>
    private static void CollectBaseArrayDelta(
        GeoRegionPartitionBaseArrays baseline,
        GeoRegionPartitionBaseArrays next,
        out int[] changedBaseTileIds,
        out int[] topologyChangedTileIds)
    {
        var changed = new List<int>();
        var topologyChanged = new List<int>();
        for (int tileId = 0; tileId < baseline.TileCount; tileId++)
        {
            bool landChanged = baseline.IsLand[tileId] != next.IsLand[tileId];
            bool waterChanged = baseline.IsWater[tileId] != next.IsWater[tileId];
            if (landChanged || waterChanged ||
                baseline.PrimaryCategoryCode[tileId] != next.PrimaryCategoryCode[tileId] ||
                baseline.PrimarySignature[tileId] != next.PrimarySignature[tileId] ||
                baseline.BiomeIdentityCode[tileId] != next.BiomeIdentityCode[tileId] ||
                baseline.LandformCode[tileId] != next.LandformCode[tileId])
            {
                changed.Add(tileId);
            }
            bool topologyChangedCell = landChanged || waterChanged ||
                                        ResolveBaseTopologyCode(baseline.PrimarySignature[tileId]) !=
                                        ResolveBaseTopologyCode(next.PrimarySignature[tileId]);
            if (topologyChangedCell) topologyChanged.Add(tileId);
        }
        changedBaseTileIds = changed.ToArray();
        topologyChangedTileIds = topologyChanged.ToArray();
    }

    /// <summary>
    /// 根据一个格子及其邻近地形重新计算基础分类，并写入目标数组。
    /// 局部更新用它避免重算整个世界。
    /// </summary>
    private static void RecalculateBaseCell(
        GeoRegionTerrainSnapshot terrain,
        GeoRegionRuleSnapshot rules,
        GeoRegionPartitionBaseArrays target,
        int tileId,
        int maxBeachDistance,
        BeachDistanceScratch beachScratch)
    {
        GeoRegionTerrainCell cell = terrain.GetCell(tileId);
        bool isWater = cell.TerrainKind == GeoRegionTerrainKind.Water;
        bool isBlock = cell.TerrainKind == GeoRegionTerrainKind.Block;
        bool isLand = cell.TerrainKind is GeoRegionTerrainKind.Ground or GeoRegionTerrainKind.Block;
        target.BiomeIdentityCode[tileId] = rules.ResolveBiomeIdentityCode(cell.BiomeId);
        target.IsWater[tileId] = isWater;
        target.IsLand[tileId] = isLand;
        target.PrimaryCategoryCode[tileId] = 0;
        target.PrimarySignature[tileId] = 0;
        target.LandformCode[tileId] = 0;

        switch (cell.TerrainKind)
        {
            case GeoRegionTerrainKind.Lava:
                target.PrimarySignature[tileId] = (int)PrimarySignature.Lava;
                return;
            case GeoRegionTerrainKind.Goo:
                target.PrimarySignature[tileId] = (int)PrimarySignature.Goo;
                return;
            case GeoRegionTerrainKind.Water:
                target.PrimarySignature[tileId] = (int)PrimarySignature.UnsplitWater;
                return;
            case GeoRegionTerrainKind.Block:
                target.PrimaryCategoryCode[tileId] = (byte)GeoRegionPrimaryCategoryCode.Mountains;
                target.PrimarySignature[tileId] = (int)PrimarySignature.Block;
                break;
            case GeoRegionTerrainKind.Ground:
                break;
            default:
                target.PrimaryCategoryCode[tileId] = (byte)GeoRegionPrimaryCategoryCode.Special;
                target.PrimarySignature[tileId] = (int)PrimarySignature.Special;
                return;
        }

        int x = tileId % terrain.Width;
        int y = tileId / terrain.Width;
        int left = x > 0 ? tileId - 1 : -1;
        int right = x < terrain.Width - 1 ? tileId + 1 : -1;
        int down = y > 0 ? tileId - terrain.Width : -1;
        int up = y < terrain.Height - 1 ? tileId + terrain.Width : -1;
        int neighborWaterCount = 0;
        int neighborWater8Count = 0;
        int neighborBlockCount = 0;
        int neighborPitCount = 0;
        AccumulateTerrainNeighbor(terrain, left, ref neighborWaterCount, ref neighborWater8Count,
            ref neighborBlockCount, ref neighborPitCount);
        AccumulateTerrainNeighbor(terrain, right, ref neighborWaterCount, ref neighborWater8Count,
            ref neighborBlockCount, ref neighborPitCount);
        AccumulateTerrainNeighbor(terrain, down, ref neighborWaterCount, ref neighborWater8Count,
            ref neighborBlockCount, ref neighborPitCount);
        AccumulateTerrainNeighbor(terrain, up, ref neighborWaterCount, ref neighborWater8Count,
            ref neighborBlockCount, ref neighborPitCount);
        if (x > 0 && y > 0 && terrain.GetCell(tileId - terrain.Width - 1).TerrainKind == GeoRegionTerrainKind.Water)
            neighborWater8Count++;
        if (x < terrain.Width - 1 && y > 0 && terrain.GetCell(tileId - terrain.Width + 1).TerrainKind == GeoRegionTerrainKind.Water)
            neighborWater8Count++;
        if (x > 0 && y < terrain.Height - 1 && terrain.GetCell(tileId + terrain.Width - 1).TerrainKind == GeoRegionTerrainKind.Water)
            neighborWater8Count++;
        if (x < terrain.Width - 1 && y < terrain.Height - 1 && terrain.GetCell(tileId + terrain.Width + 1).TerrainKind == GeoRegionTerrainKind.Water)
            neighborWater8Count++;

        bool hasOppositeBlockPair = IsBlock(terrain, left) && IsBlock(terrain, right) ||
                                    IsBlock(terrain, down) && IsBlock(terrain, up);
        int distanceToWater = ResolveBeachDistance(terrain, tileId, maxBeachDistance, beachScratch);
        var context = new GeoRegionTerrainRuleContext(
            cell,
            neighborWaterCount,
            neighborWater8Count,
            distanceToWater,
            neighborBlockCount,
            neighborPitCount,
            hasOppositeBlockPair);
        target.LandformCode[tileId] = (byte)rules.ResolveLandform(context).LandformCode;
        if (!isBlock)
        {
            GeoRegionCategoryRule primary = rules.ResolvePrimaryLand(context);
            target.PrimaryCategoryCode[tileId] = (byte)primary.PrimaryCode;
            target.PrimarySignature[tileId] =
                rules.ResolvePrimaryGroundSignature(primary.PrimaryCode, cell.BiomeId);
        }
    }

    private static void AccumulateTerrainNeighbor(
        GeoRegionTerrainSnapshot terrain,
        int tileId,
        ref int neighborWaterCount,
        ref int neighborWater8Count,
        ref int neighborBlockCount,
        ref int neighborPitCount)
    {
        if (tileId < 0) return;
        GeoRegionTerrainCell cell = terrain.GetCell(tileId);
        if (cell.TerrainKind == GeoRegionTerrainKind.Water)
        {
            neighborWaterCount++;
            neighborWater8Count++;
        }
        if (cell.TerrainKind == GeoRegionTerrainKind.Block) neighborBlockCount++;
        if (cell.IsFillablePit) neighborPitCount++;
    }

    private static bool IsBlock(GeoRegionTerrainSnapshot terrain, int tileId)
    {
        return tileId >= 0 && terrain.GetCell(tileId).TerrainKind == GeoRegionTerrainKind.Block;
    }

    /// <summary>
    /// 从给定沙滩材质格向相邻格外找水，返回规则允许范围内的最短距离；找不到则返回 -1。
    /// </summary>
    private static int ResolveBeachDistance(
        GeoRegionTerrainSnapshot terrain,
        int startTileId,
        int maxDistance,
        BeachDistanceScratch scratch)
    {
        GeoRegionTerrainCell start = terrain.GetCell(startTileId);
        if (start.TerrainKind is not (GeoRegionTerrainKind.Ground or GeoRegionTerrainKind.Block) ||
            !start.IsBeachMaterial)
        {
            return -1;
        }

        int stamp = scratch.NextStamp();
        int diameter = scratch.Diameter;
        int centerX = startTileId % terrain.Width;
        int centerY = startTileId / terrain.Width;
        int head = 0;
        int tail = 0;
        scratch.Queue[tail] = startTileId;
        scratch.Distance[tail] = 0;
        tail++;
        scratch.Visited[maxDistance * diameter + maxDistance] = stamp;

        while (head < tail)
        {
            int tileId = scratch.Queue[head];
            int currentDistance = scratch.Distance[head];
            head++;
            if (HasWaterNeighbor8(terrain, tileId)) return currentDistance;
            if (currentDistance >= maxDistance) continue;

            int x = tileId % terrain.Width;
            int y = tileId / terrain.Width;
            TryEnqueueBeachCell(terrain, tileId - 1, x - 1, y, centerX, centerY,
                maxDistance, diameter, currentDistance + 1, scratch, stamp, ref tail);
            TryEnqueueBeachCell(terrain, tileId + 1, x + 1, y, centerX, centerY,
                maxDistance, diameter, currentDistance + 1, scratch, stamp, ref tail);
            TryEnqueueBeachCell(terrain, tileId - terrain.Width, x, y - 1, centerX, centerY,
                maxDistance, diameter, currentDistance + 1, scratch, stamp, ref tail);
            TryEnqueueBeachCell(terrain, tileId + terrain.Width, x, y + 1, centerX, centerY,
                maxDistance, diameter, currentDistance + 1, scratch, stamp, ref tail);
        }
        return -1;
    }

    private static void TryEnqueueBeachCell(
        GeoRegionTerrainSnapshot terrain,
        int tileId,
        int x,
        int y,
        int centerX,
        int centerY,
        int radius,
        int diameter,
        int nextDistance,
        BeachDistanceScratch scratch,
        int stamp,
        ref int tail)
    {
        if ((uint)x >= (uint)terrain.Width || (uint)y >= (uint)terrain.Height) return;
        int localX = x - centerX + radius;
        int localY = y - centerY + radius;
        if ((uint)localX >= (uint)diameter || (uint)localY >= (uint)diameter) return;
        int localIndex = localY * diameter + localX;
        if (scratch.Visited[localIndex] == stamp) return;
        GeoRegionTerrainCell cell = terrain.GetCell(tileId);
        if (cell.TerrainKind is not (GeoRegionTerrainKind.Ground or GeoRegionTerrainKind.Block) ||
            !cell.IsBeachMaterial)
        {
            return;
        }

        scratch.Visited[localIndex] = stamp;
        scratch.Queue[tail] = tileId;
        scratch.Distance[tail] = nextDistance;
        tail++;
    }

    /// <summary>
    /// 复用海滩距离搜索所需的队列、距离和访问标记，避免逐格重复分配临时数组。
    /// </summary>
    private sealed class BeachDistanceScratch
    {
        private int stamp;

        internal BeachDistanceScratch(int maxDistance)
        {
            Diameter = maxDistance * 2 + 1;
            int capacity = Math.Max(1, Diameter * Diameter);
            Queue = new int[capacity];
            Distance = new int[capacity];
            Visited = new int[capacity];
        }

        /// <summary>一次搜索覆盖的正方形边长。</summary>
        internal int Diameter { get; }
        /// <summary>保存接下来要检查的格子。</summary>
        internal int[] Queue { get; }
        /// <summary>保存每个待检查格子离起点有多远。</summary>
        internal int[] Distance { get; }
        /// <summary>标记本轮已经看过的格子，避免重复处理。</summary>
        internal int[] Visited { get; }

        /// <summary>开始新一轮搜索并返回本轮标记；编号耗尽时先清空旧标记。</summary>
        internal int NextStamp()
        {
            if (stamp == int.MaxValue)
            {
                Array.Clear(Visited, 0, Visited.Length);
                stamp = 1;
            }
            else
            {
                stamp++;
            }
            return stamp;
        }
    }

    private static bool HasWaterNeighbor8(GeoRegionTerrainSnapshot terrain, int tileId)
    {
        int x = tileId % terrain.Width;
        int y = tileId / terrain.Width;
        int minX = Math.Max(0, x - 1);
        int maxX = Math.Min(terrain.Width - 1, x + 1);
        int minY = Math.Max(0, y - 1);
        int maxY = Math.Min(terrain.Height - 1, y + 1);
        for (int ny = minY; ny <= maxY; ny++)
        {
            int rowOffset = ny * terrain.Width;
            for (int nx = minX; nx <= maxX; nx++)
            {
                if (nx == x && ny == y) continue;
                if (terrain.GetCell(rowOffset + nx).TerrainKind == GeoRegionTerrainKind.Water) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 遍历完整地形快照，产出陆水、主分类、生态和地貌等紧凑数组。
    /// 后续六层生成共享这些结果，避免重复解释地形规则。
    /// </summary>
    private static void BuildBaseArrays(
        GeoRegionTerrainSnapshot terrain,
        GeoRegionGrid tiles,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        GeoRegionPagedArray<int> primarySignature,
        GeoRegionPagedArray<int> biomeIdentityCode,
        GeoRegionPagedArray<byte> landformCode)
    {
        var isBlock = new bool[tiles.Length];
        var isPit = new bool[tiles.Length];
        var isBeachMaterial = new bool[tiles.Length];
        var beachDistance = new int[tiles.Length];

        for (int i = 0; i < tiles.Length; i++)
        {
            GeoRegionTerrainCell cell = terrain.GetCell(i);
            biomeIdentityCode[i] = rules.ResolveBiomeIdentityCode(cell.BiomeId);
            isWater[i] = cell.TerrainKind == GeoRegionTerrainKind.Water;
            isBlock[i] = cell.TerrainKind == GeoRegionTerrainKind.Block;
            isPit[i] = cell.IsFillablePit;
            isBeachMaterial[i] = cell.IsBeachMaterial;
            beachDistance[i] = -1;

            switch (cell.TerrainKind)
            {
                case GeoRegionTerrainKind.Lava:
                    primarySignature[i] = (int)PrimarySignature.Lava;
                    break;
                case GeoRegionTerrainKind.Goo:
                    primarySignature[i] = (int)PrimarySignature.Goo;
                    break;
                case GeoRegionTerrainKind.Water:
                    primarySignature[i] = (int)PrimarySignature.UnsplitWater;
                    break;
                case GeoRegionTerrainKind.Block:
                    isLand[i] = true;
                    primaryCategoryCode[i] = (byte)GeoRegionPrimaryCategoryCode.Mountains;
                    primarySignature[i] = (int)PrimarySignature.Block;
                    break;
                case GeoRegionTerrainKind.Ground:
                    isLand[i] = true;
                    break;
                default:
                    // 未知或无类型地形也必须有主区域归属，统一划入特殊类别。
                    primaryCategoryCode[i] = (byte)GeoRegionPrimaryCategoryCode.Special;
                    primarySignature[i] = (int)PrimarySignature.Special;
                    break;
            }
        }

        var beachQueue = new int[tiles.Length];
        int beachHead = 0;
        int beachTail = 0;
        for (int i = 0; i < tiles.Length; i++)
        {
            if (!isLand[i] || !isBeachMaterial[i]) continue;
            if (!HasWaterNeighbor8(tiles, i, isWater)) continue;
            beachDistance[i] = 0;
            beachQueue[beachTail++] = i;
        }

        while (beachHead < beachTail)
        {
            int index = beachQueue[beachHead++];
            int nextDistance = beachDistance[index] + 1;
            GeoRegionGridPoint tile = tiles[index];
            if (tile.X > 0) TryExpandBeachDistance(index - 1, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
            if (tile.X < tiles.Width - 1) TryExpandBeachDistance(index + 1, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
            if (tile.Y > 0) TryExpandBeachDistance(index - tiles.Width, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
            if (tile.Y < tiles.Height - 1) TryExpandBeachDistance(index + tiles.Width, nextDistance, isLand, isBeachMaterial, beachDistance, beachQueue, ref beachTail);
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            if (!isLand[i]) continue;

            GeoRegionGridPoint tile = tiles[i];
            int left = tile.X > 0 ? i - 1 : -1;
            int right = tile.X < tiles.Width - 1 ? i + 1 : -1;
            int down = tile.Y > 0 ? i - tiles.Width : -1;
            int up = tile.Y < tiles.Height - 1 ? i + tiles.Width : -1;
            int neighborWaterCount = 0;
            int neighborWater8Count = 0;
            int neighborBlockCount = 0;
            int neighborPitCount = 0;

            AccumulateCardinal(left, isWater, isBlock, isPit, ref neighborWaterCount, ref neighborWater8Count, ref neighborBlockCount, ref neighborPitCount);
            AccumulateCardinal(right, isWater, isBlock, isPit, ref neighborWaterCount, ref neighborWater8Count, ref neighborBlockCount, ref neighborPitCount);
            AccumulateCardinal(down, isWater, isBlock, isPit, ref neighborWaterCount, ref neighborWater8Count, ref neighborBlockCount, ref neighborPitCount);
            AccumulateCardinal(up, isWater, isBlock, isPit, ref neighborWaterCount, ref neighborWater8Count, ref neighborBlockCount, ref neighborPitCount);

            if (tile.X > 0 && tile.Y > 0 && isWater[i - tiles.Width - 1]) neighborWater8Count++;
            if (tile.X < tiles.Width - 1 && tile.Y > 0 && isWater[i - tiles.Width + 1]) neighborWater8Count++;
            if (tile.X > 0 && tile.Y < tiles.Height - 1 && isWater[i + tiles.Width - 1]) neighborWater8Count++;
            if (tile.X < tiles.Width - 1 && tile.Y < tiles.Height - 1 && isWater[i + tiles.Width + 1]) neighborWater8Count++;

            bool hasOppositeBlockPair = (left >= 0 && right >= 0 && isBlock[left] && isBlock[right]) ||
                                        (down >= 0 && up >= 0 && isBlock[down] && isBlock[up]);
            GeoRegionTerrainCell cell = terrain.GetCell(i);
            var context = new GeoRegionTerrainRuleContext(
                cell,
                neighborWaterCount,
                neighborWater8Count,
                beachDistance[i],
                neighborBlockCount,
                neighborPitCount,
                hasOppositeBlockPair);

            GeoRegionCategoryRule landformRule = rules.ResolveLandform(context);
            landformCode[i] = (byte)landformRule.LandformCode;
            if (cell.Layer == GeoRegionTerrainLayer.Ground)
            {
                GeoRegionCategoryRule primaryRule = rules.ResolvePrimaryLand(context);
                primaryCategoryCode[i] = (byte)primaryRule.PrimaryCode;
                primarySignature[i] = rules.ResolvePrimaryGroundSignature(primaryRule.PrimaryCode, cell.BiomeId);
            }
        }
    }

    private static void AccumulateCardinal(
        int index,
        GeoRegionPagedArray<bool> isWater,
        bool[] isBlock,
        bool[] isPit,
        ref int neighborWaterCount,
        ref int neighborWater8Count,
        ref int neighborBlockCount,
        ref int neighborPitCount)
    {
        if (index < 0) return;
        if (isWater[index])
        {
            neighborWaterCount++;
            neighborWater8Count++;
        }
        if (isBlock[index]) neighborBlockCount++;
        if (isPit[index]) neighborPitCount++;
    }

    private static bool HasWaterNeighbor8(GeoRegionGrid tiles, int tileId, GeoRegionPagedArray<bool> isWater)
    {
        GeoRegionGridPoint tile = tiles[tileId];
        int width = tiles.Width;
        int height = tiles.Height;
        if (tile.X > 0 && isWater[tileId - 1]) return true;
        if (tile.X < width - 1 && isWater[tileId + 1]) return true;
        if (tile.Y > 0 && isWater[tileId - width]) return true;
        if (tile.Y < height - 1 && isWater[tileId + width]) return true;
        if (tile.X > 0 && tile.Y > 0 && isWater[tileId - width - 1]) return true;
        if (tile.X < width - 1 && tile.Y > 0 && isWater[tileId - width + 1]) return true;
        if (tile.X > 0 && tile.Y < height - 1 && isWater[tileId + width - 1]) return true;
        if (tile.X < width - 1 && tile.Y < height - 1 && isWater[tileId + width + 1]) return true;
        return false;
    }

    private static void TryExpandBeachDistance(
        int index,
        int distance,
        GeoRegionPagedArray<bool> isLand,
        bool[] isBeachMaterial,
        int[] beachDistance,
        int[] queue,
        ref int tail)
    {
        if (!isLand[index] || !isBeachMaterial[index] || beachDistance[index] >= 0) return;
        beachDistance[index] = distance;
        queue[tail++] = index;
    }
    /// <summary>
    /// 根据每格主分类先形成原始区域，再把过小碎片并入合适的大区域，
    /// 最终产出主区域层的待提交描述。
    /// </summary>
    private static void GeneratePrimary(
        GeoRegionPartitionInput input,
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<int> primarySignature,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        GeoRegionPagedArray<byte> landformCode,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
        bool[] includedMask,
        int[] queue,
        List<PendingRegion> pendingRegions)
    {
        var components = new List<MutableRegionComponent>(256);
        var componentOfTile = new int[tiles.Length];
        for (var i = 0; i < componentOfTile.Length; i++) componentOfTile[i] = -1;

        CollectPrimaryNonWaterComponents(tiles, width, height, primarySignature, includedMask, queue, components, componentOfTile);
        CollectPrimaryWaterComponents(tiles, width, height, input.WorldSeedId, rules, isLand, isWater, includedMask, queue, components, componentOfTile);

        RegularizeComponentCoverage(
            tiles,
            width,
            height,
            components,
            componentOfTile,
            sig => ResolvePrimaryMinTilesBySignature(rules, sig),
            ResolvePrimaryPhysicalDomain);

        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component.Removed || component.TileIds.Count <= 0) continue;

            var count = component.TileIds.Count;
            var centerX = (component.SumX + count / 2) / count;
            var centerY = (component.SumY + count / 2) / count;

            var waterKind = SignatureToWaterKind(component.Signature);
            var baseLayerType = waterKind == PrimaryWaterKind.None
                ? SigToBaseTerrainLayer(component.Signature)
                : GeoRegionTerrainLayer.Ocean;

            GeoRegionPrimaryCategoryCode dominantPrimaryCode =
                ResolveDominantPrimaryCategoryCodeOrNone(rules, primaryCategoryCode, component.TileIds);

            GeoRegionLandformCode dominantLandformCode =
                ResolveDominantLandformCategoryCodeOrNone(rules, landformCode, component.TileIds);
            BuildRawComposition(
                component,
                out int[] rawSignatures,
                out int[] rawSignatureTileCounts);

            pendingRegions.Add(new PendingRegion(component.TileIds, input.Terrain, new GeoRegionDescriptorData
            {
                Layer = GeoRegionLayer.Primary,
                CategoryCode = ResolvePrimaryRegionCategory(rules, component.Signature),
                BaseTerrainLayer = baseLayerType,
                WaterKind = waterKind,
                TouchesEdge = component.TouchesEdge,
                CoreTileCount = component.CoreTileCount,
                IsMixed = component.IsMixed,
                TopologyExempt = component.TopologyExempt,
                CoreSignature = component.Signature,
                RawSignatures = rawSignatures,
                RawSignatureTileCounts = rawSignatureTileCounts,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                DominantPrimaryCode = dominantPrimaryCode,
                DominantLandformCode = dominantLandformCode
            }));
        }
    }

    /// <summary>
    /// 从熔岩、灰疫、山地和普通地表格开始，沿相邻且分类相同的格子向外寻找，
    /// 产出主区域层的非水体原始区域。
    /// </summary>
    private static void CollectPrimaryNonWaterComponents(
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionPagedArray<int> primarySignature,
        bool[] includedMask,
        int[] queue,
        List<MutableRegionComponent> components,
        int[] componentOfTile)
    {
        var visited = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            var sig = primarySignature[i];
            if (!includedMask[i] || sig == (int)PrimarySignature.UnsplitWater || visited[i]) continue;

            if (sig == (int)PrimarySignature.None)
            {
                // 保留对基础数组异常的强制兜底，避免产生覆盖空洞。
                sig = (int)PrimarySignature.Special;
                primarySignature[i] = sig;
            }

            var count = FloodFillBySignature(tiles, width, height, i, sig, primarySignature, includedMask, visited, queue,
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
    /// 收集主区域层的水体：先找出狭长河道，再按是否接触世界边缘区分海和湖，
    /// 最后把过大的连续水面从多个起点稳定划成若干子区域。
    /// </summary>
    private static void CollectPrimaryWaterComponents(
        GeoRegionGrid tiles,
        int width,
        int height,
        int worldSeedId,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
        bool[] includedMask,
        int[] queue,
        List<MutableRegionComponent> components,
        int[] componentOfTile)
    {
        var maxHalfWidth = Math.Max(1, rules.Strait.MaxHalfWidth);
        var channelMask = BuildWaterChannelMask(tiles, width, height, isLand, isWater, includedMask, maxHalfWidth);
        var isRiver = new bool[tiles.Length];
        var riverVisited = new bool[tiles.Length];

        var riverMinTiles = Math.Max(1, rules.PrimaryRiver.MinTiles);
        var riverMaxTiles = rules.PrimaryRiver.MaxTiles;
        var riverMinAspectRatio = rules.PrimaryRiver.MinAspectRatio > 0f
            ? rules.PrimaryRiver.MinAspectRatio
            : 3.0f;

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!includedMask[i] || !isWater[i] || !channelMask[i] || riverVisited[i]) continue;

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
            nonRiverMask[i] = includedMask[i] && isWater[i] && !isRiver[i];
        }

        var visitedWater = new bool[tiles.Length];
        var owner = new int[tiles.Length];
        var distance = new int[tiles.Length];
        var membershipMark = new int[tiles.Length];
        var marksToken = 1;
        for (var i = 0; i < owner.Length; i++)
        {
            owner[i] = -1;
        }

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!nonRiverMask[i] || visitedWater[i]) continue;

            var count = FloodFillMask(tiles, width, height, i, nonRiverMask, visitedWater, queue,
                out var sumX, out var sumY, out var touchesEdge);
            if (count <= 0) continue;

            var boundarySideMask = ComputeBoundarySideMask(tiles, queue, count, width, height);
            var tileIds = new List<int>(count);
            var minTileId = int.MaxValue;
            for (var k = 0; k < count; k++)
            {
                var tileId = queue[k];
                tileIds.Add(tileId);
                if (tileId < minTileId) minTileId = tileId;
            }

            var signature = touchesEdge ? (int)PrimaryWaterSignature.Sea : (int)PrimaryWaterSignature.Lake;
            var componentSeed = ComputeWaterSplitSeed(worldSeedId, minTileId, count, boundarySideMask);
            var splitCount = ResolveWaterSplitCount(rules, count, touchesEdge, componentSeed);
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
                distance[tileId] = int.MaxValue;
            }

            splitCount = Math.Min(splitCount, tileIds.Count);
            var seeds = PickDistinctSeedTiles(tileIds, splitCount, componentSeed);
            GrowWaterClustersStable(tileIds, seeds, owner, distance, membershipMark, marksToken, tiles, width, height);

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
                clusterSumX[clusterId] += tile.X;
                clusterSumY[clusterId] += tile.Y;
                if (tile.X == 0 || tile.Y == 0 || tile.X == width - 1 || tile.Y == height - 1) clusterTouchesEdge[clusterId] = true;
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
    /// 标记两侧在限定距离内有陆地的狭窄水格，
    /// 供河流和海峡阶段筛选候选范围。
    /// </summary>
    private static bool[] BuildWaterChannelMask(
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
        bool[] includedMask,
        int maxHalfWidth)
    {
        var channel = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            if (!includedMask[i] || !isWater[i]) continue;

            var tile = tiles[i];
            var x = tile.X;
            var y = tile.Y;

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
    /// 根据连续水面的面积和最小区域面积估算拆分数。
    /// 接触世界边缘的大水面会减少拆分数量，并用稳定种子做可复现的小幅调整。
    /// </summary>
    private static int ResolveWaterSplitCount(GeoRegionRuleSnapshot rules, int size, bool touchesEdge, int stableSeed)
    {
        if (size <= 0) return 1;

        var minTiles = touchesEdge
            ? Math.Max(1, rules.PrimarySea.MinTiles)
            : Math.Max(1, rules.PrimaryLake.MinTiles);

        var sqrtScale = touchesEdge ? rules.Parameters.LargeWaterSqrtScale : rules.Parameters.ClosedWaterSqrtScale;
        var bySqrt = Math.Max(1, (int)Math.Round(Math.Sqrt(size) / sqrtScale));
        var byMinTiles = Math.Max(1, size / minTiles);
        var split = Math.Min(bySqrt, byMinTiles);

        if (touchesEdge)
        {
            split = Math.Max(1, (split + rules.Parameters.LargeWaterSplitDivisor - 1) / rules.Parameters.LargeWaterSplitDivisor);
        }

        if (split > 1)
        {
            var jitterRadius = rules.Parameters.WaterSplitJitterRadius;
            var jitterRange = jitterRadius * 2 + 1;
            var jitter = (int)(unchecked((uint)MixInt(stableSeed ^ 0x51F15E)) % (uint)jitterRange) - jitterRadius;
            split = Math.Max(1, split + jitter);
        }

        if (touchesEdge && size >= minTiles * rules.Parameters.LargeWaterForcedSplitMultiplier)
        {
            split = Math.Max(2, split);
        }

        return Math.Max(1, split);
    }

    /// <summary>
    /// 组合世界种子、最小格子编号、区域大小和接触边界方向，得到可复现的局部种子。
    /// </summary>
    private static int ComputeWaterSplitSeed(int worldSeedId, int minTileId, int componentSize, int boundarySideMask)
    {
        unchecked
        {
            var hash = worldSeedId;
            hash = hash * 16777619 ^ minTileId;
            hash = hash * 16777619 ^ componentSize;
            hash = hash * 16777619 ^ boundarySideMask;
            return hash;
        }
    }

    /// <summary>
    /// 统计连通块触达世界边界的方向掩码（左/右/下/上）。
    /// </summary>
    private static int ComputeBoundarySideMask(GeoRegionGrid tiles, int[] indices, int count, int width, int height)
    {
        var mask = 0;
        for (var i = 0; i < count; i++)
        {
            var tile = tiles[indices[i]];
            if (tile.X == 0) mask |= 1;
            if (tile.X == width - 1) mask |= 2;
            if (tile.Y == 0) mask |= 4;
            if (tile.Y == height - 1) mask |= 8;
            if (mask == 15) break;
        }

        return mask;
    }

    /// <summary>
    /// 按稳定散列顺序选择互不重复的起点，保证相同输入得到相同结果。
    /// </summary>
    private static int[] PickDistinctSeedTiles(List<int> tileIds, int count, int seed)
    {
        if (count <= 0 || tileIds == null || tileIds.Count == 0)
        {
            return Array.Empty<int>();
        }

        count = Math.Min(count, tileIds.Count);
        var candidates = new List<int>(tileIds);
        candidates.Sort((left, right) =>
        {
            uint leftHash = unchecked((uint)MixInt(seed ^ left));
            uint rightHash = unchecked((uint)MixInt(seed ^ right));
            if (leftHash != rightHash) return leftHash < rightHash ? -1 : 1;
            return left.CompareTo(right);
        });

        var result = new int[count];
        for (var i = 0; i < count; i++) result[i] = candidates[i];
        return result;
    }

    /// <summary>
    /// 从多个起点向相邻水格同步扩展，按距离、起点顺序和格子编号处理竞争，
    /// 产出每个水格所属的子区域。
    /// </summary>
    private static void GrowWaterClustersStable(
        List<int> tileIds,
        int[] seeds,
        int[] owner,
        int[] distance,
        int[] membershipMark,
        int marksToken,
        GeoRegionGrid tiles,
        int width,
        int height)
    {
        if (tileIds == null || tileIds.Count == 0 || seeds == null || seeds.Length == 0)
        {
            throw new InvalidOperationException("GeoRegion 水体拆分缺少 seed");
        }

        var frontier = new StableWaterFrontier(tileIds.Count);
        for (var seedRank = 0; seedRank < seeds.Length; seedRank++)
        {
            var seedTileId = seeds[seedRank];
            owner[seedTileId] = seedRank;
            distance[seedTileId] = 0;
            frontier.Push(seedTileId, seedRank, 0);
        }

        while (frontier.TryPop(out WaterFrontierNode node))
        {
            var tileId = node.TileId;
            if (membershipMark[tileId] != marksToken ||
                owner[tileId] != node.OwnerId ||
                distance[tileId] != node.Distance)
            {
                continue;
            }

            var tile = tiles[tileId];
            var nextDistance = node.Distance + 1;
            if (tile.X > 0)
            {
                TryExpandWaterCluster(tileId - 1, node.OwnerId, nextDistance, owner, distance, membershipMark, marksToken, frontier);
            }
            if (tile.X < width - 1)
            {
                TryExpandWaterCluster(tileId + 1, node.OwnerId, nextDistance, owner, distance, membershipMark, marksToken, frontier);
            }
            if (tile.Y > 0)
            {
                TryExpandWaterCluster(tileId - width, node.OwnerId, nextDistance, owner, distance, membershipMark, marksToken, frontier);
            }
            if (tile.Y < height - 1)
            {
                TryExpandWaterCluster(tileId + width, node.OwnerId, nextDistance, owner, distance, membershipMark, marksToken, frontier);
            }
        }

        for (var i = 0; i < tileIds.Count; i++)
        {
            var tileId = tileIds[i];
            if (owner[tileId] >= 0) continue;
            throw new InvalidOperationException("GeoRegion 水体稳定扩张未覆盖 tile: tile=" + tileId);
        }
    }

    private static void TryExpandWaterCluster(
        int tileId,
        int ownerId,
        int nextDistance,
        int[] owner,
        int[] distance,
        int[] membershipMark,
        int marksToken,
        StableWaterFrontier frontier)
    {
        if (membershipMark[tileId] != marksToken) return;

        var currentDistance = distance[tileId];
        var currentOwner = owner[tileId];
        if (nextDistance > currentDistance ||
            (nextDistance == currentDistance && currentOwner >= 0 && currentOwner <= ownerId))
        {
            return;
        }

        owner[tileId] = ownerId;
        distance[tileId] = nextDistance;
        frontier.Push(tileId, ownerId, nextDistance);
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
    /// 输入主分类内部编码，返回该类成为独立区域所需的最少格子数。
    /// </summary>
    internal static int ResolvePrimaryMinTilesBySignature(GeoRegionRuleSnapshot rules, int signature)
    {
        return signature switch
        {
            (int)PrimaryWaterSignature.Sea => Math.Max(1, rules.PrimarySea.MinTiles),
            (int)PrimaryWaterSignature.Lake => Math.Max(1, rules.PrimaryLake.MinTiles),
            (int)PrimaryWaterSignature.River => Math.Max(1, rules.PrimaryRiver.MinTiles),
            (int)PrimarySignature.Lava => Math.Max(1, rules.PrimaryLava.MinTiles),
            (int)PrimarySignature.Goo => Math.Max(1, rules.PrimaryGoo.MinTiles),
            (int)PrimarySignature.Block => Math.Max(1, rules.PrimaryMountains.MinTiles),
            (int)PrimarySignature.Special => Math.Max(1, rules.PrimarySpecial.MinTiles),
            >= GeoRegionPartitionCodec.PrimaryGroundSignatureOffset =>
                Math.Max(1, rules.GetPrimaryRule(GeoRegionPartitionCodec.DecodeGroundSignature(signature)).MinTiles),
            _ => 1
        };
    }

    /// <summary>
    /// 将主区域层的水体内部编码转换为对外使用的海、湖或河流类别。
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
    /// 将主区域层的内部编码转换为统一的对外分类编码。
    /// </summary>
    private static GeoRegionCategoryCode ResolvePrimaryRegionCategory(
        GeoRegionRuleSnapshot rules,
        int signature)
    {
        return signature switch
        {
            (int)PrimaryWaterSignature.Sea => GeoRegionCategoryCode.PrimarySea,
            (int)PrimaryWaterSignature.Lake => GeoRegionCategoryCode.PrimaryLake,
            (int)PrimaryWaterSignature.River => GeoRegionCategoryCode.PrimaryRiver,
            (int)PrimarySignature.Lava => GeoRegionCategoryCode.PrimaryLava,
            (int)PrimarySignature.Goo => GeoRegionCategoryCode.PrimaryGoo,
            (int)PrimarySignature.Block => GeoRegionCategoryCode.PrimaryMountains,
            (int)PrimarySignature.Special => GeoRegionCategoryCode.PrimarySpecial,
            >= GeoRegionPartitionCodec.PrimaryGroundSignatureOffset =>
                rules.GetPrimaryRule(GeoRegionPartitionCodec.DecodeGroundSignature(signature)).CategoryCode,
            _ => GeoRegionCategoryCode.PrimarySpecial
        };
    }

    /// <summary>
    /// 按每格地貌分类形成原始区域，把过小碎片并入合适的大区域，
    /// 产出地貌层的待提交描述。
    /// </summary>
    private static void GenerateLandform(
        GeoRegionPartitionInput input,
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> landformCode,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        bool[] includedMask,
        int[] queue,
        List<PendingRegion> pendingRegions)
    {
        var visited = new bool[tiles.Length];
        var components = new List<MutableRegionComponent>(256);
        var componentOfTile = new int[tiles.Length];
        for (var i = 0; i < componentOfTile.Length; i++) componentOfTile[i] = -1;

        for (var i = 0; i < tiles.Length; i++)
        {
            var sig = landformCode[i];
            if (!includedMask[i] || sig == (byte)GeoRegionLandformCode.None || visited[i]) continue;

            var count = FloodFillBySignature(tiles, width, height, i, sig, landformCode, includedMask, visited, queue,
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

        RegularizeComponentCoverage(
            tiles,
            width,
            height,
            components,
            componentOfTile,
            sig => ResolveLandformMinTilesBySignature(rules, sig),
            _ => 1);

        for (var i = 0; i < components.Count; i++)
        {
            var component = components[i];
            if (component.Removed || component.TileIds.Count <= 0) continue;

            var count = component.TileIds.Count;
            var centerX = (component.SumX + count / 2) / count;
            var centerY = (component.SumY + count / 2) / count;

            var dominantPrimaryCode = ResolveDominantPrimaryCategoryCode(rules, primaryCategoryCode, component.TileIds);
            var dominantLandformCode = ResolveDominantLandformCategoryCode(
                rules,
                landformCode,
                component.TileIds);
            BuildRawComposition(
                component,
                out int[] rawSignatures,
                out int[] rawSignatureTileCounts);

            pendingRegions.Add(new PendingRegion(component.TileIds, input.Terrain, new GeoRegionDescriptorData
            {
                Layer = GeoRegionLayer.Landform,
                CategoryCode = rules.GetLandformRule((GeoRegionLandformCode)component.Signature).CategoryCode,
                BaseTerrainLayer = GeoRegionTerrainLayer.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = component.TouchesEdge,
                CoreTileCount = component.CoreTileCount,
                IsMixed = component.IsMixed,
                TopologyExempt = component.TopologyExempt,
                CoreSignature = component.Signature,
                RawSignatures = rawSignatures,
                RawSignatureTileCounts = rawSignatureTileCounts,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                DominantPrimaryCode = dominantPrimaryCode,
                DominantLandformCode = dominantLandformCode
            }));
        }
    }

    /// <summary>
    /// 从陆地格沿相邻格向外寻找完整陆块，按面积产出大陆或岛屿，
    /// 同时收集后续群岛阶段需要的小岛范围。
    /// </summary>
    private static void GenerateLandmass(
        GeoRegionPartitionInput input,
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        GeoRegionPagedArray<byte> landformCode,
        bool[] includedMask,
        int[] queue,
        List<IslandInfo> islandCandidates,
        List<PendingRegion> pendingRegions)
    {
        var visited = new bool[tiles.Length];
        var islandMaxTiles = Math.Max(0, rules.Archipelago.IslandMaxTiles);
        var islandMinTiles = Math.Max(1, rules.LandmassIsland.MinTiles);

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!includedMask[i] || !isLand[i] || visited[i]) continue;

            var count = FloodFillLand(tiles, width, height, i, isLand, includedMask, visited, queue,
                out var sumX, out var sumY, out var touchesEdge, out var minX, out var minY, out var maxX, out var maxY);

            if (count <= 0) continue;
            if (count < islandMinTiles) continue;

            var centerX = (sumX + count / 2) / count;
            var centerY = (sumY + count / 2) / count;

            var dominantPrimaryCode = ResolveDominantPrimaryCategoryCode(rules, primaryCategoryCode, queue, count);
            var dominantLandformCode = ResolveDominantLandformCategoryCode(rules, landformCode, queue, count);

            List<int> tileIds = CopyTileIdList(queue, count);
            pendingRegions?.Add(new PendingRegion(tileIds, input.Terrain, new GeoRegionDescriptorData
            {
                Layer = GeoRegionLayer.Landmass,
                CategoryCode = rules.ResolveLandmass(count).CategoryCode,
                BaseTerrainLayer = GeoRegionTerrainLayer.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = touchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                DominantPrimaryCode = dominantPrimaryCode,
                DominantLandformCode = dominantLandformCode
            }));

            if (!touchesEdge && islandMaxTiles > 0 && count <= islandMaxTiles)
            {
                islandCandidates.Add(new IslandInfo(tileIds, count, sumX, sumY, minX, minY, maxX, maxY));
            }
        }
    }

    /// <summary>
    /// 从临海陆地向内计算厚度，找出狭长且与更厚陆地相接的部分，
    /// 再按面积、海岸占比和连接处宽度产出半岛区域。
    /// </summary>
    private static void GeneratePeninsula(
        GeoRegionPartitionInput input,
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        GeoRegionPagedArray<byte> landformCode,
        bool[] includedMask,
        int[] queue,
        List<PendingRegion> pendingRegions)
    {
        var asset = rules.Peninsula;
        if (asset == null) return;

        var maxThickness = asset.MaxThickness;
        if (maxThickness <= 0) return;

        var dist = new byte[tiles.Length];
        var qh = 0;
        var qt = 0;

        // 从所有临海陆地同时向内推进，得到每格离海岸的最短层数。
        for (var i = 0; i < tiles.Length; i++)
        {
            if (!includedMask[i] || !isLand[i]) continue;
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
            var x = tile.X;
            var y = tile.Y;

            var next = (byte)(d + 1);
            TryEnqueueLand(x - 1, y, width, height, isLand, includedMask, dist, next, maxThickness, queue, ref qt);
            TryEnqueueLand(x + 1, y, width, height, isLand, includedMask, dist, next, maxThickness, queue, ref qt);
            TryEnqueueLand(x, y - 1, width, height, isLand, includedMask, dist, next, maxThickness, queue, ref qt);
            TryEnqueueLand(x, y + 1, width, height, isLand, includedMask, dist, next, maxThickness, queue, ref qt);
        }

        var thin = new bool[tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
        {
            thin[i] = includedMask[i] && isLand[i] && dist[i] > 0 && dist[i] <= maxThickness;
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
                var x = tile.X;
                var y = tile.Y;

                // 统计候选区与更厚陆地相接的边数，用来衡量连接处宽度。
                if (x > 0 && isLand[tileId - 1] && !thin[tileId - 1]) neckEdges++;
                if (x < width - 1 && isLand[tileId + 1] && !thin[tileId + 1]) neckEdges++;
                if (y > 0 && isLand[tileId - width] && !thin[tileId - width]) neckEdges++;
                if (y < height - 1 && isLand[tileId + width] && !thin[tileId + width]) neckEdges++;
            }

            // 必须与更厚陆地相接，否则独立小岛也会被误判为半岛。
            if (neckEdges <= 0) continue;

            var coastRatio = coastTiles / (float)count;
            var neckRatio = neckEdges / (float)count;
            if (coastRatio < asset.MinCoastRatio) continue;
            if (neckRatio > asset.MaxNeckRatio) continue;

            var centerX = (sumX + count / 2) / count;
            var centerY = (sumY + count / 2) / count;

            var dominantPrimaryCode = ResolveDominantPrimaryCategoryCode(rules, primaryCategoryCode, queue, count);
            var dominantLandformCode = ResolveDominantLandformCategoryCode(rules, landformCode, queue, count);

            pendingRegions.Add(new PendingRegion(CopyTileIdList(queue, count), input.Terrain, new GeoRegionDescriptorData
            {
                Layer = GeoRegionLayer.Peninsula,
                CategoryCode = GeoRegionCategoryCode.Peninsula,
                BaseTerrainLayer = GeoRegionTerrainLayer.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = touchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                DominantPrimaryCode = dominantPrimaryCode,
                DominantLandformCode = dominantLandformCode
            }));
        }
    }

    /// <summary>
    /// 在狭窄水格中沿相邻格寻找候选水道，再按面积、狭长度和连接的开放水面数量筛选，
    /// 产出海峡层区域。
    /// </summary>
    private static void GenerateStrait(
        GeoRegionPartitionInput input,
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
        bool[] includedMask,
        int[] queue,
        List<PendingRegion> pendingRegions)
    {
        var asset = rules.Strait;
        if (asset == null) return;

        var maxHalfWidth = Math.Max(1, asset.MaxHalfWidth);
        var channel = BuildWaterChannelMask(tiles, width, height, isLand, isWater, includedMask, maxHalfWidth);

        var openWaterId = BuildOpenWaterComponents(tiles, width, height, isWater, channel, includedMask, queue);

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
                var x = tile.X;
                var y = tile.Y;

                if (x > 0 && openWaterId[tileId - 1] > 0) exits.Add(openWaterId[tileId - 1]);
                if (x < width - 1 && openWaterId[tileId + 1] > 0) exits.Add(openWaterId[tileId + 1]);
                if (y > 0 && openWaterId[tileId - width] > 0) exits.Add(openWaterId[tileId - width]);
                if (y < height - 1 && openWaterId[tileId + width] > 0) exits.Add(openWaterId[tileId + width]);
            }

            if (exits.Count < asset.MinExits) continue;

            var centerX = (sumX + count / 2) / count;
            var centerY = (sumY + count / 2) / count;

            pendingRegions.Add(new PendingRegion(CopyTileIdList(queue, count), input.Terrain, new GeoRegionDescriptorData
            {
                Layer = GeoRegionLayer.Strait,
                CategoryCode = GeoRegionCategoryCode.Strait,
                BaseTerrainLayer = GeoRegionTerrainLayer.Ocean,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = touchesEdge,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = count,
                DominantPrimaryCode = GeoRegionPrimaryCategoryCode.None,
                DominantLandformCode = GeoRegionLandformCode.None
            }));
        }
    }

    /// <summary>
    /// 按小岛外接矩形的距离把邻近岛屿归组，
    /// 达到岛数和总面积门槛后产出可由多块陆地组成的群岛区域。
    /// </summary>
    private static void GenerateArchipelago(
        GeoRegionPartitionInput input,
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        GeoRegionPagedArray<byte> landformCode,
        List<IslandInfo> islandCandidates,
        List<PendingRegion> pendingRegions)
    {
        var asset = rules.Archipelago;
        if (asset == null) return;
        if (islandCandidates == null || islandCandidates.Count == 0) return;

        islandCandidates.Sort((left, right) => left.MinTileId.CompareTo(right.MinTileId));

        var maxGap = Math.Max(0, asset.MaxGap);
        var minIslands = Math.Max(1, asset.MinIslands);
        var minTotalTiles = Math.Max(1, asset.MinTotalTiles);

        var cellSize = Math.Max(1, maxGap + 1);
        var cellMap = new Dictionary<long, List<int>>(256);

        // 按小岛外接矩形覆盖的粗网格建索引，减少需要逐对比较的岛屿数量。
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

        var orderedClusters = new List<List<int>>(clusters.Count);
        foreach (var cluster in clusters.Values)
        {
            cluster.Sort((left, right) =>
            {
                var tileComparison = islandCandidates[left].MinTileId.CompareTo(islandCandidates[right].MinTileId);
                return tileComparison != 0 ? tileComparison : left.CompareTo(right);
            });
            orderedClusters.Add(cluster);
        }
        orderedClusters.Sort((left, right) =>
            islandCandidates[left[0]].MinTileId.CompareTo(islandCandidates[right[0]].MinTileId));

        for (var clusterIndex = 0; clusterIndex < orderedClusters.Count; clusterIndex++)
        {
            var cluster = orderedClusters[clusterIndex];
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

            var primaryCounts = new int[GeoRegionPartitionCodec.PrimaryCodeCount];
            var landformCounts = new int[GeoRegionPartitionCodec.LandformCodeCount];
            var tileIds = new List<int>(totalTiles);

            for (var i = 0; i < cluster.Count; i++)
            {
                var island = islandCandidates[cluster[i]];
                for (var k = 0; k < island.TileIndices.Count; k++)
                {
                    var tileId = island.TileIndices[k];
                    tileIds.Add(tileId);

                    var pc = primaryCategoryCode[tileId];
                    if (pc > 0 && pc < primaryCounts.Length) primaryCounts[pc]++;
                    var lc = landformCode[tileId];
                    if (lc > 0 && lc < landformCounts.Length) landformCounts[lc]++;
                }
            }

            tileIds.Sort();
            var centerX = (sumX + totalTiles / 2) / totalTiles;
            var centerY = (sumY + totalTiles / 2) / totalTiles;

            var dominantPrimaryCode = PrimaryCategoryCodeFromCode(rules, (byte)ArgMax(primaryCounts));
            var dominantLandformCode = LandformCategoryCodeFromCode(rules, (byte)ArgMax(landformCounts));

            pendingRegions.Add(new PendingRegion(tileIds, input.Terrain, new GeoRegionDescriptorData
            {
                Layer = GeoRegionLayer.Archipelago,
                CategoryCode = GeoRegionCategoryCode.Archipelago,
                BaseTerrainLayer = GeoRegionTerrainLayer.Ground,
                WaterKind = PrimaryWaterKind.None,
                TouchesEdge = false,
                CenterX = centerX,
                CenterY = centerY,
                TileCount = totalTiles,
                DominantPrimaryCode = dominantPrimaryCode,
                DominantLandformCode = dominantLandformCode
            }));
        }
    }

    /// <summary>
    /// 输入地貌内部编码，返回该类成为独立区域所需的最少格子数。
    /// </summary>
    internal static int ResolveLandformMinTilesBySignature(GeoRegionRuleSnapshot rules, int signature)
    {
        return signature switch
        {
            (int)GeoRegionLandformCode.Plain => Math.Max(1, rules.LandformPlain.MinTiles),
            (int)GeoRegionLandformCode.Mountain => Math.Max(1, rules.LandformMountain.MinTiles),
            (int)GeoRegionLandformCode.Canyon => Math.Max(1, rules.LandformCanyon.MinTiles),
            (int)GeoRegionLandformCode.Basin => Math.Max(1, rules.LandformBasin.MinTiles),
            _ => 1
        };
    }

    /// <summary>
    /// 检查主区域层和地貌层整理后的面积门槛与隔离条件，防止碎片归并产生无效区域。
    /// </summary>
    private static void ValidateRegularizedRegions(
        GeoRegionPartitionResult result,
        GeoRegionRuleSnapshot rules)
    {
        for (int regionIndex = 0; regionIndex < result.RegionCount; regionIndex++)
        {
            GeoRegionDescriptor descriptor = result.GetRegion(regionIndex);
            if (descriptor.Layer is not (GeoRegionLayer.Primary or GeoRegionLayer.Landform)) continue;

            GeoRegionCategoryRule rule = rules.GetCategoryRule(descriptor.CategoryCode) ??
                                         throw new InvalidOperationException(
                                             $"GeoRegion 正则化结果缺少规则: category={descriptor.CategoryCode}");
            int minTiles = Math.Max(1, rule.MinTiles);
            if (!descriptor.TopologyExempt && descriptor.CoreTileCount < minTiles)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 普通正式区域核心不足: layer={descriptor.Layer}, category={descriptor.CategoryCode}, " +
                    $"core={descriptor.CoreTileCount}, min={minTiles}");
            }
            if (descriptor.TopologyExempt && descriptor.CoreTileCount >= minTiles)
            {
                throw new InvalidOperationException(
                    $"GeoRegion TopologyExempt 不应达到正式门槛: layer={descriptor.Layer}, " +
                    $"category={descriptor.CategoryCode}, core={descriptor.CoreTileCount}, min={minTiles}");
            }
            if (descriptor.TopologyExempt)
            {
                ValidateTopologyExemptIsolation(result, descriptor);
            }
        }
    }

    private static void ValidateTopologyExemptIsolation(
        GeoRegionPartitionResult result,
        GeoRegionDescriptor exempt)
    {
        for (int position = 0; position < exempt.TileCount; position++)
        {
            int tileId = exempt.GetTileId(position);
            int x = tileId % result.Width;
            int y = tileId / result.Width;
            if (x > 0) ValidateTopologyExemptNeighbor(result, exempt, tileId, tileId - 1);
            if (x < result.Width - 1) ValidateTopologyExemptNeighbor(result, exempt, tileId, tileId + 1);
            if (y > 0) ValidateTopologyExemptNeighbor(result, exempt, tileId, tileId - result.Width);
            if (y < result.Height - 1) ValidateTopologyExemptNeighbor(result, exempt, tileId, tileId + result.Width);
        }
    }

    private static void ValidateTopologyExemptNeighbor(
        GeoRegionPartitionResult result,
        GeoRegionDescriptor exempt,
        int tileId,
        int neighborTileId)
    {
        if (exempt.Layer == GeoRegionLayer.Primary &&
            result.BaseArrays.IsWater[tileId] != result.BaseArrays.IsWater[neighborTileId])
        {
            return;
        }
        int neighborRegionIndex = result.GetRegionSlot(neighborTileId, exempt.Layer);
        if (neighborRegionIndex < 0) return;
        GeoRegionDescriptor neighbor = result.GetRegion(neighborRegionIndex);
        if (ReferenceEquals(exempt, neighbor) || neighbor.TopologyExempt) return;
        throw new InvalidOperationException(
            $"GeoRegion TopologyExempt 存在合法相邻正式核心: layer={exempt.Layer}, " +
            $"tile={tileId}, neighbor={neighborTileId}");
    }

    /// <summary>
    /// 把过小分类碎片并入同一陆水范围内最合适的大区域。
    /// 若一整块连续范围都没有达到面积门槛的区域，则稳定选择其中最大的一块作为保底核心，
    /// 使每个格子最终都有归属。
    /// </summary>
    private static void RegularizeComponentCoverage(
        GeoRegionGrid tiles,
        int width,
        int height,
        List<MutableRegionComponent> components,
        int[] componentOfTile,
        Func<int, int> resolveMinTiles,
        Func<int, int> resolvePhysicalDomain)
    {
        if (components == null || components.Count == 0) return;
        if (resolveMinTiles == null) throw new ArgumentNullException(nameof(resolveMinTiles));
        if (resolvePhysicalDomain == null) throw new ArgumentNullException(nameof(resolvePhysicalDomain));

        for (int i = 0; i < components.Count; i++)
        {
            MutableRegionComponent component = components[i];
            component.CoreTileCount = component.TileIds.Count;
            component.IsFormalCore = component.CoreTileCount >= Math.Max(1, resolveMinTiles(component.Signature));
        }

        var neighborContact = new Dictionary<int, int>(16);
        var order = new List<int>(components.Count);
        while (true)
        {
            order.Clear();
            for (int i = 0; i < components.Count; i++)
            {
                MutableRegionComponent component = components[i];
                if (component.Removed || component.IsFormalCore || component.TileIds.Count <= 0) continue;
                order.Add(i);
            }
            if (order.Count == 0) break;

            order.Sort((a, b) =>
            {
                int sizeComparison = components[a].CoreTileCount.CompareTo(components[b].CoreTileCount);
                if (sizeComparison != 0) return sizeComparison;
                int tileComparison = components[a].MinTileId.CompareTo(components[b].MinTileId);
                return tileComparison != 0 ? tileComparison : a.CompareTo(b);
            });

            bool absorbedAny = false;
            for (int oi = 0; oi < order.Count; oi++)
            {
                int sourceIndex = order[oi];
                MutableRegionComponent source = components[sourceIndex];
                if (source.Removed || source.IsFormalCore || source.TileIds.Count <= 0) continue;

                neighborContact.Clear();
                for (int t = 0; t < source.TileIds.Count; t++)
                {
                    int tileId = source.TileIds[t];
                    GeoRegionGridPoint tile = tiles[tileId];
                    if (tile.X > 0)
                    {
                        TryAccumulateFormalNeighbor(sourceIndex, tileId - 1, components, componentOfTile,
                            resolvePhysicalDomain, neighborContact);
                    }
                    if (tile.X < width - 1)
                    {
                        TryAccumulateFormalNeighbor(sourceIndex, tileId + 1, components, componentOfTile,
                            resolvePhysicalDomain, neighborContact);
                    }
                    if (tile.Y > 0)
                    {
                        TryAccumulateFormalNeighbor(sourceIndex, tileId - width, components, componentOfTile,
                            resolvePhysicalDomain, neighborContact);
                    }
                    if (tile.Y < height - 1)
                    {
                        TryAccumulateFormalNeighbor(sourceIndex, tileId + width, components, componentOfTile,
                            resolvePhysicalDomain, neighborContact);
                    }
                }

                int bestTarget = SelectRegularizationTarget(source, components, neighborContact);
                if (bestTarget < 0) continue;
                MergeComponentInto(sourceIndex, bestTarget, components, componentOfTile);
                absorbedAny = true;
            }

            if (absorbedAny) continue;

            // 剩余碎片与所有合格大区域隔离，先选最大的作为保底核心，再继续吸收周边碎片。
            int exemptSeed = order[0];
            for (int i = 1; i < order.Count; i++)
            {
                int candidateIndex = order[i];
                MutableRegionComponent candidate = components[candidateIndex];
                MutableRegionComponent current = components[exemptSeed];
                if (candidate.CoreTileCount > current.CoreTileCount ||
                    candidate.CoreTileCount == current.CoreTileCount && candidate.MinTileId < current.MinTileId ||
                    candidate.CoreTileCount == current.CoreTileCount && candidate.MinTileId == current.MinTileId &&
                    candidateIndex < exemptSeed)
                {
                    exemptSeed = candidateIndex;
                }
            }

            components[exemptSeed].IsFormalCore = true;
            components[exemptSeed].TopologyExempt = true;
        }
    }

    private static int SelectRegularizationTarget(
        MutableRegionComponent source,
        List<MutableRegionComponent> components,
        Dictionary<int, int> neighborContact)
    {
        int bestTarget = -1;
        int bestExemptRank = int.MaxValue;
        int bestCompatibilityRank = int.MaxValue;
        int bestContact = -1;
        int bestSize = -1;
        int bestMinTileId = int.MaxValue;
        foreach (KeyValuePair<int, int> pair in neighborContact)
        {
            int targetIndex = pair.Key;
            MutableRegionComponent target = components[targetIndex];
            int exemptRank = target.TopologyExempt ? 1 : 0;
            int compatibilityRank = target.Signature == source.Signature ? 0 : 1;
            int contact = pair.Value;
            int targetSize = target.CoreTileCount;
            int targetMinTileId = target.MinTileId;
            if (exemptRank < bestExemptRank ||
                exemptRank == bestExemptRank && compatibilityRank < bestCompatibilityRank ||
                exemptRank == bestExemptRank && compatibilityRank == bestCompatibilityRank && contact > bestContact ||
                exemptRank == bestExemptRank && compatibilityRank == bestCompatibilityRank &&
                contact == bestContact && targetSize > bestSize ||
                exemptRank == bestExemptRank && compatibilityRank == bestCompatibilityRank &&
                contact == bestContact && targetSize == bestSize && targetMinTileId < bestMinTileId ||
                exemptRank == bestExemptRank && compatibilityRank == bestCompatibilityRank &&
                contact == bestContact && targetSize == bestSize && targetMinTileId == bestMinTileId &&
                targetIndex < bestTarget)
            {
                bestTarget = targetIndex;
                bestExemptRank = exemptRank;
                bestCompatibilityRank = compatibilityRank;
                bestContact = contact;
                bestSize = targetSize;
                bestMinTileId = targetMinTileId;
            }
        }
        return bestTarget;
    }

    private static void TryAccumulateFormalNeighbor(
        int sourceIndex,
        int neighborTileId,
        List<MutableRegionComponent> components,
        int[] componentOfTile,
        Func<int, int> resolvePhysicalDomain,
        Dictionary<int, int> neighborContact)
    {
        int targetIndex = componentOfTile[neighborTileId];
        if (targetIndex < 0 || targetIndex == sourceIndex) return;

        MutableRegionComponent source = components[sourceIndex];
        MutableRegionComponent target = components[targetIndex];
        if (target.Removed || !target.IsFormalCore || target.TileIds.Count <= 0) return;
        if (resolvePhysicalDomain(source.Signature) != resolvePhysicalDomain(target.Signature)) return;

        neighborContact[targetIndex] = neighborContact.TryGetValue(targetIndex, out int contact)
            ? contact + 1
            : 1;
    }

    private static void MergeRawComposition(
        MutableRegionComponent target,
        MutableRegionComponent source)
    {
        target.RawSignatureCounts ??= new Dictionary<int, int>
        {
            [target.Signature] = target.TileIds.Count
        };
        if (source.RawSignatureCounts == null)
        {
            target.RawSignatureCounts[source.Signature] =
                target.RawSignatureCounts.TryGetValue(source.Signature, out int count)
                    ? count + source.TileIds.Count
                    : source.TileIds.Count;
            return;
        }

        foreach (KeyValuePair<int, int> pair in source.RawSignatureCounts)
        {
            target.RawSignatureCounts[pair.Key] =
                target.RawSignatureCounts.TryGetValue(pair.Key, out int count)
                    ? count + pair.Value
                    : pair.Value;
        }
    }

    private static void MergeComponentInto(
        int sourceIndex,
        int targetIndex,
        List<MutableRegionComponent> components,
        int[] componentOfTile)
    {
        MutableRegionComponent source = components[sourceIndex];
        MutableRegionComponent target = components[targetIndex];
        target.SumX += source.SumX;
        target.SumY += source.SumY;
        target.TouchesEdge |= source.TouchesEdge;
        target.MinTileId = Math.Min(target.MinTileId, source.MinTileId);
        MergeRawComposition(target, source);
        target.IsMixed = target.RawSignatureCounts?.Count > 1;
        for (int i = 0; i < source.TileIds.Count; i++)
        {
            int tileId = source.TileIds[i];
            target.TileIds.Add(tileId);
            componentOfTile[tileId] = targetIndex;
        }

        source.TileIds.Clear();
        source.Removed = true;
    }

    private static int ResolvePrimaryPhysicalDomain(int signature)
    {
        return signature is (int)PrimarySignature.UnsplitWater or
            (int)PrimaryWaterSignature.Sea or
            (int)PrimaryWaterSignature.Lake or
            (int)PrimaryWaterSignature.River
            ? 1
            : 2;
    }

    /// <summary>
    /// 将主区域层的内部编码转换为基础地形层编码。
    /// </summary>
    private static GeoRegionTerrainLayer SigToBaseTerrainLayer(int signature)
    {
        return signature switch
        {
            (int)PrimarySignature.UnsplitWater => GeoRegionTerrainLayer.Ocean,
            (int)PrimarySignature.Lava => GeoRegionTerrainLayer.Lava,
            (int)PrimarySignature.Goo => GeoRegionTerrainLayer.Goo,
            (int)PrimarySignature.Block => GeoRegionTerrainLayer.Block,
            (int)PrimarySignature.Special => GeoRegionTerrainLayer.None,
            _ => GeoRegionTerrainLayer.Ground
        };
    }

    private static GeoRegionPrimaryCategoryCode PrimaryCategoryCodeFromCode(
        GeoRegionRuleSnapshot rules,
        byte code)
    {
        return rules.GetPrimaryRule((GeoRegionPrimaryCategoryCode)code).PrimaryCode;
    }

    private static GeoRegionLandformCode LandformCategoryCodeFromCode(
        GeoRegionRuleSnapshot rules,
        byte code)
    {
        return rules.GetLandformRule((GeoRegionLandformCode)code).LandformCode;
    }

    private static void BuildRawComposition(
        MutableRegionComponent component,
        out int[] rawSignatures,
        out int[] rawSignatureTileCounts)
    {
        if (component.RawSignatureCounts == null)
        {
            rawSignatures = new[] { component.Signature };
            rawSignatureTileCounts = new[] { component.TileIds.Count };
            return;
        }

        var signatures = new List<int>(component.RawSignatureCounts.Keys);
        signatures.Sort();
        rawSignatures = signatures.ToArray();
        rawSignatureTileCounts = new int[rawSignatures.Length];
        for (int i = 0; i < rawSignatures.Length; i++)
        {
            rawSignatureTileCounts[i] = component.RawSignatureCounts[rawSignatures[i]];
        }
    }

    private static GeoRegionPrimaryCategoryCode ResolveDominantPrimaryCategoryCode(
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        int[] indices,
        int count)
    {
        var counts = new int[GeoRegionPartitionCodec.PrimaryCodeCount];
        for (int i = 0; i < count; i++)
        {
            byte code = primaryCategoryCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }

        return PrimaryCategoryCodeFromCode(rules, (byte)ArgMax(counts));
    }

    private static GeoRegionPrimaryCategoryCode ResolveDominantPrimaryCategoryCode(
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        List<int> indices)
    {
        var counts = new int[GeoRegionPartitionCodec.PrimaryCodeCount];
        for (int i = 0; i < indices.Count; i++)
        {
            byte code = primaryCategoryCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }

        return PrimaryCategoryCodeFromCode(rules, (byte)ArgMax(counts));
    }

    private static GeoRegionLandformCode ResolveDominantLandformCategoryCode(
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> landformCode,
        int[] indices,
        int count)
    {
        var counts = new int[GeoRegionPartitionCodec.LandformCodeCount];
        for (int i = 0; i < count; i++)
        {
            byte code = landformCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }

        return LandformCategoryCodeFromCode(rules, (byte)ArgMax(counts));
    }

    private static GeoRegionPrimaryCategoryCode ResolveDominantPrimaryCategoryCodeOrNone(
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> primaryCategoryCode,
        List<int> indices)
    {
        var counts = new int[GeoRegionPartitionCodec.PrimaryCodeCount];
        for (int i = 0; i < indices.Count; i++)
        {
            byte code = primaryCategoryCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }

        int codeIndex = ArgMax(counts);
        return codeIndex > 0 && counts[codeIndex] > 0
            ? PrimaryCategoryCodeFromCode(rules, (byte)codeIndex)
            : GeoRegionPrimaryCategoryCode.None;
    }

    private static GeoRegionLandformCode ResolveDominantLandformCategoryCodeOrNone(
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> landformCode,
        List<int> indices)
    {
        var counts = new int[GeoRegionPartitionCodec.LandformCodeCount];
        for (int i = 0; i < indices.Count; i++)
        {
            byte code = landformCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }

        int codeIndex = ArgMax(counts);
        return codeIndex > 0 && counts[codeIndex] > 0
            ? LandformCategoryCodeFromCode(rules, (byte)codeIndex)
            : GeoRegionLandformCode.None;
    }

    private static GeoRegionLandformCode ResolveDominantLandformCategoryCode(
        GeoRegionRuleSnapshot rules,
        GeoRegionPagedArray<byte> landformCode,
        List<int> indices)
    {
        var counts = new int[GeoRegionPartitionCodec.LandformCodeCount];
        for (int i = 0; i < indices.Count; i++)
        {
            byte code = landformCode[indices[i]];
            if (code > 0 && code < counts.Length) counts[code]++;
        }

        return LandformCategoryCodeFromCode(rules, (byte)ArgMax(counts));
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
    /// 从起点沿上下左右寻找分类相同且位于生成范围内的格子，
    /// 返回格子数，并汇总坐标和是否接触世界边缘。
    /// </summary>
    private static int FloodFillBySignature<T>(
        GeoRegionGrid tiles,
        int width,
        int height,
        int startIdx,
        T sig,
        GeoRegionPagedArray<T> sigArray,
        bool[] includedMask,
        bool[] visited,
        int[] queue,
        out int sumX,
        out int sumY,
        out bool touchesEdge)
        where T : struct, IEquatable<T>
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

            sumX += tile.X;
            sumY += tile.Y;
            if (tile.X == 0 || tile.Y == 0 || tile.X == width - 1 || tile.Y == height - 1) touchesEdge = true;

            var x = tile.X;
            var y = tile.Y;

            if (x > 0)
            {
                var n = idx - 1;
                if (includedMask[n] && !visited[n] && sigArray[n].Equals(sig)) { visited[n] = true; queue[tail++] = n; }
            }
            if (x < width - 1)
            {
                var n = idx + 1;
                if (includedMask[n] && !visited[n] && sigArray[n].Equals(sig)) { visited[n] = true; queue[tail++] = n; }
            }
            if (y > 0)
            {
                var n = idx - width;
                if (includedMask[n] && !visited[n] && sigArray[n].Equals(sig)) { visited[n] = true; queue[tail++] = n; }
            }
            if (y < height - 1)
            {
                var n = idx + width;
                if (includedMask[n] && !visited[n] && sigArray[n].Equals(sig)) { visited[n] = true; queue[tail++] = n; }
            }
        }

        return tail;
    }

    /// <summary>
    /// 从起点沿上下左右找出整块连续陆地，
    /// 返回格子数、坐标汇总、外接矩形和是否接触世界边缘。
    /// </summary>
    private static int FloodFillLand(
        GeoRegionGrid tiles,
        int width,
        int height,
        int startIdx,
        GeoRegionPagedArray<bool> isLand,
        bool[] includedMask,
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
        minX = maxX = tile0.X;
        minY = maxY = tile0.Y;

        var head = 0;
        var tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;

        while (head < tail)
        {
            var idx = queue[head++];
            var tile = tiles[idx];
            var x = tile.X;
            var y = tile.Y;

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
                if (includedMask[n] && !visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (x < width - 1)
            {
                var n = idx + 1;
                if (includedMask[n] && !visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (y > 0)
            {
                var n = idx - width;
                if (includedMask[n] && !visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
            if (y < height - 1)
            {
                var n = idx + width;
                if (includedMask[n] && !visited[n] && isLand[n]) { visited[n] = true; queue[tail++] = n; }
            }
        }

        return tail;
    }

    /// <summary>
    /// 从起点沿上下左右找出掩码内的连续范围，并返回基础统计。
    /// </summary>
    private static int FloodFillMask(
        GeoRegionGrid tiles,
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
    /// 从起点沿上下左右找出掩码内的连续范围，并额外返回外接矩形。
    /// </summary>
    private static int FloodFillMask(
        GeoRegionGrid tiles,
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
        minX = maxX = tile0.X;
        minY = maxY = tile0.Y;

        var head = 0;
        var tail = 0;
        queue[tail++] = startIdx;
        visited[startIdx] = true;

        while (head < tail)
        {
            var idx = queue[head++];
            var tile = tiles[idx];
            var x = tile.X;
            var y = tile.Y;

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
    /// 判断指定格子的上下左右是否至少有一个水格。
    /// </summary>
    private static bool HasWaterNeighbor(GeoRegionGrid tiles, int idx, int width, int height, GeoRegionPagedArray<bool> isWater)
    {
        var tile = tiles[idx];
        var x = tile.X;
        var y = tile.Y;

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
        GeoRegionPagedArray<bool> isLand,
        bool[] includedMask,
        byte[] dist,
        byte nextDist,
        int maxThickness,
        int[] queue,
        ref int qt)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (nextDist > maxThickness) return;

        var idx = x + y * width;
        if (!includedMask[idx] || !isLand[idx]) return;
        if (dist[idx] != 0) return;

        dist[idx] = nextDist;
        queue[qt++] = idx;
    }

    /// <summary>
    /// 从某个水格沿指定方向向外探测，判断给定步数内是否能碰到陆地。
    /// </summary>
    private static bool HasLandWithin(
        int x,
        int y,
        int dx,
        int dy,
        int maxSteps,
        GeoRegionPagedArray<bool> isLand,
        GeoRegionPagedArray<bool> isWater,
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
    /// 排除狭窄水道后，从相邻格向外寻找各块开放水面，
    /// 为每块水面分配编号，供海峡阶段统计两端出口。
    /// </summary>
    private static int[] BuildOpenWaterComponents(
        GeoRegionGrid tiles,
        int width,
        int height,
        GeoRegionPagedArray<bool> isWater,
        bool[] channel,
        bool[] includedMask,
        int[] queue)
    {
        var openId = new int[tiles.Length];
        var visited = new bool[tiles.Length];
        var nextId = 1;

        for (var i = 0; i < tiles.Length; i++)
        {
            if (!includedMask[i] || !isWater[i] || channel[i] || visited[i]) continue;

            var head = 0;
            var tail = 0;
            queue[tail++] = i;
            visited[i] = true;
            openId[i] = nextId;

            while (head < tail)
            {
                var idx = queue[head++];
                var tile = tiles[idx];
                var x = tile.X;
                var y = tile.Y;

                if (x > 0)
                {
                    var n = idx - 1;
                    if (includedMask[n] && !visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
                if (x < width - 1)
                {
                    var n = idx + 1;
                    if (includedMask[n] && !visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
                if (y > 0)
                {
                    var n = idx - width;
                    if (includedMask[n] && !visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
                if (y < height - 1)
                {
                    var n = idx + width;
                    if (includedMask[n] && !visited[n] && isWater[n] && !channel[n]) { visited[n] = true; openId[n] = nextId; queue[tail++] = n; }
                }
            }

            nextId++;
        }

        return openId;
    }

    /// <summary>
    /// 判断两个岛屿外接矩形的间距是否在群岛规则允许范围内。
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
    /// 查找小岛当前所属组，并顺便缩短后续查找路径。
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
    /// 把两个小岛所在的组稳定合为一组。
    /// </summary>
    private static void Union(int[] parent, int a, int b)
    {
        var ra = Find(parent, a);
        var rb = Find(parent, b);
        if (ra == rb) return;
        if (ra < rb) parent[rb] = ra;
        else parent[ra] = rb;
    }

    /// <summary>
    /// 将二维网格坐标打包成字典键。
    /// </summary>
    private static long PackCell(int x, int y)
    {
        return ((long)x << 32) ^ (uint)y;
    }

    private static List<int> CopyTileIdList(int[] source, int count)
    {
        var result = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            result.Add(source[i]);
        }

        return result;
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

    private static int ResolveBaseTopologyCode(int signature)
    {
        return signature switch
        {
            (int)PrimarySignature.UnsplitWater => 3,
            (int)PrimarySignature.Lava => 4,
            (int)PrimarySignature.Goo => 5,
            (int)PrimarySignature.Block => 2,
            (int)PrimarySignature.Special => 6,
            _ when signature == (int)PrimaryWaterSignature.Sea ||
                    signature == (int)PrimaryWaterSignature.Lake ||
                    signature == (int)PrimaryWaterSignature.River => 3,
            _ when signature >= GeoRegionPartitionCodec.PrimaryGroundSignatureOffset => 1,
            _ => 6
        };
    }

    /// <summary>
    /// 主区域层生成阶段使用的基础分类编码。
    /// </summary>
    private enum PrimarySignature : byte
    {
        None = 0,
        UnsplitWater = 1,
        Lava = 2,
        Goo = 3,
        Block = 4,
        Special = 5
    }

    /// <summary>
    /// 水体完成细分后的内部签名。
    /// </summary>
    private enum PrimaryWaterSignature
    {
        Sea = 101,
        Lake = 102,
        River = 103
    }

    /// <summary>
    /// 记录一个待处理水格、它当前所属的起点及距起点的步数。
    /// </summary>
    private readonly struct WaterFrontierNode
    {
        internal WaterFrontierNode(int tileId, int ownerId, int distance)
        {
            TileId = tileId;
            OwnerId = ownerId;
            Distance = distance;
        }

        internal int TileId { get; }
        internal int OwnerId { get; }
        internal int Distance { get; }
    }

    /// <summary>
    /// 按距离、起点顺序和格子编号取出下一水格的稳定优先队列，
    /// 用于让大水面拆分在相同输入下保持一致。
    /// </summary>
    private sealed class StableWaterFrontier
    {
        private WaterFrontierNode[] nodes;
        private int count;

        internal StableWaterFrontier(int capacity)
        {
            nodes = new WaterFrontierNode[Math.Max(4, capacity)];
        }

        /// <summary>
        /// 加入一个待处理水格，并维持下一项的稳定处理顺序。
        /// </summary>
        internal void Push(int tileId, int ownerId, int distance)
        {
            if (count == nodes.Length) Array.Resize(ref nodes, checked(nodes.Length * 2));

            var node = new WaterFrontierNode(tileId, ownerId, distance);
            var index = count++;
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (!ComesBefore(node, nodes[parent])) break;
                nodes[index] = nodes[parent];
                index = parent;
            }
            nodes[index] = node;
        }

        /// <summary>
        /// 取出当前应最先处理的水格；队列为空时返回 false。
        /// </summary>
        internal bool TryPop(out WaterFrontierNode node)
        {
            if (count <= 0)
            {
                node = default;
                return false;
            }

            node = nodes[0];
            var last = nodes[--count];
            if (count <= 0) return true;

            var index = 0;
            while (true)
            {
                var left = index * 2 + 1;
                if (left >= count) break;
                var right = left + 1;
                var child = right < count && ComesBefore(nodes[right], nodes[left]) ? right : left;
                if (!ComesBefore(nodes[child], last)) break;
                nodes[index] = nodes[child];
                index = child;
            }
            nodes[index] = last;
            return true;
        }

        private static bool ComesBefore(WaterFrontierNode left, WaterFrontierNode right)
        {
            if (left.Distance != right.Distance) return left.Distance < right.Distance;
            if (left.OwnerId != right.OwnerId) return left.OwnerId < right.OwnerId;
            return left.TileId < right.TileId;
        }
    }

    /// <summary>
    /// 检查新生成区域未越出重算范围，再把不受影响的旧区域合入待提交列表。
    /// </summary>
    private static void AddRetainedDescriptors(
        PartitionBuildState state,
        IList<GeoRegionDescriptor> retainedDescriptors,
        bool[] includedMask,
        GeoRegionGeneratedLayerMask generatedLayers)
    {
        for (int regionIndex = 0; regionIndex < state.Regions.Count; regionIndex++)
        {
            GeoRegionDescriptor generated = state.Regions[regionIndex].Descriptor;
            for (int position = 0; position < generated.TileCount; position++)
            {
                int tileId = generated.GetTileId(position);
                if (!includedMask[tileId])
                {
                    throw new InvalidOperationException(
                        $"GeoRegion 局部 descriptor 越出 closure: layer={generated.Layer}, tile={tileId}");
                }
            }
        }

        for (int descriptorIndex = 0; descriptorIndex < retainedDescriptors.Count; descriptorIndex++)
        {
            GeoRegionDescriptor descriptor = retainedDescriptors[descriptorIndex] ??
                                             throw new InvalidOperationException("GeoRegion 保留 descriptor 为空");
            bool layerWasGenerated = IsLayerGenerated(generatedLayers, descriptor.Layer);
            if (layerWasGenerated)
            {
                for (int position = 0; position < descriptor.TileCount; position++)
                {
                    int tileId = descriptor.GetTileId(position);
                    if (includedMask[tileId])
                    {
                        throw new InvalidOperationException(
                            $"GeoRegion 保留 descriptor 与 closure 重叠: layer={descriptor.Layer}, tile={tileId}");
                    }
                }
            }

            state.Regions.Add(new PendingRegion(descriptor));
        }
    }

    private static int ComparePendingRegions(PendingRegion left, PendingRegion right)
    {
        GeoRegionDescriptor leftDescriptor = left.Descriptor;
        GeoRegionDescriptor rightDescriptor = right.Descriptor;
        int layerComparison = leftDescriptor.Layer.CompareTo(rightDescriptor.Layer);
        if (layerComparison != 0) return layerComparison;

        int leftMinTileId = leftDescriptor.TileCount > 0 ? leftDescriptor.GetTileId(0) : int.MaxValue;
        int rightMinTileId = rightDescriptor.TileCount > 0 ? rightDescriptor.GetTileId(0) : int.MaxValue;
        int tileComparison = leftMinTileId.CompareTo(rightMinTileId);
        if (tileComparison != 0) return tileComparison;

        int categoryComparison = leftDescriptor.CategoryCode.CompareTo(rightDescriptor.CategoryCode);
        if (categoryComparison != 0) return categoryComparison;
        return leftDescriptor.TileCount.CompareTo(rightDescriptor.TileCount);
    }

    private static bool IsLayerGenerated(GeoRegionGeneratedLayerMask mask, GeoRegionLayer layer)
    {
        return (mask & (GeoRegionGeneratedLayerMask)(1 << (int)layer)) != 0;
    }

    /// <summary>
    /// 根据最终区域列表建立“格子与层到区域及区内位置”的快速索引，
    /// 同时拒绝越界或同层重复归属。
    /// </summary>
    private static void BuildMembershipArrays(
        PartitionBuildState state,
        int tileCount,
        CancellationToken cancellationToken)
    {
        int indexLength = checked(tileCount * GeoRegionPartitionCodec.LayerCount);
        state.RegionSlotByTileLayer = new int[indexLength];
        state.PositionInRegionByTileLayer = new int[indexLength];
        for (int i = 0; i < indexLength; i++)
        {
            state.RegionSlotByTileLayer[i] = -1;
            state.PositionInRegionByTileLayer[i] = -1;
        }

        int membershipCount = 0;
        for (int regionSlot = 0; regionSlot < state.Regions.Count; regionSlot++)
        {
            GeoRegionDescriptor descriptor = state.Regions[regionSlot].Descriptor;
            int layer = (int)descriptor.Layer;
            if ((uint)layer >= GeoRegionPartitionCodec.LayerCount)
            {
                throw new InvalidOperationException(
                    $"GeoRegion 层级超出索引范围: region={regionSlot}, layer={descriptor.Layer}");
            }

            for (int position = 0; position < descriptor.TileCount; position++)
            {
                if ((membershipCount & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                int tileId = descriptor.GetTileId(position);
                if ((uint)tileId >= (uint)tileCount)
                {
                    throw new InvalidOperationException(
                        $"GeoRegion 包含越界 tile: region={regionSlot}, tile={tileId}, count={tileCount}");
                }

                int flatIndex = tileId * GeoRegionPartitionCodec.LayerCount + layer;
                int existingSlot = state.RegionSlotByTileLayer[flatIndex];
                if (existingSlot >= 0)
                {
                    throw new InvalidOperationException(
                        $"同一 tile 在同层重复归属: tile={tileId}, layer={descriptor.Layer}, " +
                        $"regions={existingSlot},{regionSlot}");
                }

                state.RegionSlotByTileLayer[flatIndex] = regionSlot;
                state.PositionInRegionByTileLayer[flatIndex] = position;
                membershipCount++;
            }
        }

        state.MembershipCount = membershipCount;
        state.EstimatedPersistentBytes =
            (long)state.RegionSlotByTileLayer.Length * sizeof(int) +
            (long)state.PositionInRegionByTileLayer.Length * sizeof(int) +
            (long)membershipCount * sizeof(int);
    }

    /// <summary>
    /// 只保存宽高，并按格子编号推导坐标的轻量网格视图，
    /// 供各阶段统一处理边界和上下左右邻格。
    /// </summary>
    private sealed class GeoRegionGrid
    {
        internal GeoRegionGrid(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Width = width;
            Height = height;
            Length = checked(width * height);
        }

        internal int Width { get; }
        internal int Height { get; }
        internal int Length { get; }

        internal GeoRegionGridPoint this[int tileId]
        {
            get
            {
                if ((uint)tileId >= (uint)Length) throw new ArgumentOutOfRangeException(nameof(tileId));
                return new GeoRegionGridPoint(tileId % Width, tileId / Width);
            }
        }
    }

    /// <summary>保存网格中一个格子的横纵坐标。</summary>
    private readonly struct GeoRegionGridPoint
    {
        /// <summary>创建一个坐标值。</summary>
        internal GeoRegionGridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>横向坐标。</summary>
        internal int X { get; }
        /// <summary>纵向坐标。</summary>
        internal int Y { get; }
    }

    /// <summary>
    /// 生成地区说明时暂存各项结果，避免各层传递很长的参数列表。
    /// </summary>
    private struct GeoRegionDescriptorData
    {
        /// <summary>地区属于哪一层。</summary>
        internal GeoRegionLayer Layer;
        /// <summary>地区最终采用的类别。</summary>
        internal GeoRegionCategoryCode CategoryCode;
        /// <summary>地区对应的基础地面层。</summary>
        internal GeoRegionTerrainLayer BaseTerrainLayer;
        /// <summary>水域地区属于海、湖还是河。</summary>
        internal PrimaryWaterKind WaterKind;
        /// <summary>地区是否碰到地图边缘。</summary>
        internal bool TouchesEdge;
        /// <summary>地区原始核心包含的格子数。</summary>
        internal int CoreTileCount;
        /// <summary>地区是否混合了多种原始类别。</summary>
        internal bool IsMixed;
        /// <summary>是否因没有足够大的同类地区而保留为小地区。</summary>
        internal bool TopologyExempt;
        /// <summary>核心地表的连接编号。</summary>
        internal int CoreSignature;
        /// <summary>地区实际包含的各类连接编号。</summary>
        internal int[] RawSignatures;
        /// <summary>每种连接编号各有多少格。</summary>
        internal int[] RawSignatureTileCounts;
        /// <summary>核心中数量最多的生物群系编号。</summary>
        internal string CoreBiomeId;
        /// <summary>整个地区中数量最多的生物群系编号。</summary>
        internal string DominantBiomeId;
        /// <summary>地区实际包含的生物群系编号。</summary>
        internal string[] BiomeIds;
        /// <summary>每种生物群系各有多少格。</summary>
        internal int[] BiomeTileCounts;
        /// <summary>地区中心的横向坐标。</summary>
        internal int CenterX;
        /// <summary>地区中心的纵向坐标。</summary>
        internal int CenterY;
        /// <summary>地区包含的总格子数。</summary>
        internal int TileCount;
        /// <summary>地区中数量最多的主要地表类别。</summary>
        internal GeoRegionPrimaryCategoryCode DominantPrimaryCode;
        /// <summary>地区中数量最多的地貌类别。</summary>
        internal GeoRegionLandformCode DominantLandformCode;
    }

    /// <summary>
    /// 保存尚未写入最终索引的地区，可直接包装旧地区说明，也可从新格子集合创建。
    /// </summary>
    private sealed class PendingRegion
    {
        /// <summary>保留一份无需重新计算的旧地区说明。</summary>
        internal PendingRegion(GeoRegionDescriptor descriptor)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        /// <summary>根据格子列表、地形和已算出的统计结果创建完整地区说明。</summary>
        internal PendingRegion(
            List<int> tileIds,
            GeoRegionTerrainSnapshot terrain,
            GeoRegionDescriptorData data)
        {
            if (tileIds == null) throw new ArgumentNullException(nameof(tileIds));
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (tileIds.Count != data.TileCount)
            {
                throw new InvalidOperationException(
                    "GeoRegion descriptor tile 计数不一致: layer=" + data.Layer +
                    ", list=" + tileIds.Count + ", data=" + data.TileCount);
            }

            if (data.CoreTileCount <= 0) data.CoreTileCount = data.TileCount;
            GeoRegionBiomeCompositionData biomeComposition = GeoRegionBiomeComposition.Build(
                terrain,
                tileIds,
                data.CoreTileCount);
            data.CoreBiomeId = biomeComposition.CoreBiomeId;
            data.DominantBiomeId = biomeComposition.DominantBiomeId;
            data.BiomeIds = biomeComposition.BiomeIds;
            data.BiomeTileCounts = biomeComposition.BiomeTileCounts;
            tileIds.Sort();
            Descriptor = new GeoRegionDescriptor(
                tileIds,
                data.Layer,
                data.CategoryCode,
                data.BaseTerrainLayer,
                data.WaterKind,
                data.TouchesEdge,
                data.CoreTileCount,
                data.IsMixed,
                data.TopologyExempt,
                data.CoreSignature,
                data.RawSignatures,
                data.RawSignatureTileCounts,
                data.CoreBiomeId,
                data.DominantBiomeId,
                data.BiomeIds,
                data.BiomeTileCounts,
                data.CenterX,
                data.CenterY,
                data.DominantPrimaryCode,
                data.DominantLandformCode);
        }

        /// <summary>最终将写入分区结果的地区说明。</summary>
        internal GeoRegionDescriptor Descriptor { get; }
    }

    /// <summary>
    /// 汇总一次地区划分产生的地区、格子归属、基础分类和各阶段耗时，最后统一生成结果。
    /// </summary>
    private sealed class PartitionBuildState
    {
        /// <summary>等待写入最终结果的全部地区。</summary>
        internal List<PendingRegion> Regions { get; } = new(256);
        /// <summary>按格子和层记录所属地区在列表中的位置。</summary>
        internal int[] RegionSlotByTileLayer { get; set; }
        /// <summary>按格子和层记录该格在所属地区内部的位置。</summary>
        internal int[] PositionInRegionByTileLayer { get; set; }
        /// <summary>所有层的格子归属记录总数。</summary>
        internal int MembershipCount { get; set; }
        /// <summary>最终结果预计长期占用的内存字节数。</summary>
        internal long EstimatedPersistentBytes { get; set; }
        /// <summary>下一次局部更新可以继续使用的逐格基础分类。</summary>
        internal GeoRegionPartitionBaseArrays BaseArrays { get; set; }
        /// <summary>计算逐格基础分类所花的毫秒数。</summary>
        internal double BaseArraysMilliseconds { get; set; }
        /// <summary>生成主要地区所花的毫秒数。</summary>
        internal double PrimaryMilliseconds { get; set; }
        /// <summary>生成地貌地区所花的毫秒数。</summary>
        internal double LandformMilliseconds { get; set; }
        /// <summary>生成陆块地区所花的毫秒数。</summary>
        internal double LandmassMilliseconds { get; set; }
        /// <summary>生成半岛所花的毫秒数。</summary>
        internal double PeninsulaMilliseconds { get; set; }
        /// <summary>生成海峡所花的毫秒数。</summary>
        internal double StraitMilliseconds { get; set; }
        /// <summary>生成群岛所花的毫秒数。</summary>
        internal double ArchipelagoMilliseconds { get; set; }
        /// <summary>建立格子归属查找表所花的毫秒数。</summary>
        internal double IndexMilliseconds { get; set; }
        /// <summary>整次地区划分所花的总毫秒数。</summary>
        internal double TotalMilliseconds { get; set; }

        /// <summary>把已汇总的数据组装为最终结果，并按需检查所有格子的归属。</summary>
        internal GeoRegionPartitionResult Complete(GeoRegionPartitionInput input, bool validateCoverage)
        {
            var descriptors = new List<GeoRegionDescriptor>(Regions.Count);
            for (int i = 0; i < Regions.Count; i++) descriptors.Add(Regions[i].Descriptor);
            var timing = new GeoRegionPartitionTiming(
                BaseArraysMilliseconds,
                PrimaryMilliseconds,
                LandformMilliseconds,
                LandmassMilliseconds,
                PeninsulaMilliseconds,
                StraitMilliseconds,
                ArchipelagoMilliseconds,
                IndexMilliseconds,
                TotalMilliseconds);
            var result = new GeoRegionPartitionResult(
                input.WorldSeedId,
                input.Width,
                input.Height,
                input.Revision,
                input.Rules.RuleFingerprint,
                descriptors,
                RegionSlotByTileLayer,
                PositionInRegionByTileLayer,
                MembershipCount,
                EstimatedPersistentBytes,
                BaseArrays,
                timing);
            if (validateCoverage) result.ValidateCoverage(input.Terrain);
            return result;
        }
    }

    /// <summary>
    /// 保存单次分区期间可合并的原始区域及其统计信息，
    /// 用于把过小碎片并入合适的大区域，完成后不会随结果长期保留。
    /// </summary>
    private sealed class MutableRegionComponent
    {
        internal MutableRegionComponent(
            int signature,
            List<int> tileIds,
            int sumX,
            int sumY,
            bool touchesEdge)
        {
            Signature = signature;
            TileIds = tileIds ?? new List<int>(4);
            MinTileId = int.MaxValue;
            for (var i = 0; i < TileIds.Count; i++)
            {
                if (TileIds[i] < MinTileId) MinTileId = TileIds[i];
            }
            SumX = sumX;
            SumY = sumY;
            TouchesEdge = touchesEdge;
            CoreTileCount = TileIds.Count;
        }

        internal int Signature;
        internal List<int> TileIds;
        internal int MinTileId;
        internal int SumX;
        internal int SumY;
        internal bool TouchesEdge;
        internal int CoreTileCount;
        internal bool IsFormalCore;
        internal bool IsMixed;
        internal bool TopologyExempt;
        internal Dictionary<int, int> RawSignatureCounts;
        internal bool Removed;
    }

    /// <summary>
    /// 保存群岛候选小岛的格子、面积、坐标汇总和外接矩形，
    /// 供群岛阶段判断哪些小岛应归为一组。
    /// </summary>
    private readonly struct IslandInfo
    {
        internal IslandInfo(
            List<int> tileIndices,
            int tileCount,
            int sumX,
            int sumY,
            int minX,
            int minY,
            int maxX,
            int maxY)
        {
            TileIndices = tileIndices ?? new List<int>();
            MinTileId = int.MaxValue;
            for (var i = 0; i < TileIndices.Count; i++)
            {
                if (TileIndices[i] < MinTileId) MinTileId = TileIndices[i];
            }
            TileCount = tileCount;
            SumX = sumX;
            SumY = sumY;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        internal List<int> TileIndices { get; }
        internal int TileCount { get; }
        internal int MinTileId { get; }
        internal int SumX { get; }
        internal int SumY { get; }
        internal int MinX { get; }
        internal int MinY { get; }
        internal int MaxX { get; }
        internal int MaxY { get; }
    }
}

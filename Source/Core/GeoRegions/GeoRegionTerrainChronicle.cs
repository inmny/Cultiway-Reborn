using System;
using System.Collections.Generic;
using Cultiway.Core.GeoRegions.Partitioning;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 在主线程记录每个格子的地形变化持续了多久，避免短暂变化立刻触发地区重划。
/// 它分别保存刚观察到的状态、已经持续够久而等待应用的状态，以及当前真正采用的状态。
/// 所有方法都只能由主线程调用。
/// </summary>
internal sealed class GeoRegionTerrainChronicle
{
    // 主体结构、表面生物群系和冰冻覆盖分别需要连续保持多少游戏年才算稳定变化。
    internal const int StructureMaturityYears = 1;
    internal const int SurfaceMaturityYears = 20;
    internal const int OverlayMaturityYears = 100;

    // 三类变化分别每隔多少游戏年检查一次，较慢变化无需每年重复扫描。
    internal const int StructureEpochYears = 1;
    internal const int SurfaceEpochYears = 10;
    internal const int OverlayEpochYears = 100;

    // 尚在计时的格子变化；每个格子可同时跟踪主体、表面和覆盖三类变化。
    private readonly Dictionary<int, CandidateState> candidates = new();

    // 已持续够久但尚未真正应用的格子状态，只保存与当前已应用状态不同的格子。
    private readonly Dictionary<int, GeoRegionTerrainObservation> desiredByTile = new();

    // 当前地区划分真正采用的逐格地形状态，初始化前为空。
    private GeoRegionTerrainObservation[] appliedByTile;

    // 当前世界身份，防止旧世界的格子通知混入新世界。
    private int worldSeedId = -1;

    // 待应用状态版本号在任何待处理内容变化时递增；主体拓扑版本号只在陆地、水域等结构变化时递增。
    private int desiredGeneration;
    private int topologyGeneration;

    // 下一次允许检查三类变化是否已持续够久的游戏年份。
    private int nextStructureEvaluationYear;
    private int nextSurfaceEvaluationYear;
    private int nextOverlayEvaluationYear;

    /// <summary>是否已经装入当前地区划分采用的地形状态。</summary>
    internal bool IsInitialized => appliedByTile != null;

    /// <summary>是否存在已经持续够久、等待应用的地形变化。</summary>
    internal bool HasPendingDesired => desiredByTile.Count > 0;

    /// <summary>待应用状态的当前版本号，用于识别后台任务是否已经过期。</summary>
    internal int DesiredGeneration => desiredGeneration;

    /// <summary>等待应用的格子数量。</summary>
    internal int PendingDesiredCount => desiredByTile.Count;

    /// <summary>
    /// 以当前完整地形作为已应用基线，并从当前年份之后的检查点开始记录新变化。
    /// </summary>
    internal void Initialize(
        GeoRegionTerrainSnapshot terrain,
        int currentYear)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));

        worldSeedId = terrain.WorldSeedId;
        appliedByTile = new GeoRegionTerrainObservation[terrain.CellCount];
        for (int tileId = 0; tileId < appliedByTile.Length; tileId++)
        {
            appliedByTile[tileId] = terrain.GetObservation(tileId);
        }

        candidates.Clear();
        desiredByTile.Clear();
        desiredGeneration = 1;
        topologyGeneration = 1;
        nextStructureEvaluationYear = NextAlignedBoundary(currentYear, StructureEpochYears);
        nextSurfaceEvaluationYear = NextAlignedBoundary(currentYear, SurfaceEpochYears);
        nextOverlayEvaluationYear = NextAlignedBoundary(currentYear, OverlayEpochYears);
    }

    /// <summary>
    /// 记录某个格子的最新地形状态，并更新各类变化开始持续的时间。
    /// 若格子在后台任务提交前恢复为已应用状态，会立即撤销相应待处理变化。
    /// </summary>
    internal void Observe(
        int worldIdentity,
        int tileId,
        GeoRegionTerrainObservation observation,
        double currentWorldTime)
    {
        ValidateIdentity(worldIdentity, tileId);

        GeoRegionTerrainObservation applied = appliedByTile[tileId];
        GeoRegionTerrainObservation desired = desiredByTile.TryGetValue(tileId, out var pending)
            ? pending
            : applied;
        bool desiredChanged = false;
        CandidateState candidate = candidates.TryGetValue(tileId, out var existing)
            ? existing
            : new CandidateState(observation);

        // 已持续够久但尚未应用的变化若恢复原状，立即撤销，禁止后台任务发布短暂的中间状态。
        if (!desired.Structure.Equals(applied.Structure) && observation.Structure.Equals(applied.Structure))
        {
            desired = desired.WithStructure(applied.Structure);
            candidate.TrackStructure = false;
            desiredChanged = true;
        }
        if (!desired.Surface.Equals(applied.Surface) && observation.Surface.Equals(applied.Surface))
        {
            desired = desired.WithSurface(applied.Surface);
            candidate.TrackSurface = false;
            desiredChanged = true;
        }
        if (!desired.Overlay.Equals(applied.Overlay) && observation.Overlay.Equals(applied.Overlay))
        {
            desired = desired.WithOverlay(applied.Overlay);
            candidate.TrackOverlay = false;
            desiredChanged = true;
        }

        UpdateStructureCandidate(candidate, observation, desired, currentWorldTime);
        UpdateSurfaceCandidate(candidate, observation, desired, currentWorldTime);
        UpdateOverlayCandidate(candidate, observation, desired, currentWorldTime);
        candidate.Observed = observation;

        if (candidate.HasTrackedChannel)
        {
            candidates[tileId] = candidate;
        }
        else
        {
            candidates.Remove(tileId);
        }

        if (desiredChanged)
        {
            SetDesired(tileId, desired, applied);
            BumpDesiredGeneration();
        }
    }

    /// <summary>
    /// 在到达检查年份时，判断正在跟踪的变化是否已经连续保持足够年数。
    /// 达标的变化会进入等待应用状态；返回值表示本次是否新增或更新了待处理内容。
    /// </summary>
    internal bool EvaluateDue(
        int currentYear,
        double currentWorldTime,
        double secondsPerYear)
    {
        if (!IsInitialized || candidates.Count == 0)
        {
            AdvanceDueBoundaries(currentYear);
            return false;
        }
        if (secondsPerYear <= 0d) throw new ArgumentOutOfRangeException(nameof(secondsPerYear));

        bool structureDue = currentYear >= nextStructureEvaluationYear;
        bool surfaceDue = currentYear >= nextSurfaceEvaluationYear;
        bool overlayDue = currentYear >= nextOverlayEvaluationYear;
        if (!structureDue && !surfaceDue && !overlayDue) return false;

        var completedCandidateIds = new List<int>();
        bool desiredChanged = false;
        foreach (KeyValuePair<int, CandidateState> pair in candidates)
        {
            int tileId = pair.Key;
            CandidateState candidate = pair.Value;
            GeoRegionTerrainObservation applied = appliedByTile[tileId];
            GeoRegionTerrainObservation desired = desiredByTile.TryGetValue(tileId, out var pending)
                ? pending
                : applied;
            bool tileDesiredChanged = false;

            if (structureDue && candidate.TrackStructure &&
                HasMatured(candidate.StructureSinceWorldTime, currentWorldTime, secondsPerYear, StructureMaturityYears))
            {
                desired = desired.WithStructure(candidate.Observed.Structure);
                candidate.TrackStructure = false;
                tileDesiredChanged = true;
            }
            if (surfaceDue && candidate.TrackSurface &&
                HasMatured(candidate.SurfaceSinceWorldTime, currentWorldTime, secondsPerYear, SurfaceMaturityYears))
            {
                desired = desired.WithSurface(candidate.Observed.Surface);
                candidate.TrackSurface = false;
                tileDesiredChanged = true;
            }
            if (overlayDue && candidate.TrackOverlay &&
                HasMatured(candidate.OverlaySinceWorldTime, currentWorldTime, secondsPerYear, OverlayMaturityYears))
            {
                desired = desired.WithOverlay(candidate.Observed.Overlay);
                candidate.TrackOverlay = false;
                tileDesiredChanged = true;
            }

            if (tileDesiredChanged)
            {
                SetDesired(tileId, desired, applied);
                desiredChanged = true;
            }
            if (!candidate.HasTrackedChannel) completedCandidateIds.Add(tileId);
        }

        for (int i = 0; i < completedCandidateIds.Count; i++)
        {
            candidates.Remove(completedCandidateIds[i]);
        }
        AdvanceDueBoundaries(currentYear);
        if (desiredChanged) BumpDesiredGeneration();
        return desiredChanged;
    }

    /// <summary>
    /// 从等待应用的格子中建立一份不会再变化的后台任务输入。
    /// 陆地、水域等主体结构变化优先单独成批，其余分类变化随后处理。
    /// </summary>
    internal bool TryCreateBatch(out GeoRegionTerrainChronicleBatch batch)
    {
        if (!HasPendingDesired)
        {
            batch = null;
            return false;
        }

        bool topologyBatch = false;
        foreach (KeyValuePair<int, GeoRegionTerrainObservation> pair in desiredByTile)
        {
            if (!HasTopologyDifference(appliedByTile[pair.Key], pair.Value)) continue;
            topologyBatch = true;
            break;
        }

        var tileIds = new List<int>();
        foreach (KeyValuePair<int, GeoRegionTerrainObservation> pair in desiredByTile)
        {
            bool isTopology = HasTopologyDifference(appliedByTile[pair.Key], pair.Value);
            if (topologyBatch != isTopology) continue;
            tileIds.Add(pair.Key);
        }
        tileIds.Sort();
        var observations = new GeoRegionTerrainObservation[tileIds.Count];
        for (int i = 0; i < tileIds.Count; i++)
        {
            observations[i] = desiredByTile[tileIds[i]];
        }

        batch = new GeoRegionTerrainChronicleBatch(
            worldSeedId,
            desiredGeneration,
            topologyGeneration,
            topologyBatch ? GeoRegionRepartitionLane.Topology : GeoRegionRepartitionLane.Classification,
            tileIds.ToArray(),
            observations);
        return true;
    }

    /// <summary>
    /// 检查一批后台任务输入是否仍对应当前世界和最新待应用状态。
    /// 主体结构批次只比较影响连通性的结构类别，普通分类批次比较完整状态。
    /// </summary>
    internal bool IsCurrent(GeoRegionTerrainChronicleBatch batch)
    {
        if (batch == null || batch.WorldSeedId != worldSeedId) return false;
        if (batch.Lane == GeoRegionRepartitionLane.Classification)
        {
            if (batch.DesiredGeneration != desiredGeneration) return false;
        }
        else if (batch.TopologyGeneration != topologyGeneration)
        {
            return false;
        }

        for (int i = 0; i < batch.Count; i++)
        {
            int tileId = batch.GetTileId(i);
            if (!desiredByTile.TryGetValue(tileId, out var desired)) return false;
            if (batch.Lane == GeoRegionRepartitionLane.Topology)
            {
                if (!HasTopologyDifference(appliedByTile[tileId], desired) ||
                    ResolveTopologyCode(desired.Structure) !=
                    ResolveTopologyCode(batch.GetObservation(i).Structure))
                {
                    return false;
                }
                continue;
            }
            if (!desired.Equals(batch.GetObservation(i))) return false;
        }
        return true;
    }

    /// <summary>
    /// 在地区重划成功后，把该批地形正式记为已应用，并清除已经处理完的计时状态。
    /// </summary>
    internal void Commit(GeoRegionTerrainChronicleBatch batch)
    {
        if (!IsCurrent(batch))
        {
            throw new InvalidOperationException("GeoRegion Chronicle 不能提交已过期的 desired batch");
        }

        bool committedTopology = batch.Lane == GeoRegionRepartitionLane.Topology;
        for (int i = 0; i < batch.Count; i++)
        {
            int tileId = batch.GetTileId(i);
            GeoRegionTerrainObservation observation = batch.GetObservation(i);
            if (!desiredByTile.TryGetValue(tileId, out var current))
            {
                throw new InvalidOperationException($"GeoRegion Chronicle desired 已撤销: tile={tileId}");
            }

            appliedByTile[tileId] = observation;
            if (current.Equals(observation)) desiredByTile.Remove(tileId);
            if (!candidates.TryGetValue(tileId, out CandidateState candidate)) continue;

            if (candidate.Observed.Structure.Equals(observation.Structure)) candidate.TrackStructure = false;
            if (candidate.Observed.Surface.Equals(observation.Surface)) candidate.TrackSurface = false;
            if (candidate.Observed.Overlay.Equals(observation.Overlay)) candidate.TrackOverlay = false;
            if (!candidate.HasTrackedChannel) candidates.Remove(tileId);
        }
        if (committedTopology) BumpTopologyGeneration();
    }

    /// <summary>
    /// 清除当前世界的所有基线、计时状态和待处理变化，恢复到未初始化状态。
    /// </summary>
    internal void Clear()
    {
        worldSeedId = -1;
        desiredGeneration = 0;
        topologyGeneration = 0;
        nextStructureEvaluationYear = 0;
        nextSurfaceEvaluationYear = 0;
        nextOverlayEvaluationYear = 0;
        appliedByTile = null;
        candidates.Clear();
        desiredByTile.Clear();
    }

    /// <summary>
    /// 按世界时间计算某项变化是否已经连续保持指定游戏年数。
    /// </summary>
    private static bool HasMatured(
        double sinceWorldTime,
        double currentWorldTime,
        double secondsPerYear,
        int maturityYears)
    {
        double elapsed = Math.Max(0d, currentWorldTime - sinceWorldTime);
        return elapsed / secondsPerYear >= maturityYears;
    }

    /// <summary>
    /// 更新主体结构变化的计时；内容再次改变时从最新变化时刻重新计时。
    /// </summary>
    private static void UpdateStructureCandidate(
        CandidateState candidate,
        GeoRegionTerrainObservation observation,
        GeoRegionTerrainObservation desired,
        double currentWorldTime)
    {
        if (observation.Structure.Equals(desired.Structure))
        {
            candidate.TrackStructure = false;
            return;
        }
        if (!candidate.TrackStructure || !observation.Structure.Equals(candidate.Observed.Structure))
        {
            candidate.TrackStructure = true;
            candidate.StructureSinceWorldTime = currentWorldTime;
        }
    }

    /// <summary>
    /// 更新表面生物群系变化的计时；内容再次改变时从最新变化时刻重新计时。
    /// </summary>
    private static void UpdateSurfaceCandidate(
        CandidateState candidate,
        GeoRegionTerrainObservation observation,
        GeoRegionTerrainObservation desired,
        double currentWorldTime)
    {
        if (observation.Surface.Equals(desired.Surface))
        {
            candidate.TrackSurface = false;
            return;
        }
        if (!candidate.TrackSurface || !observation.Surface.Equals(candidate.Observed.Surface))
        {
            candidate.TrackSurface = true;
            candidate.SurfaceSinceWorldTime = currentWorldTime;
        }
    }

    /// <summary>
    /// 更新冰冻等覆盖状态变化的计时；内容再次改变时从最新变化时刻重新计时。
    /// </summary>
    private static void UpdateOverlayCandidate(
        CandidateState candidate,
        GeoRegionTerrainObservation observation,
        GeoRegionTerrainObservation desired,
        double currentWorldTime)
    {
        if (observation.Overlay.Equals(desired.Overlay))
        {
            candidate.TrackOverlay = false;
            return;
        }
        if (!candidate.TrackOverlay || !observation.Overlay.Equals(candidate.Observed.Overlay))
        {
            candidate.TrackOverlay = true;
            candidate.OverlaySinceWorldTime = currentWorldTime;
        }
    }

    /// <summary>
    /// 保存或撤销一个格子的待应用状态。
    /// 如果变化影响陆地、水域等连通结构，还会推进相应版本号，使旧的主体重划任务失效。
    /// </summary>
    private void SetDesired(
        int tileId,
        GeoRegionTerrainObservation desired,
        GeoRegionTerrainObservation applied)
    {
        GeoRegionTerrainObservation previousDesired = desiredByTile.TryGetValue(tileId, out var previous)
            ? previous
            : applied;
        bool topologyProjectionChanged =
            ResolveTopologyCode(previousDesired.Structure) != ResolveTopologyCode(desired.Structure);
        if (desired.Equals(applied))
        {
            desiredByTile.Remove(tileId);
        }
        else
        {
            desiredByTile[tileId] = desired;
        }

        if (topologyProjectionChanged)
        {
            BumpTopologyGeneration();
        }
    }

    /// <summary>
    /// 把已经到达的检查点推进到当前年份之后，避免同一个检查点重复执行。
    /// </summary>
    private void AdvanceDueBoundaries(int currentYear)
    {
        if (currentYear >= nextStructureEvaluationYear)
        {
            nextStructureEvaluationYear = NextAlignedBoundary(currentYear, StructureEpochYears);
        }
        if (currentYear >= nextSurfaceEvaluationYear)
        {
            nextSurfaceEvaluationYear = NextAlignedBoundary(currentYear, SurfaceEpochYears);
        }
        if (currentYear >= nextOverlayEvaluationYear)
        {
            nextOverlayEvaluationYear = NextAlignedBoundary(currentYear, OverlayEpochYears);
        }
    }

    /// <summary>
    /// 计算严格晚于当前年份、并与指定间隔整齐对齐的下一个检查年份。
    /// </summary>
    private static int NextAlignedBoundary(int currentYear, int epochYears)
    {
        if (epochYears <= 0) throw new ArgumentOutOfRangeException(nameof(epochYears));
        if (currentYear < 0) currentYear = 0;
        long next = ((long)currentYear / epochYears + 1L) * epochYears;
        return next >= int.MaxValue ? int.MaxValue : (int)next;
    }

    /// <summary>
    /// 确认变化通知来自当前世界，并且格子编号位于地形数组范围内。
    /// </summary>
    private void ValidateIdentity(int worldIdentity, int tileId)
    {
        if (!IsInitialized) throw new InvalidOperationException("GeoRegion Chronicle 尚未初始化");
        if (worldIdentity != worldSeedId)
        {
            throw new InvalidOperationException(
                $"GeoRegion Chronicle 世界身份不匹配: expected={worldSeedId}, actual={worldIdentity}");
        }
        if ((uint)tileId >= (uint)appliedByTile.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId));
        }
    }

    /// <summary>
    /// 判断两份格子状态在陆地、水域、熔岩等影响地图连通性的类别上是否不同。
    /// </summary>
    private static bool HasTopologyDifference(
        GeoRegionTerrainObservation applied,
        GeoRegionTerrainObservation desired)
    {
        return ResolveTopologyCode(applied.Structure) != ResolveTopologyCode(desired.Structure);
    }

    /// <summary>
    /// 把详细主体结构归并为影响地图连通性的粗分类编号。
    /// </summary>
    private static int ResolveTopologyCode(GeoRegionTerrainStructure structure)
    {
        return structure.TerrainKind switch
        {
            GeoRegionTerrainKind.Ground => 1,
            GeoRegionTerrainKind.Block => 2,
            GeoRegionTerrainKind.Water => 3,
            GeoRegionTerrainKind.Lava => 4,
            GeoRegionTerrainKind.Goo => 5,
            _ => 6
        };
    }

    /// <summary>推进待应用状态版本号，并在整数上限后从一重新开始。</summary>
    private void BumpDesiredGeneration()
    {
        desiredGeneration = desiredGeneration == int.MaxValue ? 1 : desiredGeneration + 1;
    }

    /// <summary>推进主体结构版本号，并在整数上限后从一重新开始。</summary>
    private void BumpTopologyGeneration()
    {
        topologyGeneration = topologyGeneration == int.MaxValue ? 1 : topologyGeneration + 1;
    }

    /// <summary>
    /// 保存一个格子的最新观察值，以及主体、表面和覆盖三类变化各自是否正在计时。
    /// </summary>
    private sealed class CandidateState
    {
        /// <summary>以首次观察值创建格子变化计时状态。</summary>
        internal CandidateState(GeoRegionTerrainObservation observed)
        {
            Observed = observed;
        }

        // 当前最新观察到的完整格子状态。
        internal GeoRegionTerrainObservation Observed;

        // 三类变化是否正在等待达到各自所需的持续年数。
        internal bool TrackStructure;
        internal bool TrackSurface;
        internal bool TrackOverlay;

        // 三类当前内容从哪个世界时刻开始连续保持不变。
        internal double StructureSinceWorldTime;
        internal double SurfaceSinceWorldTime;
        internal double OverlaySinceWorldTime;

        /// <summary>是否还有至少一类变化正在计时。</summary>
        internal bool HasTrackedChannel => TrackStructure || TrackSurface || TrackOverlay;
    }
}

/// <summary>
/// 一批待处理地形变化所属的处理队列类别。
/// 普通分类变化可以整体被新版本替换；影响地图连通性的主体结构变化按专用版本判断是否过期。
/// </summary>
internal enum GeoRegionRepartitionLane : byte
{
    /// <summary>只改变地区分类、表面或覆盖，不改变陆地与水域连通关系。</summary>
    Classification = 0,

    /// <summary>改变陆地、水域、熔岩等主体结构，需要优先重新计算连通关系。</summary>
    Topology = 1
}

/// <summary>
/// 提交给后台地区重划任务的一批不会再变化的格子状态。
/// 它携带世界身份和状态版本号，主线程可据此拒绝已经被更新内容取代的旧任务。
/// </summary>
internal sealed class GeoRegionTerrainChronicleBatch
{
    // 本批格子编号和对应状态均在构造时复制，防止调用方随后修改后台任务输入。
    private readonly int[] tileIds;
    private readonly GeoRegionTerrainObservation[] observations;

    /// <summary>
    /// 建立一批后台任务输入，并复制格子编号与状态数组以保持内容固定。
    /// </summary>
    internal GeoRegionTerrainChronicleBatch(
        int worldSeedId,
        int desiredGeneration,
        int topologyGeneration,
        GeoRegionRepartitionLane lane,
        int[] tileIds,
        GeoRegionTerrainObservation[] observations)
    {
        if (tileIds == null) throw new ArgumentNullException(nameof(tileIds));
        if (observations == null || observations.Length != tileIds.Length)
        {
            throw new InvalidOperationException("GeoRegion Chronicle batch 数组尺寸不一致");
        }

        WorldSeedId = worldSeedId;
        DesiredGeneration = desiredGeneration;
        TopologyGeneration = topologyGeneration;
        Lane = lane;
        this.tileIds = (int[])tileIds.Clone();
        this.observations = (GeoRegionTerrainObservation[])observations.Clone();
    }

    /// <summary>本批变化所属的世界编号。</summary>
    internal int WorldSeedId { get; }

    /// <summary>建立本批输入时的完整待应用状态版本号。</summary>
    internal int DesiredGeneration { get; }

    /// <summary>建立本批输入时的主体结构版本号。</summary>
    internal int TopologyGeneration { get; }

    /// <summary>本批属于普通分类变化还是影响连通性的主体结构变化。</summary>
    internal GeoRegionRepartitionLane Lane { get; }

    /// <summary>本批包含的格子数量。</summary>
    internal int Count => tileIds.Length;

    /// <summary>返回格子编号数组的副本，调用方修改它不会影响本批内容。</summary>
    internal int[] TileIds => (int[])tileIds.Clone();

    /// <summary>按批内位置读取一个格子编号。</summary>
    internal int GetTileId(int index)
    {
        return tileIds[index];
    }

    /// <summary>按批内位置读取该格子等待应用的状态。</summary>
    internal GeoRegionTerrainObservation GetObservation(int index)
    {
        return observations[index];
    }

    /// <summary>返回整批格子状态的副本，供后台任务独立使用。</summary>
    internal GeoRegionTerrainObservation[] CopyObservations()
    {
        return (GeoRegionTerrainObservation[])observations.Clone();
    }
}

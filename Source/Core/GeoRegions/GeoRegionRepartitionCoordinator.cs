using System;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Const;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.GeoRegions.Partitioning;
using Cultiway.Core.Libraries;
using Cultiway.Core.Performance;
using UnityEngine;

namespace Cultiway.Core.GeoRegions;

/// <summary>
/// 协调游戏运行期间因地形变化触发的地区重划。
/// 主线程记录变化持续了多久，单个后台线程计算新划分，完成后再由主线程核验并安装结果。
/// </summary>
internal static class GeoRegionRepartitionCoordinator
{
    // 记录每格地形变化持续时间，以及哪些稳定变化仍等待应用。
    private static readonly GeoRegionTerrainChronicle Chronicle = new();

    // 当前已应用的地形、地区划分结果和规则副本，三者共同组成下一次增量计算的基线。
    private static GeoRegionTerrainSnapshot baselineTerrainSnapshot;
    private static GeoRegionPartitionResult baselinePartitionResult;
    private static GeoRegionRuleSnapshot baselineRules;

    // 当前唯一的后台重划任务；为空表示没有任务运行。
    private static RepartitionWork repartitionWork;

    // 最近一次后台重划失败原因；保存或切换世界前会据此停止继续处理，避免混用失败任务的结果。
    private static Exception lastFailure;

    /// <summary>
    /// 安装当前世界已完成的地形、划分结果和规则作为运行时基线，并开始记录后续地形变化。
    /// </summary>
    internal static void InstallBaseline(
        GeoRegionTerrainSnapshot terrain,
        GeoRegionPartitionResult partitionResult,
        GeoRegionRuleSnapshot rules,
        int membershipRevision)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        if (partitionResult == null) throw new ArgumentNullException(nameof(partitionResult));
        if (rules == null) throw new ArgumentNullException(nameof(rules));
        if (repartitionWork != null || baselineTerrainSnapshot != null) CancelPendingWork();
        if (terrain.WorldSeedId != partitionResult.WorldSeedId ||
            terrain.Width != partitionResult.Width ||
            terrain.Height != partitionResult.Height ||
            terrain.Revision != partitionResult.Revision ||
            rules.WorldSeedId != terrain.WorldSeedId ||
            rules.Width != terrain.Width ||
            rules.Height != terrain.Height ||
            rules.Revision != terrain.Revision)
        {
            throw new InvalidOperationException("GeoRegion 初始化 baseline 的 terrain/result/rules 身份不一致");
        }

        GeoRegionManager manager = WorldboxGame.I?.GeoRegions ??
                                   throw new InvalidOperationException("GeoRegionManager 尚未初始化");
        if (!manager.IsMembershipReady || manager.MembershipRevision != membershipRevision)
        {
            throw new InvalidOperationException(
                $"GeoRegion 初始化 baseline 与 membership 不一致: expected={membershipRevision}, actual={manager.MembershipRevision}");
        }

        baselineTerrainSnapshot = terrain;
        baselinePartitionResult = partitionResult;
        baselineRules = rules;
        repartitionWork = null;
        lastFailure = null;
        Chronicle.Initialize(terrain, Date.getCurrentYear());
    }

    /// <summary>
    /// 接收一个游戏格子的地形变化通知，确认它属于当前地图后记录最新状态和变化开始时间。
    /// </summary>
    internal static void NotifyTerrainChanged(WorldTile tile)
    {
        if (tile?.data == null || baselineTerrainSnapshot == null || baselineRules == null) return;
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager?.IsMembershipReady != true) return;

        WorldTile[] tiles = World.world?.tiles_list;
        int tileId = tile.data.tile_id;
        if (tiles == null || (uint)tileId >= (uint)tiles.Length || !ReferenceEquals(tiles[tileId], tile)) return;
        if (MapBox.current_world_seed_id != baselineTerrainSnapshot.WorldSeedId) return;

        GeoRegionTerrainObservation observation = GeoRegionTerrainCellCapture.CaptureObservation(
            tile,
            tileId,
            baselineTerrainSnapshot.Width,
            baselineRules);
        Chronicle.Observe(
            MapBox.current_world_seed_id,
            tileId,
            observation,
            SimulationTime.Now);
    }

    /// <summary>
    /// 每帧推进地区重划：检查变化是否持续够久、取消过期任务、安装已完成结果并启动下一批计算。
    /// 所有会接触游戏对象的操作都留在主线程执行。
    /// </summary>
    internal static void Tick()
    {
        int currentFrame = Time.frameCount;
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        manager?.ProcessRetiredMemberships(currentFrame);
        if (baselineTerrainSnapshot == null || manager?.IsMembershipReady != true) return;

        try
        {
            Chronicle.EvaluateDue(Date.getCurrentYear(), SimulationTime.Now, TimeScales.SecPerYear);
            if (repartitionWork != null)
            {
                if (!Chronicle.IsCurrent(repartitionWork.Batch))
                {
                    repartitionWork.Cancellation.Cancel();
                }
                if (!repartitionWork.RepartitionTask.IsCompleted) return;

                if (!Chronicle.IsCurrent(repartitionWork.Batch) || repartitionWork.RepartitionTask.IsCanceled)
                {
                    DiscardSupersededWork();
                }
                else
                {
                    CommitCompletedRepartition(currentFrame);
                }
            }

            TryStartRepartition();
        }
        catch (OperationCanceledException)
        {
            DiscardSupersededWork();
        }
        catch (Exception exception)
        {
            FailRepartition(exception);
        }
    }

    /// <summary>
    /// 保存或切换世界前等待当前重划结束，使后台线程不再持有即将被替换的世界数据。
    /// </summary>
    internal static void DrainPendingWork()
    {
        while (baselineTerrainSnapshot != null)
        {
            if (repartitionWork == null)
            {
                if (!Chronicle.HasPendingDesired) break;
                if (!TryStartRepartition()) continue;
            }

            RepartitionWork current = repartitionWork;
            try
            {
                current.RepartitionTask.GetAwaiter().GetResult();
                if (!Chronicle.IsCurrent(current.Batch))
                {
                    DiscardSupersededWork();
                    continue;
                }
                CommitCompletedRepartition(Time.frameCount);
            }
            catch (OperationCanceledException)
            {
                DiscardSupersededWork();
            }
            catch (Exception exception)
            {
                FailRepartition(exception);
                throw new InvalidOperationException("GeoRegion 增量重划失败，不能创建不完整存档", exception);
            }
        }

        if (lastFailure != null)
        {
            Exception failure = lastFailure;
            lastFailure = null;
            throw new InvalidOperationException("GeoRegion 增量重划未完成，不能创建存档", failure);
        }
    }

    /// <summary>
    /// 取消并等待当前后台任务退出，随后清除世界基线、变化记录和失败状态。
    /// 用于切换世界或重新安装完整地区数据的边界。
    /// </summary>
    internal static void CancelPendingWork()
    {
        RepartitionWork current = repartitionWork;
        repartitionWork = null;
        if (current != null)
        {
            current.Cancellation.Cancel();
            try
            {
                current.RepartitionTask.GetAwaiter().GetResult();
            }
            catch
            {
                // 清理边界只需要确认后台线程退出，不再向外传播被取消任务的异常。
            }
            current.Cancellation.Dispose();
        }

        Chronicle.Clear();
        baselineTerrainSnapshot = null;
        baselinePartitionResult = null;
        baselineRules = null;
        lastFailure = null;
    }

    /// <summary>
    /// 从已经持续够久的格子变化建立下一版地形和规则副本，并启动唯一的后台增量重划任务。
    /// 若详细地形虽变但不影响划分结果，则直接更新基线而不启动线程。
    /// </summary>
    private static bool TryStartRepartition()
    {
        if (repartitionWork != null) return false;
        if (!Chronicle.TryCreateBatch(out GeoRegionTerrainChronicleBatch batch)) return false;

        GeoRegionTerrainSnapshot previousTerrain = baselineTerrainSnapshot;
        GeoRegionPartitionResult previousResult = baselinePartitionResult;
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        WorldTile[] tiles = World.world?.tiles_list;
        if (previousTerrain == null || previousResult == null || manager?.IsMembershipReady != true ||
            tiles == null || !MatchesCurrentWorld(previousTerrain, tiles))
        {
            throw new InvalidOperationException("GeoRegion desired batch 的 baseline 已不属于当前世界");
        }

        GeoRegionLibrary library = ModClass.L?.GeoRegionLibrary ??
                                   throw new InvalidOperationException("GeoRegionLibrary 尚未初始化");
        int terrainRevision = NextRevision(previousTerrain.Revision);
        GeoRegionRuleSnapshot rules = GeoRegionRuleSnapshotFactory.Capture(
            library,
            batch.WorldSeedId,
            previousTerrain.Width,
            previousTerrain.Height,
            terrainRevision);
        int[] dirtyTileIds = batch.TileIds;
        GeoRegionTerrainSnapshot nextTerrain = previousTerrain.WithDirtyObservations(
            terrainRevision,
            dirtyTileIds,
            batch.CopyObservations(),
            out int changedCellCount);
        if (changedCellCount == 0)
        {
            baselineTerrainSnapshot = previousTerrain.WithAppliedObservations(
                dirtyTileIds,
                batch.CopyObservations());
            Chronicle.Commit(batch);
            PublishCommittedTerrainChanges(batch, baselineTerrainSnapshot.Revision);
            lastFailure = null;
            return false;
        }

        int membershipRevision = manager.MembershipRevision;
        var cancellation = new CancellationTokenSource();
        Task<GeoRegionIncrementalPartitionResult> task = StartRepartitionWorker(
            previousTerrain,
            nextTerrain,
            previousResult,
            dirtyTileIds,
            rules,
            cancellation.Token);
        ObserveFault(task);
        repartitionWork = new RepartitionWork(
            batch,
            tiles,
            previousTerrain.Revision,
            previousResult,
            nextTerrain,
            rules,
            membershipRevision,
            cancellation,
            task);
        lastFailure = null;
        return true;
    }

    /// <summary>
    /// 在低优先级后台线程中只使用普通数据计算增量划分，并把成功、取消或异常写回任务结果。
    /// </summary>
    private static Task<GeoRegionIncrementalPartitionResult> StartRepartitionWorker(
        GeoRegionTerrainSnapshot oldTerrain,
        GeoRegionTerrainSnapshot newTerrain,
        GeoRegionPartitionResult oldResult,
        int[] dirtyTileIds,
        GeoRegionRuleSnapshot rules,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<GeoRegionIncrementalPartitionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(GeoRegionIncrementalPartitioner.BuildIncremental(
                    oldTerrain,
                    newTerrain,
                    oldResult,
                    dirtyTileIds,
                    rules,
                    cancellationToken));
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "CultiwayGeoRegionIncrementalRepartition",
            Priority = System.Threading.ThreadPriority.BelowNormal
        };
        thread.Start();
        return completion.Task;
    }

    /// <summary>
    /// 在主线程核验并提交后台计算结果。
    /// 地区归属确实改变时，让旧地区尽量继续使用，再一次性安装新归属并通知相关缓存刷新。
    /// </summary>
    private static void CommitCompletedRepartition(int currentFrame)
    {
        RepartitionWork current = repartitionWork ??
                                  throw new InvalidOperationException("GeoRegion 没有可提交的增量重划任务");
        if (!Chronicle.IsCurrent(current.Batch))
        {
            DiscardSupersededWork();
            return;
        }

        GeoRegionIncrementalPartitionResult incremental = current.RepartitionTask.GetAwaiter().GetResult();
        GeoRegionPartitionResult result = incremental.Result;
        ValidateCompletedWork(current, result);
        result.ValidateCoverage(current.NextTerrain);

        if (incremental.Diagnostics.ChangedBaseTileCount == 0)
        {
            Chronicle.Commit(current.Batch);
            baselineTerrainSnapshot = current.NextTerrain;
            baselinePartitionResult = result;
            baselineRules = current.Rules;
            PublishCommittedTerrainChanges(current.Batch, current.NextTerrain.Revision);
            repartitionWork = null;
            lastFailure = null;
            current.Cancellation.Dispose();
            return;
        }

        GeoRegionManager manager = WorldboxGame.I.GeoRegions;
        GeoRegionMembershipSnapshot oldMembership = manager.GetMembershipForReconciliation(current.MembershipRevision);
        int nextMembershipRevision = NextRevision(current.MembershipRevision);
        int[] dirtyTileIds = current.Batch.TileIds;
        GeoRegionReconciliationResult reconciliation = GeoRegionReconciler.Reconcile(
            manager,
            oldMembership,
            current.PreviousPartitionResult,
            result,
            ModClass.L.GeoRegionLibrary,
            current.Tiles,
            incremental.CopyAffectedTileIds(),
            dirtyTileIds,
            nextMembershipRevision);

        manager.InstallReplacementMembership(
            reconciliation.Membership,
            reconciliation.RetiredRegions,
            currentFrame);

        // 格子与地区的对应关系已经一次性换成新版；此后先固定各项基线，再发送缓存刷新通知。
        Chronicle.Commit(current.Batch);
        baselineTerrainSnapshot = current.NextTerrain;
        baselinePartitionResult = result;
        baselineRules = current.Rules;
        PublishCommittedTerrainChanges(current.Batch, current.NextTerrain.Revision);
        repartitionWork = null;
        lastFailure = null;
        current.Cancellation.Dispose();

        RedirectSelectedRegion(reconciliation);
        manager.ApplyRuntimeChangeSet(reconciliation.ChangeSet);
        ModClass.I.CustomMapModeManager?.OnGeoRegionMembershipReplaced(reconciliation.ChangeSet);
    }

    /// <summary>把已经正式采用的地形变化作为纯数据消息发送给内容系统。</summary>
    private static void PublishCommittedTerrainChanges(
        GeoRegionTerrainChronicleBatch batch,
        int terrainRevision)
    {
        if (batch == null || batch.Count == 0) return;
        EventSystemHub.Publish(new StableTerrainChangesCommittedEvent(
            batch.WorldSeedId,
            baselineTerrainSnapshot?.Width ?? MapBox.width,
            baselineTerrainSnapshot?.Height ?? MapBox.height,
            terrainRevision,
            batch.TopologyGeneration,
            batch.Lane == GeoRegionRepartitionLane.Topology,
            batch.TileIds));
    }

    /// <summary>
    /// 提交前确认任务仍属于当前世界、当前基线、当前地区归属版本，并且结果尺寸与版本完全一致。
    /// </summary>
    private static void ValidateCompletedWork(
        RepartitionWork current,
        GeoRegionPartitionResult result)
    {
        if (!Chronicle.IsCurrent(current.Batch))
        {
            throw new OperationCanceledException("GeoRegion 增量重划 desired generation 已过期");
        }
        if (!MatchesCurrentWorld(current.NextTerrain, current.Tiles))
        {
            throw new InvalidOperationException("GeoRegion 增量重划所属世界已变化");
        }
        if (baselineTerrainSnapshot == null ||
            baselineTerrainSnapshot.Revision != current.PreviousTerrainRevision ||
            !ReferenceEquals(baselinePartitionResult, current.PreviousPartitionResult))
        {
            throw new InvalidOperationException("GeoRegion 增量重划的上一版基线已变化");
        }
        if (WorldboxGame.I.GeoRegions.MembershipRevision != current.MembershipRevision)
        {
            throw new InvalidOperationException(
                $"GeoRegion 增量重划 membership 已过期: work={current.MembershipRevision}, " +
                $"current={WorldboxGame.I.GeoRegions.MembershipRevision}");
        }
        if (result.WorldSeedId != current.WorldSeedId ||
            result.Width != current.NextTerrain.Width ||
            result.Height != current.NextTerrain.Height ||
            result.Revision != current.TerrainRevision)
        {
            throw new InvalidOperationException("GeoRegion 增量重划结果身份不一致");
        }
    }

    /// <summary>
    /// 当前选中的旧地区若已经退役，则改选最接近的新地区；没有对应项时清空选择。
    /// </summary>
    private static void RedirectSelectedRegion(GeoRegionReconciliationResult reconciliation)
    {
        GeoRegion selected = WorldboxGame.I.SelectedGeoRegion;
        if (selected == null) return;

        bool retired = false;
        for (int i = 0; i < reconciliation.RetiredRegions.Count; i++)
        {
            if (!ReferenceEquals(reconciliation.RetiredRegions[i], selected)) continue;
            retired = true;
            break;
        }
        if (!retired) return;

        WorldboxGame.I.SelectedGeoRegion = reconciliation.SelectionRedirects.TryGetValue(selected, out GeoRegion survivor)
            ? survivor
            : null;
    }

    /// <summary>
    /// 丢弃已被更新地形取代的后台任务，取消计算并释放资源，但保留最新变化供下一批重新计算。
    /// </summary>
    private static void DiscardSupersededWork()
    {
        RepartitionWork current = repartitionWork;
        repartitionWork = null;
        if (current == null) return;

        current.Cancellation.Cancel();
        if (current.RepartitionTask.IsFaulted)
        {
            _ = current.RepartitionTask.Exception;
        }
        current.Cancellation.Dispose();
        lastFailure = null;
    }

    /// <summary>
    /// 记录重划失败，取消当前任务并保留等待应用的地形变化，供后续重试并让等待调用者得到失败原因。
    /// </summary>
    private static void FailRepartition(Exception exception)
    {
        RepartitionWork current = repartitionWork;
        repartitionWork = null;
        if (current != null)
        {
            current.Cancellation.Cancel();
            current.Cancellation.Dispose();
        }

        lastFailure = exception;
        if (exception != null)
        {
            ModClass.LogError($"GeoRegion 增量重划失败，desired 状态已保留:\n{exception}");
        }
    }

    /// <summary>生成下一个非零版本号，并在整数上限后从一重新开始。</summary>
    private static int NextRevision(int revision)
    {
        return revision == int.MaxValue ? 1 : revision + 1;
    }

    /// <summary>
    /// 确认地形数据仍对应当前世界的同一个格子数组、世界编号、地图尺寸和格子数量。
    /// </summary>
    private static bool MatchesCurrentWorld(GeoRegionTerrainSnapshot terrain, WorldTile[] tiles)
    {
        return terrain != null &&
               World.world != null &&
               ReferenceEquals(World.world.tiles_list, tiles) &&
               terrain.WorldSeedId == MapBox.current_world_seed_id &&
               terrain.Width == MapBox.width &&
               terrain.Height == MapBox.height &&
               terrain.CellCount == tiles.Length;
    }

    /// <summary>
    /// 提前读取后台任务异常，避免无人读取的失败任务触发运行时的未观察异常处理。
    /// </summary>
    private static void ObserveFault(Task<GeoRegionIncrementalPartitionResult> task)
    {
        _ = task.ContinueWith(
            failedTask => _ = failedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>
    /// 一次正在运行的地区重划所需的固定输入、版本凭据、取消开关和后台任务。
    /// 这些版本凭据用于在提交时确认计算期间世界状态没有被替换。
    /// </summary>
    private sealed class RepartitionWork
    {
        /// <summary>
        /// 保存一批后台计算所需的全部输入和提交时要核对的旧版本信息。
        /// </summary>
        internal RepartitionWork(
            GeoRegionTerrainChronicleBatch batch,
            WorldTile[] tiles,
            int previousTerrainRevision,
            GeoRegionPartitionResult previousPartitionResult,
            GeoRegionTerrainSnapshot nextTerrain,
            GeoRegionRuleSnapshot rules,
            int membershipRevision,
            CancellationTokenSource cancellation,
            Task<GeoRegionIncrementalPartitionResult> repartitionTask)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            PreviousTerrainRevision = previousTerrainRevision;
            PreviousPartitionResult = previousPartitionResult ??
                                      throw new ArgumentNullException(nameof(previousPartitionResult));
            NextTerrain = nextTerrain ?? throw new ArgumentNullException(nameof(nextTerrain));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            MembershipRevision = membershipRevision;
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
            RepartitionTask = repartitionTask ?? throw new ArgumentNullException(nameof(repartitionTask));
        }

        /// <summary>本次要应用的格子变化批次及其状态版本号。</summary>
        internal GeoRegionTerrainChronicleBatch Batch { get; }

        /// <summary>本次任务所属的世界编号。</summary>
        internal int WorldSeedId => Batch.WorldSeedId;

        /// <summary>本次计算产出的下一版地形版本号。</summary>
        internal int TerrainRevision => NextTerrain.Revision;

        /// <summary>任务启动时当前世界的格子数组引用，用于发现世界已被替换。</summary>
        internal WorldTile[] Tiles { get; }

        /// <summary>任务启动时采用的上一版地形版本号。</summary>
        internal int PreviousTerrainRevision { get; }

        /// <summary>任务启动时采用的上一版地区划分纯计算结果。</summary>
        internal GeoRegionPartitionResult PreviousPartitionResult { get; }

        /// <summary>合入稳定地形变化后供后台计算使用的下一版地形。</summary>
        internal GeoRegionTerrainSnapshot NextTerrain { get; }

        /// <summary>与下一版地形同版本的地区划分规则副本。</summary>
        internal GeoRegionRuleSnapshot Rules { get; }

        /// <summary>任务启动时游戏实际采用的格子与地区对应关系版本号。</summary>
        internal int MembershipRevision { get; }

        /// <summary>任务过期或世界清理时用于通知后台线程停止。</summary>
        internal CancellationTokenSource Cancellation { get; }

        /// <summary>后台增量地区划分计算的完成结果。</summary>
        internal Task<GeoRegionIncrementalPartitionResult> RepartitionTask { get; }
    }
}

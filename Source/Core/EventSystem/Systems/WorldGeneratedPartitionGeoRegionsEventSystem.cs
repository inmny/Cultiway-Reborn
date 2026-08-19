using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.GeoLib.Components;
using Cultiway.Core.GeoRegions;
using Cultiway.Core.GeoRegions.Partitioning;
using Cultiway.Core.Libraries;
using Cultiway.Core.Performance;
using Friflo.Engine.ECS;

namespace Cultiway.Core.EventSystem.Systems;

/// <summary>
/// 新世界生成后划分全部地理区域。处理完成前会暂缓世界运行，完成后玩家才能在地图和地区界面看到完整结果。
/// </summary>
public class WorldGeneratedPartitionGeoRegionsEventSystem :
    GenericEventSystem<WorldGeneratedEvent>,
    ICooperativeSystemStep
{
    // 每次读取的地块数量，避免初始化工作长时间独占一帧。
    private const int TerrainCaptureBatchSize = 8192;
    // 当前负责新世界地区划分的系统，供保存和地图改动通知使用。
    private static WorldGeneratedPartitionGeoRegionsEventSystem instance;

    // 上一次受理的世界身份，用于忽略同一张地图重复发出的生成消息。
    private int lastWorldSeedId;
    private int lastWidth;
    private int lastHeight;
    // 为每轮划分生成唯一编号，防止旧结果写入新世界。
    private int nextRevision;
    // 当前划分进度；非空时世界仍在等待地区准备完成。
    private PartitionWork work;
    // 最近一次失败原因，保存世界时会把它明确报告给调用方。
    private Exception lastFailure;

    /// <summary>每帧最多接收一次世界生成消息。</summary>
    protected override int MaxEventsPerUpdate => 1;

    /// <summary>登记当前实例，供保存、换图和地形变化入口查询划分状态。</summary>
    public WorldGeneratedPartitionGeoRegionsEventSystem()
    {
        instance = this;
    }

    /// <summary>地区尚未准备完毕时返回 true，外部据此暂停正常世界运行。</summary>
    internal static bool BlocksSimulation => instance?.work != null;

    /// <summary>记录初始化期间被改动的地块，地区完成后会按最新地形重新处理这些位置。</summary>
    internal static void RecordTerrainMutation(WorldTile tile)
    {
        PartitionWork current = instance?.work;
        if (current == null || tile?.data == null ||
            current.WorldSeedId != MapBox.current_world_seed_id ||
            !ReferenceEquals(World.world?.tiles_list, current.Tiles))
        {
            return;
        }
        int tileId = tile.data.tile_id;
        if ((uint)tileId >= (uint)current.Tiles.Length || !ReferenceEquals(current.Tiles[tileId], tile)) return;
        current.MutatedTileIds.Add(tileId);
    }

    /// <summary>换图或退出世界时停止尚未完成的地区划分，并清除等待中的后续工作。</summary>
    internal static void CancelPendingWork()
    {
        instance?.CancelPendingWorkInternal(true);
        GeoRegionRepartitionCoordinator.CancelPendingWork();
    }

    /// <summary>
    /// 保存或切换世界前等待当前地区工作结束，避免后台线程继续读取即将被替换的世界。
    /// </summary>
    internal static void DrainPendingWork()
    {
        WorldGeneratedPartitionGeoRegionsEventSystem system = instance;
        while (system?.work != null)
        {
            PartitionWork current = system.work;
            try
            {
                if (current.BuildTask != null)
                {
                    current.BuildTask.GetAwaiter().GetResult();
                }
            }
            catch (Exception exception)
            {
                system.FailWork(current, exception);
                throw new InvalidOperationException("GeoRegion 分区失败，不能创建不完整存档", exception);
            }

            ((ICooperativeSystemStep)system).StepCooperatively();
            if (system.work == null && system.lastFailure != null)
            {
                Exception failure = system.lastFailure;
                system.lastFailure = null;
                throw new InvalidOperationException("GeoRegion 物化失败，不能创建不完整存档", failure);
            }
        }

        GeoRegionRepartitionCoordinator.DrainPendingWork();
    }

    /// <summary>返回当前处理阶段，供帧耗时统计区分读取地形、计算和创建地区。</summary>
    string ICooperativeSystemStep.CooperativePhaseName
    {
        get
        {
            PartitionWork current = work;
            if (current == null) return "geo.partition.dequeue";
            if (current.CaptureSession != null) return "geo.partition.capture";
            if (current.BuildTask == null || !current.BuildTask.IsCompleted) return "geo.partition.compute";
            if (current.Result == null || current.RegionIndex >= current.Result.RegionCount) return "geo.partition.complete";
            return "geo.partition.materialize." + current.Result.GetRegion(current.RegionIndex).Layer;
        }
    }

    /// <summary>每帧推进一小段工作，最终一次性公布完整地区结果。</summary>
    bool ICooperativeSystemStep.StepCooperatively()
    {
        if (work == null)
        {
            base.OnUpdateGroup();
            if (work == null) return true;
        }

        PartitionWork current = work;
        if (!IsCurrentWorld(current))
        {
            CancelPendingWorkInternal(true);
            return true;
        }

        try
        {
            if (current.CaptureSession != null)
            {
                if (!current.CaptureSession.CaptureNext(TerrainCaptureBatchSize)) return false;

                GeoRegionTerrainSnapshot terrain = current.CaptureSession.Complete();
                current.Terrain = terrain;
                current.CaptureSession = null;
                var input = new GeoRegionPartitionInput(terrain, current.Rules);
                current.BuildTask = StartPartitionBuild(input, current.Cancellation.Token);
                ObserveFault(current.BuildTask);
                return false;
            }

            if (current.BuildTask == null)
            {
                throw new InvalidOperationException("GeoRegion 捕获完成后未创建分区任务");
            }

            if (!current.BuildTask.IsCompleted) return true;

            if (current.Result == null)
            {
                current.Result = current.BuildTask.GetAwaiter().GetResult();
                ValidateResultIdentity(current);
                current.Materializer = new GeoRegionMaterializer(
                    WorldboxGame.I.GeoRegions,
                    ModClass.L.GeoRegionLibrary,
                    new GeoRegionNamingSession(),
                    current.WorldSeedId,
                    current.Width,
                    current.Height);
                current.MaterializeStartedTimestamp = Stopwatch.GetTimestamp();
            }

            return MaterializeNextRegion(current);
        }
        catch (OperationCanceledException)
        {
            FailWork(current, null);
            return true;
        }
        catch (Exception exception)
        {
            FailWork(current, exception);
            return true;
        }
    }

    /// <summary>
    /// 接收世界生成消息，确认地图尺寸和规则后开始分批读取地形。
    /// </summary>
    protected override void HandleEvent(WorldGeneratedEvent evt)
    {
        if (evt.Width <= 0 || evt.Height <= 0)
        {
            throw new InvalidOperationException("世界生成事件缺少有效地图尺寸");
        }

        if (evt.WorldSeedId == lastWorldSeedId &&
            evt.Width == lastWidth &&
            evt.Height == lastHeight &&
            (work != null || WorldboxGame.I?.GeoRegions?.IsMembershipReady == true))
        {
            return;
        }

        lastWorldSeedId = evt.WorldSeedId;
        lastWidth = evt.Width;
        lastHeight = evt.Height;
        lastFailure = null;

        if (ModClass.I?.TileExtendManager == null || !ModClass.I.TileExtendManager.Ready())
        {
            throw new InvalidOperationException("TileExtend 尚未完成，不能开始 GeoRegion 分区");
        }

        WorldTile[] tiles = World.world?.tiles_list;
        if (tiles == null || tiles.Length == 0)
        {
            throw new InvalidOperationException("当前世界没有可用于 GeoRegion 分区的地块");
        }

        int width = evt.Width;
        int height = evt.Height;
        if (checked(width * height) != tiles.Length)
        {
            throw new InvalidOperationException(
                $"GeoRegion 地图尺寸与 tile 数不匹配: width={width}, height={height}, tiles={tiles.Length}");
        }

        GeoRegionLibrary library = ModClass.L?.GeoRegionLibrary ??
                                   throw new InvalidOperationException("GeoRegionLibrary 尚未初始化");
        int revision = NextRevision();
        GeoRegionRuleSnapshot rules = GeoRegionRuleSnapshotFactory.Capture(
            library,
            evt.WorldSeedId,
            width,
            height,
            revision);

        CleanupOldGeoRegionBinders();
        var cancellation = new CancellationTokenSource();
        var captureSession = new GeoRegionTerrainCaptureSession(
            evt.WorldSeedId,
            width,
            height,
            revision,
            tiles,
            rules);
        work = new PartitionWork(
            evt.WorldSeedId,
            tiles,
            width,
            height,
            revision,
            rules,
            captureSession,
            cancellation);
    }

    /// <summary>在低优先级后台线程计算地区边界，减少新世界出现时的连续卡顿。</summary>
    private static Task<GeoRegionPartitionResult> StartPartitionBuild(
        GeoRegionPartitionInput input,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<GeoRegionPartitionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(GeoRegionPartitioner.BuildFull(input, cancellationToken));
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
            Name = "CultiwayGeoRegionPartition",
            Priority = ThreadPriority.BelowNormal
        };
        thread.Start();
        return completion.Task;
    }

    /// <summary>及时取出后台任务的异常，实际失败仍由逐帧处理入口统一回滚和报告。</summary>
    private static void ObserveFault(Task<GeoRegionPartitionResult> task)
    {
        _ = task.ContinueWith(
            failedTask =>
            {
                _ = failedTask.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>每次创建一个可供地图、列表和详情窗口使用的地区，全部完成后再统一启用。</summary>
    private bool MaterializeNextRegion(PartitionWork current)
    {
        ValidateResultIdentity(current);
        if (current.RegionIndex < current.Result.RegionCount)
        {
            GeoRegionDescriptor descriptor = current.Result.GetRegion(current.RegionIndex);
            GeoRegion region = current.Materializer.Materialize(descriptor);
            current.MaterializedRegions.Add(region);
            current.RegionIndex++;
            return false;
        }

        if (current.MaterializedRegions.Count != current.Result.RegionCount)
        {
            throw new InvalidOperationException(
                $"GeoRegion 物化数量不一致: materialized={current.MaterializedRegions.Count}, result={current.Result.RegionCount}");
        }

        var entries = new List<GeoRegionMembershipEntry>(current.Result.RegionCount);
        for (int i = 0; i < current.Result.RegionCount; i++)
        {
            GeoRegion region = current.MaterializedRegions[i];
            GeoRegionDescriptor descriptor = current.Result.GetRegion(i);
            if (region == null || region.isRekt())
            {
                throw new InvalidOperationException($"GeoRegion 尚未完成物化: index={i}");
            }

            entries.Add(new GeoRegionMembershipEntry(region, descriptor.Layer, descriptor.CopyTileIds()));
        }

        var membership = new GeoRegionMembershipSnapshot(
            1,
            current.Tiles,
            current.Result.CopyRegionSlotByTileLayer(),
            current.Result.CopyPositionInRegionByTileLayer(),
            entries);
        WorldboxGame.I.GeoRegions.InstallInitialMembership(membership);
        GeoRegionRepartitionCoordinator.InstallBaseline(
            current.Terrain,
            current.Result,
            current.Rules,
            membership.Revision);
        ReplayTerrainMutations(current);
        ModClass.I.CustomMapModeManager?.SetAllDirty();
        EventSystemHub.Publish(new GeoRegionsReadyEvent
        {
            WorldSeedId = current.WorldSeedId,
            Width = current.Width,
            Height = current.Height,
            MembershipRevision = membership.Revision
        });
        current.Cancellation.Dispose();
        work = null;
        lastFailure = null;
        return true;
    }

    /// <summary>重新处理划分期间发生过变化的地块，让玩家看到的地区边界符合最终地形。</summary>
    private static void ReplayTerrainMutations(PartitionWork current)
    {
        if (current.MutatedTileIds.Count == 0) return;
        var tileIds = new List<int>(current.MutatedTileIds);
        tileIds.Sort();
        for (int i = 0; i < tileIds.Count; i++)
        {
            GeoRegionRepartitionCoordinator.NotifyTerrainChanged(current.Tiles[tileIds[i]]);
        }
    }

    /// <summary>确认计算结果确实属于当前地图和本轮工作，拒绝迟到的旧结果。</summary>
    private static void ValidateResultIdentity(PartitionWork current)
    {
        GeoRegionPartitionResult result = current.Result ??
                                          throw new InvalidOperationException("GeoRegion 分区任务没有结果");
        if (result.WorldSeedId != current.WorldSeedId ||
            result.Width != current.Width ||
            result.Height != current.Height ||
            result.Revision != current.Revision)
        {
            throw new InvalidOperationException(
                $"GeoRegion 分区结果身份过期: " +
                $"work={current.WorldSeedId}/{current.Width}x{current.Height}/r{current.Revision}, " +
                $"result={result.WorldSeedId}/{result.Width}x{result.Height}/r{result.Revision}");
        }
    }

    /// <summary>确认玩家仍停留在启动本轮划分时的同一张地图。</summary>
    private static bool IsCurrentWorld(PartitionWork current)
    {
        return World.world != null &&
               current.WorldSeedId == MapBox.current_world_seed_id &&
               ReferenceEquals(World.world.tiles_list, current.Tiles) &&
               MapBox.width == current.Width &&
               MapBox.height == current.Height;
    }

    /// <summary>划分失败时撤回本轮已创建的地区，并让世界初始化明确进入失败状态。</summary>
    private void FailWork(PartitionWork current, Exception exception)
    {
        if (!ReferenceEquals(work, current)) return;

        work = null;
        lastFailure = exception ?? new OperationCanceledException("GeoRegion 分区任务已取消");
        GeoRegionRepartitionCoordinator.CancelPendingWork();
        current.Cancellation.Cancel();
        RollbackMaterializedRegions(current);
        WorldboxGame.I?.GeoRegions?.ClearMembership();
        ModClass.I?.TileExtendManager?.FailWorldInitialization(current.Tiles);
        current.Cancellation.Dispose();
        ClearQueuedEvents();

        if (exception != null)
        {
            ModClass.LogError($"[FramePriority] GeoRegion 世界初始化失败，已回滚本轮地区:\n{exception}");
        }
    }

    /// <summary>停止当前工作并清空排队消息；换图时还会允许新世界重新开始划分。</summary>
    private void CancelPendingWorkInternal(bool resetWorldIdentity)
    {
        PartitionWork current = work;
        work = null;
        lastFailure = null;
        if (current != null)
        {
            current.Cancellation.Cancel();
            RollbackMaterializedRegions(current);
            current.Cancellation.Dispose();
        }

        ClearQueuedEvents();
        if (!resetWorldIdentity) return;

        lastWorldSeedId = 0;
        lastWidth = 0;
        lastHeight = 0;
        ModClass.I?.TileExtendManager?.CancelFitNewWorld();
    }

    /// <summary>
    /// 删除尚未正式公布的地区对象，避免玩家看到半成品或旧世界残留。
    /// </summary>
    private static void RollbackMaterializedRegions(PartitionWork current)
    {
        if (current == null || current.MaterializedRegions.Count == 0) return;
        GeoRegionManager manager = WorldboxGame.I?.GeoRegions;
        if (manager == null) return;

        for (int i = 0; i < current.MaterializedRegions.Count; i++)
        {
            GeoRegion region = current.MaterializedRegions[i];
            if (region == null || region.isRekt()) continue;
            manager.removeObject(region);
        }
    }

    /// <summary>取得下一轮划分编号，达到整数上限后从 1 重新开始。</summary>
    private int NextRevision()
    {
        nextRevision = nextRevision == int.MaxValue ? 1 : nextRevision + 1;
        return nextRevision;
    }

    /// <summary>计算某个处理阶段已经耗费的毫秒数。</summary>
    private static double GetElapsedMilliseconds(long startedTimestamp)
    {
        if (startedTimestamp <= 0) return 0;
        return (Stopwatch.GetTimestamp() - startedTimestamp) * 1000d / Stopwatch.Frequency;
    }

    /// <summary>
    /// 清理旧世界遗留的地区关联记录，防止新地图上的对象误指向旧地区。
    /// </summary>
    private static void CleanupOldGeoRegionBinders()
    {
        EntityStore ecsWorld = ModClass.I.TileExtendManager.World;
        ecsWorld.Query<GeoRegionBinder>().ForEachEntity((ref GeoRegionBinder _, Entity entity) => entity.DeleteEntity());
    }

    /// <summary>保存一轮地区划分从读取地形到正式公布之间的全部进度。</summary>
    private sealed class PartitionWork
    {
        /// <summary>创建一轮工作，并固定它所属的世界、地图尺寸和地区规则。</summary>
        internal PartitionWork(
            int worldSeedId,
            WorldTile[] tiles,
            int width,
            int height,
            int revision,
            GeoRegionRuleSnapshot rules,
            GeoRegionTerrainCaptureSession captureSession,
            CancellationTokenSource cancellation)
        {
            WorldSeedId = worldSeedId;
            Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            Width = width;
            Height = height;
            Revision = revision;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            CaptureSession = captureSession ?? throw new ArgumentNullException(nameof(captureSession));
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>本轮工作所属的世界编号。</summary>
        internal int WorldSeedId { get; }
        /// <summary>本轮读取并最终安装地区归属的全部地块。</summary>
        internal WorldTile[] Tiles { get; }
        /// <summary>本轮地图宽度。</summary>
        internal int Width { get; }
        /// <summary>本轮地图高度。</summary>
        internal int Height { get; }
        /// <summary>用于辨别迟到结果的本轮编号。</summary>
        internal int Revision { get; }
        /// <summary>开始划分时固定下来的地区规则。</summary>
        internal GeoRegionRuleSnapshot Rules { get; }
        /// <summary>换图、退出或失败时用于停止后台计算。</summary>
        internal CancellationTokenSource Cancellation { get; }
        /// <summary>已创建但尚未正式公布的地区。</summary>
        internal List<GeoRegion> MaterializedRegions { get; } = new(256);
        /// <summary>划分期间发生过变化、完成后需要重新处理的地块。</summary>
        internal HashSet<int> MutatedTileIds { get; } = new();
        /// <summary>分批读取地形的当前进度。</summary>
        internal GeoRegionTerrainCaptureSession CaptureSession { get; set; }
        /// <summary>后台计算使用的完整地形记录。</summary>
        internal GeoRegionTerrainSnapshot Terrain { get; set; }
        /// <summary>正在后台执行的地区边界计算。</summary>
        internal Task<GeoRegionPartitionResult> BuildTask { get; set; }
        /// <summary>后台计算完成后得到的全部地区描述。</summary>
        internal GeoRegionPartitionResult Result { get; set; }
        /// <summary>把地区描述创建成游戏内地区对象的工具。</summary>
        internal GeoRegionMaterializer Materializer { get; set; }
        /// <summary>下一次准备创建的地区位置。</summary>
        internal int RegionIndex { get; set; }
        /// <summary>开始创建地区的时间，用于性能统计。</summary>
        internal long MaterializeStartedTimestamp { get; set; }
    }
}

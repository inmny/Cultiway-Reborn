using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cultiway.Const;
using Cultiway.Content.Const;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 角色 parallel 阶段的批处理实现。
/// 保留原版逐 tick 状态推进，只消除每角色委托、重复全局读取与已稳定动画的空调用。
/// </summary>
internal sealed class CooperativeActorParallelJobRunner :
    ICooperativeBatchParallelJobRunner<BatchActors, Actor>
{
    private const string PrepareJobId = "prepare";
    private const string UpdateTimersJobId = "update_timers";
    private const string UpdateVisibilityJobId = "update_visibility";
    private const int TimerRangeSize = 128;

    private readonly Action<int> runTimerRangeAction;
    private TimerRange[] timerRanges =
        Array.Empty<TimerRange>();
    private TimerRangeMetrics[] timerRangeMetrics =
        Array.Empty<TimerRangeMetrics>();
    private float activeTimerElapsed;
    private float activeTimerDeltaTime;
    private float activeTimerTimeScaleMultiplier;
    private bool activeTimerPaused;
    private bool collectTimerDiagnostics;

    private static int lastVisibilityFrame = -1;
    private static long timerJobBatches;
    private static long timerActors;
    private static long currentTileRefreshes;
    private static long fallUpdates;
    private static long flipUpdates;
    private static long rotationUpdates;
    private static long walkJumpUpdates;
    private static long movementSpeedUpdates;
    private static long visibilityJobsSkipped;
    private static long visibilityFrames;
    private static long visibilityActors;

    internal CooperativeActorParallelJobRunner()
    {
        runTimerRangeAction = RunTimerRange;
    }

    public bool TrySkipAllBatches(
        Job<Actor> job,
        int batchCount,
        float elapsed)
    {
        if (job.id.Equals(
                PrepareJobId,
                StringComparison.Ordinal))
        {
            // 每个真实 job 都会自行提交容器增删并准备数组，
            // 因而无需再为 Batch.prepare 唤醒一轮 worker。
            return true;
        }

        if (!job.id.Equals(
                UpdateVisibilityJobId,
                StringComparison.Ordinal) ||
            !PerformanceSettings.EnableFramePriorityScheduler)
        {
            return false;
        }

        // 可见性由每个渲染帧统一刷新，不再为每个逻辑 tick
        // 调度一轮只返回的空 worker 工作。
        if (Bench.bench_enabled)
        {
            Interlocked.Add(
                ref visibilityJobsSkipped,
                batchCount);
        }

        return true;
    }

    public bool TryRunGroup(
        IReadOnlyList<BatchActors> batches,
        int jobIndex,
        int[] activeBatchIndices,
        int activeBatchCount,
        float elapsed)
    {
        if (activeBatchCount == 0 ||
            !batches[activeBatchIndices[0]]
                .jobs_parallel[jobIndex]
                .id
                .Equals(
                    UpdateTimersJobId,
                    StringComparison.Ordinal))
        {
            return false;
        }

        int rangeCount = 0;
        int actorCount = 0;
        EnsureTimerRangeCapacity(
            activeBatchCount * 2);
        for (int i = 0;
             i < activeBatchCount;
             i++)
        {
            BatchActors batch =
                batches[
                    activeBatchIndices[i]];
            ObjectContainer<Actor> container =
                batch.jobs_parallel[
                        jobIndex]
                    .container;
            if (container.Count <= 0 &&
                !container.isDirtyContainer())
            {
                batch._array =
                    Array.Empty<Actor>();
                batch._count = 0;
                continue;
            }

            container.checkAddRemove();
            Actor[] actors =
                container.getFastSimpleArray() ??
                Array.Empty<Actor>();
            int count = container.Count;
            batch._array = actors;
            batch._count = count;
            actorCount += count;
            int batchRangeCount =
                (count + TimerRangeSize - 1) /
                TimerRangeSize;
            EnsureTimerRangeCapacity(
                rangeCount +
                batchRangeCount);
            for (int start = 0;
                 start < count;
                 start += TimerRangeSize)
            {
                timerRanges[rangeCount++] =
                    new TimerRange(
                        actors,
                        start,
                        Math.Min(
                            count,
                            start +
                            TimerRangeSize));
            }
        }

        activeTimerElapsed = elapsed;
        activeTimerDeltaTime =
            PerformanceSettings
                .FixedSimulationStepSeconds;
        activeTimerTimeScaleMultiplier =
            Math.Max(
                0f,
                elapsed /
                PerformanceSettings
                    .FixedSimulationStepSeconds);
        activeTimerPaused =
            World.world.isPaused();
        collectTimerDiagnostics =
            Bench.bench_enabled;
        if (collectTimerDiagnostics)
        {
            Array.Clear(
                timerRangeMetrics,
                0,
                rangeCount);
        }

        try
        {
            SimulationWorkerPool.Instance
                .RunIndexed(
                    0,
                    rangeCount,
                    runTimerRangeAction);
            if (collectTimerDiagnostics)
            {
                CommitTimerDiagnostics(
                    activeBatchCount,
                    actorCount,
                    rangeCount);
            }
        }
        finally
        {
            activeTimerElapsed = 0f;
            activeTimerDeltaTime = 0f;
            activeTimerTimeScaleMultiplier =
                0f;
            activeTimerPaused = false;
            collectTimerDiagnostics = false;
        }

        return true;
    }

    public bool TryRun(
        BatchActors batch,
        Job<Actor> job,
        float elapsed)
    {
        return false;
    }

    /// <summary>
    /// 镜头可见性属于表现状态。帧优先模式下每个渲染帧刷新一次，
    /// 避免在高倍速的每个逻辑 tick 中重复扫描全部角色。
    /// 调用前必须已跨过角色后台写屏障。
    /// </summary>
    internal static void RefreshFrameVisibility()
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            !Config.game_loaded ||
            SmoothLoader.isLoading() ||
            lastVisibilityFrame == Time.frameCount)
        {
            return;
        }

        ActorManager manager = World.world?.units;
        if (manager == null)
        {
            return;
        }

        lastVisibilityFrame = Time.frameCount;
        manager.checkContainer();
        manager.prepareArray();
        Actor[] actors = manager.getSimpleArray();
        int count = manager.Count;
        bool renderGameplay = MapBox.isRenderGameplay();
        int updated = 0;
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            ActorAsset asset = actor.asset;
            if (!asset.has_sprite_renderer)
            {
                continue;
            }

            if (actor.isInMagnet() ||
                actor.isInsideSomething())
            {
                actor.is_visible = false;
            }
            else if (renderGameplay)
            {
                actor.is_visible =
                    actor.current_tile.zone.visible;
            }
            else
            {
                actor.is_visible =
                    asset.visible_on_minimap;
            }

            updated++;
        }

        if (Bench.bench_enabled)
        {
            Interlocked.Increment(ref visibilityFrames);
            Interlocked.Add(ref visibilityActors, updated);
        }
    }

    internal static string GetDiagnostics()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "timer_batches={0} actors={1} " +
            "active_calls={2}/{3}/{4}/{5}/{6}(tile/fall/flip/rotation/jump) " +
            "speed={7} visibility={8}(jobs_skipped) " +
            "frames={9} actors={10}",
            Interlocked.Read(ref timerJobBatches),
            Interlocked.Read(ref timerActors),
            Interlocked.Read(ref currentTileRefreshes),
            Interlocked.Read(ref fallUpdates),
            Interlocked.Read(ref flipUpdates),
            Interlocked.Read(ref rotationUpdates),
            Interlocked.Read(ref walkJumpUpdates),
            Interlocked.Read(ref movementSpeedUpdates),
            Interlocked.Read(ref visibilityJobsSkipped),
            Interlocked.Read(ref visibilityFrames),
            Interlocked.Read(ref visibilityActors));
    }

    private void RunTimerRange(
        int rangeIndex)
    {
        TimerRange range =
            timerRanges[rangeIndex];
        TimerRangeMetrics metrics = default;
        for (int i = range.Start;
             i < range.End;
             i++)
        {
            Actor actor = range.Actors[i];
            actor._update_done = false;
            actor._beh_skip = false;

            if (actor.timer_jump_animation > 0f)
            {
                actor.timer_jump_animation -=
                    activeTimerElapsed;
            }

            if (actor.dirty_current_tile ||
                (actor._next_step_tile != null &&
                 (float)Toolbox.SquaredDistTile(
                     actor.current_tile,
                     actor._next_step_tile) > 4f))
            {
                actor.findCurrentTile();
                metrics.TileRefreshes++;
            }

            bool alive = actor.isAlive();
            actor._is_in_liquid =
                actor.current_tile.is_liquid &&
                actor.move_jump_offset.y == 0f &&
                actor.position_height <= 0f &&
                alive;

            bool cultiwayFlying =
                alive &&
                actor.isFlying() &&
                actor.data.hasFlag(
                    ContentActorDataKeys
                        .IsFlying_flag);
            if (cultiwayFlying ||
                (actor.asset.update_z &&
                 actor.position_height != 0f))
            {
                actor.updateFall();
                metrics.FallUpdates++;
            }

            if (actor.attackedBy != null &&
                !actor.attackedBy.isAlive())
            {
                actor.attackedBy = null;
            }

            if (actor.is_inside_boat)
            {
                continue;
            }

            if (NeedsFlipUpdate(actor))
            {
                actor.updateFlipRotation(
                    activeTimerElapsed);
                metrics.FlipUpdates++;
            }

            if (actor.under_forces)
            {
                for (int forceStep = 0;
                     (float)forceStep <
                     activeTimerTimeScaleMultiplier;
                     forceStep++)
                {
                    actor.updateVelocity();
                }
            }

            if (activeTimerPaused || !alive)
            {
                continue;
            }

            if (actor.rotation_cooldown > 0f ||
                actor.is_unconscious ||
                actor.target_angle.z != 0f)
            {
                actor.updateRotations(
                    activeTimerElapsed);
                metrics.RotationUpdates++;
            }

            if (actor.attack_timer >= 0f)
            {
                actor.attack_timer -=
                    activeTimerElapsed;
            }

            if (MayUpdateWalkJump(actor))
            {
                actor.updateWalkJump(
                    activeTimerDeltaTime);
                metrics.WalkJumpUpdates++;
            }

            if (actor._timeout_targets >= 0f)
            {
                actor._timeout_targets -=
                    activeTimerDeltaTime;
            }

            if (actor.timer_action >= 0f)
            {
                actor.timer_action -=
                    activeTimerElapsed;
            }

            if (actor.isAllowedToLookForEnemies())
            {
                actor.targets_to_ignore_timer
                    .update(activeTimerElapsed);
            }

            if (actor.actor_scale !=
                actor.target_scale)
            {
                actor.updateChangeScale(
                    activeTimerElapsed);
            }

            if (!actor.is_immovable &&
                actor.is_moving)
            {
                if (actor
                        ._precalc_movement_speed_skips >
                    0)
                {
                    actor
                        ._precalc_movement_speed_skips--;
                }
                else
                {
                    actor.precalcMovementSpeed();
                }

                metrics.MovementSpeedUpdates++;
            }
        }

        if (collectTimerDiagnostics)
        {
            timerRangeMetrics[rangeIndex] =
                metrics;
        }
    }

    private void CommitTimerDiagnostics(
        int batchCount,
        int actorCount,
        int rangeCount)
    {
        long tileRefreshCount = 0L;
        long fallCount = 0L;
        long flipCount = 0L;
        long rotationCount = 0L;
        long jumpCount = 0L;
        long speedCount = 0L;
        for (int i = 0; i < rangeCount; i++)
        {
            TimerRangeMetrics metrics =
                timerRangeMetrics[i];
            tileRefreshCount +=
                metrics.TileRefreshes;
            fallCount += metrics.FallUpdates;
            flipCount += metrics.FlipUpdates;
            rotationCount +=
                metrics.RotationUpdates;
            jumpCount +=
                metrics.WalkJumpUpdates;
            speedCount +=
                metrics.MovementSpeedUpdates;
        }

        Interlocked.Add(
            ref timerJobBatches,
            batchCount);
        Interlocked.Add(
            ref timerActors,
            actorCount);
        Interlocked.Add(
            ref currentTileRefreshes,
            tileRefreshCount);
        Interlocked.Add(
            ref fallUpdates,
            fallCount);
        Interlocked.Add(
            ref flipUpdates,
            flipCount);
        Interlocked.Add(
            ref rotationUpdates,
            rotationCount);
        Interlocked.Add(
            ref walkJumpUpdates,
            jumpCount);
        Interlocked.Add(
            ref movementSpeedUpdates,
            speedCount);
    }

    private void EnsureTimerRangeCapacity(
        int capacity)
    {
        if (timerRanges.Length >= capacity)
        {
            return;
        }

        int nextCapacity = Math.Max(
            PerformanceSettings
                .SimulationBatchSize,
            capacity);
        Array.Resize(
            ref timerRanges,
            nextCapacity);
        Array.Resize(
            ref timerRangeMetrics,
            nextCapacity);
    }

    private static bool NeedsFlipUpdate(Actor actor)
    {
        if (!actor.asset.can_flip)
        {
            return false;
        }

        float settledAngle =
            actor.flip ? 180f : 0f;
        return actor.flip_angle != settledAngle ||
               actor.target_angle.y !=
               actor.flip_angle;
    }

    private static bool MayUpdateWalkJump(Actor actor)
    {
        if ((!actor.is_visible &&
             actor.move_jump_offset.y == 0f) ||
            actor.position_height > 0f ||
            actor.asset.disable_jump_animation)
        {
            return false;
        }

        return actor.is_moving ||
               actor.move_jump_offset.y != 0f ||
               actor._jump_time != 0f;
    }

    private readonly struct TimerRange
    {
        internal TimerRange(
            Actor[] actors,
            int start,
            int end)
        {
            Actors = actors;
            Start = start;
            End = end;
        }

        internal Actor[] Actors { get; }
        internal int Start { get; }
        internal int End { get; }
    }

    private struct TimerRangeMetrics
    {
        internal int TileRefreshes;
        internal int FallUpdates;
        internal int FlipUpdates;
        internal int RotationUpdates;
        internal int WalkJumpUpdates;
        internal int MovementSpeedUpdates;
    }
}

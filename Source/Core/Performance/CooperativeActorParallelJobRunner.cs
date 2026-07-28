using System;
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
    private const string UpdateTimersJobId = "update_timers";
    private const string UpdateVisibilityJobId = "update_visibility";

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

    public bool TryRun(
        BatchActors batch,
        Job<Actor> job,
        float elapsed)
    {
        if (job.id.Equals(
                UpdateTimersJobId,
                StringComparison.Ordinal))
        {
            RunUpdateTimers(
                batch,
                job.container,
                elapsed);
            return true;
        }

        if (job.id.Equals(
                UpdateVisibilityJobId,
                StringComparison.Ordinal) &&
            PerformanceSettings.EnableFramePriorityScheduler)
        {
            if (Bench.bench_enabled)
            {
                Interlocked.Increment(
                    ref visibilityJobsSkipped);
            }

            return true;
        }

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

    private static void RunUpdateTimers(
        BatchActors batch,
        ObjectContainer<Actor> container,
        float elapsed)
    {
        if (container.Count <= 0 &&
            !container.isDirtyContainer())
        {
            return;
        }

        container.checkAddRemove();
        Actor[] actors =
            container.getFastSimpleArray();
        int count = container.Count;
        batch._array = actors;
        batch._count = count;

        MapBox world = World.world;
        bool paused = world.isPaused();
        float deltaTime = world.delta_time;
        float timeScaleMultiplier =
            Config.time_scale_asset.multiplier;
        bool collectDiagnostics =
            Bench.bench_enabled;
        int tileRefreshCount = 0;
        int fallCount = 0;
        int flipCount = 0;
        int rotationCount = 0;
        int jumpCount = 0;
        int speedCount = 0;

        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor._update_done = false;
            actor._beh_skip = false;

            if (actor.timer_jump_animation > 0f)
            {
                actor.timer_jump_animation -= elapsed;
            }

            if (actor.dirty_current_tile ||
                (actor._next_step_tile != null &&
                 (float)Toolbox.SquaredDistTile(
                     actor.current_tile,
                     actor._next_step_tile) > 4f))
            {
                actor.findCurrentTile();
                tileRefreshCount++;
            }

            bool alive = actor.isAlive();
            actor._is_in_liquid =
                actor.current_tile.is_liquid &&
                actor.move_jump_offset.y == 0f &&
                actor.position_height <= 0f &&
                alive;

            bool cultiwayFlying =
                alive &&
                actor.data.hasFlag(
                    ContentActorDataKeys.IsFlying_flag);
            if (cultiwayFlying ||
                (actor.asset.update_z &&
                 actor.position_height != 0f))
            {
                actor.updateFall();
                fallCount++;
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
                actor.updateFlipRotation(elapsed);
                flipCount++;
            }

            if (actor.under_forces)
            {
                for (int forceStep = 0;
                     (float)forceStep <
                     timeScaleMultiplier;
                     forceStep++)
                {
                    actor.updateVelocity();
                }
            }

            if (paused || !actor.isAlive())
            {
                continue;
            }

            if (actor.rotation_cooldown > 0f ||
                actor.is_unconscious ||
                actor.target_angle.z != 0f)
            {
                actor.updateRotations(elapsed);
                rotationCount++;
            }

            if (actor.attack_timer >= 0f)
            {
                actor.attack_timer -= elapsed;
            }

            if (MayUpdateWalkJump(actor))
            {
                actor.updateWalkJump(deltaTime);
                jumpCount++;
            }

            if (actor._timeout_targets >= 0f)
            {
                actor._timeout_targets -= deltaTime;
            }

            if (actor.timer_action >= 0f)
            {
                actor.timer_action -= elapsed;
            }

            if (actor.isAllowedToLookForEnemies())
            {
                actor.targets_to_ignore_timer.update(elapsed);
            }

            if (actor.actor_scale != actor.target_scale)
            {
                actor.updateChangeScale(elapsed);
            }

            if (!actor.is_immovable &&
                actor.is_moving)
            {
                actor.precalcMovementSpeed();
                speedCount++;
            }
        }

        if (!collectDiagnostics)
        {
            return;
        }

        Interlocked.Increment(ref timerJobBatches);
        Interlocked.Add(ref timerActors, count);
        Interlocked.Add(
            ref currentTileRefreshes,
            tileRefreshCount);
        Interlocked.Add(ref fallUpdates, fallCount);
        Interlocked.Add(ref flipUpdates, flipCount);
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
}

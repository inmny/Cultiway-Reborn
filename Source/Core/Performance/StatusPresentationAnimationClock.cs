using System;
using System.Threading;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 状态动画只影响表现。帧优先模式不再让每个逻辑 tick 修改动画字段，
/// 而是在生成稳定快照时由世界时间直接解析当前帧。
/// </summary>
internal static class StatusPresentationAnimationClock
{
    private static int snapshotMode;
    private static int worldGeneration = -1;
    private static double enabledAtWorldTime;

    internal static void SetSnapshotMode(bool enabled)
    {
        int next = enabled ? 1 : 0;
        int previous = Interlocked.Exchange(
            ref snapshotMode,
            next);
        int generation = SimulationTime.Generation;
        if (!enabled)
        {
            return;
        }

        if (previous == 0 ||
            worldGeneration != generation)
        {
            worldGeneration = generation;
            enabledAtWorldTime =
                World.world?.getCurWorldTime() ?? 0.0;
        }
    }

    internal static void Resolve(
        Status status,
        int frameCount,
        float frameInterval,
        out int frame,
        out float timeUntilNextFrame)
    {
        frame = Mathf.Clamp(
            status.anim_frame,
            0,
            Math.Max(0, frameCount - 1));
        timeUntilNextFrame =
            Math.Max(0f, status._anim_timer);
        if (frameCount <= 0 ||
            !status.asset.animated ||
            status.asset.texture == null ||
            Volatile.Read(ref snapshotMode) == 0)
        {
            return;
        }

        EnsureGeneration();
        double worldTime =
            World.world?.getCurWorldTime() ?? 0.0;
        double statusCreatedAt =
            status.data?.created_time ??
            worldTime;
        double animationOrigin =
            Math.Max(
                enabledAtWorldTime,
                statusCreatedAt);
        double elapsed =
            Math.Max(
                0.0,
                worldTime - animationOrigin);
        ResolveState(
            status.anim_frame,
            Math.Max(0f, status._anim_timer),
            elapsed,
            status.asset.loop,
            frameCount,
            Math.Max(0.0001f, frameInterval),
            out frame,
            out timeUntilNextFrame);
    }

    private static void ResolveState(
        int baseFrame,
        float baseTimer,
        double elapsed,
        bool loop,
        int frameCount,
        float frameInterval,
        out int frame,
        out float timeUntilNextFrame)
    {
        if (elapsed < baseTimer)
        {
            frame = Mathf.Clamp(
                baseFrame,
                0,
                frameCount - 1);
            timeUntilNextFrame =
                (float)(baseTimer - elapsed);
            return;
        }

        double afterFirst =
            elapsed - baseTimer;
        long advances =
            1L +
            (long)Math.Floor(
                afterFirst / frameInterval);
        long resolvedFrame =
            baseFrame + advances;
        frame = loop
            ? (int)(resolvedFrame % frameCount)
            : (int)Math.Min(
                frameCount - 1L,
                resolvedFrame);
        double withinFrame =
            afterFirst -
            Math.Floor(afterFirst / frameInterval) *
            frameInterval;
        timeUntilNextFrame =
            (float)Math.Max(
                0.0,
                frameInterval - withinFrame);
    }

    private static void EnsureGeneration()
    {
        int generation = SimulationTime.Generation;
        if (worldGeneration == generation)
        {
            return;
        }

        worldGeneration = generation;
        enabledAtWorldTime =
            World.world?.getCurWorldTime() ?? 0.0;
    }
}

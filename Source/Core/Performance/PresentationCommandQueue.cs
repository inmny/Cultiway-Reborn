using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace Cultiway.Core.Performance;

/// <summary>
/// 模拟线程到 Unity 主线程的有序表现命令队列。
/// 命令只保存值类型坐标、稳定地块 ID 和资源 ID，不跨线程持有世界对象。
/// </summary>
internal static class PresentationCommandQueue
{
    private enum CommandKind : byte
    {
        EffectAtPosition,
        EffectAtTile
    }

    private readonly struct Command
    {
        internal Command(
            long sequence,
            int worldGeneration,
            CommandKind kind,
            string assetId,
            Vector3 position,
            int tileId,
            float scale,
            int initialAnimationFrame,
            long enqueuedAt)
        {
            Sequence = sequence;
            WorldGeneration = worldGeneration;
            Kind = kind;
            AssetId = assetId;
            Position = position;
            TileId = tileId;
            Scale = scale;
            InitialAnimationFrame = initialAnimationFrame;
            EnqueuedAt = enqueuedAt;
        }

        internal long Sequence { get; }
        internal int WorldGeneration { get; }
        internal CommandKind Kind { get; }
        internal string AssetId { get; }
        internal Vector3 Position { get; }
        internal int TileId { get; }
        internal float Scale { get; }
        internal int InitialAnimationFrame { get; }
        internal long EnqueuedAt { get; }
    }

    private static readonly ConcurrentQueue<Command> queue = new();

    private static long nextSequence;
    private static long pendingCommands;
    private static long maximumPendingCommands;
    private static long enqueuedCommands;
    private static long executedCommands;
    private static long staleCommands;
    private static long failedCommands;
    private static long totalLatencyTicks;
    private static long maximumLatencyTicks;
    private static long lastExecutedSequence;

    internal static void EnqueueEffectAt(
        string effectId,
        Vector3 position,
        float scale,
        int initialAnimationFrame = -1)
    {
        if (string.IsNullOrEmpty(effectId))
        {
            return;
        }

        Enqueue(
            CommandKind.EffectAtPosition,
            effectId,
            position,
            -1,
            Mathf.Max(0.1f, scale),
            initialAnimationFrame);
    }

    internal static void EnqueueEffectAtTile(
        string effectId,
        WorldTile tile,
        float scale,
        int initialAnimationFrame = -1)
    {
        if (string.IsNullOrEmpty(effectId) || tile?.data == null)
        {
            return;
        }

        Enqueue(
            CommandKind.EffectAtTile,
            effectId,
            tile.posV3,
            tile.data.tile_id,
            Mathf.Max(0.1f, scale),
            initialAnimationFrame);
    }

    internal static void DrainMainThread()
    {
        MapBox world = World.world;
        if (world == null)
        {
            return;
        }

        int generation = SimulationTime.Generation;
        while (queue.TryDequeue(out Command command))
        {
            Interlocked.Decrement(ref pendingCommands);
            if (command.WorldGeneration != generation)
            {
                Interlocked.Increment(ref staleCommands);
                continue;
            }

            try
            {
                Execute(world, in command);
                Interlocked.Increment(ref executedCommands);
                Interlocked.Exchange(
                    ref lastExecutedSequence,
                    command.Sequence);
                RecordLatency(
                    Math.Max(
                        0L,
                        Stopwatch.GetTimestamp() - command.EnqueuedAt));
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref failedCommands);
                ModClass.LogErrorConcurrent(
                    "[PresentationCommand] 执行失败 sequence=" +
                    command.Sequence +
                    " kind=" +
                    command.Kind +
                    " asset=" +
                    command.AssetId +
                    ": " +
                    exception);
            }
        }
    }

    internal static void Clear()
    {
        while (queue.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref pendingCommands, 0L);
    }

    internal static string GetDiagnostics()
    {
        long executed = Interlocked.Read(ref executedCommands);
        return string.Format(
            CultureInfo.InvariantCulture,
            "enqueued={0} executed={1} pending={2} max_pending={3} " +
            "stale={4} failed={5} latency={6:0.00}ms(avg,max={7:0.00}) " +
            "last_sequence={8}",
            Interlocked.Read(ref enqueuedCommands),
            executed,
            Interlocked.Read(ref pendingCommands),
            Interlocked.Read(ref maximumPendingCommands),
            Interlocked.Read(ref staleCommands),
            Interlocked.Read(ref failedCommands),
            TicksToMilliseconds(
                Interlocked.Read(ref totalLatencyTicks)) /
            Math.Max(1L, executed),
            TicksToMilliseconds(
                Interlocked.Read(ref maximumLatencyTicks)),
            Interlocked.Read(ref lastExecutedSequence));
    }

    private static void Enqueue(
        CommandKind kind,
        string assetId,
        Vector3 position,
        int tileId,
        float scale,
        int initialAnimationFrame)
    {
        long sequence = Interlocked.Increment(ref nextSequence);
        queue.Enqueue(
            new Command(
                sequence,
                SimulationTime.Generation,
                kind,
                assetId,
                position,
                tileId,
                scale,
                initialAnimationFrame,
                Stopwatch.GetTimestamp()));
        Interlocked.Increment(ref enqueuedCommands);
        long pending = Interlocked.Increment(ref pendingCommands);
        UpdateMaximum(ref maximumPendingCommands, pending);
    }

    private static void Execute(MapBox world, in Command command)
    {
        BaseEffect effect;
        switch (command.Kind)
        {
            case CommandKind.EffectAtPosition:
                effect = EffectsLibrary.spawnAt(
                    command.AssetId,
                    command.Position,
                    command.Scale);
                break;
            case CommandKind.EffectAtTile:
                WorldTile tile = GetTile(world, command.TileId);
                if (tile == null)
                {
                    Interlocked.Increment(ref staleCommands);
                    return;
                }

                effect = EffectsLibrary.spawnAtTile(
                    command.AssetId,
                    tile,
                    command.Scale);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (command.InitialAnimationFrame >= 0 &&
            effect?.sprite_animation != null)
        {
            effect.sprite_animation.setFrameIndex(
                command.InitialAnimationFrame);
        }
    }

    private static WorldTile GetTile(MapBox world, int tileId)
    {
        WorldTile[] tiles = world.tiles_list;
        return tiles != null &&
               (uint)tileId < (uint)tiles.Length
            ? tiles[tileId]
            : null;
    }

    private static void RecordLatency(long elapsedTicks)
    {
        Interlocked.Add(ref totalLatencyTicks, elapsedTicks);
        UpdateMaximum(ref maximumLatencyTicks, elapsedTicks);
    }

    private static void UpdateMaximum(ref long target, long value)
    {
        long maximum = Interlocked.Read(ref target);
        while (value > maximum)
        {
            long previous = Interlocked.CompareExchange(
                ref target,
                value,
                maximum);
            if (previous == maximum)
            {
                break;
            }

            maximum = previous;
        }
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}

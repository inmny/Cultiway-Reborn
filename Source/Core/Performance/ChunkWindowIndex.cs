using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 缓存以 chunk 为中心的固定半径窗口。世界存续期间 chunk 拓扑不变，
/// 伴侣、社交和状态行为无需为每个角色重复执行相同的管理器查找。
/// </summary>
internal static class ChunkWindowIndex
{
    private static readonly ConcurrentDictionary<WindowKey, MapChunk[]>
        Windows = new();
    private static readonly object GenerationLock = new();

    private static int indexedGeneration = -1;

    internal static MapChunk[] Get(
        MapChunk origin,
        int radius)
    {
        if (origin == null)
        {
            return Array.Empty<MapChunk>();
        }

        EnsureCurrentWorld();
        return Windows.GetOrAdd(
            new WindowKey(origin, radius),
            Build);
    }

    internal static void Reset()
    {
        lock (GenerationLock)
        {
            Windows.Clear();
            Volatile.Write(ref indexedGeneration, -1);
        }
    }

    private static void EnsureCurrentWorld()
    {
        int generation = SimulationTime.Generation;
        if (Volatile.Read(ref indexedGeneration) == generation)
        {
            return;
        }

        lock (GenerationLock)
        {
            if (indexedGeneration == generation)
            {
                return;
            }

            Windows.Clear();
            Volatile.Write(
                ref indexedGeneration,
                generation);
        }
    }

    private static MapChunk[] Build(WindowKey key)
    {
        int diameter = key.Radius * 2 + 1;
        var buffer = new MapChunk[diameter * diameter];
        int count = 0;
        MapChunkManager manager =
            World.world.map_chunk_manager;
        for (int x = key.Origin.x - key.Radius;
             x <= key.Origin.x + key.Radius;
             x++)
        {
            for (int y = key.Origin.y - key.Radius;
                 y <= key.Origin.y + key.Radius;
                 y++)
            {
                MapChunk chunk = manager.get(x, y);
                if (chunk != null)
                {
                    buffer[count++] = chunk;
                }
            }
        }

        if (count == buffer.Length)
        {
            return buffer;
        }

        var result = new MapChunk[count];
        Array.Copy(buffer, result, count);
        return result;
    }

    private readonly struct WindowKey :
        IEquatable<WindowKey>
    {
        internal WindowKey(MapChunk origin, int radius)
        {
            Origin = origin;
            Radius = Math.Max(0, radius);
        }

        internal MapChunk Origin { get; }
        internal int Radius { get; }

        public bool Equals(WindowKey other)
        {
            return ReferenceEquals(Origin, other.Origin) &&
                   Radius == other.Radius;
        }

        public override bool Equals(object obj)
        {
            return obj is WindowKey other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked(
                RuntimeHelpers.GetHashCode(Origin) * 397 ^
                Radius);
        }
    }
}

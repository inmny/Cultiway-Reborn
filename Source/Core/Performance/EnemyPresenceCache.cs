using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace Cultiway.Core.Performance;

/// <summary>
/// 缓存当前逻辑 tick 内各王国是否存在任意有单位或建筑的敌对王国。
/// 该结论只用于跳过必然为空的分块敌人扫描，不改变分块缓存键与随机数推进。
/// </summary>
internal static class EnemyPresenceCache
{
    private static readonly Dictionary<Kingdom, bool> Cache =
        new();
    private static readonly Dictionary<
        Kingdom,
        HashSet<int>> NegativeKeys = new();
    private static readonly EnemyFinderData EmptyResult =
        new();

    [ThreadStatic]
    private static bool preparationActive;

    private static long queries;
    private static long cacheHits;
    private static long populatedEnemyKingdoms;
    private static long emptyEnemyKingdoms;
    private static long skippedChunkBuilds;
    private static long negativeKeyReuses;

    internal static bool IsPreparationActive =>
        preparationActive;

    internal static EnemyFinderData SharedEmptyResult =>
        EmptyResult;

    internal static void BeginPreparation()
    {
        Cache.Clear();
        preparationActive = true;
    }

    internal static void EndPreparation()
    {
        preparationActive = false;
        Cache.Clear();
    }

    internal static bool TryGetNegativeResult(
        Kingdom kingdom,
        int key)
    {
        if (kingdom == null)
        {
            return false;
        }

        if (!NegativeKeys.TryGetValue(
                kingdom,
                out HashSet<int> keys) ||
            !keys.Contains(key))
        {
            return false;
        }

        if (Bench.bench_enabled)
        {
            Interlocked.Increment(
                ref negativeKeyReuses);
        }

        return true;
    }

    internal static bool TryGetPreparationEmptyResult(
        WorldTile tile,
        Kingdom kingdom,
        int range,
        out EnemyFinderData result)
    {
        if (!preparationActive ||
            tile == null ||
            kingdom == null ||
            HasPopulatedEnemy(kingdom))
        {
            result = null;
            return false;
        }

        int key =
            tile.chunk.id * 10000 +
            range;
        if (TryGetNegativeResult(
                kingdom,
                key))
        {
            EnemiesFinder.counter_reused++;
            result = EmptyResult;
            return true;
        }

        AddNegativeResult(
            kingdom,
            key,
            range);
        result = EmptyResult;
        return true;
    }

    internal static void AddNegativeResult(
        Kingdom kingdom,
        int key,
        int range)
    {
        if (!NegativeKeys.TryGetValue(
                kingdom,
                out HashSet<int> keys))
        {
            keys = new HashSet<int>();
            NegativeKeys.Add(
                kingdom,
                keys);
        }

        keys.Add(key);
        if (!kingdom.asset.force_look_all_chunks &&
            range != 0)
        {
            Randy.randomChance(0.8f);
        }

        RecordSkippedChunkBuild();
    }

    internal static void ClearNegativeKeys(
        Kingdom kingdom)
    {
        if (kingdom != null)
        {
            NegativeKeys.Remove(kingdom);
        }
    }

    internal static bool HasPopulatedEnemy(
        Kingdom mainKingdom)
    {
        bool collectDiagnostics =
            Bench.bench_enabled;
        if (collectDiagnostics)
        {
            Interlocked.Increment(ref queries);
        }

        if (Cache.TryGetValue(
                mainKingdom,
                out bool result))
        {
            if (collectDiagnostics)
            {
                Interlocked.Increment(ref cacheHits);
            }

            return result;
        }

        result = FindPopulatedEnemy(mainKingdom);
        Cache.Add(mainKingdom, result);
        if (collectDiagnostics)
        {
            if (result)
            {
                Interlocked.Increment(
                    ref populatedEnemyKingdoms);
            }
            else
            {
                Interlocked.Increment(
                    ref emptyEnemyKingdoms);
            }
        }

        return result;
    }

    internal static void RecordSkippedChunkBuild()
    {
        if (Bench.bench_enabled)
        {
            Interlocked.Increment(
                ref skippedChunkBuilds);
        }
    }

    internal static void Clear()
    {
        preparationActive = false;
        Cache.Clear();
        NegativeKeys.Clear();
    }

    internal static string GetDiagnostics()
    {
        long queryCount =
            Interlocked.Read(ref queries);
        long hitCount =
            Interlocked.Read(ref cacheHits);
        return string.Format(
            CultureInfo.InvariantCulture,
            "queries={0} cache_hits={1} ({2:0.0}%) kingdoms={3}/{4}" +
            "(enemy/empty) chunk_builds_skipped={5} negative_reuses={6}",
            queryCount,
            hitCount,
            queryCount == 0L
                ? 0.0
                : hitCount * 100.0 / queryCount,
            Interlocked.Read(
                ref populatedEnemyKingdoms),
            Interlocked.Read(
                ref emptyEnemyKingdoms),
            Interlocked.Read(
                ref skippedChunkBuilds),
            Interlocked.Read(
                ref negativeKeyReuses));
    }

    private static bool FindPopulatedEnemy(
        Kingdom mainKingdom)
    {
        bool peacefulMonsters =
            WorldLawLibrary
                .world_law_peaceful_monsters
                .isEnabled();
        if (mainKingdom.asset.mobs &&
            peacefulMonsters)
        {
            return false;
        }

        if (HasPopulatedEnemyIn(
                mainKingdom,
                World.world.kingdoms,
                peacefulMonsters))
        {
            return true;
        }

        return HasPopulatedEnemyIn(
            mainKingdom,
            World.world.kingdoms_wild,
            peacefulMonsters);
    }

    private static bool HasPopulatedEnemyIn(
        Kingdom mainKingdom,
        IEnumerable<Kingdom> candidates,
        bool peacefulMonsters)
    {
        foreach (Kingdom candidate in candidates)
        {
            if (ReferenceEquals(
                    candidate,
                    mainKingdom) ||
                candidate.units.Count == 0 &&
                candidate.buildings.Count == 0 ||
                peacefulMonsters &&
                candidate.asset.mobs)
            {
                continue;
            }

            if (mainKingdom.isEnemy(candidate))
            {
                return true;
            }
        }

        return false;
    }
}

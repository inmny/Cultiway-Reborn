using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Utils;

namespace Cultiway.Core.Performance;

/// <summary>
/// 按 World.units 原顺序并行重建岛屿角色成员表。
/// 每个 worker 只写自己的计数单元和预先分配的数组区间，
/// 最终提交顺序与原版逐角色 Add 完全一致。
/// </summary>
internal static class ParallelIslandActorMembership
{
    private const int ParallelThreshold = 1024;

    private static readonly Action<int>
        ClassifyRangeAction =
            ClassifyRange;
    private static readonly Action<int>
        ScatterRangeAction =
            ScatterRange;
    private static readonly Dictionary<TileIsland, int>
        IslandIndices = new();

    private static int[] actorIslandIndices =
        Array.Empty<int>();
    private static int[] workIslandCounts =
        Array.Empty<int>();
    private static int[] workIslandOffsets =
        Array.Empty<int>();
    private static Actor[][] actorsByIsland =
        Array.Empty<Actor[]>();
    private static int[] actorCountsByIsland =
        Array.Empty<int>();
    private static List<Actor> activeActors;
    private static int activeIslandCount;
    private static int activeWorkCount;
    private static int validatedGeneration = -1;

    internal static void Rebuild(
        List<Actor> actors)
    {
        ListPool<TileIsland> islands =
            World.world.islands_calculator.islands;
        if (actors.Count < ParallelThreshold ||
            islands.Count == 0)
        {
            RebuildSerial(actors, islands);
            return;
        }

        Prepare(actors, islands);
        activeActors = actors;
        activeIslandCount = islands.Count;
        activeWorkCount =
            (actors.Count +
             PerformanceSettings.SimulationBatchSize -
             1) /
            PerformanceSettings.SimulationBatchSize;
        try
        {
            int cellCount =
                activeWorkCount *
                activeIslandCount;
            EnsureWorkStorage(cellCount);
            Array.Clear(
                workIslandCounts,
                0,
                cellCount);

            SimulationWorkerPool.Instance.RunIndexed(
                0,
                activeWorkCount,
                ClassifyRangeAction);
            PrepareStableOffsets();
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                activeWorkCount,
                ScatterRangeAction);
            Commit(islands);
            ValidateStableOrderOnce(islands);
        }
        finally
        {
            activeActors = null;
            activeIslandCount = 0;
            activeWorkCount = 0;
        }
    }

    private static void Prepare(
        List<Actor> actors,
        ListPool<TileIsland> islands)
    {
        IslandIndices.Clear();
        int islandCount = islands.Count;
        for (int i = 0; i < islandCount; i++)
        {
            TileIsland island = islands[i];
            island.actors.Clear();
            IslandIndices.Add(island, i);
        }

        if (actorIslandIndices.Length <
            actors.Count)
        {
            actorIslandIndices =
                new int[actors.Count];
        }

        if (actorsByIsland.Length ==
            islandCount)
        {
            return;
        }

        actorsByIsland =
            new Actor[islandCount][];
        actorCountsByIsland =
            new int[islandCount];
        for (int i = 0; i < islandCount; i++)
        {
            actorsByIsland[i] =
                Array.Empty<Actor>();
        }
    }

    private static void EnsureWorkStorage(
        int cellCount)
    {
        if (workIslandCounts.Length <
            cellCount)
        {
            workIslandCounts =
                new int[cellCount];
            workIslandOffsets =
                new int[cellCount];
        }
    }

    private static void ClassifyRange(
        int workIndex)
    {
        GetRange(
            workIndex,
            out int start,
            out int end);
        int countOffset =
            workIndex *
            activeIslandCount;
        for (int i = start; i < end; i++)
        {
            Actor actor = activeActors[i];
            if (!actor.isAlive())
            {
                actorIslandIndices[i] = -1;
                continue;
            }

            TileIsland island =
                actor.current_tile
                    .region
                    .island;
            if (!IslandIndices.TryGetValue(
                    island,
                    out int islandIndex))
            {
                throw new InvalidOperationException(
                    "角色所在岛屿不属于当前岛屿容器");
            }

            actorIslandIndices[i] =
                islandIndex;
            workIslandCounts[
                countOffset +
                islandIndex]++;
        }
    }

    private static void PrepareStableOffsets()
    {
        for (int islandIndex = 0;
             islandIndex < activeIslandCount;
             islandIndex++)
        {
            int actorCount = 0;
            for (int workIndex = 0;
                 workIndex < activeWorkCount;
                 workIndex++)
            {
                int cell =
                    workIndex *
                    activeIslandCount +
                    islandIndex;
                workIslandOffsets[cell] =
                    actorCount;
                actorCount +=
                    workIslandCounts[cell];
            }

            int previousCount =
                actorCountsByIsland[
                    islandIndex];
            Actor[] buffer =
                actorsByIsland[islandIndex];
            if (buffer.Length < actorCount)
            {
                buffer =
                    new Actor[actorCount];
                actorsByIsland[islandIndex] =
                    buffer;
            }
            else if (previousCount > actorCount)
            {
                Array.Clear(
                    buffer,
                    actorCount,
                    previousCount - actorCount);
            }

            actorCountsByIsland[
                islandIndex] = actorCount;
        }
    }

    private static void ScatterRange(
        int workIndex)
    {
        GetRange(
            workIndex,
            out int start,
            out int end);
        int offsetBase =
            workIndex *
            activeIslandCount;
        for (int i = start; i < end; i++)
        {
            int islandIndex =
                actorIslandIndices[i];
            if (islandIndex < 0)
            {
                continue;
            }

            int offsetCell =
                offsetBase +
                islandIndex;
            int targetIndex =
                workIslandOffsets[
                    offsetCell]++;
            actorsByIsland[islandIndex][
                targetIndex] =
                activeActors[i];
        }
    }

    private static void Commit(
        ListPool<TileIsland> islands)
    {
        for (int islandIndex = 0;
             islandIndex < activeIslandCount;
             islandIndex++)
        {
            List<Actor> target =
                islands[islandIndex].actors;
            Actor[] source =
                actorsByIsland[islandIndex];
            int count =
                actorCountsByIsland[
                    islandIndex];
            for (int actorIndex = 0;
                 actorIndex < count;
                 actorIndex++)
            {
                target.Add(
                    source[actorIndex]);
            }
        }
    }

    private static void ValidateStableOrderOnce(
        ListPool<TileIsland> islands)
    {
        int generation =
            SimulationTime.Generation;
        if (!SystemUtils.IsUnderDeveloper() ||
            validatedGeneration == generation)
        {
            return;
        }

        int[] cursors =
            new int[activeIslandCount];
        for (int i = 0;
             i < activeActors.Count;
             i++)
        {
            Actor actor = activeActors[i];
            if (!actor.isAlive())
            {
                continue;
            }

            int islandIndex =
                actorIslandIndices[i];
            List<Actor> islandActors =
                islands[islandIndex].actors;
            int cursor =
                cursors[islandIndex]++;
            if (cursor >= islandActors.Count ||
                !ReferenceEquals(
                    islandActors[cursor],
                    actor))
            {
                throw new InvalidOperationException(
                    "并行岛屿角色成员顺序与 World.units 不一致");
            }
        }

        for (int i = 0;
             i < activeIslandCount;
             i++)
        {
            if (cursors[i] !=
                islands[i].actors.Count)
            {
                throw new InvalidOperationException(
                    "并行岛屿角色成员数量与 World.units 不一致");
            }
        }

        validatedGeneration = generation;
    }

    private static void GetRange(
        int workIndex,
        out int start,
        out int end)
    {
        start =
            workIndex *
            PerformanceSettings.SimulationBatchSize;
        end = Math.Min(
            activeActors.Count,
            start +
            PerformanceSettings.SimulationBatchSize);
    }

    private static void RebuildSerial(
        List<Actor> actors,
        ListPool<TileIsland> islands)
    {
        for (int i = 0; i < islands.Count; i++)
        {
            islands[i].actors.Clear();
        }

        for (int i = 0; i < actors.Count; i++)
        {
            Actor actor = actors[i];
            if (actor.isAlive())
            {
                actor.current_tile
                    .region
                    .island
                    .actors
                    .Add(actor);
            }
        }
    }
}

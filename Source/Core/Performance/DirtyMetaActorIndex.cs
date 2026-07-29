using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Const;

namespace Cultiway.Core.Performance;

/// <summary>
/// 在一次有序角色扫描中生成各脏元数据管理器需要的紧凑成员表。
/// 元对象列表的清空、写入和脏状态结束仍由原版 beginChecksUnits 流程负责。
/// </summary>
internal static class DirtyMetaActorIndex
{
    private const int SubspeciesIndex = 0;
    private const int FamilyIndex = 1;
    private const int ArmyIndex = 2;
    private const int LanguageIndex = 3;
    private const int ReligionIndex = 4;
    private const int CityIndex = 5;
    private const int ClanIndex = 6;
    private const int KingdomIndex = 7;
    private const int WildKingdomIndex = 8;
    private const int CultureIndex = 9;
    private const int PlotIndex = 10;
    private const int KindCount = 11;

    private const int SubspeciesBit = 1 << SubspeciesIndex;
    private const int FamilyBit = 1 << FamilyIndex;
    private const int ArmyBit = 1 << ArmyIndex;
    private const int LanguageBit = 1 << LanguageIndex;
    private const int ReligionBit = 1 << ReligionIndex;
    private const int CityBit = 1 << CityIndex;
    private const int ClanBit = 1 << ClanIndex;
    private const int KingdomBit = 1 << KingdomIndex;
    private const int WildKingdomBit = 1 << WildKingdomIndex;
    private const int CultureBit = 1 << CultureIndex;
    private const int PlotBit = 1 << PlotIndex;

    private static readonly Action<int> ClassifyWorkItemAction =
        ClassifyWorkItem;
    private static readonly object[] Managers =
        new object[KindCount];
    private static readonly Actor[][] ActorBuffers =
        new Actor[KindCount][];
    private static readonly int[] ActorCounts =
        new int[KindCount];

    private static Actor[] aliveSource;
    private static Actor[] dyingSource;
    private static int[] partitionCounts =
        Array.Empty<int>();
    private static int[] partitionFlags =
        Array.Empty<int>();
    private static int aliveSourceCount;
    private static int dyingSourceCount;
    private static int workItemCount;
    private static int preparingMask;
    private static int activeMask;
    private static int kingdomHasBoatsMask;

    static DirtyMetaActorIndex()
    {
        for (int i = 0; i < KindCount; i++)
        {
            ActorBuffers[i] = Array.Empty<Actor>();
        }
    }

    internal static void Prepare(
        IReadOnlyList<BaseSystemManager> managers,
        Actor[] aliveActors,
        int aliveCount,
        Actor[] dyingActors,
        int dyingCount)
    {
        End();

        int enabledMask = 0;
        for (int i = 0; i < managers.Count; i++)
        {
            BaseSystemManager manager = managers[i];
            if (!manager.isUnitsDirty())
            {
                continue;
            }

            switch (manager)
            {
                case SubspeciesManager:
                    Enable(
                        SubspeciesIndex,
                        manager,
                        ref enabledMask);
                    break;
                case FamilyManager:
                    Enable(
                        FamilyIndex,
                        manager,
                        ref enabledMask);
                    break;
                case ArmyManager:
                    Enable(
                        ArmyIndex,
                        manager,
                        ref enabledMask);
                    break;
                case LanguageManager:
                    Enable(
                        LanguageIndex,
                        manager,
                        ref enabledMask);
                    break;
                case ReligionManager:
                    Enable(
                        ReligionIndex,
                        manager,
                        ref enabledMask);
                    break;
                case CityManager:
                    Enable(
                        CityIndex,
                        manager,
                        ref enabledMask);
                    break;
                case ClanManager:
                    Enable(
                        ClanIndex,
                        manager,
                        ref enabledMask);
                    break;
                case KingdomManager:
                    Enable(
                        KingdomIndex,
                        manager,
                        ref enabledMask);
                    break;
                case WildKingdomsManager:
                    Enable(
                        WildKingdomIndex,
                        manager,
                        ref enabledMask);
                    break;
                case CultureManager:
                    Enable(
                        CultureIndex,
                        manager,
                        ref enabledMask);
                    break;
                case PlotManager:
                    Enable(
                        PlotIndex,
                        manager,
                        ref enabledMask);
                    break;
            }
        }

        if (enabledMask == 0)
        {
            return;
        }

        aliveSource = aliveActors;
        aliveSourceCount = aliveCount;
        dyingSource = dyingActors;
        dyingSourceCount = dyingCount;
        preparingMask = enabledMask;

        int batchSize =
            PerformanceSettings.SimulationBatchSize;
        workItemCount =
            (aliveCount + batchSize - 1) /
            batchSize;
        int slotCount =
            workItemCount * KindCount;
        EnsurePartitionCapacity(slotCount);
        EnsurePartitionFlagCapacity(workItemCount);
        for (int kind = 0;
             kind < KindCount;
             kind++)
        {
            if ((enabledMask & (1 << kind)) != 0)
            {
                EnsureActorBufferCapacity(
                    kind,
                    aliveCount);
            }
        }

        if (workItemCount > 1)
        {
            SimulationWorkerPool.Instance.RunIndexed(
                0,
                workItemCount,
                ClassifyWorkItemAction);
        }
        else if (workItemCount == 1)
        {
            ClassifyWorkItem(0);
        }

        for (int kind = 0;
             kind < KindCount;
             kind++)
        {
            if ((enabledMask & (1 << kind)) == 0)
            {
                continue;
            }

            Actor[] buffer = ActorBuffers[kind];
            int totalCount = 0;
            for (int workIndex = 0;
                 workIndex < workItemCount;
                 workIndex++)
            {
                int count =
                    partitionCounts[
                        workIndex * KindCount +
                        kind];
                if (count == 0)
                {
                    continue;
                }

                int sourceIndex =
                    workIndex * batchSize;
                if (sourceIndex != totalCount)
                {
                    Array.Copy(
                        buffer,
                        sourceIndex,
                        buffer,
                        totalCount,
                        count);
                }

                totalCount += count;
            }

            ActorCounts[kind] = totalCount;
        }

        int actorFlags = 0;
        for (int workIndex = 0;
             workIndex < workItemCount;
             workIndex++)
        {
            actorFlags |= partitionFlags[workIndex];
        }

        kingdomHasBoatsMask = actorFlags;
        aliveSource = null;
        aliveSourceCount = 0;
        workItemCount = 0;
        Volatile.Write(
            ref activeMask,
            enabledMask);
    }

    internal static void End()
    {
        Volatile.Write(
            ref activeMask,
            0);
        aliveSource = null;
        dyingSource = null;
        aliveSourceCount = 0;
        dyingSourceCount = 0;
        workItemCount = 0;
        preparingMask = 0;
        kingdomHasBoatsMask = 0;
    }

    internal static void Clear()
    {
        End();
        for (int kind = 0;
             kind < KindCount;
             kind++)
        {
            if (ActorBuffers[kind].Length > 0)
            {
                Array.Clear(
                    ActorBuffers[kind],
                    0,
                    ActorBuffers[kind].Length);
            }

            Managers[kind] = null;
            ActorCounts[kind] = 0;
        }
    }

    internal static bool TryApply(
        SubspeciesManager manager)
    {
        if (!IsActive(
                SubspeciesIndex,
                manager))
        {
            return false;
        }

        Actor[] dying = dyingSource;
        for (int i = 0;
             i < dyingSourceCount;
             i++)
        {
            Subspecies subspecies =
                dying[i].subspecies;
            subspecies?.preserveAlive();
        }

        Actor[] actors =
            ActorBuffers[SubspeciesIndex];
        int count =
            ActorCounts[SubspeciesIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.subspecies.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        FamilyManager manager)
    {
        if (!IsActive(
                FamilyIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[FamilyIndex];
        int count =
            ActorCounts[FamilyIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.family.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        ArmyManager manager)
    {
        if (!IsActive(
                ArmyIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[ArmyIndex];
        int count =
            ActorCounts[ArmyIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.army.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        LanguageManager manager)
    {
        if (!IsActive(
                LanguageIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[LanguageIndex];
        int count =
            ActorCounts[LanguageIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.language.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        ReligionManager manager)
    {
        if (!IsActive(
                ReligionIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[ReligionIndex];
        int count =
            ActorCounts[ReligionIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.religion.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        CityManager manager)
    {
        if (!IsActive(
                CityIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[CityIndex];
        int count =
            ActorCounts[CityIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.city.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        ClanManager manager)
    {
        if (!IsActive(
                ClanIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[ClanIndex];
        int count =
            ActorCounts[ClanIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.clan.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        KingdomManager manager)
    {
        if (!IsActive(
                KingdomIndex,
                manager))
        {
            return false;
        }

        Actor[] dying = dyingSource;
        for (int i = 0;
             i < dyingSourceCount;
             i++)
        {
            dying[i].kingdom.preserveAlive();
        }

        Actor[] actors =
            ActorBuffers[KingdomIndex];
        int count =
            ActorCounts[KingdomIndex];
        if ((kingdomHasBoatsMask &
             KingdomBit) == 0 &&
            TryGetSingleDirtyKingdom(
                manager,
                out Kingdom soleKingdom))
        {
            AddActorRange(
                soleKingdom.units,
                actors,
                count);
            return true;
        }

        AppendKingdomUnits(
            actors,
            count);

        return true;
    }

    internal static bool TryApply(
        WildKingdomsManager manager)
    {
        if (!IsActive(
                WildKingdomIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[WildKingdomIndex];
        int count =
            ActorCounts[WildKingdomIndex];
        if ((kingdomHasBoatsMask &
             WildKingdomBit) == 0 &&
            TryGetSingleDirtyKingdom(
                manager,
                out Kingdom soleKingdom))
        {
            AddActorRange(
                soleKingdom.units,
                actors,
                count);
            return true;
        }

        AppendKingdomUnits(
            actors,
            count);

        return true;
    }

    internal static bool TryApply(
        CultureManager manager)
    {
        if (!IsActive(
                CultureIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[CultureIndex];
        int count =
            ActorCounts[CultureIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.culture.listUnit(actor);
        }

        return true;
    }

    internal static bool TryApply(
        PlotManager manager)
    {
        if (!IsActive(
                PlotIndex,
                manager))
        {
            return false;
        }

        Actor[] actors =
            ActorBuffers[PlotIndex];
        int count =
            ActorCounts[PlotIndex];
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            actor.plot.listUnit(actor);
        }

        using IEnumerator<Plot> enumerator =
            manager.GetEnumerator();
        while (enumerator.MoveNext())
        {
            Plot plot = enumerator.Current;
            if (plot.isActive() &&
                plot.isDirtyUnits() &&
                plot.units.Count == 0)
            {
                manager.cancelPlot(plot);
            }
        }

        return true;
    }

    private static void Enable(
        int kind,
        object manager,
        ref int mask)
    {
        Managers[kind] = manager;
        mask |= 1 << kind;
    }

    private static bool IsActive(
        int kind,
        object manager)
    {
        int mask =
            Volatile.Read(ref activeMask);
        return (mask & (1 << kind)) != 0 &&
               ReferenceEquals(
                   Managers[kind],
                   manager);
    }

    private static void AppendKingdomUnits(
        Actor[] actors,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            Actor actor = actors[i];
            if (actor.asset.is_boat)
            {
                // Kingdom.listUnit 只有船只需要额外维护 _has_boats。
                actor.kingdom.listUnit(actor);
                continue;
            }

            actor.kingdom.units.Add(actor);
        }
    }

    private static bool TryGetSingleDirtyKingdom(
        IEnumerable<Kingdom> kingdoms,
        out Kingdom result)
    {
        result = null;
        foreach (Kingdom kingdom in kingdoms)
        {
            if (!kingdom.isDirtyUnits())
            {
                continue;
            }

            if (result != null)
            {
                result = null;
                return false;
            }

            result = kingdom;
        }

        return result != null;
    }

    private static void AddActorRange(
        List<Actor> target,
        Actor[] source,
        int count)
    {
        if (count == 0)
        {
            return;
        }

        target.AddRange(
            new ArraySegment<Actor>(
                source,
                0,
                count));
    }

    private static void ClassifyWorkItem(
        int workIndex)
    {
        int slot = workIndex * KindCount;
        for (int kind = 0;
             kind < KindCount;
             kind++)
        {
            partitionCounts[slot + kind] = 0;
        }

        int actorFlags = 0;
        int batchSize =
            PerformanceSettings.SimulationBatchSize;
        int start = workIndex * batchSize;
        int end = Math.Min(
            aliveSourceCount,
            start + batchSize);
        int enabledMask = preparingMask;
        for (int i = start; i < end; i++)
        {
            Actor actor = aliveSource[i];
            if ((enabledMask & SubspeciesBit) != 0)
            {
                Subspecies subspecies =
                    actor.subspecies;
                if (subspecies != null &&
                    subspecies.isDirtyUnits())
                {
                    ActorBuffers[SubspeciesIndex][
                        start +
                        partitionCounts[
                            slot + SubspeciesIndex]++] =
                        actor;
                }
            }

            if ((enabledMask & FamilyBit) != 0)
            {
                Family family = actor.family;
                if (family != null &&
                    family.isDirtyUnits())
                {
                    ActorBuffers[FamilyIndex][
                        start +
                        partitionCounts[
                            slot + FamilyIndex]++] =
                        actor;
                }
            }

            if ((enabledMask & ArmyBit) != 0)
            {
                Army army = actor.army;
                if (army != null &&
                    army.isDirtyUnits())
                {
                    ActorBuffers[ArmyIndex][
                        start +
                        partitionCounts[
                            slot + ArmyIndex]++] =
                        actor;
                }
            }

            if ((enabledMask & LanguageBit) != 0)
            {
                Language language =
                    actor.language;
                if (language != null &&
                    language.isDirtyUnits())
                {
                    ActorBuffers[LanguageIndex][
                        start +
                        partitionCounts[
                            slot + LanguageIndex]++] =
                        actor;
                }
            }

            if ((enabledMask & ReligionBit) != 0)
            {
                Religion religion =
                    actor.religion;
                if (religion != null &&
                    religion.isDirtyUnits())
                {
                    ActorBuffers[ReligionIndex][
                        start +
                        partitionCounts[
                            slot + ReligionIndex]++] =
                        actor;
                }
            }

            if ((enabledMask & CityBit) != 0)
            {
                City city = actor.city;
                if (city != null &&
                    city.isDirtyUnits())
                {
                    ActorBuffers[CityIndex][
                        start +
                        partitionCounts[
                            slot + CityIndex]++] =
                        actor;
                }
            }

            if ((enabledMask & ClanBit) != 0)
            {
                Clan clan = actor.clan;
                if (clan != null &&
                    clan.isDirtyUnits())
                {
                    ActorBuffers[ClanIndex][
                        start +
                        partitionCounts[
                            slot + ClanIndex]++] =
                        actor;
                }
            }

            int kingdomMask =
                enabledMask &
                (KingdomBit | WildKingdomBit);
            if (kingdomMask != 0)
            {
                Kingdom kingdom = actor.kingdom;
                if (kingdom != null &&
                    kingdom.isDirtyUnits())
                {
                    int kingdomBit =
                        kingdom.wild
                            ? WildKingdomBit
                            : KingdomBit;
                    if ((kingdomMask &
                         kingdomBit) != 0)
                    {
                        int kingdomIndex =
                            kingdom.wild
                                ? WildKingdomIndex
                                : KingdomIndex;
                        ActorBuffers[kingdomIndex][
                            start +
                            partitionCounts[
                                slot + kingdomIndex]++] =
                            actor;
                        if (actor.asset.is_boat)
                        {
                            actorFlags |= kingdomBit;
                        }
                    }
                }
            }

            if ((enabledMask & CultureBit) != 0)
            {
                Culture culture =
                    actor.culture;
                if (culture != null &&
                    culture.isDirtyUnits())
                {
                    ActorBuffers[CultureIndex][
                        start +
                        partitionCounts[
                            slot + CultureIndex]++] =
                        actor;
                }
            }

            if ((enabledMask & PlotBit) != 0)
            {
                Plot plot = actor.plot;
                if (plot != null &&
                    plot.isDirtyUnits())
                {
                    ActorBuffers[PlotIndex][
                        start +
                        partitionCounts[
                            slot + PlotIndex]++] =
                        actor;
                }
            }
        }

        partitionFlags[workIndex] = actorFlags;
    }

    private static void EnsurePartitionCapacity(
        int required)
    {
        if (partitionCounts.Length >= required)
        {
            return;
        }

        partitionCounts =
            new int[required];
    }

    private static void EnsurePartitionFlagCapacity(
        int required)
    {
        if (partitionFlags.Length >= required)
        {
            return;
        }

        partitionFlags =
            new int[required];
    }

    private static void EnsureActorBufferCapacity(
        int kind,
        int required)
    {
        if (ActorBuffers[kind].Length >= required)
        {
            return;
        }

        ActorBuffers[kind] =
            new Actor[
                Math.Max(
                    PerformanceSettings.SimulationBatchSize,
                    required)];
    }
}

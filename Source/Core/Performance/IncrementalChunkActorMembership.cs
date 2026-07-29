using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Cultiway.Core.Performance;

/// <summary>
/// 增量维护 ChunkObjectContainer 的角色成员。
/// 所有列表始终按 World.units rank 排列，与原版完整重建顺序一致。
/// </summary>
internal static class IncrementalChunkActorMembership
{
    private static readonly AccessTools.FieldRef<
            ChunkObjectContainer,
            HashSet<long>>
        KingdomSetField =
            AccessTools.FieldRefAccess<
                ChunkObjectContainer,
                HashSet<long>>(
                "_hash_kingdoms");

    private static readonly AccessTools.FieldRef<
            ChunkObjectContainer,
            Dictionary<long, List<Actor>>>
        UnitsByKingdomField =
            AccessTools.FieldRefAccess<
                ChunkObjectContainer,
                Dictionary<long, List<Actor>>>(
                "_dict_units");

    private static readonly AccessTools.FieldRef<
            ChunkObjectContainer,
            Dictionary<long, List<Building>>>
        BuildingsByKingdomField =
            AccessTools.FieldRefAccess<
                ChunkObjectContainer,
                Dictionary<long, List<Building>>>(
                "_dict_buildings");

    private static readonly AccessTools.FieldRef<
            ChunkObjectContainer,
            int>
        TotalUnitsField =
            AccessTools.FieldRefAccess<
                ChunkObjectContainer,
                int>("_total_units");

    private static readonly Dictionary<long, int>
        ValidationCursors = new();

    internal static void Remove(
        ChunkObjectContainer container,
        Actor actor,
        long kingdomId)
    {
        if (!container.units_all.Remove(actor))
        {
            throw new InvalidOperationException(
                "chunk units_all 缺少待移除角色");
        }

        Dictionary<long, List<Actor>>
            unitsByKingdom =
                UnitsByKingdomField(container);
        if (!unitsByKingdom.TryGetValue(
                kingdomId,
                out List<Actor> kingdomUnits) ||
            !kingdomUnits.Remove(actor))
        {
            throw new InvalidOperationException(
                "chunk kingdom 角色表缺少待移除角色");
        }

        ref int totalUnits =
            ref TotalUnitsField(container);
        totalUnits--;
        if (totalUnits < 0)
        {
            throw new InvalidOperationException(
                "chunk 角色总数出现负值");
        }
    }

    internal static void Add(
        ChunkObjectContainer container,
        Actor actor,
        long kingdomId,
        int actorRank,
        Dictionary<Actor, int> actorRanks)
    {
        List<Actor> kingdomUnits =
            EnsureKingdom(
                container,
                kingdomId);
        InsertActorAtRank(
            container.units_all,
            actor,
            actorRank,
            actorRanks);
        InsertActorAtRank(
            kingdomUnits,
            actor,
            actorRank,
            actorRanks);
        ref int totalUnits =
            ref TotalUnitsField(container);
        totalUnits++;
    }

    internal static void ChangeKingdom(
        ChunkObjectContainer container,
        Actor actor,
        long oldKingdomId,
        long newKingdomId,
        int actorRank,
        Dictionary<Actor, int> actorRanks)
    {
        Dictionary<long, List<Actor>>
            unitsByKingdom =
                UnitsByKingdomField(container);
        if (!unitsByKingdom.TryGetValue(
                oldKingdomId,
                out List<Actor> oldUnits) ||
            !oldUnits.Remove(actor))
        {
            throw new InvalidOperationException(
                "chunk 旧 kingdom 角色表缺少待迁移角色");
        }

        List<Actor> newUnits =
            EnsureKingdom(
                container,
                newKingdomId);
        InsertActorAtRank(
            newUnits,
            actor,
            actorRank,
            actorRanks);
    }

    internal static void Validate(
        ChunkObjectContainer container,
        List<Actor> expected)
    {
        if (TotalUnitsField(container) !=
            expected.Count)
        {
            throw new InvalidOperationException(
                "chunk 角色总数与增量基线不一致");
        }

        List<Actor> unitsAll =
            container.units_all;
        if (unitsAll.Count != expected.Count)
        {
            throw new InvalidOperationException(
                "chunk units_all 数量与增量基线不一致");
        }

        Dictionary<long, List<Actor>>
            unitsByKingdom =
                UnitsByKingdomField(container);
        ValidationCursors.Clear();
        for (int i = 0; i < expected.Count; i++)
        {
            Actor actor = expected[i];
            if (!ReferenceEquals(
                    unitsAll[i],
                    actor))
            {
                throw new InvalidOperationException(
                    "chunk units_all 顺序与 World.units 不一致");
            }

            long kingdomId = actor.kingdom.id;
            if (!unitsByKingdom.TryGetValue(
                    kingdomId,
                    out List<Actor> kingdomUnits))
            {
                throw new InvalidOperationException(
                    "chunk 缺少角色所属 kingdom 成员表");
            }

            ValidationCursors.TryGetValue(
                kingdomId,
                out int cursor);
            if (cursor >= kingdomUnits.Count ||
                !ReferenceEquals(
                    kingdomUnits[cursor],
                    actor))
            {
                throw new InvalidOperationException(
                    "chunk kingdom 角色顺序与 World.units 不一致");
            }

            ValidationCursors[kingdomId] =
                cursor + 1;
        }

        foreach (KeyValuePair<
                     long,
                     List<Actor>> pair in
                 unitsByKingdom)
        {
            ValidationCursors.TryGetValue(
                pair.Key,
                out int expectedCount);
            if (pair.Value.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    "chunk kingdom 角色数量与增量基线不一致");
            }
        }
    }

    private static List<Actor> EnsureKingdom(
        ChunkObjectContainer container,
        long kingdomId)
    {
        HashSet<long> kingdomSet =
            KingdomSetField(container);
        Dictionary<long, List<Actor>>
            unitsByKingdom =
                UnitsByKingdomField(container);
        if (kingdomSet.Add(kingdomId))
        {
            if (!unitsByKingdom.TryGetValue(
                    kingdomId,
                    out List<Actor> units))
            {
                units = new List<Actor>();
                unitsByKingdom.Add(
                    kingdomId,
                    units);
                BuildingsByKingdomField(
                        container)
                    .Add(
                        kingdomId,
                        new List<Building>());
            }

            container.kingdoms.Add(kingdomId);
            return units;
        }

        return unitsByKingdom[kingdomId];
    }

    private static void InsertActorAtRank(
        List<Actor> target,
        Actor actor,
        int actorRank,
        Dictionary<Actor, int> actorRanks)
    {
        int low = 0;
        int high = target.Count;
        while (low < high)
        {
            int middle =
                low + (high - low) / 2;
            if (actorRanks[target[middle]] <
                actorRank)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        target.Insert(low, actor);
    }
}

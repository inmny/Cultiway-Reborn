using Cultiway.Core;
using Cultiway.Utils.Extension;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Content;

public static class SkavenEvolution
{
    private const float EvolutionChancePerKill = 0.13f;
    public const int GroupSize = 13;
    public const int GroupCount = 13;
    private const string GroupDataKey = "cultiway.skaven.group";
    private const string FormationSlotDataKey = "cultiway.skaven.formation_slot";
    private const string LeaderDataKeyPrefix = "cultiway.skaven.leader.";

    private static readonly ActorAsset[] Levels =
    {
        Actors.Skaven_LV1, Actors.Skaven_LV2, Actors.Skaven_LV3, Actors.Skaven_LV4, Actors.Skaven_LV5,
        Actors.Skaven_LV6, Actors.Skaven_LV7, Actors.Skaven_LV8, Actors.Skaven_LV9, Actors.Skaven_LV10,
        Actors.Skaven_LV11, Actors.Skaven_LV12, Actors.Skaven_LV13
    };

    public static void Init()
    {
        ActorExtend.RegisterActionOnKill(OnKill);
    }

    public static bool TryGetLeader(Actor actor, out Actor leader)
    {
        leader = null;
        var source = World.world.buildings.get(actor.GetSourceSpawnerId());
        if (source.isRekt() || source.asset != Buildings.SkavenBlight) return false;

        var group = GetOrAssignGroup(actor);
        var leaderKey = LeaderDataKeyPrefix + group;
        source.data.get(leaderKey, out long leaderId, -1L);
        leader = World.world.units.get(leaderId);
        if (IsValidGroupMember(leader, source.id, group)) return true;

        leader = ElectLeader(source.id, group);
        source.data.set(leaderKey, leader?.data.id ?? -1L);
        return leader != null;
    }

    public static bool IsGroupLeader(Actor actor)
    {
        return TryGetLeader(actor, out var leader) && leader == actor;
    }

    public static bool IsSkaven(Actor actor)
    {
        return actor != null && GetLevel(actor.asset) >= 0;
    }

    public static int GetOrAssignFormationSlot(Actor actor)
    {
        actor.data.get(FormationSlotDataKey, out int slot, -1);
        if (slot >= 0 && slot < GroupSize) return slot;

        var group = GetOrAssignGroup(actor);
        var usedSlots = new bool[GroupSize];
        ForEachSkaven(candidate =>
        {
            if (candidate == actor || candidate.GetSourceSpawnerId() != actor.GetSourceSpawnerId()) return;
            candidate.data.get(GroupDataKey, out int candidateGroup, -1);
            if (candidateGroup != group) return;
            candidate.data.get(FormationSlotDataKey, out int candidateSlot, -1);
            if (candidateSlot >= 0 && candidateSlot < GroupSize) usedSlots[candidateSlot] = true;
        });

        slot = 0;
        while (slot < GroupSize - 1 && usedSlots[slot]) slot++;
        actor.data.set(FormationSlotDataKey, slot);
        return slot;
    }

    private static int GetOrAssignGroup(Actor actor)
    {
        actor.data.get(GroupDataKey, out int group, -1);
        if (group >= 0 && group < GroupCount) return group;

        var counts = new int[GroupCount];
        var unassigned = new List<Actor>();
        ForEachSkaven(candidate =>
        {
            if (candidate.GetSourceSpawnerId() != actor.GetSourceSpawnerId()) return;
            candidate.data.get(GroupDataKey, out int candidateGroup, -1);
            if (candidateGroup >= 0 && candidateGroup < GroupCount)
            {
                counts[candidateGroup]++;
            }
            else
            {
                unassigned.Add(candidate);
            }
        });

        unassigned.Sort((left, right) => left.data.id.CompareTo(right.data.id));
        group = 0;
        for (var i = 0; i < unassigned.Count; i++)
        {
            while (group < GroupCount - 1 && counts[group] >= GroupSize) group++;
            unassigned[i].data.set(GroupDataKey, group);
            counts[group]++;
        }

        actor.data.get(GroupDataKey, out group, 0);
        return group;
    }

    private static Actor ElectLeader(long sourceId, int group)
    {
        Actor leader = null;
        ForEachSkaven(candidate =>
        {
            if (!IsValidGroupMember(candidate, sourceId, group)) return;
            if (leader == null || GetLevel(candidate.asset) > GetLevel(leader.asset) ||
                GetLevel(candidate.asset) == GetLevel(leader.asset) && candidate.data.id < leader.data.id)
            {
                leader = candidate;
            }
        });
        return leader;
    }

    private static bool IsValidGroupMember(Actor actor, long sourceId, int group)
    {
        if (actor.isRekt() || actor.GetSourceSpawnerId() != sourceId || GetLevel(actor.asset) < 0) return false;
        actor.data.get(GroupDataKey, out int actorGroup, -1);
        return actorGroup == group;
    }

    private static int GetLevel(ActorAsset asset)
    {
        for (var i = 0; i < Levels.Length; i++)
        {
            if (Levels[i] == asset) return i;
        }
        return -1;
    }

    public static void ForEachSkaven(System.Action<Actor> action)
    {
        for (var i = 0; i < Levels.Length; i++)
        {
            var units = Levels[i].units;
            foreach (var unit in units) action(unit);
        }
    }

    private static void OnKill(ActorExtend killer, Actor _, Kingdom __)
    {
        var actor = killer.Base;
        if (!actor.hasTrait(ActorTraits.SkavenEvolution.id) || !Randy.randomChance(EvolutionChancePerKill)) return;

        for (var i = 0; i < Levels.Length - 1; i++)
        {
            if (actor.asset != Levels[i]) continue;
            ActorTransformationService.TransformInPlace(actor, Levels[i + 1]);
            return;
        }
    }
}

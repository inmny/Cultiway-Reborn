using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Coordination;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 按巢穴增量维护鼠群成员、队长、固定槽位、巡逻状态与协调行动。
/// 除世界首次重建外，运行期不会扫描全部鼠人单位。
/// </summary>
public static class SkavenPackService
{
    /// <summary>鼠群队长席位标识。</summary>
    public const string LeaderRoleId = "leader";

    /// <summary>鼠群普通成员席位标识。</summary>
    public const string MemberRoleId = "member";

    /// <summary>每队唯一的奴隶鼠席位标识。</summary>
    public const string SlaveRoleId = "slave";

    private const string ProviderId = "cultiway.skaven_pack";
    private const string GroupDataKey = "cultiway.skaven.group";
    private const string FormationSlotDataKey = "cultiway.skaven.formation_slot";
    private const string SlaveDataKey = "cultiway.skaven.slave";
    private const double UnresolvedCombatDuration = 8d;
    private const double MobilizationDuration = 45d;
    private const double NestHeartbeat = 0.5d;
    private const int NestUpdateBudget = 8;
    private const int OrphanAdoptionBudget = 8;

    private static readonly Vector2Int[] FormationOffsets =
    [
        new(-1, -1), new(0, -1), new(1, -1),
        new(-1, 0),               new(1, 0),
        new(-1, 1),  new(0, 1),   new(1, 1),
        new(-2, 0),  new(2, 0),   new(0, -2), new(0, 2), new(-2, -2),
        new(2, 2)
    ];

    private static readonly Dictionary<long, NestRuntime> Nests = new();
    private static readonly Dictionary<long, Membership> Memberships = new();
    private static readonly Queue<long> DirtyNests = new();
    private static readonly HashSet<long> DirtyNestIds = new();
    private static readonly Queue<long> HeartbeatNests = new();
    private static readonly Queue<long> OrphanActors = new();
    private static readonly HashSet<long> OrphanActorIds = new();

    private static bool initialized;
    private static bool rebuildPending = true;

    /// <summary>绑定增量事件、群组提供者与主线程更新系统。</summary>
    public static void Init()
    {
        if (initialized) return;
        initialized = true;
        AdvancedUnitSpawner.RegisterActionOnUnitSpawned(OnUnitSpawned);
        ActorExtend.RegisterActionOnDeath(OnActorDied);
        CoordinatedActivityService.RegisterGroupProvider(new GroupProvider());
        CombatWorldService.RegisterGroupProvider(SkavenCombatGroupProvider.Instance, 100);
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    /// <summary>确保一个鼠人已进入其来源巢穴的增量编组。</summary>
    private static bool EnsureRegistered(Actor actor)
    {
        if (!SkavenEvolution.IsSkaven(actor)) return false;
        long actorId = actor.getID();
        if (Memberships.TryGetValue(actorId, out Membership membership) &&
            ResolveNest(membership.NestId) != null &&
            TryGetGroup(membership.NestId, membership.GroupIndex, out GroupRuntime current) &&
            current.Members.ContainsKey(actorId))
            return true;

        Building nest = ResolveNest(actor.GetSourceSpawnerId());
        if (nest == null) return false;
        actor.data.get(SlaveDataKey, out int isSlave, 0);
        if (isSlave == 1)
        {
            NestRuntime runtime = GetOrCreateNest(nest);
            actor.data.get(GroupDataKey, out int preferredGroup, -1);
            GroupRuntime slaveGroup = preferredGroup >= 0 && preferredGroup < runtime.Groups.Length &&
                                      runtime.Groups[preferredGroup].SlaveId == 0 &&
                                      runtime.Groups[preferredGroup].LeaderId > 0
                ? runtime.Groups[preferredGroup]
                : SelectSlaveGroup(runtime);
            if (slaveGroup == null) return false;
            RegisterSlave(nest, slaveGroup, actor);
            return Memberships.ContainsKey(actorId);
        }
        RegisterActor(nest, actor);
        return Memberships.ContainsKey(actorId);
    }

    /// <summary>进化保持编组不变，但会立即重新评选当前小队队长。</summary>
    public static void NotifyEvolution(Actor actor)
    {
        if (!EnsureRegistered(actor) ||
            !Memberships.TryGetValue(actor.getID(), out Membership membership) ||
            !TryGetGroup(membership.NestId, membership.GroupIndex, out GroupRuntime group))
            return;
        ElectLeader(group);
        MarkNestDirty(membership.NestId);
    }

    /// <summary>报告鼠群受到真实敌对攻击，并在持续八秒未解决时动员整个巢穴。</summary>
    public static void ReportThreat(Actor victim, BaseSimObject attacker, float damage)
    {
        if (damage <= 0f || !SkavenEvolution.IsHostile(attacker, victim?.kingdom) ||
            !EnsureRegistered(victim) ||
            !Memberships.TryGetValue(victim.getID(), out Membership membership) ||
            !Nests.TryGetValue(membership.NestId, out NestRuntime nest))
            return;

        GroupRuntime group = nest.Groups[membership.GroupIndex];
        double now = CurrentTime;
        nest.LatestThreatId = attacker.getID();
        nest.LatestThreatTileId = attacker.current_tile?.tile_id ?? victim.current_tile.tile_id;
        if (group.CombatStartedAt <= 0d) group.CombatStartedAt = now;
        if (now - group.CombatStartedAt >= UnresolvedCombatDuration)
            AlertNest(ResolveNest(nest.NestId), attacker);
        MarkNestDirty(nest.NestId);
    }

    /// <summary>立即把一个鼠巢动员至未来 45 秒，并记录本次真实威胁位置。</summary>
    public static void AlertNest(Building nest, BaseSimObject threat = null)
    {
        if (ResolveNest(nest?.id ?? -1) == null) return;
        NestRuntime runtime = GetOrCreateNest(nest);
        runtime.LatestThreatId = threat?.getID() ?? 0;
        runtime.LatestThreatTileId = threat?.current_tile?.tile_id ?? nest.current_tile.tile_id;
        runtime.MobilizedUntil = Math.Max(runtime.MobilizedUntil, CurrentTime + MobilizationDuration);
        MarkNestDirty(runtime.NestId);
    }

    /// <summary>由鼠群常驻工作推进当前协调行动。</summary>
    public static CoordinationParticipantResult TickActor(Actor actor)
    {
        if (!EnsureRegistered(actor)) return CoordinationParticipantResult.Leave;
        return CoordinatedActivityService.TickParticipant(actor);
    }

    /// <summary>供战术战斗适配器读取角色的稳定鼠群键。</summary>
    private static bool TryGetGroupKey(Actor actor, out CoordinationGroupKey key)
    {
        if (EnsureRegistered(actor) && Memberships.TryGetValue(actor.getID(), out Membership membership))
        {
            key = new CoordinationGroupKey(ProviderId, membership.NestId, membership.GroupIndex);
            return true;
        }
        key = default;
        return false;
    }

    /// <summary>把指定鼠群当前成员复制到调用方列表。</summary>
    private static void CollectGroupMembers(in CoordinationGroupKey key, IList<Actor> output)
    {
        if (!TryGetGroup(key.OwnerId, key.PartitionId, out GroupRuntime group)) return;
        foreach (long actorId in group.Members.Keys)
        {
            Actor actor = ResolveActor(actorId);
            if (!actor.isRekt()) output.Add(actor);
        }
    }

    /// <summary>解析指定鼠群队长。</summary>
    private static Actor ResolveGroupLeader(in CoordinationGroupKey key)
    {
        return TryGetGroup(key.OwnerId, key.PartitionId, out GroupRuntime group)
            ? ResolveActor(group.LeaderId)
            : null;
    }

    /// <summary>遍历增量索引中的当前小队队长，供可见性渲染使用。</summary>
    public static void ForEachLeader(Action<Actor> action)
    {
        foreach (NestRuntime nest in Nests.Values)
        {
            for (var i = 0; i < nest.Groups.Length; i++)
            {
                Actor leader = ResolveActor(nest.Groups[i].LeaderId);
                if (!leader.isRekt()) action(leader);
            }
        }
    }

    /// <summary>执行首次重建，然后有界推进脏巢穴与常规心跳。</summary>
    private static void Update()
    {
        if (rebuildPending) RebuildFromWorld();

        var processed = 0;
        int dirtyCount = DirtyNests.Count;
        while (processed < NestUpdateBudget && dirtyCount-- > 0)
        {
            long nestId = DirtyNests.Dequeue();
            DirtyNestIds.Remove(nestId);
            if (Nests.TryGetValue(nestId, out NestRuntime nest)) ProcessNest(nest, force: true);
            processed++;
        }

        int heartbeatCount = HeartbeatNests.Count;
        while (processed < NestUpdateBudget && heartbeatCount-- > 0)
        {
            long nestId = HeartbeatNests.Dequeue();
            if (!Nests.TryGetValue(nestId, out NestRuntime nest)) continue;
            HeartbeatNests.Enqueue(nestId);
            ProcessNest(nest, force: false);
            processed++;
        }
        ProcessOrphanAdoptions();
    }

    /// <summary>世界进入可用状态后仅执行一次全量鼠人编组重建。</summary>
    private static void RebuildFromWorld()
    {
        if (World.world?.units == null || World.world.buildings == null) return;
        rebuildPending = false;
        SkavenEvolution.ForEachSkaven(actor =>
        {
            if (actor.isRekt()) return;
            if (!EnsureRegistered(actor)) EnqueueOrphan(actor);
        });
    }

    /// <summary>清理失效成员、检查持续战斗并保证每组只有一个匹配的协调行动。</summary>
    private static void ProcessNest(NestRuntime nest, bool force)
    {
        double now = CurrentTime;
        if (!force && now < nest.NextUpdateAt) return;
        nest.NextUpdateAt = now + NestHeartbeat;
        if (ResolveNest(nest.NestId) == null)
        {
            RemoveNest(nest);
            return;
        }

        bool mobilized = nest.MobilizedUntil > now;
        for (var i = 0; i < nest.Groups.Length; i++)
        {
            GroupRuntime group = nest.Groups[i];
            RemoveInvalidMembers(group);
            if (group.Members.Count == 0)
            {
                CancelGroupActivity(group);
                continue;
            }
            ElectLeader(group);
            bool inCombat = IsGroupInCombat(group);
            if (inCombat)
            {
                if (group.CombatStartedAt <= 0d) group.CombatStartedAt = now;
                if (mobilized)
                    nest.MobilizedUntil = Math.Max(nest.MobilizedUntil, now + MobilizationDuration);
                else if (now - group.CombatStartedAt >= UnresolvedCombatDuration)
                {
                    nest.MobilizedUntil = now + MobilizationDuration;
                    mobilized = true;
                }
            }
            else
            {
                group.CombatStartedAt = 0d;
            }

            SkavenActivityMode desired = mobilized
                ? SkavenActivityMode.Defend
                : IsPatrolGroup(nest, i)
                    ? SkavenActivityMode.Patrol
                    : SkavenActivityMode.Guard;
            EnsureGroupActivity(nest, group, desired);
        }
    }

    /// <summary>创建或切换小队的驻守、巡逻或动员行动。</summary>
    private static void EnsureGroupActivity(
        NestRuntime nest,
        GroupRuntime group,
        SkavenActivityMode desired)
    {
        CoordinatedActivityDefinitionAsset definition = ResolveDefinition(desired);
        var key = new CoordinationGroupKey(ProviderId, nest.NestId, group.Index);
        bool currentExists = group.ActivityId > 0 &&
                             CoordinatedActivityService.TryGetActivityId(
                                 ResolveDefinition(group.ActivityMode).id,
                                 key,
                                 out long currentId) &&
                             currentId == group.ActivityId;
        if (currentExists && group.ActivityMode == desired) return;
        CancelGroupActivity(group);

        Actor leader = ResolveActor(group.LeaderId);
        if (leader.isRekt()) return;
        var session = new SkavenCoordinationSession(nest.NestId, group.Index, desired);
        bool started = CoordinatedActivityService.TryStart(
            definition,
            key,
            session,
            [new CoordinationInitialParticipant(leader, LeaderRoleId)],
            out long activityId);
        if (!started && activityId <= 0) return;
        group.ActivityId = activityId;
        group.ActivityMode = desired;
    }

    /// <summary>取消小队当前行动并清除运行期引用。</summary>
    private static void CancelGroupActivity(GroupRuntime group)
    {
        if (group.ActivityId > 0) CoordinatedActivityService.Cancel(group.ActivityId);
        group.ActivityId = 0;
        group.ActivityMode = SkavenActivityMode.None;
    }

    /// <summary>处理高级生成器已经完成来源绑定的新鼠人。</summary>
    private static void OnUnitSpawned(Building source, Actor actor)
    {
        if (source?.asset == Buildings.SkavenBlight && SkavenEvolution.IsSkaven(actor))
            RegisterActor(source, actor);
    }

    /// <summary>死亡事件只从所属小队移除一个成员，不扫描其他鼠人。</summary>
    private static void OnActorDied(ActorExtend actorExtend)
    {
        Actor actor = actorExtend?.Base;
        if (actor != null) RemoveActor(actor.getID());
    }

    /// <summary>把角色放入最空且未满的小队，并分配其中最小空闲槽。</summary>
    private static void RegisterActor(Building nestBuilding, Actor actor)
    {
        if (actor.isRekt() || nestBuilding == null) return;
        long actorId = actor.getID();
        if (Memberships.TryGetValue(actorId, out Membership existing))
        {
            if (existing.NestId == nestBuilding.id &&
                TryGetGroup(existing.NestId, existing.GroupIndex, out GroupRuntime existingGroup) &&
                existingGroup.Members.ContainsKey(actorId))
                return;
            RemoveActor(actorId);
        }

        NestRuntime nest = GetOrCreateNest(nestBuilding);
        actor.data.get(GroupDataKey, out int preferredGroup, -1);
        GroupRuntime group = SelectGroup(nest, preferredGroup);
        if (group == null) return;
        actor.data.get(FormationSlotDataKey, out int preferredSlot, -1);
        int slot = SelectSlot(group, preferredSlot);
        var membership = new Membership(nest.NestId, group.Index, slot, false);
        group.Members.Add(actorId, membership);
        group.UsedSlots[slot] = true;
        Memberships.Add(actorId, membership);
        actor.data.set(GroupDataKey, group.Index);
        actor.data.set(FormationSlotDataKey, slot);
        actor.data.set(SlaveDataKey, 0);
        ElectLeader(group);
        MarkNestDirty(nest.NestId);
    }

    /// <summary>把失去来源巢穴的鼠人加入有界收留队列。</summary>
    private static void EnqueueOrphan(Actor actor)
    {
        if (actor.isRekt() || !SkavenEvolution.IsSkaven(actor) ||
            Memberships.ContainsKey(actor.getID()) || !OrphanActorIds.Add(actor.getID()))
            return;
        OrphanActors.Enqueue(actor.getID());
    }

    /// <summary>按预算为失巢鼠人寻找同岛最近的空闲奴隶鼠位。</summary>
    private static void ProcessOrphanAdoptions()
    {
        int count = Math.Min(OrphanAdoptionBudget, OrphanActors.Count);
        for (var i = 0; i < count; i++)
        {
            long actorId = OrphanActors.Dequeue();
            OrphanActorIds.Remove(actorId);
            Actor actor = ResolveActor(actorId);
            if (actor.isRekt() || Memberships.ContainsKey(actorId)) continue;

            Building nest = FindAdoptionNest(actor, out GroupRuntime group);
            if (nest != null && group != null)
            {
                RegisterSlave(nest, group, actor);
            }
            else
            {
                EnqueueOrphan(actor);
            }
        }
    }

    /// <summary>查找同岛、已有正式成员且仍有奴隶鼠位的最近疫巢。</summary>
    private static Building FindAdoptionNest(Actor actor, out GroupRuntime selectedGroup)
    {
        selectedGroup = null;
        Building selectedNest = null;
        float bestDistance = float.MaxValue;
        foreach (NestRuntime runtime in Nests.Values)
        {
            Building nest = ResolveNest(runtime.NestId);
            if (nest == null || actor.current_tile == null ||
                !actor.current_tile.isSameIsland(nest.current_tile))
                continue;

            GroupRuntime group = SelectSlaveGroup(runtime);
            if (group == null) continue;
            float distance = Toolbox.SquaredDistVec2Float(actor.current_position, nest.current_position);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            selectedNest = nest;
            selectedGroup = group;
        }
        return selectedNest;
    }

    /// <summary>按小队编号选择第一个有正式队长且奴隶位空闲的小队。</summary>
    private static GroupRuntime SelectSlaveGroup(NestRuntime nest)
    {
        for (var i = 0; i < nest.Groups.Length; i++)
        {
            GroupRuntime group = nest.Groups[i];
            if (group.SlaveId == 0 && group.LeaderId > 0) return group;
        }
        return null;
    }

    /// <summary>将失巢鼠人绑定为指定小队的奴隶鼠，不占用正式成员槽。</summary>
    private static void RegisterSlave(Building nest, GroupRuntime group, Actor actor)
    {
        long actorId = actor.getID();
        if (group.SlaveId != 0 || Memberships.ContainsKey(actorId)) return;
        var membership = new Membership(nest.id, group.Index, SkavenEvolution.GroupSize, true);
        group.Members.Add(actorId, membership);
        group.SlaveId = actorId;
        Memberships.Add(actorId, membership);
        actor.SetSourceSpawnerId(nest.id);
        actor.SetSourceSpawnerAssetId(nest.asset.id);
        actor.data.set(GroupDataKey, group.Index);
        actor.data.set(FormationSlotDataKey, SkavenEvolution.GroupSize);
        actor.data.set(SlaveDataKey, 1);
        MarkNestDirty(nest.id);
    }

    /// <summary>从增量索引移除角色，并在必要时重新选举队长。</summary>
    private static void RemoveActor(long actorId)
    {
        if (!Memberships.TryGetValue(actorId, out Membership membership)) return;
        Memberships.Remove(actorId);
        if (!TryGetGroup(membership.NestId, membership.GroupIndex, out GroupRuntime group)) return;
        group.Members.Remove(actorId);
        if (membership.IsSlave)
            group.SlaveId = 0;
        else if (membership.Slot >= 0 && membership.Slot < group.UsedSlots.Length)
            group.UsedSlots[membership.Slot] = false;
        if (group.LeaderId == actorId) ElectLeader(group);
        MarkNestDirty(membership.NestId);
    }

    /// <summary>移除小队索引中已经死亡或来源改变的成员。</summary>
    private static void RemoveInvalidMembers(GroupRuntime group)
    {
        using var invalid = new ListPool<long>();
        foreach (KeyValuePair<long, Membership> pair in group.Members)
        {
            Actor actor = ResolveActor(pair.Key);
            if (actor.isRekt() ||
                !SkavenEvolution.IsSkaven(actor) ||
                actor.GetSourceSpawnerId() != group.NestId)
                invalid.Add(pair.Key);
        }
        for (var i = 0; i < invalid.Count; i++) RemoveActor(invalid[i]);
    }

    /// <summary>优先保留已有数据，否则按编号依次填满每个十三人小队。</summary>
    private static GroupRuntime SelectGroup(NestRuntime nest, int preferredGroup)
    {
        if (preferredGroup >= 0 && preferredGroup < nest.Groups.Length &&
            HasFreeMemberSlot(nest.Groups[preferredGroup]))
            return nest.Groups[preferredGroup];
        for (var i = 0; i < nest.Groups.Length; i++)
        {
            GroupRuntime candidate = nest.Groups[i];
            if (HasFreeMemberSlot(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>正式成员容量只由十三个普通阵位决定，不计奴隶鼠位。</summary>
    private static bool HasFreeMemberSlot(GroupRuntime group)
    {
        for (var i = 0; i < group.UsedSlots.Length; i++)
        {
            if (!group.UsedSlots[i]) return true;
        }
        return false;
    }

    /// <summary>选择优先保留已有数据、否则编号最小的空闲阵位。</summary>
    private static int SelectSlot(GroupRuntime group, int preferredSlot)
    {
        if (preferredSlot >= 0 && preferredSlot < group.UsedSlots.Length &&
            !group.UsedSlots[preferredSlot])
            return preferredSlot;
        for (var i = 0; i < group.UsedSlots.Length; i++)
        {
            if (!group.UsedSlots[i]) return i;
        }
        return group.UsedSlots.Length - 1;
    }

    /// <summary>按进化等级优先、稳定 ID 次序选举队长。</summary>
    private static void ElectLeader(GroupRuntime group)
    {
        Actor selected = null;
        foreach (long actorId in group.Members.Keys)
        {
            if (Memberships.TryGetValue(actorId, out Membership membership) && membership.IsSlave) continue;
            Actor candidate = ResolveActor(actorId);
            if (candidate.isRekt()) continue;
            if (selected == null ||
                SkavenEvolution.GetLevel(candidate.asset) > SkavenEvolution.GetLevel(selected.asset) ||
                SkavenEvolution.GetLevel(candidate.asset) == SkavenEvolution.GetLevel(selected.asset) &&
                candidate.getID() < selected.getID())
                selected = candidate;
        }
        group.LeaderId = selected?.getID() ?? 0;
    }

    /// <summary>判断小队是否仍有成员处于真实战斗任务或持有攻击目标。</summary>
    private static bool IsGroupInCombat(GroupRuntime group)
    {
        foreach (long actorId in group.Members.Keys)
        {
            Actor actor = ResolveActor(actorId);
            if (!actor.isRekt() &&
                (actor.has_attack_target && !actor.attack_target.isRekt() ||
                 actor.ai.task?.in_combat == true))
                return true;
        }
        return false;
    }

    /// <summary>判断小队编号是否落在本巢本轮的一至三个巡逻组内。</summary>
    private static bool IsPatrolGroup(NestRuntime nest, int groupIndex)
    {
        int distance = (groupIndex - nest.FirstPatrolGroup + SkavenEvolution.GroupCount) %
                       SkavenEvolution.GroupCount;
        return distance < nest.PatrolGroupCount;
    }

    /// <summary>为巡逻队长选择同岛且位于巢穴半径 16 内的新目标。</summary>
    private static WorldTile SelectPatrolTarget(WorldTile nestTile)
    {
        for (var i = 0; i < 12; i++)
        {
            int x = nestTile.x + Randy.randomInt(-16, 17);
            int y = nestTile.y + Randy.randomInt(-16, 17);
            WorldTile target = World.world.GetTile(x, y);
            if (target != null && target.isSameIsland(nestTile)) return target;
        }
        return nestTile;
    }

    /// <summary>解析小队行动的静态定义。</summary>
    private static CoordinatedActivityDefinitionAsset ResolveDefinition(SkavenActivityMode mode)
    {
        return mode switch
        {
            SkavenActivityMode.Patrol => CoordinationActivities.SkavenPatrol,
            SkavenActivityMode.Defend => CoordinationActivities.SkavenDefend,
            _ => CoordinationActivities.SkavenGuard
        };
    }

    /// <summary>取得或创建一个巢穴运行时。</summary>
    private static NestRuntime GetOrCreateNest(Building nest)
    {
        if (Nests.TryGetValue(nest.id, out NestRuntime runtime))
            return runtime;
        runtime = new NestRuntime(nest);
        Nests.Add(nest.id, runtime);
        HeartbeatNests.Enqueue(nest.id);
        return runtime;
    }

    /// <summary>解析有效鼠巢。</summary>
    private static Building ResolveNest(long nestId)
    {
        Building nest = World.world?.buildings?.get(nestId);
        return nest != null && !nest.isRekt() && nest.asset == Buildings.SkavenBlight ? nest : null;
    }

    /// <summary>解析巢穴中的指定小队。</summary>
    private static bool TryGetGroup(long nestId, int groupIndex, out GroupRuntime group)
    {
        group = null;
        if (!Nests.TryGetValue(nestId, out NestRuntime nest) ||
            groupIndex < 0 || groupIndex >= nest.Groups.Length)
            return false;
        group = nest.Groups[groupIndex];
        return true;
    }

    /// <summary>把巢穴加入高优先级更新队列。</summary>
    private static void MarkNestDirty(long nestId)
    {
        if (!Nests.ContainsKey(nestId) || !DirtyNestIds.Add(nestId)) return;
        DirtyNests.Enqueue(nestId);
    }

    /// <summary>移除被摧毁巢穴及其全部活动和成员索引。</summary>
    private static void RemoveNest(NestRuntime nest)
    {
        for (var i = 0; i < nest.Groups.Length; i++)
        {
            GroupRuntime group = nest.Groups[i];
            CancelGroupActivity(group);
            foreach (long actorId in group.Members.Keys)
            {
                Memberships.Remove(actorId);
                EnqueueOrphan(ResolveActor(actorId));
            }
        }
        Nests.Remove(nest.NestId);
        DirtyNestIds.Remove(nest.NestId);
    }

    /// <summary>解析仍存活的角色。</summary>
    private static Actor ResolveActor(long actorId)
    {
        return actorId > 0 ? World.world?.units?.get(actorId) : null;
    }

    /// <summary>清除世界运行期索引，并要求新世界首次更新时重新构建。</summary>
    private static void ClearWorldState()
    {
        Nests.Clear();
        Memberships.Clear();
        DirtyNests.Clear();
        DirtyNestIds.Clear();
        HeartbeatNests.Clear();
        OrphanActors.Clear();
        OrphanActorIds.Clear();
        rebuildPending = true;
    }

    /// <summary>读取当前世界模拟时间。</summary>
    private static double CurrentTime => World.world?.getCurWorldTime() ?? 0d;

    /// <summary>供协调行动读取鼠群长期成员关系的适配器。</summary>
    private sealed class GroupProvider : ICoordinationGroupProvider
    {
        /// <inheritdoc />
        public string Id => ProviderId;

        /// <inheritdoc />
        public bool IsValid(in CoordinationGroupKey key)
        {
            return ResolveNest(key.OwnerId) != null &&
                   TryGetGroup(key.OwnerId, key.PartitionId, out GroupRuntime group) &&
                   group.Members.Count > 0;
        }

        /// <inheritdoc />
        public bool Contains(in CoordinationGroupKey key, Actor actor)
        {
            return ResolveNest(key.OwnerId) != null &&
                   !actor.isRekt() &&
                   Memberships.TryGetValue(actor.getID(), out Membership membership) &&
                   membership.NestId == key.OwnerId &&
                   membership.GroupIndex == key.PartitionId;
        }

        /// <inheritdoc />
        public void CollectMembers(in CoordinationGroupKey key, IList<Actor> output)
        {
            CollectGroupMembers(key, output);
        }

        /// <inheritdoc />
        public Actor ResolveLeader(in CoordinationGroupKey key)
        {
            return ResolveGroupLeader(key);
        }
    }

    /// <summary>把鼠群的稳定编组接入来源无关的战术情报与支援共享层。</summary>
    private sealed class SkavenCombatGroupProvider : ICombatGroupProvider
    {
        internal static readonly SkavenCombatGroupProvider Instance = new();

        /// <inheritdoc />
        public string Id => ProviderId;

        /// <inheritdoc />
        public bool TryResolveGroup(Actor actor, out CombatGroupKey key)
        {
            if (TryGetGroupKey(actor, out CoordinationGroupKey groupKey))
            {
                key = new CombatGroupKey(Id, groupKey.OwnerId, groupKey.PartitionId);
                return true;
            }
            key = default;
            return false;
        }

        /// <inheritdoc />
        public void CollectMembers(in CombatGroupKey key, IList<Actor> output)
        {
            var groupKey = new CoordinationGroupKey(ProviderId, key.OwnerId, key.PartitionId);
            CollectGroupMembers(groupKey, output);
        }

        /// <inheritdoc />
        public Actor ResolveLeader(in CombatGroupKey key)
        {
            var groupKey = new CoordinationGroupKey(ProviderId, key.OwnerId, key.PartitionId);
            return ResolveGroupLeader(groupKey);
        }

        /// <inheritdoc />
        public CombatDirective ResolveDefaultDirective(in CombatGroupKey key, Actor actor)
        {
            if (!TryGetGroup(key.OwnerId, key.PartitionId, out GroupRuntime group))
                return CombatDirective.Attack;
            if (Nests.TryGetValue(key.OwnerId, out NestRuntime nest) &&
                nest.MobilizedUntil > CurrentTime)
                return CombatDirective.Protect;
            return group.ActivityMode switch
            {
                SkavenActivityMode.Guard => CombatDirective.Hold,
                SkavenActivityMode.Defend => CombatDirective.Protect,
                _ => CombatDirective.Attack
            };
        }

        /// <inheritdoc />
        public bool UsesRoutMechanics => false;
    }

    /// <summary>单个鼠巢的增量运行时。</summary>
    private sealed class NestRuntime
    {
        /// <summary>创建 13 个固定小队槽。</summary>
        internal NestRuntime(Building nest)
        {
            NestId = nest.id;
            Groups = new GroupRuntime[SkavenEvolution.GroupCount];
            for (var i = 0; i < Groups.Length; i++) Groups[i] = new GroupRuntime(NestId, i);
            PatrolGroupCount = Randy.randomInt(1, 4);
            FirstPatrolGroup = (int)(nest.id % SkavenEvolution.GroupCount);
        }

        internal long NestId { get; }
        internal GroupRuntime[] Groups { get; }
        internal int PatrolGroupCount { get; }
        internal int FirstPatrolGroup { get; }
        internal double MobilizedUntil { get; set; }
        internal long LatestThreatId { get; set; }
        internal int LatestThreatTileId { get; set; } = -1;
        internal double NextUpdateAt { get; set; }
    }

    /// <summary>一个最多 13 人的小队运行时。</summary>
    private sealed class GroupRuntime
    {
        /// <summary>创建固定容量的小队。</summary>
        internal GroupRuntime(long nestId, int index)
        {
            NestId = nestId;
            Index = index;
        }

        internal long NestId { get; }
        internal int Index { get; }
        internal Dictionary<long, Membership> Members { get; } = new();
        internal bool[] UsedSlots { get; } = new bool[SkavenEvolution.GroupSize];
        internal long SlaveId { get; set; }
        internal long LeaderId { get; set; }
        internal double CombatStartedAt { get; set; }
        internal long ActivityId { get; set; }
        internal SkavenActivityMode ActivityMode { get; set; }
        internal int PatrolTargetTileId { get; set; } = -1;
        internal double NextPatrolTargetAt { get; set; }
    }

    /// <summary>角色在巢穴编组中的稳定位置。</summary>
    private readonly struct Membership
    {
        /// <summary>创建成员索引。</summary>
        internal Membership(long nestId, int groupIndex, int slot, bool isSlave)
        {
            NestId = nestId;
            GroupIndex = groupIndex;
            Slot = slot;
            IsSlave = isSlave;
        }

        internal long NestId { get; }
        internal int GroupIndex { get; }
        internal int Slot { get; }
        internal bool IsSlave { get; }
    }

    /// <summary>鼠群协调行动类别。</summary>
    private enum SkavenActivityMode
    {
        /// <summary>尚未建立行动。</summary>
        None,

        /// <summary>留守巢穴。</summary>
        Guard,

        /// <summary>在巢穴周围巡逻。</summary>
        Patrol,

        /// <summary>向近期威胁位置动员。</summary>
        Defend
    }

    /// <summary>鼠群行动会话，负责队长目标与成员阵位，不替代个人战术规划。</summary>
    private sealed class SkavenCoordinationSession : ICoordinatedActivitySession
    {
        private readonly long nestId;
        private readonly int groupIndex;
        private readonly SkavenActivityMode mode;

        /// <summary>绑定一个小队与行动类别。</summary>
        internal SkavenCoordinationSession(long nestId, int groupIndex, SkavenActivityMode mode)
        {
            this.nestId = nestId;
            this.groupIndex = groupIndex;
            this.mode = mode;
        }

        /// <inheritdoc />
        public void CollectCandidates(
            in CoordinatedActivityView activity,
            CoordinationRoleDefinition role,
            IList<CoordinationCandidate> output)
        {
            if (!TryGetGroup(nestId, groupIndex, out GroupRuntime group)) return;
            foreach (long actorId in group.Members.Keys)
            {
                Actor actor = ResolveActor(actorId);
                if (actor.isRekt()) continue;
                bool slave = Memberships.TryGetValue(actorId, out Membership membership) && membership.IsSlave;
                bool leader = actorId == group.LeaderId;
                if (role.Id == LeaderRoleId && leader)
                    output.Add(new CoordinationCandidate(actor, 1000f));
                else if (role.Id == SlaveRoleId && slave)
                    output.Add(new CoordinationCandidate(actor, 1000f));
                else if (role.Id == MemberRoleId && !leader)
                {
                    if (!slave) output.Add(new CoordinationCandidate(actor, SkavenEvolution.GetLevel(actor.asset)));
                }
            }
        }

        /// <inheritdoc />
        public bool IsParticipantValid(
            in CoordinatedActivityView activity,
            in CoordinationParticipantView participant,
            Actor actor)
        {
            if (!TryGetGroup(nestId, groupIndex, out GroupRuntime group) ||
                actor.isRekt() ||
                !group.Members.ContainsKey(actor.getID()))
                return false;
            bool slave = Memberships.TryGetValue(actor.getID(), out Membership membership) && membership.IsSlave;
            return participant.RoleId switch
            {
                LeaderRoleId => actor.getID() == group.LeaderId,
                SlaveRoleId => slave,
                MemberRoleId => actor.getID() != group.LeaderId && !slave,
                _ => false
            };
        }

        /// <inheritdoc />
        public void OnStageChanged(in CoordinationUpdateContext context)
        {
            RefreshOrders(context.Controller, context.Now);
        }

        /// <inheritdoc />
        public CoordinationSessionResult Update(in CoordinationUpdateContext context)
        {
            if (!TryGetGroup(nestId, groupIndex, out GroupRuntime group) || group.Members.Count == 0)
                return CoordinationSessionResult.Fail;
            RefreshOrders(context.Controller, context.Now);
            return CoordinationSessionResult.Continue;
        }

        /// <inheritdoc />
        public CoordinationParticipantResult TickParticipant(in CoordinationParticipantContext context)
        {
            Building nest = ResolveNest(nestId);
            if (nest == null) return CoordinationParticipantResult.FailActivity;
            if (mode == SkavenActivityMode.Guard && context.PlacementReady)
            {
                if (!context.Actor.is_inside_building)
                    context.Actor.stayInBuilding(nest);
            }
            else if (context.Actor.is_inside_building)
            {
                context.Actor.exitBuilding();
            }
            return CoordinationParticipantResult.Continue;
        }

        /// <inheritdoc />
        public string ResolvePresentationLocaleKey(
            in CoordinatedActivityView activity,
            in CoordinationParticipantView participant)
        {
            return mode switch
            {
                SkavenActivityMode.Patrol => "Task.Unit.Cultiway.Skaven.Patrol",
                SkavenActivityMode.Defend => "Task.Unit.Cultiway.Skaven.Defend",
                _ => "Task.Unit.Cultiway.Skaven.Guard"
            };
        }

        /// <inheritdoc />
        public void OnEnded(in CoordinatedActivityResult result)
        {
        }

        /// <summary>根据行动类别设置队长目标与成员动态跟随阵位。</summary>
        private void RefreshOrders(ICoordinatedActivityController controller, double now)
        {
            if (!TryGetGroup(nestId, groupIndex, out GroupRuntime group) ||
                !Nests.TryGetValue(nestId, out NestRuntime nest) ||
                ResolveNest(nestId)?.current_tile is not { } nestTile)
                return;
            WorldTile leaderTarget = ResolveLeaderTarget(nest, group, nestTile, now);
            if (leaderTarget == null) return;
            CoordinatedActivityView activity = controller.View;
            for (var i = 0; i < activity.Participants.Count; i++)
            {
                CoordinationParticipantView participant = activity.Participants[i];
                if (participant.RoleId == LeaderRoleId)
                {
                    controller.SetPlacement(
                        participant.ActorId,
                        CoordinationPlacementOrder.AtTile(
                            leaderTarget.tile_id,
                            default,
                            mode == SkavenActivityMode.Guard ? 1.5f : 1f,
                            holdPosition: mode != SkavenActivityMode.Patrol,
                            suspendWhileInCombat: true));
                    continue;
                }
                int slot = Memberships.TryGetValue(participant.ActorId, out Membership membership)
                    ? membership.Slot
                    : 0;
                Vector2Int offset = FormationOffsets[slot % FormationOffsets.Length];
                controller.SetPlacement(
                    participant.ActorId,
                    CoordinationPlacementOrder.FollowActor(
                        group.LeaderId,
                        offset,
                        1.25f,
                        1.5f,
                        suspendWhileInCombat: true));
            }
        }

        /// <summary>解析驻守巢穴、巡逻点或近期威胁位置。</summary>
        private WorldTile ResolveLeaderTarget(
            NestRuntime nest,
            GroupRuntime group,
            WorldTile nestTile,
            double now)
        {
            if (mode == SkavenActivityMode.Guard) return nestTile;
            if (mode == SkavenActivityMode.Defend)
            {
                Actor threat = ResolveActor(nest.LatestThreatId);
                if (!threat.isRekt()) return threat.current_tile;
                return ResolveTile(nest.LatestThreatTileId) ?? nestTile;
            }

            WorldTile patrol = ResolveTile(group.PatrolTargetTileId);
            Actor leader = ResolveActor(group.LeaderId);
            bool arrived = patrol != null && !leader.isRekt() &&
                           Toolbox.SquaredDistVec2Float(leader.current_position, patrol.posV3) <= 2.25f;
            if (patrol == null || arrived || now >= group.NextPatrolTargetAt)
            {
                patrol = SelectPatrolTarget(nestTile);
                group.PatrolTargetTileId = patrol.tile_id;
                group.NextPatrolTargetAt = now + 8d;
            }
            return patrol;
        }
    }

    /// <summary>按世界稳定 ID 解析地块。</summary>
    private static WorldTile ResolveTile(int tileId)
    {
        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && tileId >= 0 && tileId < tiles.Length ? tiles[tileId] : null;
    }

    /// <summary>鼠群服务的主线程更新入口。</summary>
    private sealed class UpdateSystem : BaseSystem
    {
        /// <inheritdoc />
        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            Update();
        }
    }
}

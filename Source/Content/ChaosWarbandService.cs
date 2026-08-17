using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Coordination;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>按神塔维护混沌战帮，每名大魔固定领导一个小组。</summary>
public static class ChaosWarbandService
{
    public const string LeaderRoleId = "chaos_leader";
    public const string MemberRoleId = "chaos_member";
    private const string ProviderId = "cultiway.chaos_warband";
    private const string GroupDataKey = "cultiway.chaos.group";
    private const double Heartbeat = 0.5d;
    private static readonly Dictionary<long, TowerRuntime> Towers = new();
    private static readonly Dictionary<long, Membership> Memberships = new();
    private static bool initialized;
    private static bool rebuildPending = true;

    private static readonly ActorAsset[] NurgleLeaders =
        [Actors.GreatUncleanOneButcher, Actors.GreatUncleanOneBellRinger, Actors.GreatUncleanOneRainFather];
    private static readonly ActorAsset[] SlaaneshLeaders =
        [Actors.KeeperSecrets, Actors.KeeperSecretsNakari, Actors.ExaltedKeeperSecrets];
    private static readonly ActorAsset[] TzeentchLeaders =
        [Actors.LordChange, Actors.KairosFateweaver, Actors.ExaltedLordChange];
    private static readonly ActorAsset[] KhorneLeaders =
        [Actors.Bloodthirster, Actors.AnggrathUnbound, Actors.ExaltedBloodthirster];
    private const int PatrolRadius = 16;
    private const int PatrolTargetAttempts = 12;
    private const double PatrolTargetDuration = 8d;

    public static void Init()
    {
        if (initialized) return;
        initialized = true;
        AdvancedUnitSpawner.RegisterActionOnUnitSpawned(OnUnitSpawned);
        ActorExtend.RegisterActionOnDeath(actor => Remove(actor?.Base));
        CoordinatedActivityService.RegisterGroupProvider(new GroupProvider());
        CombatWorldService.RegisterGroupProvider(CombatProvider.Instance, 90);
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    public static CoordinationParticipantResult TickActor(Actor actor)
    {
        if (!EnsureRegistered(actor)) return CoordinationParticipantResult.Leave;
        return CoordinatedActivityService.TickParticipant(actor);
    }

    /// <summary>把升魔后的领袖直接绑定到指定神塔对应的大魔战帮。</summary>
    public static void RegisterAscendedLeader(Actor actor, Building tower)
    {
        if (actor == null || tower == null || ResolveTower(tower) == null || !IsLeader(actor.asset, tower.asset))
            return;
        Remove(actor);
        actor.SetSourceSpawnerId(tower.id);
        actor.SetSourceSpawnerAssetId(tower.asset.id);
        Register(tower, actor);
    }

    private static void OnUnitSpawned(Building source, Actor actor)
    {
        if (ResolveTower(source) != null) Register(source, actor);
    }

    private static void Update()
    {
        if (rebuildPending) RebuildFromWorld();
        using var invalidTowerIds = new ListPool<long>();
        foreach (TowerRuntime tower in Towers.Values)
        {
            if (ResolveTower(tower.TowerId) == null)
                invalidTowerIds.Add(tower.TowerId);
            else
                ProcessTower(tower);
        }
        for (var i = 0; i < invalidTowerIds.Count; i++) RemoveTower(invalidTowerIds[i]);
    }

    private static void RebuildFromWorld()
    {
        if (World.world?.units == null || World.world.buildings == null) return;
        rebuildPending = false;
        ForEachChaosActor(actor => EnsureRegistered(actor));
    }

    private static void ProcessTower(TowerRuntime tower)
    {
        if (World.world.getCurWorldTime() < tower.NextUpdateAt) return;
        tower.NextUpdateAt = World.world.getCurWorldTime() + Heartbeat;
        for (var i = 0; i < tower.Groups.Length; i++)
        {
            GroupRuntime group = tower.Groups[i];
            RemoveInvalidMembers(group);
            Actor leader = ResolveActor(group.LeaderId);
            if (leader.isRekt())
            {
                if (group.ActivityId > 0) CoordinatedActivityService.Cancel(group.ActivityId);
                group.ActivityId = 0;
                continue;
            }
            EnsureActivity(tower, group, leader);
        }
    }

    private static void EnsureActivity(TowerRuntime tower, GroupRuntime group, Actor leader)
    {
        var key = new CoordinationGroupKey(ProviderId, tower.TowerId, group.Index);
        if (group.ActivityId > 0 &&
            CoordinatedActivityService.TryGetActivityId(CoordinationActivities.ChaosWarband.id, key, out long current) &&
            current == group.ActivityId)
            return;
        var session = new ChaosWarbandSession(tower.TowerId, group.Index);
        if (CoordinatedActivityService.TryStart(
                CoordinationActivities.ChaosWarband,
                key,
                session,
                [new CoordinationInitialParticipant(leader, LeaderRoleId)],
                out long activityId))
            group.ActivityId = activityId;
    }

    private static bool EnsureRegistered(Actor actor)
    {
        if (actor.isRekt() || !IsChaos(actor)) return false;
        if (Memberships.ContainsKey(actor.getID())) return true;
        Building tower = ResolveTower(actor.GetSourceSpawnerId());
        if (tower == null) return false;
        Register(tower, actor);
        return Memberships.ContainsKey(actor.getID());
    }

    private static void Register(Building towerBuilding, Actor actor)
    {
        if (actor.isRekt() || !IsChaos(actor) || Memberships.ContainsKey(actor.getID())) return;
        TowerRuntime tower = GetOrCreateTower(towerBuilding);
        int leaderIndex = GetLeaderIndex(actor.asset, tower.Asset);
        int groupIndex = leaderIndex >= 0 ? leaderIndex : SelectMemberGroup(tower);
        GroupRuntime group = tower.Groups[groupIndex];
        var membership = new Membership(tower.TowerId, groupIndex, leaderIndex >= 0);
        group.Members.Add(actor.getID(), membership);
        Memberships.Add(actor.getID(), membership);
        if (leaderIndex >= 0) group.LeaderId = actor.getID();
        actor.data.set(GroupDataKey, groupIndex);
    }

    private static int SelectMemberGroup(TowerRuntime tower)
    {
        var selected = 0;
        for (var i = 1; i < tower.Groups.Length; i++)
            if (tower.Groups[i].MemberCount < tower.Groups[selected].MemberCount) selected = i;
        return selected;
    }

    private static void Remove(Actor actor)
    {
        if (actor == null || !Memberships.Remove(actor.getID(), out Membership membership) ||
            !Towers.TryGetValue(membership.TowerId, out TowerRuntime tower)) return;
        GroupRuntime group = tower.Groups[membership.GroupIndex];
        group.Members.Remove(actor.getID());
        if (group.LeaderId == actor.getID()) group.LeaderId = 0;
    }

    private static void RemoveTower(long towerId)
    {
        if (!Towers.TryGetValue(towerId, out TowerRuntime tower)) return;
        for (var i = 0; i < tower.Groups.Length; i++)
        {
            GroupRuntime group = tower.Groups[i];
            if (group.ActivityId > 0) CoordinatedActivityService.Cancel(group.ActivityId);
            foreach (long actorId in group.Members.Keys) Memberships.Remove(actorId);
        }
        Towers.Remove(towerId);
    }

    private static void RemoveInvalidMembers(GroupRuntime group)
    {
        using var invalid = new ListPool<long>();
        foreach (long id in group.Members.Keys)
        {
            Actor actor = ResolveActor(id);
            if (actor.isRekt() || !IsChaos(actor) || ResolveTower(actor.GetSourceSpawnerId())?.id != group.TowerId)
                invalid.Add(id);
        }
        for (var i = 0; i < invalid.Count; i++) Remove(ResolveActor(invalid[i]));
    }

    private static TowerRuntime GetOrCreateTower(Building tower)
    {
        if (Towers.TryGetValue(tower.id, out TowerRuntime runtime)) return runtime;
        runtime = new TowerRuntime(tower.id, tower.asset);
        Towers.Add(tower.id, runtime);
        return runtime;
    }

    private static int GetLeaderIndex(ActorAsset asset, BuildingAsset tower)
    {
        ActorAsset[] leaders = GetLeaders(tower);
        for (var i = 0; i < leaders.Length; i++) if (leaders[i] == asset) return i;
        return -1;
    }

    private static bool IsLeader(ActorAsset asset, BuildingAsset tower) => GetLeaderIndex(asset, tower) >= 0;

    private static ActorAsset[] GetLeaders(BuildingAsset tower) => tower == Buildings.NurgleTower ? NurgleLeaders :
        tower == Buildings.SlaaneshTower ? SlaaneshLeaders :
        tower == Buildings.TzeentchTower ? TzeentchLeaders : KhorneLeaders;

    private static bool IsChaos(Actor actor) => actor != null && ResolveTower(actor.GetSourceSpawnerId()) != null;

    private static void ForEachChaosActor(Action<Actor> action)
    {
        ActorAsset[][] groups =
            [NurgleLeaders, SlaaneshLeaders, TzeentchLeaders, KhorneLeaders];
        for (var g = 0; g < groups.Length; g++)
            for (var i = 0; i < groups[g].Length; i++) foreach (Actor actor in groups[g][i].units) action(actor);
        ActorAsset[] members =
        [Actors.UncleanCreature, Actors.NurgleSpirit, Actors.NurgleDiseaseCarrier, Actors.PlagueBringer, Actors.PlagueToad,
         Actors.Daemonette, Actors.Hellflayer, Actors.SlaaneshSeeker, Actors.SlaaneshMistress, Actors.SlaaneshFiend,
         Actors.PinkHorrorTzeentch, Actors.BlueHorrorTzeentch, Actors.IridescentHorrorTzeentch, Actors.FlamerTzeentch, Actors.ScreamersTzeentch,
         Actors.BloodletterKhorne, Actors.FleshHoundKhorne, Actors.BloodcrusherKhorne, Actors.MinotaurKhorne, Actors.SkullCannonKhorne];
        for (var i = 0; i < members.Length; i++) foreach (Actor actor in members[i].units) action(actor);
    }

    private static Building ResolveTower(long id) => ResolveTower(World.world?.buildings?.get(id));
    private static Building ResolveTower(Building tower) => tower != null && !tower.isRekt() &&
        (tower.asset == Buildings.NurgleTower || tower.asset == Buildings.SlaaneshTower ||
         tower.asset == Buildings.TzeentchTower || tower.asset == Buildings.KhorneTower) ? tower : null;
    private static Actor ResolveActor(long id) => id > 0 ? World.world?.units?.get(id) : null;

    private static void ClearWorldState()
    {
        Towers.Clear(); Memberships.Clear(); rebuildPending = true;
    }

    private sealed class UpdateSystem : BaseSystem, IWorldStateClearable
    {
        void IWorldStateClearable.ClearWorldState() { ChaosWarbandService.ClearWorldState(); }
        protected override void OnUpdateGroup() { Update(); }
    }
    private sealed class TowerRuntime
    {
        internal TowerRuntime(long id, BuildingAsset asset) { TowerId = id; Asset = asset; Groups = [new GroupRuntime(id, 0), new GroupRuntime(id, 1), new GroupRuntime(id, 2)]; }
        internal long TowerId; internal BuildingAsset Asset; internal GroupRuntime[] Groups; internal double NextUpdateAt;
    }
    private sealed class GroupRuntime
    {
        internal GroupRuntime(long towerId, int index) { TowerId = towerId; Index = index; }
        internal long TowerId; internal int Index; internal long LeaderId; internal long ActivityId; internal Dictionary<long, Membership> Members = new();
        internal int MemberCount => Members.Count;
        internal int PatrolTargetTileId = -1;
        internal double NextPatrolTargetAt;
    }
    private readonly struct Membership
    {
        internal Membership(long towerId, int groupIndex, bool leader) { TowerId = towerId; GroupIndex = groupIndex; IsLeader = leader; }
        internal readonly long TowerId; internal readonly int GroupIndex; internal readonly bool IsLeader;
    }

    private sealed class GroupProvider : ICoordinationGroupProvider
    {
        public string Id => ProviderId;
        public bool IsValid(in CoordinationGroupKey key) => Towers.TryGetValue(key.OwnerId, out TowerRuntime tower) && key.PartitionId >= 0 && key.PartitionId < 3 && tower.Groups[key.PartitionId].Members.Count > 0;
        public bool Contains(in CoordinationGroupKey key, Actor actor) => actor != null && Memberships.TryGetValue(actor.getID(), out Membership m) && m.TowerId == key.OwnerId && m.GroupIndex == key.PartitionId;
        public void CollectMembers(in CoordinationGroupKey key, IList<Actor> output) { if (!Towers.TryGetValue(key.OwnerId, out TowerRuntime tower)) return; foreach (long id in tower.Groups[key.PartitionId].Members.Keys) { Actor actor = ResolveActor(id); if (!actor.isRekt()) output.Add(actor); } }
        public Actor ResolveLeader(in CoordinationGroupKey key) => Towers.TryGetValue(key.OwnerId, out TowerRuntime tower) ? ResolveActor(tower.Groups[key.PartitionId].LeaderId) : null;
    }
    private sealed class CombatProvider : ICombatGroupProvider
    {
        internal static readonly CombatProvider Instance = new();
        public string Id => ProviderId;
        public bool TryResolveGroup(Actor actor, out CombatGroupKey key) { if (Memberships.TryGetValue(actor.getID(), out Membership m)) { key = new CombatGroupKey(Id, m.TowerId, m.GroupIndex); return true; } key = default; return false; }
        public void CollectMembers(in CombatGroupKey key, IList<Actor> output) => new GroupProvider().CollectMembers(new CoordinationGroupKey(Id, key.OwnerId, key.PartitionId), output);
        public Actor ResolveLeader(in CombatGroupKey key) => new GroupProvider().ResolveLeader(new CoordinationGroupKey(Id, key.OwnerId, key.PartitionId));
        public CombatDirective ResolveDefaultDirective(in CombatGroupKey key, Actor actor) => CombatDirective.Attack;
        public bool UsesRoutMechanics => false;
    }
    private sealed class ChaosWarbandSession : ICoordinatedActivitySession
    {
        private readonly long towerId; private readonly int groupIndex;
        internal ChaosWarbandSession(long towerId, int groupIndex) { this.towerId = towerId; this.groupIndex = groupIndex; }
        public void CollectCandidates(in CoordinatedActivityView activity, CoordinationRoleDefinition role, IList<CoordinationCandidate> output) { if (!Towers.TryGetValue(towerId, out TowerRuntime tower)) return; foreach (long id in tower.Groups[groupIndex].Members.Keys) { Actor actor = ResolveActor(id); if (actor.isRekt()) continue; bool leader = id == tower.Groups[groupIndex].LeaderId; if (role.Id == LeaderRoleId && leader) output.Add(new CoordinationCandidate(actor, 1000f)); else if (role.Id == MemberRoleId && !leader) output.Add(new CoordinationCandidate(actor, 1f)); } }
        public bool IsParticipantValid(in CoordinatedActivityView activity, in CoordinationParticipantView participant, Actor actor) => Towers.TryGetValue(towerId, out TowerRuntime tower) && tower.Groups[groupIndex].Members.ContainsKey(actor.getID()) && !actor.isRekt() && (participant.RoleId == LeaderRoleId ? actor.getID() == tower.Groups[groupIndex].LeaderId : actor.getID() != tower.Groups[groupIndex].LeaderId);
        public void OnStageChanged(in CoordinationUpdateContext context) => Refresh(context.Controller);
        public CoordinationSessionResult Update(in CoordinationUpdateContext context) { Refresh(context.Controller); return CoordinationSessionResult.Continue; }
        public CoordinationParticipantResult TickParticipant(in CoordinationParticipantContext context) => CoordinationParticipantResult.Continue;
        public string ResolvePresentationLocaleKey(in CoordinatedActivityView activity, in CoordinationParticipantView participant) => "Task.Unit.Cultiway.Chaos.Warband";
        public void OnEnded(in CoordinatedActivityResult result) { }
        private void Refresh(ICoordinatedActivityController controller)
        {
            if (!Towers.TryGetValue(towerId, out TowerRuntime tower) ||
                tower.Groups[groupIndex].LeaderId <= 0 ||
                ResolveTower(towerId)?.current_tile is not { } towerTile)
                return;

            GroupRuntime group = tower.Groups[groupIndex];
            WorldTile patrolTarget = ResolvePatrolTarget(towerTile, group, World.world?.getCurWorldTime() ?? 0d);
            if (patrolTarget == null) return;
            long leaderId = group.LeaderId;
            foreach (CoordinationParticipantView participant in controller.View.Participants)
            {
                if (participant.RoleId == LeaderRoleId)
                {
                    controller.SetPlacement(
                        participant.ActorId,
                        CoordinationPlacementOrder.AtTile(
                            patrolTarget.tile_id,
                            default,
                            1f,
                            holdPosition: false,
                            suspendWhileInCombat: true));
                    continue;
                }

                controller.SetPlacement(
                    participant.ActorId,
                    CoordinationPlacementOrder.FollowActor(
                        leaderId,
                        new Vector2Int((int)(participant.ActorId % 3) - 1,
                            (int)(participant.ActorId / 3) - 1),
                        1.25f,
                        1.5f,
                        suspendWhileInCombat: true));
            }
        }

        private static WorldTile ResolvePatrolTarget(WorldTile towerTile, GroupRuntime group, double now)
        {
            WorldTile patrolTarget = ResolveTile(group.PatrolTargetTileId);
            Actor leader = ResolveActor(group.LeaderId);
            bool arrived = patrolTarget != null && !leader.isRekt() &&
                           Toolbox.SquaredDistVec2Float(leader.current_position, patrolTarget.posV3) <= 2.25f;
            if (patrolTarget == null || arrived || now >= group.NextPatrolTargetAt)
            {
                patrolTarget = SelectPatrolTarget(towerTile);
                group.PatrolTargetTileId = patrolTarget.tile_id;
                group.NextPatrolTargetAt = now + PatrolTargetDuration;
            }
            return patrolTarget;
        }

        private static WorldTile SelectPatrolTarget(WorldTile towerTile)
        {
            for (var i = 0; i < PatrolTargetAttempts; i++)
            {
                int x = towerTile.x + Randy.randomInt(-PatrolRadius, PatrolRadius + 1);
                int y = towerTile.y + Randy.randomInt(-PatrolRadius, PatrolRadius + 1);
                WorldTile target = World.world.GetTile(x, y);
                if (target != null && target.isSameIsland(towerTile)) return target;
            }
            return towerTile;
        }
    }

    private static WorldTile ResolveTile(int tileId)
    {
        WorldTile[] tiles = World.world?.tiles_list;
        return tiles != null && tileId >= 0 && tileId < tiles.Length ? tiles[tileId] : null;
    }
}

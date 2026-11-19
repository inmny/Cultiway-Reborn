using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Railways;
using Cultiway.Content.VillageDuties;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.ControlledTasks;
using Cultiway.Core.Coordination;
using UnityEngine;

namespace Cultiway.Content.VillageMilitary;

/// <summary>村庄小队的例行勤务、整军命令与固定方格阵列，战斗群组继续使用原版 Army。</summary>
public static class VillageMilitaryActivities
{
    /// <summary>村庄小队协调群组编号。</summary>
    public const string ProviderId = "cultiway.village.squad";
    /// <summary>统一成员席位；指挥者由稳定编制指定。</summary>
    private const string MemberRole = "member";
    /// <summary>工作月普通勤务。</summary>
    private static CoordinatedActivityDefinitionAsset routine;
    /// <summary>可以打断休息和特殊自主活动的明确军令。</summary>
    private static CoordinatedActivityDefinitionAsset military;
    /// <summary>原版显式战术命令的有效时间与类别。</summary>
    private static readonly Dictionary<long, (CombatDirective directive, double until, int tile)> orders = new();

    /// <summary>登记两个行动定义，复用已有通用协调工作与战斗系统。</summary>
    public static void Init()
    {
        CoordinatedActivityService.RegisterGroupProvider(new Provider());
        routine = Definition("Cultiway.Village.Routine", 250);
        military = Definition("Cultiway.Village.Military", 8000);
        CoordinatedActivityService.RegisterDefinition(routine);
        CoordinatedActivityService.RegisterDefinition(military);
    }

    /// <summary>运行中的原版军令仍由同一个 Army 执行，另存下协调移动需要的目标。</summary>
    /// <param name="army">收到明确军令的原版军队。</param>
    /// <param name="directive">原版战术指令种类。</param>
    /// <param name="duration">指令持续秒数，零表示持续有效。</param>
    public static void RecordOrder(Army army, CombatDirective directive, float duration)
    {
        var target = directive == CombatDirective.Retreat ? army.getCity()?.getTile() : army.getCaptain()?.current_tile;
        orders[army.id] = (directive, duration > 0 ? World.world.getCurWorldTime() + duration : double.MaxValue, target?.tile_id ?? -1);
        VillageMilitaryService.InvalidateOrders(army.getCity());
    }

    /// <summary>清除跨世界的军令引用。</summary>
    public static void Clear() => orders.Clear();

    /// <summary>国家归属变化后不沿用原国家的持续战术命令。</summary>
    /// <param name="city">变更归属的村庄。</param>
    public static void ClearOrders(City city)
    { if (city?.army != null) orders.Remove(city.army.id); VillageMilitaryService.InvalidateOrders(city); }

    /// <summary>显式撤退和驻守优先于旧出征目标；进攻仍使用原版指定的区域。</summary>
    /// <param name="city">正在执行军令的村庄。</param>
    private static WorldTile MilitaryTarget(City city)
    {
        bool explicitOrder = orders.TryGetValue(city.army?.id ?? 0, out var order) && World.world.getCurWorldTime() < order.until;
        if (explicitOrder && order.directive != CombatDirective.Attack && order.tile >= 0) return World.world.tiles_list[order.tile];
        if (city.hasAttackZoneOrder()) return city.target_attack_zone.centerTile;
        if (explicitOrder && order.tile >= 0) return World.world.tiles_list[order.tile];
        return city.getTile();
    }

    /// <summary>判断村庄当前是否有实际军令；宣战状态本身不取消休息。</summary>
    /// <param name="city">需要判断作息例外的村庄。</param>
    public static bool IsOrdered(City city)
    {
        if (VillageDutyService.HasEmergency(city)) return true;
        return city.hasArmy() && orders.TryGetValue(city.army.id, out var order) && World.world.getCurWorldTime() < order.until;
    }

    /// <summary>单个小队的参与资格，直接操控和乘坐状态暂不被接管。</summary>
    /// <param name="actor">本次尝试参加的人物。</param>
    /// <param name="city">所属村庄。</param>
    /// <param name="ordered">是否执行明确军令或紧急防卫。</param>
    private static bool Eligible(Actor actor, City city, bool ordered) => actor != null && actor.city == city &&
        actor.army == city.army && VillageDutyService.CanPerformMilitaryDuty(actor, ordered) &&
        !actor.is_inside_boat && !actor.isInsideSomething() && !actor.isHungry() &&
        !ControllableUnit.isControllingUnit(actor) && !ControlledTaskOrderService.TryGetActiveOrderId(actor.id, out _) &&
        life.taxi.TaxiManager.getRequestForActor(actor) == null &&
        Cultiway.Core.Pathfinding.PortalManager.GetRequest(actor) == null && actor.ai.task?.flag_boat_related != true &&
        (!ordered || MilitaryTarget(city).isSameIsland(actor.current_tile));

    /// <summary>配置与现有协调工作对应的长期席位。</summary>
    /// <param name="id">行动定义编号。</param>
    /// <param name="priority">与其他协调活动竞争时的优先级。</param>
    private static CoordinatedActivityDefinitionAsset Definition(string id, int priority) => new()
    {
        id = id, Priority = priority, Preemptible = true, RecruitmentTimeoutSeconds = 4,
        AssemblyTimeoutSeconds = 30, HeartbeatSeconds = .25f, MinimumReadyCount = 1,
        MinimumReadyRatio = 0, RunningReadinessPolicy = CoordinationRunningReadinessPolicy.Ignore,
        Roles = [new CoordinationRoleDefinition { Id = MemberRole, MinimumCount = 1, MaximumCount = 0,
            MinimumReadyCount = 1, ParticipationMode = CoordinationParticipationMode.Forced,
            AllowLateJoin = true, ParticipantLifetime = CoordinationParticipantLifetime.ActivityBound,
            ExecutionTaskIds = [ActorTasks.CoordinatedActivity.id] }]
    };

    /// <summary>只处理真实小队，休息、领队变化和任务轮换时统一释放旧行动。</summary>
    /// <param name="city">正在安排勤务的村庄。</param>
    /// <param name="data">该村现有编制。</param>
    public static void Update(City city, VillageMilitaryData data)
    {
        bool ordered = IsOrdered(city);
        double now = World.world.getCurWorldTime();
        int target = ordered ? MilitaryTarget(city).tile_id : -1;
        if (target != data.MilitaryTarget) { data.MilitaryTarget = target; data.OrderRevision++; }
        foreach (var squad in data.Squads)
        {
            var members = squad.Members.Keys.Select(id => World.world.units.get(id)).Where(a => Eligible(a, city, ordered)).ToList();
            var leader = members.FirstOrDefault(a => a.id == squad.Commander);
            if (leader == null) { members.Sort(MilitaryMeritService.Compare); leader = members.FirstOrDefault(); }
            if (leader == null) { VillageMilitaryService.Cancel(squad); continue; }
            if (squad.ActivityId > 0 && squad.Ordered == ordered && squad.OrderRevision == data.OrderRevision && squad.DutyLeader == leader.id &&
                CoordinatedActivityService.TryGetActivityId((ordered ? military : routine).id,
                    new CoordinationGroupKey(ProviderId, city.id, squad.Id), out long current) && current == squad.ActivityId &&
                (ordered || now < squad.NextRoutine)) continue;
            VillageMilitaryService.Cancel(squad);
            squad.Ordered = ordered; squad.DutyLeader = leader.id; squad.OrderRevision = data.OrderRevision;
            if (!ordered) { squad.Routine = (squad.Routine + 1) % 3; squad.NextRoutine = now + (squad.Routine == 0 ? 30 : 10); }
            var destination = SelectTarget(city, squad, leader, ordered);
            if (!ordered && squad.Routine == 0 && destination == null)
            { squad.Routine = 2; squad.NextRoutine = now + 10; destination = SelectTarget(city, squad, leader, false); }
            squad.TargetTile = destination?.tile_id ?? leader.current_tile.tile_id;
            var initial = members.Select(a => new CoordinationInitialParticipant(a, MemberRole)).ToArray();
            if (CoordinatedActivityService.TryStart(ordered ? military : routine,
                new CoordinationGroupKey(ProviderId, city.id, squad.Id), new Session(city.id, squad.Id), initial, out long activity))
            {
                squad.ActivityId = activity;
                foreach (var actor in members) Activate(actor);
            }
        }
    }

    /// <summary>安全结束旧工作后切换到已经分配的协调席位，避免自然工作长时间不结束。</summary>
    /// <param name="actor">已经取得小队活动席位的人物。</param>
    private static void Activate(Actor actor)
    {
        if (actor.ai.job?.id == ActorJobs.CoordinatedActivity.id || !VillageDutyService.CanChangeAssignment(actor) || actor.timer_action > 0) return;
        string job = null;
        if (!CoordinatedActivityService.TryContinueAssignedJob(actor, ref job)) return;
        actor.cancelAllBeh(); actor.ai.setJob(job);
    }

    /// <summary>选择同岛安全点，固定尝试次数而不为每次巡逻扫描全村。</summary>
    /// <param name="city">巡逻和驻守所属村庄。</param>
    /// <param name="squad">当前勤务轮次所属小队。</param>
    /// <param name="leader">实际带队人物。</param>
    /// <param name="ordered">是否应使用军令目标。</param>
    private static WorldTile SelectTarget(City city, VillageSquad squad, Actor leader, bool ordered)
    {
        if (ordered)
        {
            return MilitaryTarget(city);
        }
        var center = city.getTile();
        if (squad.Routine == 0)
        {
            var barracks = city.buildings.Where(b => b.asset.type == "type_barracks" && b.isUsable() &&
                !b.isUnderConstruction() && !b.isAbandoned() && b.current_tile.isSameIsland(leader.current_tile))
                .OrderBy(b => Toolbox.SquaredDistTile(leader.current_tile, b.current_tile)).FirstOrDefault();
            if (barracks == null) return null;
            center = barracks.current_tile;
        }
        int radius = !ordered && squad.Routine == 1 ? 18 : 8;
        for (int i = 0; i < 16; i++)
        {
            var tile = World.world.GetTile(center.x + Randy.randomInt(-radius, radius + 1), center.y + Randy.randomInt(-radius, radius + 1));
            if (tile != null && tile.zone?.city == city && RailTrackRoutePlanner.IsSafeGround(tile) &&
                (tile.building == null || tile.building.asset.flora) && tile.isSameIsland(leader.current_tile)) return tile;
        }
        return squad.Routine == 0 ? null : leader.current_tile;
    }

    /// <summary>把稳定村庄小队适配为已有协调行动群组。</summary>
    private sealed class Provider : ICoordinationGroupProvider
    {
        /// <inheritdoc />
        public string Id => ProviderId;
        /// <inheritdoc />
        public bool IsValid(in CoordinationGroupKey key) => Resolve(key.OwnerId, key.PartitionId, out _, out var squad) && squad.Members.Count > 0;
        /// <inheritdoc />
        public bool Contains(in CoordinationGroupKey key, Actor actor) => actor != null &&
            Resolve(key.OwnerId, key.PartitionId, out _, out var squad) && squad.Members.ContainsKey(actor.id);
        /// <inheritdoc />
        public void CollectMembers(in CoordinationGroupKey key, IList<Actor> output)
        {
            if (!Resolve(key.OwnerId, key.PartitionId, out _, out var squad)) return;
            foreach (long id in squad.Members.Keys) if (World.world.units.get(id) is { } actor && actor.isAlive()) output.Add(actor);
        }
        /// <inheritdoc />
        public Actor ResolveLeader(in CoordinationGroupKey key) => Resolve(key.OwnerId, key.PartitionId, out _, out var squad)
            ? World.world.units.get(squad.DutyLeader) : null;
    }

    /// <summary>由稳定编号解析当前世界中的真实单位。</summary>
    /// <param name="cityId">所属村庄编号。</param>
    /// <param name="squadId">下级小队编号。</param>
    /// <param name="city">解析得到的当前村庄。</param>
    /// <param name="squad">解析得到的当前小队。</param>
    private static bool Resolve(long cityId, int squadId, out City city, out VillageSquad squad)
    {
        city = World.world.cities.get(cityId);
        squad = city == null || !city.isAlive() ? null : VillageMilitaryService.Get(city).Squads.FirstOrDefault(s => s.Id == squadId);
        return squad != null;
    }

    /// <summary>每个演员只属于一个小队活动，上级只计算军令和阵列锚点。</summary>
    private sealed class Session : ICoordinatedActivitySession
    {
        /// <summary>所属村庄。</summary>
        private readonly long cityId;
        /// <summary>所属小队。</summary>
        private readonly int squadId;
        /// <summary>到场操练的下次经验结算时刻。</summary>
        private double nextPractice;
        /// <summary>动作按画面时间留出间隔，高倍速下也能看清挥击。</summary>
        private float nextDrillAnimation;
        /// <summary>建立一份不持有永久游戏对象引用的会话。</summary>
        /// <param name="cityId">所属村庄编号。</param>
        /// <param name="squadId">本次行动的小队编号。</param>
        public Session(long cityId, int squadId) { this.cityId = cityId; this.squadId = squadId; }
        /// <inheritdoc />
        public void CollectCandidates(in CoordinatedActivityView activity, CoordinationRoleDefinition role, IList<CoordinationCandidate> output)
        {
            if (!Resolve(cityId, squadId, out var city, out var squad)) return;
            foreach (long id in squad.Members.Keys)
                if (World.world.units.get(id) is { } a && Eligible(a, city, squad.Ordered)) output.Add(new CoordinationCandidate(a, 0));
        }
        /// <inheritdoc />
        public bool IsParticipantValid(in CoordinatedActivityView activity, in CoordinationParticipantView participant, Actor actor)
            => Resolve(cityId, squadId, out var city, out var squad) && squad.Members.ContainsKey(actor.id) && Eligible(actor, city, squad.Ordered);
        /// <inheritdoc />
        public void OnStageChanged(in CoordinationUpdateContext context) => Refresh(context.Controller);
        /// <inheritdoc />
        public CoordinationSessionResult Update(in CoordinationUpdateContext context)
        {
            if (!Resolve(cityId, squadId, out var city, out var squad) || squad.Ordered != IsOrdered(city) ||
                squad.OrderRevision != VillageMilitaryService.Get(city).OrderRevision ||
                !squad.Ordered && (!VillageDutyService.IsWorkMonth(city) || context.Now >= squad.NextRoutine)) return CoordinationSessionResult.Complete;
            Refresh(context.Controller);
            if (!squad.Ordered && squad.Routine == 0 && context.Controller.View.Stage == CoordinatedActivityStage.Running)
            {
                bool experience = context.Now >= nextPractice;
                bool animate = Time.unscaledTime >= nextDrillAnimation;
                if (experience) nextPractice = context.Now + 5;
                if (animate) nextDrillAnimation = Time.unscaledTime + .8f;
                if (experience || animate)
                    foreach (var p in context.Controller.View.Participants)
                    {
                        if (!p.Ready || World.world.units.get(p.ActorId) is not { } actor || actor.isFighting() ||
                            actor.is_moving || !VillageDutyService.CanPerformMilitaryDuty(actor, false)) continue;
                        if (experience) actor.addExperience(1);
                        if (animate) actor.punchTargetAnimation((Vector3)actor.current_position + new Vector3(squad.Heading.x, squad.Heading.y));
                    }
            }
            return CoordinationSessionResult.Continue;
        }
        /// <inheritdoc />
        public CoordinationParticipantResult TickParticipant(in CoordinationParticipantContext context) => CoordinationParticipantResult.Continue;
        /// <inheritdoc />
        public string ResolvePresentationLocaleKey(in CoordinatedActivityView activity, in CoordinationParticipantView participant)
            => Resolve(cityId, squadId, out _, out var squad) ? "Cultiway.Village.Task." + (squad.Ordered ? "Orders" : squad.Routine.ToString()) : "Cultiway.Village.Title";
        /// <inheritdoc />
        public void OnEnded(in CoordinatedActivityResult result)
        {
            var squad = VillageMilitaryService.FindCachedSquad(cityId, squadId);
            if (squad != null && squad.ActivityId == result.ActivityId) squad.ActivityId = 0;
        }

        /// <summary>更新已有成员的位置指令，整军行动投影到同一个实际统领。</summary>
        /// <param name="controller">当前小队活动的位置和到场状态控制器。</param>
        private void Refresh(ICoordinatedActivityController controller)
        {
            if (!Resolve(cityId, squadId, out var city, out var squad)) return;
            var leader = World.world.units.get(squad.DutyLeader);
            if (leader == null || !leader.isAlive()) return;
            var data = VillageMilitaryService.Get(city);
            var armyLeader = squad.Ordered ? city.army?.getCaptain() : null;
            var anchor = armyLeader != null && Eligible(armyLeader, city, true) ? armyLeader : leader;
            var target = World.world.tiles_list[squad.TargetTile];
            if (!squad.Ordered && squad.Routine == 1 && Toolbox.SquaredDistTile(leader.current_tile, target) < 4)
            { squad.TargetTile = SelectTarget(city, squad, leader, false).tile_id; target = World.world.tiles_list[squad.TargetTile]; }
            Vector2 direction = (Vector2)target.posV3 - anchor.current_position;
            if (direction.sqrMagnitude > 2)
            {
                if (squad.Ordered) data.ArmyHeading = direction; else squad.Heading = direction;
            }
            Vector2 heading = squad.Ordered ? data.ArmyHeading : squad.Heading;
            int leaderSlot = squad.Members.TryGetValue(leader.id, out int value) ? value : 0;
            var occupied = new HashSet<int>();
            foreach (var p in controller.View.Participants)
            {
                var actor = World.world.units.get(p.ActorId);
                if (actor == null) continue;
                Activate(actor);
                if (actor == anchor)
                {
                    controller.SetDomainReady(actor.id, true);
                    controller.SetPlacement(actor.id, CoordinationPlacementOrder.AtTile(target.tile_id, default, 1.25f,
                        holdPosition: squad.Ordered || squad.Routine != 1, suspendWhileInCombat: true));
                }
                else
                {
                    var offset = squad.Ordered && anchor == armyLeader
                        ? VillageRectangularFormationPlanner.ArmyOffset(data, squad, actor, anchor.id)
                        : actor == leader ? Vector2Int.zero : VillageRectangularFormationPlanner.MemberOffset(data.Policy, squad.Members[actor.id], leaderSlot);
                    offset = VillageRectangularFormationPlanner.Orient(offset, heading);
                    offset = SafeOffset(anchor, offset, occupied);
                    controller.SetDomainReady(actor.id, true);
                    controller.SetPlacement(actor.id, CoordinationPlacementOrder.FollowActor(anchor.id, offset, 1f, 1.5f, true));
                }
            }
        }

        /// <summary>障碍旁只尝试十二个邻点，再暂时收拢跟随；空地恢复后重新使用原槽位。</summary>
        /// <param name="anchor">人物跟随的实际领队。</param>
        /// <param name="desired">固定槽位对应的理想偏移。</param>
        /// <param name="occupied">本小队本轮已经采用的格子。</param>
        private static Vector2Int SafeOffset(Actor anchor, Vector2Int desired, HashSet<int> occupied)
        {
            for (int i = 0; i < 13; i++)
            {
                int radius = (i + 3) / 4;
                var delta = i == 0 ? Vector2Int.zero : (i % 4) switch
                { 0 => Vector2Int.up * radius, 1 => Vector2Int.right * radius,
                  2 => Vector2Int.down * radius, _ => Vector2Int.left * radius };
                var offset = desired + delta;
                var tile = World.world.GetTile(anchor.current_tile.x + offset.x, anchor.current_tile.y + offset.y);
                if (!RailTrackRoutePlanner.IsSafeGround(tile) || !tile.isSameIsland(anchor.current_tile) ||
                    tile.building != null && !tile.building.asset.flora || occupied.Contains(tile.tile_id)) continue;
                occupied.Add(tile.tile_id); return offset;
            }
            return Vector2Int.zero;
        }
    }
}

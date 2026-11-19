using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.VillageDuties;
using Cultiway.Core.Coordination;
using Friflo.Engine.ECS.Systems;
using Newtonsoft.Json;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.VillageMilitary;

/// <summary>按村庄维护唯一原版 Army 的下级编制、月份策略和后备候选。</summary>
public static partial class VillageMilitaryService
{
    /// <summary>随原版村庄数据保存的编制记录。</summary>
    public const string SaveKey = "cw.village.duties";
    /// <summary>当前世界已经访问过的村庄。</summary>
    private static readonly Dictionary<long, VillageMilitaryData> states = new();
    /// <summary>数据所属的世界，读档更换世界时重新建立索引。</summary>
    private static MapStats world;
    /// <summary>村庄处理轮转位置。</summary>
    private static int cityCursor;
    /// <summary>服务和群组提供者只注册一次。</summary>
    private static bool initialized;
    /// <summary>每个村庄一轮最多改变三十二人的军籍或槽位。</summary>
    private const int PersonnelBudget = 32;

    /// <summary>注册内容侧更新系统及现有协调与战术群组适配。</summary>
    public static void Init()
    {
        if (initialized) return;
        initialized = true;
        VillageMilitaryActivities.Init();
        ModClass.I.GeneralLogicSystems.Add(new UpdateSystem());
    }

    /// <summary>首次访问村庄时恢复唯一一份保存数据。</summary>
    /// <param name="city">当前世界中的村庄。</param>
    public static VillageMilitaryData Get(City city)
    {
        EnsureWorld();
        if (states.TryGetValue(city.id, out var state))
        {
            SynchronizeKingdomPolicy(city, state);
            return state;
        }
        city.data.get(SaveKey, out string json, null);
        state = json == null ? new VillageMilitaryData() : JsonConvert.DeserializeObject<VillageMilitaryData>(json);
        if (state?.Policy == null || !state.Policy.IsValid() || state.PendingPolicy != null && !state.PendingPolicy.IsValid() ||
            state.Squads == null || state.Battalions == null || state.Reserves == null || state.Occupations == null || state.NextId < 1)
            throw new InvalidDataException("村庄作息或军事编制存档无效。");
        var ids = new HashSet<int>();
        foreach (var battalion in state.Battalions)
            if (battalion == null || battalion.Id <= 0 || battalion.Id >= state.NextId || !ids.Add(battalion.Id))
                throw new InvalidDataException("村庄营编号重复或越界。");
        foreach (var squad in state.Squads)
        {
            if (squad?.Members == null || squad.Id <= 0 || squad.Id >= state.NextId || !ids.Add(squad.Id) ||
                !state.Battalions.Any(b => b.Id == squad.BattalionId) || squad.Members.Values.Distinct().Count() != squad.Members.Count ||
                squad.Members.Values.Any(slot => slot < 0 || slot >= state.Policy.SquadSize))
                throw new InvalidDataException("村庄小队编号、归属或人员槽位无效。");
            foreach (long id in squad.Members.Keys)
            {
                if (state.Memberships.ContainsKey(id)) throw new InvalidDataException("一个人物不能同时属于两个小队。");
                state.Memberships.Add(id, squad);
            }
        }
        states.Add(city.id, state);
        SynchronizeKingdomPolicy(city, state);
        return state;
    }

    /// <summary>保存已经访问过的村庄，不把临时活动和路径写入数据。</summary>
    /// <param name="city">正在写入原版存档的村庄。</param>
    public static void Save(City city)
    {
        if (world == World.world.map_stats && states.TryGetValue(city.id, out var state))
            city.data.set(SaveKey, JsonConvert.SerializeObject(state));
    }

    /// <summary>人物职业、归属或死亡发生变化时只唤醒相关村庄。</summary>
    /// <param name="city">受到人物变化影响的村庄。</param>
    public static void Invalidate(City city)
    {
        if (city != null && states.TryGetValue(city.id, out var state)) state.NextUpdate = 0;
    }

    /// <summary>新军令使旧协调目标失效，不读取尚未加载的村庄数据。</summary>
    /// <param name="city">接到新命令的村庄。</param>
    internal static void InvalidateOrders(City city)
    {
        if (city == null || !states.TryGetValue(city.id, out var state)) return;
        state.OrderRevision++; state.NextUpdate = 0;
    }

    /// <summary>原版完成实际岗位计算后才允许检查原版兵额。</summary>
    /// <param name="city">完成岗位计算的村庄。</param>
    internal static void ArmyQuotaCalculated(City city)
    {
        if (!Config.game_loaded || SmoothLoader.isLoading()) return;
        Get(city).ArmyQuotaReady = true; Invalidate(city);
    }

    /// <summary>清理一个消亡村庄持有的协调席位。</summary>
    /// <param name="city">正在消亡的村庄。</param>
    public static void Remove(City city)
    {
        if (!states.TryGetValue(city.id, out var state)) return;
        foreach (var squad in state.Squads) Cancel(squad);
        VillageMilitaryActivities.ClearOrders(city); states.Remove(city.id);
    }

    /// <summary>查询人物所属的真实下级编制。</summary>
    /// <param name="actor">具有村庄归属的人物。</param>
    public static VillageSquad SquadOf(Actor actor)
    {
        if (actor?.city == null || !KingdomPolicyService.Has(actor.city, KingdomPolicyCard.ArmyOrganization)) return null;
        Get(actor.city).Memberships.TryGetValue(actor.id, out var squad);
        return squad;
    }

    /// <summary>结束旧行动时只查询已经存在的名册，清世界期间不重新读入村庄。</summary>
    /// <param name="cityId">活动所属村庄编号。</param>
    /// <param name="squadId">活动所属小队编号。</param>
    internal static VillageSquad FindCachedSquad(long cityId, int squadId) => states.TryGetValue(cityId, out var state)
        ? state.Squads.FirstOrDefault(s => s.Id == squadId) : null;

    /// <summary>取消一个小队的旧行动后统一清除执行引用。</summary>
    /// <param name="squad">需要释放行动的小队。</param>
    public static void Cancel(VillageSquad squad)
    {
        if (squad.ActivityId > 0) CoordinatedActivityService.Cancel(squad.ActivityId);
        squad.ActivityId = 0; squad.DutyLeader = 0;
    }

    /// <summary>换世界时清空运行期资料，不修改旧世界人物。</summary>
    public static void Clear()
    {
        var previous = states.Values.ToArray();
        states.Clear(); kingdomPolicies.Clear(); world = null; cityCursor = 0;
        VillageMilitaryActivities.Clear();
        foreach (var state in previous) foreach (var squad in state.Squads) Cancel(squad);
    }

    /// <summary>清除失效成员，再填真实空位、补任指挥者和安排当前勤务。</summary>
    /// <param name="city">本轮预算允许处理的村庄。</param>
    public static void Reconcile(City city)
    {
        var state = Get(city);
        state.NextUpdate = World.world.getCurWorldTime() + 1d;
        if (!KingdomPolicyService.Has(city, KingdomPolicyCard.ArmyOrganization))
        {
            if (state.Memberships.Count > 0)
            {
                foreach (var squad in state.Squads) { Cancel(squad); squad.Members.Clear(); squad.Commander = 0; }
                state.Memberships.Clear(); state.FormationDirty = true; state.AppointmentsDirty = true;
                VillageMilitaryActivities.ClearOrders(city);
            }
            VillageOccupationService.Reconcile(city, state); UpdateCivilianDuty(city, state); return;
        }
        int changes = 0;
        bool quotaReady = state.ArmyQuotaReady && city.status.warrior_slots == city.jobs.countCurrentJobs(CitizenJobLibrary.attacker);
        foreach (var actor in city.units)
        {
            if (actor.city != city || !actor.isAlive() || !actor.isWarrior() && !actor.hasArmy() ||
                !VillageDutyService.CanChangeAssignment(actor)) continue;
            if ((actor.isKing() || actor.isCityLeader()) && actor.army == city.army && actor.hasArmy()) actor.removeFromArmy();
            else if (quotaReady && changes < PersonnelBudget && actor.isWarrior() && actor.inOwnCityBorders() && (city.isArmyOverLimit() || !city.hasEnoughFoodForArmy()))
            {
                city.checkIfWarriorStillOk(actor);
                if (!actor.isWarrior()) { state.Reserves.Add(actor.id); changes++; }
            }
        }
        var active = city.units.Where(a => a.city == city && a.isAlive() && a.isWarrior() && !a.isKing() && !a.isCityLeader()).ToList();
        if (state.PendingPolicy != null && active.All(VillageDutyService.CanChangeAssignment))
        {
            state.Policy = state.PendingPolicy; state.PendingPolicy = null; state.Reorganize = true;
        }
        // 名册只保留本村仍有真实军籍的人，保留空槽以供后续补员。
        var activeIds = new HashSet<long>(active.Select(a => a.id));
        foreach (var squad in state.Squads)
        {
            foreach (long id in squad.Members.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            { squad.Members.Remove(id); state.Memberships.Remove(id); state.AppointmentsDirty = true; state.FormationDirty = true; }
        }
        state.Reserves.RemoveWhere(id => World.world.units.get(id) is not { } a || !a.isAlive() || a.city != city || a.isWarrior());
        if (state.Reorganize && active.All(VillageDutyService.CanChangeAssignment)) RebuildLayout(state);
        if (active.Count > state.Policy.Capacity || active.Any(a => !state.Memberships.ContainsKey(a.id)))
            active.Sort((a, b) =>
            {
                int retained = state.Memberships.ContainsKey(b.id).CompareTo(state.Memberships.ContainsKey(a.id));
                return retained != 0 ? retained : MilitaryMeritService.Compare(a, b);
            });
        int count = 0;
        foreach (var actor in active)
        {
            if (++count > state.Policy.Capacity)
            {
                if (changes < PersonnelBudget && VillageDutyService.CanChangeAssignment(actor))
                { ReleaseToReserve(city, state, actor); changes++; }
                continue;
            }
            if (changes >= PersonnelBudget || state.Memberships.ContainsKey(actor.id) && actor.army != null && actor.army == city.army) continue;
            if (actor.army != city.army || actor.army == null)
            {
                if (!VillageDutyService.CanChangeAssignment(actor)) continue;
                if (actor.army != null) actor.removeFromArmy();
                if (!city.hasArmy()) World.world.armies.newArmy(actor, city);
                else actor.setArmy(city.army);
                changes++; state.AppointmentsDirty = true; state.FormationDirty = true;
            }
            if (actor.army != city.army || state.Memberships.ContainsKey(actor.id)) continue;
            if (changes >= PersonnelBudget) continue;
            var target = FindVacancy(state);
            if (target == null) continue;
            int slot = 0;
            while (target.Members.ContainsValue(slot)) slot++;
            target.Members.Add(actor.id, slot); state.Memberships.Add(actor.id, target);
            state.Reserves.Remove(actor.id); state.AppointmentsDirty = true; state.FormationDirty = true;
            changes++;
        }
        if (changes < PersonnelBudget) TryRecruit(city, state);
        if (state.AppointmentsDirty) ElectCommanders(city, state, false);
        VillageOccupationService.Reconcile(city, state);
        UpdateCivilianDuty(city, state);
        VillageMilitaryActivities.Update(city, state);
    }

    /// <summary>正式重编保留命名记录，清理旧槽位并在本轮重新填入实际兵员。</summary>
    /// <param name="state">已经提交新编制设置的村庄记录。</param>
    private static void RebuildLayout(VillageMilitaryData state)
    {
        foreach (var squad in state.Squads) { Cancel(squad); squad.Members.Clear(); squad.Commander = 0; }
        state.Memberships.Clear();
        state.Battalions = state.Battalions.Take(state.Policy.BattalionsPerArmy).ToList();
        state.Squads = state.Squads.Take((int)((long)state.Policy.BattalionsPerArmy * state.Policy.SquadsPerBattalion)).ToList();
        for (int i = 0; i < state.Squads.Count; i++)
        {
            int battalionIndex = i / state.Policy.SquadsPerBattalion;
            while (state.Battalions.Count <= battalionIndex) state.Battalions.Add(new VillageBattalion { Id = state.NextId++ });
            state.Squads[i].BattalionId = state.Battalions[battalionIndex].Id;
        }
        state.AppointmentsDirty = true; state.FormationDirty = true; state.Reorganize = false;
    }

    /// <summary>只按实际需求创建一个下级单位，不预建全部空营空队。</summary>
    /// <param name="state">需要补入一名成员的村庄记录。</param>
    private static VillageSquad FindVacancy(VillageMilitaryData state)
    {
        var existing = state.Squads.FirstOrDefault(s => s.Members.Count < state.Policy.SquadSize);
        if (existing != null) return existing;
        var battalion = state.Battalions.FirstOrDefault(b => state.Squads.Count(s => s.BattalionId == b.Id) < state.Policy.SquadsPerBattalion);
        if (battalion == null)
        {
            if (state.Battalions.Count >= state.Policy.BattalionsPerArmy) return null;
            battalion = new VillageBattalion { Id = state.NextId++ }; state.Battalions.Add(battalion);
        }
        var squad = new VillageSquad { Id = state.NextId++, BattalionId = battalion.Id };
        state.Squads.Add(squad);
        return squad;
    }

    /// <summary>运行期分批处理村庄；不让打开窗口或暂停世界推进人员调度。</summary>
    private sealed class UpdateSystem : BaseSystem, IWorldStateClearable
    {
        /// <inheritdoc />
        void IWorldStateClearable.ClearWorldState() => Clear();
        /// <inheritdoc />
        protected override void OnUpdateGroup()
        {
            if (!Config.game_loaded || World.world == null || SmoothLoader.isLoading() || World.world.isPaused()) return;
            var cities = World.world.cities.list;
            int budget = Math.Min(2, cities.Count);
            for (int i = 0; i < budget; i++)
            {
                if (cityCursor >= cities.Count) cityCursor = 0;
                var city = cities[cityCursor++];
                if (city.isAlive() && World.world.getCurWorldTime() >= Get(city).NextUpdate) Reconcile(city);
            }
        }
    }
}

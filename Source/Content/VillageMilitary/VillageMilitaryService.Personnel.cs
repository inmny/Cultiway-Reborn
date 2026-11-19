using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.KingdomPolicies;

namespace Cultiway.Content.VillageMilitary;

/// <summary>原版征退役、人事任命和村庄例行岗位的衔接。</summary>
public static partial class VillageMilitaryService
{
    /// <summary>原版征兵只能录用当前确有空缺并选中的人物。</summary>
    /// <param name="city">提出征召的村庄。</param>
    /// <param name="actor">已经通过原版资格检查的候选人。</param>
    public static bool CanRecruit(City city, Actor actor)
    {
        if (!Config.game_loaded || SmoothLoader.isLoading()) return true;
        if (!actor.isAdult()) return false;
        if (!KingdomPolicyService.Has(city, KingdomPolicyCard.ArmyOrganization)) return true;
        var state = Get(city);
        return actor.id == state.RecruitCandidate && city.status.warriors_current < state.Policy.Capacity &&
               (VillageDutyService.IsWorkMonth(city) || VillageDutyService.HasEmergency(city));
    }

    /// <summary>释放超额兵员时使用原版职业与军队更新流程。</summary>
    /// <param name="city">士兵所属的村庄。</param>
    /// <param name="state">保存后备标记和槽位的记录。</param>
    /// <param name="actor">可以安全退役的人物。</param>
    private static void ReleaseToReserve(City city, VillageMilitaryData state, Actor actor)
    {
        if (state.Memberships.TryGetValue(actor.id, out var squad))
        {
            squad.Members.Remove(actor.id); state.Memberships.Remove(actor.id);
            if (squad.Commander == actor.id) squad.Commander = 0;
        }
        actor.stopBeingWarrior();
        state.Reserves.Add(actor.id); state.AppointmentsDirty = true; state.FormationDirty = true;
        Invalidate(city);
    }

    /// <summary>有真实空缺时优先征召后备，每轮仅录用一人并遵守原版征兵间隔。</summary>
    /// <param name="city">有编制空缺的村庄。</param>
    /// <param name="state">本村编制与后备名单。</param>
    private static void TryRecruit(City city, VillageMilitaryData state)
    {
        if (city.status.warriors_current >= state.Policy.Capacity || city.isArmyFull() || city._timer_warrior > 0 ||
            !WorldLawLibrary.world_law_civ_army.isEnabled() || !city.hasEnoughFoodForArmy() ||
            city.getResourcesAmount("gold") <= 10 || city.isGettingCaptured() ||
            !VillageDutyService.IsWorkMonth(city) && !VillageDutyService.HasEmergency(city)) return;
        var candidates = city.units.Where(a => VillageDutyService.IsResident(a) && !a.isWarrior() &&
            !a.isKing() && !a.isCityLeader() && a.isAdult() && VillageDutyService.CanChangeAssignment(a)).ToList();
        candidates.Sort((a, b) =>
        {
            int reserve = state.Reserves.Contains(b.id).CompareTo(state.Reserves.Contains(a.id));
            return reserve != 0 ? reserve : MilitaryMeritService.Compare(a, b);
        });
        if (candidates.Count == 0) return;
        int start = state.RecruitCursor % candidates.Count;
        for (int i = 0; i < Math.Min(32, candidates.Count); i++)
        {
            var actor = candidates[(start + i) % candidates.Count];
            state.RecruitCursor = start + i + 1;
            state.RecruitCandidate = actor.id;
            bool recruited;
            try { recruited = city.tryToMakeWarrior(actor); }
            finally { state.RecruitCandidate = 0; }
            if (!recruited) continue;
            state.Reserves.Remove(actor.id);
            if (!city.hasArmy()) World.world.armies.newArmy(actor, city);
            else actor.setArmy(city.army);
            var squad = FindVacancy(state);
            if (squad != null)
            {
                int slot = 0; while (squad.Members.ContainsValue(slot)) slot++;
                squad.Members[actor.id] = slot; state.Memberships[actor.id] = squad;
            }
            state.RecruitCursor = 0; state.AppointmentsDirty = true; state.FormationDirty = true;
            return;
        }
    }

    /// <summary>取本单位现役且可以正式任职的军功排序结果。</summary>
    /// <param name="ids">本次任命范围内的人物编号。</param>
    /// <param name="city">任命所属的村庄。</param>
    private static List<Actor> Candidates(IEnumerable<long> ids, City city)
    {
        var result = ids.Select(id => World.world.units.get(id)).Where(a => a != null && a.isAlive() &&
            a.city == city && a.army == city.army && a.isWarrior() && !a.isKing() && !a.isCityLeader()).ToList();
        result.Sort(MilitaryMeritService.Compare);
        return result;
    }

    /// <summary>空缺时选任，玩家明确要求重新评定时才更换仍然合格的现任。</summary>
    /// <param name="city">实际 Army 所属村庄。</param>
    /// <param name="state">当前三层编制记录。</param>
    /// <param name="replace">是否明确要求重新评定仍有效的现任。</param>
    public static void ElectCommanders(City city, VillageMilitaryData state, bool replace)
    {
        if (!city.hasArmy()) return;
        bool changed = false;
        foreach (var squad in state.Squads)
        {
            var candidates = Candidates(squad.Members.Keys, city);
            if (!replace && candidates.Any(a => a.id == squad.Commander)) continue;
            long leader = candidates.FirstOrDefault()?.id ?? 0;
            if (leader != squad.Commander) { squad.Commander = leader; Cancel(squad); changed = true; }
        }
        foreach (var battalion in state.Battalions)
        {
            var candidates = Candidates(state.Squads.Where(s => s.BattalionId == battalion.Id).Select(s => s.Commander), city);
            if (!replace && candidates.Any(a => a.id == battalion.Commander)) continue;
            long leader = candidates.FirstOrDefault()?.id ?? 0;
            if (battalion.Commander != leader) { battalion.Commander = leader; changed = true; }
        }
        var armyCandidates = Candidates(state.Battalions.Select(b => b.Commander), city);
        var captain = city.army.getCaptain();
        if (replace || captain == null || !armyCandidates.Any(a => a == captain))
        {
            var leader = armyCandidates.FirstOrDefault();
            if (leader != captain) { city.army.setCaptain(leader); changed = true; }
        }
        if (changed) { state.FormationDirty = true; foreach (var squad in state.Squads) Cancel(squad); }
        state.AppointmentsDirty = false;
    }

    /// <summary>原版检查统领时优先使用已建立的下级指挥链。</summary>
    /// <param name="army">原版正在检查的军队。</param>
    public static bool EnsureCaptain(Army army)
    {
        var city = army.getCity();
        if (city == null || !Config.game_loaded || SmoothLoader.isLoading()) return false;
        if (!KingdomPolicyService.Has(city, KingdomPolicyCard.ArmyOrganization)) return false;
        var state = Get(city);
        if (state.Memberships.Count == 0) return false;
        var captain = army.getCaptain();
        if (state.AppointmentsDirty || captain == null || !captain.isAlive() || captain.army != army)
            ElectCommanders(city, state, false);
        return true;
    }

    /// <summary>预览与正式选任相同的三层军功排序，不改变任何现任或人员位置。</summary>
    /// <param name="city">准备重新评定的村庄。</param>
    public static List<(int layer, long before, long after)> PreviewAppointments(City city)
    {
        var state = Get(city);
        var changes = new List<(int, long, long)>();
        var squadLeaders = new Dictionary<int, long>();
        foreach (var squad in state.Squads)
        {
            long leader = Candidates(squad.Members.Keys, city).FirstOrDefault()?.id ?? 0;
            squadLeaders[squad.Id] = leader;
            if (leader != squad.Commander) changes.Add((0, squad.Commander, leader));
        }
        var battalionLeaders = new List<long>();
        foreach (var battalion in state.Battalions)
        {
            long leader = Candidates(state.Squads.Where(s => s.BattalionId == battalion.Id).Select(s => squadLeaders[s.Id]), city).FirstOrDefault()?.id ?? 0;
            battalionLeaders.Add(leader);
            if (leader != battalion.Commander) changes.Add((1, battalion.Commander, leader));
        }
        long captain = Candidates(battalionLeaders, city).FirstOrDefault()?.id ?? 0;
        long oldCaptain = city.army?.getCaptain()?.id ?? 0;
        if (captain != oldCaptain) changes.Add((2, oldCaptain, captain));
        return changes;
    }

    /// <summary>工作状态变化时停止可安全结束的普通生产岗位，保留已有工程进度。</summary>
    /// <param name="city">需要同步作息的村庄。</param>
    /// <param name="state">本村已应用的月份设置。</param>
    private static void UpdateCivilianDuty(City city, VillageMilitaryData state)
    {
        // 休息月和未达到生产年龄的居民都在安全动作结束后离岗。
        foreach (var actor in city.units)
        {
            bool nativeWork = actor.citizen_job != null && actor.citizen_job != CitizenJobLibrary.attacker;
            bool projectWork = actor.ai.job?.id == ActorJobs.CollectiveProject.id;
            if (!nativeWork && !projectWork ||
                !VillageDutyService.IsResident(actor) || VillageDutyService.Classify(actor) != VillageResidentKind.Worker ||
                VillageDutyService.CanTakeCivilianJob(actor) || FamilyFarming.FamilyFarmService.CanSeasonalWork(actor) ||
                !VillageDutyService.CanChangeAssignment(actor) || actor.timer_action > 0) continue;
            actor.cancelAllBeh();
        }
    }

    /// <summary>编辑已有营或小队名称，空文本恢复按层级名与编号显示。</summary>
    /// <param name="city">下级单位所属村庄。</param>
    /// <param name="unitId">本村内稳定的营或小队编号。</param>
    /// <param name="name">新名称，空文本恢复默认。</param>
    public static bool Rename(City city, int unitId, string name)
    {
        if (name == null || name.Length > 40 || name.Contains("\n") || name.Contains("\r")) return false;
        var state = Get(city);
        var squad = state.Squads.FirstOrDefault(s => s.Id == unitId);
        var battalion = state.Battalions.FirstOrDefault(b => b.Id == unitId);
        if (squad != null) squad.Name = name.Trim();
        else if (battalion != null) battalion.Name = name.Trim();
        else return false;
        Save(city);
        return true;
    }

    /// <summary>沿用原版军队自定义名称标记，名称随原版 ArmyData 保存。</summary>
    /// <param name="city">唯一军队所属的村庄。</param>
    /// <param name="name">玩家输入的军队实例名称。</param>
    public static bool RenameArmy(City city, string name)
    {
        if (!city.hasArmy() || string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Contains("\n") || name.Contains("\r")) return false;
        city.army.data.custom_name = true; city.army.setName(name.Trim()); return true;
    }
}

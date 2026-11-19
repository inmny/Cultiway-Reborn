using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.VillageDuties;
using Cultiway.Content.VillageMilitary;
using Cultiway.Content.KingdomPolicies;
using Cultiway.Content.UI.KingdomPolicies;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.VillageDuties;

/// <summary>以池化行和分页展示人员、后备以及真实 Army 的下级结构。</summary>
public sealed partial class WindowVillageDuties
{
    /// <summary>每页最多生成四十个人员条目。</summary>
    private const int PeoplePerPage = 40;

    /// <summary>建立姓名搜索、直接筛选与人员分页。</summary>
    private void BuildPeople()
    {
        var actions = UiLayout.Create(pages[3].transform, "Filters", true, 500, 24, 4);
        search = UiSearchField.Create(actions.transform, "Search", "", T("Search"), 244, 24).Input;
        search.onValueChanged.AddListener(_ => { peoplePage = 0; if (!binding) Refresh(); });
        filter = Button(actions.transform, "Filter", 124, ShowFilter);
        sorting = Button(actions.transform, "Sort", 124, ShowSorting);
        var paging = UiLayout.Create(pages[3].transform, "Paging", true, 500, 24, 4);
        previous = Button(paging.transform, "Previous", 100, () => { peoplePage--; Refresh(); scroll.verticalNormalizedPosition = 1; });
        pageText = UiElements.CreateText(paging.transform, "Page", "", 292, 24, 7);
        next = Button(paging.transform, "Next", 100, () => { peoplePage++; Refresh(); scroll.verticalNormalizedPosition = 1; });
        peopleList = UiLayout.Create(pages[3].transform, "Residents", false, 500, 0, 4).transform;
        peopleRows = new MonoObjPool<VillageInfoRow>(VillageInfoRow.Prefab, peopleList, deactive_action: r => r.Unbind());
    }

    /// <summary>分类统计与军事状态分别显示，特殊军职者只在居民分类中计一次。</summary>
    /// <param name="city">当前村庄。</param>
    /// <param name="data">实际编制与后备记录。</param>
    private void RefreshOverview(City city, VillageMilitaryData data)
    {
        int[] counts = new int[4]; int active = 0, specialMilitary = 0;
        foreach (var actor in city.units)
        {
            if (!VillageDutyService.IsResident(actor)) continue;
            var kind = VillageDutyService.Classify(actor); counts[(int)kind]++;
            if (actor.isWarrior() && !actor.isKing() && !actor.isCityLeader())
            { active++; if (kind == VillageResidentKind.Special) specialMilitary++; }
        }
        bool organized = KingdomPolicyService.Has(city, KingdomPolicyCard.ArmyOrganization);
        overview.text = F("CurrentMonth", Date.getCurrentMonth(), T(VillageDutyService.IsWorkMonth(city) ? "Work" : "Rest")) + "\n\n" +
            F("ClassificationSummary", counts[0], counts[1], counts[2], counts[3]) + "\n\n" +
            F("MilitarySummary", active, specialMilitary, data.Policy.Capacity, data.Reserves.Count) + "\n" +
            (organized ? F("StructureSummary", data.Battalions.Count, Label(data.OrganizationNames.BattalionName, "Battalion"), data.Squads.Count, Label(data.OrganizationNames.SquadName, "Squad")) : WindowKingdomPolicies.T("NativeArmy")) + "\n" +
            T(VillageMilitaryActivities.IsOrdered(city) ? "OrdersActive" : "NoOrders") + "\n" +
            (!organized ? "" : data.PendingPolicy != null ? T("PendingReorganization") : data.Memberships.Count != active ? F("OrganizationProgress", data.Memberships.Count, active) : "");
    }

    /// <summary>实例名为空时用当前层级名和顺序号显示，内部编号不进入界面。</summary>
    /// <param name="data">提供层级称呼与顺序的编制。</param>
    /// <param name="squad">需要显示的小队。</param>
    internal static string SquadName(VillageMilitaryData data, VillageSquad squad) => string.IsNullOrEmpty(squad.Name)
        ? Label(data.OrganizationNames.SquadName, "Squad") + " " + (data.Squads.IndexOf(squad) + 1) : squad.Name;

    /// <summary>营的默认显示名称。</summary>
    /// <param name="data">提供层级称呼与顺序的编制。</param>
    /// <param name="battalion">需要显示的营。</param>
    private static string BattalionName(VillageMilitaryData data, VillageBattalion battalion) => string.IsNullOrEmpty(battalion.Name)
        ? Label(data.OrganizationNames.BattalionName, "Battalion") + " " + (data.Battalions.IndexOf(battalion) + 1) : battalion.Name;

    /// <summary>失效人物用明确空缺文字显示。</summary>
    /// <param name="id">正式指挥者编号。</param>
    private static string ActorName(long id) => World.world.units.get(id) is { } actor && actor.isAlive() ? actor.getName() : T("Vacant");

    /// <summary>刚生成或空闲人物没有原版任务文字时显示明确的自由活动。</summary>
    /// <param name="actor">需要展示当前任务的人物。</param>
    internal static string TaskText(Actor actor)
    {
        string text = actor.getTaskText();
        return string.IsNullOrEmpty(text) || text == "???" ? T("Free") : text;
    }

    /// <summary>指挥者详情同时解释正式职位、军功与日常代理。</summary>
    /// <param name="title">本层采用的自定义职位称呼。</param>
    /// <param name="id">正式任职者编号。</param>
    private static string CommanderText(string title, long id)
    {
        var actor = World.world.units.get(id);
        return F("Commander", title, ActorName(id), actor == null ? 0 : MilitaryMeritService.Get(actor));
    }

    /// <summary>只展开用户选中的营，定时刷新复用现有条目。</summary>
    /// <param name="city">当前村庄和唯一原版军队。</param>
    /// <param name="data">当前实际营队名册。</param>
    private void RefreshOrganization(City city, VillageMilitaryData data)
    {
        armyRows.ResetToStart(); int count = 0;
        organizationScope.text = F("KingdomOrganization", city.kingdom.name, city.kingdom.cities.Count);
        var names = data.OrganizationNames;
        var army = city.army;
        AddArmyRow(Label(names.ArmyName, "Army") + " · " + (army?.name ?? T("NoArmy")), CommanderText(Label(names.ArmyLeaderTitle, "ArmyLeader"), army?.getCaptain()?.id ?? 0),
            T("Formation"), () => { previewLevel = 2; previewUnit = 0; Refresh(); }); count++;
        foreach (var battalion in data.Battalions)
        {
            var squads = data.Squads.Where(s => s.BattalionId == battalion.Id).ToArray();
            int members = squads.Sum(s => s.Members.Count);
            AddArmyRow(BattalionName(data, battalion) + " · " + F("Members", members, (long)data.Policy.SquadSize * data.Policy.SquadsPerBattalion),
                CommanderText(Label(names.BattalionLeaderTitle, "BattalionLeader"), battalion.Commander), T(expanded.Contains(battalion.Id) ? "Collapse" : "Expand"),
                () => { if (!expanded.Add(battalion.Id)) expanded.Remove(battalion.Id); previewLevel = 1; previewUnit = battalion.Id; Refresh(); },
                () => RenameUnit(battalion.Id, battalion.Name)); count++;
            if (!expanded.Contains(battalion.Id)) continue;
            foreach (var squad in squads)
            {
                string state = squad.ActivityId > 0 ? T("Task." + (squad.Ordered ? "Orders" : squad.Routine.ToString())) : T("Free");
                if (squad.DutyLeader > 0 && squad.DutyLeader != squad.Commander) state += " · " + F("Acting", ActorName(squad.DutyLeader));
                AddArmyRow("    " + SquadName(data, squad) + " · " + F("Members", squad.Members.Count, data.Policy.SquadSize),
                    CommanderText(Label(names.SquadLeaderTitle, "SquadLeader"), squad.Commander) + "\n" + state,
                    T("Formation"), () => { previewLevel = 0; previewUnit = squad.Id; Refresh(); }, () => RenameUnit(squad.Id, squad.Name)); count++;
            }
        }
        armyRows.ClearUnsed();
        UiLayout.SetSize(armyList, 500, count * 56);
        UiLayout.SetSize(pages[2].transform, 500, 450 + count * 56);
        armyName.interactable = army != null;
    }

    /// <summary>一次绑定编制行的动作。</summary>
    /// <param name="title">单位名称和人数。</param>
    /// <param name="body">指挥者与任务说明。</param>
    /// <param name="firstLabel">主要按钮文字。</param>
    /// <param name="onFirst">展开单位或预览阵列的操作。</param>
    /// <param name="rename">可选的改名操作。</param>
    private void AddArmyRow(string title, string body, string firstLabel, Action onFirst, Action rename = null)
        => armyRows.GetNext().Bind(title, body, firstLabel, onFirst, T("Rename"), rename);

    /// <summary>打开直接选择菜单而非循环切换筛选值。</summary>
    private void ShowFilter()
    {
        string[] keys = { "All", "Kind.Soldier", "Kind.Worker", "Kind.Manager", "Kind.Special", "Reserve", "ActiveMilitary" };
        var choices = new List<UiOptionMenuOption>();
        for (int i = 0; i < keys.Length; i++)
        {
            int index = i;
            choices.Add(new UiOptionMenuOption { Label = T(keys[i]), Selected = filterIndex == i,
                Select = () => { filterIndex = index; peoplePage = 0; Refresh(); } });
        }
        options.Show(T("Filter"), choices);
    }

    /// <summary>姓名与军功采用明确选项。</summary>
    private void ShowSorting()
    {
        var choices = new List<UiOptionMenuOption>();
        string[] keys = { "SortMerit", "SortName" };
        for (int i = 0; i < keys.Length; i++)
        {
            int index = i;
            choices.Add(new UiOptionMenuOption { Label = T(keys[i]), Selected = sortIndex == i,
                Select = () => { sortIndex = index; peoplePage = 0; Refresh(); } });
        }
        options.Show(T("Sort"), choices);
    }

    /// <summary>分页只创建可见人数，列表同时显示真实军籍与后备标记。</summary>
    /// <param name="city">需要展示的村庄。</param>
    /// <param name="data">用于解释军籍的记录。</param>
    private void RefreshPeople(City city, VillageMilitaryData data)
    {
        var residents = new List<(Actor actor, VillageResidentKind kind)>();
        foreach (var actor in city.units)
        {
            if (!VillageDutyService.IsResident(actor) || search.text.Length > 0 && actor.getName().IndexOf(search.text, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var kind = VillageDutyService.Classify(actor);
            if (filterIndex is >= 1 and <= 4 && (int)kind != filterIndex - 1 ||
                filterIndex == 5 && !data.Reserves.Contains(actor.id) || filterIndex == 6 && !actor.isWarrior()) continue;
            residents.Add((actor, kind));
        }
        residents.Sort((a, b) => sortIndex == 0 ? MilitaryMeritService.Compare(a.actor, b.actor) : string.Compare(a.actor.getName(), b.actor.getName(), StringComparison.CurrentCulture));
        int totalPages = Math.Max(1, (residents.Count + PeoplePerPage - 1) / PeoplePerPage);
        peoplePage = Math.Max(0, Math.Min(peoplePage, totalPages - 1));
        previous.interactable = peoplePage > 0; next.interactable = peoplePage + 1 < totalPages;
        pageText.text = F("Pages", peoplePage + 1, totalPages, residents.Count);
        string[] filters = { "All", "Kind.Soldier", "Kind.Worker", "Kind.Manager", "Kind.Special", "Reserve", "ActiveMilitary" };
        UiElements.SetButtonLabel(filter, T(filters[filterIndex])); UiElements.SetButtonLabel(sorting, T(sortIndex == 0 ? "SortMerit" : "SortName"));
        peopleRows.ResetToStart(); int count = 0;
        foreach (var person in residents.Skip(peoplePage * PeoplePerPage).Take(PeoplePerPage))
        {
            var actor = person.actor;
            string military = data.Memberships.TryGetValue(actor.id, out var squad) ? SquadName(data, squad) :
                data.Reserves.Contains(actor.id) ? T("ReserveFree") : T("None");
            string detail = T("Kind." + person.kind) + " · " + F("MeritValue", MilitaryMeritService.Get(actor)) + " · " + military;
            if (data.Occupations.TryGetValue(actor.id, out string occupation)) detail += " · " + OccupationName(occupation);
            peopleRows.GetNext().Bind(actor.getName(), detail + "\n" + TaskText(actor), T("Inspect"), () => Inspect(actor),
                T("Locate"), () => Locate(actor)); count++;
        }
        if (count == 0) { peopleRows.GetNext().Bind(T("NoPeople"), T("NoPeopleHint")); count = 1; }
        peopleRows.ClearUnsed();
        UiLayout.SetSize(peopleList, 500, count * 56);
        UiLayout.SetSize(pages[3].transform, 500, 60 + count * 56);
    }

    /// <summary>沿用已有生物详情窗口，不复制人物面板。</summary>
    /// <param name="actor">需要检视的真实人物。</param>
    private static void Inspect(Actor actor)
    { if (actor.isAlive()) { SelectedUnit.clear(); SelectedUnit.select(actor); WindowNewCreatureInfo.Show(); } }

    /// <summary>关闭弹窗并使用原版镜头定位。</summary>
    /// <param name="actor">需要定位的真实人物。</param>
    private void Locate(Actor actor)
    { if (actor.isAlive()) { GetComponent<ScrollWindow>().clickHide(); MoveCamera.instance.focusOn(actor.current_tile.posV3); } }
}

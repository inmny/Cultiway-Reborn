using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.FamilyFarming;
using Cultiway.Content.VillageDuties;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.VillageDuties;

/// <summary>在现有村庄窗口内配置家庭田块，并查看真实劳动、原粮和风车状态。</summary>
public sealed partial class WindowVillageDuties
{
    /// <summary>起步宽、高及家庭面积上限的编辑草稿。</summary>
    private readonly InputField[] farmFields = new InputField[3];
    /// <summary>本村农业与加工概况。</summary>
    private Text farmSummary;
    /// <summary>当前家庭筛选和分页。</summary>
    private Text farmPageText;
    /// <summary>上一页按钮。</summary>
    private Button farmPrevious;
    /// <summary>下一页按钮。</summary>
    private Button farmNext;
    /// <summary>农户列表容器。</summary>
    private Transform farmList;
    /// <summary>复用已有条目外观和固定操作区域。</summary>
    private MonoObjPool<VillageInfoRow> farmRows;
    /// <summary>农户列表页码。</summary>
    private int farmPage;
    /// <summary>从原版家庭进入时的家庭筛选，最小值表示全部。</summary>
    private long farmFamily = long.MinValue;

    /// <summary>原版家庭详情跳转到本村的对应农户。</summary>
    /// <param name="city">家庭成员实际居住的村庄。</param>
    /// <param name="family">需要查看的原版家庭。</param>
    public static void OpenFamilyFarms(City city, Family family)
    {
        Open(city, 5);
        if (instance == null) return;
        instance.farmFamily = family.id; instance.farmPage = 0; instance.Refresh();
    }

    /// <summary>使用现有滚动页和池化列表，不创建另一套窗口。</summary>
    private void BuildFarms()
    {
        var parent = pages[5].transform;
        Text(parent, "Farm.Hint", 24);
        Text(parent, "Farm.CalendarHint", 24);
        Text(parent, "Farm.FertilizerHint", 24);
        var fieldsRow = UiLayout.Create(parent, "FarmSettings", true, 500, 28, UiTheme.Current.Metrics.SpacingSm);
        string[] keys = { "Farm.Width", "Farm.Height", "Farm.MaxArea" };
        for (int i = 0; i < keys.Length; i++)
        {
            var label = UiElements.CreateText(fieldsRow.transform, "Label" + i, T(keys[i]), i == 2 ? 92 : 72, 28, 7, TextAnchor.MiddleLeft);
            translated.Add((label, keys[i])); farmFields[i] = Input(fieldsRow.transform, "Count" + i, 72, keys[i], true);
        }
        farmSummary = UiElements.CreateText(parent, "FarmSummary", "", 500, 32, 7, TextAnchor.UpperLeft);
        var actions = UiLayout.Create(parent, "FarmPages", true, 500, 24, UiTheme.Current.Metrics.SpacingSm);
        Button(actions.transform, "Farm.All", 108, () => { farmFamily = long.MinValue; farmPage = 0; Refresh(); });
        farmPrevious = Button(actions.transform, "Previous", 80, () => { farmPage--; Refresh(); scroll.verticalNormalizedPosition = 1; });
        farmPageText = UiElements.CreateText(actions.transform, "Pages", "", 216, 24, 7);
        farmNext = Button(actions.transform, "Next", 80, () => { farmPage++; Refresh(); scroll.verticalNormalizedPosition = 1; });
        farmList = UiLayout.Create(parent, "Households", false, 500, 0, UiTheme.Current.Metrics.SpacingSm).transform;
        farmRows = new MonoObjPool<VillageInfoRow>(VillageInfoRow.Prefab, farmList, deactive_action: r => r.Unbind());
    }

    /// <summary>输入校验只发生在玩家编辑边界，失败时不修改已有田块。</summary>
    /// <param name="width">解析后的起步宽度。</param>
    /// <param name="height">解析后的起步高度。</param>
    /// <param name="maxArea">解析后的每户面积上限。</param>
    private bool ReadFarmDraft(out int width, out int height, out int maxArea)
    {
        width = height = maxArea = 0;
        return int.TryParse(farmFields[0].text, out width) && int.TryParse(farmFields[1].text, out height) &&
            int.TryParse(farmFields[2].text, out maxArea) && FamilyFarmData.ValidSettings(width, height, maxArea);
    }

    /// <summary>按实际田块和参与者展示农户；不把家庭总人口当作农民数。</summary>
    /// <param name="city">窗口当前绑定的村庄。</param>
    private void RefreshFarms(City city)
    {
        // 先统计全村生产状态，再对农户分页，最后绑定可见的池化条目。
        var state = FamilyFarmService.State(city); var data = state.Data;
        int carried = city.units.Sum(a => a.inventory.getResource(FamilyFarmService.RawGrainId));
        farmSummary.text = F("Farm.Summary", state.Farmers.Count, state.Farmers.Sum(p => p.Value.Count),
            state.Land.Keys.Count(c => FamilyFarmService.CanFarm(state, FamilyFarmService.Tile(c))), data.Retiring.Count) + "\n" +
            F("Farm.Stocks", city.getResourcesAmount(FamilyFarmService.RawGrainId), carried,
                city.getTotalFood(), city.countBuildingsType("type_windmill"), FamilyFarmService.MillingWorkers(city));
        var owners = data.Plots.Select(p => p.Owner).Concat(state.Farmers.Keys).Distinct()
            .Where(o => farmFamily == long.MinValue || o == farmFamily).OrderBy(o => o).ToArray();
        int totalPages = Math.Max(1, (owners.Length + PeoplePerPage - 1) / PeoplePerPage);
        farmPage = Math.Max(0, Math.Min(farmPage, totalPages - 1));
        farmPrevious.interactable = farmPage > 0; farmNext.interactable = farmPage + 1 < totalPages;
        farmPageText.text = F("Pages", farmPage + 1, totalPages, owners.Length);
        farmRows.ResetToStart(); int count = 0;
        foreach (long owner in owners.Skip(farmPage * PeoplePerPage).Take(PeoplePerPage))
        {
            var plots = data.Plots.Where(p => p.Owner == owner).ToArray();
            state.Farmers.TryGetValue(owner, out var farmers);
            var family = owner > 0 ? World.world.families.get(owner) : null;
            var actor = owner < 0 ? World.world.units.get(-owner) : null;
            string name = owner == 0 ? T("Farm.Unowned") : family?.name ?? actor?.getName() ?? T("Farm.Household");
            string reason = farmers == null || farmers.Count == 0 ? "NoFarmers" :
                !farmers.Any(a => VillageDutyService.CanTakeCivilianJob(a) || FamilyFarmService.CanSeasonalWork(a)) ? "Rest" :
                state.Reasons.TryGetValue(owner, out string waiting) ? waiting : "Working";
            int usable = plots.SelectMany(p => p.Cells()).Count(c => FamilyFarmService.CanFarm(state, FamilyFarmService.Tile(c)));
            int cultivated = plots.SelectMany(p => p.Cells()).Count(c => FamilyFarmService.Tile(c).Type.farm_field);
            string dimensions = string.Join(" / ", plots.Take(4).Select(p => p.Width + "×" + p.Height));
            string detail = F("Farm.Row", farmers?.Count ?? 0, usable, cultivated) + "\n" + dimensions + " · " + T("Farm.State." + reason);
            if (farmers != null)
            {
                var actions = farmers.Select(a => FamilyFarmService.CurrentAction(a.id)).Where(a => a != "Idle")
                    .GroupBy(a => a).Take(2).Select(g => T("Farm.Action." + g.Key) + " " + g.Count());
                detail += " · " + string.Join(" / ", actions);
            }
            var target = plots.FirstOrDefault();
            farmRows.GetNext().Bind(name, detail, T("Locate"), target == null ? null : () => LocateFarm(target),
                T("Farm.Family"), family == null ? null : () => InspectFamily(family)); count++;
        }
        if (count == 0) { farmRows.GetNext().Bind(T("Farm.Empty"), T("Farm.EmptyHint")); count = 1; }
        farmRows.ClearUnsed(); UiLayout.SetSize(farmList, 500, count * 56);
        UiLayout.SetSize(pages[5].transform, 500, 176 + UiTheme.Current.Metrics.SpacingSm + count * 56);
    }

    /// <summary>使用原版镜头定位田块中心。</summary>
    /// <param name="plot">当前田块。</param>
    private void LocateFarm(FamilyFarmPlot plot)
    {
        var tile = World.world.GetTile(plot.X + plot.Width / 2, plot.Y + plot.Height / 2);
        GetComponent<ScrollWindow>().clickHide(); MoveCamera.instance.focusOn(tile.posV3);
    }

    /// <summary>沿用原版家庭详情和返回历史。</summary>
    /// <param name="family">需要查看的真实家庭。</param>
    private static void InspectFamily(Family family)
    {
        SelectedMetas.selected_family = family;
        ScrollWindow.showWindow(AssetManager.meta_type_library.getAsset(MetaType.Family).window_name);
    }
}

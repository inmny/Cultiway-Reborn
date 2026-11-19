using System;
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

/// <summary>月份、编制草稿、提交预览和重选指挥者交互。</summary>
public sealed partial class WindowVillageDuties
{
    /// <summary>建立总览，具体数值只在刷新时绑定。</summary>
    private void BuildOverview()
    {
        overview = UiElements.CreateText(pages[0].transform, "Summary", "", 500, 112, 8, TextAnchor.UpperLeft);
        Text(pages[0].transform, "Rules", 72);
        UiLayout.SetSize(pages[0].transform, 500, 188);
    }

    /// <summary>十二个月份用独立二元控件表达，不使用循环切换候选按钮。</summary>
    private void BuildCalendar()
    {
        for (int row = 0; row < 2; row++)
        {
            var line = UiLayout.Create(pages[1].transform, "Months" + row, true, 500, 32, UiTheme.Current.Metrics.SpacingSm);
            for (int col = 0; col < 6; col++)
            {
                int index = row * 6 + col;
                var toggle = months[index] = UiElements.CreateToggle(line.transform, "Month" + index, "", true, 80, 32);
                toggle.onValueChanged.AddListener(value =>
                {
                    if (binding || draft == null) return;
                    if (value) draft.WorkMonths |= 1 << index; else draft.WorkMonths &= ~(1 << index);
                    RefreshMonths(); RefreshPreview();
                });
            }
        }
        var actions = UiLayout.Create(pages[1].transform, "CalendarActions", true, 500, 24, UiTheme.Current.Metrics.SpacingSm);
        Button(actions.transform, "AllWork", 160, () => SetMonths(4095));
        Button(actions.transform, "AllRest", 160, () => SetMonths(0));
        Button(actions.transform, "DefaultMonths", 160, () => SetMonths(VillageDutyPolicy.DefaultWorkMonths));
        Text(pages[1].transform, "CalendarHint", 84);
        UiLayout.SetSize(pages[1].transform, 500, 192);
    }

    /// <summary>原版月份名称配合明确工作与休息文字，当前月另加标记。</summary>
    private void RefreshMonths()
    {
        for (int i = 0; i < 12; i++)
        {
            string name = LocalizedTextManager.getText(AssetManager.months.getMonth(i + 1).getLocaleID());
            months[i].GetComponentInChildren<Text>().text = name + (Date.getCurrentMonth() == i + 1 ? " •" : "") +
                "\n" + T(draft.IsWorkMonth(i + 1) ? "Work" : "Rest");
            months[i].SetIsOnWithoutNotify(draft.IsWorkMonth(i + 1));
        }
    }

    /// <summary>快捷设置仅修改本地草稿。</summary>
    /// <param name="mask">十二个月各自是否工作的标记。</param>
    private void SetMonths(int mask) { draft.WorkMonths = mask; RefreshMonths(); RefreshPreview(); }

    /// <summary>建立三层名称、称谓和数量要求，顶层数量始终是唯一军队的营数。</summary>
    private void BuildOrganization()
    {
        organizationScope = UiElements.CreateText(pages[2].transform, "KingdomScope", "", 500, 24, 8, TextAnchor.MiddleLeft);
        Text(pages[2].transform, "OrganizationHint", 36);
        for (int i = 0; i < 3; i++)
        {
            var line = UiLayout.Create(pages[2].transform, "Layer" + i, true, 500, 28, UiTheme.Current.Metrics.SpacingSm);
            var label = UiElements.CreateText(line.transform, "LayerLabel", T("Layer." + i), 48, 28, 7);
            translated.Add((label, "Layer." + i));
            fields[i] = Input(line.transform, "Name" + i, 112, "LayerName");
            fields[i + 3] = Input(line.transform, "Leader" + i, 112, "LeaderTitle");
            fields[i + 6] = Input(line.transform, "Count" + i, 64, "Quantity", true);
            var meaning = UiElements.CreateText(line.transform, "Meaning", T("Quantity." + i), 148, 28, 7, TextAnchor.MiddleLeft);
            translated.Add((meaning, "Quantity." + i));
        }
        var columns = UiLayout.Create(pages[2].transform, "FormationColumns", true, 500, 28, UiTheme.Current.Metrics.SpacingSm);
        var columnLabel = UiElements.CreateText(columns.transform, "Label", T("Columns"), 180, 28, 7);
        translated.Add((columnLabel, "Columns"));
        fields[9] = Input(columns.transform, "Columns", 64, "Columns", true);
        var nameRow = UiLayout.Create(pages[2].transform, "ArmyName", true, 500, 28, UiTheme.Current.Metrics.SpacingSm);
        var nameLabel = UiElements.CreateText(nameRow.transform, "Label", T("ArmyInstanceName"), 140, 28, 7);
        translated.Add((nameLabel, "ArmyInstanceName"));
        armyName = Input(nameRow.transform, "Name", 350, "ArmyInstanceName");
        armyName.onValueChanged.AddListener(_ => { if (!binding) armyNameChanged = true; });
        var actions = UiLayout.Create(pages[2].transform, "MilitaryActions", true, 500, 24, UiTheme.Current.Metrics.SpacingSm);
        Button(actions.transform, "Reappoint", 244, PreviewAppointments);
        Button(actions.transform, "ArmyFormation", 244, () => { previewLevel = 2; previewUnit = 0; Refresh(); });
        formation = VillageFormationPreview.Create(pages[2].transform);
        armyList = UiLayout.Create(pages[2].transform, "ArmyTree", false, 500, 0, 4).transform;
        armyRows = new MonoObjPool<VillageInfoRow>(VillageInfoRow.Prefab, armyList, deactive_action: r => r.Unbind());
    }

    /// <summary>创建输入框并只在编辑后刷新预览。</summary>
    /// <param name="parent">所属编辑行。</param>
    /// <param name="name">本行内的控件名称。</param>
    /// <param name="width">输入框宽度。</param>
    /// <param name="placeholder">空输入框的提示文字编号。</param>
    /// <param name="numeric">是否只接受整数。</param>
    private InputField Input(Transform parent, string name, float width, string placeholder, bool numeric = false)
    {
        var input = UiElements.CreateInput(parent, name, "", T(placeholder), width, 28);
        input.lineType = InputField.LineType.SingleLine; input.characterLimit = numeric ? 10 : 40;
        input.textComponent.supportRichText = false;
        if (numeric) input.characterValidation = InputField.CharacterValidation.Integer;
        input.onValueChanged.AddListener(_ => { if (!binding) RefreshPreview(); });
        return input;
    }

    /// <summary>解析草稿属于用户输入边界，失败时保留输入并显示明确说明。</summary>
    /// <param name="policy">成功解析的独立配置。</param>
    private bool ReadDraft(out VillageDutyPolicy policy)
    {
        policy = draft?.Copy(); if (policy == null) return false;
        policy.SquadName = fields[0].text.Trim(); policy.BattalionName = fields[1].text.Trim(); policy.ArmyName = fields[2].text.Trim();
        policy.SquadLeaderTitle = fields[3].text.Trim(); policy.BattalionLeaderTitle = fields[4].text.Trim(); policy.ArmyLeaderTitle = fields[5].text.Trim();
        if (!int.TryParse(fields[6].text, out policy.SquadSize) || !int.TryParse(fields[7].text, out policy.SquadsPerBattalion) ||
            !int.TryParse(fields[8].text, out policy.BattalionsPerArmy) || !int.TryParse(fields[9].text, out policy.FormationColumns)) return false;
        policy.OccupationTargets.Clear();
        foreach (var pair in occupationFields)
        {
            if (string.IsNullOrWhiteSpace(pair.Value.text)) continue;
            if (!int.TryParse(pair.Value.text, out int target) || target < 0) return false;
            policy.OccupationTargets.Add(pair.Key, target);
        }
        return policy.IsValid();
    }

    /// <summary>在应用之前展示每村容量和全国预计转为后备人数。</summary>
    private void RefreshPreview()
    {
        if (apply == null || City == null || draft == null) return;
        if (!ReadFarmDraft(out int width, out int height, out int maxArea))
        { apply.interactable = false; feedback.color = UiTheme.Current.Palette.Error; feedback.text = T("Farm.InvalidSettings"); return; }
        bool valid = ReadDraft(out var proposed);
        apply.interactable = valid;
        feedback.color = valid ? UiTheme.Current.Palette.MutedText : UiTheme.Current.Palette.Error;
        if (!valid) { feedback.text = T("InvalidPolicy"); return; }
        var data = VillageMilitaryService.Get(City);
        long released = City.kingdom.cities.Sum(c => Math.Max(0L, c.status.warriors_current - proposed.Capacity));
        feedback.text = data.PendingPolicy != null ? T("PendingReorganization") : F("CapacityPreview", data.Policy.Capacity,
            proposed.Capacity, released);
        if (page == 5) feedback.text = F("Farm.SettingsPreview", width, height, maxArea);
        var required = page switch {
            1 => KingdomPolicyCard.WorkCalendar, 2 => KingdomPolicyCard.ArmyOrganization,
            4 => KingdomPolicyCard.OccupationPlanning, 5 => KingdomPolicyCard.PrivateFarms, _ => (KingdomPolicyCard)0 };
        if (required != 0 && !KingdomPolicyService.Has(City, required))
        { feedback.color = UiTheme.Current.Palette.Warning; feedback.text = WindowKingdomPolicies.T("InactiveSettings"); }
        if (page == 2) formation.Bind(City, data, proposed, previewUnit, previewLevel);
    }

    /// <summary>提交给领域服务，不直接改写军队列表或人员数据。</summary>
    private void ApplyDraft()
    {
        var city = City;
        if (city == null || city.kingdom.id != draftKingdomId) { ReloadDraft(); return; }
        if (!ReadFarmDraft(out int width, out int height, out int maxArea) ||
            !ReadDraft(out var policy) || !VillageMilitaryService.Apply(city, policy)) return;
        FamilyFarming.FamilyFarmService.Apply(city, width, height, maxArea);
        if (armyNameChanged) VillageMilitaryService.RenameArmy(city, armyName.text);
        ReloadDraft();
        WorldTip.showNowTop(T("Applied"), false);
    }

    /// <summary>共用安全区内的名称编辑与任命预览，不改变原版窗口返回行为。</summary>
    private void BuildModal()
    {
        var frame = UiWindowFrame.CreateContentSize(BackgroundTransform, "VillageModal", 360, 194, UiTheme.Current.Metrics.SpacingLg);
        var body = UiLayout.Create(frame.Content, "Body", false, 360, 194, 6);
        modalText = UiElements.CreateText(body.transform, "Explanation", "", 360, 120, 7, TextAnchor.UpperLeft);
        modalInput = UiElements.CreateInput(body.transform, "Name", "", T("UnitName"), 360, 28);
        modalInput.characterLimit = 40; modalInput.textComponent.supportRichText = false;
        var actions = UiLayout.Create(body.transform, "Actions", true, 360, 24, 6);
        Button(actions.transform, "Confirm", 176, () => { modalAction?.Invoke(); modal.Hide(); Refresh(); });
        Button(actions.transform, "Close", 176, () => modal.Hide());
        modal = new UiModal(frame.Root.gameObject, owner);
    }

    /// <summary>绑定打开时的具体单位，提交时再验证目标村庄。</summary>
    /// <param name="id">本村内的稳定单位编号。</param>
    /// <param name="name">打开弹窗时的实例名称。</param>
    private void RenameUnit(int id, string name)
    {
        long boundCity = cityId;
        modalText.text = T("RenameHint"); modalInput.gameObject.SetActive(true); modalInput.SetTextWithoutNotify(name);
        modalAction = () => { if (cityId == boundCity && City != null) VillageMilitaryService.Rename(City, id, modalInput.text); };
        modal.Show();
    }

    /// <summary>先展示同一排名规则计算的任命变化，再由玩家提交。</summary>
    private void PreviewAppointments()
    {
        var city = City; if (city == null) return;
        var changes = VillageMilitaryService.PreviewAppointments(city);
        modalInput.gameObject.SetActive(false);
        modalText.text = T("AppointmentRule") + "\n" + (changes.Count == 0 ? T("NoAppointmentChanges") :
            string.Join("\n", changes.Take(5).Select(c => F("AppointmentChange", T("Layer." + c.layer), ActorName(c.before), ActorName(c.after)))));
        long bound = city.id;
        modalAction = () =>
        {
            if (City == null || City.id != bound) return;
            if (City.units.Any(a => a.isWarrior() && !VillageDutyService.CanChangeAssignment(a)))
            { WorldTip.showNowTop(T("BusyAppointment"), false); return; }
            VillageMilitaryService.ElectCommanders(City, VillageMilitaryService.Get(City), true);
            VillageMilitaryService.Save(City);
        };
        modal.Show();
    }
}

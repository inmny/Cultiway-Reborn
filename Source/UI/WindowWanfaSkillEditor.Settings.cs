using System;
using System.Globalization;
using System.Linq;
using Cultiway.Core.SkillLibV3.Settings;
using UnityEngine;

namespace Cultiway.UI;

public sealed partial class WindowWanfaSkillEditor
{
    /// <summary>单招元素选择使用现有的可搜索菜单。</summary>
    private UiOptionMenu _settingsMenu;

    /// <summary>在杂项页编辑当前法术的通用参数。</summary>
    private void BuildSkillSettings()
    {
        SettingChoice("Cultiway.SkillSettings.Nature", _draft.NatureId == null
                ? "Cultiway.SkillSettings.Nature.Inherit".Localize() : SkillNatureCatalog.Natures[_draft.NatureId].NameKey.Localize(),
            () => ShowSettingsOptions("Cultiway.SkillSettings.Nature", new[] { new UiOptionMenuOption
                {
                    Label = "Cultiway.SkillSettings.Nature.Inherit".Localize(), Selected = _draft.NatureId == null,
                    Select = () => ApplyMutation(() => _draft.NatureId = null)
                } }.Concat(SkillNatureCatalog.Natures.Values.Where(nature =>
                    ModClass.I.SkillV3.SkillLib.get(_draft.EntityAssetId).SupportsNature(nature.Id)).Select(nature => new UiOptionMenuOption
                {
                    Label = nature.NameKey.Localize(), Selected = _draft.NatureId == nature.Id,
                    Select = () => ApplyMutation(() => _draft.NatureId = nature.Id)
                })).ToList()));
        SettingNumber("Cultiway.SkillSettings.Area", () => _draft.AreaMultiplier, value => _draft.AreaMultiplier = value, .1f, 8);
        SettingNumber("Cultiway.SkillSettings.Power", () => _draft.PowerMultiplier, value => _draft.PowerMultiplier = value, .05f, 8);
        SettingNumber("Cultiway.SkillSettings.Duration", () => _draft.Duration, value => _draft.Duration = value, 0, 30);
        SettingToggle("Cultiway.SkillSettings.ContactEnabled", _draft.ContactEnabled, value => _draft.ContactEnabled = value);
        SettingToggle("Cultiway.SkillSettings.ConductiveWater", _draft.ConductiveWater, value => _draft.ConductiveWater = value);
    }

    /// <summary>提供单招性质的可搜索选项。</summary>
    /// <param name="title">标题的本地化编号。</param>
    /// <param name="options">当前可选项目。</param>
    private void ShowSettingsOptions(string title, System.Collections.Generic.List<UiOptionMenuOption> options)
    {
        _settingsMenu ??= new UiOptionMenu(BackgroundTransform, _editorCanvasGroup, UiWindowContext.Bind(BackgroundTransform).ScrollbarTemplate,
            new UiOptionMenuConfig
            {
                SearchPlaceholder = "Cultiway.SkillSettings.Search".Localize(), EmptyText = "Cultiway.SkillSettings.Empty".Localize(),
                CloseText = "Cultiway.SkillSettings.Close".Localize(), CloseTooltipTitle = "Cultiway.SkillSettings.Close".Localize(),
                CloseTooltipDescription = "Cultiway.SkillSettings.Close".Localize()
            });
        _settingsMenu.Show(title.Localize(), options, true);
    }

    /// <summary>复用现有列表行打开选择菜单。</summary>
    /// <param name="key">字段的本地化编号。</param>
    /// <param name="value">当前选择。</param>
    /// <param name="select">打开菜单。</param>
    private void SettingChoice(string key, string value, Action select) => _rowPool.GetNext().Setup(
        key.Localize(), value, "Cultiway.SkillSettings.Select".Localize(), true, select, UiIcons.Select);

    /// <summary>在编辑入口检查数值，沿用当前草稿的撤销和刷新。</summary>
    /// <param name="key">字段的本地化编号。</param>
    /// <param name="get">读取当前数值。</param>
    /// <param name="set">应用合法数值。</param>
    /// <param name="minimum">允许的最小值。</param>
    /// <param name="maximum">允许的最大值。</param>
    private void SettingNumber(string key, Func<float> get, Action<float> set, float minimum, float maximum)
    {
        var row = _rowPool.GetNext(); row.Setup(key.Localize(), $"{minimum}～{maximum}", string.Empty, false, null);
        var input = UiElements.CreateInput(row.UseInlineControls(100f), "Value", get().ToString("0.##", CultureInfo.InvariantCulture), string.Empty, 96f, 22f);
        input.onEndEdit.AddListener(text =>
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) && !float.IsNaN(number) && !float.IsInfinity(number))
                ApplyMutation(() => set(Mathf.Clamp(number, minimum, maximum)));
            else RefreshPage();
        });
    }

    /// <summary>使用现有二元开关编辑单招行为。</summary>
    /// <param name="key">字段的本地化编号。</param>
    /// <param name="value">当前值。</param>
    /// <param name="set">应用新值。</param>
    private void SettingToggle(string key, bool value, Action<bool> set)
    {
        var row = _rowPool.GetNext(); row.Setup(key.Localize(), string.Empty, string.Empty, false, null);
        UiElements.CreateToggle(row.UseInlineControls(100f), "Value", string.Empty, value, 96f, 22f)
            .onValueChanged.AddListener(next => ApplyMutation(() => set(next)));
    }
}

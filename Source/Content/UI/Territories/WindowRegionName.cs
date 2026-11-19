using Cultiway.Content.Territories;
using Cultiway.UI;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Territories;

/// <summary>在原版窗口中编辑指定地区名称，保存后立即更新地图。</summary>
public sealed class WindowRegionName : AbstractWideWindow<WindowRegionName>
{
    /// <summary>名称编辑窗口编号。</summary>
    public const string Id = "Cultiway.UI.WindowRegionName";
    /// <summary>紧凑名称编辑窗口尺寸。</summary>
    public static readonly Vector2 WindowSize = new(320, 210);
    /// <summary>已经创建的窗口。</summary>
    private static WindowRegionName instance;
    /// <summary>打开窗口时固定的地区。</summary>
    private static CustomTerritory target;
    /// <summary>名称输入框。</summary>
    private InputField nameInput;
    /// <summary>地区的村庄及面积。</summary>
    private Text summary;
    /// <summary>输入不合法时的提示。</summary>
    private Text feedback;
    /// <summary>保存按钮。</summary>
    private Button save;
    /// <summary>取消按钮。</summary>
    private Button cancel;

    /// <summary>打开指定地区的名称编辑。</summary>
    /// <param name="region">本次操作的地区。</param>
    internal static void Open(CustomTerritory region)
    {
        target = region;
        ScrollWindow.showWindow(Id);
        instance.Refresh();
    }

    /// <summary>换世界时丢弃旧地区，关闭未提交的编辑。</summary>
    internal static void ClearWorldState()
    {
        target = null;
        if (instance == null) return;
        instance.nameInput.text = string.Empty;
        if (instance.gameObject.activeInHierarchy) instance.GetComponent<ScrollWindow>().clickHide();
    }

    /// <summary>创建一次输入框和原版风格按钮。</summary>
    protected override void Init()
    {
        instance = this;
        UiWindowContext.Bind(BackgroundTransform);
        var m = UiTheme.Current.Metrics;
        var root = UiLayout.Create(BackgroundTransform, "RegionNameEditor", false, 240, 142, m.SpacingLg);
        root.transform.localPosition = new Vector3(0, -8);
        summary = UiElements.CreateText(root.transform, "RegionSummary", "", 240, 24, 7);
        summary.color = UiTheme.Current.Palette.MutedText;
        nameInput = UiElements.CreateInput(root.transform, "RegionName", "", T("NamePlaceholder"), 240, m.ControlLarge);
        nameInput.lineType = InputField.LineType.SingleLine;
        nameInput.characterLimit = TerritoryService.MaxNameLength;
        nameInput.textComponent.supportRichText = false;
        feedback = UiElements.CreateText(root.transform, "Feedback", "", 240, 28, 7);
        feedback.color = UiTheme.Current.Palette.Error;
        var actions = UiLayout.Create(root.transform, "Actions", true, 240, m.ControlMedium, m.SpacingLg);
        save = UiElements.CreateButton(actions.transform, "Save", T("SaveName"), 116, m.ControlMedium, SaveName);
        cancel = UiElements.CreateButton(actions.transform, "Cancel", T("Cancel"), 116, m.ControlMedium, Close);
    }

    /// <summary>每次打开时读取当前名称和语言。</summary>
    public override void OnNormalEnable() => Refresh();

    /// <summary>统一刷新当前地区和按钮状态。</summary>
    private void Refresh()
    {
        nameInput.text = target == null ? string.Empty : TerritoryService.GetName(target);
        ((Text)nameInput.placeholder).text = T("NamePlaceholder");
        summary.text = target == null ? T("NoRegion") : string.Format(T("EditorSummary"), target.Id,
            TerritoryService.GetOwner(target)?.name ?? T("Vacant"), target.Zones.Count);
        feedback.text = string.Empty;
        save.interactable = target != null;
        save.GetComponentInChildren<Text>().text = T("SaveName");
        cancel.GetComponentInChildren<Text>().text = T("Cancel");
    }

    /// <summary>提交到地区服务，不修改地图形状或村庄归属。</summary>
    private void SaveName()
    {
        string error = TerritoryService.Rename(target, nameInput.text);
        if (error != null) { feedback.text = T(error); return; }
        WorldTip.showNow(string.Format(T("Renamed"), TerritoryService.GetName(target)), false, "top");
        Close();
    }

    /// <summary>取消和保存完成后共用原版关闭流程。</summary>
    private void Close()
    {
        target = null;
        GetComponent<ScrollWindow>().clickHide();
    }

    /// <summary>读取本功能的界面文本。</summary>
    /// <param name="suffix">文本键后缀。</param>
    /// <returns>当前语言文本。</returns>
    private static string T(string suffix) => LocalizedTextManager.getText("Cultiway.Territories." + suffix);
}

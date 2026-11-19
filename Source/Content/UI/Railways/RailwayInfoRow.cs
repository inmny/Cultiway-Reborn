using System;
using Cultiway.Abstract;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Railways;

/// <summary>铁路详情的池化条目，正文与固定操作区分开，不阻挡滚轮。</summary>
public sealed class RailwayInfoRow : APrefabPreview<RailwayInfoRow>
{
    /// <summary>标题。</summary>
    private Text title;
    /// <summary>两行详情。</summary>
    private Text detail;
    /// <summary>定位按钮。</summary>
    private Button locate;
    /// <summary>查看关联对象按钮。</summary>
    private Button open;
    /// <summary>语义背景。</summary>
    private UiListRowChrome chrome;

    /// <summary>解析自己创建的固定节点。</summary>
    protected override void OnInit()
    {
        title = transform.Find("Labels/Title").GetComponent<Text>();
        detail = transform.Find("Labels/Detail").GetComponent<Text>();
        locate = transform.Find("Locate").GetComponent<Button>();
        open = transform.Find("Open").GetComponent<Button>();
        chrome = UiListRowChrome.From(gameObject);
    }

    /// <summary>一次性绑定正文、动作和状态，复用时替换旧回调。</summary>
    /// <param name="heading">标题。</param>
    /// <param name="body">详情。</param>
    /// <param name="onLocate">定位动作。</param>
    /// <param name="onOpen">查看动作。</param>
    /// <param name="state">当前语义状态。</param>
    internal void Bind(string heading, string body, Action onLocate, Action onOpen, UiControlState state)
    {
        Init(); title.text = heading; detail.text = body; chrome.SetState(state);
        locate.gameObject.SetActive(onLocate != null); open.gameObject.SetActive(onOpen != null);
        UiElements.SetButtonBinding(locate, onLocate, "Cultiway.Railways.Locate", "Cultiway.Railways.LocateTip");
        UiElements.SetButtonBinding(open, onOpen, "Cultiway.Railways.Open", "Cultiway.Railways.OpenTip");
    }

    /// <summary>清世界时解除对人物、城市和建筑的引用。</summary>
    internal void Unbind()
    {
        if (locate == null) return;
        locate.onClick.RemoveAllListeners(); open.onClick.RemoveAllListeners();
    }

    /// <summary>只创建一次行结构，文本不能挤入按钮区域。</summary>
    private static void _init()
    {
        var m = UiTheme.Current.Metrics;
        var obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(RailwayInfoRow), true, 460, 44, m.SpacingSm);
        UiListRowChrome.Attach(obj, false);
        obj.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset((int)m.SpacingMd, (int)m.SpacingMd, (int)m.SpacingSm, (int)m.SpacingSm);
        var labels = UiLayout.Create(obj.transform, "Labels", false, 392, 36, 0);
        UiElements.CreateText(labels.transform, "Title", "", 392, 16, 8, TextAnchor.UpperLeft, FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", "", 392, 20, 7, TextAnchor.UpperLeft);
        UiElements.CreateIconButton(obj.transform, "Locate", UiIcons.Select, m.ControlSmall, m.ControlSmall, () => { });
        UiElements.CreateIconButton(obj.transform, "Open", UiIcons.Info, m.ControlSmall, m.ControlSmall, () => { });
        Prefab = obj.AddComponent<RailwayInfoRow>();
    }
}

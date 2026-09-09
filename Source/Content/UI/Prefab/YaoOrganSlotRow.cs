using System;
using Cultiway.Abstract;
using Cultiway.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Content.UI.Prefab;

/// <summary>妖兽器官编辑器中的一行：身体位置、器官名称、等级与常用操作。</summary>
public sealed class YaoOrganSlotRow : APrefabPreview<YaoOrganSlotRow>
{
    private UiListRowChrome _chrome;
    private Text _slotName;
    private Image _icon;
    private Text _organName;
    private Text _detail;
    private Button _rankDown;
    private Button _rankUp;
    private Button _pick;
    private Button _remove;

    protected override void OnInit()
    {
        _chrome = UiListRowChrome.From(gameObject);
        _slotName = transform.Find("SlotName").GetComponent<Text>();
        _icon = transform.Find("Icon").GetComponent<Image>();
        _organName = transform.Find("Labels/Name").GetComponent<Text>();
        _detail = transform.Find("Labels/Detail").GetComponent<Text>();
        _rankDown = transform.Find("Actions/RankDown").GetComponent<Button>();
        _rankUp = transform.Find("Actions/RankUp").GetComponent<Button>();
        _pick = transform.Find("Actions/Pick").GetComponent<Button>();
        _remove = transform.Find("Actions/Remove").GetComponent<Button>();
    }

    /// <summary>按行内容刷新文本、图标与按钮可用状态。</summary>
    /// <param name="slotName">身体位置名称。</param>
    /// <param name="organName">器官名称；空位行显示占位文本。</param>
    /// <param name="detail">等级、来源与复杂度的摘要。</param>
    /// <param name="iconPath">器官贴图路径；空位行传入 null 隐藏图标。</param>
    /// <param name="hasOrgan">该行是否承载了器官；空位行只提供更换入口。</param>
    /// <param name="canRankDown">是否还能降低等级。</param>
    /// <param name="canRankUp">是否还能提升等级。</param>
    /// <param name="pickOrgan">点击更换时触发。</param>
    /// <param name="rankDown">点击降低等级时触发。</param>
    /// <param name="rankUp">点击提升等级时触发。</param>
    /// <param name="removeOrgan">点击移除时触发。</param>
    public void Setup(string slotName, string organName, string detail, string iconPath, bool hasOrgan,
        bool canRankDown, bool canRankUp, Action pickOrgan, Action rankDown, Action rankUp, Action removeOrgan)
    {
        Init();
        _slotName.text = slotName;
        _organName.text = organName;
        _detail.text = detail;
        _chrome.SetState(UiControlState.Normal);
        _icon.gameObject.SetActive(!string.IsNullOrEmpty(iconPath));
        if (!string.IsNullOrEmpty(iconPath)) _icon.sprite = UiResources.GetSprite(iconPath);

        UiElements.SetButtonBinding(_rankDown, rankDown, "Cultiway.YaoOrganEditor.Action.RankDown",
            "Cultiway.YaoOrganEditor.Tooltip.RankDown", hasOrgan && canRankDown);
        UiElements.SetButtonBinding(_rankUp, rankUp, "Cultiway.YaoOrganEditor.Action.RankUp",
            "Cultiway.YaoOrganEditor.Tooltip.RankUp", hasOrgan && canRankUp);
        UiElements.SetButtonBinding(_pick, pickOrgan, "Cultiway.YaoOrganEditor.Action.Pick",
            "Cultiway.YaoOrganEditor.Tooltip.Pick");
        UiElements.SetButtonBinding(_remove, removeOrgan, "Cultiway.YaoOrganEditor.Action.Remove",
            "Cultiway.YaoOrganEditor.Tooltip.Remove", hasOrgan);
    }

    private static void _init()
    {
        var obj = UiLayout.Create(ModClass.I.PrefabLibrary, nameof(YaoOrganSlotRow), true, 500f, 38f, 3f);
        UiListRowChrome.Attach(obj, false);
        obj.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 4, 2, 2);

        UiElements.CreateText(obj.transform, "SlotName", string.Empty, 76f, 34f, 7, TextAnchor.MiddleLeft,
            FontStyle.Bold);

        var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(obj.transform, false);
        UiLayout.SetSize(iconObj.transform, 34f, 34f);
        Image icon = iconObj.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var labels = UiLayout.Create(obj.transform, "Labels", false, 224f, 34f, 0f);
        UiElements.CreateText(labels.transform, "Name", string.Empty, 224f, 17f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        UiElements.CreateText(labels.transform, "Detail", string.Empty, 224f, 17f, 6);

        // 操作按钮组脱离行的水平布局，整体锚定在行右缘。
        var actions = new GameObject("Actions", typeof(RectTransform), typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        actions.transform.SetParent(obj.transform, false);
        RectTransform actionsRect = actions.GetComponent<RectTransform>();
        actionsRect.anchorMin = new Vector2(1f, 0f);
        actionsRect.anchorMax = new Vector2(1f, 1f);
        actionsRect.pivot = new Vector2(1f, 0.5f);
        actionsRect.sizeDelta = new Vector2(108f, -8f);
        actionsRect.anchoredPosition = new Vector2(-34f, 0f);
        actions.GetComponent<LayoutElement>().ignoreLayout = true;
        HorizontalLayoutGroup actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 3f;
        actionLayout.childForceExpandWidth = false;
        actionLayout.childAlignment = TextAnchor.MiddleRight;
        UiElements.CreateIconButton(actions.transform, "RankDown", UiIcons.MoveDown, 20f, 24f, () => { }, 3f,
            1.45f, 90f);
        UiElements.CreateIconButton(actions.transform, "RankUp", UiIcons.MoveUp, 20f, 24f, () => { }, 3f, 1.45f,
            90f);
        UiElements.CreateIconButton(actions.transform, "Pick", UiIcons.Edit, 28f, 24f, () => { }, 3f);
        UiElements.CreateIconButton(actions.transform, "Remove", UiIcons.Delete, 28f, 24f, () => { });
        Prefab = obj.AddComponent<YaoOrganSlotRow>();
    }
}

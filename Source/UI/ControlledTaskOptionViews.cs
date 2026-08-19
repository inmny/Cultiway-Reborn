using System;
using Cultiway.Core.ControlledTasks;
using strings;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>受控任务选项视图共用的图标和提示文本处理。</summary>
internal static class ControlledTaskOptionView
{
    internal static void SetIcon(Image image, Button iconButton, ControlledTaskOption option)
    {
        Sprite sprite = option?.IconSprite;
        if (sprite == null && !string.IsNullOrEmpty(option?.IconPath))
            sprite = UiResources.GetSprite(option.IconPath);

        image.sprite = sprite;
        image.overrideSprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        iconButton.gameObject.SetActive(sprite != null);
    }

    internal static void SetTooltip(Image icon, ControlledTaskOption option)
    {
        UiTooltip.Clear(icon.gameObject);
        if (option == null) return;
        if (option.SpecialItemId > 0)
        {
            int itemId = option.SpecialItemId;
            UiTooltip.Set(icon.gameObject, () =>
                Tooltip.show(icon.gameObject, WorldboxGame.Tooltips.SpecialItem.id, new TooltipData
                {
                    tip_name = itemId.ToString()
                }));
            return;
        }

        string description = string.IsNullOrEmpty(option.Summary)
            ? option.ReasonLocaleKey.Localize()
            : option.Summary;
        if (!string.IsNullOrEmpty(description))
            UiTooltip.Set(icon.gameObject, option.Label, description);
    }

    internal static void ConfigureIconButton(Button button, Image image)
    {
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }
}

/// <summary>参数选择器的紧凑横向条目，宽度由展示类型决定而不是强行填满窗口。</summary>
internal sealed class ControlledTaskOptionListRow : MonoBehaviour
{
    internal const float Height = 28f;

    private Button button;
    private Button iconButton;
    private Image icon;
    private Image selectedMarker;
    private Text label;
    private Action<string> selected;
    private string optionKey;

    internal static ControlledTaskOptionListRow CreateTemplate(Transform parent, float width)
    {
        GameObject root = new GameObject("ControlledTaskOptionListRow", typeof(RectTransform),
            typeof(Image), typeof(Button), typeof(LayoutElement), typeof(UiListRowRootMarker),
            typeof(ControlledTaskOptionListRow));
        root.transform.SetParent(parent, false);
        UiLayout.SetSize(root.transform, width, Height);
        UiResources.ApplySurface(root.GetComponent<Image>(), UiSurface.Button);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Button));
        iconObject.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(5f, 0f);
        iconRect.sizeDelta = new Vector2(18f, 18f);
        Image icon = iconObject.GetComponent<Image>();
        icon.raycastTarget = true;
        Button iconButton = iconObject.GetComponent<Button>();
        ControlledTaskOptionView.ConfigureIconButton(iconButton, icon);

        Text label = UiElements.CreateText(root.transform, "Label", string.Empty, width - 48f, 26f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(28f, 1f);
        labelRect.offsetMax = new Vector2(-22f, -1f);
        label.GetComponent<LayoutElement>().ignoreLayout = true;
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 5;
        label.resizeTextMaxSize = 7;

        GameObject markerObject = new GameObject("Selected", typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(root.transform, false);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(1f, 0.5f);
        markerRect.pivot = new Vector2(1f, 0.5f);
        markerRect.anchoredPosition = new Vector2(-4f, 0f);
        markerRect.sizeDelta = new Vector2(12f, 12f);
        Image marker = markerObject.GetComponent<Image>();
        UiResources.SetImage(marker, UiIcons.Confirm);
        marker.color = UiTheme.Current.Palette.Success;
        marker.raycastTarget = false;
        markerObject.SetActive(false);

        return root.GetComponent<ControlledTaskOptionListRow>();
    }

    internal void Initialize(Action<string> selectionAction)
    {
        button = GetComponent<Button>();
        iconButton = transform.Find("Icon").GetComponent<Button>();
        icon = iconButton.GetComponent<Image>();
        selectedMarker = transform.Find("Selected").GetComponent<Image>();
        label = transform.Find("Label").GetComponent<Text>();
        selected = selectionAction;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(InvokeSelection);
        iconButton.onClick.RemoveAllListeners();
        iconButton.onClick.AddListener(InvokeSelection);
    }

    internal void Setup(ControlledTaskOption option, bool isSelected, float width)
    {
        optionKey = option?.Key;
        RectTransform rect = GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, Height);
        LayoutElement layout = GetComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;

        label.text = option?.Label ?? string.Empty;
        label.color = option?.Enabled == true
            ? UiTheme.Current.Palette.PrimaryText
            : UiTheme.Current.Palette.MutedText;
        ControlledTaskOptionView.SetIcon(icon, iconButton, option);
        ControlledTaskOptionView.SetTooltip(icon, option);
        selectedMarker.gameObject.SetActive(isSelected);
        button.interactable = option?.Enabled == true;
        UiStateStyle.ApplyVisual(button,
            isSelected
                ? UiControlState.Selected
                : option?.Enabled == true ? UiControlState.Normal : UiControlState.Disabled);
    }

    internal void Clear()
    {
        optionKey = null;
        label.text = string.Empty;
        icon.sprite = null;
        icon.overrideSprite = null;
        iconButton.gameObject.SetActive(false);
        selectedMarker.gameObject.SetActive(false);
        UiTooltip.Clear(icon.gameObject);
    }

    private void InvokeSelection()
    {
        if (!string.IsNullOrEmpty(optionKey)) selected?.Invoke(optionKey);
    }
}

/// <summary>背包节奏的材料格，只显示真实物品图标和选中标记。</summary>
internal sealed class ControlledTaskOptionGridCell : MonoBehaviour
{
    internal const float CellSize = 34f;
    private const float IconSize = 26f;
    private const float MarkerSize = 10f;

    private Button button;
    private Button iconButton;
    private Image icon;
    private Image selectedMarker;
    private Action<string> selected;
    private string optionKey;

    internal static ControlledTaskOptionGridCell CreateTemplate(Transform parent)
    {
        GameObject root = new GameObject("ControlledTaskOptionGridCell", typeof(RectTransform),
            typeof(Image), typeof(Button), typeof(LayoutElement), typeof(ControlledTaskOptionGridCell));
        root.transform.SetParent(parent, false);
        UiLayout.SetSize(root.transform, CellSize, CellSize);
        UiResources.ApplySurface(root.GetComponent<Image>(), UiSurface.Button);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(Button));
        iconObject.transform.SetParent(root.transform, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(IconSize, IconSize);
        Image icon = iconObject.GetComponent<Image>();
        icon.raycastTarget = true;
        Button iconButton = iconObject.GetComponent<Button>();
        ControlledTaskOptionView.ConfigureIconButton(iconButton, icon);

        GameObject markerObject = new GameObject("Selected", typeof(RectTransform), typeof(Image));
        markerObject.transform.SetParent(root.transform, false);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = Vector2.one;
        markerRect.pivot = Vector2.one;
        markerRect.anchoredPosition = new Vector2(-1f, -1f);
        markerRect.sizeDelta = new Vector2(MarkerSize, MarkerSize);
        Image marker = markerObject.GetComponent<Image>();
        UiResources.SetImage(marker, UiIcons.Confirm);
        marker.color = UiTheme.Current.Palette.Success;
        marker.raycastTarget = false;
        markerObject.SetActive(false);

        return root.GetComponent<ControlledTaskOptionGridCell>();
    }

    internal void Initialize(Action<string> selectionAction)
    {
        button = GetComponent<Button>();
        iconButton = transform.Find("Icon").GetComponent<Button>();
        icon = iconButton.GetComponent<Image>();
        selectedMarker = transform.Find("Selected").GetComponent<Image>();
        selected = selectionAction;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(InvokeSelection);
        iconButton.onClick.RemoveAllListeners();
        iconButton.onClick.AddListener(InvokeSelection);
    }

    internal void Setup(ControlledTaskOption option, bool isSelected)
    {
        optionKey = option?.Key;
        ControlledTaskOptionView.SetIcon(icon, iconButton, option);
        ControlledTaskOptionView.SetTooltip(icon, option);
        selectedMarker.gameObject.SetActive(isSelected);
        button.interactable = option?.Enabled == true;
        UiStateStyle.ApplyVisual(button,
            isSelected
                ? UiControlState.Selected
                : option?.Enabled == true ? UiControlState.Normal : UiControlState.Disabled);
    }

    internal void Clear()
    {
        optionKey = null;
        icon.sprite = null;
        icon.overrideSprite = null;
        iconButton.gameObject.SetActive(false);
        selectedMarker.gameObject.SetActive(false);
        UiTooltip.Clear(icon.gameObject);
    }

    private void InvokeSelection()
    {
        if (!string.IsNullOrEmpty(optionKey)) selected?.Invoke(optionKey);
    }
}

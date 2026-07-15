using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI;

internal static class BaibaoUiFactory
{
    private static readonly Color SelectedColor = new(0.68f, 0.68f, 0.68f, 1f);
    private static readonly Color NormalColor = Color.white;

    public static Color SelectionColor => SelectedColor;

    public static GameObject CreatePanel(Transform parent, string name, bool horizontal, float width, float height,
        float spacing = 3f, TextAnchor? alignment = null)
    {
        GameObject panel = WanfaUiFactory.CreateLayout(parent, name, horizontal, width, height, spacing, alignment);
        const int padding = 6;
        if (horizontal)
            panel.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
        else
            panel.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
        Image image = panel.AddComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.82f, 0.82f, 0.82f, 0.94f);
        return panel;
    }

    public static Text CreateSectionTitle(Transform parent, string name, string value, float width)
    {
        Text title = WanfaUiFactory.CreateText(parent, name, value, width, 18f, 8, TextAnchor.MiddleLeft,
            FontStyle.Bold);
        title.color = new Color(1f, 0.86f, 0.55f, 1f);
        return title;
    }

    public static void SetSelected(Button button, bool selected)
    {
        button.GetComponent<Image>().color = selected ? SelectedColor : NormalColor;
    }

    public static void AddScrollBackground(Transform content)
    {
        const float contentInset = 6f;
        float scrollbarAreaWidth = WanfaUiFactory.OriginalVerticalScrollbarReservedWidth + 2f;
        RectTransform viewport = (RectTransform)content.parent;
        RectTransform root = (RectTransform)viewport.parent;

        GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(root, false);
        backgroundObject.transform.SetAsFirstSibling();
        WanfaUiFactory.Stretch(backgroundObject.GetComponent<RectTransform>(), 0f, scrollbarAreaWidth, 0f, 0f);
        Image background = backgroundObject.GetComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        background.type = Image.Type.Sliced;
        background.raycastTarget = false;

        viewport.offsetMin = new Vector2(contentInset, contentInset);
        viewport.offsetMax = new Vector2(-(scrollbarAreaWidth + contentInset), -contentInset);
    }

    public static Button CreateSwatchButton(Transform parent, string name, Color color, float size,
        UnityAction action)
    {
        Button button = WanfaUiFactory.CreateIconButton(parent, name, BaibaoUiIcons.Color, size, size, action, 4f);
        Image icon = button.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = null;
        icon.color = color;
        icon.preserveAspect = false;
        return button;
    }

    public static void AddSearchIcon(InputField input)
    {
        GameObject icon = new("SearchIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(input.transform, false);
        RectTransform rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(14f, 14f);
        rect.anchoredPosition = new Vector2(10f, 0f);
        Image image = icon.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite(BaibaoUiIcons.Search);
        image.preserveAspect = true;
        image.raycastTarget = false;
        input.textComponent.rectTransform.offsetMin = new Vector2(20f, 1f);
        input.placeholder.GetComponent<RectTransform>().offsetMin = new Vector2(20f, 1f);
    }

    public static void SetButtonLabel(Button button, string value)
    {
        button.GetComponentInChildren<Text>().text = value;
    }
}

internal sealed class BaibaoMenuOption
{
    public string Label;
    public string IconPath;
    public string SearchText;
    public bool Selected;
    public Action Select;
}

/// <summary>窗口内复用的模态选项菜单，用于直接选择筛选与排序项。</summary>
internal sealed class BaibaoOptionMenu
{
    private const int ColumnCount = 2;
    private const int SearchThreshold = 10;
    private const int MaxVisibleRows = 5;
    private const float PanelWidth = 272f;
    private const float InnerWidth = 260f;
    private const float ScrollWidth = 248f;
    private const float CellWidth = 103f;
    private const float CellHeight = 24f;
    private const float GridSpacing = 4f;
    private const float TitleHeight = 20f;
    private const float SearchHeight = 22f;
    private const float CloseHeight = 22f;
    private const float FooterHeight = 30f;
    private const float ViewportInset = 6f;
    private const float ContentPadding = 4f;
    private const float PanelPadding = 6f;
    private const float PanelSpacing = 4f;

    private readonly GameObject _panel;
    private readonly Text _title;
    private readonly InputField _search;
    private readonly RectTransform _optionScrollRoot;
    private readonly RectTransform _optionViewport;
    private readonly Transform _optionContent;
    private readonly ScrollRect _optionScroll;
    private readonly GameObject _scrollbarMask;
    private readonly Text _empty;
    private readonly List<Button> _optionButtons = new();
    private readonly List<BaibaoMenuOption> _options = new();
    private readonly CanvasGroup _owner;
    private bool _searchVisible;

    public BaibaoOptionMenu(Transform parent, CanvasGroup owner, Transform scrollbarMaskTemplate)
    {
        _owner = owner;
        _panel = WanfaUiFactory.CreateLayout(parent, "BaibaoOptionMenu", false, PanelWidth, 120f, PanelSpacing,
            TextAnchor.UpperCenter);
        _panel.transform.localPosition = new Vector3(0f, -4f, 0f);
        VerticalLayoutGroup panelLayout = _panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(
            (int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
        panelLayout.childForceExpandWidth = false;
        Image background = _panel.AddComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowEmptyFrame");
        background.type = Image.Type.Sliced;
        _title = WanfaUiFactory.CreateText(_panel.transform, "Title", string.Empty, InnerWidth, TitleHeight, 8,
            TextAnchor.MiddleCenter, FontStyle.Bold);

        _search = WanfaUiFactory.CreateInput(_panel.transform, "Search", string.Empty,
            "Cultiway.Baibao.UI.Placeholder.SearchShapes".Localize(), InnerWidth, SearchHeight);
        BaibaoUiFactory.AddSearchIcon(_search);
        _search.onValueChanged.AddListener(FilterOptions);

        _optionContent = WanfaUiFactory.CreateScrollGridContent(_panel.transform, "Options", ScrollWidth, 64f,
            ColumnCount, new Vector2(CellWidth, CellHeight), new Vector2(GridSpacing, GridSpacing));
        _optionScrollRoot = (RectTransform)_optionContent.parent.parent;
        _optionViewport = (RectTransform)_optionContent.parent;
        GridLayoutGroup optionLayout = _optionContent.GetComponent<GridLayoutGroup>();
        optionLayout.childAlignment = TextAnchor.UpperCenter;
        optionLayout.padding = new RectOffset((int)ContentPadding, (int)ContentPadding,
            (int)ContentPadding, (int)ContentPadding);
        Image optionsBackground = _optionScrollRoot.gameObject.AddComponent<Image>();
        optionsBackground.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        optionsBackground.type = Image.Type.Sliced;
        WanfaUiFactory.AttachOriginalVerticalScrollbar(_optionContent, scrollbarMaskTemplate);
        _optionScroll = _optionScrollRoot.GetComponent<ScrollRect>();
        _scrollbarMask = _optionScrollRoot.Find("Scrollbar Vertical Mask").gameObject;
        _empty = WanfaUiFactory.CreateText(_optionScrollRoot, "Empty",
            "Cultiway.Baibao.UI.State.NoMenuResults".Localize(), ScrollWidth - 8f, 24f, 7,
            TextAnchor.MiddleCenter);
        _empty.raycastTarget = false;

        GameObject footer = WanfaUiFactory.CreateLayout(_panel.transform, "Footer", true, InnerWidth,
            FooterHeight, 0f, TextAnchor.MiddleCenter);
        footer.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
        Button close = WanfaUiFactory.CreateIconTextButton(footer.transform, "Close", BaibaoUiIcons.Cancel,
            "Cultiway.Baibao.UI.Action.CloseMenu".Localize(), InnerWidth - 12f, CloseHeight, Hide);
        WanfaUiFactory.SetTooltip(close.gameObject, "Cultiway.Baibao.UI.Action.CloseMenu",
            "Cultiway.Baibao.UI.Tooltip.CloseMenu");
        _panel.SetActive(false);
    }

    public void Show(string title, IReadOnlyList<BaibaoMenuOption> options, bool searchable = false)
    {
        _title.text = title;
        ClearOptions();
        for (int i = 0; i < options.Count; i++)
        {
            BaibaoMenuOption option = options[i];
            Button button = WanfaUiFactory.CreateIconTextButton(_optionContent, $"Option{i}",
                option.IconPath ?? BaibaoUiIcons.Options, option.Label, CellWidth, CellHeight, () =>
                {
                    Hide();
                    option.Select();
                });
            WanfaUiFactory.SetButtonIcon(button,
                option.Selected ? BaibaoUiIcons.Confirm : option.IconPath ?? BaibaoUiIcons.Options);
            BaibaoUiFactory.SetSelected(button, option.Selected);
            Text label = button.GetComponentInChildren<Text>();
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 5;
            label.resizeTextMaxSize = 7;
            _optionButtons.Add(button);
            _options.Add(option);
        }

        _searchVisible = searchable && options.Count > SearchThreshold;
        _search.gameObject.SetActive(_searchVisible);
        _search.SetTextWithoutNotify(string.Empty);
        FilterOptions(string.Empty);
        _panel.transform.SetAsLastSibling();
        _panel.SetActive(true);
        _owner.interactable = false;
    }

    private void FilterOptions(string value)
    {
        string search = value.Trim();
        int visibleCount = 0;
        for (int i = 0; i < _optionButtons.Count; i++)
        {
            BaibaoMenuOption option = _options[i];
            string searchText = option.SearchText ?? option.Label;
            bool visible = search.Length == 0 ||
                           searchText.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            _optionButtons[i].gameObject.SetActive(visible);
            if (visible) visibleCount++;
        }

        _empty.gameObject.SetActive(visibleCount == 0);
        Resize(visibleCount);
    }

    private void Resize(int visibleCount)
    {
        int contentRows = Mathf.CeilToInt(visibleCount / (float)ColumnCount);
        int layoutCount = _searchVisible ? _options.Count : visibleCount;
        int layoutRows = Mathf.Max(1, Mathf.CeilToInt(layoutCount / (float)ColumnCount));
        int visibleRows = Mathf.Min(layoutRows, MaxVisibleRows);
        float contentHeight = ContentPadding * 2f + visibleRows * CellHeight +
                              Mathf.Max(0, visibleRows - 1) * GridSpacing;
        float scrollHeight = contentHeight + ViewportInset * 2f;
        bool scrollable = contentRows > MaxVisibleRows;

        WanfaUiFactory.SetLayout(_optionScrollRoot, ScrollWidth, scrollHeight);
        _scrollbarMask.SetActive(scrollable);
        _optionViewport.offsetMin = new Vector2(ViewportInset, ViewportInset);
        _optionViewport.offsetMax = new Vector2(
            scrollable ? -(WanfaUiFactory.OriginalVerticalScrollbarReservedWidth + ViewportInset) : -ViewportInset,
            -ViewportInset);
        _optionScroll.verticalNormalizedPosition = 1f;

        float panelHeight = PanelPadding * 2f + TitleHeight + scrollHeight + FooterHeight + PanelSpacing * 2f;
        if (_searchVisible) panelHeight += SearchHeight + PanelSpacing;
        WanfaUiFactory.SetLayout(_panel.transform, PanelWidth, panelHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_panel.transform);
    }

    private void ClearOptions()
    {
        for (int i = 0; i < _optionButtons.Count; i++)
        {
            _optionButtons[i].gameObject.SetActive(false);
            UnityEngine.Object.Destroy(_optionButtons[i].gameObject);
        }
        _optionButtons.Clear();
        _options.Clear();
    }

    public void Hide()
    {
        _panel.SetActive(false);
        _owner.interactable = true;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

internal sealed class UiOptionMenuOption
{
    public string Label;
    public string IconPath;
    public string SearchText;
    public bool Selected;
    public Action Select;
}

internal sealed class UiOptionMenuConfig
{
    public int ColumnCount = 2;
    public int SearchThreshold = 10;
    public int MaxVisibleRows = 5;
    public float PanelWidth = 272f;
    public float CellHeight = 24f;
    public string SearchPlaceholder;
    public string EmptyText;
    public string CloseText;
    public string CloseTooltipTitle;
    public string CloseTooltipDescription;
    public string DefaultIconPath = UiIcons.Options;
}

/// <summary>可搜索、自动限高并使用原版滚动条的通用模态选项菜单。</summary>
internal sealed class UiOptionMenu
{
    private const float GridSpacing = 4f;
    private const float TitleHeight = 20f;
    private const float SearchHeight = 22f;
    private const float CloseHeight = 22f;
    private const float FooterHeight = 30f;
    private const float ViewportInset = 6f;
    private const float ContentPadding = 4f;
    private const float PanelPadding = 6f;
    private const float PanelSpacing = 4f;

    private readonly UiOptionMenuConfig _config;
    private readonly float _innerWidth;
    private readonly float _scrollWidth;
    private readonly float _cellWidth;
    private readonly Text _title;
    private readonly InputField _search;
    private readonly UiScrollPane _optionsPane;
    private readonly UiEmptyState _empty;
    private readonly UiModal _modal;
    private readonly List<Button> _optionButtons = new();
    private readonly List<UiOptionMenuOption> _options = new();
    private bool _searchVisible;

    public UiOptionMenu(Transform parent, CanvasGroup owner, Transform scrollbarTemplate,
        UiOptionMenuConfig config)
    {
        _config = config;
        _innerWidth = config.PanelWidth - PanelPadding * 2f;
        _scrollWidth = _innerWidth - ViewportInset * 2f;
        _cellWidth = (_scrollWidth - ViewportInset * 2f - UiTheme.Current.Metrics.ScrollbarReservedWidth -
                      ContentPadding * 2f - GridSpacing * (config.ColumnCount - 1)) / config.ColumnCount;

        GameObject panel = UiLayout.Create(parent, "OptionMenu", false, config.PanelWidth, 120f, PanelSpacing,
            TextAnchor.UpperCenter);
        panel.transform.localPosition = new Vector3(0f, -4f);
        VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset((int)PanelPadding, (int)PanelPadding, (int)PanelPadding,
            (int)PanelPadding);
        panelLayout.childForceExpandWidth = false;
        Image background = panel.AddComponent<Image>();
        UiResources.ApplySurface(background, UiSurface.WindowEmpty);

        _title = UiElements.CreateText(panel.transform, "Title", string.Empty, _innerWidth, TitleHeight, 8,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        _search = UiSearchField.Create(panel.transform, "Search", string.Empty, config.SearchPlaceholder,
            _innerWidth, SearchHeight).Input;
        _search.onValueChanged.AddListener(FilterOptions);

        _optionsPane = UiScrollPane.CreateGrid(panel.transform, "Options", _scrollWidth, 64f,
            config.ColumnCount, new Vector2(_cellWidth, config.CellHeight), new Vector2(GridSpacing, GridSpacing));
        GridLayoutGroup optionLayout = _optionsPane.Content.GetComponent<GridLayoutGroup>();
        optionLayout.childAlignment = TextAnchor.UpperCenter;
        optionLayout.padding = new RectOffset((int)ContentPadding, (int)ContentPadding,
            (int)ContentPadding, (int)ContentPadding);
        _optionsPane.AttachOriginalScrollbar(scrollbarTemplate);
        _optionsPane.SetSurface(UiSurface.WindowInner, ViewportInset);
        _empty = new UiEmptyState(_optionsPane.Root, config.EmptyText, _scrollWidth - 8f, 24f);

        GameObject footer = UiLayout.Create(panel.transform, "Footer", true, _innerWidth, FooterHeight, 0f,
            TextAnchor.MiddleCenter);
        footer.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
        Button close = UiElements.CreateIconTextButton(footer.transform, "Close", UiIcons.Cancel,
            config.CloseText, _innerWidth - 12f, CloseHeight, Hide);
        UiTooltip.Set(close.gameObject, config.CloseTooltipTitle, config.CloseTooltipDescription);
        _modal = new UiModal(panel, owner);
    }

    public void Show(string title, IReadOnlyList<UiOptionMenuOption> options, bool searchable = false)
    {
        _title.text = title;
        ClearOptions();
        for (int i = 0; i < options.Count; i++)
        {
            UiOptionMenuOption option = options[i];
            Button button = UiElements.CreateIconTextButton(_optionsPane.Content, $"Option{i}",
                option.IconPath ?? _config.DefaultIconPath, option.Label, _cellWidth, _config.CellHeight, () =>
                {
                    Hide();
                    option.Select();
                });
            UiElements.SetButtonIcon(button,
                option.Selected ? UiIcons.Confirm : option.IconPath ?? _config.DefaultIconPath);
            UiStateStyle.SetSelected(button, option.Selected);
            Text label = button.GetComponentInChildren<Text>();
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 5;
            label.resizeTextMaxSize = 7;
            _optionButtons.Add(button);
            _options.Add(option);
        }

        _searchVisible = searchable && options.Count > _config.SearchThreshold;
        _search.gameObject.SetActive(_searchVisible);
        _search.SetTextWithoutNotify(string.Empty);
        FilterOptions(string.Empty);
        _modal.Show();
    }

    private void FilterOptions(string value)
    {
        string search = value.Trim();
        int visibleCount = 0;
        for (int i = 0; i < _optionButtons.Count; i++)
        {
            UiOptionMenuOption option = _options[i];
            string searchText = option.SearchText ?? option.Label;
            bool visible = search.Length == 0 ||
                           searchText.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            _optionButtons[i].gameObject.SetActive(visible);
            if (visible) visibleCount++;
        }

        _empty.SetVisible(visibleCount == 0);
        Resize(visibleCount);
    }

    private void Resize(int visibleCount)
    {
        int contentRows = Mathf.CeilToInt(visibleCount / (float)_config.ColumnCount);
        int layoutRows = Mathf.Max(1, contentRows);
        int visibleRows = Mathf.Min(layoutRows, _config.MaxVisibleRows);
        float contentHeight = ContentPadding * 2f + visibleRows * _config.CellHeight +
                              Mathf.Max(0, visibleRows - 1) * GridSpacing;
        float scrollHeight = contentHeight + ViewportInset * 2f;
        bool scrollable = contentRows > _config.MaxVisibleRows;

        _optionsPane.Resize(_scrollWidth, scrollHeight);
        _optionsPane.SetScrollbarVisible(scrollable);
        _optionsPane.ResetToTop();

        float panelHeight = PanelPadding * 2f + TitleHeight + scrollHeight + FooterHeight + PanelSpacing * 2f;
        if (_searchVisible) panelHeight += SearchHeight + PanelSpacing;
        UiLayout.SetSize(_modal.Panel.transform, _config.PanelWidth, panelHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_modal.Panel.transform);
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
        _modal.Hide();
    }
}

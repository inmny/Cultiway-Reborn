using System;
using System.Collections.Generic;
using Cultiway.Abstract;
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
    /// <summary>前三个文本值为本地化键时，用原版文本组件跟随语言切换。</summary>
    public bool LocalizeLabels;
}

/// <summary>可搜索、自动限高并使用原版滚动条的通用模态选项菜单。</summary>
internal sealed class UiOptionMenu
{
    /// <summary>完整文本放在可换行的提示正文，原版提示标题只有固定高度。</summary>
    private const string FullLabelKey = "Cultiway.UI.OptionMenu.FullLabel";
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
    private readonly UiWindowFrame _frame;
    private readonly float _innerWidth;
    private readonly float _scrollWidth;
    private readonly float _cellWidth;
    private readonly Text _title;
    private readonly InputField _search;
    private readonly UiScrollPane _optionsPane;
    private readonly UiEmptyState _empty;
    /// <summary>按每次打开的用途指定空目录文字，语言切换仍使用原版组件。</summary>
    private readonly LocalizedText _emptyLocale;
    private readonly UiModal _modal;
    /// <summary>复用长目录的按钮，关闭菜单时清除旧点击与提示。</summary>
    private readonly MonoObjPool<Button> _buttonPool;
    private readonly List<Button> _optionButtons = new();
    private readonly List<UiOptionMenuOption> _options = new();
    private bool _searchVisible;

    public UiOptionMenu(Transform parent, CanvasGroup owner, Transform scrollbarTemplate,
        UiOptionMenuConfig config)
    {
        _config = config;
        _frame = UiWindowFrame.CreateOuterSize(parent, "OptionMenu", config.PanelWidth, 120f, PanelPadding);
        _frame.Root.localPosition = new Vector3(0f, -4f);
        _innerWidth = config.PanelWidth - _frame.Insets.Horizontal;
        _scrollWidth = _innerWidth - ViewportInset * 2f;
        float scrollbarSlotWidth = UiTheme.Current.Metrics.ScrollbarSlotWidth;
        _cellWidth = (_scrollWidth - ViewportInset * 2f - scrollbarSlotWidth -
                      ContentPadding * 2f - GridSpacing * (config.ColumnCount - 1)) / config.ColumnCount;

        VerticalLayoutGroup panelLayout = _frame.Content.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing = PanelSpacing;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = false;
        panelLayout.childForceExpandHeight = false;

        _title = UiElements.CreateText(_frame.Content, "Title", string.Empty, _innerWidth, TitleHeight, 8,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        _title.resizeTextForBestFit = true;
        _title.resizeTextMinSize = 6;
        _title.resizeTextMaxSize = 8;
        _search = UiSearchField.Create(_frame.Content, "Search", string.Empty, config.SearchPlaceholder,
            _innerWidth, SearchHeight).Input;
        _search.onValueChanged.AddListener(FilterOptions);

        _optionsPane = UiScrollPane.CreateGrid(_frame.Content, "Options", _scrollWidth, 64f,
            config.ColumnCount, new Vector2(_cellWidth, config.CellHeight), new Vector2(GridSpacing, GridSpacing));
        GridLayoutGroup optionLayout = _optionsPane.Content.GetComponent<GridLayoutGroup>();
        optionLayout.childAlignment = TextAnchor.UpperCenter;
        optionLayout.padding = new RectOffset((int)ContentPadding, (int)ContentPadding,
            (int)ContentPadding, (int)ContentPadding);
        _optionsPane.AttachOriginalScrollbar(scrollbarTemplate);
        _optionsPane.SetSurface(UiSurface.WindowInner, ViewportInset);
        _empty = new UiEmptyState(_optionsPane.Root, config.EmptyText, _scrollWidth - 8f, 24f);
        Button template = UiElements.CreateIconTextButton(_optionsPane.Content, "OptionTemplate", config.DefaultIconPath,
            string.Empty, _cellWidth, config.CellHeight, null);
        Text templateLabel = template.GetComponentInChildren<Text>();
        templateLabel.resizeTextForBestFit = true;
        templateLabel.resizeTextMinSize = 5;
        templateLabel.resizeTextMaxSize = 7;
        template.gameObject.SetActive(false);
        _buttonPool = new MonoObjPool<Button>(template, _optionsPane.Content);

        GameObject footer = UiLayout.Create(_frame.Content, "Footer", true, _innerWidth, FooterHeight, 0f,
            TextAnchor.MiddleCenter);
        footer.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(6, 6, 4, 4);
        Button close = UiElements.CreateIconTextButton(footer.transform, "Close", UiIcons.Cancel,
            config.CloseText, _innerWidth - 12f, CloseHeight, Hide);
        UiTooltip.Set(close.gameObject, config.CloseTooltipTitle, config.CloseTooltipDescription);
        if (config.LocalizeLabels)
        {
            ((Text)_search.placeholder).gameObject.AddComponent<LocalizedText>().setKeyAndUpdate(config.SearchPlaceholder);
            _emptyLocale = _empty.Text.gameObject.AddComponent<LocalizedText>();
            _emptyLocale.setKeyAndUpdate(config.EmptyText);
            close.GetComponentInChildren<Text>().gameObject.AddComponent<LocalizedText>().setKeyAndUpdate(config.CloseText);
        }
        _modal = new UiModal(_frame.Root.gameObject, owner);
    }

    /// <summary>使用同一搜索和滚动展示选项。</summary>
    /// <param name="title">已翻译的菜单标题。</param><param name="options">本次可选项。</param>
    /// <param name="searchable">候选达到门槛时显示搜索。</param>
    /// <param name="emptyText">目录本身为空时的专用文字，遵循配置的本地化方式；筛不到选项仍用默认文字。</param>
    public void Show(string title, IReadOnlyList<UiOptionMenuOption> options, bool searchable = false, string emptyText = null)
    {
        _title.text = title;
        if (_title.preferredWidth > _innerWidth) UiTooltip.Set(_title.gameObject, FullLabelKey, title);
        else UiTooltip.Clear(_title.gameObject);
        string empty = options.Count == 0 ? emptyText ?? _config.EmptyText : _config.EmptyText;
        if (_emptyLocale != null) _emptyLocale.setKeyAndUpdate(empty);
        else _empty.Text.text = empty;
        ClearOptions();
        for (int i = 0; i < options.Count; i++)
        {
            UiOptionMenuOption option = options[i];
            Button button = _buttonPool.GetNext();
            button.name = $"Option{i}";
            Text label = button.GetComponentInChildren<Text>(true);
            label.text = option.Label;
            if (label.preferredWidth > _cellWidth - 18f) UiTooltip.Set(button.gameObject, FullLabelKey, option.Label);
            button.onClick.AddListener(() =>
            {
                Hide();
                option.Select();
            });
            UiElements.SetButtonIcon(button,
                option.Selected ? UiIcons.Confirm : option.IconPath ?? _config.DefaultIconPath);
            UiStateStyle.SetSelected(button, option.Selected);
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

        float frameContentHeight = TitleHeight + scrollHeight + FooterHeight + PanelSpacing * 2f;
        if (_searchVisible) frameContentHeight += SearchHeight + PanelSpacing;
        _frame.ResizeContent(_innerWidth, frameContentHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_frame.Content);
    }

    /// <summary>归还按钮之前解除旧资料及提示，供下一个目录使用。</summary>
    private void ClearOptions()
    {
        foreach (Button button in _optionButtons)
        {
            button.onClick.RemoveAllListeners();
            UiTooltip.Clear(button.gameObject);
        }
        if (_optionButtons.Count > 0) _buttonPool.Clear();
        _optionButtons.Clear();
        _options.Clear();
    }

    /// <summary>保留窗口，关闭弹层并释放本次候选的回调。</summary>
    public void Hide()
    {
        _modal.Hide();
        UiTooltip.Clear(_title.gameObject);
        ClearOptions();
    }

    /// <summary>父窗口关闭或换世界时关闭弹层，释放当前候选。</summary>
    public void Clear() => Hide();
}

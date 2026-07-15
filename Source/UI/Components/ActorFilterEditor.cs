using System;
using System.Collections.Generic;
using System.Text;
using Cultiway.Core.ActorFiltering;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.Components;

/// <summary>可嵌入不同配置窗口的角色逻辑筛选表达式编辑器。</summary>
internal sealed class ActorFilterEditor : IDisposable
{
    private const float CategoryWidth = 132f;
    private const float SymbolWidth = 94f;
    private const float CategoryButtonWidth = 106f;
    private const float SymbolContentWidth = 68f;
    private const float OptionCardWidth = 82f;
    private const float OptionCardHeight = 56f;
    private const float BannerScale = 0.5f;

    private ActorFilterSettings _settings;
    private string _emptyExpressionKey;
    private string _semanticsKey;
    private readonly Button[] _symbolButtons = new Button[6];
    private readonly List<Button> _predicateButtons = new();
    private Text _expressionText;
    private Text _semanticsText;
    private TipButton _expressionTip;
    private Button _undoExpressionButton;
    private Button _clearExpressionButton;
    private Transform _categoryContent;
    private Transform _optionContent;
    private Button[] _categoryButtons = Array.Empty<Button>();
    private ActorFilterDescriptor[] _filterTypes = Array.Empty<ActorFilterDescriptor>();
    private int _filterTypeIndex;

    public ActorFilterEditor(Transform root, float width, float expressionHeight, float browserHeight,
        Transform scrollbarMaskTemplate, ActorFilterSettings settings, string emptyExpressionKey,
        string semanticsKey)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _emptyExpressionKey = emptyExpressionKey;
        _semanticsKey = semanticsKey;
        CreateExpressionBar(root, width, expressionHeight);
        CreateFilterBrowser(root, width, browserHeight, scrollbarMaskTemplate);
        _settings.Changed += Refresh;
        RefreshWorldOptions();
    }

    /// <summary>将同一个编辑器切换到另一份世界工具配置，并完整刷新当前世界候选项。</summary>
    public void Bind(ActorFilterSettings settings, string emptyExpressionKey, string semanticsKey)
    {
        if (_settings != settings)
        {
            _settings.Changed -= Refresh;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.Changed += Refresh;
        }
        _emptyExpressionKey = emptyExpressionKey;
        _semanticsKey = semanticsKey;
        _semanticsText.text = _semanticsKey.Localize();
        RefreshWorldOptions();
    }

    public void Dispose()
    {
        _settings.Changed -= Refresh;
    }

    /// <summary>重新读取当前世界的过滤类别和候选对象，并刷新表达式状态。</summary>
    public void RefreshWorldOptions()
    {
        var previousId = _filterTypes.Length > 0 && _filterTypeIndex < _filterTypes.Length
            ? _filterTypes[_filterTypeIndex].Id
            : null;
        _filterTypes = new ActorFilterDescriptor[ActorFilterCatalog.Types.Count];
        for (var i = 0; i < _filterTypes.Length; i++) _filterTypes[i] = ActorFilterCatalog.Types[i];
        _filterTypeIndex = Array.FindIndex(_filterTypes, descriptor => descriptor.Id == previousId);
        if (_filterTypeIndex < 0) _filterTypeIndex = 0;
        RebuildCategoryButtons();
        RebuildOptionCards();
        Refresh();
    }

    private void CreateExpressionBar(Transform root, float width, float height)
    {
        var bar = UiLayout.Create(root, "ExpressionBar", true, width, height, 4f,
            TextAnchor.MiddleLeft);
        Image barBackground = bar.AddComponent<Image>();
        UiResources.ApplySurface(barBackground, UiSurface.WindowInner);
        _expressionText = UiElements.CreateText(bar.transform, "Expression", string.Empty,
            width - 68f, height, 7, TextAnchor.MiddleLeft);
        UiTooltip.Set(_expressionText.gameObject,
            "Cultiway.ActorFilter.UI.Section.Expression", _emptyExpressionKey);
        _expressionTip = _expressionText.GetComponent<TipButton>();

        _undoExpressionButton = UiElements.CreateIconButton(bar.transform, "Undo", UiIcons.Undo,
            30f, 30f, _settings.RemoveLastToken, 5f);
        UiTooltip.Set(_undoExpressionButton.gameObject,
            "Cultiway.ActorFilter.UI.Action.UndoExpression",
            "Cultiway.ActorFilter.UI.Action.UndoExpression.Description");

        _clearExpressionButton = UiElements.CreateIconButton(bar.transform, "Clear", UiIcons.Reset,
            30f, 30f, _settings.ClearExpression, 5f);
        UiTooltip.Set(_clearExpressionButton.gameObject,
            "Cultiway.ActorFilter.UI.Action.ClearExpression",
            "Cultiway.ActorFilter.UI.Action.ClearExpression.Description");
    }

    private void CreateFilterBrowser(Transform root, float width, float height, Transform scrollbarMaskTemplate)
    {
        var browser = UiLayout.Create(root, "FilterBrowser", true, width, height, 4f,
            TextAnchor.UpperLeft);
        var optionWidth = width - CategoryWidth - SymbolWidth - 8f;

        UiScrollPane categories = UiScrollPane.CreateVertical(browser.transform, "Categories",
            CategoryWidth, height);
        categories.AttachOriginalScrollbar(scrollbarMaskTemplate);
        categories.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs);
        _categoryContent = categories.Content;
        NestedVerticalScrollRelay.Attach(_categoryContent);

        UiScrollPane options = UiScrollPane.CreateGrid(browser.transform, "Options",
            optionWidth, height, 3, new Vector2(OptionCardWidth, OptionCardHeight), new Vector2(4f, 4f));
        options.AttachOriginalScrollbar(scrollbarMaskTemplate);
        options.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs);
        _optionContent = options.Content;
        NestedVerticalScrollRelay.Attach(_optionContent);

        UiScrollPane symbolsPane = UiScrollPane.CreateVertical(browser.transform, "Symbols", SymbolWidth, height);
        symbolsPane.AttachOriginalScrollbar(scrollbarMaskTemplate);
        symbolsPane.SetSurface(UiSurface.WindowInner, UiTheme.Current.Metrics.SpacingXs);
        Transform symbols = symbolsPane.Content;
        NestedVerticalScrollRelay.Attach(symbols);
        symbols.GetComponent<VerticalLayoutGroup>().spacing = 4f;
        UiElements.CreateText(symbols, "Title",
            "Cultiway.ActorFilter.UI.Section.Symbols".Localize(), SymbolContentWidth, 18f, 7,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        _symbolButtons[(int)ActorFilterTokenKind.Not] = CreateSymbolButton(symbols,
            "Not", "Not", ActorFilterTokenKind.Not);
        _symbolButtons[(int)ActorFilterTokenKind.And] = CreateSymbolButton(symbols,
            "And", "And", ActorFilterTokenKind.And);
        _symbolButtons[(int)ActorFilterTokenKind.Or] = CreateSymbolButton(symbols,
            "Or", "Or", ActorFilterTokenKind.Or);
        _symbolButtons[(int)ActorFilterTokenKind.LeftParenthesis] = CreateSymbolButton(symbols,
            "LeftParenthesis", "LeftParenthesis", ActorFilterTokenKind.LeftParenthesis);
        _symbolButtons[(int)ActorFilterTokenKind.RightParenthesis] = CreateSymbolButton(symbols,
            "RightParenthesis", "RightParenthesis", ActorFilterTokenKind.RightParenthesis);
        _semanticsText = UiElements.CreateText(symbols, "Semantics", _semanticsKey.Localize(),
            SymbolContentWidth, 60f, 6, TextAnchor.UpperLeft);
    }

    private Button CreateSymbolButton(Transform parent, string name, string localeSuffix,
        ActorFilterTokenKind kind)
    {
        var localeKey = $"Cultiway.ActorFilter.UI.Symbol.{localeSuffix}";
        var button = UiElements.CreateButton(parent, name, localeKey.Localize(), SymbolContentWidth, 32f,
            () => _settings.AppendSymbol(kind));
        UiTooltip.Set(button.gameObject, localeKey, $"{localeKey}.Description");
        return button;
    }

    private void Refresh()
    {
        if (_expressionText == null) return;
        var state = _settings.ExpressionState;
        if (state.IsEmpty)
        {
            _expressionText.text = _emptyExpressionKey.Localize();
            _expressionText.alignment = TextAnchor.MiddleCenter;
            _expressionText.color = new Color(1f, 1f, 1f, 0.72f);
        }
        else
        {
            _expressionText.text = FormatExpression(_settings.Expression);
            var errorKey = GetErrorKey(state.Error);
            if (!state.IsComplete && errorKey != null) _expressionText.text += $"\n{errorKey.Localize()}";
            _expressionText.alignment = TextAnchor.MiddleLeft;
            _expressionText.color = state.IsComplete ? Color.white : new Color(1f, 0.82f, 0.25f, 1f);
        }
        _expressionTip.textOnClickDescription = _expressionText.text;

        var hasTokens = _settings.Expression.Count > 0;
        _undoExpressionButton.interactable = hasTokens;
        _clearExpressionButton.interactable = hasTokens;
        for (var i = 0; i < _symbolButtons.Length; i++)
        {
            if (_symbolButtons[i] != null)
                _symbolButtons[i].interactable = state.CanAppend((ActorFilterTokenKind)i);
        }
        var canAppendPredicate = state.CanAppend(ActorFilterTokenKind.Predicate);
        for (var i = 0; i < _predicateButtons.Count; i++)
        {
            if (_predicateButtons[i] != null) _predicateButtons[i].interactable = canAppendPredicate;
        }
        RefreshCategorySelection();
    }

    private static string GetErrorKey(ActorFilterExpressionError error)
    {
        return error switch
        {
            ActorFilterExpressionError.Invalid => "Cultiway.ActorFilter.UI.Expression.Invalid",
            ActorFilterExpressionError.ExpectCondition => "Cultiway.ActorFilter.UI.Expression.ExpectCondition",
            ActorFilterExpressionError.ExpectRightParenthesis =>
                "Cultiway.ActorFilter.UI.Expression.ExpectRightParenthesis",
            _ => null
        };
    }

    private static string FormatExpression(IReadOnlyList<ActorFilterToken> expression)
    {
        var result = new StringBuilder();
        var previous = ActorFilterTokenKind.Or;
        for (var i = 0; i < expression.Count; i++)
        {
            var token = expression[i];
            if (i > 0 && token.Kind != ActorFilterTokenKind.RightParenthesis &&
                previous != ActorFilterTokenKind.LeftParenthesis)
                result.Append(' ');

            switch (token.Kind)
            {
                case ActorFilterTokenKind.Predicate:
                    var descriptor = ActorFilterCatalog.Get(token.Predicate.TypeId);
                    var typeName = descriptor == null ? token.Predicate.TypeId : descriptor.NameKey.Localize();
                    result.Append(typeName).Append('「').Append(token.Predicate.DisplayName).Append('」');
                    break;
                case ActorFilterTokenKind.Not:
                    result.Append("Cultiway.ActorFilter.UI.Symbol.Not".Localize());
                    break;
                case ActorFilterTokenKind.And:
                    result.Append("Cultiway.ActorFilter.UI.Symbol.And".Localize());
                    break;
                case ActorFilterTokenKind.Or:
                    result.Append("Cultiway.ActorFilter.UI.Symbol.Or".Localize());
                    break;
                case ActorFilterTokenKind.LeftParenthesis:
                    result.Append('(');
                    break;
                case ActorFilterTokenKind.RightParenthesis:
                    result.Append(')');
                    break;
            }
            previous = token.Kind;
        }
        return result.ToString();
    }

    private void RebuildCategoryButtons()
    {
        UiLayout.ClearChildren(_categoryContent);
        _categoryButtons = new Button[_filterTypes.Length];
        for (var i = 0; i < _filterTypes.Length; i++)
        {
            var index = i;
            var descriptor = _filterTypes[i];
            var label = descriptor.NameKey.Localize();
            var button = string.IsNullOrEmpty(descriptor.IconPath)
                ? UiElements.CreateButton(_categoryContent, $"Category_{i}", label, CategoryButtonWidth, 24f,
                    () => SelectCategory(index))
                : UiElements.CreateIconTextButton(_categoryContent, $"Category_{i}", descriptor.IconPath,
                    label, CategoryButtonWidth, 24f, () => SelectCategory(index));
            UiTooltip.Set(button.gameObject, label,
                "Cultiway.ActorFilter.UI.Filter.Category.Description".Localize());
            _categoryButtons[i] = button;
        }
        RefreshCategorySelection();
    }

    private void SelectCategory(int index)
    {
        if (index < 0 || index >= _filterTypes.Length || index == _filterTypeIndex) return;
        _filterTypeIndex = index;
        RefreshCategorySelection();
        RebuildOptionCards();
    }

    private void RefreshCategorySelection()
    {
        for (var i = 0; i < _categoryButtons.Length; i++)
        {
            if (_categoryButtons[i] == null) continue;
            UiStateStyle.SetSelected(_categoryButtons[i], i == _filterTypeIndex);
        }
    }

    private void RebuildOptionCards()
    {
        UiLayout.ClearChildren(_optionContent);
        _predicateButtons.Clear();
        if (_filterTypes.Length == 0) return;

        var descriptor = _filterTypes[_filterTypeIndex];
        var options = descriptor.GetOptions();
        if (options.Count == 0)
        {
            UiElements.CreateText(_optionContent, "NoOptions",
                "Cultiway.ActorFilter.UI.Filter.NoOptions".Localize(), OptionCardWidth, OptionCardHeight, 6,
                TextAnchor.MiddleCenter);
            return;
        }

        for (var i = 0; i < options.Count; i++) CreateOptionCard(descriptor, options[i], i);
        Refresh();
    }

    private void CreateOptionCard(ActorFilterDescriptor descriptor, ActorFilterOption option, int index)
    {
        var card = new GameObject($"Option_{index}", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        card.transform.SetParent(_optionContent, false);
        UiLayout.SetSize(card.transform, OptionCardWidth, OptionCardHeight);
        var cardImage = card.GetComponent<Image>();
        UiResources.ApplySurface(cardImage, UiSurface.Button);

        UnityAction append = () => AppendPredicate(descriptor, option);
        var cardButton = card.GetComponent<Button>();
        cardButton.onClick.AddListener(append);
        _predicateButtons.Add(cardButton);

        var visual = new GameObject("Visual", typeof(RectTransform));
        visual.transform.SetParent(card.transform, false);
        var visualRect = visual.GetComponent<RectTransform>();
        visualRect.anchorMin = Vector2.zero;
        visualRect.anchorMax = Vector2.one;
        visualRect.offsetMin = new Vector2(4f, 17f);
        visualRect.offsetMax = new Vector2(-4f, -3f);

        if (!TryCreateMetaBanner(visual.transform, descriptor, option, append))
            CreateOptionIcon(visual.transform, option.IconPath ?? descriptor.IconPath);

        var label = UiElements.CreateText(card.transform, "Label", option.DisplayName,
            OptionCardWidth - 6f, 15f, 6, TextAnchor.MiddleCenter, FontStyle.Bold);
        var labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.offsetMin = new Vector2(3f, 1f);
        labelRect.offsetMax = new Vector2(-3f, 16f);
        label.raycastTarget = false;

        var description = string.Format("Cultiway.ActorFilter.UI.Filter.Option.Description".Localize(),
            descriptor.NameKey.Localize());
        UiTooltip.Set(card, option.DisplayName, description);
    }

    private bool TryCreateMetaBanner(Transform parent, ActorFilterDescriptor descriptor,
        ActorFilterOption option, UnityAction append)
    {
        if (descriptor.MetaType == MetaType.None || option.MetaObject == null || !option.MetaObject.isAlive())
            return false;
        var customization = AssetManager.meta_customization_library.getAsset(descriptor.MetaType);
        if (customization?.get_banner == null) return false;

        var banner = customization.get_banner(customization, option.MetaObject, parent);
        if (banner == null) return false;
        banner.gameObject.SetActive(true);
        var bannerRect = banner.gameObject.GetComponent<RectTransform>();
        bannerRect.anchorMin = bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRect.anchoredPosition = Vector2.zero;
        var scale = new Vector3(BannerScale, BannerScale, 1f);
        banner.transform.localScale = scale;
        var hover = banner.gameObject.GetComponent<UiButtonHoverAnimation>();
        if (hover != null) hover.default_scale = scale;
        var tip = banner.gameObject.GetComponent<TipButton>();
        if (tip != null) tip.setDefaultScale(scale);

        var buttons = banner.gameObject.GetComponentsInChildren<Button>(true);
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i].onClick.AddListener(append);
            _predicateButtons.Add(buttons[i]);
        }
        return true;
    }

    private static void CreateOptionIcon(Transform parent, string iconPath)
    {
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(parent, false);
        var rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(30f, 30f);
        rect.anchoredPosition = Vector2.zero;
        var image = icon.GetComponent<Image>();
        image.sprite = UiResources.GetSprite(string.IsNullOrEmpty(iconPath) ? UiIcons.Info : iconPath);
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void AppendPredicate(ActorFilterDescriptor descriptor, ActorFilterOption option)
    {
        if (option.MetaObject != null && !option.MetaObject.isAlive()) return;
        _settings.AppendPredicate(new ActorFilterEntry(descriptor.Id, option.Id, option.DisplayName));
    }

}

using System;
using System.Collections.Generic;
using System.Text;
using Cultiway.Core.Progression;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI;

/// <summary>配置升级雨进阶方式、数值以及按逻辑表达式组合的过滤条件。</summary>
public sealed class WindowUpgradeRainConfig : AbstractWideWindow<WindowUpgradeRainConfig>
{
    public const string Id = "Cultiway.UI.WindowUpgradeRainConfig";
    public static readonly Vector2 WindowSize = new(600f, 360f);

    private const float RootWidth = 520f;
    private const float RootHeight = 318f;
    private const float TopHeight = 94f;
    private const float ExpressionHeight = 40f;
    private const float BrowserHeight = 176f;
    private const float CategoryWidth = 132f;
    private const float OptionWidth = 286f;
    private const float SymbolWidth = 94f;
    private const float ModeButtonWidth = 100f;
    private const float CategoryButtonWidth = 106f;
    private const float SymbolContentWidth = 68f;
    private const float OptionCardWidth = 82f;
    private const float OptionCardHeight = 56f;
    private const float BannerScale = 0.5f;

    private readonly Button[] _modeButtons = new Button[5];
    private readonly Button[] _symbolButtons = new Button[6];
    private readonly List<Button> _predicateButtons = new();
    private InputField _amountInput;
    private Text _amountSummary;
    private Text _expressionText;
    private TipButton _expressionTip;
    private Button _undoExpressionButton;
    private Button _clearExpressionButton;
    private Transform _categoryContent;
    private Transform _optionContent;
    private Button[] _categoryButtons = Array.Empty<Button>();
    private UpgradeRainFilterDescriptor[] _filterTypes = Array.Empty<UpgradeRainFilterDescriptor>();
    private int _filterTypeIndex;

    protected override void Init()
    {
        var originalScrollView = BackgroundTransform.Find("Scroll View");
        var scrollbarMaskTemplate = originalScrollView.Find("Scrollbar Vertical Mask");
        originalScrollView.gameObject.SetActive(false);
        var root = WanfaUiFactory.CreateLayout(BackgroundTransform, "UpgradeRainRoot", false,
            RootWidth, RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        CreateTopSection(root.transform);
        CreateExpressionBar(root.transform);
        CreateFilterBrowser(root.transform, scrollbarMaskTemplate);

        UpgradeRainService.Settings.Changed += Refresh;
        RefreshFilterTypes();
        Refresh();
    }

    public override void OnNormalEnable()
    {
        RefreshFilterTypes();
        Refresh();
    }

    private void OnDestroy()
    {
        UpgradeRainService.Settings.Changed -= Refresh;
    }

    private void CreateTopSection(Transform root)
    {
        var top = WanfaUiFactory.CreateLayout(root, "Top", false, RootWidth, TopHeight, 8f);
        WanfaUiFactory.CreateText(top.transform, "ModeTitle",
            "Cultiway.UpgradeRain.UI.Section.Mode".Localize(), RootWidth, 18f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);

        var modeRow = WanfaUiFactory.CreateLayout(top.transform, "Modes", true, RootWidth, 28f, 4f);
        _modeButtons[(int)UpgradeRainMode.FixedMinor] = CreateModeButton(modeRow.transform,
            "FixedMinor", "Cultiway.UpgradeRain.UI.Mode.FixedMinor", UpgradeRainMode.FixedMinor);
        _modeButtons[(int)UpgradeRainMode.CappedMinor] = CreateModeButton(modeRow.transform,
            "CappedMinor", "Cultiway.UpgradeRain.UI.Mode.CappedMinor", UpgradeRainMode.CappedMinor);
        _modeButtons[(int)UpgradeRainMode.FixedMajor] = CreateModeButton(modeRow.transform,
            "FixedMajor", "Cultiway.UpgradeRain.UI.Mode.FixedMajor", UpgradeRainMode.FixedMajor);
        _modeButtons[(int)UpgradeRainMode.CappedMajor] = CreateModeButton(modeRow.transform,
            "CappedMajor", "Cultiway.UpgradeRain.UI.Mode.CappedMajor", UpgradeRainMode.CappedMajor);
        _modeButtons[(int)UpgradeRainMode.ToRealm] = CreateModeButton(modeRow.transform,
            "ToRealm", "Cultiway.UpgradeRain.UI.Mode.ToRealm", UpgradeRainMode.ToRealm);

        var amountRow = WanfaUiFactory.CreateLayout(top.transform, "Amount", true, RootWidth, 28f, 4f);
        var decrease = WanfaUiFactory.CreateIconButton(amountRow.transform, "Decrease", WanfaUiIcons.Remove,
            28f, 26f, () => UpgradeRainService.Settings.SetAmount(UpgradeRainService.Settings.Amount - 1));
        WanfaUiFactory.SetTooltip(decrease.gameObject, "Cultiway.UpgradeRain.UI.Action.Decrease",
            "Cultiway.UpgradeRain.UI.Action.Decrease.Description");

        _amountInput = WanfaUiFactory.CreateInput(amountRow.transform, "AmountInput", "1",
            "Cultiway.UpgradeRain.UI.Placeholder.Amount".Localize(), 80f, 26f);
        _amountInput.contentType = InputField.ContentType.IntegerNumber;
        _amountInput.characterLimit = 5;
        _amountInput.onEndEdit.AddListener(CommitAmountInput);

        var increase = WanfaUiFactory.CreateIconButton(amountRow.transform, "Increase", WanfaUiIcons.Add,
            28f, 26f, () => UpgradeRainService.Settings.SetAmount(UpgradeRainService.Settings.Amount + 1));
        WanfaUiFactory.SetTooltip(increase.gameObject, "Cultiway.UpgradeRain.UI.Action.Increase",
            "Cultiway.UpgradeRain.UI.Action.Increase.Description");
        _amountSummary = WanfaUiFactory.CreateText(amountRow.transform, "AmountSummary", string.Empty,
            372f, 26f, 7, TextAnchor.MiddleLeft);
    }

    private static Button CreateModeButton(Transform parent, string name, string localeKey,
        UpgradeRainMode mode)
    {
        var button = WanfaUiFactory.CreateButton(parent, name, localeKey.Localize(), ModeButtonWidth, 26f,
            () => UpgradeRainService.Settings.SetMode(mode));
        WanfaUiFactory.SetTooltip(button.gameObject, localeKey, $"{localeKey}.Description");
        return button;
    }

    private void CreateExpressionBar(Transform root)
    {
        var bar = WanfaUiFactory.CreateLayout(root, "ExpressionBar", true, RootWidth,
            ExpressionHeight, 4f, TextAnchor.MiddleLeft);
        SetPaneBackground(bar);
        _expressionText = WanfaUiFactory.CreateText(bar.transform, "Expression", string.Empty,
            452f, ExpressionHeight, 7, TextAnchor.MiddleLeft);
        WanfaUiFactory.SetTooltip(_expressionText.gameObject,
            "Cultiway.UpgradeRain.UI.Section.Expression",
            "Cultiway.UpgradeRain.UI.Expression.Empty");
        _expressionTip = _expressionText.GetComponent<TipButton>();

        _undoExpressionButton = WanfaUiFactory.CreateIconButton(bar.transform, "Undo", WanfaUiIcons.Undo,
            30f, 30f, UpgradeRainService.Settings.RemoveLastToken, 5f);
        WanfaUiFactory.SetTooltip(_undoExpressionButton.gameObject,
            "Cultiway.UpgradeRain.UI.Action.UndoExpression",
            "Cultiway.UpgradeRain.UI.Action.UndoExpression.Description");

        _clearExpressionButton = WanfaUiFactory.CreateIconButton(bar.transform, "Clear", WanfaUiIcons.Reset,
            30f, 30f, UpgradeRainService.Settings.ClearExpression, 5f);
        WanfaUiFactory.SetTooltip(_clearExpressionButton.gameObject,
            "Cultiway.UpgradeRain.UI.Action.ClearExpression",
            "Cultiway.UpgradeRain.UI.Action.ClearExpression.Description");
    }

    private void CreateFilterBrowser(Transform root, Transform scrollbarMaskTemplate)
    {
        var browser = WanfaUiFactory.CreateLayout(root, "FilterBrowser", true, RootWidth,
            BrowserHeight, 4f, TextAnchor.UpperLeft);

        _categoryContent = WanfaUiFactory.CreateScrollContent(browser.transform, "Categories",
            CategoryWidth, BrowserHeight);
        WanfaUiFactory.AttachOriginalVerticalScrollbar(_categoryContent, scrollbarMaskTemplate);
        SetScrollPaneBackground(_categoryContent.parent.parent.gameObject);

        _optionContent = WanfaUiFactory.CreateScrollGridContent(browser.transform, "Options",
            OptionWidth, BrowserHeight, 3, new Vector2(OptionCardWidth, OptionCardHeight), new Vector2(4f, 4f));
        WanfaUiFactory.AttachOriginalVerticalScrollbar(_optionContent, scrollbarMaskTemplate);
        SetScrollPaneBackground(_optionContent.parent.parent.gameObject);

        var symbols = WanfaUiFactory.CreateScrollContent(browser.transform, "Symbols",
            SymbolWidth, BrowserHeight);
        WanfaUiFactory.AttachOriginalVerticalScrollbar(symbols, scrollbarMaskTemplate);
        var symbolLayout = symbols.GetComponent<VerticalLayoutGroup>();
        symbolLayout.spacing = 4f;
        SetScrollPaneBackground(symbols.parent.parent.gameObject);
        WanfaUiFactory.CreateText(symbols, "Title",
            "Cultiway.UpgradeRain.UI.Section.Symbols".Localize(), SymbolContentWidth, 18f, 7,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        _symbolButtons[(int)UpgradeRainFilterTokenKind.Not] = CreateSymbolButton(symbols,
            "Not", "Not", UpgradeRainFilterTokenKind.Not);
        _symbolButtons[(int)UpgradeRainFilterTokenKind.And] = CreateSymbolButton(symbols,
            "And", "And", UpgradeRainFilterTokenKind.And);
        _symbolButtons[(int)UpgradeRainFilterTokenKind.Or] = CreateSymbolButton(symbols,
            "Or", "Or", UpgradeRainFilterTokenKind.Or);
        _symbolButtons[(int)UpgradeRainFilterTokenKind.LeftParenthesis] = CreateSymbolButton(symbols,
            "LeftParenthesis", "LeftParenthesis", UpgradeRainFilterTokenKind.LeftParenthesis);
        _symbolButtons[(int)UpgradeRainFilterTokenKind.RightParenthesis] = CreateSymbolButton(symbols,
            "RightParenthesis", "RightParenthesis", UpgradeRainFilterTokenKind.RightParenthesis);
        WanfaUiFactory.CreateText(symbols, "Semantics",
            "Cultiway.UpgradeRain.UI.Filter.Semantics".Localize(), SymbolContentWidth, 60f, 6,
            TextAnchor.UpperLeft);
    }

    private static Button CreateSymbolButton(Transform parent, string name, string localeSuffix,
        UpgradeRainFilterTokenKind kind)
    {
        var localeKey = $"Cultiway.UpgradeRain.UI.Symbol.{localeSuffix}";
        var button = WanfaUiFactory.CreateButton(parent, name, localeKey.Localize(), SymbolContentWidth, 32f,
            () => UpgradeRainService.Settings.AppendSymbol(kind));
        WanfaUiFactory.SetTooltip(button.gameObject, localeKey, $"{localeKey}.Description");
        return button;
    }

    private void CommitAmountInput(string value)
    {
        if (int.TryParse(value, out var amount)) UpgradeRainService.Settings.SetAmount(amount);
        Refresh();
    }

    private void Refresh()
    {
        if (_amountInput == null) return;
        var settings = UpgradeRainService.Settings;
        for (var i = 0; i < _modeButtons.Length; i++)
        {
            _modeButtons[i].GetComponent<Image>().sprite = SpriteTextureLoader.getSprite(
                i == (int)settings.Mode ? "ui/special/button2" : "ui/special/button");
        }

        _amountInput.text = settings.Amount.ToString();
        _amountSummary.text = GetAmountSummary(settings);
        RefreshExpressionControls();
        RefreshCategorySelection();
    }

    private static string GetAmountSummary(UpgradeRainSettings settings)
    {
        if (settings.Mode != UpgradeRainMode.ToRealm)
        {
            var key = settings.Mode switch
            {
                UpgradeRainMode.FixedMinor => "Cultiway.UpgradeRain.UI.Format.FixedMinor",
                UpgradeRainMode.CappedMinor => "Cultiway.UpgradeRain.UI.Format.CappedMinor",
                UpgradeRainMode.CappedMajor => "Cultiway.UpgradeRain.UI.Format.CappedMajor",
                _ => "Cultiway.UpgradeRain.UI.Format.FixedMajor"
            };
            return string.Format(key.Localize(), settings.Amount);
        }

        if (settings.Expression.Count == 1 &&
            settings.Expression[0].Kind == UpgradeRainFilterTokenKind.Predicate &&
            settings.Expression[0].Predicate.TypeId == UpgradeRainFilterCatalog.CultisysTypeId)
        {
            var cultisys = ProgressionService.GetRegistered(settings.Expression[0].Predicate.ValueId);
            var targetLevel = settings.Amount - 1;
            if (cultisys != null && targetLevel >= 0 && targetLevel < cultisys.LevelNumber)
            {
                return string.Format("Cultiway.UpgradeRain.UI.Format.TargetRealmNamed".Localize(),
                    settings.Amount, cultisys.GetLevelName(targetLevel));
            }
        }
        return string.Format("Cultiway.UpgradeRain.UI.Format.TargetRealm".Localize(), settings.Amount);
    }

    private void RefreshExpressionControls()
    {
        var settings = UpgradeRainService.Settings;
        var state = settings.ExpressionState;
        if (state.IsEmpty)
        {
            _expressionText.text = "Cultiway.UpgradeRain.UI.Expression.Empty".Localize();
            _expressionText.alignment = TextAnchor.MiddleCenter;
            _expressionText.color = new Color(1f, 1f, 1f, 0.72f);
        }
        else
        {
            _expressionText.text = FormatExpression(settings.Expression);
            if (!state.IsComplete && !string.IsNullOrEmpty(state.ErrorKey))
                _expressionText.text += $"\n{state.ErrorKey.Localize()}";
            _expressionText.alignment = TextAnchor.MiddleLeft;
            _expressionText.color = state.IsComplete ? Color.white : new Color(1f, 0.82f, 0.25f, 1f);
        }
        _expressionTip.textOnClickDescription = _expressionText.text;

        var hasTokens = settings.Expression.Count > 0;
        _undoExpressionButton.interactable = hasTokens;
        _clearExpressionButton.interactable = hasTokens;
        for (var i = 0; i < _symbolButtons.Length; i++)
        {
            if (_symbolButtons[i] != null)
                _symbolButtons[i].interactable = state.CanAppend((UpgradeRainFilterTokenKind)i);
        }
        var canAppendPredicate = state.CanAppend(UpgradeRainFilterTokenKind.Predicate);
        for (var i = 0; i < _predicateButtons.Count; i++)
        {
            if (_predicateButtons[i] != null) _predicateButtons[i].interactable = canAppendPredicate;
        }
    }

    private static string FormatExpression(IReadOnlyList<UpgradeRainFilterToken> expression)
    {
        var result = new StringBuilder();
        var previous = UpgradeRainFilterTokenKind.Or;
        for (var i = 0; i < expression.Count; i++)
        {
            var token = expression[i];
            if (i > 0 && token.Kind != UpgradeRainFilterTokenKind.RightParenthesis &&
                previous != UpgradeRainFilterTokenKind.LeftParenthesis)
            {
                result.Append(' ');
            }

            switch (token.Kind)
            {
                case UpgradeRainFilterTokenKind.Predicate:
                {
                    var descriptor = UpgradeRainFilterCatalog.Get(token.Predicate.TypeId);
                    var typeName = descriptor == null
                        ? token.Predicate.TypeId
                        : descriptor.NameKey.Localize();
                    result.Append(typeName).Append('「').Append(token.Predicate.DisplayName).Append('」');
                    break;
                }
                case UpgradeRainFilterTokenKind.Not:
                    result.Append("Cultiway.UpgradeRain.UI.Symbol.Not".Localize());
                    break;
                case UpgradeRainFilterTokenKind.And:
                    result.Append("Cultiway.UpgradeRain.UI.Symbol.And".Localize());
                    break;
                case UpgradeRainFilterTokenKind.Or:
                    result.Append("Cultiway.UpgradeRain.UI.Symbol.Or".Localize());
                    break;
                case UpgradeRainFilterTokenKind.LeftParenthesis:
                    result.Append('(');
                    break;
                case UpgradeRainFilterTokenKind.RightParenthesis:
                    result.Append(')');
                    break;
            }
            previous = token.Kind;
        }
        return result.ToString();
    }

    private void RefreshFilterTypes()
    {
        var previousId = _filterTypes.Length > 0 && _filterTypeIndex < _filterTypes.Length
            ? _filterTypes[_filterTypeIndex].Id
            : null;
        _filterTypes = new UpgradeRainFilterDescriptor[UpgradeRainFilterCatalog.Types.Count];
        for (var i = 0; i < _filterTypes.Length; i++)
            _filterTypes[i] = UpgradeRainFilterCatalog.Types[i];
        _filterTypeIndex = Array.FindIndex(_filterTypes, descriptor => descriptor.Id == previousId);
        if (_filterTypeIndex < 0) _filterTypeIndex = 0;
        RebuildCategoryButtons();
        RebuildOptionCards();
    }

    private void RebuildCategoryButtons()
    {
        ClearChildren(_categoryContent);
        _categoryButtons = new Button[_filterTypes.Length];
        for (var i = 0; i < _filterTypes.Length; i++)
        {
            var index = i;
            var descriptor = _filterTypes[i];
            var label = descriptor.NameKey.Localize();
            var button = string.IsNullOrEmpty(descriptor.IconPath)
                ? WanfaUiFactory.CreateButton(_categoryContent, $"Category_{i}", label, CategoryButtonWidth, 24f,
                    () => SelectCategory(index))
                : WanfaUiFactory.CreateIconTextButton(_categoryContent, $"Category_{i}",
                    descriptor.IconPath, label, CategoryButtonWidth, 24f, () => SelectCategory(index));
            WanfaUiFactory.SetTooltip(button.gameObject, label,
                "Cultiway.UpgradeRain.UI.Filter.Category.Description".Localize());
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
            _categoryButtons[i].GetComponent<Image>().sprite = SpriteTextureLoader.getSprite(
                i == _filterTypeIndex ? "ui/special/button2" : "ui/special/button");
        }
    }

    private void RebuildOptionCards()
    {
        ClearChildren(_optionContent);
        _predicateButtons.Clear();
        if (_filterTypes.Length == 0) return;

        var descriptor = _filterTypes[_filterTypeIndex];
        var options = descriptor.GetOptions();
        if (options.Count == 0)
        {
            WanfaUiFactory.CreateText(_optionContent, "NoOptions",
                "Cultiway.UpgradeRain.UI.Filter.NoOptions".Localize(), OptionCardWidth, OptionCardHeight, 6,
                TextAnchor.MiddleCenter);
            return;
        }

        for (var i = 0; i < options.Count; i++) CreateOptionCard(descriptor, options[i], i);
        RefreshExpressionControls();
    }

    private void CreateOptionCard(UpgradeRainFilterDescriptor descriptor,
        UpgradeRainFilterOption option, int index)
    {
        var card = new GameObject($"Option_{index}", typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        card.transform.SetParent(_optionContent, false);
        WanfaUiFactory.SetLayout(card.transform, OptionCardWidth, OptionCardHeight);
        var cardImage = card.GetComponent<Image>();
        cardImage.sprite = SpriteTextureLoader.getSprite("ui/special/button");
        cardImage.type = Image.Type.Sliced;

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

        var label = WanfaUiFactory.CreateText(card.transform, "Label", option.DisplayName,
            OptionCardWidth - 6f, 15f, 6, TextAnchor.MiddleCenter, FontStyle.Bold);
        var labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.offsetMin = new Vector2(3f, 1f);
        labelRect.offsetMax = new Vector2(-3f, 16f);
        label.raycastTarget = false;

        var description = string.Format("Cultiway.UpgradeRain.UI.Filter.Option.Description".Localize(),
            descriptor.NameKey.Localize());
        WanfaUiFactory.SetTooltip(card, option.DisplayName, description);
    }

    private bool TryCreateMetaBanner(Transform parent, UpgradeRainFilterDescriptor descriptor,
        UpgradeRainFilterOption option, UnityAction append)
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
        image.sprite = SpriteTextureLoader.getSprite(string.IsNullOrEmpty(iconPath)
            ? WanfaUiIcons.Overview
            : iconPath);
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void AppendPredicate(UpgradeRainFilterDescriptor descriptor,
        UpgradeRainFilterOption option)
    {
        if (option.MetaObject != null && !option.MetaObject.isAlive()) return;
        UpgradeRainService.Settings.AppendPredicate(new UpgradeRainFilterEntry(
            descriptor.Id, option.Id, option.DisplayName));
    }

    private static void SetPaneBackground(GameObject pane)
    {
        var image = pane.GetComponent<Image>() ?? pane.AddComponent<Image>();
        ConfigurePaneBackground(image);
    }

    private static void SetScrollPaneBackground(GameObject pane)
    {
        var background = new GameObject("Pane Background", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(pane.transform, false);
        background.transform.SetAsFirstSibling();
        var rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-WanfaUiFactory.OriginalVerticalScrollbarReservedWidth, 0f);
        var image = background.GetComponent<Image>();
        image.raycastTarget = false;
        ConfigurePaneBackground(image);
    }

    private static void ConfigurePaneBackground(Image image)
    {
        image.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        image.type = Image.Type.Sliced;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Object.Destroy(child);
        }
    }
}

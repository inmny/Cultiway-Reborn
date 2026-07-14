using System.Globalization;
using Cultiway.Core.Components;
using Cultiway.Core.WorldTools;
using Cultiway.UI.Components;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>配置灵根雨的八项相对权重、综合强度和角色过滤表达式。</summary>
public sealed class WindowElementRootRainConfig : AbstractWideWindow<WindowElementRootRainConfig>
{
    public const string Id = "Cultiway.UI.WindowElementRootRainConfig";
    public static readonly Vector2 WindowSize = new(600f, 380f);

    // 520 内容宽 + 4 内容内边距 + 2 视口左边距 + 20 原版滚动条占位。
    private const float ScrollWidth = 546f;
    private const float RootWidth = 520f;
    private const float RootHeight = 338f;
    private const float CompositionHeight = 262f;
    private const float PreviewHeight = 208f;
    private const float WeightEditorWidth = 322f;
    private const float PreviewWidth = 194f;
    private const float DiagramSize = 172f;
    private const float ExpressionHeight = 40f;
    private const float BrowserHeight = 176f;

    private InputField _strengthInput;
    private Text _summary;
    private Text _normalizationSummary;
    private ElementRootWeightEditor _weightEditor;
    private ElementRootDiagram _diagram;
    private ActorFilterEditor _filterEditor;
    private ScrollRect _outerScroll;

    protected override void Init()
    {
        var originalScrollView = BackgroundTransform.Find("Scroll View");
        var scrollbarMaskTemplate = originalScrollView.Find("Scrollbar Vertical Mask");
        originalScrollView.gameObject.SetActive(false);

        var content = WanfaUiFactory.CreateScrollContent(BackgroundTransform, "ElementRootRainScroll",
            ScrollWidth, RootHeight);
        var scrollRoot = content.parent.parent;
        scrollRoot.localPosition = new Vector3(0f, -8f);
        WanfaUiFactory.AttachOriginalVerticalScrollbar(content, scrollbarMaskTemplate,
            BackgroundTransform as RectTransform);
        _outerScroll = scrollRoot.GetComponent<ScrollRect>();

        CreateCompositionSection(content);
        _filterEditor = new ActorFilterEditor(content, RootWidth, ExpressionHeight, BrowserHeight,
            scrollbarMaskTemplate, ElementRootRainService.Settings.Filter,
            "Cultiway.ElementRootRain.UI.Expression.Empty",
            "Cultiway.ElementRootRain.UI.Filter.Semantics");

        ElementRootRainService.Settings.Changed += Refresh;
        Refresh();
    }

    public override void OnNormalEnable()
    {
        _filterEditor?.RefreshWorldOptions();
        Refresh();
        if (_outerScroll != null) _outerScroll.verticalNormalizedPosition = 1f;
    }

    private void OnDestroy()
    {
        ElementRootRainService.Settings.Changed -= Refresh;
        _filterEditor?.Dispose();
    }

    private void CreateCompositionSection(Transform root)
    {
        var section = WanfaUiFactory.CreateLayout(root, "Composition", false, RootWidth,
            CompositionHeight, 4f);
        WanfaUiFactory.CreateText(section.transform, "Title",
            "Cultiway.ElementRootRain.UI.Section.Composition".Localize(), RootWidth, 18f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);

        var body = WanfaUiFactory.CreateLayout(section.transform, "Weight Editor And Preview", true,
            RootWidth, PreviewHeight, 4f, TextAnchor.UpperLeft);
        var settings = ElementRootRainService.Settings;
        _weightEditor = new ElementRootWeightEditor(body.transform, WeightEditorWidth, PreviewHeight,
            ElementRootRainSettings.MaxRatio, settings.GetRatio, settings.GetNormalizedRatio,
            settings.SetRatio);

        var preview = WanfaUiFactory.CreateLayout(body.transform, "Preview", false,
            PreviewWidth, PreviewHeight, 4f, TextAnchor.UpperCenter);
        _diagram = ElementRootDiagram.Create(preview.transform, "Element Root Diagram", DiagramSize,
            ElementRootDiagramDetail.Large);
        _summary = WanfaUiFactory.CreateText(preview.transform, "Summary", string.Empty,
            PreviewWidth, PreviewHeight - DiagramSize - 4f, 7, TextAnchor.MiddleCenter, FontStyle.Bold);

        CreateStrengthRow(section.transform);
    }

    private void CreateStrengthRow(Transform parent)
    {
        var strengthRow = WanfaUiFactory.CreateLayout(parent, "Strength", true,
            RootWidth, 28f, 4f);
        var strengthLabel = WanfaUiFactory.CreateText(strengthRow.transform, "Label",
            "Cultiway.ElementRootRain.UI.Strength".Localize(), 76f, 26f, 7,
            TextAnchor.MiddleLeft, FontStyle.Bold);
        WanfaUiFactory.SetTooltip(strengthLabel.gameObject,
            "Cultiway.ElementRootRain.UI.Strength",
            "Cultiway.ElementRootRain.UI.Strength.Description");
        var decrease = WanfaUiFactory.CreateIconButton(strengthRow.transform, "Decrease",
            WanfaUiIcons.Remove, 28f, 26f,
            () => ElementRootRainService.Settings.SetStrength(ElementRootRainService.Settings.Strength - 0.1f));
        WanfaUiFactory.SetTooltip(decrease.gameObject,
            "Cultiway.ElementRootRain.UI.Action.DecreaseStrength",
            "Cultiway.ElementRootRain.UI.Action.DecreaseStrength.Description");

        _strengthInput = WanfaUiFactory.CreateInput(strengthRow.transform, "StrengthInput", "2",
            "Cultiway.ElementRootRain.UI.Placeholder.Strength".Localize(), 80f, 26f);
        _strengthInput.contentType = InputField.ContentType.DecimalNumber;
        _strengthInput.characterLimit = 8;
        _strengthInput.onEndEdit.AddListener(CommitStrength);

        var increase = WanfaUiFactory.CreateIconButton(strengthRow.transform, "Increase",
            WanfaUiIcons.Add, 28f, 26f,
            () => ElementRootRainService.Settings.SetStrength(ElementRootRainService.Settings.Strength + 0.1f));
        WanfaUiFactory.SetTooltip(increase.gameObject,
            "Cultiway.ElementRootRain.UI.Action.IncreaseStrength",
            "Cultiway.ElementRootRain.UI.Action.IncreaseStrength.Description");

        _normalizationSummary = WanfaUiFactory.CreateText(strengthRow.transform, "Normalization", string.Empty,
            292f, 26f, 7, TextAnchor.MiddleLeft);
        WanfaUiFactory.SetTooltip(_normalizationSummary.gameObject,
            "Cultiway.ElementRootRain.UI.Normalization.Title".Localize(),
            "Cultiway.ElementRootRain.UI.Ratio.Normalization".Localize());
    }

    private void CommitStrength(string value)
    {
        if (TryParseNumber(value, out var strength)) ElementRootRainService.Settings.SetStrength(strength);
        Refresh();
    }

    private void Refresh()
    {
        if (_strengthInput == null) return;
        var settings = ElementRootRainService.Settings;
        _weightEditor.Refresh();
        _strengthInput.SetTextWithoutNotify(settings.Strength.ToString("0.###", CultureInfo.CurrentCulture));

        var weights = new float[8];
        var weightSum = 0f;
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = settings.GetRatio(i);
            weightSum += weights[i];
        }

        _normalizationSummary.text = weightSum > 0f
            ? string.Format("Cultiway.ElementRootRain.UI.Format.Normalization".Localize(),
                weightSum.ToString("0.###", CultureInfo.CurrentCulture))
            : "Cultiway.ElementRootRain.UI.InvalidRatios".Localize();

        if (settings.TryResolveComposition(out var composition))
        {
            var root = new ElementRoot(composition);
            var strengthText = root.GetStrength().ToString("0.###", CultureInfo.CurrentCulture);
            _summary.text = string.Format("Cultiway.ElementRootRain.UI.Format.Summary".Localize(),
                root.Type.GetName(), strengthText);
            _summary.color = Color.white;
            _diagram.SetValues(weights, root.Type.GetName(),
                string.Format("Cultiway.ElementRootDiagram.Tooltip.Strength".Localize(), strengthText));
        }
        else
        {
            _summary.text = "Cultiway.ElementRootRain.UI.InvalidRatios".Localize();
            _summary.color = new Color(1f, 0.82f, 0.25f, 1f);
            _diagram.SetValues(weights);
        }
    }

    private static bool TryParseNumber(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

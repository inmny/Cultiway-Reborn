using System.Globalization;
using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Core.WorldTools;
using Cultiway.UI.Components;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>配置灵根雨的八项相对比例、综合强度和角色过滤表达式。</summary>
public sealed class WindowElementRootRainConfig : AbstractWideWindow<WindowElementRootRainConfig>
{
    public const string Id = "Cultiway.UI.WindowElementRootRainConfig";
    public static readonly Vector2 WindowSize = new(600f, 380f);

    private const float RootWidth = 520f;
    private const float RootHeight = 338f;
    private const float CompositionHeight = 114f;
    private const float ExpressionHeight = 40f;
    private const float BrowserHeight = 176f;
    private const float RatioControlWidth = 127f;

    private static readonly string[] ElementIconPaths =
    {
        "cultiway/icons/element_root/iron",
        "cultiway/icons/element_root/wood",
        "cultiway/icons/element_root/water",
        "cultiway/icons/element_root/fire",
        "cultiway/icons/element_root/earth",
        "cultiway/icons/element_root/neg",
        "cultiway/icons/element_root/pos",
        "cultiway/icons/element_root/entropy"
    };

    private readonly InputField[] _ratioInputs = new InputField[8];
    private InputField _strengthInput;
    private Text _summary;
    private ActorFilterEditor _filterEditor;

    protected override void Init()
    {
        var originalScrollView = BackgroundTransform.Find("Scroll View");
        var scrollbarMaskTemplate = originalScrollView.Find("Scrollbar Vertical Mask");
        originalScrollView.gameObject.SetActive(false);
        var root = WanfaUiFactory.CreateLayout(BackgroundTransform, "ElementRootRainRoot", false,
            RootWidth, RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        CreateCompositionSection(root.transform);
        _filterEditor = new ActorFilterEditor(root.transform, RootWidth, ExpressionHeight, BrowserHeight,
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

        var firstRow = WanfaUiFactory.CreateLayout(section.transform, "Ratios1", true,
            RootWidth, 28f, 4f);
        var secondRow = WanfaUiFactory.CreateLayout(section.transform, "Ratios2", true,
            RootWidth, 28f, 4f);
        for (var i = 0; i < _ratioInputs.Length; i++)
            CreateRatioControl(i < 4 ? firstRow.transform : secondRow.transform, i);

        var strengthRow = WanfaUiFactory.CreateLayout(section.transform, "Strength", true,
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
        _summary = WanfaUiFactory.CreateText(strengthRow.transform, "Summary", string.Empty,
            292f, 26f, 7, TextAnchor.MiddleLeft);
    }

    private void CreateRatioControl(Transform parent, int elementIndex)
    {
        var control = WanfaUiFactory.CreateLayout(parent, $"Ratio_{elementIndex}", true,
            RatioControlWidth, 28f, 2f);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(control.transform, false);
        WanfaUiFactory.SetLayout(icon.transform, 20f, 20f);
        var image = icon.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite(ElementIconPaths[elementIndex]);
        image.preserveAspect = true;
        image.raycastTarget = false;
        WanfaUiFactory.SetTooltip(icon, ElementIndex.ElementNames[elementIndex].Localize(),
            "Cultiway.ElementRootRain.UI.Ratio.Description".Localize());

        WanfaUiFactory.CreateText(control.transform, "Label",
            ElementIndex.ElementNames[elementIndex].Localize(), 16f, 26f, 7,
            TextAnchor.MiddleCenter, FontStyle.Bold);
        var input = WanfaUiFactory.CreateInput(control.transform, "RatioInput", "1",
            "Cultiway.ElementRootRain.UI.Placeholder.Ratio".Localize(), 85f, 26f);
        input.contentType = InputField.ContentType.DecimalNumber;
        input.characterLimit = 8;
        var index = elementIndex;
        input.onEndEdit.AddListener(value => CommitRatio(index, value));
        _ratioInputs[elementIndex] = input;
    }

    private void CommitRatio(int elementIndex, string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var ratio))
            ElementRootRainService.Settings.SetRatio(elementIndex, ratio);
        Refresh();
    }

    private void CommitStrength(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var strength))
            ElementRootRainService.Settings.SetStrength(strength);
        Refresh();
    }

    private void Refresh()
    {
        if (_strengthInput == null) return;
        var settings = ElementRootRainService.Settings;
        for (var i = 0; i < _ratioInputs.Length; i++)
            _ratioInputs[i].text = settings.GetRatio(i).ToString("0.###", CultureInfo.CurrentCulture);
        _strengthInput.text = settings.Strength.ToString("0.###", CultureInfo.CurrentCulture);

        if (settings.TryResolveComposition(out var composition))
        {
            var root = new ElementRoot(composition);
            _summary.text = string.Format("Cultiway.ElementRootRain.UI.Format.Summary".Localize(),
                root.Type.GetName(), root.GetStrength().ToString("0.###", CultureInfo.CurrentCulture));
            _summary.color = Color.white;
        }
        else
        {
            _summary.text = "Cultiway.ElementRootRain.UI.InvalidRatios".Localize();
            _summary.color = new Color(1f, 0.82f, 0.25f, 1f);
        }
    }
}

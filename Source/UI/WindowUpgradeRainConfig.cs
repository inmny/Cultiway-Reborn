using Cultiway.Core.ActorFiltering;
using Cultiway.Core.Progression;
using Cultiway.UI.Components;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

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
    private const float ModeButtonWidth = 100f;

    private readonly Button[] _modeButtons = new Button[5];
    private InputField _amountInput;
    private Text _amountSummary;
    private ActorFilterEditor _filterEditor;

    protected override void Init()
    {
        var originalScrollView = BackgroundTransform.Find("Scroll View");
        var scrollbarMaskTemplate = originalScrollView.Find("Scrollbar Vertical Mask");
        originalScrollView.gameObject.SetActive(false);
        var root = WanfaUiFactory.CreateLayout(BackgroundTransform, "UpgradeRainRoot", false,
            RootWidth, RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        CreateTopSection(root.transform);
        _filterEditor = new ActorFilterEditor(root.transform, RootWidth, ExpressionHeight, BrowserHeight,
            scrollbarMaskTemplate, UpgradeRainService.Settings.Filter,
            "Cultiway.UpgradeRain.UI.Expression.Empty",
            "Cultiway.UpgradeRain.UI.Filter.Semantics");

        UpgradeRainService.Settings.Changed += Refresh;
        Refresh();
    }

    public override void OnNormalEnable()
    {
        _filterEditor?.RefreshWorldOptions();
        Refresh();
    }

    private void OnDestroy()
    {
        UpgradeRainService.Settings.Changed -= Refresh;
        _filterEditor?.Dispose();
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

        var expression = settings.Filter.Expression;
        if (expression.Count == 1 && expression[0].Kind == ActorFilterTokenKind.Predicate &&
            expression[0].Predicate.TypeId == ActorFilterCatalog.CultisysTypeId)
        {
            var cultisys = ProgressionService.GetRegistered(expression[0].Predicate.ValueId);
            var targetLevel = settings.Amount - 1;
            if (cultisys != null && targetLevel >= 0 && targetLevel < cultisys.LevelNumber)
            {
                return string.Format("Cultiway.UpgradeRain.UI.Format.TargetRealmNamed".Localize(),
                    settings.Amount, cultisys.GetLevelName(targetLevel));
            }
        }
        return string.Format("Cultiway.UpgradeRain.UI.Format.TargetRealm".Localize(), settings.Amount);
    }
}

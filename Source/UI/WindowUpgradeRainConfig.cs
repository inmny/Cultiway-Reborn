using Cultiway.Core.ActorFiltering;
using Cultiway.Core.Progression;
using Cultiway.Core.WorldTools;
using Cultiway.UI.Components;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>配置帝流浆进阶方式、数值以及按逻辑表达式组合的过滤条件。</summary>
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

    private readonly UiSegmentedTabs _modes = new();
    private InputField _amountInput;
    private Text _amountSummary;
    private ActorFilterEditor _filterEditor;

    protected override void Init()
    {
        UiWindowContext context = UiWindowContext.Bind(BackgroundTransform);
        var root = UiLayout.Create(BackgroundTransform, "UpgradeRainRoot", false,
            RootWidth, RootHeight, 4f);
        root.transform.localPosition = new Vector3(0f, -8f);

        CreateTopSection(root.transform);
        _filterEditor = new ActorFilterEditor(root.transform, RootWidth, ExpressionHeight, BrowserHeight,
            context.ScrollbarTemplate, UpgradeRainService.Settings.Filter,
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
        var top = UiLayout.Create(root, "Top", false, RootWidth, TopHeight, 8f);
        UiElements.CreateText(top.transform, "ModeTitle",
            "Cultiway.UpgradeRain.UI.Section.Mode".Localize(), RootWidth, 18f, 8,
            TextAnchor.MiddleLeft, FontStyle.Bold);

        var modeRow = UiLayout.Create(top.transform, "Modes", true, RootWidth, 28f, 4f);
        CreateModeButton(modeRow.transform,
            "FixedMinor", "Cultiway.UpgradeRain.UI.Mode.FixedMinor", UpgradeRainMode.FixedMinor);
        CreateModeButton(modeRow.transform,
            "CappedMinor", "Cultiway.UpgradeRain.UI.Mode.CappedMinor", UpgradeRainMode.CappedMinor);
        CreateModeButton(modeRow.transform,
            "FixedMajor", "Cultiway.UpgradeRain.UI.Mode.FixedMajor", UpgradeRainMode.FixedMajor);
        CreateModeButton(modeRow.transform,
            "CappedMajor", "Cultiway.UpgradeRain.UI.Mode.CappedMajor", UpgradeRainMode.CappedMajor);
        CreateModeButton(modeRow.transform,
            "ToRealm", "Cultiway.UpgradeRain.UI.Mode.ToRealm", UpgradeRainMode.ToRealm);

        var amountRow = UiLayout.Create(top.transform, "Amount", true, RootWidth, 28f, 4f);
        var decrease = UiElements.CreateIconButton(amountRow.transform, "Decrease", UiIcons.Remove,
            28f, 26f, () => UpgradeRainService.Settings.SetAmount(UpgradeRainService.Settings.Amount - 1));
        UiTooltip.Set(decrease.gameObject, "Cultiway.UpgradeRain.UI.Action.Decrease",
            "Cultiway.UpgradeRain.UI.Action.Decrease.Description");

        _amountInput = UiElements.CreateInput(amountRow.transform, "AmountInput", "1",
            "Cultiway.UpgradeRain.UI.Placeholder.Amount".Localize(), 80f, 26f);
        _amountInput.contentType = InputField.ContentType.IntegerNumber;
        _amountInput.characterLimit = 5;
        _amountInput.onEndEdit.AddListener(CommitAmountInput);

        var increase = UiElements.CreateIconButton(amountRow.transform, "Increase", UiIcons.Add,
            28f, 26f, () => UpgradeRainService.Settings.SetAmount(UpgradeRainService.Settings.Amount + 1));
        UiTooltip.Set(increase.gameObject, "Cultiway.UpgradeRain.UI.Action.Increase",
            "Cultiway.UpgradeRain.UI.Action.Increase.Description");
        _amountSummary = UiElements.CreateText(amountRow.transform, "AmountSummary", string.Empty,
            372f, 26f, 7, TextAnchor.MiddleLeft);
    }

    private void CreateModeButton(Transform parent, string name, string localeKey,
        UpgradeRainMode mode)
    {
        var button = UiElements.CreateButton(parent, name, localeKey.Localize(), ModeButtonWidth, 26f,
            () => UpgradeRainService.Settings.SetMode(mode));
        UiTooltip.Set(button.gameObject, localeKey, $"{localeKey}.Description");
        _modes.Add(button);
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
        _modes.SetSelected((int)settings.Mode);
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

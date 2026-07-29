using System.Collections.Generic;
using System.Globalization;
using Cultiway.Core.Combat;
using NeoModLoader.api;
using NeoModLoader.General.UI.Window;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>
/// 无来源伤害等级配置窗口：按 <see cref="AttackType"/> 列出每类无来源伤害的替代境界等级，
/// 每行提供类别名（左）、滑杆（中）与数值输入框（右）。
/// </summary>
public sealed class WindowSourcelessDamageLevelConfig : AbstractWindow<WindowSourcelessDamageLevelConfig>
{
    public const string Id = "Cultiway.UI.WindowSourcelessDamageLevelConfig";

    private const float ContentWidth = 190f;
    private const float RowHeight = 22f;
    private const float LabelWidth = 56f;
    private const float SliderWidth = 86f;
    private const float InputWidth = 36f;
    private const float RowSpacing = 4f;

    private readonly List<Row> _rows = new();

    protected override void Init()
    {
        var layout = ContentTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = UiTheme.Current.Metrics.SpacingSm;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentTransform.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        UiElements.CreateSectionTitle(ContentTransform, "Header",
            "Cultiway.SourcelessDamage.Header".Localize(), ContentWidth);

        foreach (AttackType type in SourcelessDamageLevels.Categories)
        {
            BuildRow(ContentTransform, type);
        }

        SourcelessDamageLevels.Changed += Refresh;
        Refresh();
    }

    public override void OnNormalEnable()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        SourcelessDamageLevels.Flush();
        SourcelessDamageLevels.Changed -= Refresh;
    }

    private void BuildRow(Transform parent, AttackType type)
    {
        var row = UiLayout.Create(parent, $"Row.{type}", true, ContentWidth, RowHeight, RowSpacing,
            TextAnchor.MiddleLeft);

        UiElements.CreateText(row.transform, "Label", GetAttackTypeLabel(type),
            LabelWidth, RowHeight, 7, TextAnchor.MiddleLeft);

        var slider = UiElements.CreateNativeSlider(row.transform, "Slider", SliderWidth, RowHeight,
            0f, SourcelessDamageLevels.MaxLevel, SourcelessDamageLevels.GetLevel(type));
        slider.wholeNumbers = true;

        var input = UiElements.CreateInput(row.transform, "Input",
            SourcelessDamageLevels.GetLevel(type).ToString("0"),
            "0", InputWidth, RowHeight);
        input.contentType = InputField.ContentType.IntegerNumber;
        input.characterLimit = 4;

        slider.onValueChanged.AddListener(value =>
        {
            SourcelessDamageLevels.SetLevel(type, value);
            input.text = SourcelessDamageLevels.GetLevel(type).ToString("0");
        });
        input.onEndEdit.AddListener(value =>
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                SourcelessDamageLevels.SetLevel(type, parsed);
            }
            input.text = SourcelessDamageLevels.GetLevel(type).ToString("0");
            slider.SetValueWithoutNotify(SourcelessDamageLevels.GetLevel(type));
        });

        _rows.Add(new Row(type, slider, input));
    }

    private void Refresh()
    {
        if (_rows.Count == 0) return;
        foreach (var row in _rows)
        {
            var value = SourcelessDamageLevels.GetLevel(row.Type);
            row.Slider.SetValueWithoutNotify(value);
            row.Input.text = value.ToString("0");
        }
    }

    private static string GetAttackTypeLabel(AttackType type)
    {
        return $"Cultiway.SourcelessDamage.AttackType.{type}".Localize();
    }

    private readonly struct Row
    {
        public Row(AttackType type, SliderExtended slider, InputField input)
        {
            Type = type;
            Slider = slider;
            Input = input;
        }

        public AttackType Type { get; }
        public SliderExtended Slider { get; }
        public InputField Input { get; }
    }
}

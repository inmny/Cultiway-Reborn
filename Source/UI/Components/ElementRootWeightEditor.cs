using System;
using System.Globalization;
using Cultiway.Const;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI.Components;

/// <summary>同时提供原版滑块、精确数字输入和归一化结果的八元素权重编辑器。</summary>
internal sealed class ElementRootWeightEditor
{
    private const float RowHeight = 24f;
    private const float SeparatorHeight = 3f;

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

    private readonly Func<int, float> _getWeight;
    private readonly Func<int, float> _getNormalizedWeight;
    private readonly Action<int, float> _setWeight;
    private readonly float _maxWeight;
    private readonly SliderExtended[] _sliders = new SliderExtended[8];
    private readonly InputField[] _inputs = new InputField[8];
    private readonly Text[] _percentages = new Text[8];
    private bool _refreshing;

    /// <summary>创建编辑器并绑定权重读取、归一化读取和写入委托。</summary>
    public ElementRootWeightEditor(Transform parent, float width, float height, float maxWeight,
        Func<int, float> getWeight, Func<int, float> getNormalizedWeight, Action<int, float> setWeight)
    {
        _maxWeight = maxWeight;
        _getWeight = getWeight ?? throw new ArgumentNullException(nameof(getWeight));
        _getNormalizedWeight = getNormalizedWeight ?? throw new ArgumentNullException(nameof(getNormalizedWeight));
        _setWeight = setWeight ?? throw new ArgumentNullException(nameof(setWeight));

        var root = WanfaUiFactory.CreateLayout(parent, "Element Weights", false, width, height, 1f);
        for (var i = 0; i < _sliders.Length; i++)
        {
            CreateRow(root.transform, width, i);
            if (i == ElementIndex.Earth || i == ElementIndex.Pos) CreateSeparator(root.transform, width);
        }
    }

    /// <summary>从绑定的数据源刷新八项权重、滑块位置和归一化百分比。</summary>
    public void Refresh()
    {
        _refreshing = true;
        for (var i = 0; i < _sliders.Length; i++)
        {
            var value = _getWeight(i);
            _sliders[i].SetValueWithoutNotify(value);
            _inputs[i].SetTextWithoutNotify(value.ToString("0.###", CultureInfo.CurrentCulture));
            _percentages[i].text = (_getNormalizedWeight(i) * 100f)
                .ToString("0.#", CultureInfo.CurrentCulture) + "%";
        }
        _refreshing = false;
    }

    private void CreateRow(Transform parent, float width, int elementIndex)
    {
        var row = WanfaUiFactory.CreateLayout(parent, $"Element_{elementIndex}", true,
            width, RowHeight, 4f, TextAnchor.MiddleLeft);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        icon.transform.SetParent(row.transform, false);
        WanfaUiFactory.SetLayout(icon.transform, 18f, 18f);
        var iconImage = icon.GetComponent<Image>();
        iconImage.sprite = SpriteTextureLoader.getSprite(ElementIconPaths[elementIndex]);
        iconImage.preserveAspect = true;
        WanfaUiFactory.SetTooltip(icon, ElementIndex.ElementNames[elementIndex].Localize(),
            "Cultiway.ElementRootRain.UI.Ratio.Description".Localize());

        WanfaUiFactory.CreateText(row.transform, "Label", ElementIndex.ElementNames[elementIndex].Localize(),
            20f, RowHeight, 7, TextAnchor.MiddleCenter, FontStyle.Bold);

        var slider = WanfaUiFactory.CreateNativeSlider(row.transform, "Weight Slider", 155f, RowHeight,
            0f, _maxWeight, _getWeight(elementIndex));
        WanfaUiFactory.SetTooltip(slider.gameObject, ElementIndex.ElementNames[elementIndex].Localize(),
            "Cultiway.ElementRootRain.UI.Ratio.Description".Localize(),
            "Cultiway.ElementRootRain.UI.Ratio.Normalization".Localize());
        slider.onValueChanged.AddListener(value =>
        {
            if (!_refreshing) _setWeight(elementIndex, value);
        });
        _sliders[elementIndex] = slider;

        var input = WanfaUiFactory.CreateInput(row.transform, "Weight Input", "50",
            "Cultiway.ElementRootRain.UI.Placeholder.Ratio".Localize(), 54f, 22f);
        input.contentType = InputField.ContentType.DecimalNumber;
        input.characterLimit = 8;
        input.onEndEdit.AddListener(value => CommitInput(elementIndex, value));
        _inputs[elementIndex] = input;

        _percentages[elementIndex] = WanfaUiFactory.CreateText(row.transform, "Normalized", string.Empty,
            50f, RowHeight, 7, TextAnchor.MiddleRight);
    }

    private static void CreateSeparator(Transform parent, float width)
    {
        var separator = new GameObject("Group Separator", typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        separator.transform.SetParent(parent, false);
        WanfaUiFactory.SetLayout(separator.transform, width, SeparatorHeight);
        separator.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
    }

    private void CommitInput(int elementIndex, string text)
    {
        if (TryParseWeight(text, out var value) && value >= 0f && value <= _maxWeight)
            _setWeight(elementIndex, value);
        Refresh();
    }

    private static bool TryParseWeight(string text, out float value)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
               float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

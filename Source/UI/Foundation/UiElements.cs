using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>创建无业务含义的原子 UI 元素。</summary>
internal static class UiElements
{
    private static GameObject _settingButtonTemplate;

    public static Text CreateText(Transform parent, string name, string value, float width, float height,
        int fontSize = 7, TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal,
        VerticalWrapMode verticalOverflow = VerticalWrapMode.Truncate)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, width, height);
        Text text = obj.GetComponent<Text>();
        text.font = UiTheme.Current.Font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = UiTheme.Current.Palette.PrimaryText;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = verticalOverflow;
        text.text = value;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, string label, float width, float height,
        UnityAction action)
    {
        Button button = CreateButtonRoot(parent, name, width, height, action);
        Text text = CreateText(button.transform, "Text", label, width, height, 7, TextAnchor.MiddleCenter,
            FontStyle.Bold);
        UiLayout.Stretch(text.rectTransform);
        return button;
    }

    public static Button CreateIconButton(Transform parent, string name, string iconPath, float width, float height,
        UnityAction action, float iconInset = 4f, float iconScale = 1f, float iconRotation = 0f)
    {
        Button button = CreateButtonRoot(parent, name, width, height, action);

        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(button.transform, false);
        UiLayout.Stretch(icon.GetComponent<RectTransform>(), iconInset, iconInset, iconInset, iconInset);
        Image iconImage = icon.GetComponent<Image>();
        UiResources.SetImage(iconImage, iconPath);
        iconImage.raycastTarget = false;
        icon.transform.localScale = Vector3.one * iconScale;
        icon.transform.localRotation = Quaternion.Euler(0f, 0f, iconRotation);
        return button;
    }

    private static Button CreateButtonRoot(Transform parent, string name, float width, float height,
        UnityAction action)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, width, height);
        UiResources.ApplySurface(obj.GetComponent<Image>(), UiSurface.Button);
        Button button = obj.GetComponent<Button>();
        if (action != null) button.onClick.AddListener(action);
        return button;
    }

    public static Button CreateIconTextButton(Transform parent, string name, string iconPath, string label,
        float width, float height, UnityAction action)
    {
        Button button = CreateButton(parent, name, label, width, height, action);
        Text text = button.GetComponentInChildren<Text>();
        text.rectTransform.offsetMin = new Vector2(18f, 0f);

        GameObject icon = new("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(button.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(14f, 14f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        Image iconImage = icon.GetComponent<Image>();
        UiResources.SetImage(iconImage, iconPath);
        iconImage.raycastTarget = false;
        return button;
    }

    public static Toggle CreateIconToggle(Transform parent, string name, string iconPath, bool value, float width,
        float height, UiIconToggleStyle style = UiIconToggleStyle.Neutral)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, width, height);
        Image background = obj.GetComponent<Image>();
        UiResources.ApplySurface(background, UiSurface.Button);

        GameObject baseIcon = new("Icon", typeof(RectTransform), typeof(Image));
        baseIcon.transform.SetParent(obj.transform, false);
        UiLayout.Stretch(baseIcon.GetComponent<RectTransform>(), 5f, 5f, 4f, 4f);
        Image baseImage = baseIcon.GetComponent<Image>();
        UiResources.SetImage(baseImage, iconPath);
        baseImage.color = style == UiIconToggleStyle.Favorite
            ? ColorStyleLibrary.m.favorite_not_selected
            : UiTheme.Current.Palette.Disabled;
        baseImage.raycastTarget = false;

        GameObject selectedIcon = new("Selected", typeof(RectTransform), typeof(Image));
        selectedIcon.transform.SetParent(obj.transform, false);
        UiLayout.Stretch(selectedIcon.GetComponent<RectTransform>(), 5f, 5f, 4f, 4f);
        Image selectedImage = selectedIcon.GetComponent<Image>();
        UiResources.SetImage(selectedImage, iconPath);
        selectedImage.color = style == UiIconToggleStyle.Favorite
            ? ColorStyleLibrary.m.favorite_selected
            : UiTheme.Current.Palette.Selected;
        selectedImage.raycastTarget = false;

        Toggle toggle = obj.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = selectedImage;
        toggle.isOn = value;
        return toggle;
    }

    public static void SetButtonIcon(Button button, string iconPath, bool flipHorizontal = false)
    {
        Image icon = button.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = UiResources.GetSprite(iconPath);
        Vector3 scale = icon.rectTransform.localScale;
        scale.x = flipHorizontal ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        icon.rectTransform.localScale = scale;
    }

    public static void SetButtonLabel(Button button, string value)
    {
        button.GetComponentInChildren<Text>().text = value;
    }

    public static InputField CreateInput(Transform parent, string name, string value, string placeholder,
        float width, float height)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, width, height);
        UiResources.ApplySurface(obj.GetComponent<Image>(), UiSurface.WindowInner);

        Text text = CreateText(obj.transform, "Text", value, width - 8f, height);
        UiLayout.Stretch(text.rectTransform, 4f, 4f, 1f, 1f);
        Text placeholderText = CreateText(obj.transform, "Placeholder", placeholder, width - 8f, height);
        placeholderText.color = UiTheme.Current.Palette.PlaceholderText;
        UiLayout.Stretch(placeholderText.rectTransform, 4f, 4f, 1f, 1f);

        InputField input = obj.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.text = value;
        return input;
    }

    /// <summary>克隆原版设置窗口的滑块区域，保留原版槽、手柄和滚轮转发行为。</summary>
    public static SliderExtended CreateNativeSlider(Transform parent, string name, float width, float height,
        float minValue, float maxValue, float value)
    {
        _settingButtonTemplate ??= Resources.Load<GameObject>(UiResources.SettingButtonPrefab);
        Transform sliderAreaTemplate = _settingButtonTemplate.transform.Find("SliderArea") ??
                                       throw new InvalidOperationException("原版 ui/SettingButton 缺少 SliderArea");
        GameObject sliderArea = UnityEngine.Object.Instantiate(sliderAreaTemplate.gameObject, parent, false);
        sliderArea.name = name;
        sliderArea.SetActive(true);
        UiLayout.SetSize(sliderArea.transform, width, height);
        sliderArea.transform.localScale = Vector3.one;

        SliderExtended slider = sliderArea.GetComponentInChildren<SliderExtended>(true) ??
                                throw new InvalidOperationException("原版 SliderArea 缺少 SliderExtended");
        slider.gameObject.SetActive(true);
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = false;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.SetValueWithoutNotify(Mathf.Clamp(value, minValue, maxValue));
        return slider;
    }

    public static Toggle CreateToggle(Transform parent, string name, string label, bool value, float width,
        float height)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        UiLayout.SetSize(obj.transform, width, height);

        GameObject box = new("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(obj.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.sizeDelta = new Vector2(12f, 12f);
        boxRect.anchoredPosition = new Vector2(6f, 0f);
        UiResources.ApplySurface(box.GetComponent<Image>(), UiSurface.ToggleBox);

        GameObject check = new("Check", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        UiLayout.Stretch(check.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
        check.GetComponent<Image>().color = UiTheme.Current.Palette.Success;

        Text text = CreateText(obj.transform, "Label", label, width - 16f, height);
        RectTransform textRect = text.rectTransform;
        UiLayout.Stretch(textRect, 16f);

        Toggle toggle = obj.GetComponent<Toggle>();
        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();
        toggle.isOn = value;
        return toggle;
    }

    public static GameObject CreatePanel(Transform parent, string name, bool horizontal, float width, float height,
        float spacing = 3f, TextAnchor? alignment = null, UiSurface surface = UiSurface.WindowInner,
        int padding = 6)
    {
        GameObject panel = UiLayout.Create(parent, name, horizontal, width, height, spacing, alignment);
        if (horizontal)
            panel.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
        else
            panel.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(padding, padding, padding, padding);
        Image image = panel.AddComponent<Image>();
        UiResources.ApplySurface(image, surface,
            surface == UiSurface.WindowInner ? UiTheme.Current.Palette.InnerPanelTint : null);
        return panel;
    }

    public static Text CreateSectionTitle(Transform parent, string name, string value, float width)
    {
        Text title = CreateText(parent, name, value, width, 18f, 8, TextAnchor.MiddleLeft, FontStyle.Bold);
        title.color = UiTheme.Current.Palette.AccentText;
        return title;
    }

    public static Button CreateSwatchButton(Transform parent, string name, Color color, float size,
        UnityAction action)
    {
        Button button = CreateIconButton(parent, name, UiIcons.Color, size, size, action);
        Image icon = button.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = null;
        icon.color = color;
        icon.preserveAspect = false;
        return button;
    }
}

internal enum UiIconToggleStyle
{
    Neutral,
    Favorite,
}

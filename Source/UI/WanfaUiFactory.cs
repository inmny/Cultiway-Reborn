using System;
using Cultiway.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.UI;

internal static class WanfaUiFactory
{
    public const float OriginalVerticalScrollbarReservedWidth = 18f;
    private const float OriginalScrollbarWidth = 17.5f;
    private static GameObject _settingButtonTemplate;

    public static GameObject CreateLayout(Transform parent, string name, bool horizontal, float width, float height,
        float spacing = 3f, TextAnchor? alignment = null)
    {
        var type = horizontal ? typeof(HorizontalLayoutGroup) : typeof(VerticalLayoutGroup);
        var obj = new GameObject(name, typeof(RectTransform), type, typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);
        if (horizontal)
        {
            var layout = obj.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment ?? TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            var layout = obj.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment ?? TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }
        return obj;
    }

    public static Text CreateText(Transform parent, string name, string value, float width, float height,
        int fontSize = 7, TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);
        var text = obj.GetComponent<Text>();
        text.font = UIUtils.GetCurrentFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text = value;
        return text;
    }

    public static Button CreateButton(Transform parent, string name, string label, float width, float height,
        UnityAction action)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);
        var image = obj.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite("ui/special/button");
        image.type = Image.Type.Sliced;
        var button = obj.GetComponent<Button>();
        button.onClick.AddListener(action);
        var text = CreateText(obj.transform, "Text", label, width, height, 7, TextAnchor.MiddleCenter, FontStyle.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    public static Button CreateIconButton(Transform parent, string name, string iconPath, float width, float height,
        UnityAction action, float iconInset = 4f, float iconScale = 1f, float iconRotation = 0f)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);
        var image = obj.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite("ui/special/button");
        image.type = Image.Type.Sliced;
        var button = obj.GetComponent<Button>();
        button.onClick.AddListener(action);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(obj.transform, false);
        Stretch(icon.GetComponent<RectTransform>(), iconInset, iconInset, iconInset, iconInset);
        var iconImage = icon.GetComponent<Image>();
        iconImage.sprite = SpriteTextureLoader.getSprite(iconPath);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        icon.transform.localScale = Vector3.one * iconScale;
        icon.transform.localRotation = Quaternion.Euler(0f, 0f, iconRotation);
        return button;
    }

    public static Button CreateIconTextButton(Transform parent, string name, string iconPath, string label,
        float width, float height, UnityAction action)
    {
        var button = CreateButton(parent, name, label, width, height, action);
        var text = button.GetComponentInChildren<Text>();
        var textRect = text.rectTransform;
        textRect.offsetMin = new Vector2(18f, 0f);

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(button.transform, false);
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(14f, 14f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);
        var iconImage = icon.GetComponent<Image>();
        iconImage.sprite = SpriteTextureLoader.getSprite(iconPath);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        return button;
    }

    public static Toggle CreateIconToggle(Transform parent, string name, string iconPath, bool value, float width,
        float height)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);
        var background = obj.GetComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/button");
        background.type = Image.Type.Sliced;

        var baseIcon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        baseIcon.transform.SetParent(obj.transform, false);
        Stretch(baseIcon.GetComponent<RectTransform>(), 5f, 5f, 4f, 4f);
        var baseImage = baseIcon.GetComponent<Image>();
        baseImage.sprite = SpriteTextureLoader.getSprite(iconPath);
        baseImage.color = ColorStyleLibrary.m.favorite_not_selected;
        baseImage.preserveAspect = true;
        baseImage.raycastTarget = false;

        var selectedIcon = new GameObject("Selected", typeof(RectTransform), typeof(Image));
        selectedIcon.transform.SetParent(obj.transform, false);
        Stretch(selectedIcon.GetComponent<RectTransform>(), 5f, 5f, 4f, 4f);
        var selectedImage = selectedIcon.GetComponent<Image>();
        selectedImage.sprite = SpriteTextureLoader.getSprite(iconPath);
        selectedImage.color = ColorStyleLibrary.m.favorite_selected;
        selectedImage.preserveAspect = true;
        selectedImage.raycastTarget = false;

        var toggle = obj.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = selectedImage;
        toggle.isOn = value;
        return toggle;
    }

    public static void SetButtonIcon(Button button, string iconPath, bool flipHorizontal = false)
    {
        var icon = button.transform.Find("Icon").GetComponent<Image>();
        icon.sprite = SpriteTextureLoader.getSprite(iconPath);
        var scale = icon.rectTransform.localScale;
        scale.x = flipHorizontal ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        icon.rectTransform.localScale = scale;
    }

    public static void SetTooltip(GameObject target, string title, string description, string detail = null)
    {
        var tipButton = PrepareTipButton(target);
        tipButton.type = WorldboxGame.Tooltips.RawTip.id;
        tipButton.textOnClick = title;
        tipButton.textOnClickDescription = description;
        tipButton.text_description_2 = detail;
        tipButton.clickAction = null;
        tipButton.setHoverAction(tipButton.showTooltipDefault);
    }

    public static void SetTooltip(InputField input, string title, string description, string detail = null)
    {
        var icon = input.transform.Find("SearchIcon");
        var target = icon == null ? CreateInputTooltipIcon(input) : icon.gameObject;
        SetTooltip(target, title, description, detail);
    }

    public static void SetTooltip(Toggle toggle, string title, string description, string detail = null)
    {
        var icon = toggle.transform.Find("Icon");
        if (icon == null) icon = toggle.transform.Find("Box");
        if (icon == null)
        {
            throw new InvalidOperationException($"Toggle {toggle.name} 缺少可承载 TipButton 的图标");
        }

        var target = icon.gameObject;
        SetTooltip(target, title, description, detail);
        var button = target.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => toggle.isOn = !toggle.isOn);
    }

    public static void SetTooltip(GameObject target, TooltipAction action)
    {
        var tipButton = PrepareTipButton(target);
        tipButton.clickAction = null;
        tipButton.setHoverAction(action);
    }

    private static TipButton PrepareTipButton(GameObject target)
    {
        if (!target.TryGetComponent<Button>(out _) && !target.TryGetComponent<Slider>(out _))
        {
            if (target.GetComponent<Selectable>() != null)
            {
                throw new InvalidOperationException($"TipButton 目标 {target.name} 不能直接使用 Selectable 控件");
            }
            var hoverButton = target.AddComponent<Button>();
            hoverButton.transition = Selectable.Transition.None;
            hoverButton.targetGraphic = null;
            hoverButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var graphic = target.GetComponent<Graphic>();
            if (graphic != null) graphic.raycastTarget = true;
        }

        var tipButton = target.GetComponent<TipButton>() ?? target.AddComponent<TipButton>();
        return tipButton;
    }

    private static GameObject CreateInputTooltipIcon(InputField input)
    {
        var icon = new GameObject("TooltipIcon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(input.transform, false);
        var rect = icon.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(12f, 12f);
        rect.anchoredPosition = new Vector2(-8f, 0f);
        var image = icon.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite(WanfaUiIcons.Overview);
        image.preserveAspect = true;

        var textRect = input.textComponent.rectTransform;
        textRect.offsetMax = new Vector2(-18f, textRect.offsetMax.y);
        var placeholderRect = input.placeholder.GetComponent<RectTransform>();
        placeholderRect.offsetMax = new Vector2(-18f, placeholderRect.offsetMax.y);
        return icon;
    }

    public static InputField CreateInput(Transform parent, string name, string value, string placeholder,
        float width, float height)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);
        var image = obj.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        image.type = Image.Type.Sliced;

        var text = CreateText(obj.transform, "Text", value, width - 8f, height, 7, TextAnchor.MiddleLeft);
        Stretch(text.rectTransform, 4f, 4f, 1f, 1f);
        var placeholderText = CreateText(obj.transform, "Placeholder", placeholder, width - 8f, height, 7,
            TextAnchor.MiddleLeft);
        placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
        Stretch(placeholderText.rectTransform, 4f, 4f, 1f, 1f);

        var input = obj.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.text = value;
        return input;
    }

    /// <summary>
    /// 克隆原版设置窗口的滑块区域，使自定义窗口沿用原版槽、填充、手柄和滚轮转发行为。
    /// </summary>
    public static SliderExtended CreateNativeSlider(Transform parent, string name, float width, float height,
        float minValue, float maxValue, float value)
    {
        _settingButtonTemplate ??= Resources.Load<GameObject>("ui/SettingButton");
        var sliderAreaTemplate = _settingButtonTemplate?.transform.Find("SliderArea");
        if (sliderAreaTemplate == null)
            throw new InvalidOperationException("原版 ui/SettingButton 缺少 SliderArea");

        var sliderArea = UnityEngine.Object.Instantiate(sliderAreaTemplate.gameObject, parent, false);
        sliderArea.name = name;
        sliderArea.SetActive(true);
        SetLayout(sliderArea.transform, width, height);
        sliderArea.transform.localScale = Vector3.one;

        var slider = sliderArea.GetComponentInChildren<SliderExtended>(true);
        if (slider == null)
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
        var obj = new GameObject(name, typeof(RectTransform), typeof(Toggle), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(obj.transform, false);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 0.5f);
        boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.sizeDelta = new Vector2(12f, 12f);
        boxRect.anchoredPosition = new Vector2(6f, 0f);
        box.GetComponent<Image>().sprite = SpriteTextureLoader.getSprite("ui/button");

        var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        Stretch(check.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
        check.GetComponent<Image>().color = new Color(0.3f, 0.9f, 0.55f, 1f);

        var text = CreateText(obj.transform, "Label", label, width - 16f, height, 7, TextAnchor.MiddleLeft);
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = Vector2.zero;

        var toggle = obj.GetComponent<Toggle>();
        toggle.targetGraphic = box.GetComponent<Image>();
        toggle.graphic = check.GetComponent<Image>();
        toggle.isOn = value;
        return toggle;
    }

    public static Transform CreateScrollContent(Transform parent, string name, float width, float height)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        SetLayout(root.transform, width, height);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(root.transform, false);
        Stretch(viewport.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return content.transform;
    }

    /// <summary>创建固定列数、按行向下扩展的滚动网格内容。</summary>
    public static Transform CreateScrollGridContent(Transform parent, string name, float width, float height,
        int columns, Vector2 cellSize, Vector2 spacing)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        SetLayout(root.transform, width, height);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(root.transform, false);
        Stretch(viewport.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        var layout = content.GetComponent<GridLayoutGroup>();
        layout.cellSize = cellSize;
        layout.spacing = spacing;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Mathf.Max(1, columns);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return content.transform;
    }

    /// <summary>
    ///     克隆原版窗口的竖向滚动条，并绑定到指定滚动内容所在的 ScrollRect。
    ///     该方法同时采用原版滚轮灵敏度，并从 viewport 右侧为滚动条预留空间。
    ///     指定右侧目标时，滚动条左缘会改为贴合该 RectTransform 的右缘。
    /// </summary>
    public static void AttachOriginalVerticalScrollbar(Transform content, Transform scrollbarMaskTemplate,
        RectTransform rightSideTarget = null)
    {
        if (content == null || scrollbarMaskTemplate == null) return;
        if (content.parent is not RectTransform viewport || viewport.parent is not RectTransform root) return;
        var scroll = root.GetComponent<ScrollRect>();
        if (scroll == null || scroll.verticalScrollbar != null) return;

        scroll.scrollSensitivity = 60f;
        viewport.offsetMax = new Vector2(-(OriginalVerticalScrollbarReservedWidth + 2f), viewport.offsetMax.y);

        var maskObject = UnityEngine.Object.Instantiate(scrollbarMaskTemplate.gameObject, root, false);
        maskObject.name = "Scrollbar Vertical Mask";
        maskObject.SetActive(true);
        var maskRect = maskObject.GetComponent<RectTransform>();
        maskRect.anchorMin = new Vector2(1f, 0f);
        maskRect.anchorMax = Vector2.one;
        maskRect.pivot = new Vector2(1f, 0.5f);
        maskRect.anchoredPosition = Vector2.zero;
        maskRect.sizeDelta = new Vector2(OriginalScrollbarWidth, -4f);
        maskRect.localScale = Vector3.one;
        if (rightSideTarget != null)
        {
            var targetRightWorld = rightSideTarget.TransformPoint(
                new Vector3(rightSideTarget.rect.xMax, 0f, 0f));
            var targetRightInRoot = root.InverseTransformPoint(targetRightWorld).x;
            maskRect.anchoredPosition = new Vector2(
                targetRightInRoot - root.rect.xMax + maskRect.sizeDelta.x,
                maskRect.anchoredPosition.y);
        }
        var rectMask = maskObject.GetComponent<RectMask2D>();
        if (rectMask != null) rectMask.enabled = true;

        var scrollbar = maskObject.GetComponentInChildren<Scrollbar>(true);
        if (scrollbar == null)
        {
            UnityEngine.Object.Destroy(maskObject);
            return;
        }

        var scrollbarRect = scrollbar.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = Vector2.zero;
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.offsetMin = Vector2.zero;
        scrollbarRect.offsetMax = Vector2.zero;
        scrollbarRect.localScale = Vector3.one;

        var backgroundRect = scrollbar.transform.Find("Background") as RectTransform;
        if (backgroundRect != null)
        {
            var backgroundX = backgroundRect.anchoredPosition.x;
            var backgroundWidth = backgroundRect.sizeDelta.x;
            backgroundRect.anchorMin = new Vector2(0.5f, 0f);
            backgroundRect.anchorMax = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = new Vector2(backgroundX, 0f);
            backgroundRect.sizeDelta = new Vector2(backgroundWidth, 0f);
        }

        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.value = 1f;
        scrollbar.gameObject.SetActive(true);

        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.verticalScrollbarSpacing = 0f;
    }

    public static void SetLayout(Transform transform, float width, float height)
    {
        var rect = transform.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        var layout = transform.GetComponent<LayoutElement>() ?? transform.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f,
        float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}

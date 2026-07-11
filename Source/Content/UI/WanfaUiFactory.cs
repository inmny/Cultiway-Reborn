using Cultiway.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cultiway.Content.UI;

internal static class WanfaUiFactory
{
    public static GameObject CreateLayout(Transform parent, string name, bool horizontal, float width, float height,
        float spacing = 3f)
    {
        var type = horizontal ? typeof(HorizontalLayoutGroup) : typeof(VerticalLayoutGroup);
        var obj = new GameObject(name, typeof(RectTransform), type, typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        SetLayout(obj.transform, width, height);
        if (horizontal)
        {
            var layout = obj.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            var layout = obj.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
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

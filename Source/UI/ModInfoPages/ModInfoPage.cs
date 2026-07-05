using System;
using Cultiway.Utils;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cultiway.UI.ModInfoPages;

public abstract class ModInfoPage
{
    protected static readonly Color PrimaryTextColor = Color.white;
    protected static readonly Color MutedTextColor = new(0.78f, 0.76f, 0.68f, 1f);
    protected static readonly Color AccentTextColor = new(1f, 0.64f, 0.16f, 1f);
    protected static readonly Color GoodColor = new(0.34f, 0.82f, 0.43f, 1f);
    protected static readonly Color WarnColor = new(0.95f, 0.72f, 0.23f, 1f);
    protected static readonly Color CoolColor = new(0.33f, 0.73f, 0.9f, 1f);

    public abstract string Id { get; }
    public abstract string TitleKey { get; }
    public abstract string DescriptionKey { get; }
    public abstract string IconPath { get; }

    public Transform CreateContent(Transform parent, Transform titleTemplate, float width)
    {
        GameObject root = new($"content_{Id}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one;
        SetLayoutWidth(root.transform, width);

        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = root.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform title = CreateTitleContainer(root.transform, titleTemplate, width);
        SetLayoutWidth(title, width, false);

        BuildContent(root.transform, width);

        root.SetActive(false);
        return root.transform;
    }

    protected abstract void BuildContent(Transform root, float width);

    protected Transform CreateCard(Transform parent, string name, float width, int left = 7, int right = 7, int top = 6, int bottom = 6, float spacing = 3f, bool fitHeight = true)
    {
        GameObject card = new(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        card.transform.localScale = Vector3.one;
        SetLayoutWidth(card.transform, width);

        Image background = card.GetComponent<Image>();
        background.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        background.type = Image.Type.Sliced;
        background.color = Color.white;

        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(left, right, top, bottom);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = fitHeight ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        return card.transform;
    }

    protected Transform CreatePlainGroup(Transform parent, string name, float width, bool horizontal = false, float spacing = 3f, TextAnchor alignment = TextAnchor.MiddleLeft, bool fitHeight = true)
    {
        Type layoutType = horizontal ? typeof(HorizontalLayoutGroup) : typeof(VerticalLayoutGroup);
        GameObject group = new(name, typeof(RectTransform), layoutType, typeof(LayoutElement));
        group.transform.SetParent(parent, false);
        group.transform.localScale = Vector3.one;
        SetLayoutWidth(group.transform, width);
        if (fitHeight)
        {
            ContentSizeFitter fitter = group.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        if (horizontal)
        {
            HorizontalLayoutGroup layout = group.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            VerticalLayoutGroup layout = group.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        return group.transform;
    }

    protected Text AddText(Transform parent, string name, string text, int fontSize, FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft, Color? color = null, float width = -1f)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        obj.transform.localScale = Vector3.one;

        Text textComponent = obj.GetComponent<Text>();
        textComponent.font = UIUtils.GetCurrentFont();
        textComponent.fontSize = fontSize;
        textComponent.fontStyle = style;
        textComponent.alignment = alignment;
        textComponent.color = color ?? PrimaryTextColor;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Overflow;
        textComponent.supportRichText = true;
        textComponent.text = text;

        float resolvedWidth = width > 0f ? width : GetVerticalContentWidth(parent);
        if (resolvedWidth > 0f)
        {
            SetLayoutWidth(obj.transform, resolvedWidth);
        }

        LayoutElement layout = obj.GetComponent<LayoutElement>();
        layout.minHeight = 0f;
        layout.preferredHeight = -1f;
        layout.flexibleHeight = 0f;
        return textComponent;
    }

    protected Transform AddIconText(Transform parent, string name, string iconPath, string title, string body, float width, Color? titleColor = null)
    {
        Transform row = CreatePlainGroup(parent, name, width, true, 4f, TextAnchor.MiddleLeft);

        GameObject iconObj = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(row, false);
        iconObj.transform.localScale = Vector3.one;
        SetLayoutSize(iconObj.transform, 16f, 16f);
        SetImage(iconObj.transform, SafeSprite(iconPath));

        Transform texts = CreatePlainGroup(row, "Texts", width - 20f, false, 0f, TextAnchor.MiddleLeft, false);
        AddText(texts, "Title", title, 7, FontStyle.Bold, TextAnchor.MiddleLeft, titleColor ?? AccentTextColor);
        AddText(texts, "Body", body, 6, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
        return row;
    }

    protected Transform AddMiniCard(Transform parent, string name, string iconPath, string title, string body, float width)
    {
        Transform card = CreateCard(parent, name, width, 4, 4, 4, 4, 1f, false);
        Transform header = CreatePlainGroup(card, "Header", width - 8f, true, 3f, TextAnchor.MiddleLeft);

        GameObject iconObj = new("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(header, false);
        iconObj.transform.localScale = Vector3.one;
        SetLayoutSize(iconObj.transform, 11f, 11f);
        SetImage(iconObj.transform, SafeSprite(iconPath));

        AddText(header, "Title", title, 6, FontStyle.Bold, TextAnchor.MiddleLeft, AccentTextColor, width - 22f);
        AddText(card, "Body", body, 5, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
        return card;
    }

    protected Transform AddBadge(Transform parent, string text, float width, Color color)
    {
        GameObject badge = new("Badge", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        badge.transform.SetParent(parent, false);
        badge.transform.localScale = Vector3.one;
        SetLayoutSize(badge.transform, width, 13f);

        Image image = badge.GetComponent<Image>();
        image.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        image.type = Image.Type.Sliced;
        image.color = new Color(color.r, color.g, color.b, 0.75f);

        Text label = AddText(badge.transform, "Text", text, 5, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return badge.transform;
    }

    protected Transform AddProgress(Transform parent, string label, float value, float width, Color color)
    {
        Transform group = CreatePlainGroup(parent, "Progress", width, false, 1f);
        AddText(group, "Label", label, 6, FontStyle.Bold, TextAnchor.MiddleLeft, PrimaryTextColor);

        GameObject bar = new("Bar", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bar.transform.SetParent(group, false);
        bar.transform.localScale = Vector3.one;
        SetLayoutSize(bar.transform, width, 7f);
        Image barBackground = bar.GetComponent<Image>();
        barBackground.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        barBackground.type = Image.Type.Sliced;
        barBackground.color = new Color(0.12f, 0.13f, 0.11f, 0.85f);

        GameObject fill = new("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        fill.transform.localScale = Vector3.one;
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        fillRect.offsetMin = new Vector2(1f, 1f);
        fillRect.offsetMax = new Vector2(-1f, -1f);

        Image fillImage = fill.GetComponent<Image>();
        fillImage.sprite = SpriteTextureLoader.getSprite("ui/special/windowInnerSliced");
        fillImage.type = Image.Type.Sliced;
        fillImage.color = color;
        return group;
    }

    protected void AddBullet(Transform parent, string text)
    {
        AddText(parent, "Bullet", "- " + text, 6, FontStyle.Normal, TextAnchor.UpperLeft, MutedTextColor);
    }

    protected Transform AddDivider(Transform parent, float width)
    {
        GameObject divider = new("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        divider.transform.SetParent(parent, false);
        divider.transform.localScale = Vector3.one;
        SetLayoutSize(divider.transform, width, 1f);
        divider.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);
        return divider.transform;
    }

    protected Transform AddWideImage(Transform parent, string name, string spritePath, float contentWidth, float sideSpace = 6f, float maxHeight = -1f)
    {
        Sprite sprite = SafeSprite(spritePath);
        float availableWidth = Mathf.Max(1f, contentWidth - sideSpace * 2f);
        Vector2 imageSize = GetAspectFitSize(sprite, availableWidth, maxHeight);

        GameObject container = new(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        container.transform.SetParent(parent, false);
        container.transform.localScale = Vector3.one;
        SetLayoutSize(container.transform, contentWidth, imageSize.y);

        HorizontalLayoutGroup layout = container.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(Mathf.RoundToInt(sideSpace), Mathf.RoundToInt(sideSpace), 0, 0);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject imageObj = new("Image", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        imageObj.transform.SetParent(container.transform, false);
        imageObj.transform.localScale = Vector3.one;
        SetLayoutSize(imageObj.transform, imageSize.x, imageSize.y);
        SetImage(imageObj.transform, sprite);
        return container.transform;
    }

    protected Transform AddTwoColumnRow(Transform parent, string name, float width, float spacing = 4f)
    {
        return CreatePlainGroup(parent, name, width, true, spacing, TextAnchor.MiddleCenter);
    }

    private Transform CreateTitleContainer(Transform parent, Transform titleTemplate, float width)
    {
        Transform title;
        if (titleTemplate != null)
        {
            title = Object.Instantiate(titleTemplate.gameObject, parent, false).transform;
            title.name = "tab_title_container_" + Id;
            title.localScale = Vector3.one;
        }
        else
        {
            GameObject fallback = new("tab_title_container_" + Id, typeof(RectTransform), typeof(LayoutElement));
            fallback.transform.SetParent(parent, false);
            fallback.transform.localScale = Vector3.one;
            title = fallback.transform;
            Text fallbackTitle = AddText(title, "title_tab", TitleKey, 9, FontStyle.Bold, TextAnchor.MiddleCenter, width: width);
            fallbackTitle.gameObject.AddComponent<LocalizedText>();
        }

        title.Find("title_tab")?.GetComponent<LocalizedText>()?.setKeyAndUpdate(TitleKey);
        Sprite icon = SafeSprite(IconPath);
        SetImage(title.Find("icon_left"), icon);
        SetImage(title.Find("icon_right"), icon);
        SetLayoutWidth(title, width, false);
        return title;
    }

    protected static void SetLayoutWidth(Transform transform, float width, bool resetHeight = true)
    {
        RectTransform rect = transform.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(width, resetHeight ? 0f : rect.sizeDelta.y);
        }

        LayoutElement layout = transform.GetComponent<LayoutElement>() ?? transform.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        if (!resetHeight) return;

        layout.minHeight = -1f;
        layout.preferredHeight = -1f;
        layout.flexibleHeight = -1f;
    }

    protected static void SetLayoutSize(Transform transform, float width, float height)
    {
        RectTransform rect = transform.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(width, height);
        }

        LayoutElement layout = transform.GetComponent<LayoutElement>() ?? transform.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = 0f;
        layout.minHeight = height;
        layout.preferredHeight = height;
        layout.flexibleHeight = 0f;
    }

    private static float GetVerticalContentWidth(Transform parent)
    {
        VerticalLayoutGroup layout = parent.GetComponent<VerticalLayoutGroup>();
        if (layout == null) return -1f;

        LayoutElement parentLayout = parent.GetComponent<LayoutElement>();
        if (parentLayout == null || parentLayout.preferredWidth <= 0f) return -1f;

        float width = parentLayout.preferredWidth - layout.padding.left - layout.padding.right;
        return width > 0f ? width : -1f;
    }

    private static Vector2 GetAspectFitSize(Sprite sprite, float maxWidth, float maxHeight)
    {
        Rect rect = sprite.rect;
        float sourceWidth = rect.width > 0f ? rect.width : 1f;
        float sourceHeight = rect.height > 0f ? rect.height : 1f;
        float width = maxWidth;
        float height = width * sourceHeight / sourceWidth;
        if (maxHeight > 0f && height > maxHeight)
        {
            height = maxHeight;
            width = height * sourceWidth / sourceHeight;
        }

        return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    protected static Sprite SafeSprite(string path)
    {
        return SpriteTextureLoader.getSprite(path) ?? SpriteTextureLoader.getSprite("cultiway/icons/iconTab");
    }

    protected static void SetImage(Transform transform, Sprite sprite)
    {
        Image image = transform?.GetComponent<Image>();
        if (image == null) return;

        image.sprite = sprite;
        image.overrideSprite = sprite;
        image.preserveAspect = true;
    }
}

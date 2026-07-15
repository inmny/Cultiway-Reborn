using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>统一滚动区结构、表面、四向留白和原版竖向滚动条。</summary>
internal sealed class UiScrollPane
{
    public RectTransform Root { get; }
    public RectTransform Viewport { get; }
    public Transform Content { get; }
    public ScrollRect ScrollRect { get; }
    public GameObject ScrollbarMask { get; private set; }

    private UiScrollPane(RectTransform root, RectTransform viewport, Transform content, ScrollRect scrollRect)
    {
        Root = root;
        Viewport = viewport;
        Content = content;
        ScrollRect = scrollRect;
    }

    public static UiScrollPane CreateVertical(Transform parent, string name, float width, float height)
    {
        UiScrollPane pane = CreateRoot(parent, name, width, height);
        GameObject content = new("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(pane.Viewport, false);
        ConfigureContentRect(content.GetComponent<RectTransform>());

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        pane.ScrollRect.content = content.GetComponent<RectTransform>();
        return new UiScrollPane(pane.Root, pane.Viewport, content.transform, pane.ScrollRect);
    }

    public static UiScrollPane CreateGrid(Transform parent, string name, float width, float height, int columns,
        Vector2 cellSize, Vector2 spacing)
    {
        UiScrollPane pane = CreateRoot(parent, name, width, height);
        GameObject content = new("Content", typeof(RectTransform), typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(pane.Viewport, false);
        ConfigureContentRect(content.GetComponent<RectTransform>());

        GridLayoutGroup layout = content.GetComponent<GridLayoutGroup>();
        layout.cellSize = cellSize;
        layout.spacing = spacing;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis = GridLayoutGroup.Axis.Horizontal;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = Mathf.Max(1, columns);
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        pane.ScrollRect.content = content.GetComponent<RectTransform>();
        return new UiScrollPane(pane.Root, pane.Viewport, content.transform, pane.ScrollRect);
    }

    private static UiScrollPane CreateRoot(Transform parent, string name, float width, float height)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        UiLayout.SetSize(root.transform, width, height);

        GameObject viewport = new("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(root.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        UiLayout.Stretch(viewportRect, 2f, 2f, 2f, 2f);
        viewport.GetComponent<Image>().color = UiTheme.Current.Palette.Normal;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return new UiScrollPane(root.GetComponent<RectTransform>(), viewportRect, null, scroll);
    }

    private static void ConfigureContentRect(RectTransform contentRect)
    {
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;
    }

    public void AttachOriginalScrollbar(Transform scrollbarTemplate, RectTransform rightSideTarget = null)
    {
        if (ScrollbarMask != null) return;

        ScrollRect.scrollSensitivity = 60f;
        Viewport.offsetMax = new Vector2(
            -(UiTheme.Current.Metrics.ScrollbarReservedWidth + UiTheme.Current.Metrics.SpacingXs),
            Viewport.offsetMax.y);

        GameObject maskObject = UnityEngine.Object.Instantiate(scrollbarTemplate.gameObject, Root, false);
        maskObject.name = "Scrollbar Vertical Mask";
        maskObject.SetActive(true);
        RectTransform maskRect = maskObject.GetComponent<RectTransform>();
        maskRect.anchorMin = new Vector2(1f, 0f);
        maskRect.anchorMax = Vector2.one;
        maskRect.pivot = new Vector2(1f, 0.5f);
        maskRect.anchoredPosition = Vector2.zero;
        maskRect.sizeDelta = new Vector2(UiTheme.Current.Metrics.OriginalScrollbarWidth, -4f);
        maskRect.localScale = Vector3.one;
        if (rightSideTarget != null)
        {
            Vector3 targetRightWorld = rightSideTarget.TransformPoint(new Vector3(rightSideTarget.rect.xMax, 0f));
            float targetRightInRoot = Root.InverseTransformPoint(targetRightWorld).x;
            maskRect.anchoredPosition = new Vector2(
                targetRightInRoot - Root.rect.xMax + maskRect.sizeDelta.x,
                maskRect.anchoredPosition.y);
        }

        RectMask2D rectMask = maskObject.GetComponent<RectMask2D>();
        if (rectMask != null) rectMask.enabled = true;
        Scrollbar scrollbar = maskObject.GetComponentInChildren<Scrollbar>(true) ??
                              throw new InvalidOperationException("原版滚动条模板缺少 Scrollbar");
        RectTransform scrollbarRect = scrollbar.GetComponent<RectTransform>();
        UiLayout.Stretch(scrollbarRect);
        scrollbarRect.localScale = Vector3.one;

        RectTransform backgroundRect = scrollbar.transform.Find("Background") as RectTransform;
        if (backgroundRect != null)
        {
            float backgroundX = backgroundRect.anchoredPosition.x;
            float backgroundWidth = backgroundRect.sizeDelta.x;
            backgroundRect.anchorMin = new Vector2(0.5f, 0f);
            backgroundRect.anchorMax = new Vector2(0.5f, 1f);
            backgroundRect.anchoredPosition = new Vector2(backgroundX, 0f);
            backgroundRect.sizeDelta = new Vector2(backgroundWidth, 0f);
        }

        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.value = 1f;
        scrollbar.gameObject.SetActive(true);
        ScrollRect.verticalScrollbar = scrollbar;
        ScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        ScrollRect.verticalScrollbarSpacing = 0f;
        ScrollbarMask = maskObject;
    }

    public void SetSurface(UiSurface surface, float contentInset = 0f, bool excludeScrollbar = true)
    {
        Transform existing = Root.Find("Background");
        GameObject backgroundObject;
        if (existing == null)
        {
            backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(Root, false);
            backgroundObject.transform.SetAsFirstSibling();
        }
        else
        {
            backgroundObject = existing.gameObject;
        }

        float reserved = excludeScrollbar && ScrollbarMask != null
            ? UiTheme.Current.Metrics.ScrollbarReservedWidth + UiTheme.Current.Metrics.SpacingXs
            : 0f;
        UiLayout.Stretch(backgroundObject.GetComponent<RectTransform>(), 0f, reserved);
        Image background = backgroundObject.GetComponent<Image>();
        UiResources.ApplySurface(background, surface);
        background.raycastTarget = false;

        Viewport.offsetMin = new Vector2(contentInset, contentInset);
        Viewport.offsetMax = new Vector2(-(reserved + contentInset), -contentInset);
    }

    public void SetViewportInsets(float inset, bool reserveScrollbar)
    {
        float reserved = reserveScrollbar && ScrollbarMask != null
            ? UiTheme.Current.Metrics.ScrollbarReservedWidth
            : 0f;
        Viewport.offsetMin = new Vector2(inset, inset);
        Viewport.offsetMax = new Vector2(-(reserved + inset), -inset);
    }

    public void SetScrollbarVisible(bool visible)
    {
        ScrollbarMask.SetActive(visible);
        SetViewportInsets(UiTheme.Current.Metrics.SpacingMd, visible);
    }

    public void Resize(float width, float height)
    {
        UiLayout.SetSize(Root, width, height);
    }

    public void ResetToTop()
    {
        ScrollRect.verticalNormalizedPosition = 1f;
    }
}

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

    private Image surfaceBackground;
    private UiSurface surface;
    private float minimumContentInset;
    private bool excludeScrollbar;
    private bool usesWindowFrame;
    private bool surfaceConfigured;

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
            -UiTheme.Current.Metrics.ScrollbarSlotWidth,
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
        if (surfaceConfigured) ApplySurfaceLayout(true);
    }

    /// <summary>应用无独立外框语义的普通表面和统一 Viewport 留白。</summary>
    public void SetSurface(UiSurface targetSurface, float contentInset = 0f, bool reserveScrollbarSlot = true)
    {
        surface = targetSurface;
        minimumContentInset = contentInset;
        excludeScrollbar = reserveScrollbarSlot;
        usesWindowFrame = false;
        surfaceConfigured = true;
        ApplySurfaceLayout(ScrollbarMask != null && ScrollbarMask.activeSelf);
    }

    /// <summary>
    /// 应用 WindowEmpty 外框。Viewport 会自动避开 Sprite 的九宫格边界，调用方不得再把它当普通表面。
    /// </summary>
    public void SetWindowFrame(float minimumInset = 0f, bool reserveScrollbarSlot = true)
    {
        minimumContentInset = minimumInset;
        excludeScrollbar = reserveScrollbarSlot;
        usesWindowFrame = true;
        surfaceConfigured = true;
        ApplySurfaceLayout(ScrollbarMask != null && ScrollbarMask.activeSelf);
    }

    private void ApplySurfaceLayout(bool scrollbarVisible)
    {
        EnsureSurfaceBackground();
        float reserved = excludeScrollbar && ScrollbarMask != null && scrollbarVisible
            ? UiTheme.Current.Metrics.ScrollbarSlotWidth
            : 0f;
        UiLayout.Stretch(surfaceBackground.rectTransform, 0f, reserved);

        if (usesWindowFrame)
        {
            UiFrameInsets insets = UiResources.ApplyWindowFrame(surfaceBackground, minimumContentInset);
            Viewport.offsetMin = new Vector2(insets.Left, insets.Bottom);
            Viewport.offsetMax = new Vector2(-(reserved + insets.Right), -insets.Top);
            return;
        }

        UiResources.ApplySurface(surfaceBackground, surface);
        Viewport.offsetMin = new Vector2(minimumContentInset, minimumContentInset);
        Viewport.offsetMax = new Vector2(-(reserved + minimumContentInset), -minimumContentInset);
    }

    private void EnsureSurfaceBackground()
    {
        if (surfaceBackground != null) return;
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
        surfaceBackground = backgroundObject.GetComponent<Image>() ?? backgroundObject.AddComponent<Image>();
        surfaceBackground.raycastTarget = false;
    }

    public void SetScrollbarVisible(bool visible)
    {
        if (ScrollbarMask == null)
            throw new InvalidOperationException("设置滚动条显隐前必须先绑定原版滚动条模板");
        ScrollbarMask.SetActive(visible);
        if (surfaceConfigured) ApplySurfaceLayout(visible);
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

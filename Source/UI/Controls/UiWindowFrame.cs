using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>
/// 带可见 WindowEmpty 边框的容器。Root 只绘制外框，所有业务子元素必须放入 Content，
/// Content 会按 Sprite 九宫格边界和主题最小间距自动内缩。
/// </summary>
internal sealed class UiWindowFrame
{
    public RectTransform Root { get; }
    public RectTransform Content { get; }
    public Image Background { get; }
    public UiFrameInsets Insets { get; }

    private UiWindowFrame(Transform parent, string name, float minimumContentInset, bool raycastTarget)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        Root = root.GetComponent<RectTransform>();
        Background = root.GetComponent<Image>();
        Background.raycastTarget = raycastTarget;
        Insets = UiResources.ApplyWindowFrame(Background, minimumContentInset);

        GameObject content = new("Content", typeof(RectTransform));
        content.transform.SetParent(root.transform, false);
        Content = content.GetComponent<RectTransform>();
        UiLayout.Stretch(Content, Insets.Left, Insets.Right, Insets.Bottom, Insets.Top);
    }

    public static UiWindowFrame CreateOuterSize(Transform parent, string name, float width, float height,
        float minimumContentInset = 0f, bool raycastTarget = true)
    {
        UiWindowFrame frame = new(parent, name, minimumContentInset, raycastTarget);
        frame.ResizeOuter(width, height);
        return frame;
    }

    public static UiWindowFrame CreateContentSize(Transform parent, string name, float contentWidth,
        float contentHeight, float minimumContentInset = 0f, bool raycastTarget = true)
    {
        UiWindowFrame frame = new(parent, name, minimumContentInset, raycastTarget);
        frame.ResizeContent(contentWidth, contentHeight);
        return frame;
    }

    public void ResizeOuter(float width, float height)
    {
        UiLayout.SetSize(Root, width, height);
    }

    public void ResizeContent(float width, float height)
    {
        ResizeOuter(width + Insets.Horizontal, height + Insets.Vertical);
    }
}

using System;
using UnityEngine;

namespace Cultiway.UI;

/// <summary>一次解析代码构建窗口依赖的原版节点，后续控件只使用有类型引用。</summary>
internal sealed class UiWindowContext
{
    private static readonly Vector2 EmptyWindowSize = new(240f, 320f);

    public Transform Background { get; }
    public Transform OriginalScrollView { get; }
    public Transform ScrollbarTemplate { get; }

    private UiWindowContext(Transform background, Transform originalScrollView, Transform scrollbarTemplate)
    {
        Background = background;
        OriginalScrollView = originalScrollView;
        ScrollbarTemplate = scrollbarTemplate;
    }

    public static UiWindowContext Bind(Transform background, bool hideOriginalScrollView = true)
    {
        PositionBackButton(background);
        Transform originalScrollView = background.Find("Scroll View") ??
                                       throw new InvalidOperationException($"窗口 {background.name} 缺少 Scroll View");
        Transform scrollbarTemplate = originalScrollView.Find("Scrollbar Vertical Mask") ??
                                      throw new InvalidOperationException(
                                          $"窗口 {background.name} 的 Scroll View 缺少 Scrollbar Vertical Mask");
        if (hideOriginalScrollView) originalScrollView.gameObject.SetActive(false);
        return new UiWindowContext(background, originalScrollView, scrollbarTemplate);
    }

    /// <summary>按原版 empty prefab 的基准尺寸，使宽窗口的返回按钮保持原有左上角边距。</summary>
    public static void PositionBackButton(Transform background)
    {
        RectTransform backgroundRect = (RectTransform)background;
        RectTransform backButton = background.parent.Find("BackButtonContainer") as RectTransform ??
                                   throw new InvalidOperationException(
                                       $"窗口 {background.parent.name} 缺少 BackButtonContainer");
        Vector2 position = backButton.anchoredPosition;
        position.x = (EmptyWindowSize.x - backgroundRect.sizeDelta.x) * 0.5f;
        position.y = (backgroundRect.sizeDelta.y - EmptyWindowSize.y) * 0.5f;
        backButton.anchoredPosition = position;
    }
}

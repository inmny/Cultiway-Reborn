using UnityEngine;

namespace Cultiway.UI;

/// <summary>宽窗口随实际分辨率及原版界面大小缩小，避开左侧窗口工具栏。</summary>
internal sealed class UiWideWindowFit : MonoBehaviour
{
    /// <summary>原版窗口、背景及工具栏，只解析一次。</summary>
    private Transform parent;
    /// <summary>窗口背景的实际尺寸。</summary>
    private RectTransform background;
    /// <summary>原版窗口工具栏的实际宽度。</summary>
    private RectTransform toolbar;
    /// <summary>原版窗口未适配前的缩放。</summary>
    private Vector3 originalScale;
    /// <summary>上一帧已处理的屏幕及界面比例。</summary>
    private float width = -1f, height, parentScale, toolbarScale;

    /// <summary>在窗口初始化时登记尺寸，保留原版窗口动画和所有子控件。</summary>
    /// <param name="background">当前宽窗口背景。</param>
    public static void Attach(Transform background)
    {
        Transform window = background.parent;
        var fit = window.gameObject.AddComponent<UiWideWindowFit>();
        fit.parent = window.parent;
        fit.background = (RectTransform)background;
        fit.toolbar = (RectTransform)CanvasMain.instance.canvas_windows.transform.Find("WindowToolbarContainer/WindowToolbar");
        fit.originalScale = window.localScale;
    }

    /// <summary>重新打开时按当前界面大小计算，不保留上一次的屏幕条件。</summary>
    private void OnEnable() => width = -1f;

    /// <summary>等原版画布完成缩放后更新；屏幕及界面比例没变时不重新布局。</summary>
    private void LateUpdate()
    {
        float unit = parent.lossyScale.x, tools = toolbar.lossyScale.x;
        if (width == Screen.width && height == Screen.height && parentScale == unit && toolbarScale == tools) return;
        width = Screen.width; height = Screen.height; parentScale = unit; toolbarScale = tools;
        // 两侧留出工具栏同等宽度，仍沿用原版居中的窗口及滑动动画。
        float available = width - 2f * (toolbar.rect.width * tools + 12f);
        float fit = Mathf.Min(1f, available / ((background.rect.width + 16f) * unit),
            (height - 24f) / ((background.rect.height + 60f) * unit));
        transform.localScale = originalScale * fit;
    }
}

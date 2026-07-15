using UnityEngine;

namespace Cultiway.UI;

/// <summary>项目 UI 的统一主题入口，集中提供字体、尺寸和语义颜色。</summary>
internal sealed class UiTheme
{
    public static UiTheme Current { get; } = new();

    public UiMetrics Metrics { get; } = new();
    public UiPalette Palette { get; } = new();

    public Font Font => LocalizedTextManager.current_font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

    private UiTheme()
    {
    }
}

/// <summary>供不同窗口共同使用的尺寸阶梯。</summary>
internal sealed class UiMetrics
{
    public float SpacingXs => 2f;
    public float SpacingSm => 4f;
    public float SpacingMd => 6f;
    public float SpacingLg => 8f;
    public float SpacingXl => 12f;
    public float ControlSmall => 22f;
    public float ControlMedium => 24f;
    public float ControlLarge => 28f;
    public float ScrollbarReservedWidth => 18f;
    public float OriginalScrollbarWidth => 17.5f;
}

/// <summary>普通控件状态使用的语义颜色；领域可视化颜色仍由所属功能维护。</summary>
internal sealed class UiPalette
{
    public Color PrimaryText => Color.white;
    public Color MutedText => new(0.78f, 0.76f, 0.68f, 1f);
    public Color PlaceholderText => new(1f, 1f, 1f, 0.45f);
    public Color AccentText => new(1f, 0.86f, 0.55f, 1f);
    public Color Normal => Color.white;
    public Color Selected => new(0.68f, 0.68f, 0.68f, 1f);
    public Color Disabled => new(0.62f, 0.62f, 0.62f, 0.72f);
    public Color Success => new(0.3f, 0.9f, 0.55f, 1f);
    public Color Warning => new(1f, 0.82f, 0.25f, 1f);
    public Color Error => new(1f, 0.45f, 0.38f, 1f);
    public Color ErrorSurface => new(1f, 0.62f, 0.58f, 1f);
    public Color InnerPanelTint => new(0.82f, 0.82f, 0.82f, 0.94f);
}

internal enum UiSurface
{
    None,
    Button,
    WindowInner,
    WindowEmpty,
    DestructiveButton,
    ToggleBox,
}

internal enum UiControlState
{
    Normal,
    Selected,
    Disabled,
    Destructive,
    Success,
    Warning,
    Error,
}

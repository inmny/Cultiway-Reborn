using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>一次性应用控件状态，避免窗口分别修改 sprite、颜色和交互性。</summary>
internal static class UiStateStyle
{
    public static void SetSelected(Button button, bool selected)
    {
        ApplyVisual(button, selected ? UiControlState.Selected : UiControlState.Normal);
    }

    public static void ApplyVisual(Button button, UiControlState state)
    {
        Image image = button.GetComponent<Image>();
        UiResources.ApplySurface(image,
            state == UiControlState.Destructive ? UiSurface.DestructiveButton : UiSurface.Button,
            ResolveColor(state));
    }

    public static void ApplyRow(Image image, UiControlState state)
    {
        image.color = ResolveColor(state);
    }

    public static Color ResolveColor(UiControlState state)
    {
        UiPalette palette = UiTheme.Current.Palette;
        return state switch
        {
            UiControlState.Selected => palette.Selected,
            UiControlState.Disabled => palette.Disabled,
            UiControlState.Success => palette.Success,
            UiControlState.Warning => palette.Warning,
            UiControlState.Error => palette.ErrorSurface,
            _ => palette.Normal,
        };
    }
}

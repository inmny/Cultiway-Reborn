using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>列表 Row 的统一背景与可选点击表面，领域组件继续拥有具体槽位。</summary>
internal sealed class UiListRowChrome
{
    public Image Background { get; }
    public Button Button { get; }

    private UiListRowChrome(Image background, Button button)
    {
        Background = background;
        Button = button;
    }

    public static UiListRowChrome Attach(GameObject root, bool clickable)
    {
        Image background = root.GetComponent<Image>() ?? root.AddComponent<Image>();
        UiResources.ApplySurface(background, UiSurface.WindowInner);
        Button button = clickable ? root.GetComponent<Button>() ?? root.AddComponent<Button>() : null;
        if (button != null) button.targetGraphic = background;
        return new UiListRowChrome(background, button);
    }

    public static UiListRowChrome From(GameObject root)
    {
        return new UiListRowChrome(root.GetComponent<Image>(), root.GetComponent<Button>());
    }

    public void SetState(UiControlState state)
    {
        UiStateStyle.ApplyRow(Background, state);
    }
}

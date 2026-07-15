using UnityEngine.UI;

namespace Cultiway.UI;

/// <summary>为原版 PowerButton 应用统一外观，但不改变其导航、选择和拖动语义。</summary>
internal static class UiPowerButtonAdapter
{
    public static void ApplyWorldToolConfigStyle(PowerButton button)
    {
        UiResources.ApplySurface(button.GetComponent<Image>(), UiSurface.DestructiveButton);
    }
}

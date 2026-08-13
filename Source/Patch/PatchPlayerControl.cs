using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>阻止小世界和外围空白输入进入主世界 Power、Tile 与单位操作。</summary>
internal static class PatchPlayerControl
{
    [HarmonyPrefix, HarmonyPatch(typeof(PlayerControl), "updateControls")]
    private static bool updateControls_prefix()
    {
        if (ModClass.I?.SubWorldManager == null || !ModClass.I.SubWorldManager.RouteWorldInput()) return true;
        AssetManager.hotkey_library.checkHotKeyActions();
        return false;
    }
}

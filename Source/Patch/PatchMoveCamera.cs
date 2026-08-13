using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

/// <summary>把主相机移动范围扩展到当前小世界槽位，并在相机更新后同步视图。</summary>
internal static class PatchMoveCamera
{
    [HarmonyPrefix, HarmonyPatch(typeof(MoveCamera), "cameraToBounds")]
    private static bool cameraToBounds_prefix(MoveCamera __instance)
    {
        if (ModClass.I?.SubWorldManager == null ||
            !ModClass.I.SubWorldManager.TryGetCameraBounds(out Rect bounds)) return true;

        Vector3 current = __instance.transform.position;
        __instance.transform.position = new Vector3(
            Mathf.Clamp(current.x, bounds.xMin, bounds.xMax),
            Mathf.Clamp(current.y, bounds.yMin, bounds.yMax),
            -0.5f);
        World.world.nameplate_manager.update();
        return false;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(MoveCamera), nameof(MoveCamera.update))]
    private static void update_postfix()
    {
        ModClass.I?.SubWorldManager?.UpdateWorldViews();
    }
}

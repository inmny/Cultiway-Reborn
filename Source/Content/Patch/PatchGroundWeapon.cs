using Cultiway.Content.Combat;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>地面已有真实归属的物品不能被原版当作遗失的无主物清理。</summary>
internal static class PatchGroundWeapon
{
    /// <summary>只保护器物玩法已登记的原物品，不改变其他物品的销毁规则。</summary>
    /// <param name="__instance">原物品。</param><param name="__result">原版清理结果。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Item), nameof(Item.isReadyForRemoval))]
    private static bool KeepGroundItem(Item __instance, ref bool __result)
    {
        if (!GroundWeaponService.IsRetained(__instance)) return true;
        __result = false;
        return false;
    }
}

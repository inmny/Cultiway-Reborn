using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchKingdom
{
    [HarmonyPrefix, HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getColor))]
    private static void GetColor_prefix(Kingdom __instance)
    {
        if (__instance.asset != null) return;

        var actorAssetId = __instance.data?.original_actor_asset;
        if (!string.IsNullOrEmpty(actorAssetId))
        {
            var actorAsset = AssetManager.actor_library.get(actorAssetId);
            if (actorAsset != null && !string.IsNullOrEmpty(actorAsset.kingdom_id_civilization))
            {
                __instance.asset = AssetManager.kingdoms.get(actorAsset.kingdom_id_civilization);
            }
        }
        __instance.asset ??= AssetManager.kingdoms.get("neutral");
    }
}

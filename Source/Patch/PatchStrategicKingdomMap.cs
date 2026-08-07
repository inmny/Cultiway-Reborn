using Cultiway.Content;
using Cultiway.Content.MapModeVisuals;
using Cultiway.Core;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchStrategicKingdomMap
{
    [HarmonyPostfix, HarmonyPatch(typeof(ZoneCalculator), nameof(ZoneCalculator.updateAnimationsAndSelections))]
    private static void updateAnimationsAndSelections_postfix(ZoneCalculator __instance)
    {
        if (IsActive()) __instance.sprRnd.enabled = false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(QuantumSpriteLibrary), "drawCursorZones")]
    private static bool drawCursorZones_prefix()
    {
        return !IsActive();
    }

    [HarmonyPrefix, HarmonyPatch(typeof(TileZone), "setCity")]
    private static void setCity_prefix(TileZone __instance, out long __state)
    {
        __state = ResolveOwnerId(__instance.city?.kingdom);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(TileZone), "setCity")]
    private static void setCity_postfix(TileZone __instance, long __state)
    {
        if (__state == ResolveOwnerId(__instance.city?.kingdom)) return;
        if (TryGetRenderer(out KingdomMapRenderer renderer)) renderer.MarkZoneDirty(__instance);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(City), "setKingdom")]
    private static void setKingdom_prefix(City __instance, out long __state)
    {
        __state = ResolveOwnerId(__instance.kingdom);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(City), "setKingdom")]
    private static void setKingdom_postfix(City __instance, long __state)
    {
        if (__state == ResolveOwnerId(__instance.kingdom)) return;
        if (TryGetRenderer(out KingdomMapRenderer renderer)) renderer.MarkCityDirty(__instance);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(KingdomManager), nameof(KingdomManager.removeObject))]
    private static void removeKingdom_prefix(Kingdom pKingdom)
    {
        if (pKingdom != null && TryGetRenderer(out KingdomMapRenderer renderer))
            renderer.MarkKingdomDirty(pKingdom);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(MetaObjectData), nameof(MetaObjectData.setColorID))]
    private static void setColorID_postfix(MetaObjectData __instance)
    {
        if (__instance is not KingdomData) return;
        if (TryGetRenderer(out KingdomMapRenderer renderer)) renderer.MarkColorsDirty();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.finishMakingWorld))]
    private static void finishMakingWorld_postfix()
    {
        ModClass.I.CustomMapModeManager.SetAllDirty();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.clearWorld))]
    private static void clearWorld_postfix()
    {
        ModClass.I.CustomMapModeManager.ClearWorldRenderers();
    }

    private static bool TryGetRenderer(out KingdomMapRenderer renderer)
    {
        renderer = null;
        if (!IsActive()) return false;
        return ModClass.I.CustomMapModeManager.TryGetRenderer(MapModes.StrategicKingdom, out renderer);
    }

    private static long ResolveOwnerId(Kingdom kingdom)
    {
        return kingdom == null || kingdom.isRekt() || kingdom.isNeutral() ? 0 : kingdom.getID();
    }

    private static bool IsActive()
    {
        return ModClass.I?.CustomMapModeManager?.CurrMapMode == MapModes.StrategicKingdom;
    }
}

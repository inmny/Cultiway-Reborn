using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchZones
{
    [HarmonyPostfix, HarmonyPatch(typeof(Zones), nameof(Zones.getCurrentMapBorderMode))]
    private static void getCurrentMapBorderMode_postfix(ref MetaType __result, bool pCheckOnlyOption = false)
    {
        if (__result != MetaType.None) return;
        CustomMapModeAsset current = ModClass.I.CustomMapModeManager.CurrMapMode;
        if (current != null && current.redirect_map_mode != MetaTypeExtend.None)
        {
            __result = current.redirect_map_mode.Back();
        }
        else if (WorldboxGame.MetaTypes.Sect.isActive(pCheckOnlyOption))
        {
            __result = MetaTypeExtend.Sect.Back();
        }
        else if (
            WorldboxGame.MetaTypes.GeoRegion.isActive(pCheckOnlyOption)
            || ModClass.I.CustomMapModeManager.CurrMapMode == CustomMapModeLibrary.GeoRegionLandform
            || ModClass.I.CustomMapModeManager.CurrMapMode == CustomMapModeLibrary.GeoRegionMorphology
            || ModClass.I.CustomMapModeManager.CurrMapMode == CustomMapModeLibrary.GeoRegionLandmass
        )
        {
            __result = MetaTypeExtend.GeoRegion.Back();
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Zones), nameof(Zones.getMapMetaAsset))]
    private static void getMapMetaAsset_postfix(ref MetaTypeAsset __result)
    {
        if (__result != null) return;
        CustomMapModeAsset current = ModClass.I.CustomMapModeManager.CurrMapMode;
        if (current == null || current.redirect_map_mode == MetaTypeExtend.None) return;
        __result = current.redirect_map_mode.Back().getAsset();
    }
}

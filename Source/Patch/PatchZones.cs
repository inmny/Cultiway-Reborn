using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchZones
{
    [HarmonyPostfix, HarmonyPatch(typeof(Zones), nameof(Zones.getCurrentMapBorderMode))]
    private static void getCurrentMapBorderMode_postfix(ref MetaType __result, bool pCheckOnlyOption = false)
    {
        if (__result != MetaType.None) return;
        if (WorldboxGame.MetaTypes.Sect.isActive(pCheckOnlyOption))
        {
            __result = MetaTypeExtend.Sect.Back();
        }
        else if (WorldboxGame.MetaTypes.GeoRegion.isActive(pCheckOnlyOption))
        {
            __result = MetaTypeExtend.GeoRegion.Back();
        }
    }
}
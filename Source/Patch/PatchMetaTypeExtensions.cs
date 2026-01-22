using Cultiway.Const;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchMetaTypeExtensions
{
    [HarmonyPrefix, HarmonyPatch(typeof(MetaTypeExtensions), nameof(MetaTypeExtensions.AsString))]
    private static bool AsString_prefix(MetaType pType, ref string __result)
    {
        switch (pType.Extend())
        {
            case MetaTypeExtend.Sect:
                __result = "sect";
                return false;
            case MetaTypeExtend.GeoRegion:
                __result = "geo_region";
                return false;
        }
        return true;
    }
}
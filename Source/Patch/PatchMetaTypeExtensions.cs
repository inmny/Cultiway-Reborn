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
                
                return false;
        }
        return true;
    }
}
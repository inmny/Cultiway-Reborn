using Cultiway.Core;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>
/// 监听城市领地和国家归属变化，通知地区界面重新统计其中显示的城市与国家。
/// </summary>
internal static class PatchGeoRegionComposition
{
    /// <summary>城市接管地块前记住原城市，以便事后判断玩家可见的地区组成是否变化。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(TileZone), "setCity")]
    private static void setCity_prefix(TileZone __instance, out City __state)
    {
        __state = __instance.city;
    }

    /// <summary>地块所属城市变化后，通知相关地区更新国家和城市列表。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(TileZone), "setCity")]
    private static void setCity_postfix(TileZone __instance, City __state)
    {
        if (ReferenceEquals(__state, __instance.city)) return;
        WorldboxGame.I?.GeoRegions?.NotifyZoneCompositionChanged(__instance);
    }

    /// <summary>城市更换国家前记住原国家。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(City), "setKingdom")]
    private static void setKingdom_prefix(City __instance, out Kingdom __state)
    {
        __state = __instance.kingdom;
    }

    /// <summary>城市所属国家变化后，通知包含该城市的地区更新展示。</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(City), "setKingdom")]
    private static void setKingdom_postfix(City __instance, Kingdom __state)
    {
        if (ReferenceEquals(__state, __instance.kingdom)) return;
        WorldboxGame.I?.GeoRegions?.NotifyCityCompositionChanged(__instance);
    }
}

using Cultiway.Content.UI.VillageDuties;
using Cultiway.Content.UI.KingdomPolicies;
using Cultiway.Content.VillageDuties;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>在原版村庄、军队和人物详情中提供制度与军功入口。</summary>
internal static class PatchVillageDutyInfo
{
    /// <summary>国家详情直接进入本国政策卡。</summary>
    /// <param name="__instance">原版国家详情窗口。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(KingdomWindow), nameof(KingdomWindow.showStatsRows))]
    private static void KingdomEntry(KingdomWindow __instance)
    {
        var kingdom = __instance.meta_object;
        if (kingdom == null) return;
        var row = __instance.showStatRow("Cultiway.Policies.Title", WindowKingdomPolicies.T("Open"), pIconPath: "iconKingdomList");
        row.on_click_value = () => WindowKingdomPolicies.Open(kingdom);
    }

    /// <summary>村庄窗口始终使用当前选中村庄，刷新时覆盖单个回调。</summary>
    /// <param name="__instance">原版村庄详情窗口。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(CityWindow), nameof(CityWindow.showStatsRows))]
    private static void CityEntry(CityWindow __instance)
    {
        var city = __instance.meta_object;
        if (city == null) return;
        var row = __instance.showStatRow("Cultiway.Village.Title", WindowVillageDuties.T("Open"), pIconPath: "iconArmy");
        row.on_click_value = () => WindowVillageDuties.Open(city);
    }

    /// <summary>原版军队页面回到所属村庄的唯一军队编制。</summary>
    /// <param name="__instance">原版军队详情窗口。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(ArmyWindow), nameof(ArmyWindow.showStatsRows))]
    private static void ArmyEntry(ArmyWindow __instance)
    {
        var city = __instance.meta_object?.getCity();
        if (city == null) return;
        var row = __instance.showStatRow("Cultiway.Village.Organization", WindowVillageDuties.T("Open"), pIconPath: "iconArmy");
        row.on_click_value = () => WindowVillageDuties.Open(city, 2);
    }

    /// <summary>所有智慧居民都能查看军功，退役和特殊身份不会隐藏记录。</summary>
    /// <param name="__instance">原版人物详情窗口。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(UnitWindow), nameof(UnitWindow.showStatsRows))]
    private static void PersonalMerit(UnitWindow __instance)
    {
        var actor = __instance.actor;
        if (!VillageDutyService.IsResident(actor)) return;
        var row = __instance.showStatRow("Cultiway.Village.Merit", MilitaryMeritService.Get(actor), pIconPath: "iconKills");
        row.on_click_value = () => WindowVillageDuties.Open(actor.city, 3);
    }
}

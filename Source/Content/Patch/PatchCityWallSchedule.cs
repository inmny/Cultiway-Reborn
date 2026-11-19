using ai.behaviours;
using Cultiway.Content.Systems.Logic;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Patch;

/// <summary>
/// 在城市更新时调度城墙与护城河，并在大厅施工完成后立即更新独立护城河。
/// </summary>
internal static class PatchCityWallSchedule
{
    [Hotfixable]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityBehCheckCitizenTasks), nameof(CityBehCheckCitizenTasks.execute))]
    private static void execute_postfix(City pCity)
    {
        Plots.TryScheduleHumanWall(pCity);
    }

    /// <summary>大厅的新建和升级施工真正完成后，按已记录的等级更新一次护城河。</summary>
    /// <param name="__instance">刚刚结束施工的建筑。</param>
    [Hotfixable]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Building), nameof(Building.completeConstruction))]
    private static void HallConstructionCompleted(Building __instance)
    {
        if (__instance.asset.type == "type_hall" && __instance.city?.data != null)
        {
            Plots.TryUpdateCityMoat(__instance.city);
            OfficialRoadSystem.RequestRefresh();
        }
    }
}

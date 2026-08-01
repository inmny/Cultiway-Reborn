using ai.behaviours;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Patch;

/// <summary>
/// 在城市更新循环里驱动东方人族的城墙修筑调度（见 <see cref="Plots.TryScheduleEasternHumanWall"/>）。
/// 挂在原版 <c>CityBehCheckCitizenTasks.execute</c> 之后，按城市逐个检查。
/// </summary>
internal static class PatchCityWallSchedule
{
    [Hotfixable]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityBehCheckCitizenTasks), nameof(CityBehCheckCitizenTasks.execute))]
    private static void execute_postfix(City pCity)
    {
        Plots.TryScheduleEasternHumanWall(pCity);
    }
}

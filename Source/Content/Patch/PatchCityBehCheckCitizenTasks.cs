using ai.behaviours;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Patch;

internal static class PatchCityBehCheckCitizenTasks
{
    [Hotfixable]
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CityBehCheckCitizenTasks), nameof(CityBehCheckCitizenTasks.execute))]
    private static void execute_postfix(CityBehCheckCitizenTasks __instance, City pCity)
    {
        if (!DebugConfig.isOn(DebugOption.SystemCityTasks)) return;
        if (__instance._citizens_left <= 0) return;

        global::CitizenJobs jobs_container = pCity.jobs;
        __instance.addToJob(CitizenJobs.HerbCollector, jobs_container, 1, 1);
    }
}
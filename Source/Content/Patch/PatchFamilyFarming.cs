using System.Linq;
using ai.behaviours;
using Cultiway.Content.FamilyFarming;
using Cultiway.Content.UI.VillageDuties;
using Cultiway.Content.VillageDuties;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>替换原版风车半径农田入口，并在保存和作物生长时应用家庭田地规则。</summary>
internal static class PatchFamilyFarming
{
    /// <summary>全村缓存来自家庭田块，不再执行围绕风车的原版扫描。</summary>
    /// <param name="pCity">准备刷新农业缓存的村庄。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(CityBehCheckFarms), nameof(CityBehCheckFarms.check))]
    private static bool FarmCache(City pCity)
    {
        if (!FamilyFarmService.IsPrivate(pCity)) return true;
        FamilyFarmService.RefreshFarmCache(pCity); return false;
    }

    /// <summary>公有制继续使用原版风车范围，同时允许收完切换前留下的作物。</summary>
    /// <param name="pCity">原版刚完成农田扫描的村庄。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(CityBehCheckFarms), nameof(CityBehCheckFarms.check))]
    private static void PublicCrops(City pCity) { if (!FamilyFarmService.IsPrivate(pCity)) FamilyFarmService.AppendPublicCrops(pCity); }

    /// <summary>用家庭归属和土地合法性替代原版依赖临时缓存的农田衰退判断。</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(WorldBehaviourActions), nameof(WorldBehaviourActions.updateFarmDecay))]
    private static bool Decay() { FamilyFarmService.UpdateDecay(); return false; }

    /// <summary>内城里的作物立即停止生长，成熟者保留一次收获。</summary>
    /// <param name="__instance">即将生长的麦类作物。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Wheat), nameof(Wheat.update))]
    private static bool Grow(Wheat __instance) => !Config.game_loaded || SmoothLoader.isLoading() || FamilyFarmService.CanGrow(__instance.building);

    /// <summary>村庄存档保存唯一的家庭经营记录。</summary>
    /// <param name="__instance">正在保存的村庄。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.save))]
    private static void Save(City __instance) => FamilyFarmService.Save(__instance);

    /// <summary>结束工作时释放本轮农活和加工工位。</summary>
    /// <param name="__instance">结束工作的人物。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.endJob))]
    private static void Release(Actor __instance) => FamilyFarmService.Release(__instance);

    /// <summary>领地变化后只标记范围失效，不立即重复扫描土地。</summary>
    /// <param name="__instance">改变领地的村庄。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.addZone))]
    private static void LandAdded(City __instance) => FamilyFarmService.InvalidateLand(__instance);

    /// <summary>失去领地后更新内城轮廓和农田合法性。</summary>
    /// <param name="__instance">失去领地的村庄。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(City), nameof(City.removeZone))]
    private static void LandRemoved(City __instance) => FamilyFarmService.InvalidateLand(__instance);

    /// <summary>施肥和神力催熟也不能绕过内城立即停耕。</summary>
    /// <param name="__instance">准备直接催熟的作物。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(Wheat), nameof(Wheat.growFull))]
    private static bool GrowFull(Wheat __instance) => !Config.game_loaded || SmoothLoader.isLoading() || FamilyFarmService.CanGrow(__instance.building);

    /// <summary>村庄销毁时丢弃运行引用。</summary>
    /// <param name="__instance">即将消亡的村庄。</param>
    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.destroyCity))]
    private static void Remove(City __instance) => FamilyFarmService.Remove(__instance);

    /// <summary>原粮积压且没有风车时，优先选择已经通过原版材料和技术条件的风车建设。</summary>
    /// <param name="pCity">原版正在决定建设内容的村庄。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.calcPossibleBuildings))]
    private static void PrioritizeMill(City pCity)
    {
        if (pCity.getResourcesAmount(FamilyFarmService.RawGrainId) == 0 || pCity.countBuildingsType("type_windmill", false) > 0) return;
        var mill = CityBehBuild._possible_buildings.FirstOrDefault(o => !o.upgrade && o.getBuildingAsset(pCity).type == "type_windmill");
        if (mill == null) return;
        CityBehBuild._possible_buildings.Clear(); CityBehBuild._possible_buildings.Add(mill);
    }

    /// <summary>在原版家庭详情中进入该家庭成员实际居住村庄的农田页。</summary>
    /// <param name="__instance">当前原版家庭详情。</param>
    [HarmonyPostfix, HarmonyPatch(typeof(FamilyWindow), nameof(FamilyWindow.showStatsRows))]
    private static void FamilyEntry(FamilyWindow __instance)
    {
        var family = __instance.meta_object;
        var city = family?.units.FirstOrDefault(a => a.hasCity() && a.isAlive())?.city;
        if (city == null) return;
        var row = __instance.showStatRow("Cultiway.Village.Farms", WindowVillageDuties.T("Open"), pIconPath: "citizen_jobs/iconCitizenJobFarmer");
        row.on_click_value = () => WindowVillageDuties.OpenFamilyFarms(city, family);
    }
}

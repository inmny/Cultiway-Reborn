using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai.behaviours;
using Cultiway.Content;
using HarmonyLib;
using strings;
using UnityEngine;

namespace Cultiway.Patch;

/// <summary>
/// 「厅火之邑」城市布局的建筑聚集锚点补丁。
/// 原版 CityBehBuild.tryToBuildInZones 以 City.getTile()(= 篝火)为锚点，
/// 每次建造挑选离锚点最近的可用格，使城市从篝火向外填充。
/// 这里把该锚点调用改写为 GetClusteringCenter：拥有「厅火之邑」文化且已修建
/// 村庄大厅(type_hall)时改为围绕大厅，否则回退原版篝火。
/// 仅影响建筑排布的聚集中心，城市扩张/寻路/外交等仍沿用篝火锚点。
/// </summary>
internal static class PatchCityBehBuild
{
    [HarmonyTranspiler, HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.buildTick))]
    private static IEnumerable<CodeInstruction> buildTick_house_upgrade_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        MethodInfo getBuildingList = AccessTools.Method(typeof(City), nameof(City.getBuildingListOfID));
        MethodInfo selector = AccessTools.Method(typeof(PatchCityBehBuild), nameof(SelectBuildingForUpgrade));

        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].Calls(getBuildingList)) continue;
            for (int j = i + 1; j < list.Count; j++)
            {
                if (list[j].operand is not MethodInfo method || method.Name != "GetRandom") continue;
                list.Insert(j, new CodeInstruction(OpCodes.Ldarg_0));
                list[j + 1].opcode = OpCodes.Call;
                list[j + 1].operand = selector;
                return list;
            }
        }

        ModClass.LogWarningConcurrent("[CityBuild] transpiler: 未找到民房升级随机选择点，中心优先升级未生效");
        return list;
    }

    private static Building SelectBuildingForUpgrade(List<Building> buildings, City city)
    {
        if (buildings == null || buildings.Count == 0) return null;
        if (buildings[0]?.asset?.type != S_BuildingType.type_house) return buildings.GetRandom();

        Building selected = null;
        float selectedDistance = float.MaxValue;
        foreach (Building building in buildings)
        {
            if (!PatchBuilding.CanClearUpgradeFootprint(building)) continue;
            Vector2 position = building.current_tile.pos;
            float distance = (position - city.city_center).sqrMagnitude;
            if (distance >= selectedDistance) continue;
            selected = building;
            selectedDistance = distance;
        }
        return selected;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(City), nameof(City.hasSpecialTownPlans))]
    private static bool hasSpecialTownPlans_prefix(City __instance, ref bool __result)
    {
        if (__instance == null || !__instance.hasCulture()
                               || !__instance.culture.hasTrait(CultureTraits.HallHearthId)) return true;
        __result = false;
        return false;
    }

    /// <summary>
    /// 建筑聚集锚点。第二个 bool 参数仅为对齐 City.getTile(bool) 的调用栈（原调用点已压入默认实参 false），不使用。
    /// </summary>
    public static WorldTile GetClusteringCenter(City pCity, bool _)
    {
        if (pCity != null && pCity.hasCulture()
            && pCity.culture.hasTrait(CultureTraits.HallHearthId))
        {
            Building hall = pCity.getBuildingOfType(S_BuildingType.type_hall);
            if (hall != null)
            {
                return hall.current_tile;
            }
        }
        return pCity.getTile();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(CityBehBuild), nameof(CityBehBuild.tryToBuildInZones))]
    private static IEnumerable<CodeInstruction> tryToBuildInZones_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        var helper = AccessTools.Method(typeof(PatchCityBehBuild), nameof(GetClusteringCenter));

        int patched = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var instr = list[i];
            if ((instr.opcode == OpCodes.Call || instr.opcode == OpCodes.Callvirt)
                && instr.operand is MethodInfo mi
                && mi.Name == nameof(City.getTile)
                && mi.DeclaringType == typeof(City))
            {
                instr.opcode = OpCodes.Call;
                instr.operand = helper;
                patched++;
            }
        }

        if (patched == 0)
        {
            ModClass.LogWarningConcurrent("[HallHearth] transpiler: 未在 CityBehBuild.tryToBuildInZones 找到 City.getTile 调用，布局补丁未生效");
        }
        else if (patched > 1)
        {
            ModClass.LogWarningConcurrent($"[HallHearth] transpiler: 替换了 {patched} 处 getTile 调用（预期仅 1 处）");
        }

        return list;
    }
}

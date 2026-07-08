using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Core;
using Cultiway.Content;
using Cultiway.Core.BuildingComponents;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchBuilding
{
    [HarmonyTranspiler, HarmonyPatch(typeof(Building), nameof(Building.setBuilding))]
    private static IEnumerable<CodeInstruction> setBuilding_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var index = list.FindIndex(x =>
            x.opcode == OpCodes.Ldfld && (x.operand as FieldInfo)?.Name == nameof(BuildingAsset.tower));
        if (index != -1)
        {
            var insert_idx = index - 1;
            var old_instruction = list[insert_idx];
            list.InsertRange(insert_idx, new []
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchBuilding), nameof(checkMoreBuildingComponents)))
            });
            old_instruction.MoveLabelsTo(list[insert_idx]);
        }
        return list;
    }

    private static void checkMoreBuildingComponents(Building building)
    {
        var bae = building.asset.GetExtend<BuildingAssetExtend>();
        if (bae.advanced_unit_spawner)
        {
            building.addComponent<AdvancedUnitSpawner>().Setup(bae.advanced_unit_spawner_config);
        }
        foreach (var portal_asset in ModClass.L.PortalLibrary.list)
        {
            if (portal_asset.Buildings.Contains(building.asset))
            {
                Portal portal = building.GetBuildingComponent<Portal>();
                if (portal == null)
                {
                    portal = building.addComponent<Portal>();
                    portal.Asset = portal_asset;
                }
                portal_asset.RequestRebuildGraph?.Invoke(portal);
                break;
            }
        }
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.startRemove))]
    private static void startRemove_prefix(Building __instance)
    {
        if (__instance.isOnRemove()) return;
        __instance.asset.GetExtend<BuildingAssetExtend>().action_on_removed
            ?.Invoke(__instance, __instance.current_tile);
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.makeRuins))]
    private static void makeRuins_prefix(Building __instance)
    {
        __instance.asset.GetExtend<BuildingAssetExtend>().action_on_ruins?.Invoke(__instance, __instance.current_tile);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.startRemove))]
    private static void startRemove_postfix(Building __instance)
    {
        ClearWallsIfBonfire(__instance);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Building), nameof(Building.makeRuins))]
    private static void makeRuins_postfix(Building __instance)
    {
        ClearWallsIfBonfire(__instance);
    }

    /// <summary>篝火（城市核心）被摧毁/变废墟时，立即清除该城市的全部城墙（不等下次谋划）。</summary>
    private static void ClearWallsIfBonfire(Building b)
    {
        if (b?.asset == null || b.city == null) return;
        if (b.asset.type == "type_bonfire") Plots.ClearCityWalls(b.city);
    }
}
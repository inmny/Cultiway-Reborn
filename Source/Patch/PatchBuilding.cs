using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Core;
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
}
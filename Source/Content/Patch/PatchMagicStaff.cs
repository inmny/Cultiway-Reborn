using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Cultiway.Content.Patch;

/// <summary>
/// 将法杖要求和魔法师武器偏好接入原版法术、领用装备、战利品换装及武器打造流程。
/// </summary>
internal static class PatchMagicStaff
{

    [HarmonyPrefix, HarmonyPatch(typeof(ItemCrafting), nameof(ItemCrafting.craftItem))]
    private static bool CraftItemPrefix(Actor pActor, string pCreatorName, EquipmentType pType, int pTries,
        City pCity, ref bool __result)
    {
        if (pType != EquipmentType.Weapon ||
            !MagicStaffTools.TryCraftPreferredStaff(pActor, pCreatorName, pTries, pCity, out var crafted))
            return true;

        __result = crafted;
        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(City), nameof(City.giveItem))]
    private static IEnumerable<CodeInstruction> GiveItemTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceItemValueReads(instructions, 2, nameof(City.giveItem));
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.takeItems))]
    private static IEnumerable<CodeInstruction> TakeItemsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceItemValueReads(instructions, 2, nameof(Actor.takeItems));
    }

    /// <summary>
    /// 将原版 Item.getValue() 调用替换为带当前角色武器偏好的价值计算。
    /// 两个目标方法的第一个参数均为获得装备的 Actor：实例方法使用 this，静态方法使用 pActor。
    /// </summary>
    private static IEnumerable<CodeInstruction> ReplaceItemValueReads(IEnumerable<CodeInstruction> instructions,
        int expectedCount, string patchedMethod)
    {
        var getValue = AccessTools.Method(typeof(Item), nameof(Item.getValue));
        var resolveValue = AccessTools.Method(typeof(MagicStaffTools),
            nameof(MagicStaffTools.ResolveEquipmentPreferenceValue));
        var replaced = 0;

        foreach (var instruction in instructions)
        {
            if (!instruction.Calls(getValue))
            {
                yield return instruction;
                continue;
            }

            // 原调用栈已有 Item；再压入 Actor 后改调静态方法 (Item, Actor)。
            var loadActor = new CodeInstruction(OpCodes.Ldarg_0);
            loadActor.labels.AddRange(instruction.labels);
            loadActor.blocks.AddRange(instruction.blocks);
            instruction.labels.Clear();
            instruction.blocks.Clear();
            yield return loadActor;
            yield return new CodeInstruction(OpCodes.Call, resolveValue);
            replaced++;
        }

        if (replaced != expectedCount)
            throw new InvalidOperationException(
                $"{patchedMethod} 中预期替换 {expectedCount} 个 Item.getValue 调用，实际为 {replaced} 个");
    }
}

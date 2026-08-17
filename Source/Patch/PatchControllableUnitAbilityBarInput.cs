using Cultiway.UI;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

/// <summary>让能力栏、任务面板和选点模式统一拥有附体角色输入。</summary>
internal static class PatchControllableUnitAbilityBarInput
{
    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackPressedLeft))]
    private static void isAttackPressedLeft_postfix(ref bool __result) => Suppress(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackPressedRight))]
    private static void isAttackPressedRight_postfix(ref bool __result) => Suppress(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackJustPressedLeft))]
    private static void isAttackJustPressedLeft_postfix(ref bool __result) => Suppress(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackJustPressedRight))]
    private static void isAttackJustPressedRight_postfix(ref bool __result) => Suppress(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isMovementActionActive))]
    private static void isMovementActionActive_postfix(ref bool __result) => SuppressModal(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.getMovementVector))]
    private static void getMovementVector_postfix(ref Vector2 __result)
    {
        if (ControlledPossessionInputGate.BlocksPossessionActions) __result = Vector2.zero;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isActionPressedJump))]
    private static void isActionPressedJump_postfix(ref bool __result) => SuppressModal(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isActionPressedTalk))]
    private static void isActionPressedTalk_postfix(ref bool __result) => SuppressModal(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isActionPressedDash))]
    private static void isActionPressedDash_postfix(ref bool __result) => SuppressModal(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isActionPressedBackstep))]
    private static void isActionPressedBackstep_postfix(ref bool __result) => SuppressModal(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isActionPressedSteal))]
    private static void isActionPressedSteal_postfix(ref bool __result) => SuppressModal(ref __result);

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isActionPressedSwear))]
    private static void isActionPressedSwear_postfix(ref bool __result) => SuppressModal(ref __result);

    private static void Suppress(ref bool result)
    {
        if (result && ControlledPossessionInputGate.ShouldSuppressPossessionAction()) result = false;
    }

    private static void SuppressModal(ref bool result)
    {
        if (result && ControlledPossessionInputGate.BlocksPossessionActions) result = false;
    }
}

using Cultiway.UI;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>避免点击附体能力带时同时触发原版普通攻击或踢击。</summary>
internal static class PatchControllableUnitAbilityBarInput
{
    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackPressedLeft))]
    private static void isAttackPressedLeft_postfix(ref bool __result)
    {
        SuppressOverAbilityBar(ref __result);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackPressedRight))]
    private static void isAttackPressedRight_postfix(ref bool __result)
    {
        SuppressOverAbilityBar(ref __result);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackJustPressedLeft))]
    private static void isAttackJustPressedLeft_postfix(ref bool __result)
    {
        SuppressOverAbilityBar(ref __result);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ControllableUnit), nameof(ControllableUnit.isAttackJustPressedRight))]
    private static void isAttackJustPressedRight_postfix(ref bool __result)
    {
        SuppressOverAbilityBar(ref __result);
    }

    private static void SuppressOverAbilityBar(ref bool result)
    {
        if (result && ControlledActiveAbilityBar.ConsumesPointerInput()) result = false;
    }
}

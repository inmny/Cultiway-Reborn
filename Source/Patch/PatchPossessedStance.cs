using Cultiway.Core.Skills;
using HarmonyLib;

namespace Cultiway.Patch;

/// <summary>原版附体处理每个人物的移动和跳闪前，确认其站姿是否已由共同动作接管。</summary>
internal static class PatchPossessedStance
{
    [HarmonyPrefix, HarmonyPatch(typeof(StatusLibrary), "checkPossessedMovement")]
    private static bool BeforeMovement(Actor __0)
    {
        if (CanUseNativeMovement(__0)) return true;
        __0.setPossessedMovement(false);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(StatusLibrary), "checkPossessedFlip")]
    private static bool BeforeFacing(Actor __0) => CanUseNativeMovement(__0);

    [HarmonyPrefix, HarmonyPatch(typeof(StatusLibrary), "checkJump")]
    private static bool BeforeJump(Actor __0) => CanUseNativeMovement(__0);

    [HarmonyPrefix, HarmonyPatch(typeof(StatusLibrary), "checkDash")]
    private static bool BeforeDash(Actor __0) => CanUseNativeMovement(__0);

    [HarmonyPrefix, HarmonyPatch(typeof(StatusLibrary), "checkBackstep")]
    private static bool BeforeBackstep(Actor __0) => CanUseNativeMovement(__0);

    /// <summary>只认这个人物实际占用的站姿，独立手臂和其他附体人物仍各自接收输入。</summary>
    private static bool CanUseNativeMovement(Actor actor) =>
        !BodyControlState.HasControl(actor.id, BodyOccupancy.Stance);
}

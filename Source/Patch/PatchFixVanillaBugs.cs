using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchFixVanillaBugs
{
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.updateRotations))]
    private static IEnumerable<CodeInstruction> Actor_updateRotations(IEnumerable<CodeInstruction> codes)
    {
        foreach (var code in codes)
        {
            if (code.opcode == OpCodes.Call &&
                ((code.operand as MethodInfo)?.Name.Contains(nameof(Actor.is_unconscious)) ?? false))
            {
                code.operand = AccessTools.Method(typeof(Actor), nameof(Actor.isLying));
            }
            yield return code;
        }
    }
}
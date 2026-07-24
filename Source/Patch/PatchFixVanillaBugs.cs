using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchFixVanillaBugs
{
    [HarmonyPrefix, HarmonyPatch(typeof(EffectsCamera), "LateUpdate")]
    private static bool EffectsCamera_LateUpdate()
    {
        // 原版会把屏幕尺寸除以 3 后直接创建 RenderTexture；
        // 加载早期尺寸可能暂时为 0，此时等待下一帧尺寸就绪即可。
        return Screen.width >= 3 && Screen.height >= 3;
    }

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

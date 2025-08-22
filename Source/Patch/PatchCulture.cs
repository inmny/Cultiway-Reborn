using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchCulture
{
    /// <summary>
    /// 修复老马的傻逼代码
    /// </summary>
    [HarmonyTranspiler, HarmonyPatch(typeof(Culture), nameof(Culture.getOnomasticData))]
    private static IEnumerable<CodeInstruction> getOnomasticData_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindIndex(x =>
            x.opcode == OpCodes.Callvirt && (x.operand as MethodInfo)?.Name == nameof(OnomasticsData.setDebugTest));
        list[insert_idx - 1].opcode = OpCodes.Ldnull;
        list[insert_idx].opcode = OpCodes.Ret;

        return list;
    }
}
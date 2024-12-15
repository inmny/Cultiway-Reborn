using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace Cultiway.Utils;

public static class HarmonyUtils
{
    public static void LogCodes(List<CodeInstruction> codes)
    {
        StringBuilder sb = new();
        foreach (CodeInstruction code in codes) sb.AppendLine(code.ToString());

        ModClass.LogInfo(sb.ToString());
    }
}
using System.Text.RegularExpressions;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace Cultiway.Patch;

internal static class PatchWorldLogMessage
{
    [HarmonyPostfix, HarmonyPatch(typeof(WorldLogMessageExtensions), nameof(WorldLogMessageExtensions.getFormatedText))]
    private static void getFormatedText_postfix(ref string __result, ref WorldLogMessage pMessage, Text pTextField,
                                                bool       pColorField,
                                                bool       pColorTags)
    {
        var key = pMessage.text;
        string text = LM.Get(key);
        Color color = Toolbox.color_log_neutral;
        switch (key)
        {
            case "":
                break;
            default:
                if (Regex.IsMatch(key, @"cultisys_.*_\d*_msg"))
                {
                    text = text.Replace("$actor$", pMessage.special1);
                    pMessage.icon = "iconCrown";
                }
                else
                {
                    return;
                }

                break;
        }

        if (pColorField)
        {
            pTextField.color = color;
        }

        __result = text;
    }
}
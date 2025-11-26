using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using strings;

namespace Cultiway.Content.Patch
{
    internal static class PatchAboutBookTooltip
    {
        [HarmonyTranspiler, HarmonyPatch(typeof(CultureBookButton), nameof(CultureBookButton.showTooltip))]
        private static IEnumerable<CodeInstruction> showTooltip_transpiler(IEnumerable<CodeInstruction> codes)
        {
            var list = codes.ToList();
            var index = list.FindIndex(x => x.opcode == OpCodes.Ldstr);
            list[index] = new (OpCodes.Ldarg_0);
            list.Insert(index+1, new (OpCodes.Call, AccessTools.Method(typeof(PatchAboutBookTooltip), nameof(GetTooltipID))));
            return list;
        } 
        private static string GetTooltipID(CultureBookButton button) 
        {
            if (button._book.getAsset() == BookTypes.Cultibook)
            {
                return Tooltips.Cultibook.id;
            }
            else
            {
                return S_Tooltip.book;
            }
        }
    }
}
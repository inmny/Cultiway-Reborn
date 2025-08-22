using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchNameSetAsset
{
    [HarmonyPrefix, HarmonyPatch(typeof(NameSetAsset), nameof(NameSetAsset.get))]
    private static bool get_prefix(NameSetAsset __instance, MetaType pType, ref string __result)
    {
        var extend_type = pType.Extend();
        switch (extend_type)
        {
            case MetaTypeExtend.Sect:
                __result = __instance.GetExtend<NameSetAssetExtend>().Sect;
                return false;
        }

        return true;
    }

    private static MetaTypeExtend[] AdditionNameSetTypes =
    [
        MetaTypeExtend.Sect
    ];

    public static void SpecialPatch()
    {
        new Harmony("inmny.cultiway.namesetasset").Patch(AccessTools.Method(typeof(NameSetAsset).GetNestedType(
                "<getTypes>d__9",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            "MoveNext"),
            transpiler: new HarmonyMethod(typeof(PatchNameSetAsset), nameof(getTypes_transpiler)));
    }
    private static IEnumerable<CodeInstruction> getTypes_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        var switch_code = list.Find(x => x.opcode == OpCodes.Switch);

        var labels = ((Label[])switch_code.operand).ToList();
        var state_fld =
            AccessTools.Field(
                typeof(NameSetAsset).GetNestedType("<getTypes>d__9",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                "<>1__state");
        var current_fld  =
            AccessTools.Field(
                typeof(NameSetAsset).GetNestedType("<getTypes>d__9",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                "<>2__current");

        foreach (var type in AdditionNameSetTypes)
        {
            var insert_idx = list.Count - 5;
            var last_state_idx = list.Count - 9;
            int last_state = 0;
            if (list[last_state_idx].opcode == OpCodes.Ldc_I4_8)
            {
                last_state = 8;
            }
            else if (list[last_state_idx].opcode == OpCodes.Ldc_I4)
            {
                last_state = list[last_state_idx].operand as int? ?? 0;
            }
            
            list.InsertRange(insert_idx, [
                new (OpCodes.Ldarg_0),
                new (OpCodes.Ldc_I4_M1),
                new (OpCodes.Stfld, state_fld),
                new (OpCodes.Ldarg_0),
                new (OpCodes.Ldc_I4, (int)type),
                new (OpCodes.Stfld, current_fld),
                new (OpCodes.Ldarg_0),
                new (OpCodes.Ldc_I4, last_state + 1),
                new (OpCodes.Stfld, state_fld),
                new (OpCodes.Ldc_I4_1),
                new (OpCodes.Ret)
            ]);
            var label = new Label();
            list[insert_idx].labels.Add(label);
            labels.Add(label);
        }

        switch_code.operand = labels.ToArray();
        
        return list;
    }
}
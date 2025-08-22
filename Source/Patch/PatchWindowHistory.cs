using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchWindowHistory
{
    [HarmonyTranspiler, HarmonyPatch(typeof(WindowHistory), nameof(WindowHistory.addIntoHistory))]
    private static IEnumerable<CodeInstruction> addIntoHistory_transpiler(IEnumerable<CodeInstruction> codes, ILGenerator il)
    {
        var list = codes.ToList();
        var extend_history = il.DeclareLocal(typeof(WindowHistoryExtend));
        list.InsertRange(0, [
            new (OpCodes.Newobj, typeof(WindowHistoryExtend).GetConstructor(new Type[0])),
            new (OpCodes.Stloc, extend_history.LocalIndex)
        ]);
        
        var insert_idx = list.FindIndex(x => x.opcode == OpCodes.Call && (x.operand as MethodInfo)?.Name == "get_Current") + 1;
        list.InsertRange(insert_idx, [
            new (OpCodes.Dup),
            new(OpCodes.Ldloc, extend_history.LocalIndex),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchWindowHistory), nameof(ActionInAdding)))
        ]);

        insert_idx = list.FindIndex(x => x.opcode == OpCodes.Ret);
        list.InsertRange(insert_idx, [
            new(OpCodes.Ldloc, extend_history.LocalIndex),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchWindowHistory), nameof(AddHistoryExtend)))
        ]);
        return list;
    }

    private static void ActionInAdding(MetaTypeAsset asset, WindowHistoryExtend extend_history)
    {
        asset.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionUpdate?.Invoke(extend_history);
    }

    private static void ActionInReturn(MetaTypeAsset asset, WindowHistoryExtend extend_history)
    {
        asset.GetExtend<MetaTypeAssetExtend>().ExtendWindowHistoryActionRestore?.Invoke(extend_history);
    }

    private static readonly List<WindowHistoryExtend> _list = new();
    private static void AddHistoryExtend(WindowHistoryExtend extend_history)
    {
        _list.Add(extend_history);
    }
    private static WindowHistoryExtend PopHistoryExtend()
    {
        return _list.Pop();
    }
    [HarmonyTranspiler, HarmonyPatch(typeof(WindowHistory), nameof(WindowHistory.returnWindowBack))]
    private static IEnumerable<CodeInstruction> returnWindowBack_transpiler(IEnumerable<CodeInstruction> codes, ILGenerator il)
    {
        var list = codes.ToList();
        var extend_history = il.DeclareLocal(typeof(WindowHistoryExtend));
        var current_idx = 0;
        while (current_idx < list.Count)
        {
            var store_history_idx = list.FindIndex(current_idx, x => x.opcode == OpCodes.Stloc_0);
            if (store_history_idx != -1)
            {
                list.InsertRange(store_history_idx + 1, [
                    new(OpCodes.Call, AccessTools.Method(typeof(PatchWindowHistory), nameof(PopHistoryExtend))),
                    new(OpCodes.Stloc, extend_history.LocalIndex)
                ]);
            }
            else
            {
                break;
            }

            current_idx = store_history_idx + 1;
        }

        var insert_idx = list.FindIndex(x => x.opcode == OpCodes.Call && (x.operand as MethodInfo)?.Name == "get_Current")+1;
        list.InsertRange(insert_idx, [
            new (OpCodes.Dup),
            new (OpCodes.Ldloc, extend_history.LocalIndex),
            new (OpCodes.Call, AccessTools.Method(typeof(PatchWindowHistory), nameof(ActionInReturn)))
        ]);

        return list;
    }
}
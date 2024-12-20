using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Core;
using Cultiway.Utils;
using HarmonyLib;

namespace Cultiway.Patch;

internal static class PatchMapBox
{
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(MapBox), nameof(MapBox.applyAttack))]
    private static IEnumerable<CodeInstruction> applyAttack_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = new List<CodeInstruction>(codes);

        var add_exp_idx = list.FindIndex(x =>
            x.opcode == OpCodes.Callvirt && (x.operand as MemberInfo)?.Name == nameof(Actor.addExperience));

        CodeInstruction start_call_gethit = list[add_exp_idx + 1];

        list[add_exp_idx + 2].opcode = OpCodes.Ldloc_2;
        list[add_exp_idx + 3].opcode = OpCodes.Nop;

        list.InsertRange(add_exp_idx + 1, new CodeInstruction[]
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(AttackData), nameof(AttackData.initiator))),
            new(OpCodes.Ldloc_1),
            new(OpCodes.Conv_R4),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(AttackData), nameof(AttackData.attack_type))),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchMapBox), nameof(recalc_damage))),
            new(OpCodes.Stloc_2)
        });

        start_call_gethit.MoveLabelsTo(list[add_exp_idx + 1]);

        HarmonyUtils.LogCodes(list);

        return list;
    }

    private static float recalc_damage(BaseSimObject attacker, float damage, AttackType attack_type)
    {
        if (!attacker.isActor()) return damage;

        ElementComposition damage_composition = EnumUtils.DamageCompositionFromDamageType(attack_type);
        for (var i = 0; i < 8; i++)
            damage += (0.125f + damage_composition[i]) * attacker.stats[WorldboxGame.BaseStats.MasterStats[i]];

        return damage;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.finishMakingWorld))]
    private static void finishMakingWorld_postfix()
    {
        ModClass.I.TileExtendManager.FitNewWorld();
    }
}
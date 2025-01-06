using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using HarmonyLib;
using UnityEngine;

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

        var ldsfld_knockback_reduction_idx = list.FindIndex(x =>
            x.opcode == OpCodes.Ldsfld && (x.operand as FieldInfo)?.Name == nameof(S.knockback_reduction));
        var start_calc_knockback_idx = ldsfld_knockback_reduction_idx - 4;
        CodeInstruction start_calc_knockback = list[start_calc_knockback_idx];

        list.RemoveRange(start_calc_knockback_idx,
            list.FindIndex(start_calc_knockback_idx, x => x.opcode == OpCodes.Stloc_2)-start_calc_knockback_idx+1);
        list.InsertRange(start_calc_knockback_idx, new CodeInstruction[]
        {
            new(OpCodes.Ldloc_2),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(AttackData), nameof(AttackData.initiator))),
            new(OpCodes.Ldarg_1),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchMapBox), nameof(calc_knockback_reduction))),
            new(OpCodes.Stloc_2)
        });
        start_calc_knockback.MoveLabelsTo(list[start_calc_knockback_idx]);
        

        return list;
    }

    private static float calc_knockback_reduction(float knockback, BaseSimObject attacker, BaseSimObject target)
    {
        var reduction = target.stats[S.knockback_reduction];
        
        var attacker_power_level = attacker.isActor() ? attacker.a.GetExtend().GetPowerLevel() : 0;
        var power_level = target.isActor() ? target.a.GetExtend().GetPowerLevel() : 0;
        if (power_level > attacker_power_level)
        {
            reduction += (power_level - attacker_power_level) * 1000f;
        }
        if (reduction >= 0)
        {
            knockback *= (1 / (1 + reduction));
        }
        else
        {
            knockback *= (1 - reduction);
        }

        if (knockback < 0.01f)
        {
            return 0;
        }

        return knockback;
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
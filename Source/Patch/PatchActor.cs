using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Cultiway.Patch;

internal static class PatchActor
{
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.tryToAttack))]
    private static bool tryToAttack_prefix(Actor __instance, BaseSimObject pTarget, ref bool __result)
    {
        __result = __instance.GetExtend().TryToAttack(pTarget);
        return false;
    }
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot), HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    public static void getHit_snapshot(Actor      __instance,                      float pDamage, bool pFlash = true,
                                       AttackType pAttackType  = AttackType.Other, BaseSimObject pAttacker = null,
                                       bool       pSkipIfShake = true,             bool pMetallicWeapon = false)
    {
        throw new NotImplementedException();
    }
    [Hotfixable]
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    public static IEnumerable<CodeInstruction> getHit_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        // 取消受击僵直
        for (var i = 0; i < list.Count - 1; i++)
        {
            CodeInstruction ldc = list[i];
            CodeInstruction stfld = list[i + 1];
            if (ldc.opcode                          == OpCodes.Ldc_R4 && stfld.opcode == OpCodes.Stfld &&
                (stfld.operand as MemberInfo)?.Name == nameof(ActorBase.timer_action))
            {
                ldc.operand = 0.0f;
                break;
            }
        }

        return list;
    }
    [Hotfixable]
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    private static bool getHit_prefix(Actor      __instance,                      float pDamage, bool pFlash = true,
                                      AttackType pAttackType  = AttackType.Other, BaseSimObject pAttacker = null,
                                      bool       pSkipIfShake = true)
    {
        if (pSkipIfShake && __instance.shake_active)
        {
            return true;
        }
        __instance.GetExtend().GetHit(pDamage, ref EnumUtils.DamageCompositionFromDamageType(pAttackType), pAttacker);
        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.newCreature))]
    private static IEnumerable<CodeInstruction> newCreature_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = new List<CodeInstruction>(codes);

        var idx = list.FindIndex(x => x.opcode                        == OpCodes.Call &&
                                      (x.operand as MethodBase)?.Name == nameof(ActorBase.generatePersonality));
        list.InsertRange(idx + 1, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_new_creature)))
        ]);
        return list;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(ActorManager), nameof(ActorManager.spawnPopPoint))]
    private static IEnumerable<CodeInstruction> spawnPopPoint_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var idx = list.FindIndex(x =>
            x.opcode == OpCodes.Call && (x.operand as MethodBase)?.Name == nameof(ActorManager.finalizeActor)) + 1;
        list.InsertRange(idx, [
            new(OpCodes.Ldloc_2),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_new_creature)))
        ]);

        return list;
    }

    private static void _extend_new_creature(Actor actor)
    {
        actor.GetExtend().ExtendNewCreature();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.updateStats))]
    private static IEnumerable<CodeInstruction> updateStats_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = new List<CodeInstruction>(codes);

        var idx = list.FindIndex(x => x.opcode                       == OpCodes.Stfld &&
                                      (x.operand as FieldInfo)?.Name == nameof(ActorBase.has_status_frozen));
        list.InsertRange(idx + 1, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_update_stats)))
        ]);
        return list;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.updateStats))]
    private static void updateStats_postfix(ActorBase __instance)
    {
        __instance.a.GetExtend().PostUpdateStats();
    }

    private static void _extend_update_stats(ActorBase actor)
    {
        ((Actor)actor).GetExtend().ExtendUpdateStats();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ActorManager), nameof(ActorManager.destroyObject))]
    private static void destroy_postfix(Actor pActor)
    {
        ModClass.I.ActorExtendManager.Destroy(pActor.data.id);
    }
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.newKillAction))]
    private static void newKillAction_postfix(Actor __instance, Actor pDeadUnit, Kingdom pPrevKingdom)
    {
        __instance.GetExtend().NewKillAction(pDeadUnit, pPrevKingdom);
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.killHimself))]
    private static void killHimself_prefix(Actor __instance)
    {
        if (__instance.isAlive())
        {
            __instance.GetExtend().OnDeath();
        }
    }
}
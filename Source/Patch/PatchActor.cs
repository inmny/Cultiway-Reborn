using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Patch;

internal static class PatchActor
{
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.addChildren))]
    private static void addChildren_postfix(Actor __instance)
    {
        __instance.GetExtend().OnAddChildren();
    }
    /// <summary>
    /// 实现<see cref="ActorAssetExtend.hide_hand_item"/>
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.checkHasRenderedItem))]
    private static void getHandRendererAsset_postfix(Actor __instance, ref bool __result)
    {
        if (__result == false) return;
        if (!__instance.asset.GetExtend<ActorAssetExtend>().hide_hand_item) return;
        __result = false;
    }
    /// <summary>
    /// 实现<see cref="ActorAssetExtend.sleep_standing_up"/>
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.isLying))]
    private static void isLying_postfix(Actor __instance, ref bool __result)
    {
        if (!__result) return;
        if (!__instance._has_status_sleeping) return;
        __result = !__instance.getActorAsset().GetExtend<ActorAssetExtend>().sleep_standing_up;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.addStatusEffect))]
    private static bool addStatusEffect_prefix(Actor __instance, StatusAsset pStatusAsset, ref float pOverrideTimer,
        ref bool __result)
    {
        if (pStatusAsset.affects_mind && __instance.hasTag("strong_mind"))
        {
            __result = false;
            return false;
        }

        if (!pStatusAsset.GetExtend<StatusAssetExtend>().negative)
        {
            return true;
        }
        var ae = __instance.GetExtend();

        var level = ae.GetPowerLevel();
        if (level == 0f) return true;
        var time = Mathf.Log(pStatusAsset.duration, Mathf.Pow(DamageCalcHyperParameters.PowerBase, level));

        if (time < 0.02f)
        {
            __result = false;
            return false;
        }

        pOverrideTimer = time;

        return true;
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.tryToAttack))]
    private static bool tryToAttack_prefix(Actor __instance, BaseSimObject pTarget, bool pDoChecks, Action pKillAction, float pBonusAreOfEffect, ref bool __result)
    {
        if (pTarget.isRekt()) return true;
        __result = __instance.GetExtend().TryToAttack(pTarget, pKillAction, pBonusAreOfEffect, pDoChecks);
        return false;
    }
    
    [HarmonyReversePatch(HarmonyReversePatchType.Snapshot), HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    public static void getHit_snapshot(Actor      __instance,                      float pDamage, bool pFlash = true,
                                       AttackType pAttackType  = AttackType.Other, BaseSimObject pAttacker = null,
                                       bool       pSkipIfShake = true,             bool pMetallicWeapon = false, bool pCheckDamageReduction = true)
    {
        throw new NotImplementedException();
    }
    [Hotfixable]
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.getHit))]
    public static IEnumerable<CodeInstruction> getHit_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();
        for (var i = 0; i < list.Count - 1; i++)
        {
            CodeInstruction ldc = list[i];
            CodeInstruction stfld = list[i + 1];
            if (ldc.opcode                          == OpCodes.Ldc_R4 && stfld.opcode == OpCodes.Stfld &&
                (stfld.operand as MemberInfo)?.Name == nameof(Actor.timer_action))
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
                                      bool       pSkipIfShake = true, bool pCheckDamageReduction = false)
    {
        if (__instance == pAttacker) return false;
        if (pSkipIfShake && __instance._shake_active)
        {
            return true;
        }
        __instance.GetExtend().GetHit(pDamage, ref EnumUtils.DamageCompositionFromDamageType(pAttackType), pAttacker, ignore_damage_reduction: !pCheckDamageReduction);
        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.newCreature))]
    private static IEnumerable<CodeInstruction> newCreature_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = new List<CodeInstruction>(codes);

        var idx = list.FindIndex(x => x.opcode                        == OpCodes.Call &&
                                      (x.operand as MethodBase)?.Name == nameof(Actor.generatePersonality));
        list.InsertRange(idx + 1, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_new_creature)))
        ]);
        return list;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(ActorManager), nameof(ActorManager.createBabyActorFromData))]
    private static IEnumerable<CodeInstruction> spawnPopPoint_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var idx = list.FindIndex(x =>
            x.opcode == OpCodes.Call && (x.operand as MethodBase)?.Name == nameof(ActorManager.finalizeActor)) + 1;
        list.InsertRange(idx, [
            new(OpCodes.Ldloc_1),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_new_creature)))
        ]);

        return list;
    }

    private static void _extend_new_creature(Actor actor)
    {
        actor.GetExtend().ExtendNewCreature();
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.updateStats))]
    private static IEnumerable<CodeInstruction> updateStats_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = new List<CodeInstruction>(codes);

        var idx_normal_update = list.FindIndex(x => x.opcode == OpCodes.Callvirt &&
                                                    (x.operand as MethodInfo)?.Name == nameof(BaseStats.normalize)) - 2;
        var old_inst = list[idx_normal_update];
        list.InsertRange(idx_normal_update, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_extend_update_stats)))
        ]);
        var new_inst = list[idx_normal_update];
        old_inst.MoveLabelsTo(new_inst);
        
        var idx_post_update = list.FindIndex(idx_normal_update+5, x => x.opcode == OpCodes.Callvirt &&
                                                    (x.operand as MethodInfo)?.Name == nameof(BaseStats.normalize)) - 2;
        old_inst = list[idx_post_update];
        list.InsertRange(idx_post_update, [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(_post_update_stats)))
        ]);
        new_inst = list[idx_post_update];
        old_inst.MoveLabelsTo(new_inst);

        return list;
    }
    private static void _post_update_stats(Actor actor)
    {
        actor.GetExtend().PostUpdateStats();
    }

    private static void _extend_update_stats(Actor actor)
    {
        actor.GetExtend().ExtendUpdateStats();
    }
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.clearManagers))]
    private static void clearManagers_postfix(Actor __instance)
    {
        var ae = __instance.GetExtend();
        if (__instance.HasSect())
        {
            WorldboxGame.I.Sects.unitDied(ae.sect);
            ae.sect = null;
        }
    }
    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.newKillAction))]
    private static void newKillAction_postfix(Actor __instance, Actor pDeadUnit, Kingdom pPrevKingdom)
    {
        __instance.GetExtend().NewKillAction(pDeadUnit, pPrevKingdom);
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.die))]
    private static void killHimself_prefix(Actor __instance, bool pDestroy)
    {
        if (__instance.isAlive() || pDestroy)
        {
            var ae = __instance.GetExtend();
            ae.OnDeath();
        }
    }
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.Dispose))]
    private static void Dispose_prefix(Actor __instance)
    {
        var ae = __instance.GetExtend();
        PathFinder.Instance.Cleanup(__instance.data.id);
        ae.Dispose();
    }
}
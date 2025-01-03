using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Patch;

internal static class PatchActor
{
    [HarmonyPostfix, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.nextJobActor))]
    private static void nextJobActor_postfix(ref string __result, ActorBase pActor)
    {
        if (pActor.asset.unit && pActor.city != null && !pActor.isProfession(UnitProfession.Warrior))
        {
            var chance = 0.2f;
            if (pActor.hasTrait(ActorTraits.Cultivator.id)) chance = 0.8f;

            var ae = (pActor as Actor).GetExtend();

            if (Toolbox.randomChance(1 - chance))
            {
                if (ae.HasComponent<Jindan>() && Toolbox.randomChance(0.4f)) __result = ActorJobs.ElixirCrafter.id;

                return;
            }

            if (ae.HasCultisys<Xian>())
            {
                __result = ActorJobs.XianCultivator.id;
            }
        }
    }

    [Hotfixable]
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Actor), nameof(Actor.killHimself))]
    private static void killHimself_postfix(Actor __instance)
    {
        if (!__instance.isAlive()) return;
        ActorExtend dead_ae = __instance.GetExtend();
        if (!dead_ae.HasCultisys<Xian>()) return;
        BaseSimObject receiver = __instance.attackedBy ?? __instance;
        if (receiver.city == null) return;
        CityExtend ce = receiver.city.GetExtend();

        SpecialItemUtils.Builder item_builder =
            SpecialItemUtils.StartBuild(ItemShapes.Ball.id, __instance.data.created_time, __instance.getName());
        if (dead_ae.HasComponent<Jindan>()) item_builder.AddComponent(dead_ae.GetComponent<Jindan>());
        if (dead_ae.HasComponent<XianBase>()) item_builder.AddComponent(dead_ae.GetComponent<XianBase>());
        if (dead_ae.HasComponent<ElementRoot>()) item_builder.AddComponent(dead_ae.GetComponent<ElementRoot>());

        ce.AddSpecialItem(item_builder.Build());
    }

    #region 飞行

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.checkSpriteToRender))]
    private static IEnumerable<CodeInstruction> checkSpriteToRender_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindIndex(x => x.opcode == OpCodes.Ldfld &&
                                             (x.operand as FieldInfo)?.Name ==
                                             nameof(ActorBase.is_moving)) - 1;
        var jmp_idx = insert_idx;
        var jmp_label = new Label();
        list[jmp_idx].labels.Add(jmp_label);

        var end_idx = list.FindIndex(x => x.opcode == OpCodes.Ldloc_S && (x.operand as LocalBuilder)?.LocalIndex == 4);
        var end_label = list[end_idx].labels.First();

        list.InsertRange(insert_idx, [
            // var tmp = check_is_flying(this);
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(check_is_flying))),
            // if (!tmp) goto label;
            new(OpCodes.Brfalse, jmp_label),
            // actorAnimation = this.animationContainer.idle;
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(ActorBase),              nameof(ActorBase.animationContainer))),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(AnimationContainerUnit), nameof(AnimationContainerUnit.idle))),
            new(OpCodes.Stloc_S, 4),
            new(OpCodes.Br, end_label)
        ]);

        //ModClass.LogInfo($"\n{list.Join(x => x.ToString(), "\n")}");
        return list;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.goTo))]
    private static bool goTo_prefix(ref ExecuteEvent __result, ActorBase __instance, WorldTile pTile)
    {
        if (Toolbox.DistTile(__instance.currentTile, pTile) < ContentSetting.MinFlyDist) return true;
        if (try_goTo_fast((Actor)__instance, pTile))
        {
            __result = ExecuteEvent.True;
            return false;
        }

        return true;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.goTo))]
    private static void goTo_postfix(ref ExecuteEvent __result, ActorBase __instance, WorldTile pTile)
    {
        if (__result == ExecuteEvent.True)
        {
            var len = 0f;
            var last_tile = __instance.currentTile;
            for (int i = __instance.current_path_index; i < __instance.current_path.Count; i++)
            {
                var tile = __instance.current_path[i];
                len += Toolbox.DistTile(last_tile, tile);
                last_tile = tile;
            }

            if (len < ContentSetting.MinFlyDist) return;
        }

        if (try_goTo_fast((Actor)__instance, pTile))
        {
            __result = ExecuteEvent.True;
        }
    }

    [Hotfixable]
    private static bool try_goTo_fast(Actor actor, WorldTile tile)
    {
        var ae = actor.GetExtend();
        if (!ae.HasCultisys<Xian>()) return false;
        ref var xian = ref ae.GetCultisys<Xian>();

        if (xian.CurrLevel >= XianSetting.TransportLevel)
        {
            actor.current_path.Clear();
            // 放一个特效
            actor.setCurrentTilePosition(tile);
            return true;
        }

        if (xian.CurrLevel >= XianSetting.FlyLevel)
        {
            actor.data.addFlag(ContentActorDataKeys.IsFlying_flag);
            actor.flying = true;

            return ActorMove.goTo(actor, tile, true, true) == ExecuteEvent.True;
        }

        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.b1_checkUnderForce))]
    private static IEnumerable<CodeInstruction> b1_checkUnderForce_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var idx = list.FindLastIndex(x => x.opcode == OpCodes.Brfalse);
        var ret_label = list[idx].operand;
        list.InsertRange(idx + 1, [
            new(OpCodes.Ldarg_0), new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(check_is_flying))),
            new(OpCodes.Brtrue, ret_label)
        ]);
        return list;
    }

    private static bool check_is_flying(Actor actor)
    {
        return actor.isAlive() && actor.data.hasFlag(ContentActorDataKeys.IsFlying_flag);
    }

    [Hotfixable]
    [HarmonyPrefix, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.updateFall))]
    private static bool updateFall_prefix(ActorBase __instance, float pElapsed)
    {
        var actor = (Actor)__instance;
        if (!check_is_flying(actor)) return true;

        var delta = ContentSetting.FlyHeight - actor.zPosition.y;
        actor.zPosition.y += Mathf.Min(Mathf.Abs(delta), pElapsed * 20f) * Mathf.Sign(delta);
        actor.setPosDirty();

        return false;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.updateParallelChecks))]
    private static IEnumerable<CodeInstruction> updateParallelChecks_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindLastIndex(x =>
            x.opcode == OpCodes.Ldflda && (x.operand as FieldInfo)?.Name == nameof(Actor.zPosition)) - 1;
        var jmp_idx = list.FindLastIndex(x =>
            x.opcode == OpCodes.Callvirt && (x.operand as MethodInfo)?.Name == nameof(Actor.updateFall)) - 2;

        var jmp_label = new Label();
        list[jmp_idx].labels.Add(jmp_label);

        list.InsertRange(insert_idx, [
            new(OpCodes.Ldarg_0), new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(check_is_flying))),
            new(OpCodes.Brtrue, jmp_label)
        ]);
        return list;
    }

    [HarmonyTranspiler, HarmonyPatch(typeof(ActorBase), nameof(ActorBase.precalcMovementSpeed))]
    private static IEnumerable<CodeInstruction> precalcMovementSpeed_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindIndex(x => x.opcode == OpCodes.Stloc_0) + 1;
        list.InsertRange(insert_idx, [
            new(OpCodes.Ldloc_0),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchActor), nameof(get_fly_speed_mod))),
            new(OpCodes.Mul),
            new(OpCodes.Stloc_0)
        ]);
        return list;
    }

    [Hotfixable]
    private static float get_fly_speed_mod(ActorBase actor)
    {
        return actor.data.hasFlag(ContentActorDataKeys.IsFlying_flag) ? ContentSetting.FlySpeedMod : 1;
    }

    #endregion
}
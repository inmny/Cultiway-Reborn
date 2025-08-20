using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using ai.behaviours;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using HarmonyLib;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Patch;

internal static class PatchAboutFly
{
    [HarmonyPrefix, HarmonyPatch(typeof(BehFindRandomTile), nameof(BehFindRandomTile.execute))]
    private static bool BehFindRandomTile_execute_prefix(Actor pActor, ref BehResult __result)
    {
        var ae = pActor.GetExtend();
        if (!ae.TryGetComponent(out Xian xian) || xian.CurrLevel < XianSetting.WeaponFlyLevel)
        {
            return true;
        }

        WorldTile tile = null;
        var island_calculator = World.world.islands_calculator;
        if (island_calculator.islands.Any())
        {
            TileIsland ground_island = null;

            if (island_calculator.islands_ground.Count != 0)
            {
                int[] weights = new int[island_calculator.islands_ground.Count];
                var last_weight = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] = last_weight + island_calculator.islands_ground[i].getTileCount();
                    last_weight = weights[i];
                }

                ground_island = island_calculator.islands_ground[RdUtils.RandomIndexWithAccumWeight(weights)];
            }

            if (ground_island != null && ground_island.regions.Count > 0)
            {
                tile = ground_island.getRandomTile();
            }
        }

        if (tile == null)
        {
            tile = World.world.tiles_list.GetRandom();
        }
        pActor.beh_tile_target = tile;
        __result = BehResult.Continue;
        return false;
    }
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.calculateMainSprite))]
    private static IEnumerable<CodeInstruction> checkSpriteToRender_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindIndex(x => x.opcode == OpCodes.Call &&
                                             (x.operand as MethodInfo)?.Name == "get_is_moving") - 1;
        var jmp_idx = insert_idx;
        var jmp_label = new Label();
        list[jmp_idx].labels.Add(jmp_label);

        var end_idx = list.FindIndex(x => x.opcode == OpCodes.Ldloc_2);
        var end_label = list[end_idx].labels.First();

        list.InsertRange(insert_idx, [
            // var tmp = check_is_flying(this);
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchAboutFly), nameof(check_is_flying))),
            // if (!tmp) goto label;
            new(OpCodes.Brfalse, jmp_label),
            // actorAnimation = this.animationContainer.idle;
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(Actor),              nameof(Actor.animation_container))),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(AnimationContainerUnit), nameof(AnimationContainerUnit.idle))),
            new(OpCodes.Stloc_2),
            new(OpCodes.Br, end_label)
        ]);

        //ModClass.LogInfo($"\n{list.Join(x => x.ToString(), "\n")}");
        return list;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.goTo))]
    private static bool goTo_prefix(ref ExecuteEvent __result, Actor __instance, WorldTile pTile)
    {
        if (Toolbox.DistTile(__instance.current_tile, pTile) < ContentSetting.MinFlyDist) return true;
        if (try_goTo_fast((Actor)__instance, pTile))
        {
            __result = ExecuteEvent.True;
            return false;
        }

        return true;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.goTo))]
    private static void goTo_postfix(ref ExecuteEvent __result, Actor __instance, WorldTile pTile)
    {
        if (__result == ExecuteEvent.True)
        {
            var len = 0f;
            var last_tile = __instance.current_tile;
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

        if (xian.CurrLevel >= XianSetting.WeaponFlyLevel)
        {
            if (!actor.hasWeapon() && xian.CurrLevel < XianSetting.CloudFlyLevel) return false;
            actor.data.addFlag(ContentActorDataKeys.IsFlying_flag);
            actor.setFlying(true);
            actor.precalcMovementSpeed(true);

            actor.clearOldPath();
            actor.setTileTarget(tile);
            actor.current_path.Add(tile);
            return true;
            return ActorMove.goTo(actor, tile, true, true, true) == ExecuteEvent.True;
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
            new(OpCodes.Ldarg_0), new(OpCodes.Call, AccessTools.Method(typeof(PatchAboutFly), nameof(check_is_flying))),
            new(OpCodes.Brtrue, ret_label)
        ]);
        return list;
    }
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.updateParallelChecks))]
    private static IEnumerable<CodeInstruction> updateParallelChecks(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindIndex(x =>
            x.opcode == OpCodes.Ldfld && (x.operand as FieldInfo)?.Name == nameof(ActorAsset.update_z)) - 2;
        var jump_target_idx = list.FindIndex(x =>
            x.opcode == OpCodes.Callvirt && (x.operand as MethodInfo)?.Name == nameof(Actor.updateFall)) - 1;
        Label label;
        if (list[jump_target_idx].labels.Count > 0)
        {
            label = list[jump_target_idx].labels[0];
        }
        else
        {
            label = new Label();
            list[jump_target_idx].labels.Add(label);
        }
        list.InsertRange(insert_idx, new []
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchAboutFly), nameof(check_is_flying))),
            new CodeInstruction(OpCodes.Brtrue, label)
        });

        return list;
    }
    private static bool check_is_flying(Actor actor)
    {
        return actor.isAlive() && actor.data.hasFlag(ContentActorDataKeys.IsFlying_flag);
    }

    [Hotfixable]
    [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.updateFall))]
    private static bool updateFall_prefix(Actor __instance)
    {
        var actor = (Actor)__instance;
        if (!check_is_flying(actor)) return true;

        var delta = ContentSetting.FlyHeight - actor.position_height;
        actor.position_height += Mathf.Min(Mathf.Abs(delta), World.world.elapsed * 20f) * Mathf.Sign(delta);

        return false;
    }
/*
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.updateParallelChecks))]
    private static IEnumerable<CodeInstruction> updateParallelChecks_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindLastIndex(x =>
            x.opcode == OpCodes.Ldflda && (x.operand as FieldInfo)?.Name == nameof(Actor.position_height)) - 1;
        var jmp_idx = list.FindLastIndex(x =>
            x.opcode == OpCodes.Callvirt && (x.operand as MethodInfo)?.Name == nameof(Actor.updateFall)) - 2;

        var jmp_label = new Label();
        list[jmp_idx].labels.Add(jmp_label);

        list.InsertRange(insert_idx, [
            new(OpCodes.Ldarg_0), new(OpCodes.Call, AccessTools.Method(typeof(PatchAboutFly), nameof(check_is_flying))),
            new(OpCodes.Brtrue, jmp_label)
        ]);
        return list;
    }
*/
    [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.precalcMovementSpeed))]
    private static IEnumerable<CodeInstruction> precalcMovementSpeed_transpiler(IEnumerable<CodeInstruction> codes)
    {
        var list = codes.ToList();

        var insert_idx = list.FindIndex(x => x.opcode == OpCodes.Stloc_3) + 1;
        list.InsertRange(insert_idx, [
            new(OpCodes.Ldloc_3),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(PatchAboutFly), nameof(get_fly_speed_mod))),
            new(OpCodes.Mul),
            new(OpCodes.Stloc_3)
        ]);
        return list;
    }

    [Hotfixable]
    private static float get_fly_speed_mod(Actor actor)
    {
        return actor.data.hasFlag(ContentActorDataKeys.IsFlying_flag) ? ContentSetting.FlySpeedMod : 1;
    }
}
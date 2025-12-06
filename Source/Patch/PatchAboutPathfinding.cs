using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using ai.behaviours;
using Cultiway.Core.Pathfinding;
using HarmonyLib;
using life.taxi;

namespace Cultiway.Patch
{
    internal static class PatchAboutPathfinding
    {
        /**寻路调整方案
        * （1）完全替换原版寻路，调用goTo后会提交一个任务用以多线程寻路，goTo这边的结果始终为成功
        * （2）多线程寻路不要求最优，但省时、流式输出。
        * （3）当调用goTo之后会将生物设置为等待寻路结果的情况，啥事都不干（如果有突发事件则直接打断寻路任务）
        */
        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.goTo))]
        private static bool goTo_prefix(ref ExecuteEvent __result, Actor __instance, WorldTile pTile,
            bool pPathOnWater = false, bool pWalkOnBlocks = false, bool pWalkOnLava = false,
            int pLimitPathfindingRegions = 0)
        {
            //AbortPath(__instance);

            __instance.setTileTarget(pTile);
            __instance.next_step_position = __instance.current_tile?.posV3 ?? __instance.next_step_position;

            PathFinder.Instance.RequestPath(__instance, pTile, pPathOnWater, pWalkOnBlocks, pWalkOnLava,
                pLimitPathfindingRegions);

            __result = ExecuteEvent.True;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.updatePathMovement))]
        private static bool updatePathMovement(Actor __instance)
        {
            if (!PathFinder.Instance.TryPeekStep(__instance, out var step, out var finished))
            {
                __instance.setNotMoving();
                __instance.timer_action = 1f;
                return false;
            }

            var result = HandleStep(__instance, step);
            switch (result)
            {
                case PathProcessResult.Consumed:
                    PathFinder.Instance.ConsumeStep(__instance);
                    break;
                case PathProcessResult.Abort:
                    PathFinder.Instance.Cancel(__instance);
                    __instance.cancelAllBeh();
                    break;
                case PathProcessResult.Deferred:
                    __instance.timer_action = 1f;
                    __instance.setNotMoving();
                    break;
            }
            if (__instance.tile_target == null)
            {
                PathFinder.Instance.Cancel(__instance);
                __instance.cancelAllBeh();
            }
            if (finished)
            {
                __instance.setNotMoving();
            }

            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.isUsingPath))]
        private static void isUsingPath_postfix(Actor __instance, ref bool __result)
        {
            __result = __result || (PathFinder.Instance.IsActorPathing(__instance) && __instance.tile_target != null);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.clearWorld))]
        private static void clearWorld_prefix()
        {
            PathFinder.Instance.Clear();
            PortalRegistry.Instance.Clear();
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.Dispose))]
        private static void Dispose_prefix(Actor __instance)
        {
            if (__instance.data == null) return;
            lock (PathFinder.ActorSyncLock)
            {
                PathFinder.Instance.Cancel(__instance);
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Building), nameof(Building.setState))]
        private static void setState_prefix(Building __instance, BuildingState pState)
        {
            if (__instance.asset.docks)
            {
                if (pState == BuildingState.Normal)
                {
                    PortalRegistry.Instance.RegisterOrUpdate(new PortalDefinition(__instance.id, __instance.getConstructionTile(), 1, 1, new List<PortalConnection>()));
                    WaterConnectivityUpdater.RequestRebuild();
                }
                else if (pState == BuildingState.Ruins || pState == BuildingState.Removed)
                {
                    PortalRegistry.Instance.Remove(__instance.id);
                    WaterConnectivityUpdater.RequestRebuild();
                }
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(MapChunkManager), nameof(MapChunkManager.updateDirty))]
        private static void updateDirty_prefix(MapChunkManager __instance, ref bool __state)
        {
            __state = false;
            if (!DebugConfig.isOn(DebugOption.SystemUpdateDirtyChunks))
            {
                return;
            }
            if (!__instance.isAllChunksDirty() && World.world.isActionHappening())
            {
                return;
            }
            var dirtyLinks = __instance._dirty_chunks_links;
            var dirtyRegions = __instance._dirty_chunks_regions;
            if ((dirtyLinks == null || dirtyRegions == null) ||
                (dirtyLinks.Count == 0 && dirtyRegions.Count == 0))
            {
                return;
            }
            __state = true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(MapChunkManager), nameof(MapChunkManager.updateDirty))]
        private static void updateDirty_postfix(bool __state)
        {
            if (!__state) return;
            WaterConnectivityUpdater.RequestRebuild();
        }


        private static PathProcessResult HandleStep(Actor actor, PathStep step)
        {
            return step.Method switch
            {
                MovementMethod.Walk => HandleWalk(actor, step.Tile),
                MovementMethod.Swim => HandleSwim(actor, step.Tile),
                MovementMethod.Sail => HandleSail(actor, step.Tile),
                _ => PathProcessResult.Deferred
            };
        }

        private static PathProcessResult HandleWalk(Actor actor, WorldTile tile)
        {
            return TryMove(actor, tile, allowBlocks: false, allowLava: false, allowOcean: true);
        }

        private static PathProcessResult HandleSwim(Actor actor, WorldTile tile)
        {
            return TryMove(actor, tile, allowBlocks: false, allowLava: true, allowOcean: true);
        }
        [HarmonyTranspiler, HarmonyPatch(typeof(BehBoatTransportDoLoading), nameof(BehBoatTransportDoLoading.execute))]
        private static IEnumerable<CodeInstruction> BehBoatTransportDoLoading_execute_transpiler(IEnumerable<CodeInstruction> codes)
        {
            var list = codes.ToList();
            
            // 在list中找到这样一个下标i，第i个是ldc.i4.2，第i+1个是callvirt，方法名为setState
            for (int i = 0; i < list.Count - 1; i++)
            {
                var inst = list[i];
                var nextInst = list[i + 1];
                if (inst.opcode == OpCodes.Ldc_I4_2 &&
                    nextInst.opcode == OpCodes.Callvirt &&
                    (nextInst.operand as MethodInfo)?.Name == nameof(TaxiRequest.setState))
                {
                    list.InsertRange(i - 1, new []
                    {
                        new CodeInstruction(OpCodes.Ldarg_1),
                        new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatchAboutPathfinding), nameof(LoadCommonPassengers))),
                    });
                    break;
                }
            }
            return list;
        }
        private static void LoadCommonPassengers(Actor actor, TaxiRequest request)
        {
            var passengers = request.getActors();
            var boat = request.getBoat();
            using var to_add = new ListPool<Actor>(passengers.Count);
            foreach (var passenger in passengers)
            {
                if (passenger.is_inside_boat) continue;
                passenger.data.transportID = actor.data.id;
                passenger.is_inside_boat = true;
                passenger.inside_boat = boat;
                to_add.Add(passenger);
            }
            foreach (var passenger in to_add)
            {
                boat.addPassenger(passenger);
            }
        }
        private static PathProcessResult HandleSail(Actor actor, WorldTile tile)
        {
            var request = TaxiManager.getRequestForActor(actor);
            if (request == null)
            {
                TaxiManager.newRequest(actor, tile);
                return PathProcessResult.Deferred;
            }
            return PathProcessResult.Deferred;
        }

        private static PathProcessResult TryMove(Actor actor, WorldTile tile, bool allowBlocks, bool allowLava,
            bool allowOcean)
        {
            if (tile == null)
            {
                return PathProcessResult.Abort;
            }

            var tileType = tile.Type;
            if (!allowBlocks && tileType.block && !actor.ignoresBlocks())
            {
                return PathProcessResult.Abort;
            }

            if (!allowLava && actor.asset.die_in_lava && tileType.lava)
            {
                return PathProcessResult.Abort;
            }

            if (!allowOcean && tileType.ocean && actor.isDamagedByOcean())
            {
                return PathProcessResult.Abort;
            }

            if (tileType.damaged_when_walked)
            {
                actor.current_tile?.tryToBreak();
            }

            if (tile.isOnFire() && !actor.isImmuneToFire() && !(actor.current_tile?.isOnFire() ?? false))
            {
                return PathProcessResult.Abort;
            }

            actor.moveTo(tile);
            return PathProcessResult.Consumed;
        }

        private enum PathProcessResult
        {
            Consumed,
            Deferred,
            Abort
        }
    }
}

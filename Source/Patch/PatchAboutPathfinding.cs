using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ai;
using ai.behaviours;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Libraries;
using Cultiway.Core.Pathfinding;
using Cultiway.Utils.Extension;
using HarmonyLib;
using life.taxi;
using strings;
using tools;

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
                PathRecoveryManager.TryRequest(__instance);
                return false;
            }

            var result = HandleStep(__instance, step);
            switch (result)
            {
                case PathProcessResult.Consumed:
                    PathFinder.Instance.ConsumeStep(__instance);
                    PathRecoveryManager.OnProgress(__instance);
                    break;
                case PathProcessResult.Abort:
                    PathFinder.Instance.Cancel(__instance);
                    if (!PathRecoveryManager.OnFailureAndRecover(__instance))
                    {
                        __instance.cancelAllBeh();
                    }
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
            PathRecoveryManager.Clear();
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
            foreach (var portal_asset in ModClass.L.PortalLibrary.list)
            {
                if (portal_asset.Buildings.Contains(__instance.asset))
                {
                    if (pState == BuildingState.Normal)
                    {
                        Portal portal = __instance.GetBuildingComponent<Portal>();
                        if (portal == null)
                        {
                            portal = __instance.addComponent<Portal>();
                            portal.Asset = portal_asset;
                        }
                        PortalRegistry.Instance.RegisterOrUpdate(new PortalDefinition(portal, __instance.id, __instance.getConstructionTile(), 1, 1, new List<PortalConnection>()));
                        portal_asset.RequestRebuildGraph?.Invoke(portal);
                    }
                    else if (pState == BuildingState.Ruins || pState == BuildingState.Removed)
                    {
                        PortalRegistry.Instance.Remove(__instance.id);

                        Portal portal = __instance.GetBuildingComponent<Portal>();
                        if (portal == null)
                        {
                            portal = __instance.addComponent<Portal>();
                            portal.Asset = portal_asset;
                        }
                        portal_asset.RequestRebuildGraph?.Invoke(portal);
                    }
                    break;
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
        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.u1_checkInside))]
        private static bool u1_checkInside_prefix(Actor __instance)
        {
            if (!__instance.isInsideSomething())
            {
                return false;
            }
            if (__instance.is_inside_boat)
            {
                Actor actor = __instance.inside_boat?.actor ?? World.world.units.get(__instance.data.transportID);
                if (actor == null)
                {
                    __instance.is_inside_boat = false;
                    return false;
                }
                __instance.setCurrentTilePosition(actor.current_tile);
                __instance.skipUpdates();
            }
            return false;
        }


        private static PathProcessResult HandleStep(Actor actor, PathStep step)
        {
            return step.Method switch
            {
                MovementMethod.Walk => TryMove(actor, step.Tile, step.Penalty.HasFlag(StepPenalty.Block), allowLava: step.Penalty.HasFlag(StepPenalty.Lava), allowOcean: step.Penalty.HasFlag(StepPenalty.Ocean)),
                MovementMethod.Swim => TryMove(actor, step.Tile, step.Penalty.HasFlag(StepPenalty.Block), allowLava: step.Penalty.HasFlag(StepPenalty.Lava), allowOcean: step.Penalty.HasFlag(StepPenalty.Ocean)),
                MovementMethod.Portal => HandlePortal(actor, step),
                _ => PathProcessResult.Deferred
            };
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
                if (PathFinder.Instance.TryPeekStep(passenger, out var step, out var finished))
                {
                    if (step.Method == MovementMethod.Portal)
                    {
                        PathFinder.Instance.ConsumeStep(passenger);
                    }
                }
            }
        }
        private static PathProcessResult HandlePortal(Actor actor, PathStep step)
        {
            var request = PortalManager.GetRequest(actor);
            if (request == null)
            {
                PortalManager.NewRequest(step.Entry.Portal, step.Exit.Portal, actor);
                return PathProcessResult.Deferred;
            }
            return PathProcessResult.Deferred;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BehBoatFindRequest), nameof(BehBoatFindRequest.execute))]
        private static bool BehBoatFindRequest_prefix(BehBoatFindRequest __instance, Actor pActor, ref BehResult __result)
        {
            PortalManager.CancelDriverRequest(pActor);
            if (PortalManager.AssignNewRequestForDriver(pActor, PortalLibrary.Dock))
            {
                __result = __instance.forceTask(pActor, S_Task.boat_transport_go_load, true, false);
                return false;
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BehBoatTransportFindTilePickUp), nameof(BehBoatTransportFindTilePickUp.execute))]
        private static bool BehBoatTranportFindTilePickUp_prefix(BehBoatTransportFindTilePickUp __instance, Actor pActor, ref BehResult __result)
        {
            var portal_request = PortalManager.GetRequestForDriver(pActor);
            if (portal_request == null || portal_request.IsCompleted())
            {
                return true;
            }
            var dock_building = portal_request.Portals[0].PortalBuilding;
            var dock_tile = dock_building.component_docks.getOceanTileInSameOcean(pActor.current_tile);
            if (dock_tile == null)
            {
                // 如果找不到码头，则放弃这个码头的上客。这个码头的下客挪到下一个码头，并让他们重新寻路（应该是在寻路系统中进行自动纠错）。
                portal_request.Portals[1].ToUnload.UnionWith(portal_request.Portals[0].ToUnload);
                portal_request.Portals.RemoveAt(0);

                __result = BehResult.RepeatStep;
                return false;
            }
            pActor.beh_tile_target = dock_tile;
            __instance.boat.passengerWaitCounter = 0;
            __result = BehResult.Continue;
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BehBoatTransportDoLoading), nameof(BehBoatTransportDoLoading.execute))]
        private static bool BehBoatTransportDoLoading_prefix(BehBoatTransportDoLoading __instance, Actor pActor, ref BehResult __result)
        {
            var portal_request = PortalManager.GetRequestForDriver(pActor);
            if (portal_request == null || portal_request.IsCompleted())
            {
                return true;
            }
            var continue_loading = true;
            if (__instance.boat.passengerWaitCounter > 4 || __instance.boat.countPassengers() >= 100)
            {
                continue_loading = false;
            }
            else if (portal_request.Portals[0].ToLoad.All(a => __instance.boat.hasPassenger(a)))
            {
                continue_loading = false;
            }
            if (continue_loading)
            {
                foreach (var a in portal_request.Portals[0].ToLoad)
                {
                    __instance.boat.addPassenger(a);
                    if (PathFinder.Instance.TryPeekStep(a, out var step, out var finished))
                    {
                        if (step.Method == MovementMethod.Portal)
                        {
                            PathFinder.Instance.ConsumeStep(a);
                        }
                    }
                }
                portal_request.State = PortalRequestState.WaitingPassengers;
                pActor.timer_action = 12f;
                __instance.boat.passengerWaitCounter++;
                __result = BehResult.RepeatStep;
                return false;
            }
            if (!__instance.boat.hasPassengers())
            {
                PortalManager.CancelDriverRequest(pActor);
                __result = BehResult.Stop;
                return false;
            }
            portal_request.State = PortalRequestState.Driving;
            portal_request.Portals.RemoveAt(0);
            
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BehBoatTransportFindTileUnload), nameof(BehBoatTransportFindTileUnload.execute))]
        private static bool BehBoatTransportFindTileUnload_prefix(BehBoatTransportFindTileUnload __instance, Actor pActor, ref BehResult __result)
        {
            var portal_request = PortalManager.GetRequestForDriver(pActor);
            if (portal_request == null || portal_request.IsCompleted())
            {
                return true;
            }
            var current_portal = portal_request.Portals[0];
            var tile = OceanHelper.findTileForBoat(pActor.current_tile, current_portal.PortalTile);
            if (tile == null)
            {
                PortalManager.CancelDriverRequest(pActor);
                __result = BehResult.Stop;
                return false;
            }
            pActor.beh_tile_target = tile;
            __result = BehResult.Continue;
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BehBoatTransportUnloadUnits), nameof(BehBoatTransportUnloadUnits.execute))]
        private static bool BehBoatTransportUnloadUnits_prefix(BehBoatTransportUnloadUnits __instance, Actor pActor, ref BehResult __result)
        {
            var portal_request = PortalManager.GetRequestForDriver(pActor);
            if (portal_request == null || portal_request.IsCompleted())
            {
                return true;
            }
            var current_portal = portal_request.Portals[0];
            var unload_tile = current_portal.PortalTile ?? pActor.current_tile;
            var passengers = __instance.boat.getPassengers().ToList();
            if (unload_tile == null || (pActor.current_tile != null &&
                                        Toolbox.SquaredDistTile(unload_tile, pActor.current_tile) > 36))
            {
                unload_tile = pActor.current_tile;
            }
            __instance.boat.unloadPassengers(unload_tile, false);
            foreach (var passenger in passengers)
            {
                if (passenger == null || passenger.isRekt()) continue;
                PathFinder.Instance.Cancel(passenger);
                PathFinder.Instance.TryRequestRecover(passenger, passenger.tile_target ?? unload_tile);
            }
            if (portal_request.Portals.Count == 1)
            {
                portal_request.Cancel();
                __result = BehResult.Continue;
                return false;
            }
            __instance.forceTask(pActor, S_Task.boat_transport_go_load, true, false);
            __result = BehResult.Skip;
            return false;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.checkEventUnitsDestroy))]
        private static void checkEventUnitsDestroy_postfix()
        {
            PortalManager.RemoveDeadUnits();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.checkEventBuildingsDestroy))]
        private static void checkEventBuildingsDestroy_postfix()
        {
            PortalManager.RemoveDeadBuildings();
        }

        private static PathProcessResult TryMove(Actor actor, WorldTile tile, bool allowBlocks, bool allowLava,
            bool allowOcean)
        {
            if (tile == null)
            {
                return PathProcessResult.Abort;
            }

            var tileType = tile.Type;
            if (tileType == null)
            {
                return PathProcessResult.Abort;
            }
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

            if (tile.isOnFire() && !actor.isImmuneToFire() && !(actor.current_tile?.isOnFire() ?? false))
            {
                return PathProcessResult.Abort;
            }

            if (tileType.damaged_when_walked)
            {
                actor.current_tile?.tryToBreak();
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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
using UnityEngine;

namespace Cultiway.Patch
{
    internal static class PatchAboutPathfinding
    {
        private const float DiagonalTileDistance = 1.41421356237f;
        private const float CalibrationRepeatCooldownSeconds = 0.25f;
        private const string SocializeGoToTargetTaskId = "socialize_go_to_target";
        private static readonly ConcurrentDictionary<long, CalibrationState> CalibrationStates = new();

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
            if (__instance?.data == null || pTile == null)
            {
                __result = ExecuteEvent.False;
                return false;
            }

            if (__instance.current_tile == pTile)
            {
                PathFinder.Instance.Cancel(__instance);
                __instance.clearOldPath();
                __instance.setTileTarget(pTile);
                __instance.moveTo(pTile);
                __result = ExecuteEvent.True;
                return false;
            }

            if (!PathFinder.Instance.CanAcceptRequest(__instance, pTile, out _))
            {
                __result = ExecuteEvent.False;
                return false;
            }

            __instance.setTileTarget(pTile);
            __instance.next_step_position = __instance.current_tile?.posV3 ?? __instance.next_step_position;
            __instance.setNotMoving();

            __result = PathFinder.Instance.RequestPath(__instance, pTile, pPathOnWater, pWalkOnBlocks, pWalkOnLava,
                pLimitPathfindingRegions)
                ? ExecuteEvent.True
                : ExecuteEvent.False;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.updatePathMovement))]
        private static bool updatePathMovement(Actor __instance)
        {
            if (__instance == null)
            {
                return true;
            }

            if (__instance.isFollowingLocalPath() || __instance.current_path_global != null)
            {
                return true;
            }

            TryUpdateCustomPathMovement(__instance, true);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryUpdateCustomPathMovement(Actor actor, bool handleNoRequest)
        {
            var poll = PathFinder.Instance.OpenReadyCursor(actor, out var cursor);
            return TryHandleCustomPathPoll(actor, poll, ref cursor, handleNoRequest);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryHandleCustomPathPoll(
            Actor actor,
            PathPollResult poll,
            ref PathFinder.ReadyPathCursor cursor,
            bool handleNoRequest)
        {
            if (poll.Kind == PathPollKind.StepReady)
            {
                var stepResult = HandleStep(actor, poll.Step);
                switch (stepResult.Kind)
                {
                    case PathProcessKind.Consumed:
                        if (cursor.IsValid)
                        {
                            cursor.Consume();
                        }
                        else
                        {
                            PathFinder.Instance.ConsumeStep(actor);
                        }

                        PathRecoveryManager.OnProgress(actor);
                        break;
                    case PathProcessKind.Abort:
                        PathFinder.Instance.Cancel(actor);
                        cursor = default;
                        HandlePathFailure(actor, stepResult.FailureReason);
                        break;
                    case PathProcessKind.Deferred:
                        actor.timer_action = 1f;
                        actor.setNotMoving();
                        break;
                }

                if (actor.tile_target == null)
                {
                    PathFinder.Instance.Cancel(actor);
                    cursor = default;
                    actor.stopMovement();
                    PathRecoveryManager.OnProgress(actor);
                }

                return true;
            }

            if (!handleNoRequest)
            {
                if (poll.Kind != PathPollKind.Waiting)
                {
                    return false;
                }

                actor.setNotMoving();
                actor.next_step_position = actor.current_tile?.posV3 ?? actor.next_step_position;
                actor.timer_action = 0.05f;
                return true;
            }

            switch (poll.Kind)
            {
                case PathPollKind.Waiting:
                    actor.setNotMoving();
                    actor.next_step_position = actor.current_tile?.posV3 ?? actor.next_step_position;
                    actor.timer_action = 0.05f;
                    return true;
                case PathPollKind.Completed:
                    actor.setNotMoving();
                    PathRecoveryManager.OnProgress(actor);
                    return true;
                case PathPollKind.Failed:
                    HandlePathFailure(actor, poll.FailureReason);
                    return true;
                case PathPollKind.Cancelled:
                    actor.setNotMoving();
                    PathRecoveryManager.Clear(actor);
                    return true;
                case PathPollKind.NoRequest:
                    actor.setNotMoving();
                    actor.timer_action = 1f;
                    PathRecoveryManager.TryRequest(actor);
                    return true;
            }

            return true;
        }

        private static bool CanUseFastMoveTo(WorldTile tile)
        {
            return GetFastMoveBlockReason(tile) == SlowMoveReason.None;
        }

        private static SlowMoveReason GetFastMoveBlockReason(WorldTile tile)
        {
            Building building = tile.building;
            if (tile.Type.step_action != null)
            {
                return SlowMoveReason.TileStepAction;
            }

            if (building?.asset == null || !building.asset.flora)
            {
                return SlowMoveReason.None;
            }

            BuildingAsset asset = building.asset;
            switch (asset.flora_type)
            {
                case FloraType.Fungi:
                    return WorldLawLibrary.world_law_exploding_mushrooms.isEnabled()
                        ? SlowMoveReason.FungiLaw
                        : SlowMoveReason.None;
                case FloraType.Plant:
                    if (asset.type == "type_flower" && WorldLawLibrary.world_law_nectar_nap.isEnabled())
                    {
                        return SlowMoveReason.FlowerNectarLaw;
                    }

                    return WorldLawLibrary.world_law_plants_tickles.isEnabled() ||
                           WorldLawLibrary.world_law_root_pranks.isEnabled()
                        ? SlowMoveReason.PlantLaw
                        : SlowMoveReason.None;
                default:
                    return SlowMoveReason.None;
            }
        }

        private static void FastMoveTo(Actor actor, WorldTile tile, bool adjacentStep)
        {
            SetMoveStepTile(actor, tile, adjacentStep);
            actor.next_step_position = new Vector2(tile.posV3.x, tile.posV3.y);
        }

        private static void FastMoveToWithMoveToSideEffects(
            Actor actor,
            WorldTile tile,
            bool adjacentStep)
        {
            if (!actor.has_attack_target &&
                actor.current_tile != null &&
                tile.isOnFire() &&
                !actor.current_tile.isOnFire() &&
                !actor.isImmuneToFire())
            {
                actor.cancelAllBeh();
                return;
            }

            SetMoveStepTile(actor, tile, adjacentStep);
            ApplyStepActionForCurrentTile(actor);

            actor.next_step_position = new Vector2(tile.posV3.x, tile.posV3.y);
        }

        private static void SetMoveStepTile(Actor actor, WorldTile tile, bool adjacentStep)
        {
            if (!actor._is_moving)
            {
                actor._is_moving = true;
                actor.batch.c_update_movement.Add(actor);
            }

            actor._next_step_tile = tile;
            if (adjacentStep)
            {
                actor.current_tile = tile;
            }
            else if ((float)Toolbox.SquaredDistTile(actor.current_tile, tile) > 4f)
            {
                actor.dirty_current_tile = true;
            }
            else
            {
                actor.current_tile = tile;
            }
        }

        private static void ApplyStepActionForCurrentTile(Actor actor)
        {
            var currentTile = actor.current_tile;
            var tileType = currentTile?.Type;
            if (tileType == null)
            {
                return;
            }

            if (tileType.step_action != null && Randy.randomChance(tileType.step_action_chance))
            {
                tileType.step_action(currentTile, actor);
            }

            var building = currentTile.building;
            if (building == null || !building.asset.flora)
            {
                return;
            }

            var buildingAsset = building.asset;
            switch (buildingAsset.flora_type)
            {
                case FloraType.Fungi:
                    if (WorldLawLibrary.world_law_exploding_mushrooms.isEnabled())
                    {
                        MapAction.damageWorld(currentTile, 5, AssetManager.terraform.get("grenade"));
                        EffectsLibrary.spawnAtTileRandomScale("fx_explosion_small", currentTile, 0.1f, 0.15f);
                    }

                    break;
                case FloraType.Plant:
                    if (buildingAsset.type == "type_flower" &&
                        WorldLawLibrary.world_law_nectar_nap.isEnabled() &&
                        Randy.randomChance(0.1f))
                    {
                        actor.makeSleep(10f);
                        break;
                    }

                    if (WorldLawLibrary.world_law_plants_tickles.isEnabled() && Randy.randomChance(0.3f))
                    {
                        actor.tryToGetSurprised(currentTile);
                    }

                    if (WorldLawLibrary.world_law_root_pranks.isEnabled() && Randy.randomChance(0.2f))
                    {
                        actor.makeStunned();
                    }

                    break;
            }
        }

        private static void HandlePathFailure(Actor actor, PathFailureReason reason)
        {
            actor.setNotMoving();
            if (PathRecoveryManager.OnFailureAndRecover(actor, reason))
            {
                return;
            }

            PathRecoveryManager.Clear(actor);
            actor.cancelAllBeh();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Actor), nameof(Actor.isUsingPath))]
        private static void isUsingPath_postfix(Actor __instance, ref bool __result)
        {
            if (!__result && __instance?.tile_target != null)
            {
                __result = PathFinder.Instance.IsActorPathing(__instance);
            }
        }

        [HarmonyTranspiler, HarmonyPatch(typeof(Actor), nameof(Actor.u10_checkSmoothMovement))]
        private static IEnumerable<CodeInstruction> u10_checkSmoothMovement_transpiler(IEnumerable<CodeInstruction> codes)
        {
            var checkCalibrate = AccessTools.Method(typeof(Actor), "checkCalibrateTargetPosition");
            var updateMovement = AccessTools.Method(typeof(Actor), "updateMovement", new[] { typeof(float), typeof(float) });
            var checkCalibrateOptimized = AccessTools.Method(typeof(PatchAboutPathfinding),
                nameof(CheckCalibrateTargetPositionOptimized));
            var updateMovementOptimized = AccessTools.Method(typeof(PatchAboutPathfinding),
                nameof(UpdateMovementOptimized));

            foreach (var code in codes)
            {
                if (code.Calls(checkCalibrate))
                {
                    yield return new CodeInstruction(OpCodes.Call, checkCalibrateOptimized).WithLabels(code.labels);
                    continue;
                }

                if (code.Calls(updateMovement))
                {
                    yield return new CodeInstruction(OpCodes.Call, updateMovementOptimized).WithLabels(code.labels);
                    continue;
                }

                yield return code;
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Actor), "updateMovement")]
        private static bool updateMovement_prefix(Actor __instance, float pElapsed, float pWalkedDistance = 0f)
        {
            UpdateMovementOptimized(__instance, pElapsed, pWalkedDistance);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckCalibrateTargetPositionOptimized(Actor actor)
        {
            var target = actor.beh_actor_target;
            if (target == null)
            {
                return;
            }

            if (actor.hasRangeAttack())
            {
                return;
            }

            var action = actor.hasTask() ? actor.ai.action : null;
            if (action == null || !action.calibrate_target_position)
            {
                return;
            }

            var isActorTarget = target.isActor();
            var targetActor = target.a;
            var targetTile = targetActor?.current_tile;
            var tileTarget = actor.tile_target;
            if (!isActorTarget || targetTile == null || tileTarget == null)
            {
                return;
            }

            var dx = targetTile.x - tileTarget.x;
            var dy = targetTile.y - tileTarget.y;
            var maxDist = action.check_actor_target_position_distance;
            if (dx * dx + dy * dy > maxDist * maxDist)
            {
                if (IsSocializeGoToTarget(actor))
                {
                    return;
                }

                if (ShouldSkipRepeatedCalibration(actor, action, targetActor, targetTile))
                {
                    return;
                }

                actor.clearPathForCalibration();
                action.startExecute(actor);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateMovementOptimized(Actor actor, float elapsed, float walkedDistance)
        {
            var movementBudget = actor._current_combined_movement_speed * elapsed;
            var canFlip = actor.asset.can_flip && actor.checkFlip();
            var customPathCursor = default(PathFinder.ReadyPathCursor);

            for (int i = 0; i < 256; i++)
            {
                var current = actor.current_position;
                var target = actor.next_step_position;

                if (canFlip)
                {
                    actor.setFlip(current.x < target.x);
                }

                var movementDelta = movementBudget - walkedDistance;
                if (movementDelta < 0f)
                {
                    movementDelta = 0f;
                }

                var dx = target.x - current.x;
                var dy = target.y - current.y;
                var distSq = dx * dx + dy * dy;
                var deltaSq = movementDelta * movementDelta;

                if (distSq >= deltaSq)
                {
                    if (movementDelta > 0f && distSq > 0f)
                    {
                        var scale = movementDelta / Mathf.Sqrt(distSq);
                        actor.current_position = new Vector2(current.x + dx * scale, current.y + dy * scale);
                    }
                    return;
                }

                actor.current_position = target;
                var walked = GetBoundaryWalkedDistance(distSq);

                ContinuePathMovementFromSmooth(actor, ref customPathCursor);

                if (!actor.is_moving)
                {
                    return;
                }

                walkedDistance += walked;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetBoundaryWalkedDistance(float distSq)
        {
            if (distSq <= 0f)
            {
                return 0f;
            }

            if (distSq > 0.999f && distSq < 1.001f)
            {
                return 1f;
            }

            if (distSq > 1.999f && distSq < 2.001f)
            {
                return DiagonalTileDistance;
            }

            return Mathf.Sqrt(distSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ContinuePathMovementFromSmooth(
            Actor actor,
            ref PathFinder.ReadyPathCursor customPathCursor)
        {
            if (actor.isFollowingLocalPath() || actor.current_path_global != null)
            {
                actor.updatePathMovement();
                return;
            }

            if (actor.tile_target != null)
            {
                PathPollResult poll;
                if (customPathCursor.IsValid)
                {
                    poll = customPathCursor.Poll();
                }
                else
                {
                    poll = PathFinder.Instance.OpenReadyCursor(actor, out customPathCursor);
                }

                if (TryHandleCustomPathPoll(actor, poll, ref customPathCursor, false))
                {
                    return;
                }
            }

            actor.stopMovement();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(MapBox), nameof(MapBox.clearWorld))]
        private static void clearWorld_prefix()
        {
            CalibrationStates.Clear();
        }
        [HarmonyPrefix, HarmonyPatch(typeof(Actor), nameof(Actor.Dispose))]
        private static void Dispose_prefix(Actor __instance)
        {
            if (__instance.data == null) return;
            CalibrationStates.TryRemove(__instance.data.id, out _);
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
            if (step.Method == MovementMethod.Portal)
            {
                return HandlePortal(actor, step);
            }

            return step.Method switch
            {
                MovementMethod.Walk => TryMove(actor, step),
                MovementMethod.Swim => TryMove(actor, step),
                _ => PathProcessResult.Deferred()
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
                ConsumeReadyPortalStep(passenger);
            }
        }

        private static void ConsumeReadyPortalStep(Actor actor)
        {
            if (PathFinder.Instance.PeekReadyStep(actor, out var readyStep).Kind == PathPollKind.StepReady &&
                readyStep.Step.Method == MovementMethod.Portal)
            {
                readyStep.Consume();
            }
        }

        private static PathProcessResult HandlePortal(Actor actor, PathStep step)
        {
            if (step.Entry?.Portal == null || step.Exit?.Portal == null)
            {
                return PathProcessResult.Abort(PathFailureReason.PortalUnavailable);
            }

            var request = PortalManager.GetRequest(actor);
            if (request == null)
            {
                var created = step.Entry.Portal.Asset == global::Cultiway.Content.Portals.TeleportArray
                    ? PortalManager.NewRequest(step.Entry, step.Exit, actor)
                    : PortalManager.NewRequest(step.Entry.Portal, step.Exit.Portal, actor);
                if (!created)
                {
                    return PathProcessResult.Abort(PathFailureReason.PortalUnavailable);
                }

                return PathProcessResult.Deferred();
            }

            return request.IsCompleted()
                ? PathProcessResult.Abort(PathFailureReason.TransportFailed)
                : PathProcessResult.Deferred();
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
            if (portal_request.Portals == null || portal_request.Portals.Count < 2)
            {
                portal_request.Cancel();
                __result = BehResult.Stop;
                return false;
            }

            var current_portal = portal_request.Portals[0];
            var dock_building = current_portal?.PortalBuilding;
            var dock_tile = dock_building?.component_docks?.getOceanTileInSameOcean(pActor.current_tile);
            if (dock_tile == null)
            {
                // 如果找不到码头，则放弃这个码头的上客。这个码头的下客挪到下一个码头，并让他们重新寻路（应该是在寻路系统中进行自动纠错）。
                if (portal_request.Portals.Count > 1)
                {
                    if (current_portal?.ToUnload != null)
                    {
                        portal_request.Portals[1].ToUnload.UnionWith(current_portal.ToUnload);
                        portal_request.Portals[1].MergePassengerTilesFrom(current_portal);
                    }
                    portal_request.Portals.RemoveAt(0);

                    __result = BehResult.RepeatStep;
                    return false;
                }

                portal_request.Cancel();
                __result = BehResult.Stop;
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
                    ConsumeReadyPortalStep(a);
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

        private static PathProcessResult TryMove(Actor actor, PathStep step)
        {
            if (actor?.data == null || actor.asset == null)
            {
                return PathProcessResult.Abort(PathFailureReason.InvalidActor);
            }

            var currentTile = actor.current_tile;
            if (currentTile == null)
            {
                return PathProcessResult.Abort(PathFailureReason.InvalidStart);
            }

            var tile = step.Tile;
            if (tile == null)
            {
                return PathProcessResult.Abort(PathFailureReason.UnsafeStep);
            }

            var isBoat = actor.asset.is_boat;
            var tileType = tile.Type;
            if (tileType == null)
            {
                return PathProcessResult.Abort(PathFailureReason.UnsafeStep);
            }

            if (isBoat && !tile.isGoodForBoat())
            {
                actor.callbacks_cancel_path_movement?.Invoke(actor);
                return PathProcessResult.Abort(PathFailureReason.StepBlocked);
            }

            if (tileType.damaged_when_walked)
            {
                currentTile.tryToBreak();
            }

            var adjacentStep = (step.Hazards & HazardFlags.Direct) == 0;
            var plannedFire = (step.Hazards & HazardFlags.Fire) != 0;
            var slowMoveReason = isBoat ? SlowMoveReason.Boat : SlowMoveReason.None;
            var useFastMove = false;
            if (!isBoat)
            {
                if (plannedFire)
                {
                    useFastMove = true;
                }
                else
                {
                    slowMoveReason = GetFastMoveBlockReason(tile);
                    useFastMove = slowMoveReason == SlowMoveReason.None;
                }
            }

            if (useFastMove)
            {
                FastMoveTo(actor, tile, adjacentStep);
            }
            else if (CanReplayMoveToSideEffects(slowMoveReason))
            {
                FastMoveToWithMoveToSideEffects(actor, tile, adjacentStep);
            }
            else
            {
                actor.moveTo(tile);
            }

            return PathProcessResult.Consumed();
        }

        private readonly struct PathProcessResult
        {
            private PathProcessResult(PathProcessKind kind, PathFailureReason failureReason)
            {
                Kind = kind;
                FailureReason = failureReason;
            }

            public PathProcessKind Kind { get; }
            public PathFailureReason FailureReason { get; }

            public static PathProcessResult Consumed()
            {
                return new PathProcessResult(PathProcessKind.Consumed, PathFailureReason.None);
            }

            public static PathProcessResult Deferred()
            {
                return new PathProcessResult(PathProcessKind.Deferred, PathFailureReason.None);
            }

            public static PathProcessResult Abort(PathFailureReason reason)
            {
                return new PathProcessResult(PathProcessKind.Abort,
                    reason == PathFailureReason.None ? PathFailureReason.UnsafeStep : reason);
            }
        }

        private enum PathProcessKind
        {
            Consumed,
            Deferred,
            Abort
        }

        private enum SlowMoveReason
        {
            None,
            Boat,
            TileStepAction,
            FungiLaw,
            FlowerNectarLaw,
            PlantLaw,
            Unknown
        }

        private static bool CanReplayMoveToSideEffects(SlowMoveReason reason)
        {
            return reason == SlowMoveReason.TileStepAction ||
                   reason == SlowMoveReason.FungiLaw ||
                   reason == SlowMoveReason.FlowerNectarLaw ||
                   reason == SlowMoveReason.PlantLaw;
        }

        private static bool IsSocializeGoToTarget(Actor actor)
        {
            return actor?.ai?.task?.id == SocializeGoToTargetTaskId;
        }

        private static bool ShouldSkipRepeatedCalibration(
            Actor actor,
            BehaviourActionActor action,
            Actor targetActor,
            WorldTile targetTile)
        {
            if (actor?.data == null || action == null || targetActor?.data == null || targetTile?.data == null)
            {
                return false;
            }

            var actorId = actor.data.id;
            var targetId = targetActor.data.id;
            var targetTileId = targetTile.data.tile_id;
            var now = Time.unscaledTime;
            if (CalibrationStates.TryGetValue(actorId, out var state) &&
                state.TargetId == targetId &&
                ReferenceEquals(state.Action, action) &&
                now < state.NextAllowedTime)
            {
                return true;
            }

            CalibrationStates[actorId] =
                new CalibrationState(targetId, targetTileId, action, now + CalibrationRepeatCooldownSeconds);
            return false;
        }

        private readonly struct CalibrationState
        {
            public CalibrationState(long targetId, int targetTileId, BehaviourActionActor action, float nextAllowedTime)
            {
                TargetId = targetId;
                TargetTileId = targetTileId;
                Action = action;
                NextAllowedTime = nextAllowedTime;
            }

            public long TargetId { get; }
            public int TargetTileId { get; }
            public BehaviourActionActor Action { get; }
            public float NextAllowedTime { get; }
        }

    }
}

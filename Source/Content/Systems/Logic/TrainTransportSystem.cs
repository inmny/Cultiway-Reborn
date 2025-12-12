using System;
using System.Collections.Generic;
using System.Linq;
using ai;
using Cultiway.Core.Pathfinding;
using Friflo.Engine.ECS.Systems;
using UnityEngine;
using tools;

namespace Cultiway.Content
{
    /// <summary>
    /// 负责处理火车站的PortalRequest：发车、停靠、上下客、销毁。
    /// </summary>
    public class TrainTransportSystem : BaseSystem
    {
        private enum RideStage
        {
            Preparing,
            StationStop,
            Travelling,
            Completed,
            Failed
        }

        private sealed class RideState
        {
            public PortalRequest Request;
            public Actor Train;
            public RideStage Stage;
            public float PrepareDeadline;
            public float StopBaseDeadline;
            public float StopHardDeadline;
            public float StopStartTime;
            public List<WorldTile> CurrentTrack;
            public PortalRequest.SinglePortal CurrentPortal;
            public readonly HashSet<Actor> Carrying = new();
        }

        private readonly Dictionary<PortalRequest, RideState> _rides = new();
        private readonly Dictionary<long, float> _stationCooldown = new();

        protected override void OnUpdateGroup()
        {
            base.OnUpdateGroup();
            if (!Config.game_loaded)
            {
                return;
            }

            var snapshot = PortalManager.SnapshotRequests();
            CleanupMissing(snapshot);

            foreach (var request in snapshot)
            {
                if (request == null || request.IsCompleted() || !IsTrainRequest(request))
                {
                    CleanupRide(request);
                    continue;
                }

                if (!_rides.TryGetValue(request, out var state))
                {
                    state = new RideState
                    {
                        Request = request,
                        Stage = RideStage.Preparing,
                        PrepareDeadline = Time.time + TrainConfig.PrepareWaitMax
                    };
                    _rides[request] = state;
                }

                switch (state.Stage)
                {
                    case RideStage.Preparing:
                        UpdatePreparing(state);
                        break;
                    case RideStage.StationStop:
                        UpdateStationStop(state);
                        break;
                    case RideStage.Travelling:
                        UpdateTravelling(state);
                        break;
                }
            }
        }

        private void CleanupMissing(List<PortalRequest> snapshot)
        {
            var snapshotSet = new HashSet<PortalRequest>(snapshot);
            foreach (var kv in _rides.ToList())
            {
                if (!snapshotSet.Contains(kv.Key) || kv.Key.IsCompleted())
                {
                    DestroyRide(kv.Value);
                    _rides.Remove(kv.Key);
                }
            }
        }

        private static bool IsTrainRequest(PortalRequest request)
        {
            if (request?.Portals == null || request.Portals.Count == 0)
            {
                return false;
            }

            return request.PortalType == Portals.TrainStation;
        }

        private void UpdatePreparing(RideState state)
        {
            var request = state.Request;
            if (request.Portals.Count < 2)
            {
                request.Cancel();
                CleanupRide(request);
                return;
            }

            var origin = request.Portals[0];
            var target = request.Portals[1];
            if (origin?.PortalBuilding == null || target?.PortalBuilding == null)
            {
                request.Cancel();
                CleanupRide(request);
                return;
            }

            if (!TrainTrackRepairSystem.TryGetPath(origin.PortalBuilding.id, target.PortalBuilding.id,
                    out var track))
            {
                request.Cancel();
                CleanupRide(request);
                return;
            }

            // 乘客太久未到，不再等待。
            var now = Time.time;
            var readyAt = Math.Max(state.PrepareDeadline, GetStationCooldown(origin.PortalBuilding.id));
            if (now < readyAt)
            {
                return;
            }

            var spawnTile = origin.PortalTile ?? origin.PortalBuilding.getConstructionTile();
            var train = World.world.units.createNewUnit(Actors.Train.id, spawnTile);
            if (train == null)
            {
                request.Cancel();
                CleanupRide(request);
                return;
            }

            var trainComp = train.getActorComponent<ActorComponents.Train>();
            if (trainComp == null)
            {
                request.Cancel();
                CleanupRide(request);
                return;
            }

            trainComp.ConfigureTrack(track, true, true);
            trainComp.Hide();
            state.Train = train;
            state.CurrentTrack = track;
            state.CurrentPortal = origin;
            state.StopStartTime = now;
            state.StopBaseDeadline = now + TrainConfig.StopBaseWait;
            state.StopHardDeadline = state.StopBaseDeadline + TrainConfig.StopExtraWaitMax;
            state.Stage = RideStage.StationStop;
            request.State = PortalRequestState.WaitingPassengers;
        }

        private void UpdateStationStop(RideState state)
        {
            var request = state.Request;
            if (request.Portals.Count == 0)
            {
                FinishRequest(state);
                return;
            }

            var portal = request.Portals[0];
            if (!IsPortalAlive(portal))
            {
                request.Cancel();
                FinishRequest(state);
                return;
            }

            state.CurrentPortal = portal;
            DropPassengers(state, portal);
            BoardPassengers(state, portal);

            var now = Time.time;
            bool baseElapsed = now >= state.StopBaseDeadline;
            bool hardElapsed = now >= state.StopHardDeadline;
            bool loadFinished = portal.ToLoad.Count == 0;

            if (!baseElapsed && !hardElapsed)
            {
                return;
            }

            if (!loadFinished && !hardElapsed)
            {
                return;
            }

            // 清理未能上车的迟到乘客
            IgnoreLatePassengers(portal);

            // 当前站点处理完毕，准备出发或结束
            request.Portals.RemoveAt(0);
            if (request.Portals.Count == 0)
            {
                FinishRequest(state);
                return;
            }

            var nextPortal = request.Portals[0];
            if (nextPortal?.PortalBuilding == null || !TrainTrackRepairSystem.TryGetPath(
                    portal.PortalBuilding.id, nextPortal.PortalBuilding.id, out var track))
            {
                request.Cancel();
                FinishRequest(state);
                return;
            }

            state.CurrentTrack = track;
            state.StopStartTime = now;
            state.StopBaseDeadline = now + TrainConfig.StopBaseWait;
            state.StopHardDeadline = state.StopBaseDeadline + TrainConfig.StopExtraWaitMax;
            state.Stage = RideStage.Travelling;
            request.State = PortalRequestState.Driving;

            if (state.Train != null)
            {
                var trainComp = state.Train.getActorComponent<ActorComponents.Train>();
                trainComp?.ConfigureTrack(track, true, true);
                trainComp?.Show();
                trainComp?.ResetProgress(true);
            }

            _stationCooldown[portal.PortalBuilding.id] = now + TrainConfig.MinDepartInterval;
        }

        private void UpdateTravelling(RideState state)
        {
            if (state.Train == null || state.Train.isRekt())
            {
                state.Request.Cancel();
                FinishRequest(state);
                return;
            }

            var trainComp = state.Train.getActorComponent<ActorComponents.Train>();
            if (trainComp == null)
            {
                state.Request.Cancel();
                FinishRequest(state);
                return;
            }

            if (!trainComp.IsSegmentFinished)
            {
                return;
            }

            // 到站
            trainComp.Hide();
            state.Stage = RideStage.StationStop;
            state.Request.State = PortalRequestState.WaitingPassengers;
            state.StopStartTime = Time.time;
            state.StopBaseDeadline = state.StopStartTime + TrainConfig.StopBaseWait;
            state.StopHardDeadline = state.StopBaseDeadline + TrainConfig.StopExtraWaitMax;
        }

        private void DropPassengers(RideState state, PortalRequest.SinglePortal portal)
        {
            if (portal?.ToUnload == null)
            {
                return;
            }
            var unloadTile = portal.PortalTile ?? state.Train?.current_tile ?? portal.PortalBuilding?.current_tile;
            foreach (var passenger in portal.ToUnload.ToList())
            {
                if (passenger == null || passenger.isRekt())
                {
                    portal.ToUnload.Remove(passenger);
                    state.Carrying.Remove(passenger);
                    continue;
                }

                if (state.Carrying.Contains(passenger))
                {
                    passenger.is_inside_boat = false;
                }

                if (unloadTile != null)
                {
                    passenger.spawnOn(unloadTile, 0f);
                }
                else
                {
                    passenger.spawnOn(passenger.current_tile, 0f);
                }
                state.Carrying.Remove(passenger);
                portal.ToUnload.Remove(passenger);
            }
        }

        private void BoardPassengers(RideState state, PortalRequest.SinglePortal portal)
        {
            if (portal?.ToLoad == null)
            {
                return;
            }

            var tile = portal.PortalTile ?? portal.PortalBuilding?.current_tile;
            if (tile == null)
            {
                return;
            }

            foreach (var passenger in portal.ToLoad.ToList())
            {
                if (passenger == null || passenger.isRekt())
                {
                    portal.ToLoad.Remove(passenger);
                    continue;
                }

                if (!HasReachedStation(passenger, tile))
                {
                    continue;
                }

                passenger.is_inside_boat = true;
                state.Carrying.Add(passenger);
                portal.ToLoad.Remove(passenger);

                if (PathFinder.Instance.TryPeekStep(passenger, out var step, out var _)
                    && step.Method == MovementMethod.Portal)
                {
                    PathFinder.Instance.ConsumeStep(passenger);
                }
            }
        }

        private void IgnoreLatePassengers(PortalRequest.SinglePortal portal)
        {
            if (portal?.ToLoad == null)
            {
                return;
            }

            foreach (var passenger in portal.ToLoad.ToList())
            {
                if (passenger == null || passenger.isRekt())
                {
                    portal.ToLoad.Remove(passenger);
                    continue;
                }
                portal.ToLoad.Remove(passenger);
                PathFinder.Instance.TryRequestRecover(passenger, passenger.tile_target ?? passenger.current_tile);
            }
        }

        private void FinishRequest(RideState state)
        {
            state.Request.Cancel();
            DestroyRide(state);
        }

        private void DestroyRide(RideState state)
        {
            if (state == null)
            {
                return;
            }
            var unload_tile = state.Train?.current_tile ?? state.CurrentPortal?.PortalTile ?? state.CurrentPortal?.PortalBuilding?.current_tile;

            foreach (var passenger in state.Carrying.ToList())
            {
                if (passenger == null || passenger.isRekt()) continue;
                passenger.is_inside_boat = false;
                if (unload_tile != null)
                {
                    passenger.spawnOn(unload_tile, 0f);
                }
                else
                {
                    passenger.spawnOn(passenger.current_tile, 0f);
                }
            }

            if (state.Train != null && !state.Train.isRekt())
            {
                state.Train.die(true);
            }

            state.Carrying.Clear();
            _rides.Remove(state.Request);
        }

        private void CleanupRide(PortalRequest request)
        {
            if (request == null) return;
            if (_rides.TryGetValue(request, out var state))
            {
                DestroyRide(state);
            }
        }

        private float GetStationCooldown(long stationId)
        {
            if (_stationCooldown.TryGetValue(stationId, out var ready))
            {
                return ready;
            }
            return 0f;
        }

        private static bool IsPortalAlive(PortalRequest.SinglePortal portal)
        {
            return portal != null && portal.PortalBuilding != null && !portal.PortalBuilding.isRekt();
        }

        private static bool HasReachedStation(Actor actor, WorldTile tile)
        {
            if (actor?.current_tile == null || tile == null) return false;
            return Toolbox.SquaredDistTile(actor.current_tile, tile) <= 4;
        }
    }
}


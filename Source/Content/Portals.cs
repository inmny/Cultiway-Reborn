using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cultiway.Abstract;
using Cultiway.Content.Systems.Logic;
using Cultiway.Core.Libraries;

namespace Cultiway.Content
{
    [Dependency(typeof(Buildings))]
    public class Portals : ExtendLibrary<PortalAsset, Portals>
    {
        protected override bool AutoRegisterAssets()
        {
            return true;
        }
        public static PortalAsset TrainStation {get; private set;}
        public static PortalAsset TeleportArray {get; private set;}
        protected override void OnInit()
        {
            TrainStation.Buildings.Add(Buildings.TrainStation);
            TrainStation.CancelOnMissingBuilding = true;
            TrainStation.FreezeAfterAssignment = true;
            TrainStation.PassengerRequested = Railways.TrainPassengerService.RememberJourney;
            TrainStation.PassengerTile = portal => Railways.RailwayStationService.GetPassengerTile(portal.building);
            TrainStation.ResolveRoute = Railways.RailwayRouteService.GetRoute;
            TrainStation.CanRequest = Railways.RailwayPassengerRules.CanRequest;
            TrainStation.PassengerGroupKey = Railways.RailwayPassengerRules.GroupKey;
            TrainStation.CanJoinJourney = (actor, request, start, target) =>
                TrainTransportSystem.Instance.CanJoinJourney(actor, request, start, target);
            TrainStation.RequestRebuildGraph = _ => TrainTrackRepairSystem.RequestRebuild();
            TeleportArray.Buildings.Add(Buildings.TeleportArray);
            TeleportArray.RequestRebuildGraph = _ => TeleportArraySystem.RequestRebuild();
        }
    }
}

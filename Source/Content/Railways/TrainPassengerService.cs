using System.Linq;
using System.Collections.Generic;
using Cultiway.Core.BuildingComponents;
using Cultiway.Core.Pathfinding;

namespace Cultiway.Content.Railways;

/// <summary>通过原版承载关系统一上下车和旅程恢复。</summary>
internal static class TrainPassengerService
{
    /// <summary>最终目的地格子。</summary>
    internal const string GoalKey = "cw.railway.goal";
    /// <summary>始发站编号。</summary>
    internal const string OriginKey = "cw.railway.origin";
    /// <summary>下车站编号。</summary>
    internal const string DestinationKey = "cw.railway.destination";
    /// <summary>按载客车厢顺序排列的座位编号。</summary>
    internal const string SeatKey = "cw.railway.seat";

    /// <summary>在等车前保存业务目的地，而非上车后再读取已被清除的目标。</summary>
    /// <param name="passenger">申请乘车的人。</param>
    /// <param name="origin">始发站。</param>
    /// <param name="destination">下车站。</param>
    internal static void RememberJourney(Actor passenger, Portal origin, Portal destination)
    {
        passenger.data.get(GoalKey, out int goal, -1);
        if (goal < 0)
            passenger.data.set(GoalKey, (passenger.tile_target ?? destination.building.current_tile).tile_id);
        passenger.data.set(OriginKey, origin.building.id);
        passenger.data.set(DestinationKey, destination.building.id);
        RailwayPassengerRules.Remember(passenger);
    }

    /// <summary>只有实际到站、未在其他载具内的人才能占用本车客位。</summary>
    /// <param name="passenger">候车者。</param>
    /// <param name="train">已经停站的列车。</param>
    /// <returns>实际登车时为真。</returns>
    internal static bool Board(Actor passenger, ActorComponents.Train train)
    {
        if (passenger.is_inside_boat || passenger.isFighting() || ControllableUnit.isControllingUnit(passenger) ||
            !RailwayPassengerRules.IsCommandCurrent(passenger) || passenger.kingdom != train.Head.kingdom ||
            train.Carrier.countPassengers() >= train.PassengerCapacity) return false;
        var used = new HashSet<int>(train.Carrier.getPassengers().Select(p =>
        {
            p.data.get(SeatKey, out int seat, -1);
            return seat;
        }));
        int free = 0;
        while (used.Contains(free)) free++;
        passenger.data.set(SeatKey, free);
        passenger.embarkInto(train.Carrier);
        RailwayPassengerRules.Boarded(passenger);
        return true;
    }

    /// <summary>只允许本车实际乘客下车，然后恢复剩余出行。</summary>
    /// <param name="passenger">需要下车的乘客。</param>
    /// <param name="carrier">本车承载组件。</param>
    /// <param name="tile">实际下车地点。</param>
    /// <param name="completed">是否完成原定下车。</param>
    internal static void Disembark(Actor passenger, Boat carrier, WorldTile tile, bool completed = true)
    {
        if (!carrier.hasPassenger(passenger)) return;
        carrier.removePassenger(passenger);
        if (!passenger.isAlive()) return;
        passenger.disembarkTo(carrier, FindSafeTile(tile));
        RailwayPassengerRules.EndJourney(passenger, completed);
        Recover(passenger);
    }

    /// <summary>撤销未上车者的整段行程，实际位置保持不动。</summary>
    /// <param name="passenger">取消候车的人。</param>
    /// <param name="request">原运输任务。</param>
    internal static void CancelWaiting(Actor passenger, PortalRequest request)
    {
        request.RemovePassenger(passenger);
        if (passenger != null) RailwayPassengerRules.EndJourney(passenger, false);
        if (passenger != null && passenger.isAlive() && !passenger.is_inside_boat) Recover(passenger);
    }

    /// <summary>释放全部实际乘客，不使用车站下客名单推断车内人员。</summary>
    /// <param name="carrier">被毁或终止的列车。</param>
    /// <param name="tile">最后实际位置。</param>
    internal static void Evacuate(Boat carrier, WorldTile tile)
    {
        foreach (var passenger in carrier.getPassengers().ToArray()) Disembark(passenger, carrier, tile, false);
    }

    /// <summary>优先在事故周边的安全陆地下车，不传送到远站。</summary>
    /// <param name="tile">实际停车或事故格。</param>
    /// <returns>当前格或相邻的安全格。</returns>
    private static WorldTile FindSafeTile(WorldTile tile)
    {
        if (tile.building?.asset == Buildings.TrainStation && tile.building.isNormal())
        {
            var point = RailwayStationService.GetPassengerTile(tile.building);
            if (point != null) return point;
        }
        if (IsSafe(tile)) return tile;
        foreach (var neighbour in tile.neighboursAll)
            if (IsSafe(neighbour)) return neighbour;
        return tile;
    }

    /// <summary>判断可站立的安全陆地。</summary>
    /// <param name="tile">需要检查的格子。</param>
    /// <returns>可安全下车时为真。</returns>
    private static bool IsSafe(WorldTile tile)
    {
        return RailTrackRoutePlanner.IsSafeGround(tile) && tile.building == null;
    }

    /// <summary>清除乘坐记录，并从实际位置继续走向保存的最终目标。</summary>
    /// <param name="passenger">结束本次乘坐的人。</param>
    private static void Recover(Actor passenger)
    {
        passenger.data.get(GoalKey, out int goal, -1);
        goal = RailwayPassengerRules.ResumeGoal(passenger, goal);
        passenger.data.removeInt(GoalKey);
        passenger.data.removeInt(SeatKey);
        passenger.data.removeLong(OriginKey);
        passenger.data.removeLong(DestinationKey);
        RailwayPassengerRules.ClearJourney(passenger);
        PathFinder.Instance.Cancel(passenger);
        if (goal >= 0 && goal < World.world.tiles_list.Length)
            PathFinder.Instance.TryRequestRecover(passenger, World.world.tiles_list[goal]);
    }
}

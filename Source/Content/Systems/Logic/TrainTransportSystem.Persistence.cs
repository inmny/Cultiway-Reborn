using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.Persistence;
using Cultiway.Core.Performance;

namespace Cultiway.Content;

/// <summary>列车行程、候车人员与上车关系随铁路一同保存。</summary>
public partial class TrainTransportSystem
{
    /// <summary>内容入口只创建一个列车调度系统。</summary>
    private static TrainTransportSystem instance;
    /// <summary>登记当前运行的调度器，供世界存档取得真实行程。</summary>
    public TrainTransportSystem() => instance = this;

    /// <summary>保存全部仍在进行的铁路行程。</summary>
    internal static TrainTransportSaveData CaptureWorldSave()
    {
        var data = new TrainTransportSaveData();
        foreach (PortalRequest request in PortalManager.SnapshotRequests())
        {
            if (request.IsCompleted() || !IsTrainRequest(request)) continue;
            instance._rides.TryGetValue(request, out RideState state);
            data.Rides.Add(new TrainRideSaveData
            {
                Train = state?.Train?.data.id ?? -1, Stage = (int)(state?.Stage ?? RideStage.Preparing),
                PrepareAt = state?.PrepareDeadline ?? SimulationTime.Now,
                StopBaseAt = state?.StopBaseDeadline ?? 0, StopHardAt = state?.StopHardDeadline ?? 0,
                StopStartedAt = state?.StopStartTime ?? 0,
                Carrying = state?.Carrying.Select(actor => actor.data.id).ToArray() ?? Array.Empty<long>(),
                Track = state?.CurrentTrack?.Select(tile => tile.tile_id).ToArray(),
                CurrentStop = state?.CurrentPortal == null ? null : CaptureStop(state.CurrentPortal),
                Stops = request.Portals.Select(CaptureStop).ToList(),
                State = (int)request.State, AllowEmpty = request.AllowEmptyRide, Experimental = request.IsExperimentalRide,
                Motion = state?.Train?.getActorComponent<ActorComponents.Train>()?.CaptureWorldSave()
            });
        }
        return data;
    }

    /// <summary>恢复当前行程，不重新发车或重复上客。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    /// <param name="report">本次读取的转换与跳过报告。</param>
    internal static void RestoreWorldSave(TrainTransportSaveData data, SaveLoadReport report)
    {
        instance.ResetForNewWorld();
        foreach (TrainRideSaveData value in data.Rides)
        {
            try
            {
                var request = new PortalRequest
                {
                    PortalType = Portals.TrainStation, Driver = World.world.units.get(value.Train),
                    State = (PortalRequestState)value.State, AllowEmptyRide = value.AllowEmpty,
                    IsExperimentalRide = value.Experimental, Portals = value.Stops.Select(RestoreStop).ToList()
                };
                if (value.Stage < 0 || value.Stage > (int)RideStage.Failed || value.State < 0 || value.State > 3 ||
                    value.Train >= 0 && request.Driver == null || value.Track?.Any(tile => (uint)tile >= (uint)World.world.tiles_list.Length) == true)
                    throw new InvalidDataException("列车、路线或行程状态无效");
                var state = new RideState
                {
                    Request = request, Train = request.Driver, Stage = (RideStage)value.Stage,
                    PrepareDeadline = value.PrepareAt, StopBaseDeadline = value.StopBaseAt,
                    StopHardDeadline = value.StopHardAt, StopStartTime = value.StopStartedAt,
                    CurrentTrack = value.Track?.Select(tile => World.world.tiles_list[tile]).ToList(),
                    CurrentPortal = value.CurrentStop == null ? null : RestoreStop(value.CurrentStop)
                };
                foreach (long id in value.Carrying)
                {
                    Actor actor = World.world.units.get(id);
                    if (actor == null) continue;
                    state.Carrying.Add(actor);
                    actor.is_inside_boat = true;
                    actor.data.transportID = value.Train;
                }
                if (value.Motion != null)
                {
                    var train = state.Train?.getActorComponent<ActorComponents.Train>();
                    if (train == null) throw new InvalidDataException("列车组件未能恢复");
                    train.RestoreWorldSave(value.Motion);
                }
                PortalManager.AddRestoredRequest(request);
                instance._rides.Add(request, state);
            }
            catch (InvalidDataException error)
            {
                report.Skip("列车行程", value.Train.ToString(), error.Message);
                foreach (long id in value.Carrying)
                {
                    Actor actor = World.world.units.get(id);
                    if (actor == null) continue;
                    actor.is_inside_boat = false;
                    actor.data.transportID = -1;
                    actor.spawnOn(actor.current_tile, 0);
                }
            }
        }
    }

    /// <summary>候车点只保存人物、建筑与地块编号。</summary>
    /// <param name="value">当前值。</param>
    private static TrainStopSaveData CaptureStop(PortalRequest.SinglePortal value) => new()
    {
        Station = value.PortalBuilding.id, Tile = value.PortalTile?.tile_id ?? -1,
        Load = value.ToLoad.Select(actor => actor.data.id).ToArray(),
        Unload = value.ToUnload.Select(actor => actor.data.id).ToArray(),
        LoadTiles = value.LoadTiles?.ToDictionary(entry => entry.Key, entry => entry.Value.tile_id),
        UnloadTiles = value.UnloadTiles?.ToDictionary(entry => entry.Key, entry => entry.Value.tile_id)
    };

    /// <summary>核对候车点后连接实际车站与旅客。</summary>
    /// <param name="value">当前值。</param>
    private static PortalRequest.SinglePortal RestoreStop(TrainStopSaveData value)
    {
        Building station = World.world.buildings.get(value.Station);
        if (station?.asset != Buildings.TrainStation || value.Tile < -1 || value.Tile >= World.world.tiles_list.Length)
            throw new InvalidDataException("行程车站或位置已不存在");
        return new PortalRequest.SinglePortal
        {
            PortalBuilding = station, PortalTile = value.Tile < 0 ? station.current_tile : World.world.tiles_list[value.Tile],
            ToLoad = new HashSet<Actor>(value.Load.Select(id => World.world.units.get(id)).Where(actor => actor != null)),
            ToUnload = new HashSet<Actor>(value.Unload.Select(id => World.world.units.get(id)).Where(actor => actor != null)),
            LoadTiles = RestoreTiles(value.LoadTiles), UnloadTiles = RestoreTiles(value.UnloadTiles)
        };
    }

    /// <summary>读取有效的上下车目的地。</summary>
    /// <param name="values">人物到地块编号的对应资料。</param>
    private static Dictionary<long, WorldTile> RestoreTiles(Dictionary<long, int> values)
        => values?.Where(entry => (uint)entry.Value < (uint)World.world.tiles_list.Length)
            .ToDictionary(entry => entry.Key, entry => World.world.tiles_list[entry.Value]);
}

/// <summary>当前铁路行程集合。</summary>
internal sealed class TrainTransportSaveData
{
    /// <summary>正在候车、停靠或行驶的行程。</summary>
    public List<TrainRideSaveData> Rides = new();
}

/// <summary>一趟铁路行程的完整稳定记录。</summary>
internal sealed class TrainRideSaveData
{
    /// <summary>列车人物编号。</summary>
    public long Train;
    /// <summary>调度阶段。</summary>
    public int Stage;
    /// <summary>候车阶段。</summary>
    public int State;
    /// <summary>准备期限。</summary>
    public double PrepareAt;
    /// <summary>普通停站期限。</summary>
    public double StopBaseAt;
    /// <summary>最长停站期限。</summary>
    public double StopHardAt;
    /// <summary>开始停站时间。</summary>
    public double StopStartedAt;
    /// <summary>已搭乘的人物。</summary>
    public long[] Carrying;
    /// <summary>当前行驶路径。</summary>
    public int[] Track;
    /// <summary>当前或刚离开的车站。</summary>
    public TrainStopSaveData CurrentStop;
    /// <summary>剩余停靠点。</summary>
    public List<TrainStopSaveData> Stops;
    /// <summary>是否允许空车。</summary>
    public bool AllowEmpty;
    /// <summary>是否为自动试运行。</summary>
    public bool Experimental;
    /// <summary>列车在轨道上的实际进度。</summary>
    public ActorComponents.TrainMotionSaveData Motion;
}

/// <summary>一个停靠点及计划上下车的旅客。</summary>
internal sealed class TrainStopSaveData
{
    /// <summary>车站建筑编号。</summary>
    public long Station;
    /// <summary>停靠地块。</summary>
    public int Tile;
    /// <summary>上车人物编号。</summary>
    public long[] Load;
    /// <summary>下车人物编号。</summary>
    public long[] Unload;
    /// <summary>上车前的目的地。</summary>
    public Dictionary<long, int> LoadTiles;
    /// <summary>下车后的目的地。</summary>
    public Dictionary<long, int> UnloadTiles;
}

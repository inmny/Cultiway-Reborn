using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Persistence;

namespace Cultiway.Content;

/// <summary>铁路连接与修复进度随世界保存，格子贴图由原版保存。</summary>
public partial class TrainTrackRepairSystem
{
    /// <summary>采集车站之间实际铺设过的路线，包括待修复路线。</summary>
    internal static TrainNetworkSaveData CaptureWorldSave() => new()
    {
        Links = Links.Values.Select(value => new TrainLinkSaveData
        {
            A = value.StationAId, B = value.StationBId, Tiles = value.Tiles.Select(tile => tile.tile_id).ToArray(),
            State = (int)value.Status, Failures = value.FailCount, RepairAt = value.NextRepairTime
        }).ToList()
    };

    /// <summary>独立恢复路线，再重建车站连通索引。</summary>
    /// <param name="data">待保存或恢复的资料。</param>
    /// <param name="report">本次读取的转换与跳过报告。</param>
    internal static void RestoreWorldSave(TrainNetworkSaveData data, SaveLoadReport report)
    {
        foreach (TrainLinkSaveData value in data.Links)
        {
            Building a = World.world.buildings.get(value.A);
            Building b = World.world.buildings.get(value.B);
            if (a == null || b == null || value.Tiles == null || value.Tiles.Length == 0 ||
                value.Tiles.Any(tile => (uint)tile >= (uint)World.world.tiles_list.Length) || value.State < 0 || value.State > 2)
            {
                report.Skip("铁路", value.A + "—" + value.B, "车站、路线或状态无效");
                continue;
            }
            RegisterLink(a, b, value.Tiles.Select(tile => World.world.tiles_list[tile]).ToList());
            var key = LinkKey.Create(value.A, value.B);
            LinkInfo info = Links[key];
            info.Status = (LinkStatus)value.State;
            info.FailCount = value.Failures;
            info.NextRepairTime = value.RepairAt;
            if (info.Status == LinkStatus.Pending) Pending.Add(key);
        }
        RebuildTrainConnectivity();
        _dirtyConnectivity = false;
    }
}

/// <summary>铁路网的长期连接。</summary>
internal sealed class TrainNetworkSaveData
{
    /// <summary>已建成或等待修复的线路。</summary>
    public List<TrainLinkSaveData> Links = new();
}

/// <summary>一条铁路实际经过的地块和修复进度。</summary>
internal sealed class TrainLinkSaveData
{
    /// <summary>起始车站。</summary>
    public long A;
    /// <summary>到达车站。</summary>
    public long B;
    /// <summary>按顺序经过的地块。</summary>
    public int[] Tiles;
    /// <summary>路线状态。</summary>
    public int State;
    /// <summary>已经失败的修复次数。</summary>
    public int Failures;
    /// <summary>下次允许修复的世界时间。</summary>
    public double RepairAt;
}

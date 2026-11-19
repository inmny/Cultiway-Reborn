using System.Collections.Generic;

namespace Cultiway.Content.Railways;

/// <summary>不保存游戏对象引用的列车与在途行程。</summary>
internal sealed class TrainSaveData
{
    /// <summary>有序车厢，火车头固定存在。</summary>
    public TrainCarKind[] Cars;
    /// <summary>长期车辆归属、停车和维修状态。</summary>
    public RailwayVehicleSaveData Vehicle;
    /// <summary>当前行程用途。</summary>
    public RailwayVehicleMode Purpose;
    /// <summary>当前行程是否遵守运兵许可。</summary>
    public bool Military;
    /// <summary>下一安全站结束本次载客。</summary>
    public bool InterruptAtNextStation;
    /// <summary>同国与军令批次。</summary>
    public string GroupKey;
    /// <summary>当前区间的有序格子编号。</summary>
    public int[] Track;
    /// <summary>当前轨道进度。</summary>
    public float Distance;
    /// <summary>站内等待区间的首次申请顺序；零表示未排队。</summary>
    public long QueueOrder;
    /// <summary>是否正在站内停靠。</summary>
    public bool AtStation;
    /// <summary>是否正在沿旧轨倒退回站。</summary>
    public bool Returning;
    /// <summary>当前或上一个车站编号。</summary>
    public long CurrentStation;
    /// <summary>剩余基础停靠时间。</summary>
    public float BaseRemaining;
    /// <summary>剩余最长停靠时间。</summary>
    public float HardRemaining;
    /// <summary>剩余站序和候车记录。</summary>
    public List<TrainStopSaveData> Stops = new();
}

/// <summary>一个停站的持久化上下客记录。</summary>
internal sealed class TrainStopSaveData
{
    /// <summary>建筑编号。</summary>
    public long Station;
    /// <summary>上下客点格子。</summary>
    public int Tile;
    /// <summary>尚未实际登车的人。</summary>
    public long[] ToLoad;
    /// <summary>应在本站下车的人。</summary>
    public long[] ToUnload;
}

/// <summary>一条由编号较小的端点保存的铁路连接。</summary>
internal sealed class TrainLinkSaveData
{
    /// <summary>原始路径起站。</summary>
    public long StationA;
    /// <summary>原始路径终站。</summary>
    public long StationB;
    /// <summary>有序轨道格子。</summary>
    public int[] Tiles;
    /// <summary>承担轨道费用的城市编号。</summary>
    public long Payer;
    /// <summary>当前施工或运营状态。</summary>
    public RailwayLinkState State;
    /// <summary>解除暂停后继续的状态。</summary>
    public RailwayLinkState ResumeState;
    /// <summary>暂停或受损原因。</summary>
    public string Reason;
    /// <summary>下一批施工起点。</summary>
    public int Cursor;
    /// <summary>本次工程累计新铺格数。</summary>
    public int Laid;
    /// <summary>已付的八格费用份数。</summary>
    public int Paid;
    /// <summary>预计总费用份数。</summary>
    public int Budget;
    /// <summary>连续维修失败次数。</summary>
    public int Failures;
    /// <summary>距离下一次维修的模拟秒数。</summary>
    public float RetryRemaining;
    /// <summary>是否由玩家手绘，受损后等待玩家或城市重新连接。</summary>
    public bool PlayerDrawn;
}

/// <summary>铁路工程与运营的明确状态。</summary>
internal enum RailwayLinkState
{
    /// <summary>新线分批施工。</summary>
    Construction,
    /// <summary>完整可运营。</summary>
    Normal,
    /// <summary>受损等待维修。</summary>
    Damaged,
    /// <summary>收费补轨或绕线施工。</summary>
    Repairing,
    /// <summary>条件不足，等待实际变化。</summary>
    Paused,
    /// <summary>端点失效或工程撤销。</summary>
    Revoked
}

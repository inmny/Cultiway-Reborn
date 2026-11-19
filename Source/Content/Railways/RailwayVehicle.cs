namespace Cultiway.Content.Railways;

/// <summary>一列长期保留的车辆及其归属和当前停靠位置。</summary>
internal sealed class RailwayVehicle
{
    /// <summary>代表整列车的原版车头。</summary>
    internal Actor Actor;
    /// <summary>归属车站；失去归属时为零。</summary>
    internal long Home;
    /// <summary>车辆所属国家，不随车站被占领而改变。</summary>
    internal long Country;
    /// <summary>归属城市。</summary>
    internal long City;
    /// <summary>实际停靠车站；区间运行时为零。</summary>
    internal long Station;
    /// <summary>当前运行或停放状态。</summary>
    internal RailwayVehicleMode Mode;
    /// <summary>逐次维修不足一点生命的累计量。</summary>
    internal float RepairFraction;
    /// <summary>下一次允许贸易的模拟时刻。</summary>
    internal double TradeAvailable;
    /// <summary>连续完成的优先任务数量。</summary>
    internal int PriorityStreak;
    /// <summary>当前行程目标，用于判断是否已有车去接客。</summary>
    internal long Destination;
    /// <summary>连续无法返站的开始时刻；负数表示返站正常。</summary>
    internal double StrandedSince = -1;
    /// <summary>交易标记和访问历史；金币只保存在原版背包。</summary>
    internal RailwayTradeSaveData Trade = new();
    /// <summary>实物补给的预留和交付记录，不另存一份实物库存。</summary>
    internal RailwayFreightSaveData Freight = new();
    /// <summary>真实车厢与承载组件。</summary>
    internal ActorComponents.Train Body => Actor.getActorComponent<ActorComponents.Train>();
}

/// <summary>车站可以展示和调度的车辆状态。</summary>
internal enum RailwayVehicleMode
{
    /// <summary>站内空闲。</summary>
    Idle,
    /// <summary>沿轨接客。</summary>
    Pickup,
    /// <summary>执行客运。</summary>
    Passenger,
    /// <summary>空车返站。</summary>
    Returning,
    /// <summary>归属站维修。</summary>
    Repairing,
    /// <summary>等待安全路线或重新归属。</summary>
    Stopped,
    /// <summary>执行贸易往返。</summary>
    Trade,
    /// <summary>执行同国实物补给。</summary>
    Freight
}

/// <summary>车辆随车头保存的归属和停放状态。</summary>
internal sealed class RailwayVehicleSaveData
{
    /// <summary>归属站编号。</summary>
    public long Home;
    /// <summary>原所属国家编号。</summary>
    public long Country;
    /// <summary>归属城市编号。</summary>
    public long City;
    /// <summary>当前停靠站编号。</summary>
    public long Station;
    /// <summary>车辆状态。</summary>
    public RailwayVehicleMode Mode;
    /// <summary>未满一点的维修累计量。</summary>
    public float RepairFraction;
    /// <summary>下一次贸易前还需等待的秒数。</summary>
    public float TradeRemaining;
    /// <summary>连续优先任务数。</summary>
    public int PriorityStreak;
    /// <summary>正在前往的最终车站。</summary>
    public long Destination;
    /// <summary>连续无法返站的累计模拟秒数，读档不重置。</summary>
    public float StrandedElapsed;
    /// <summary>本次交易动作与历史。</summary>
    public RailwayTradeSaveData Trade = new();
    /// <summary>实物补给的来源、目标和预留或尚未卸下的数量。</summary>
    public RailwayFreightSaveData Freight = new();
}

/// <summary>归属站的造车间隔与拆下的完整车厢。</summary>
internal sealed class RailwayDepotSaveData
{
    /// <summary>下一次新造车头前的剩余秒数。</summary>
    public float BuildRemaining;
    /// <summary>未挂接的载客车厢数。</summary>
    public int PassengerCars;
    /// <summary>未挂接的货运车厢数。</summary>
    public int CargoCars;
}

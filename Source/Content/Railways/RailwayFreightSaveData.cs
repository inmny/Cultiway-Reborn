using System.Collections.Generic;

namespace Cultiway.Content.Railways;

/// <summary>一车一份实物补给记录，实际货物只存在原版背包中。</summary>
internal sealed class RailwayFreightSaveData
{
    /// <summary>本次补给的进度。</summary>
    public RailwayFreightState State;
    /// <summary>装货车站。</summary>
    public long SourceStation;
    /// <summary>实际出货城市。</summary>
    public long SourceCity;
    /// <summary>当前收货车站。</summary>
    public long TargetStation;
    /// <summary>当前收货城市。</summary>
    public long TargetCity;
    /// <summary>任务所属国家，装卸时仍须核对。</summary>
    public long Country;
    /// <summary>装货前是预留量，装货后是背包中尚待卸下的份额，不用于生成货物。</summary>
    public Dictionary<string, int> Amounts = new();
    /// <summary>是否正在退还源城或送回同国归属站。</summary>
    public bool ReturningCargo;
    /// <summary>本次实际累计卸下的数量，包含退货。</summary>
    public int Unloaded;
    /// <summary>最近一次实际接收资源的城市。</summary>
    public long ReceivedCity;
    /// <summary>取消、改送或等待原因的本地化后缀。</summary>
    public string Reason = "";
    /// <summary>仍占用源城预留或目标补给缺口。</summary>
    internal bool Active => State is RailwayFreightState.Reserved or RailwayFreightState.Loaded;
}

/// <summary>实物补给的明确阶段。</summary>
internal enum RailwayFreightState
{
    /// <summary>尚未执行过实物补给。</summary>
    None,
    /// <summary>预留材料，等待实际到源站装车。</summary>
    Reserved,
    /// <summary>材料已扣出并装入背包。</summary>
    Loaded,
    /// <summary>本批货物全部实际入库。</summary>
    Delivered,
    /// <summary>未装货的任务已取消。</summary>
    Cancelled
}

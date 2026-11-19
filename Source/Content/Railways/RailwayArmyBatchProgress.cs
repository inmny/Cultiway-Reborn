using Newtonsoft.Json;

namespace Cultiway.Content.Railways;

/// <summary>一个真实军令批次的可查询人数，随原版军队保存。</summary>
internal sealed class RailwayArmyBatchProgress
{
    /// <summary>累计加入本批的人数。</summary>
    public int Total;
    /// <summary>尚未登车的人数。</summary>
    public int Waiting;
    /// <summary>实际在列车内的人数。</summary>
    public int Onboard;
    /// <summary>完成原定下车的人数。</summary>
    public int Completed;
    /// <summary>改令、阵亡或事故中断的人数。</summary>
    public int Cancelled;

    /// <summary>读取某个军令批次，不扫描军队成员。</summary>
    /// <param name="army">原版军队。</param>
    /// <param name="generation">命令批次编号。</param>
    internal static RailwayArmyBatchProgress Read(Army army, long generation)
    {
        army.data.get("cw.railway.batch." + generation, out string json, null);
        return json == null ? new RailwayArmyBatchProgress() : JsonConvert.DeserializeObject<RailwayArmyBatchProgress>(json);
    }

    /// <summary>一次人数变化完成后写回军队存档。</summary>
    /// <param name="army">原版军队。</param>
    /// <param name="generation">命令批次编号。</param>
    internal void Save(Army army, long generation)
        => army.data.set("cw.railway.batch." + generation, JsonConvert.SerializeObject(this));
}

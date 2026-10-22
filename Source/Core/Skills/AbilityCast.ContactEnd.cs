namespace Cultiway.Core.Skills;

/// <summary>预测中的结束交接不吞掉结束前仍然有效的最后接触路径。</summary>
public sealed partial class AbilityCast
{
    /// <summary>本轮预测刚交接、碎解或开始收束，真实确认前仍检查旧状态最后路径。</summary>
    /// <param name="node">生成部位，身体使用负一。</param>
    private bool FinalPathInPrediction(int node) => node >= 0 && nodes[node].Force == 0f && savedNodes[node].Force > 0f &&
        (nodes[node].Leaving && !savedNodes[node].Leaving || nodes[node].Recovering && !savedNodes[node].Recovering || nodes[node].EndSent && !savedNodes[node].EndSent &&
            (Plan.Nodes[node].RemainderReceivers.Length > 0 || Plan.Nodes[node].Fracture != null));

    /// <summary>预测发生结束时只用先前余力筛选候选；真实命中仍回退到接触时点结算。</summary>
    /// <param name="node">实际生成部位。</param>
    internal float ContactSweepForce(int node) => FinalPathInPrediction(node) ? savedNodes[node].Force : nodes[node].Force;

    /// <summary>结束时点本身属于后续状态，原主体只允许在它之前确认接触。</summary>
    /// <param name="node">实际生成部位或负一身体。</param>
    /// <param name="fraction">本轮候选接触的时间比例。</param>
    internal bool CanSweepAt(int node, float fraction) => !FinalPathInPrediction(node) || savedAge + (Age - savedAge) * fraction < Age;
}
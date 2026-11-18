using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>内部作用实例沿用同一身体、姿态及控制，只有实际作用和接触限额分段。</summary>
public sealed partial class AbilityCast
{
    /// <summary>最后开始的作用及预测副本；结果收势或中断后保持原实例。</summary>
    private int bodyStage, savedBodyStage;
    /// <summary>本次限额耗尽的作用及预测副本，不结束后续已声明窗口。</summary>
    private int exhaustedBodyStage = -1, savedExhaustedBodyStage = -1;
    /// <summary>当前身体的实际作用实例。</summary>
    public int BodyStage => bodyStage;

    /// <summary>预测起点的身体作用，用于保留边界之前的真实路径。</summary>
    internal int PreviousBodyStage => savedBodyStage;

    /// <summary>身体本身及明确依附其作用过程的生成部位使用相同结果与时段。</summary>
    /// <param name="node">实际部位，负一表示身体。</param>
    internal bool UsesBodyWindow(int node) => node < 0 || Plan.Nodes[node].BodyWindow;

    /// <summary>身体真正结束时收回明确依赖它的形体，其他离体主体继续原生命周期。</summary>
    /// <param name="interrupted">是否由身体中断而提前结束。</param>
    private void EndBodyWindows(bool interrupted = false)
    {
        for (int i = 0; i < nodes.Length; i++)
            if (Plan.Nodes[i].BodyWindow) EndNode(i, interrupted);
    }

    /// <summary>同名body组和身体在本段预测中是否切换了实际作用。</summary>
    /// <param name="node">实际部位。</param>
    internal bool ContactScopeChanged(int node) => bodyStage != savedBodyStage &&
        (node < 0 || Plan.Nodes[node].ContactGroup == Plan.Nodes.Length);

    /// <summary>把身体的真实窗口裁到本次预测路径，独立生成部位继续自己的时段。</summary>
    /// <param name="node">实际部位。</param><param name="stage">需要确认的作用实例。</param>
    /// <param name="from">可用路径起点。</param><param name="until">可用路径终点。</param>
    internal bool ContactInterval(int node, int stage, out float from, out float until)
    {
        from = 0f; until = 1f;
        if (!UsesBodyWindow(node)) return true;
        CompiledBodyStage window = Plan.Body.Stages[stage];
        if (window.From > Age || window.Until < savedAge) return false;
        if (window.From > savedAge) from = window.From == Age ? 1f : (window.From - savedAge) / (Age - savedAge);
        if (window.Until < Age) until = window.Until == savedAge ? 0f : (window.Until - savedAge) / (Age - savedAge);
        return true;
    }

    /// <summary>在同一世界时点切换实例，再由原身体曲线解算姿态。</summary>
    private void UpdateBodyStage()
    {
        if (Plan.Body == null || bodyInterrupted || bodyResponse.HasValue) return;
        int next = Plan.Body.StageAt(Age);
        if (next == bodyStage) return;
        bodyStage = next;
        bodyActionStages = 0;
        bodyContactAt = -1f;
        BodyContactRevision++;
        for (int i = 0; i < nodes.Length; i++)
            if (Plan.Nodes[i].BodyWindow)
            {
                nodes[i].ActionStages = 0;
                nodes[i].ActionInstance++;
            }
    }

    /// <summary>耗尽只停止本次身体作用，支撑、控制和连续姿态仍运行至实际结束。</summary>
    internal void ExhaustBodyStage()
    {
        exhaustedBodyStage = bodyStage;
        BodyContactRevision++;
    }
}

namespace Cultiway.Core.Skills;

/// <summary>对应的生效时段沿用真实部位的出现时刻和共同世界时间。</summary>
public sealed partial class AbilityCast
{
    /// <summary>清楚区分仍有物理形状和该形状可以传递作用。</summary>
    /// <param name="node">实际映像部位。</param>
    internal bool CorrespondenceActive(int node)
    {
        CompiledCorrespondence relation = Plan.Nodes[node].Correspondence;
        NodeState state = nodes[node];
        return state.Started && Age >= state.StartedAt + relation.ActiveFrom && Age < state.StartedAt + relation.ActiveUntil;
    }

    /// <summary>把映像传递窗口裁到共同预测段，开始包含，停止时点排除。</summary>
    /// <param name="node">实际映像部位。</param><param name="from">允许传递的最早进度。</param>
    /// <param name="until">停止传递的进度，超出本步时为正无穷。</param>
    internal bool CorrespondenceInterval(int node, out float from, out float until)
    {
        from = 0f;
        until = float.PositiveInfinity;
        CompiledCorrespondence relation = Plan.Nodes[node].Correspondence;
        NodeState state = nodes[node];
        float start = state.StartedAt + relation.ActiveFrom;
        float end = state.StartedAt + relation.ActiveUntil;
        if (!state.Started || Age < start || savedAge >= end) return false;
        if (start > savedAge) from = (start - savedAge) / (Age - savedAge);
        if (end <= Age) until = (end - savedAge) / (Age - savedAge);
        return from < until;
    }
}

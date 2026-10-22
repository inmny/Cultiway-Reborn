namespace Cultiway.Core.Skills;

/// <summary>单次施放内的实际材料变化，保持与姿态、作用区及预测恢复一致。</summary>
public sealed partial class AbilityCast
{
    /// <summary>读取当前实际材料及物态，不读取显示颜色。</summary>
    /// <param name="node">已经展开的生成部位。</param>
    public AbilityMatter Matter(int node) => nodes[node].Matter;

    /// <summary>读取受材料保持影响的实际形变进度。</summary>
    /// <param name="node">生成部位。</param>
    public float ShapeAge(int node) => nodes[node].ShapeAge;
    /// <summary>读取当前是否保持内部形状；整体移动和已脱离部件各自继续。</summary>
    /// <param name="node">生成部位。</param>
    public bool IsShapeHeld(int node) => nodes[node].ShapeHeld || nodes[node].Stopped;

    /// <summary>在确认的时点执行尚未发生的对应变化，不重置力、归属或接触预算。</summary>
    /// <param name="node">实际发生变化的部位。</param>
    /// <param name="trigger">本次已经确认的触发。</param>
    internal void ApplyMatterChanges(int node, ConstructionChangeTrigger trigger)
    {
        if (node < 0 || !nodes[node].Exists) return;
        CompiledMatterChange[] changes = Plan.Nodes[node].Changes;
        ref NodeState state = ref nodes[node];
        for (int i = 0; i < changes.Length; i++)
        {
            CompiledMatterChange change = changes[i];
            if ((state.MatterChangesDone & (1 << i)) != 0 || change.Trigger != trigger || NodeAge(node) < change.At) continue;
            state.MatterChangesDone |= (ushort)(1 << i);
            AbilityMatter previous = state.Matter;
            if (ReferenceEquals(previous, change.Matter)) continue;
            state.Matter = change.Matter;
            state.ShapeHeld = change.Matter.HoldsShape || previous.State == "liquid" && change.Matter.State == "solid";
            state.Revision++;
            SignalEvent(node, ConstructionChangeTrigger.MatterChanged, state.Transform.GetColumn(3));
            for (int child = node + 1; child < nodes.Length; child++)
                if (IsAttachedBelow(child, node)) nodes[child].Revision++;
        }
    }
}

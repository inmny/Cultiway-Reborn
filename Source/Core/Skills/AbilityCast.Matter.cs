namespace Cultiway.Core.Skills;

/// <summary>单次施放内的实际材料变化，保持与姿态、作用区及预测恢复一致。</summary>
public sealed partial class AbilityCast
{
    /// <summary>本体是否保留作者原有的固定伤害属性；附加伤害不走这条规则。</summary>
    /// <param name="node">实际发生接触的部位。</param><param name="impact">本次使用的完整作用。</param>
    internal ElementComposition? FixedDamageElement(int node, CompiledConstructionImpact impact) =>
        node >= 0 && Plan.Nodes[node].FollowMatter ? null : impact.Element;

    /// <summary>当前位置和两端显示共用实际材料选出的表面。</summary>
    /// <param name="node">实际显示部位。</param>
    private (string Surface, string MatterSurface) MatterAppearance(int node)
    {
        CompiledForm form = Plan.Nodes[node];
        AbilityMatter matter = nodes[node].Matter;
        return (form.Resource.AppearanceOverride(form.Surface, matter, form.FollowMatter),
            form.FollowMatter ? matter.EditedSurface : matter.Surface);
    }

    /// <summary>读取当前实际材料及物态，不读取显示颜色。</summary>
    /// <param name="node">已经展开的生成部位。</param>
    public AbilityMatter Matter(int node) => nodes[node].Matter;

    /// <summary>读取受材料保持影响的实际形变进度。</summary>
    /// <param name="node">生成部位。</param>
    public float ShapeAge(int node) => nodes[node].ShapeAge;
    /// <summary>读取当前是否保持内部形状；整体移动和已脱离部件各自继续。</summary>
    /// <param name="node">生成部位。</param>
    public bool IsShapeHeld(int node) => nodes[node].ShapeHeld || nodes[node].MotionStopped && nodes[node].GrowthStopped;

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
            ChangeMatter(node, change.Matter);
            if (change.WorldMatter != null) state.WorldMatterPending |= (ushort)(1 << i);
            NativeStarted(Plan.Nodes[node].MatterSteps?[i] ?? -1);
        }
    }

    /// <summary>明确改变同一主体的材料，真实变化才发布一次事件。</summary>
    /// <param name="node">实际部位。</param>
    /// <param name="matter">已经解析的完整材料及物态。</param>
    private void ChangeMatter(int node, AbilityMatter matter)
    {
        ref NodeState state = ref nodes[node]; AbilityMatter previous = state.Matter;
        if (ReferenceEquals(previous, matter)) return;
        state.Matter = matter;
        if (matter.ErosionFactor <= 0) InterruptErosion(node);
        if (!AbilityBurning.CanBurn(matter)) state.Burning.Extinguish();
        state.ShapeHeld = matter.HoldsShape ||
                          matter.State == "solid" && (previous.State == "liquid" || state.ShapeHeld);
        if (state.ShapeHeld && flows?[node]?.Started == true)
        {
            flows[node].StopMotion();
            state.Velocity = flows[node].Velocity;
        }

        state.Revision++;
        SignalEvent(node, ConstructionChangeTrigger.MatterChanged, state.Transform.GetColumn(3));
        for (int child = node + 1; child < nodes.Length; child++)
            if (IsAttachedBelow(child, node)) nodes[child].Revision++;
    }
}

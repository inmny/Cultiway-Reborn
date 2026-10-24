using System;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>单次命名操作的真实状态，不把中断算成正常完成。</summary>
public enum ConstructionStepState
{
    /// <summary>输入、部位或时点尚未就绪。</summary>
    Waiting = 0,
    /// <summary>已经实际开始。</summary>
    Running = 1,
    /// <summary>已经实际完成。</summary>
    Completed = 2,
    /// <summary>目标失效、过程被替换或施放中断。</summary>
    Interrupted = 3
}

/// <summary>已解析输出驱动真实操作；没有步骤的旧构造不分配额外运行状态。</summary>
public sealed partial class AbilityCast
{
    /// <summary>本次步骤及预测副本，只含数值和不可变材料引用。</summary>
    private readonly StepState[] stepStates, savedStepStates;

    /// <summary>步骤事实随共同预测和回退复制。</summary>
    private struct StepState
    {
        /// <summary>真实阶段。</summary>
        internal ConstructionStepState State;
        /// <summary>实际开始与完成时的来源事实。</summary>
        internal ConstructionOutputValue Started, Completed;
    }

    /// <summary>开发与界面按稳定编号读取实际状态，计算热路径直接使用索引。</summary>
    /// <param name="id">能力内的步骤编号。</param>
    public ConstructionStepState StepStateOf(string id)
    {
        int index = Array.FindIndex(Plan.Steps, step => step.Id == id);
        if (index < 0) throw new ArgumentException("步骤不存在：" + id, nameof(id));
        if (cancelled && stepStates[index].State is ConstructionStepState.Waiting or ConstructionStepState.Running) return ConstructionStepState.Interrupted;
        return stepStates[index].State;
    }

    /// <summary>只返回已经发生的真实输出，不根据预估历时制造事实。</summary>
    /// <param name="id">步骤编号。</param><param name="output">约定的输出名。</param><param name="value">实际来源与确认时刻。</param>
    /// <param name="stage">所需阶段。</param>
    public bool TryGetOutput(string id, string output, out ConstructionOutputValue value, ConstructionOutputStage stage = ConstructionOutputStage.Auto)
    {
        value = default;
        int index = Array.FindIndex(Plan.Steps, step => step.Id == id);
        if (index < 0) return false;
        var port = Plan.Steps[index].Contract.Outputs.FirstOrDefault(p => p.Name == output);
        if (port == null) return false;
        bool completed = stage == ConstructionOutputStage.Completed || stage == ConstructionOutputStage.Auto && port.Kind is ConstructionValueKind.Process or ConstructionValueKind.Result;
        ref StepState state = ref stepStates[index];
        if (completed ? state.State != ConstructionStepState.Completed : state.State == ConstructionStepState.Waiting) return false;
        if (!completed && state.State == ConstructionStepState.Interrupted && state.Started.Matter == null) return false;
        ConstructionOutputValue fact = completed ? state.Completed : state.Started;
        value = new ConstructionOutputValue(fact.Node, fact.Parent, port.Kind, fact.At, fact.Position, fact.Matter, fact.Authority);
        return true;
    }

    /// <summary>检查实际身份及阶段，失去的部位或连接不能凭旧输出复活。</summary>
    /// <param name="step">已解析的步骤索引。</param>
    private bool StepInputsReady(int step)
    {
        var plan = Plan.Steps[step];
        foreach (var input in plan.Inputs)
        {
            if (input.Step >= 0)
            {
                var source = stepStates[input.Step];
                if (input.Completed ? source.State != ConstructionStepState.Completed : source.Started.Matter == null) return false;
            }
            if (input.Kind is ConstructionValueKind.Part or ConstructionValueKind.Structure or ConstructionValueKind.Relation)
            {
                if (!nodes[input.Node].Exists || !nodes[input.Node].Started) return false;
                if (input.Connection && nodes[input.Node].Free) return false;
            }
        }
        if (!nodes[plan.Node].Exists || !nodes[plan.Node].Started) return false;
        if (plan.Action.Kind is ConstructionOperationKind.Gather or ConstructionOperationKind.Detach && nodes[plan.Node].Free) return false;
        return true;
    }

    /// <summary>实际执行后记录开始；即时操作同时产生确认结果。</summary>
    /// <param name="operation">已经应用的共同操作。</param>
    private void StepApplied(CompiledEventOperation operation)
    {
        if (operation.Step < 0) return;
        ref StepState state = ref stepStates[operation.Step];
        state.Started = StepFact(operation.Node); state.State = ConstructionStepState.Running;
        if (operation.Kind is ConstructionOperationKind.Detach or ConstructionOperationKind.Stop or ConstructionOperationKind.ConvertMatter)
        { state.Completed = state.Started; state.State = ConstructionStepState.Completed; }
    }

    /// <summary>复制当前位置、材料、父关系与真实控制归属。</summary>
    /// <param name="node">实际部位。</param>
    private ConstructionOutputValue StepFact(int node) => new(node, nodes[node].Free ? -1 : Plan.Nodes[node].Parent, ConstructionValueKind.Part,
        Age, nodes[node].Transform.GetColumn(3), nodes[node].Matter, nodes[node].Authority);

    /// <summary>先确认已有过程，再执行新就绪输入；每个状态单向改变，队列有界。</summary>
    private void UpdateSteps()
    {
        if (stepStates.Length == 0 || cancelled) return;
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < Plan.Steps.Length; i++)
            {
                var plan = Plan.Steps[i]; ref StepState step = ref stepStates[i]; var action = plan.Action; ref NodeState node = ref nodes[plan.Node];
                if (step.State == ConstructionStepState.Running)
                {
                    bool completed = false, interrupted = !node.Exists;
                    switch (action.Kind)
                    {
                        case ConstructionOperationKind.Grow:
                            completed = ReferenceEquals(node.GrowthOperation, action) && node.GrowthAge >= action.Seconds;
                            interrupted |= !ReferenceEquals(node.GrowthOperation, action) || node.GrowthStopped;
                            break;
                        case ConstructionOperationKind.Animate:
                            completed = ReferenceEquals(node.Motion, action) && !action.Motion.Loop && node.MotionAge >= action.Motion.Duration;
                            interrupted |= !ReferenceEquals(node.Motion, action) || node.MotionStopped;
                            break;
                        case ConstructionOperationKind.Gather:
                            completed = ReferenceEquals(gathers[plan.Node].Process, action.Gather) && !node.Free && Age >= gathers[plan.Node].StartedAt + action.Gather.Seconds;
                            interrupted |= node.Free || !ReferenceEquals(gathers[plan.Node].Process, action.Gather);
                            break;
                        case ConstructionOperationKind.Fly:
                            completed = ReferenceEquals(node.Flight, action.Flight) && node.MovementStopped;
                            interrupted |= !ReferenceEquals(node.Flight, action.Flight);
                            break;
                        case ConstructionOperationKind.Spin:
                            completed = ReferenceEquals(node.SpinOperation, action) && node.Spin.sqrMagnitude == 0;
                            interrupted |= !ReferenceEquals(node.SpinOperation, action);
                            break;
                    }
                    if (completed && node.Exists) { step.Completed = StepFact(plan.Node); step.State = ConstructionStepState.Completed; changed = true; }
                    else if (interrupted) { step.State = ConstructionStepState.Interrupted; changed = true; }
                }
                if (step.State != ConstructionStepState.Waiting) continue;
                if (!node.Exists || operations[action.Index].Done) { step.State = ConstructionStepState.Interrupted; changed = true; continue; }
                bool failed = false;
                foreach (var input in plan.Inputs)
                {
                    if (input.Step >= 0 && stepStates[input.Step].State == ConstructionStepState.Interrupted &&
                        (input.Completed || stepStates[input.Step].Started.Matter == null)) { failed = true; break; }
                    if (input.Connection && nodes[input.Node].Free && (input.Step < 0 || stepStates[input.Step].Started.Matter != null)) { failed = true; break; }
                }
                if (failed) { step.State = ConstructionStepState.Interrupted; changed = true; continue; }
                if (NodeAge(plan.Node) < action.At || !StepInputsReady(i)) continue;
                if (action.When == null) { operations[action.Index].Received = true; operations[action.Index].Due = Age; }
                if (!operations[action.Index].Received || operations[action.Index].Due > Age) continue;
                ApplyOperation(action.Index); changed = true;
            }
        } while (changed);
    }
}

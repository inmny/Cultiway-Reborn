using System;
using System.Linq;
using System.Collections.Generic;

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
    /// <summary>真实碎片出生事实及预测副本，按实际部位索引保存。</summary>
    private readonly ConstructionOutputValue[] stepParts, savedStepParts;

    /// <summary>步骤事实随共同预测和回退复制。</summary>
    private struct StepState
    {
        /// <summary>真实阶段。</summary>
        internal ConstructionStepState State;
        /// <summary>实际开始与完成时的来源事实。</summary>
        internal ConstructionOutputValue Started, Completed;
        /// <summary>已确认的世界事实链及结束状态。</summary>
        internal ConstructionWorldReceipt Receipts;
        /// <summary>实际接受的波次或回执序号。</summary>
        internal int Confirmations;
        /// <summary>世界维护者已确认完整处理。</summary>
        internal bool WorldComplete;
        /// <summary>世界正在确认本次请求，期间来源结束不抢先抹掉真实结果。</summary>
        internal bool WorldPending;
    }

    /// <summary>开发与界面按稳定编号读取实际状态，计算热路径直接使用索引。</summary>
    /// <param name="id">能力内的步骤编号。</param>
    /// <param name="node">指定实际部位，负一读取整组状态。</param>
    public ConstructionStepState StepStateOf(string id, int node = -1)
    {
        int found = 0, waiting = 0, running = 0, completed = 0;
        for (int i = 0; i < Plan.Steps.Length; i++)
        {
            if (Plan.Steps[i].Id == null || Plan.Steps[i].Id != id || node >= 0 && Plan.Steps[i].Node != node) continue;
            found++; var state = stepStates[i].State;
            if (state == ConstructionStepState.Waiting) waiting++;
            if (state == ConstructionStepState.Running) running++;
            if (state == ConstructionStepState.Completed) completed++;
        }
        if (found == 0) throw new ArgumentException("步骤不存在：" + id, nameof(id));
        if (completed == found) return ConstructionStepState.Completed;
        if (cancelled) return ConstructionStepState.Interrupted;
        if (waiting == found) return ConstructionStepState.Waiting;
        return running > 0 || waiting > 0 ? ConstructionStepState.Running : ConstructionStepState.Interrupted;
    }

    /// <summary>只返回已经发生的真实输出，不根据预估历时制造事实。</summary>
    /// <param name="id">步骤编号。</param><param name="output">约定的输出名。</param><param name="value">实际来源与确认时刻。</param>
    /// <param name="stage">所需阶段。</param>
    public bool TryGetOutput(string id, string output, out ConstructionOutputValue value, ConstructionOutputStage stage = ConstructionOutputStage.Auto)
    {
        value = default;
        int index = Array.FindIndex(Plan.Steps, step => step.Id != null && step.Id == id);
        if (index < 0) return false;
        if (Plan.Steps.Count(step => step.Id == id) != 1) return false;
        var port = Plan.Steps[index].Contract.Outputs.FirstOrDefault(p => p.Name == output);
        if (port == null || port.Collection) return false;
        return ReadStepOutput(index, port, stage, out value);
    }

    /// <summary>把全部已确认输出追加到调用方复用的集合，不隐式只返回第一项。</summary>
    /// <param name="id">原命名步骤。</param><param name="output">输出端口。</param><param name="values">调用方复用的结果集合。</param><param name="stage">所需真实阶段。</param>
    public int AppendStepOutputs(string id, string output, List<ConstructionOutputValue> values, ConstructionOutputStage stage = ConstructionOutputStage.Auto)
    {
        int before = values.Count;
        for (int i = 0; i < Plan.Steps.Length; i++)
        {
            if (Plan.Steps[i].Id != id) continue;
            if (Plan.Steps[i].Id != null) AppendOutput(i, output, values, stage);
        }
        return values.Count - before;
    }

    /// <summary>两种查询共用同一组实际输出事实。</summary>
    /// <param name="index">实际执行实例。</param><param name="output">输出端口。</param><param name="values">复用集合。</param><param name="stage">真实阶段。</param>
    private void AppendOutput(int index, string output, List<ConstructionOutputValue> values, ConstructionOutputStage stage)
    {
        var port = Plan.Steps[index].Contract.Outputs.FirstOrDefault(p => p.Name == output);
        if (port == null || !ReadStepOutput(index, port, stage, out var value)) return;
        if (!port.Collection) { values.Add(value); return; }
        foreach (int piece in Plan.Steps[index].Action.Fracture.Pieces)
            if (stepParts[piece].Matter != null) values.Add(stepParts[piece]);
    }

    /// <summary>取得一个已执行实例的阶段事实，公共集合和标量查询共用。</summary>
    /// <param name="index">真实执行实例。</param><param name="port">已解析输出。</param><param name="stage">所需阶段。</param><param name="value">实际事实。</param>
    private bool ReadStepOutput(int index, ConstructionOperationPort port, ConstructionOutputStage stage, out ConstructionOutputValue value)
    {
        value = default;
        var action = Plan.Steps[index].Action;
        bool world = port.Kind == ConstructionValueKind.Material && (action.Kind == ConstructionOperationKind.Leave || action.WorldMatter != null);
        bool completed = world || stage == ConstructionOutputStage.Completed || stage == ConstructionOutputStage.Auto && (port.Collection || port.Kind is ConstructionValueKind.Process or ConstructionValueKind.Result);
        ref StepState state = ref stepStates[index];
        if (completed ? state.State != ConstructionStepState.Completed : state.State == ConstructionStepState.Waiting) return false;
        if (!completed && state.State == ConstructionStepState.Interrupted && state.Started.Matter == null) return false;
        ConstructionOutputValue fact = completed ? state.Completed : state.Started;
        value = new ConstructionOutputValue(fact.Node, fact.Parent, port.Kind, fact.At, fact.Position, fact.Matter, fact.Authority,
            world ? fact.Receipt.Volume : fact.Volume, fact.Force, world ? fact.Receipt.Resource : fact.Resource, fact.Receipt);
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
        if (operation.Immediate)
        { state.Completed = state.Started; state.State = ConstructionStepState.Completed; }
    }

    /// <summary>复制当前位置、材料、父关系与真实控制归属。</summary>
    /// <param name="node">实际部位。</param>
    /// <param name="receipt">已经完成的世界确认。</param>
    private ConstructionOutputValue StepFact(int node, ConstructionWorldReceipt receipt = null) => new(node, nodes[node].Free ? -1 : Plan.Nodes[node].Parent, ConstructionValueKind.Part,
        Age, nodes[node].Transform.GetColumn(3), nodes[node].Matter, nodes[node].Authority, nodes[node].Volume, nodes[node].Force, nodes[node].Resource, receipt);

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
                    ConstructionOperationProgress progress = action.Implementation.Progress(this, action, plan.Native);
                    bool completed = step.WorldComplete || progress.Completed, interrupted = !node.Exists || progress.Interrupted;
                    if (completed && (node.Exists || action.Fracture != null || action.Flow != null || step.WorldComplete))
                    {
                        step.Completed = StepFact(plan.Node, step.Receipts); step.State = ConstructionStepState.Completed; changed = true;
                        if (action.Fracture != null)
                            foreach (int piece in action.Fracture.Pieces) stepParts[piece] = StepFact(piece);
                    }
                    else if (interrupted && !step.WorldPending && !(action.Recovery != null && node.RecoveryProgress >= 1) && !(action.Kind == ConstructionOperationKind.Leave && node.Leaving))
                    { step.State = ConstructionStepState.Interrupted; changed = true; }
                }
                if (step.State != ConstructionStepState.Waiting) continue;
                if (plan.Native)
                {
                    if (!node.Exists || NativeUnavailable(plan)) { step.State = ConstructionStepState.Interrupted; changed = true; }
                    continue;
                }
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
                if (action.When == null) ReceiveOperationAt(action.Index, Age);
                if (!operations[action.Index].Received || operations[action.Index].Due > Age) continue;
                ApplyOperation(action.Index); changed = true;
            }
        } while (changed);
    }
}

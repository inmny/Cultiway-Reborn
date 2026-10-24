using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>原部位操作只在实际执行处登记输出，不再执行一遍。</summary>
public sealed partial class AbilityCast
{
    /// <summary>工具按部位和原操作名称查询真实输出。</summary>
    /// <param name="path">展开部位路径。</param><param name="operation">原操作名称。</param><param name="output">输出端口。</param>
    /// <param name="value">已发生的事实。</param><param name="stage">所需阶段。</param>
    public bool TryGetOperationOutput(string path, string operation, string output, out ConstructionOutputValue value, ConstructionOutputStage stage = ConstructionOutputStage.Auto)
    {
        value = default; int index = OperationStep(path, operation);
        if (index < 0) return false;
        var port = Plan.Steps[index].Contract.Outputs.FirstOrDefault(item => item.Name == output);
        return port != null && !port.Collection && ReadStepOutput(index, port, stage, out value);
    }

    /// <summary>查询一个原操作的实际状态。</summary>
    /// <param name="path">展开部位路径。</param><param name="operation">原操作名称。</param>
    public ConstructionStepState OperationStateOf(string path, string operation)
    {
        int index = OperationStep(path, operation);
        if (index < 0) throw new ArgumentException("不存在已命名的原部位操作：" + path + "/" + operation);
        var state = stepStates[index].State;
        return cancelled && state != ConstructionStepState.Completed ? ConstructionStepState.Interrupted : state;
    }

    /// <summary>取得原操作的完整集合，不偷偷返回第一件。</summary>
    /// <param name="path">展开部位路径。</param><param name="operation">原操作名称。</param><param name="output">输出端口。</param>
    /// <param name="values">调用方复用的集合。</param><param name="stage">所需阶段。</param>
    public int AppendOperationOutputs(string path, string operation, string output, List<ConstructionOutputValue> values, ConstructionOutputStage stage = ConstructionOutputStage.Auto)
    {
        int before = values.Count, index = OperationStep(path, operation);
        if (index >= 0) AppendOutput(index, output, values, stage);
        return values.Count - before;
    }

    /// <summary>取得原操作按时间顺序确认的全部世界回执。</summary>
    /// <param name="path">展开部位路径。</param><param name="operation">原操作名称。</param><param name="values">复用的回执集合。</param>
    public int AppendOperationReceipts(string path, string operation, List<ConstructionWorldReceipt> values)
    {
        int before = values.Count, index = OperationStep(path, operation);
        if (index < 0) return 0;
        for (var receipt = stepStates[index].Receipts; receipt != null; receipt = receipt.Previous) values.Add(receipt);
        values.Reverse(before, values.Count - before); return values.Count - before;
    }

    /// <summary>名称只在外部查询时解析，实际推进使用编译后的索引。</summary>
    /// <param name="path">实际部位。</param><param name="operation">原操作名称。</param>
    private int OperationStep(string path, string operation) => Array.FindIndex(Plan.Steps,
        step => step.OperationId != null && step.OperationId == operation && Plan.Nodes[step.Node].Path == path);

    /// <summary>取得一种原定时操作的输出索引，未命名时为负一。</summary>
    /// <param name="node">实际部位。</param><param name="kind">原操作种类。</param>
    private int NativeStep(int node, ConstructionOperationKind kind) => Plan.Nodes[node].NativeSteps?[(int)kind] ?? -1;

    /// <summary>原执行入口已经接受操作后登记一次，记录与共同步骤完全相同。</summary>
    /// <param name="node">实际部位。</param><param name="kind">原定时操作。</param>
    private void NativeStarted(int node, ConstructionOperationKind kind) => NativeStarted(NativeStep(node, kind));

    /// <summary>改质可有多个原操作，因此直接使用逐项索引。</summary>
    /// <param name="index">实际步骤，负一表示没有命名。</param>
    private void NativeStarted(int index)
    {
        if (index < 0 || cancelled || stepStates[index].State != ConstructionStepState.Waiting) return;
        var action = Plan.Steps[index].Action;
        if (action.Recovery != null) nodes[action.Node].RecoveryOperation = action;
        if (action.Kind == ConstructionOperationKind.Leave) nodes[action.Node].LeaveOperation = action;
        StepApplied(action);
    }

    /// <summary>初始姿态建好后记录尺寸、旋转、动作及布局，避免把默认零矩阵当作出生位置。</summary>
    /// <param name="node">本轮已经采样的部位。</param><param name="age">该部位的真实年龄。</param>
    private void ObserveNativePose(int node, float age)
    {
        var form = Plan.Nodes[node];
        ref NodeState state = ref nodes[node];
        if (!state.GrowthChanged && !state.GrowthStopped && age >= form.Growth.x) NativeStarted(node, ConstructionOperationKind.Grow);
        if (state.SpinOperation == null && age >= form.SpinAt) NativeStarted(node, ConstructionOperationKind.Spin);
        if (state.Motion == null && !state.MotionStopped && state.MotionAge >= 0) NativeStarted(node, ConstructionOperationKind.Animate);
        if (state.ScheduledDetachDone) NativeStarted(node, ConstructionOperationKind.Detach);
        if (state.ScheduledFlightDone && ReferenceEquals(state.Flight, form.Flight)) NativeStarted(node, ConstructionOperationKind.Fly);
        if (form.Gather != null && gathers[node].InitialStarted && ReferenceEquals(gathers[node].Process, form.Gather)) NativeStarted(node, ConstructionOperationKind.Gather);
    }

    /// <summary>原过程在实际开始前已经被永久替换或失去连接时，不无限等待。</summary>
    /// <param name="step">只观察的原操作。</param>
    private bool NativeUnavailable(CompiledConstructionStep step)
    {
        ref NodeState state = ref nodes[step.Node];
        if (state.Recovering || state.Leaving) return true;
        return step.Action.Kind switch
        {
            ConstructionOperationKind.Grow => state.GrowthChanged || state.GrowthStopped,
            ConstructionOperationKind.Spin => state.SpinOperation != null,
            ConstructionOperationKind.Animate => state.Motion != null || state.MotionStopped,
            ConstructionOperationKind.Detach => state.Free && !state.ScheduledDetachDone,
            ConstructionOperationKind.Gather => state.Free,
            _ => false
        };
    }
}

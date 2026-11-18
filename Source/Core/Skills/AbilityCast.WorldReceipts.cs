using System;
using System.Collections.Generic;

namespace Cultiway.Core.Skills;

/// <summary>世界回执只在正式确认后进入步骤，不从预测进度制造入账或命中。</summary>
public sealed partial class AbilityCast
{
    /// <summary>步骤已在数值推进中就绪，等主线程确认实际效果。</summary>
    /// <param name="index">实际步骤。</param>
    internal bool AwaitingStepWorld(int index) => !cancelled && stepStates[index].State == ConstructionStepState.Running &&
        stepStates[index].WorldPending;

    /// <summary>当前条件未满足，依赖此实际结果的步骤也不能完成。</summary>
    /// <param name="index">已经尝试的步骤。</param>
    internal void RejectStepWorld(int index)
    {
        stepStates[index].WorldPending = false;
        stepStates[index].State = ConstructionStepState.Interrupted;
        UpdateSteps();
    }

    /// <summary>世界已接受独立侵蚀，后续按真实结果等待，来源结束仍可中断步骤。</summary>
    /// <param name="index">实际步骤，未命名时为负一。</param>
    internal void AwaitWorldErosion(int index)
    {
        if (index < 0 || cancelled) return;
        stepStates[index].WorldPending = false;
        UpdateSteps();
    }

    /// <summary>保留已发生的部分结果，再让依赖完成的后续停止。</summary>
    /// <param name="index">真实步骤。</param><param name="receipt">实际世界结果。</param>
    internal void InterruptStepWorld(int index, ConstructionWorldReceipt receipt)
    {
        ConfirmStepWorld(index, receipt, false);
        if (index < 0 || cancelled) return;
        stepStates[index].WorldPending = false;
        if (stepStates[index].State == ConstructionStepState.Running) stepStates[index].State = ConstructionStepState.Interrupted;
        UpdateSteps();
    }
    /// <summary>读取明确输入的整份材料身份；返回真且集合为空时，后端仍须保持空选择。</summary>
    /// <param name="step">当前步骤，负一表示未绑定。</param><param name="output">本次提交复用的编号集合。</param>
    internal bool ReadWorldMaterialInputs(int step, List<long> output)
    {
        output.Clear();
        if (step < 0) return false;
        bool bound = false;
        foreach (var input in Plan.Steps[step].Inputs)
        {
            if (!input.WorldMaterials) continue;
            bound = true;
            for (var receipt = stepStates[input.Step].Receipts; receipt != null; receipt = receipt.Previous)
                foreach (long id in receipt.MaterialIdentities)
                    if (!output.Contains(id)) output.Add(id);
        }
        return bound;
    }

    /// <summary>实际收束沿用启动它的同一世界材料输入。</summary>
    /// <param name="node">正在收束的部位。</param><param name="output">本次提交复用的编号集合。</param>
    internal bool ReadRecoveryMaterials(int node, List<long> output) => ReadWorldMaterialInputs(nodes[node].RecoveryOperation?.Step ?? -1, output);

    /// <summary>按原步骤查询全部已确认回执，调用方复用结果集合。</summary>
    /// <param name="id">命名步骤。</param><param name="output">调用方的回执集合。</param>
    public int AppendStepReceipts(string id, List<ConstructionWorldReceipt> output)
    {
        int before = output.Count;
        for (int i = 0; i < Plan.Steps.Length; i++)
        {
            if (Plan.Steps[i].Id == null || Plan.Steps[i].Id != id) continue;
            int start = output.Count;
            for (var receipt = stepStates[i].Receipts; receipt != null; receipt = receipt.Previous) output.Add(receipt);
            output.Reverse(start, output.Count - start);
        }
        return output.Count - before;
    }

    /// <summary>确认一份实际结果，导电按准确波序号接收，重复确认不再次放行。</summary>
    /// <param name="index">编译后的真实步骤。</param><param name="receipt">实际提交结果。</param><param name="complete">此步骤的全部提交已经完成。</param>
    internal void ConfirmStepWorld(int index, ConstructionWorldReceipt receipt, bool complete = true)
    {
        if (index < 0 || cancelled) return;
        ref StepState state = ref stepStates[index];
        if (state.State != ConstructionStepState.Running || receipt.Sequence != state.Confirmations + 1) return;
        int node = Plan.Steps[index].Node;
        state.Receipts = new ConstructionWorldReceipt(receipt, state.Receipts, node); state.Confirmations++;
        state.WorldPending = false;
        state.WorldComplete = complete || receipt.Kind == ConstructionWorldResultKind.Conduction && (!nodes[node].Exists || nodes[node].Force <= 0);
        UpdateSteps();
    }

    /// <summary>将真实总请求及入账按未确认份额顺序分配，不按各片分别舍入。</summary>
    /// <param name="requested">原支付通道本次处理的总请求。</param><param name="granted">实际总入账。</param><param name="clock">确认时点。</param>
    internal void ConfirmStepRecovery(double requested, double granted, double clock)
    {
        if (cancelled || !Plan.HasStepRecovery) return;
        // 先分配正式请求，再分配实际入账，最后发布各部位新增的确认事实。
        for (int node = 0; node < nodes.Length; node++)
        {
            ref NodeState state = ref nodes[node];
            double request = Math.Min(requested, Math.Max(0, state.RecoveryEligible - state.RecoveryRequested));
            state.RecoveryRequested += request; requested -= request;
        }
        for (int node = 0; node < nodes.Length; node++)
        {
            ref NodeState state = ref nodes[node];
            double credit = Math.Min(granted, Math.Max(0, state.RecoveryRequested - state.RecoveryGranted));
            state.RecoveryGranted += credit; granted -= credit;
        }
        for (int node = 0; node < nodes.Length; node++)
        {
            ref NodeState state = ref nodes[node]; var operation = state.RecoveryOperation;
            if (operation == null || operation.Step < 0) continue;
            ref StepState step = ref stepStates[operation.Step];
            if (step.State != ConstructionStepState.Running || step.WorldComplete) continue;
            double request = state.RecoveryRequested - state.ReportedRecoveryRequest, credit = state.RecoveryGranted - state.ReportedRecoveryGranted,
                volume = state.RecoveryConverted - state.ReportedRecoveryVolume;
            bool complete = state.RecoveryProgress >= 1;
            if (request == 0 && credit == 0 && volume == 0 && !complete) continue;
            state.ReportedRecoveryRequest = state.RecoveryRequested; state.ReportedRecoveryGranted = state.RecoveryGranted; state.ReportedRecoveryVolume = state.RecoveryConverted;
            ConfirmStepWorld(operation.Step, new ConstructionWorldReceipt(ConstructionWorldResultKind.Recovery, node, clock, step.Confirmations + 1,
                volume: volume, requested: request, granted: credit), complete);
        }
    }
}

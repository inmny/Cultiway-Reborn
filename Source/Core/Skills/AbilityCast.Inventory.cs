using System.Collections.Generic;

namespace Cultiway.Core.Skills;

/// <summary>库存操作只提出可回退的请求，真实物件由主线程在共同确认时处理。</summary>
public sealed partial class AbilityCast
{
    /// <summary>取出已到时或收到真实事件的请求，原顺序决定同刻使用次序。</summary>
    /// <param name="output">调用方复用的请求集合。</param>
    internal void TakeInventoryRequests(List<CompiledEventOperation> output)
    {
        output.Clear();
        if (cancelled) return;
        foreach (var action in Plan.InventoryOperations)
        {
            if (!operations[action.Index].WorldRequested) continue;
            operations[action.Index].WorldRequested = false;
            output.Add(action);
            if (action.Step >= 0) stepStates[action.Step].WorldPending = true;
        }
    }

    /// <summary>物料不足或借用失效使该步骤中断，后续完成依赖不会被放行。</summary>
    /// <param name="action">实际消耗操作。</param>
    internal void RejectInventory(CompiledEventOperation action)
    {
        if (action.Step < 0) return;
        stepStates[action.Step].WorldPending = false;
        stepStates[action.Step].State = ConstructionStepState.Interrupted;
        UpdateSteps();
    }
}

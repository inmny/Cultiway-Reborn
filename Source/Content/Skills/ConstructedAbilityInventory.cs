using System.Collections.Generic;
using Cultiway.Core.Skills;

namespace Cultiway.Content.Skills;

/// <summary>库存消耗与材料转换共用主线程确认窗口，工作线程不接触物品。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>同次确认复用的真实请求。</summary>
    private readonly List<CompiledEventOperation> inventoryRequests = new();

    /// <summary>每次请求只能消耗本施放已经借到的材料，完成输出来自实际回执。</summary>
    /// <param name="running">本次正式施放。</param><param name="clock">确认世界时间。</param>
    private static void ConfirmInventory(RunningCast running, double clock)
    {
        if (running.Cast.Plan.InventoryOperations.Length == 0) return;
        running.Cast.TakeInventoryRequests(simulation.inventoryRequests);
        foreach (var action in simulation.inventoryRequests)
        {
            if (running.Inventory == null || !running.Inventory.TryConsume(action.Index, (CompiledInventoryUse)action.Parameters, out var identities))
                running.Cast.RejectInventory(action);
            else
                running.Cast.ConfirmStepWorld(action.Step, new ConstructionWorldReceipt(ConstructionWorldResultKind.Inventory,
                    action.Node, clock, items: identities));
        }
    }
}

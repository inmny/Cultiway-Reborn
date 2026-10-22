using System.Collections.Generic;

namespace Cultiway.Core.Skills;

/// <summary>世界材料转换请求属于可回退的本次运行状态，真实世界只由主线程确认。</summary>
public sealed partial class AbilityCast
{
    /// <summary>领取已真正执行的转换，不以材料颜色变化推断事件。</summary>
    /// <param name="output">本轮复用请求缓冲。</param>
    internal void TakeWorldMatterRequests(List<AbilityWorldMatterRequest> output)
    {
        output.Clear();
        if (cancelled) return;
        for (int node = 0; node < nodes.Length; node++)
        {
            ushort pending = nodes[node].WorldMatterPending;
            nodes[node].WorldMatterPending = 0;
            if (nodes[node].ConductionPending)
            {
                nodes[node].ConductionPending = false;
                output.Add(new AbilityWorldMatterRequest(node, Plan.Nodes[node].Conduction));
            }

            var changes = Plan.Nodes[node].Changes;
            for (int i = 0; i < changes.Length; i++)
                if ((pending & (1 << i)) != 0)
                    output.Add(new AbilityWorldMatterRequest(node, changes[i].Matter, changes[i].WorldMatter));
        }

        foreach (var op in Plan.EventOperations)
            if (operations[op.Index].WorldRequested)
            {
                operations[op.Index].WorldRequested = false;
                output.Add(new AbilityWorldMatterRequest(op.Node, op.Matter, op.WorldMatter));
            }

        foreach (var op in Plan.EventOperations)
            if (operations[op.Index].ConductionRequested)
            {
                operations[op.Index].ConductionRequested = false;
                output.Add(new AbilityWorldMatterRequest(op.Node, op.Conduction));
            }

        output.Sort((a, b) =>
            a.Node != b.Node ? a.Node.CompareTo(b.Node) : a.Order.CompareTo(b.Order));
    }
}
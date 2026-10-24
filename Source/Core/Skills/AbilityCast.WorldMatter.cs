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
        foreach (int node in Plan.Program.NativeWorldNodes)
        {
            ushort pending = nodes[node].WorldMatterPending;
            nodes[node].WorldMatterPending = 0;
            TakeConduction(node, Plan.Nodes[node].Conduction, ref nodes[node].Conduction, output, NativeStep(node, ConstructionOperationKind.Conduct));

            var changes = Plan.Nodes[node].Changes;
            for (int i = 0; i < changes.Length; i++)
                if ((pending & (1 << i)) != 0)
                {
                    int step = Plan.Nodes[node].MatterSteps?[i] ?? -1;
                    output.Add(new AbilityWorldMatterRequest(node, changes[i].Matter, changes[i].WorldMatter, step));
                    if (step >= 0) stepStates[step].WorldPending = true;
                }
        }

        foreach (int index in Plan.Program.WorldChanges)
            if (operations[index].WorldRequested)
            {
                var command = Plan.Program.Commands[Plan.Program.EventCommands[index]];
                var matter = Plan.Program.Matters[command.Parameter];
                operations[index].WorldRequested = false;
                output.Add(new AbilityWorldMatterRequest(command.Node, matter.Matter, matter.World, command.Step));
                if (command.Step >= 0) stepStates[command.Step].WorldPending = true;
            }

        foreach (int index in Plan.Program.Conduction)
        {
            var command = Plan.Program.Commands[Plan.Program.EventCommands[index]];
            TakeConduction(command.Node, Plan.Program.Conductors[command.Parameter], ref operations[index].Conduction, output, command.Step);
        }

        output.Sort((a, b) =>
            a.Node != b.Node ? a.Node.CompareTo(b.Node) :
            a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Pulse.CompareTo(b.Pulse));
    }

    /// <summary>领取本轮新波数；结束或收束后的旧请求不成为真实作用。</summary>
    /// <param name="node">实际部位。</param><param name="settings">定时或条件的同一配置。</param>
    /// <param name="run">可回退的时序。</param><param name="output">本次确认缓冲。</param>
    private void TakeConduction(int node, CompiledConduction settings, ref ConductionRun run,
        List<AbilityWorldMatterRequest> output, int step = -1)
    {
        int from = run.Emitted - run.Pending;
        run.Pending = 0;
        if (!nodes[node].Exists || nodes[node].Recovering || nodes[node].Leaving) return;
        for (int wave = from; wave < run.Emitted; wave++)
        {
            output.Add(new AbilityWorldMatterRequest(node, settings, wave + 1, step));
            if (step >= 0) stepStates[step].WorldPending = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>连续资料中的一段，不为每个部位另建可变列表。</summary>
internal readonly struct CompiledRange
{
    /// <summary>首项和总数。</summary>
    internal readonly int Start, Count;
    /// <summary>不包含的末项。</summary>
    internal int End => Start + Count;
    /// <summary>记录一段已经展开的资料。</summary>
    /// <param name="start">首项。</param><param name="count">总数。</param>
    internal CompiledRange(int start, int count) { Start = start; Count = count; }
}

/// <summary>只保存数值和参数索引的24字节派发指令，按实际种类连续排列。</summary>
internal readonly struct CompiledCommand
{
    /// <summary>本计划同类参数与函数的直接索引。</summary>
    internal readonly int Group;
    /// <summary>真实部位、独占执行状态、输出步骤及对应参数行。</summary>
    internal readonly int Node, State, Step, Parameter;
    /// <summary>部位时钟中的开始门槛。</summary>
    internal readonly float At;
    /// <summary>从已经检查的操作生成纯数值指令。</summary>
    /// <param name="action">稳定的执行身份。</param><param name="parameter">本种类参数表中的位置。</param><param name="group">本类直接索引。</param>
    internal CompiledCommand(CompiledEventOperation action, int parameter, int group)
    { Group = group; Node = action.Node; State = action.Index; Step = action.Step; At = action.At; Parameter = parameter; }
}

/// <summary>生长只有实际采用的目标和秒数。</summary>
internal readonly struct CompiledGrowthParameters
{
    /// <summary>目标倍率和历时。</summary>
    internal readonly float Scale, Seconds;
    /// <summary>复制已检查数值。</summary>
    /// <param name="scale">目标倍率。</param><param name="seconds">历时。</param>
    internal CompiledGrowthParameters(float scale, float seconds) { Scale = scale; Seconds = seconds; }
}

/// <summary>制作动作的同一份共享参数，单次状态只持有它和自己的进度。</summary>
internal sealed class CompiledMotionParameters
{
    /// <summary>不可变制作资源。</summary>
    internal readonly Cultiway.Core.Visuals.Forms.FormMotion Motion;
    /// <summary>播放速度、过渡秒数和真实阶段。</summary>
    internal readonly float Speed, Blend, From, Until;
    /// <summary>动作自身的维持。</summary>
    internal readonly CompiledConstructionHold Hold;
    /// <summary>保存已检查的同一套动作参数。</summary>
    /// <param name="motion">实际资源。</param><param name="speed">速度。</param><param name="blend">过渡。</param><param name="hold">维持。</param><param name="from">有效开始。</param><param name="until">有效结束。</param>
    internal CompiledMotionParameters(Cultiway.Core.Visuals.Forms.FormMotion motion, float speed, float blend, CompiledConstructionHold hold, float from, float until)
    { Motion = motion; Speed = speed; Blend = blend; Hold = hold; From = from; Until = until; }
}

/// <summary>本次物态输出和可选世界选择共用，不保留原始描述。</summary>
internal sealed class CompiledMatterParameters
{
    /// <summary>已经解析的输出及世界选择。</summary>
    internal readonly AbilityMatter Matter;
    /// <summary>可选世界选取。</summary>
    internal readonly CompiledWorldMatter World;
    /// <summary>保存已解析资料。</summary>
    /// <param name="matter">输出材料。</param><param name="world">世界选择。</param>
    internal CompiledMatterParameters(AbilityMatter matter, CompiledWorldMatter world) { Matter = matter; World = world; }
}

/// <summary>编译边界建立的同类参数表、部位范围和直接索引，运行不按描述重新筛选。</summary>
internal sealed class CompiledOperationProgram
{
    /// <summary>按种类分组的紧凑指令。</summary>
    internal readonly CompiledCommand[] Commands;
    /// <summary>状态索引直接对应指令；部位范围保持原资料顺序。</summary>
    internal readonly int[] EventCommands;
    /// <summary>各部位原顺序的全局事件状态范围。</summary>
    internal readonly CompiledRange[] NodeEvents, NodeTimers, NodeConduction;
    /// <summary>需要跟踪开始门槛、导电波次和世界转换的状态索引。</summary>
    internal readonly int[] Timers, Conduction, WorldChanges;
    /// <summary>只有实际定时导电或世界物态变化的部位参与原生请求领取。</summary>
    internal readonly int[] NativeWorldNodes;
    /// <summary>只保存实际使用的同类参数表与执行函数，稳定编号无需连续。</summary>
    internal readonly CompiledOperationGroup[] Groups;

    /// <summary>先按种类建立参数表，再保留原状态索引，最后合并步骤输入。</summary>
    /// <param name="plan">全部绑定已经完成的同版本计划。</param>
    internal CompiledOperationProgram(CompiledAbility plan)
    {
        var commands = new List<CompiledCommand>(); var groups = new List<CompiledOperationGroup>();
        foreach (var grouping in plan.EventOperations.GroupBy(action => action.Kind).OrderBy(group => group.Key))
        {
            var actions = grouping.ToArray(); int start = commands.Count;
            for (int parameter = 0; parameter < actions.Length; parameter++) commands.Add(new CompiledCommand(actions[parameter], parameter, groups.Count));
            groups.Add(actions[0].Implementation.Group(actions, new CompiledRange(start, commands.Count - start)));
        }
        Commands = commands.ToArray(); Groups = groups.ToArray(); EventCommands = new int[plan.EventOperations.Length];
        for (int i = 0; i < Commands.Length; i++) EventCommands[Commands[i].State] = i;
        NodeEvents = new CompiledRange[plan.FormCount]; NodeTimers = new CompiledRange[plan.FormCount]; NodeConduction = new CompiledRange[plan.FormCount];
        var timers = new List<int>(); var conduction = new List<int>(); var changes = new List<int>(); int offset = 0;
        for (int node = 0; node < plan.FormCount; node++)
        {
            var actions = plan.Nodes[node].EventOperations;
            NodeEvents[node] = new CompiledRange(offset, actions.Length); offset += actions.Length;
            int timerAt = timers.Count, conductionAt = conduction.Count;
            foreach (var action in actions)
            {
                if (action.Step >= 0 || action.When == null) timers.Add(action.Index);
                if (action.Conduction != null) conduction.Add(action.Index);
                if (action.WorldMatter != null) changes.Add(action.Index);
            }
            NodeTimers[node] = new CompiledRange(timerAt, timers.Count - timerAt); NodeConduction[node] = new CompiledRange(conductionAt, conduction.Count - conductionAt);
        }
        Timers = timers.ToArray(); Conduction = conduction.ToArray(); WorldChanges = changes.ToArray();
        NativeWorldNodes = Enumerable.Range(0, plan.FormCount).Where(node => plan.Nodes[node].Conduction != null || plan.Nodes[node].Changes.Any(change => change.WorldMatter != null)).ToArray();
        var inputs = new CompiledStepInput[plan.Steps.Sum(step => step.Inputs.Length)]; int inputAt = 0;
        foreach (var step in plan.Steps)
        {
            int count = step.Inputs.Length; foreach (var input in step.Inputs) inputs[inputAt++] = input;
            step.Inputs = new CompiledStepInputs(inputs, inputAt - count, count);
        }
    }
    /// <summary>数值阶段直接读取已经确定类型的连续参数表。</summary>
    /// <typeparam name="T">本类实际参数。</typeparam><param name="command">已编译指令。</param>
    internal T Parameter<T>(CompiledCommand command) => ((CompiledOperationGroup<T>)Groups[command.Group]).Parameters[command.Parameter];

}

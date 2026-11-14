using System;
using System.IO;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>一份完整展开计划的固定结构和执行容量，不把创法来源递归展开成新施放。</summary>
public sealed class ConstructionBudget
{
    /// <summary>原有最大嵌套深度，根部为零。</summary>
    public const int MaximumDepth = 16;
    /// <summary>每部位最多32项原操作，涵盖原有16次物态变化和其他实际操作。</summary>
    public const int MaximumNodeOperations = 32;
    /// <summary>原操作输出和命名步骤共用的展开容量。</summary>
    public const int MaximumSteps = 128;
    /// <summary>每部位条件操作与命名执行共用的容量。</summary>
    public const int MaximumEventOperations = 16;
    /// <summary>全部当前合法结构能够容纳的工作容量，使用整次计划而非单个子结构。</summary>
    public const int MaximumWork = 4194304;
    /// <summary>全部实际作用形状的容量；不从显示网格三角形数量推断。</summary>
    public const int MaximumContacts = 262144;
    /// <summary>编译缓存只保留有限修订；正在施放的计划由实际使用者继续持有。</summary>
    public const int MaximumCachedPlans = 4096;
    /// <summary>包含预留真实碎片和后段的部位数。</summary>
    public int Forms { get; }
    /// <summary>全部原操作与展开命名执行数量。</summary>
    public int Operations { get; }
    /// <summary>实际参与输入输出跟踪的有限步骤数。</summary>
    public int Steps { get; }
    /// <summary>实际保存的曲段数，循环不在编译时复制曲段。</summary>
    public int PathSegments { get; }
    /// <summary>流散数值格数，运行不会在每次接触后创建新网格。</summary>
    public int FlowCells { get; }
    /// <summary>固定形状和动态流散最多提供的作用形状数。</summary>
    public int ContactShapes { get; }
    /// <summary>身体、骨架、操作及数值格的共同工作量。</summary>
    public int Work { get; }

    /// <summary>先统计真实展开容量，再检查有明确完成时长的依赖与维持。</summary>
    /// <param name="plan">完整同版本计划。</param>
    internal ConstructionBudget(CompiledAbility plan)
    {
        Forms = plan.FormCount; Steps = plan.Steps.Length; Work = plan.WorkSize;
        Operations = plan.Nodes.Sum(node => node.Operations.Length);
        PathSegments = plan.Nodes.Sum(node => (node.FlightPath?.Count ?? 0) + node.EventOperations.Sum(operation => operation.Flight?.Path?.Count ?? 0));
        FlowCells = plan.Nodes.Sum(node => node.FlowSettings == null ? 0 : node.FlowSettings.Cells * node.FlowSettings.Cells);
        ContactShapes = plan.Nodes.Sum(node => node.FlowSettings == null ? node.Resource.Contacts.Length : node.FlowSettings.Cells * node.FlowSettings.Cells * 4);
        ContactShapes += plan.EventOperations.Where(operation => operation.Propagation != null).Sum(operation => operation.Propagation.MaximumCells);
        if (Work > MaximumWork || ContactShapes > MaximumContacts)
            throw new InvalidDataException("构造预算：共同工作量或实际作用形状超过整次容量");
        var starts = new double[Steps]; var ends = new double[Steps];
        var births = Enumerable.Repeat(-1d, Forms).ToArray(); var dynamic = new bool[Forms];
        for (int i = 0; i < Forms; i++) Birth(i);
        for (int i = 0; i < Steps; i++)
        {
            var step = plan.Steps[i]; var action = step.Action; var node = plan.Nodes[step.Node];
            double start = births[step.Node] + action.At + (action.When?.Delay ?? 0f);
            if (action.When is { Source: -1, Trigger: ConstructionChangeTrigger.End }) start = Math.Max(start, plan.Body.Duration + action.When.Delay);
            else if (action.When != null) start = Math.Max(start, (action.When.Source < 0 ? 0d : births[action.When.Source]) + action.When.Delay);
            if (action.When is { Source: -1, Trigger: ConstructionChangeTrigger.End } && births[step.Node] + action.At > plan.Body.Duration + .000001d)
                throw new InvalidDataException("Steps/" + (step.Id ?? step.OperationId) + "：身体单次收势早于操作接收门槛");
            foreach (var input in step.Inputs)
                if (input.Step >= 0)
                {
                    double ready = input.Completed ? ends[input.Step] : starts[input.Step];
                    if (input.Completed && double.IsPositiveInfinity(ready))
                        throw new InvalidDataException("Steps/" + step.Id + "：循环制作动作没有自然完成输出，请使用开始后的部位或实际事件");
                    start = Math.Max(start, ready);
                }
            starts[i] = start;
            double deadline = dynamic[step.Node] ? plan.Lifetime : Math.Min(plan.Lifetime, node.Presence.y);
            double required = action.Hold.Seconds == 0f ? 0d : action.Hold.At + action.Hold.Seconds;
            if (start > deadline + .000001d || required > 0d && (start + required > deadline + .000001d || action.At + required > node.Presence.y + .000001d))
                throw new InvalidDataException("Steps/" + (step.Id ?? step.OperationId) + "：最早可执行时点或维持超过实际存在截止");
            double span = action.Motion != null ? action.Motion.Duration / action.MotionSpeed : action.Gather?.Seconds ?? (action.Kind == ConstructionOperationKind.Grow ? action.Seconds : 0d);
            double hold = step.Native && action.Motion != null ? node.Hold.Seconds : action.Hold.Seconds;
            ends[i] = action.Motion?.Loop == true ? double.PositiveInfinity : start + span + hold;
        }

        // 事件发生时刻仍由实机决定；这里只取不晚于真实开始的下界，不能用未知出生去误拒绝合法后段。
        double Birth(int index)
        {
            if (births[index] >= 0d) return births[index];
            var node = plan.Nodes[index]; double at = 0d;
            if (node.Parent >= 0 && node.Activation?.Placement != ConstructionEventPlacement.Event)
            { at = Birth(node.Parent); dynamic[index] |= dynamic[node.Parent]; }
            if (node.Activation != null)
            {
                var gate = node.Activation;
                double source = gate.Source < 0 ? gate.Trigger == ConstructionChangeTrigger.End ? plan.Body.Duration : 0d : Birth(gate.Source);
                at = Math.Max(at, source + gate.Delay); dynamic[index] = true;
            }
            dynamic[index] |= node.FragmentSource >= 0 || node.AnchorBuild > 0f;
            return births[index] = at;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>放置替换世界投送时，以真实就绪步骤保留原开始条件，不制造运动完成。</summary>
internal static partial class ConstructionPlacementReferences
{
    /// <summary>先确认可替换范围，再沿开始引用保留前序链，最后统一写入独立草稿。</summary>
    /// <param name="definition">已经编译的原资料或其独立副本。</param><param name="plan">真实部位及步骤。</param>
    /// <param name="apply">通过后是否改写独立草稿。</param><param name="preserveStarts">新规则保留真实开始门槛；旧请求沿原改写。</param>
    /// <returns>不能等价转换的位置；空值表示可以改造。</returns>
    internal static string Rewrite(AbilityDefinition definition, CompiledAbility plan, bool apply, bool preserveStarts = true)
    {
        if (!preserveStarts) return RewriteLegacy(definition, plan, apply);
        var native = definition.Root.Operations.Where(Movement).Where(operation => operation.Id != null)
            .ToDictionary(operation => operation.Id, StringComparer.Ordinal);
        var removed = definition.Steps.Where(step => Movement(step.Operation) &&
            plan.Steps.Any(compiled => compiled.Id == step.Id && compiled.Node == 0))
            .ToDictionary(step => step.Id, StringComparer.Ordinal);
        foreach (var step in removed.Values)
            if (plan.Steps.Any(compiled => compiled.Id == step.Id && compiled.Node != 0))
                return "Steps/" + step.Id + "：同一运动同时作用于根部和其他部位，不能整体改为随动";
        if (plan.Nodes.Any(node => node.Activation?.Source == 0 && node.Activation.Trigger == ConstructionChangeTrigger.DistanceFromFlightStart) ||
            plan.EventOperations.Any(operation => operation.When?.Source == 0 && operation.When.Trigger == ConstructionChangeTrigger.DistanceFromFlightStart))
            return "root：仍依赖将被移除的飞行距离事件，放置不能代替真实行程";
        var keepNative = new HashSet<string>(StringComparer.Ordinal);
        var keepSteps = new HashSet<string>(StringComparer.Ordinal);
        var edits = new List<ConstructionOutputReference>();
        foreach (ConstructionStep step in definition.Steps.Where(step => !removed.ContainsKey(step.Id)))
        {
            string error = Visit(step);
            if (error != null) return error;
        }
        if (!apply) return null;
        // 先完成全部检查，失败不会留下半份改写；就绪沿原时点、条件和前序真正执行。
        foreach (ConstructionOutputReference reference in edits) reference.Output = "ready";
        for (int i = 0; i < definition.Root.Operations.Length; i++)
            if (definition.Root.Operations[i].Id is string id && keepNative.Contains(id))
                definition.Root.Operations[i] = Ready(definition.Root.Operations[i]);
        foreach (string id in keepSteps) removed[id].Operation = Ready(removed[id].Operation);
        return null;

        string Visit(ConstructionStep step)
        {
            foreach (var entry in step.Inputs)
            {
                ConstructionOutputReference input = entry.Value;
                ConstructionOperation operation = null;
                ConstructionStep producer = null;
                if (input.Node == "root" && input.Operation != null) native.TryGetValue(input.Operation, out operation);
                else if (input.Step != null && removed.TryGetValue(input.Step, out producer)) operation = producer.Operation;
                if (operation == null) continue;
                string path = "Steps/" + step.Id + "/Inputs/" + entry.Key;
                ConstructionOperationPort output = ConstructionOperations.Describe(operation.Kind).Outputs.Single(port => port.Name == input.Output);
                if (input.Stage == ConstructionOutputStage.Completed || input.Stage == ConstructionOutputStage.Auto &&
                    output.Kind is ConstructionValueKind.Process or ConstructionValueKind.Result)
                    return path + "：仍在等待将被移除的运动完成，放置不能代替真实飞行或脱离结果";
                if (output.Kind is ConstructionValueKind.Process or ConstructionValueKind.Result) edits.Add(input);
                if (producer == null) keepNative.Add(operation.Id);
                else if (keepSteps.Add(producer.Id))
                {
                    string error = Visit(producer);
                    if (error != null) return error;
                }
            }
            return null;
        }
    }

    /// <summary>只留下原开始要求；不保留虚假的速度、飞行过程或解除连接结果。</summary>
    /// <param name="source">被明确替换的原运动。</param>
    private static ConstructionOperation Ready(ConstructionOperation source) => new()
    { Id = source.Id, Kind = ConstructionOperationKind.Ready, At = source.At, When = source.When };

    /// <summary>放置会替代根部的世界投送及解除连接。</summary>
    /// <param name="operation">共同边界已检查的操作。</param>
    private static bool Movement(ConstructionOperation operation) => operation.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Detach;
}

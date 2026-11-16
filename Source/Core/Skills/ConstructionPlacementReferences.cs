using System;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>移除根部运动时，只消去同一真实主体已经保证的开始依赖，不制造完成事实。</summary>
internal static class ConstructionPlacementReferences
{
    /// <summary>先找会移除的运动，再检查保留步骤，最后只在独立草稿上改写可等价的输入。</summary>
    /// <param name="definition">已编译的原资料或其独立副本。</param>
    /// <param name="plan">原资料的真实部位及步骤目标。</param>
    /// <param name="apply">是否将通过的改写应用到独立草稿。</param>
    /// <returns>不能等价转换的具体位置；空值表示可以改造。</returns>
    internal static string Rewrite(AbilityDefinition definition, CompiledAbility plan, bool apply)
    {
        var native = definition.Root.Operations.Where(Movement).Where(operation => operation.Id != null)
            .ToDictionary(operation => operation.Id, StringComparer.Ordinal);
        var removed = definition.Steps.Where(step => Movement(step.Operation) &&
            plan.Steps.Any(compiled => compiled.Id == step.Id && compiled.Node == 0))
            .ToDictionary(step => step.Id, StringComparer.Ordinal);
        foreach (var step in removed.Values)
            if (plan.Steps.Any(compiled => compiled.Id == step.Id && compiled.Node != 0))
                return "Steps/" + step.Id + "：同一运动同时作用于根部和其他部位，不能整体改为随动";
        foreach (var step in definition.Steps.Where(step => !removed.ContainsKey(step.Id)))
        {
            float at = step.Operation.At;
            foreach (var entry in step.Inputs.ToArray())
            {
                ConstructionOutputReference input = entry.Value;
                ConstructionOperation operation = null;
                ConstructionStep producer = null;
                if (input.Node == "root" && input.Operation != null) native.TryGetValue(input.Operation, out operation);
                else if (input.Step != null && removed.TryGetValue(input.Step, out producer)) operation = producer.Operation;
                if (operation == null) continue;
                string path = "Steps/" + step.Id + "/Inputs/" + entry.Key;
                var port = ConstructionOperations.Describe(operation.Kind).Outputs.Single(output => output.Name == input.Output);
                if (input.Stage == ConstructionOutputStage.Completed || input.Stage == ConstructionOutputStage.Auto &&
                    port.Kind is ConstructionValueKind.Process or ConstructionValueKind.Result)
                    return path + "：仍在等待将被移除的运动完成，随动不能代替真实飞行或脱离结果";
                if (operation.When != null || step.Operation.When != null && operation.At > step.Operation.At || producer != null && (producer.Inputs.Count != 1 ||
                    producer.Inputs["part"].Node != "root" || producer.Inputs["part"].Operation != null) ||
                    plan.Steps.Any(compiled => compiled.Id == step.Id && compiled.Node != 0))
                    return path + "：运动开始还依赖额外事件、输出或其他部位，不能改为根部就绪";
                if (entry.Key is not ("after" or "part") || entry.Key == "part" && port.Kind is not
                    (ConstructionValueKind.Part or ConstructionValueKind.Structure))
                    return path + "：所需输出不能由随动主体的开始替代";
                if (!apply) continue;
                // 同根部的开始是既有part输入已经保证的事实；原运动的定时仍约束后续操作。
                at = Math.Max(at, operation.At);
                if (entry.Key == "after") step.Inputs.Remove(entry.Key);
                else step.Inputs[entry.Key] = new ConstructionOutputReference
                { Node = "root", Output = input.Output, Stage = ConstructionOutputStage.Started, Mode = input.Mode };
            }
            if (apply) step.Operation.At = at;
        }
        return null;
    }

    /// <summary>随动会替代根部的世界投送及解除连接。</summary>
    /// <param name="operation">已经过共同边界检查的实际操作。</param>
    private static bool Movement(ConstructionOperation operation) => operation.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Detach;
}

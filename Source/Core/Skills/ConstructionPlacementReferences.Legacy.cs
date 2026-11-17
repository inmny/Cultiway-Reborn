using System;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>仅正式旧等待请求使用原开始改写规则，保持冻结选择、结果及费用。</summary>
internal static partial class ConstructionPlacementReferences
{
    /// <summary>保留第十六至二十一版已冻结请求的原选择和明确失败。</summary>
    /// <param name="definition">旧请求独立草稿。</param><param name="plan">原计划。</param><param name="apply">是否执行原改写。</param>
    private static string RewriteLegacy(AbilityDefinition definition, CompiledAbility plan, bool apply)
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

}

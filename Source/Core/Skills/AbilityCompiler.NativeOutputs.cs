using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>为已命名的原部位操作接通共同输出，保持原有执行路径。</summary>
public static partial class AbilityCompiler
{
    /// <summary>先登记实际条件操作，再给原定时过程建立只读观察索引。</summary>
    /// <param name="definition">完整构造。</param><param name="nodes">已经展开的真实部位。</param><param name="steps">同一运行步骤集合。</param>
    private static Dictionary<(string, string), CompiledConstructionStep> CompileNativeOutputs(AbilityDefinition definition, List<CompiledForm> nodes, List<CompiledConstructionStep> steps)
    {
        var result = new Dictionary<(string, string), CompiledConstructionStep>();
        for (int n = 0; n < nodes.Count; n++)
        {
            var node = nodes[n];
            if (!node.HasNamedOperations || node.FragmentSource >= 0 || node.RemainderRoot >= 0) continue;
            var original = Declaration(definition.Root, node.DeclarationPath);
            int matter = 0;
            for (int i = 0; i < original.Operations.Length; i++)
            {
                var operation = original.Operations[i];
                int change = operation.Kind == ConstructionOperationKind.ConvertMatter && operation.When == null ? matter++ : -1;
                if (operation.Id == null) continue;
                string path = node.Path + "/Operations/" + operation.Id;
                if (steps.Count >= ConstructionBudget.MaximumSteps) Fail(path, "原操作输出及命名步骤合计超过128项");
                var action = operation.When == null
                    ? new CompiledEventOperation(operation, ReadVector(operation.Vector, path), definition.Lifetime, path, node.Resource, original, node.Fracture, bound: true, order: i)
                    : node.EventOperations.Single(item => item.Id == operation.Id);
                action.Node = n; action.Step = steps.Count;
                var step = new CompiledConstructionStep(null, n, Array.Empty<CompiledStepInput>(), action,
                    ConstructionOperations.Describe(operation.Kind), operation.Id, operation.When == null);
                steps.Add(step); result.Add((node.Path, operation.Id), step);
                if (!step.Native) continue;
                if (change >= 0)
                {
                    node.MatterSteps ??= Enumerable.Repeat(-1, node.Changes.Length).ToArray();
                    node.MatterSteps[change] = action.Step;
                }
                else
                {
                    node.NativeSteps ??= Enumerable.Repeat(-1, ConstructionOperations.All.Count).ToArray();
                    node.NativeSteps[(int)operation.Kind] = action.Step;
                }
            }
        }
        return result;
    }
}

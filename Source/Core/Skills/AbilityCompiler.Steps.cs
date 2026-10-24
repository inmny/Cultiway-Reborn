using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>命名输出在创建边界解析成直接索引，运行中不解释引用字符串。</summary>
public static partial class AbilityCompiler
{
    /// <summary>先解析依赖和真实部位，再复用共同数值编译建立操作。</summary>
    /// <param name="definition">完整当前构造。</param><param name="nodes">已展开的真实部位。</param>
    private static CompiledConstructionStep[] CompileSteps(AbilityDefinition definition, List<CompiledForm> nodes)
    {
        ConstructionStep[] sources = definition.Steps;
        if (sources == null || sources.Length > 128) Fail("Steps", "命名操作集合缺失或超过128项");
        if (sources.Length == 0) return Array.Empty<CompiledConstructionStep>();
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var paths = nodes.Select((node, index) => (node.Path, index)).ToDictionary(item => item.Path, item => item.index, StringComparer.Ordinal);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null || string.IsNullOrWhiteSpace(sources[i].Id) || names.ContainsKey(sources[i].Id)) Fail("Steps[" + i + "]", "步骤编号为空或重复");
            names.Add(sources[i].Id, i);
        }
        var visiting = new byte[sources.Length]; var compiled = new CompiledConstructionStep[sources.Length]; var sorted = new List<CompiledConstructionStep>();
        for (int i = 0; i < sources.Length; i++) Visit(i);
        return sorted.ToArray();

        // 输出身份按依赖递归确定，因此引用不受声明先后影响。
        CompiledConstructionStep Visit(int index)
        {
            if (visiting[index] == 2) return compiled[index];
            ConstructionStep source = sources[index]; string path = "Steps/" + source.Id;
            if (visiting[index] == 1) Fail(path, "输出引用形成循环");
            visiting[index] = 1;
            if (source.Inputs == null || !source.Inputs.TryGetValue("part", out var subject)) Fail(path, "缺少实际part输入");
            ConstructionOperationContract contract = ConstructionOperations.Resolve(source.Operation, path, bound: true);
            ConstructionOperation operation = source.Operation;
            if (operation.Kind is ConstructionOperationKind.Fracture or ConstructionOperationKind.Flow or ConstructionOperationKind.Recover or ConstructionOperationKind.Leave or ConstructionOperationKind.Conduct)
                Fail(path, "此操作的集合或世界输出执行仍在3.04接入中，不能只生成占位结果");
            if (operation.WorldMatter != null) Fail(path, "世界材料结果继续在3.04接入，不能把本地物态变化当成世界确认");
            if (!Finite(operation.At) || operation.At < 0 || operation.At >= definition.Lifetime || !Finite(operation.Seconds) || !Finite(operation.TurnRate) || operation.TurnRate < 0 ||
                operation.Trigger != ConstructionChangeTrigger.Time) Fail(path, "操作时点、历时或独立触发无效，实际条件使用When或输出输入");
            var inputs = new List<CompiledStepInput>(); int target = -1;
            foreach (var entry in source.Inputs)
            {
                var port = contract.Inputs.FirstOrDefault(p => p.Name == entry.Key);
                if (port == null) Fail(path, "操作没有输入端口：" + entry.Key);
                var input = Resolve(entry.Value, path + "/Inputs/" + entry.Key);
                if (!port.Kinds.Contains(input.Kind)) Fail(path, "输入资料种类不匹配：" + entry.Key + "收到" + input.Kind);
                if (entry.Key is not ("part" or "after" or "event" or "parent" or "connection")) Fail(path, "此输入的实际传递仍在3.04接入中：" + entry.Key);
                if (entry.Key == "part") target = input.Node;
                inputs.Add(new CompiledStepInput(input.Step, input.Node, input.Kind, input.Completed, entry.Key == "connection"));
            }
            CompiledForm node = nodes[target];
            if (node.FragmentSource >= 0) Fail(path, "碎片集合输出继续在3.04接入，不把完整主体资源冒充单片");
            foreach (var entry in source.Inputs.Where(item => item.Key is "parent" or "connection"))
            {
                var input = Resolve(entry.Value, path);
                if (entry.Key == "parent" && input.Node != node.Parent || entry.Key == "connection" && input.Node != target)
                    Fail(path, "所选父体或连接不属于实际目标部位");
            }
            if (operation.Kind == ConstructionOperationKind.Detach && node.Parent < 0 && !(target == 0 && definition.Binding?.Follow == true))
                Fail(path, "所选部位没有可解除的真实连接");
            if (operation.Kind == ConstructionOperationKind.Gather && (node.Parent < 0 || node.Activation?.Placement == ConstructionEventPlacement.Event))
                Fail(path, "聚合需要实际父体和汇入布局");
            if (node.EventOperations.Length >= 16) Fail(path, "单部位条件及命名操作合计超过16项");
            var original = Declaration(definition.Root, node.DeclarationPath);
            var action = new CompiledEventOperation(operation, ReadVector(operation.Vector, path), definition.Lifetime, path, node.Resource, original, null, bound: true)
            { Step = sorted.Count };
            var result = new CompiledConstructionStep(source.Id, target, inputs.ToArray(), action, contract);
            node.EventOperations = node.EventOperations.Concat(new[] { action }).ToArray();
            node.Operations = node.Operations.Concat(new[] { new CompiledOperationRequirement(node.Path, original.Operations.Length + index, operation, contract) }).ToArray();
            compiled[index] = result; sorted.Add(result); visiting[index] = 2; return result;
        }

        CompiledStepInput Resolve(ConstructionOutputReference reference, string path)
        {
            if (reference == null || !Enum.IsDefined(typeof(ConstructionOutputStage), reference.Stage) || string.IsNullOrWhiteSpace(reference.Node) == string.IsNullOrWhiteSpace(reference.Step))
            { Fail(path, "输入必须明确引用一个实际部位或步骤输出"); return default; }
            if (!string.IsNullOrWhiteSpace(reference.Node))
            {
                if (!paths.TryGetValue(reference.Node, out int node)) Fail(path, "不存在实际部位：" + reference.Node);
                if (reference.Stage == ConstructionOutputStage.Completed) Fail(path, "直接部位没有可等待的操作完成结果");
                ConstructionValueKind kind = reference.Output is null or "part" ? ConstructionValueKind.Part : reference.Output == "structure" ? ConstructionValueKind.Structure : ConstructionValueKind.Relation;
                if (reference.Output is not (null or "part" or "structure" or "connection")) Fail(path, "直接部位没有该输出：" + reference.Output);
                return new CompiledStepInput(-1, node, kind, false, reference.Output == "connection");
            }
            if (!names.TryGetValue(reference.Step, out int source)) Fail(path, "不存在步骤：" + reference.Step);
            CompiledConstructionStep producer = Visit(source);
            var output = producer.Contract.Outputs.FirstOrDefault(port => port.Name == reference.Output);
            if (output == null) Fail(path, "步骤没有所选输出：" + reference.Output);
            if (output.Collection) Fail(path, "集合输出接入尚未完成，不能隐式只取第一项");
            bool completed = reference.Stage == ConstructionOutputStage.Completed || reference.Stage == ConstructionOutputStage.Auto && output.Kind is ConstructionValueKind.Process or ConstructionValueKind.Result;
            return new CompiledStepInput(producer.Action.Step, producer.Node, output.Kind, completed, output.Kind == ConstructionValueKind.Relation);
        }
    }

    /// <summary>按编译时保留的原声明路径读取不可变执行参数所需的节点说明。</summary>
    /// <param name="root">原根部。</param><param name="path">去除挂点展开后缀的原声明路径。</param>
    private static ConstructionNode Declaration(ConstructionNode root, string path)
    {
        ConstructionNode current = root;
        foreach (string part in path.Split('/').Skip(1)) current = current.Children.Single(child => child.Id == part);
        return current;
    }
}

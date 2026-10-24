using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>命名输出和集合在创建边界展开为真实部位索引。</summary>
public static partial class AbilityCompiler
{
    /// <summary>先解析依赖和集合，再复用共同执行参数，最后核对动态表面的运动。</summary>
    /// <param name="definition">完整当前构造。</param><param name="nodes">已展开的真实部位。</param>
    private static CompiledConstructionStep[] CompileSteps(AbilityDefinition definition, List<CompiledForm> nodes)
    {
        ConstructionStep[] sources = definition.Steps;
        if (sources == null || sources.Length > 128) Fail("Steps", "命名操作集合缺失或超过128项");
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var paths = nodes.Select((node, index) => (node.Path, index)).ToDictionary(item => item.Path, item => item.index, StringComparer.Ordinal);
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null || string.IsNullOrWhiteSpace(sources[i].Id) || names.ContainsKey(sources[i].Id)) Fail("Steps[" + i + "]", "步骤编号为空或重复");
            names.Add(sources[i].Id, i);
        }
        var visiting = new byte[sources.Length]; var compiled = new CompiledConstructionStep[sources.Length][]; var sorted = new List<CompiledConstructionStep>();
        var native = CompileNativeOutputs(definition, nodes, sorted);
        if (sources.Length == 0) return sorted.ToArray();
        for (int i = 0; i < sources.Length; i++) Visit(i);
        foreach (var node in nodes.Where(node => node.FlowSettings != null))
        {
            if (node.Resource.Meshes.Length == 0 || node.Fracture != null) Fail(node.Path, "流散需要真实生成几何，不能在同一部位同时保留固定碎片");
            foreach (var flight in node.EventOperations.Select(op => op.Flight).Append(node.FlowAt < 0 || node.FlyAt > node.FlowAt ? node.Flight : null).Where(flight => flight != null))
                if (flight.Path != null || flight.Velocity.z != 0 || flight.Acceleration.z != 0) Fail(node.Path, "浅层流散只支持水平投送，不能以三维路径代替真实水格");
        }
        return sorted.ToArray();

        CompiledConstructionStep[] Visit(int index)
        {
            if (visiting[index] == 2) return compiled[index];
            ConstructionStep source = sources[index]; string path = "Steps/" + source.Id;
            if (source.Operation?.Id != null) Fail(path, "Steps操作使用外层步骤名称，不能再声明部位操作名称");
            if (visiting[index] == 1) Fail(path, "输出引用形成循环");
            visiting[index] = 1;
            if (source.Inputs == null || !source.Inputs.ContainsKey("part")) Fail(path, "缺少实际part输入");
            ConstructionOperationContract contract = ConstructionOperations.Resolve(source.Operation, path, bound: true);
            ConstructionOperation operation = source.Operation;
            if (!Finite(operation.At) || operation.At < 0 || operation.At >= definition.Lifetime || !Finite(operation.Seconds) || !Finite(operation.TurnRate) || operation.TurnRate < 0 ||
                operation.Trigger != ConstructionChangeTrigger.Time) Fail(path, "操作时点、历时或独立触发无效，实际条件使用When或输出输入");
            var bindings = new Dictionary<string, CompiledStepInput[]>(StringComparer.Ordinal);
            foreach (var entry in source.Inputs)
            {
                var port = contract.Inputs.FirstOrDefault(p => p.Name == entry.Key);
                if (port == null) Fail(path, "操作没有输入端口：" + entry.Key);
                var values = Resolve(entry.Value, path + "/Inputs/" + entry.Key);
                if (values.Any(input => !port.Kinds.Contains(input.Kind))) Fail(path, "输入资料种类不匹配：" + entry.Key);
                if (entry.Key is not ("part" or "after" or "event" or "parent" or "connection" or "worldMaterials" or "water")) Fail(path, "现有操作输出不能替代施放上下文输入：" + entry.Key);
                bool world = entry.Key is "worldMaterials" or "water";
                if (world && (entry.Value.Stage == ConstructionOutputStage.Started || values.Any(input => input.Step < 0 ||
                    sorted[input.Step].Action.Kind != ConstructionOperationKind.Leave && sorted[input.Step].Action.WorldMatter == null)))
                    Fail(path, "世界材料输入需要已经确认的留置或世界转换输出：" + entry.Key);
                if (entry.Key == "worldMaterials" && operation.WorldMatter == null && operation.Recovery?.WorldMaterials != true)
                    Fail(path, "实际世界材料输入需要明确的世界转换或世界收束参数");
                if (values.Length > 1 && entry.Value.Mode == ConstructionInputMode.Single) Fail(path, "集合必须明确选择Each或All：" + entry.Key);
                if (entry.Key == "part" && entry.Value.Mode == ConstructionInputMode.All) Fail(path, "实际部位集合需要Each逐个处理，不能合成一个假部位");
                bindings.Add(entry.Key, values);
            }
            var result = new List<CompiledConstructionStep>();
            foreach (var subject in bindings["part"])
            {
                int target = subject.Node; CompiledForm node = nodes[target]; var inputs = new List<CompiledStepInput>();
                foreach (var entry in bindings)
                {
                    var selected = entry.Value;
                    if (entry.Key == "part") selected = new[] { subject };
                    else if (selected.Length > 1 && source.Inputs[entry.Key].Mode == ConstructionInputMode.Each)
                    {
                        int expected = entry.Key == "parent" ? node.Parent : target;
                        selected = selected.Where(input => input.Node == expected).ToArray();
                        if (selected.Length != 1) Fail(path, "逐个输入缺少唯一的同部位对应：" + entry.Key);
                    }
                    foreach (var input in selected)
                    {
                        if (entry.Key == "parent" && input.Node != node.Parent || entry.Key == "connection" && input.Node != target) Fail(path, "所选父体或连接不属于实际目标部位");
                        bool world = entry.Key is "worldMaterials" or "water";
                        inputs.Add(new CompiledStepInput(input.Step, input.Node, input.Kind, input.Completed || world, entry.Key == "connection", world));
                    }
                }
                if (operation.Kind == ConstructionOperationKind.Detach && (node.FragmentSource >= 0 || node.Parent < 0 && !(target == 0 && definition.Binding?.Follow == true)))
                    Fail(path, "所选部位没有可解除的真实连接");
                if (operation.Kind == ConstructionOperationKind.Gather && (node.Parent < 0 || node.FragmentSource >= 0 || node.Activation?.Placement == ConstructionEventPlacement.Event))
                    Fail(path, "聚合需要仍存在的父连接，已脱落碎片的重新连接继续按后续动作实现");
                if (node.EventOperations.Length >= 16 || sorted.Count >= 128) Fail(path, "单部位操作超过16项或展开步骤超过128项");
                if ((operation.Kind is ConstructionOperationKind.Recover or ConstructionOperationKind.Leave or ConstructionOperationKind.Conduct || operation.WorldMatter != null) && node.Resource.Meshes.Length == 0)
                    Fail(path, "世界操作需要实际生成主体，空支点不能制造材料或回能");
                if (operation.WorldMatter != null && AbilityMatters.Resolve(operation.Matter, operation.State, path).State is not ("liquid" or "solid"))
                    Fail(path, "世界材料转换需要明确的液态或固态输出");
                if (operation.Kind == ConstructionOperationKind.Recover && (node.Recovery != null || node.EventOperations.Any(op => op.Recovery != null))) Fail(path, "同一部位只能声明一套真实收束");
                if (operation.Kind == ConstructionOperationKind.Leave)
                {
                    if (node.Residue != null) Fail(path, "同一部位只能交给世界一次");
                    node.Residue = new CompiledResidue(operation.Residue, path);
                }
                if (operation.Kind == ConstructionOperationKind.Fracture)
                {
                    if (node.Fracture != null) Fail(path, "同一主体只能声明一套真实分件");
                    node.Fracture = new CompiledFracture(operation.Fracture, node.Resource, node.ContactRole, definition.Lifetime, path);
                    int start = nodes.Count; AppendFragments(node, target, definition.Lifetime, nodes);
                    for (int n = start; n < nodes.Count; n++) paths.Add(nodes[n].Path, n);
                }
                if (operation.Kind == ConstructionOperationKind.Flow && node.FlowSettings != null) Fail(path, "同一部位只能声明一套实际流散");
                var original = node.FragmentSource < 0 ? Declaration(definition.Root, node.DeclarationPath) : new ConstructionNode { Id = node.Path, Rotation = VectorArray(node.Rotation.eulerAngles) };
                var action = new CompiledEventOperation(operation, ReadVector(operation.Vector, path), definition.Lifetime, path, node.Resource, original, node.Fracture, bound: true, order: original.Operations.Length + index)
                { Step = sorted.Count };
                var step = new CompiledConstructionStep(source.Id, target, inputs.ToArray(), action, contract);
                node.EventOperations = node.EventOperations.Concat(new[] { action }).ToArray();
                node.Operations = node.Operations.Concat(new[] { new CompiledOperationRequirement(node.Path, original.Operations.Length + index, operation, contract) }).ToArray();
                result.Add(step); sorted.Add(step);
            }
            compiled[index] = result.ToArray(); visiting[index] = 2; return compiled[index];
        }

        CompiledStepInput[] Resolve(ConstructionOutputReference reference, string path)
        {
            if (reference == null || !Enum.IsDefined(typeof(ConstructionOutputStage), reference.Stage) || !Enum.IsDefined(typeof(ConstructionInputMode), reference.Mode) ||
                string.IsNullOrWhiteSpace(reference.Node) == string.IsNullOrWhiteSpace(reference.Step) || reference.Operation != null && (string.IsNullOrWhiteSpace(reference.Operation) || string.IsNullOrWhiteSpace(reference.Node)))
            { Fail(path, "输入必须明确引用一个实际部位或步骤输出，选择方式和阶段必须有效"); return null; }
            CompiledConstructionStep[] producers;
            if (!string.IsNullOrWhiteSpace(reference.Node) && reference.Operation != null)
            {
                if (!native.TryGetValue((reference.Node, reference.Operation), out var operation)) Fail(path, "不存在已命名的原部位操作：" + reference.Node + "/" + reference.Operation);
                producers = new[] { operation };
            }
            else if (!string.IsNullOrWhiteSpace(reference.Node))
            {
                if (!paths.TryGetValue(reference.Node, out int node)) Fail(path, "不存在实际部位：" + reference.Node);
                if (reference.Stage == ConstructionOutputStage.Completed) Fail(path, "直接部位没有可等待的操作完成结果");
                ConstructionValueKind kind = reference.Output is null or "part" ? ConstructionValueKind.Part : reference.Output == "structure" ? ConstructionValueKind.Structure : ConstructionValueKind.Relation;
                if (reference.Output is not (null or "part" or "structure" or "connection")) Fail(path, "直接部位没有该输出：" + reference.Output);
                return new[] { new CompiledStepInput(-1, node, kind, false, reference.Output == "connection") };
            }
            else
            {
                if (!names.TryGetValue(reference.Step, out int source)) Fail(path, "不存在步骤：" + reference.Step);
                producers = Visit(source);
            }
            var values = new List<CompiledStepInput>();
            foreach (var producer in producers)
            {
                var output = producer.Contract.Outputs.FirstOrDefault(port => port.Name == reference.Output);
                if (output == null) Fail(path, "步骤没有所选输出：" + reference.Output);
                bool completed = reference.Stage == ConstructionOutputStage.Completed || reference.Stage == ConstructionOutputStage.Auto &&
                    (output.Collection || output.Kind is ConstructionValueKind.Process or ConstructionValueKind.Result ||
                    output.Kind == ConstructionValueKind.Material && (producer.Action.Kind == ConstructionOperationKind.Leave || producer.Action.WorldMatter != null));
                int[] targets = output.Collection ? nodes[producer.Node].Fracture.Pieces : new[] { producer.Node };
                foreach (int target in targets) values.Add(new CompiledStepInput(producer.Action.Step, target, output.Kind, completed, output.Kind == ConstructionValueKind.Relation));
            }
            return values.ToArray();
        }
    }

    /// <summary>按原声明路径读取节点说明，不保存可编辑对象到运行计划。</summary>
    /// <param name="root">原根部。</param><param name="path">原声明路径。</param>
    private static ConstructionNode Declaration(ConstructionNode root, string path)
    {
        ConstructionNode current = root;
        foreach (string part in path.Split('/').Skip(1)) current = current.Children.Single(child => child.Id == part);
        return current;
    }

    /// <summary>为实际分件的共同数值编译提供局部朝向。</summary>
    /// <param name="vector">已解析数值。</param>
    private static float[] VectorArray(Vector3 vector) => new[] { vector.x, vector.y, vector.z };
}

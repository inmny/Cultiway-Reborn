using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Skills;

/// <summary>在创建边界解析事件关系，拒绝没有起点的等待结构。</summary>
public static partial class AbilityCompiler
{
    /// <summary>先解析引用与来源能力，再核对依赖，最后建立直接监听索引。</summary>
    /// <param name="nodes">已经有限展开的部位。</param>
    /// <param name="body">可选的真实身体动作。</param>
    /// <param name="operations">运行中的有限条件操作。</param>
    private static int[][] CompileActivations(List<CompiledForm> nodes, CompiledBodyAction body, List<CompiledEventOperation> operations)
    {
        if (operations.Count == 0 && !nodes.Any(node => node.Activation != null)) return Array.Empty<int[]>();
        var paths = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < nodes.Count; i++)
            if (paths.ContainsKey(nodes[i].Path)) Fail(nodes[i].Path, "事件引用需要唯一的完整部位路径");
            else paths.Add(nodes[i].Path, i);
        var children = new List<int>[nodes.Count];
        var listeners = new List<int>[(nodes.Count + 1) * CompiledConstructionCondition.EventCount];
        for (int i = 0; i < nodes.Count; i++)
        {
            CompiledForm node = nodes[i];
            if (node.Parent >= 0) (children[node.Parent] ??= new List<int>()).Add(i);
            CompiledConstructionActivation gate = node.Activation;
            if (gate == null) continue;
            ResolveCondition(i, gate);
            if (gate.Trigger == ConstructionChangeTrigger.End && gate.Placement == ConstructionEventPlacement.Attached)
                for (int parent = node.Parent; parent >= 0; parent = nodes[parent].Parent)
                    if (parent == gate.Source) Fail(node.Path, "结束事件不能继续附着在已结束的来源链，请改用事件处独立生成");
            int bucket = (gate.Source < 0 ? nodes.Count : gate.Source) * CompiledConstructionCondition.EventCount + (int)gate.Trigger - 1;
            (listeners[bucket] ??= new List<int>()).Add(i);
        }
        foreach (CompiledEventOperation operation in operations)
        {
            ResolveCondition(operation.Node, operation.When);
            if (operation.When.Source == operation.Node && operation.When.Trigger == ConstructionChangeTrigger.End)
                Fail(nodes[operation.Node].Path, "自身结束后不能再改变自身运动，可建立独立后续部位");
            int bucket = (operation.When.Source < 0 ? nodes.Count : operation.When.Source) * CompiledConstructionCondition.EventCount + (int)operation.When.Trigger - 1;
            (listeners[bucket] ??= new List<int>()).Add(~operation.Index);
        }
        foreach (CompiledEventOperation operation in operations)
            if (operation.Kind == ConstructionOperationKind.Fly && operation.When.Trigger == ConstructionChangeTrigger.End && operation.When.Delay > 0)
                for (int parent = nodes[operation.Node].Parent; parent >= 0; parent = nodes[parent].Parent)
                    if (parent == operation.When.Source && !nodes[operation.Node].EventOperations.Any(other => other.Kind == ConstructionOperationKind.Detach &&
                        other.When.Source == parent && other.When.Trigger == ConstructionChangeTrigger.End && other.When.Delay == 0 && other.At <= operation.At))
                        Fail(nodes[operation.Node].Path, "父体结束时应先立即脱离，再延迟改变飞行");
        for (int i = 0; i < nodes.Count; i++)
        {
            int start = i * CompiledConstructionCondition.EventCount;
            nodes[i].SizeThresholds = listeners.Skip(start + (int)ConstructionChangeTrigger.SizeAbove - 1).Take(2).Where(list => list != null)
                .SelectMany(list => list).Select(index => index < 0 ? operations[~index].When.Threshold : nodes[index].Activation.Threshold).Distinct().OrderBy(value => value).ToArray();
        }
        var visited = new byte[nodes.Count];
        for (int i = 0; i < nodes.Count; i++) Visit(i);
        for (int i = 0; i < nodes.Count; i++) nodes[i].Children = children[i]?.ToArray() ?? Array.Empty<int>();
        return listeners.Select(list => list?.ToArray() ?? Array.Empty<int>()).ToArray();

        // 结构关系和事件来源都是开始的必要条件，不能只检查显式事件的循环。
        void Visit(int index)
        {
            if (visited[index] == 2) return;
            if (visited[index] == 1) Fail(nodes[index].Path, "事件等待形成循环，缺少可先开始的部位");
            visited[index] = 1;
            CompiledForm node = nodes[index];
            if (node.Activation?.Source >= 0) Visit(node.Activation.Source);
            if (node.Parent >= 0 && node.Activation?.Placement != ConstructionEventPlacement.Event) Visit(node.Parent);
            visited[index] = 2;
        }

        // 开始和运动共用来源能力检查，不让两个入口各推一套条件。
        void ResolveCondition(int target, CompiledConstructionCondition condition)
        {
            condition.Source = Resolve(target, condition.SourcePath);
            if (condition.Source < 0)
            {
                if (body == null || condition.Trigger is not (ConstructionChangeTrigger.ActorContact or ConstructionChangeTrigger.End))
                    Fail(nodes[target].Path, "此事件需要实际身体，身体目前只提供真实接触和自然收势");
                return;
            }
            CompiledForm source = nodes[condition.Source];
            if (condition.Trigger == ConstructionChangeTrigger.ActorContact && source.ContactRole != ConstructionContactRole.Attack ||
                condition.Trigger == ConstructionChangeTrigger.Clash && source.ContactRole == ConstructionContactRole.Decoration ||
                condition.Trigger == ConstructionChangeTrigger.MatterChanged && source.Changes.Length == 0 && !source.EventOperations.Any(op => op.Matter != null))
                Fail(nodes[target].Path, "所选来源没有该人物接触、交锋或材料变化能力：" + source.Path);
            if (condition.Trigger == ConstructionChangeTrigger.ForceBelow && (source.ContactRole == ConstructionContactRole.Decoration || source.Strength <= 0))
                Fail(nodes[target].Path, "损耗条件需要有实际作用量并参与交锋的来源：" + source.Path);
            if (condition.Trigger is ConstructionChangeTrigger.SizeAbove or ConstructionChangeTrigger.SizeBelow && source.Growth.x < 0 &&
                !source.EventOperations.Any(op => op.Kind == ConstructionOperationKind.Grow))
                Fail(nodes[target].Path, "成长条件需要已有初始或条件生长过程：" + source.Path);
        }

        int Resolve(int current, string reference)
        {
            if (reference == "body") return -1;
            int original = current;
            if (reference == "root" || reference.StartsWith("root/", StringComparison.Ordinal))
            {
                if (paths.TryGetValue(reference, out int absolute)) return absolute;
                Fail(nodes[current].Path, "找不到事件来源：" + reference);
            }
            string rest = reference;
            while (rest == ".." || rest.StartsWith("../", StringComparison.Ordinal))
            {
                current = nodes[current].Parent;
                if (current < 0) Fail(nodes[original].Path, "事件来源超出根部：" + reference);
                rest = rest.Length == 2 ? "" : rest.Substring(3);
            }
            if (rest == "" || rest == ".") return current;
            if (rest.StartsWith("./", StringComparison.Ordinal)) rest = rest.Substring(2);
            if (paths.TryGetValue(nodes[current].Path + "/" + rest, out int relative)) return relative;
            Fail(nodes[current].Path, "找不到事件来源：" + reference);
            return -1;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Random = System.Random;

namespace Cultiway.Content.Skills;

/// <summary>受理时实际可用的材料种类与件数，不保存物件或库存引用。</summary>
public sealed class SynthesisInventoryMaterial
{
    /// <summary>当时已经存在的物品形状。</summary>
    public readonly string Shape;
    /// <summary>在构造单次预算内可取用的实际件数。</summary>
    public readonly int Count;
    /// <summary>冻结外部输入，旧请求没有该列表。</summary>
    /// <param name="shape">稳定形状。</param><param name="count">实际件数。</param>
    [JsonConstructor]
    public SynthesisInventoryMaterial(string shape, int count)
    {
        if (string.IsNullOrWhiteSpace(shape) || shape.Length > 256 || count < 1 || count > 64)
            throw new ArgumentException("创作材料需要真实形状及1至64件的数量");
        Shape = shape; Count = count;
    }
}

/// <summary>从实际库存输入构造普通消耗步骤，原术与受理时输入保持。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>只采集当前可用原材料，排序和截取遵守创作预算。</summary>
    /// <param name="actor">真实作者。</param>
    private static SynthesisInventoryMaterial[] CaptureInventoryMaterials(ActorExtend actor) => actor.GetItems()
        .Where(ConstructedInventoryInputs.IsMaterial).GroupBy(item => item.GetComponent<ItemShape>().shape_id)
        .OrderBy(group => group.Key, StringComparer.Ordinal).Take(128)
        .Select(group => new SynthesisInventoryMaterial(group.Key, Math.Min(64, group.Count()))).ToArray();

    /// <summary>只在新规则使用独立随机源，不改变旧请求的选择及费用。</summary>
    /// <param name="definition">独立草稿。</param><param name="seed">冻结种子。</param><param name="resources">冻结材料。</param>
    private static AbilityDefinition GenerateInventory(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x153d8b27));
        if (random.NextDouble() >= .35d) return definition;
        var plan = AbilityCompiler.Compile(definition, false);
        return CanVaryInventory(definition, resources, plan) ? VaryInventory(definition, random, resources, plan) : definition;
    }

    /// <summary>选择已有且只展开一份的部位，给实际消耗留出执行窗口。</summary>
    /// <param name="definition">已有草稿。</param><param name="plan">相同编译结果。</param>
    private static (ConstructionNode Node, string Path, int Index)[] InventorySites(AbilityDefinition definition, CompiledAbility plan) =>
        ConstructionContactEdits.Parts(definition.Root).Select(part => (part.Node, part.Path,
            Index: Array.FindIndex(plan.Nodes, node => node.DeclarationPath == part.Path)))
        .Where(part => part.Index >= 0 && part.Node.Activation == null && plan.Nodes.Count(node => node.DeclarationPath == part.Path) == 1 &&
            plan.Nodes[part.Index].Presence.y - plan.Nodes[part.Index].Presence.x >= .3f &&
            plan.Nodes[part.Index].EventOperations.Length < ConstructionBudget.MaximumEventOperations).ToArray();

    /// <summary>已有声明先占用冻结件数，不从同一批数量重复创建新需求。</summary>
    /// <param name="definition">已有资料。</param><param name="resources">冻结材料。</param>
    private static SynthesisInventoryMaterial[] InventoryChoices(AbilityDefinition definition, SynthesisResources resources)
    {
        var declared = definition.Inventory ?? Array.Empty<ConstructionInventoryInput>();
        return resources.InventoryMaterials.Select(material => (material.Shape,
                Count: material.Count - declared.Where(input => input.Shape == material.Shape).Sum(input => input.Count)))
            .Where(material => material.Count > 0).Select(material => new SynthesisInventoryMaterial(material.Shape, material.Count)).ToArray();
    }

    /// <summary>只提供当前真实材料及步骤预算允许的改造。</summary>
    /// <param name="definition">已有结构。</param><param name="resources">冻结材料。</param><param name="plan">同一完整编译。</param>
    private static bool CanVaryInventory(AbilityDefinition definition, SynthesisResources resources, CompiledAbility plan) =>
        (definition.Inventory?.Length ?? 0) < 16 && (definition.Inventory?.Sum(input => input.Count) ?? 0) < 64 &&
        plan.Steps.Length < ConstructionBudget.MaximumSteps && InventoryChoices(definition, resources).Length > 0 && InventorySites(definition, plan).Length > 0;

    /// <summary>声明本次需要的件数，再建立真实消耗及可被后续引用的完成输出。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">固定选择。</param>
    /// <param name="resources">原材料输入。</param><param name="plan">同一原编译。</param>
    private static AbilityDefinition VaryInventory(AbilityDefinition definition, Random random, SynthesisResources resources, CompiledAbility plan)
    {
        var choices = InventoryChoices(definition, resources); var material = choices[random.Next(choices.Length)];
        var sites = InventorySites(definition, plan); var site = sites[random.Next(sites.Length)];
        var declared = definition.Inventory ?? Array.Empty<ConstructionInventoryInput>();
        string input = Enumerable.Range(0, declared.Length + 1).Select(index => "material_" + index).First(id => declared.All(value => value.Id != id));
        var names = new HashSet<string>(DerivationNodes(definition.Root).Select(node => node.Id).Concat(definition.Steps.Select(step => step.Id))
            .Concat(DerivationNodes(definition.Root).SelectMany(node => node.Operations).Select(operation => operation.Id)), StringComparer.Ordinal);
        string stepId = Enumerable.Range(0, names.Count + 1).Select(index => "consume_" + index).First(id => !names.Contains(id));
        int maximum = Math.Min(Math.Min(material.Count, 3), 64 - declared.Sum(value => value.Count));
        int count = 1 + random.Next(maximum);
        definition.Inventory = declared.Concat(new[] { new ConstructionInventoryInput { Id = input, Shape = material.Shape, Count = count } }).ToArray();
        definition.Steps = definition.Steps.Concat(new[] { new ConstructionStep
        {
            Id = stepId, Operation = new ConstructionOperation { Kind = ConstructionOperationKind.ConsumeInventory,
                At = plan.Nodes[site.Index].Presence.x + .1f, InventoryUse = new ConstructionInventoryUse { Input = input, Count = 1 + random.Next(count) } },
            Inputs = new() { ["part"] = new() { Node = site.Path } }
        } }).ToArray();
        return definition;
    }
}

/// <summary>旧请求保留完整结果和输入，新请求才冻结原材料资料。</summary>
internal sealed class CreationV26ToV27 : ISaveMigration
{
    /// <summary>已接通结束去向的正式人物区段。</summary>
    public int FromVersion => 26;
    /// <summary>不改变旧规则和已选输入。</summary><param name="data">原保存资料。</param>
    public JObject Apply(JObject data) => data;
}

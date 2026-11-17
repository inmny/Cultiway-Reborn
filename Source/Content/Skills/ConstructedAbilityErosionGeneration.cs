using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Usage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>受理时支持侵蚀的材料名称，只保存生成选择所需的稳定资料。</summary>
public sealed class SynthesisErosionMaterial
{
    /// <summary>原材料及其明确物态。</summary>
    [JsonProperty(Required = Required.Always)] public readonly string Matter, State;

    /// <summary>在正式读取边界检查稳定名称，不从当前目录重选。</summary>
    /// <param name="matter">材料名称。</param><param name="state">明确物态。</param>
    [JsonConstructor]
    public SynthesisErosionMaterial(string matter, string state)
    {
        if (string.IsNullOrWhiteSpace(matter) || string.IsNullOrWhiteSpace(state) || matter.Length > 128 || state.Length > 128)
            throw new ArgumentException("创作侵蚀材料需要有效的材料及物态名称");
        Matter = matter; State = state;
    }
}

/// <summary>独立创作和原术衍生共用普通侵蚀、老化及原物件声明。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>使用独立随机源，前二十七版请求保持完整结果。</summary>
    /// <param name="definition">已有独立结构。</param><param name="seed">冻结种子。</param>
    /// <param name="resources">受理时完整输入。</param>
    private static AbilityDefinition GenerateErosionAndAging(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x692f71a3));
        if (random.NextDouble() >= .6d) return definition;
        var choices = new List<(string Name, Func<AbilityDefinition> Apply)>();
        AddErosionAndAgingChoices(definition, AbilityCompiler.Compile(definition, false), resources, random, choices);
        return choices.Count == 0 ? definition : choices[random.Next(choices.Count)].Apply();
    }

    /// <summary>先筛实际材料和接触窗口，再只改变选中的一种过程，其余结构及引用保持。</summary>
    /// <param name="definition">本次独占草稿。</param><param name="plan">同一编译结果。</param>
    /// <param name="resources">冻结目录。</param><param name="random">本次选择。</param>
    /// <param name="choices">与既有变化共同参与选择。</param>
    private static void AddErosionAndAgingChoices(AbilityDefinition definition, CompiledAbility plan, SynthesisResources resources,
        Random random, List<(string Name, Func<AbilityDefinition> Apply)> choices)
    {
        var sites = ConstructionContactEdits.Parts(definition.Root)
            .Select(part => (part.Node, part.Path, Index: Array.FindIndex(plan.Nodes, node => node.DeclarationPath == part.Path)))
            .Where(part => part.Index >= 0 && plan.Nodes.Count(node => node.DeclarationPath == part.Path) == 1 &&
                plan.Nodes[part.Index].Resource.Meshes.Length > 0 && plan.Nodes[part.Index].Volume > 0 &&
                plan.Nodes[part.Index].FlowSettings == null && plan.Nodes[part.Index].FragmentSource < 0 &&
                plan.Nodes[part.Index].RemainderRoot < 0 && !plan.Nodes[part.Index].BodyWindow &&
                plan.Nodes[part.Index].Presence.y - plan.Nodes[part.Index].Presence.x > .05f &&
                plan.Nodes[part.Index].EventOperations.Length < ConstructionBudget.MaximumEventOperations)
            .ToArray();
        var self = sites.Where(site => resources.ErosionMaterials.Any(material =>
            material.Matter == plan.Nodes[site.Index].Matter.Id && material.State == plan.Nodes[site.Index].Matter.State)).ToArray();
        if (self.Length > 0) choices.Add(("主体侵蚀", () =>
        {
            var site = self[random.Next(self.Length)];
            return VaryErosion(definition, site.Node, site.Index, plan, resources, random, false);
        }));
        if (sites.Length > 0 && resources.ErosionMaterials.Length > 0) choices.Add(("世界侵蚀", () =>
        {
            var site = sites[random.Next(sites.Length)];
            return VaryErosion(definition, site.Node, site.Index, plan, resources, random, true);
        }));
        var endings = EndingSites(definition, plan).Select(site => site.Path).ToArray();
        var residue = self.Where(site => SiteOperations(definition, site.Node, site.Index, plan)
            .Any(operation => operation.Kind == ConstructionOperationKind.Leave) || endings.Contains(site.Path)).ToArray();
        if (residue.Length > 0) choices.Add(("留置侵蚀", () =>
        {
            var site = residue[random.Next(residue.Length)];
            ConstructionOperation leave = SiteOperations(definition, site.Node, site.Index, plan)
                .FirstOrDefault(operation => operation.Kind == ConstructionOperationKind.Leave);
            if (leave == null)
            {
                leave = new ConstructionOperation { Kind = ConstructionOperationKind.Leave,
                    At = plan.Nodes[site.Index].Presence.x + (plan.Nodes[site.Index].Presence.y - plan.Nodes[site.Index].Presence.x) * .8f };
                site.Node.Operations = site.Node.Operations.Append(leave).ToArray();
            }
            leave.Residue ??= new ConstructionResidue();
            leave.Residue.Erosion = new ConstructionErosion { Fraction = .2f + (float)random.NextDouble() * .6f };
            leave.Residue.ErosionSeconds = 2 + random.Next(7);
            return definition;
        }));
        var contacts = CreationContactSites(definition).Where(site => site.Impact.Targets == SkillUseTargetRelation.Hostile).ToArray();
        foreach (string kind in new[] { "生灵老化", "装备腐蚀", "库存催朽" })
            if (contacts.Length > 0) choices.Add((kind, () =>
            {
                var site = contacts[random.Next(contacts.Length)];
                var impact = JsonConvert.DeserializeObject<ConstructionImpact>(JsonConvert.SerializeObject(site.Impact));
                if (kind == "生灵老化") impact.AgeYears = 1 + random.Next(40);
                else if (kind == "装备腐蚀") impact.EquipmentErosion = new ConstructionEquipmentErosion
                {
                    Fraction = .15f + (float)random.NextDouble() * .65f,
                    Slots = Enum.GetValues(typeof(EquipmentType)).Cast<EquipmentType>().OrderBy(_ => random.Next()).Take(1 + random.Next(2)).ToArray()
                };
                else impact.InventoryAging = new ConstructionInventoryAging
                {
                    Seconds = 5 + random.Next(56), MaximumItems = 1 + random.Next(3),
                    Shapes = resources.InventoryMaterials.Length > 0 && random.Next(2) == 0
                        ? new[] { resources.InventoryMaterials[random.Next(resources.InventoryMaterials.Length)].Shape } : null
                };
                site.Set(impact); return definition;
            }));
    }

    /// <summary>原部位及独立命名步骤共用同一选中部位，不通过改写依赖制造完成。</summary>
    /// <param name="definition">完整草稿。</param><param name="node">实际声明。</param>
    /// <param name="index">已展开部位。</param><param name="plan">同一编译结果。</param>
    private static IEnumerable<ConstructionOperation> SiteOperations(AbilityDefinition definition, ConstructionNode node, int index,
        CompiledAbility plan) => node.Operations.Concat(definition.Steps.Where(step =>
            plan.Steps.Any(actual => actual.Node == index && actual.OperationId == null && actual.Id == step.Id)).Select(step => step.Operation));

    /// <summary>已有过程只改参数，新过程追加声明，世界过程选用受理时的材料目录。</summary>
    /// <param name="definition">独立草稿。</param><param name="node">选中声明。</param><param name="index">实际部位。</param>
    /// <param name="plan">原编译结果。</param><param name="resources">冻结目录。</param>
    /// <param name="random">固定随机源。</param><param name="world">是否选择世界材料。</param>
    private static AbilityDefinition VaryErosion(AbilityDefinition definition, ConstructionNode node, int index, CompiledAbility plan,
        SynthesisResources resources, Random random, bool world)
    {
        ConstructionOperation operation = SiteOperations(definition, node, index, plan).FirstOrDefault(value =>
            value.Kind == ConstructionOperationKind.Erode && (value.WorldMatter != null) == world);
        if (operation == null)
        {
            operation = new ConstructionOperation { Kind = ConstructionOperationKind.Erode, At = plan.Nodes[index].Presence.x };
            node.Operations = node.Operations.Append(operation).ToArray();
        }
        operation.Erosion = new ConstructionErosion { Fraction = .2f + (float)random.NextDouble() * .6f };
        operation.Seconds = world ? .5f + (float)random.NextDouble() * 2f :
            definition.Lifetime * (.1f + (float)random.NextDouble() * .2f);
        if (world)
        {
            var material = resources.ErosionMaterials[random.Next(resources.ErosionMaterials.Length)];
            operation.WorldMatter = new ConstructionWorldMatter
            { FromMatter = material.Matter, FromState = material.State, OwnedOnly = random.Next(2) == 0, MaximumVolume = 1 + random.Next(16) };
        }
        return definition;
    }
}

/// <summary>旧请求保留完整原输入，新请求才冻结侵蚀目录并采用规则28。</summary>
internal sealed class CreationV28ToV29 : ISaveMigration
{
    /// <summary>原生成材料交出请求区段。</summary>
    public int FromVersion => 28;
    /// <summary>不补写当前材料目录，不重新抽取。</summary><param name="data">原资料副本。</param>
    public JObject Apply(JObject data) => data;
}

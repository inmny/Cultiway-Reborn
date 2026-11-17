using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = System.Random;

namespace Cultiway.Content.Skills;

/// <summary>使用受理时冻结的真实成形动作，逐段建立部位并衔接后续过程。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>独立随机源只影响新规则，已有请求保留原选择和费用。</summary>
    /// <param name="definition">当前完整草稿。</param><param name="seed">冻结种子。</param>
    /// <param name="resources">冻结的模型及实际动作。</param>
    private static AbilityDefinition GenerateFormation(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x652a791b));
        if (random.NextDouble() >= .35d || definition.Root == null) return definition;
        var parents = FormationParents(definition, AbilityCompiler.Compile(definition, false));
        return parents.Length > 0 ? VaryFormation(definition, random, resources, parents) : definition;
    }

    /// <summary>单一实际部位提供明确完成输出，保留整招份数及层级上限。</summary>
    /// <param name="definition">已有完整结构。</param>
    /// <param name="plan">已经编译的同一结构，复用实际展开关系。</param>
    private static (ConstructionNode Node, string Path, float Scale)[] FormationParents(AbilityDefinition definition, CompiledAbility plan)
    {
        if (definition.Root == null || definition.Lifetime < 1.2f) return Array.Empty<(ConstructionNode, string, float)>();
        if (plan.FormCount >= AbilityCompiler.MaxForms / 2) return Array.Empty<(ConstructionNode, string, float)>();
        var single = new HashSet<string>(plan.Nodes.GroupBy(form => form.DeclarationPath).Where(group => group.Count() == 1)
            .Select(group => group.Key), StringComparer.Ordinal);
        return ConstructionContactEdits.Parts(definition.Root).Where(part => !string.IsNullOrEmpty(part.Node.Form) &&
            single.Contains(part.Path) && part.Path.Count(character => character == '/') < ConstructionBudget.MaximumDepth).ToArray();
    }

    /// <summary>先建立真实成形子体，再由动作完成输出启动运动或换材，原部位和步骤保持。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">本次固定选择。</param>
    /// <param name="resources">受理时已经固定的成形资料。</param>
    /// <param name="parents">已经核对的单一实际部位及路径。</param>
    private static AbilityDefinition VaryFormation(AbilityDefinition definition, Random random, SynthesisResources resources,
        (ConstructionNode Node, string Path, float Scale)[] parents)
    {
        var parent = parents[random.Next(parents.Length)];
        var forms = resources.Forms.Where(form => form.Formations?.Length > 0).ToArray();
        SynthesisForm resource = forms[random.Next(forms.Length)];
        var names = new HashSet<string>(DerivationNodes(definition.Root).Select(node => node.Id)
            .Concat(definition.Steps.Select(step => step.Id)), StringComparer.Ordinal);
        string id = Enumerable.Range(0, AbilityCompiler.MaxForms + definition.Steps.Length + 1)
            .Select(index => "forming_" + index).First(name => !names.Contains(name) && !names.Contains(name + "_continue"));
        string path = parent.Path + "/" + id;
        float seconds = Mathf.Min(definition.Lifetime * .45f, .6f + (float)random.NextDouble());
        float size = .6f + (float)random.NextDouble() * .6f;
        bool contact = parent.Node.ContactRole == ConstructionContactRole.Attack && random.Next(3) == 0;
        var child = new ConstructionNode
        {
            Id = id, Form = resource.Id, Matter = parent.Node.Matter, State = parent.Node.State,
            // 沿用创作器的地图尺寸计算，附着时抵消累计声明倍率，事件处独立生成时使用自身尺寸。
            Scale = size / resource.Size / (contact ? 1f : parent.Scale),
            Activation = contact ? new ConstructionActivation() : null,
            Operations = new[] { new ConstructionOperation
            {
                Id = "formation", Kind = ConstructionOperationKind.Animate,
                Clip = resource.Formations[random.Next(resource.Formations.Length)], Seconds = seconds
            } }
        };
        var next = random.Next(3) switch
        {
            0 => new ConstructionOperation { Kind = ConstructionOperationKind.Fly, Vector = new[] { 0f, 0f, 3f + (float)random.NextDouble() * 4f } },
            1 => new ConstructionOperation { Kind = ConstructionOperationKind.Spin, Vector = new[] { 0f, 0f, 30f + (float)random.NextDouble() * 90f } },
            _ => new ConstructionOperation { Kind = ConstructionOperationKind.ConvertMatter, Matter = child.Matter == "iron" ? "ice" : "iron" }
        };
        parent.Node.Children = parent.Node.Children.Concat(new[] { child }).ToArray();
        definition.Steps = definition.Steps.Concat(new[] { new ConstructionStep
        {
            Id = id + "_continue", Operation = next,
            Inputs = new()
            {
                ["part"] = new() { Node = path },
                ["after"] = new() { Node = path, Operation = "formation", Output = "animation", Stage = ConstructionOutputStage.Completed }
            }
        } }).ToArray();
        return definition;
    }
}

/// <summary>旧请求保留完整输入，新请求才选择逐段成形动作。</summary>
internal sealed class CreationV23ToV24 : ISaveMigration
{
    /// <summary>第二十三版正式人物请求区段。</summary>
    public int FromVersion => 23;
    /// <summary>不补选当前模型或重新定价。</summary><param name="data">原完整请求。</param>
    public JObject Apply(JObject data) => data;
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = System.Random;

namespace Cultiway.Content.Skills;

/// <summary>从受理时已有控制构造形变，不把模型选择留到请求完成时。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>新规则使用独立随机源，原术衍生继续共用同一形变构造。</summary>
    /// <param name="definition">当前独占结构。</param><param name="seed">固定种子。</param>
    /// <param name="resources">冻结的模型与真实控制。</param>
    private static AbilityDefinition GenerateDeformation(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x175d862b));
        if (random.NextDouble() >= .35d || definition.Root == null) return definition;
        CompiledAbility plan = AbilityCompiler.Compile(definition, false);
        return CanVaryDeformation(definition, resources, plan) ? VaryDeformation(definition, random, resources, plan) : definition;
    }

    /// <summary>单一实际部位提供明确输出，已有动态流散和制作阶段标记保持自己的规则。</summary>
    /// <param name="definition">原有完整结构。</param><param name="resources">冻结控制。</param><param name="plan">同一编译结果。</param>
    private static (ConstructionNode Node, string Path, SynthesisForm Form)[] DeformationSites(AbilityDefinition definition, SynthesisResources resources, CompiledAbility plan)
    {
        var forms = resources.Forms.Concat(resources.DeformationForms).Where(form => form.Deformable).ToArray();
        return ConstructionContactEdits.Parts(definition.Root).Select(part => (part.Node, part.Path,
                Form: forms.FirstOrDefault(form => form.Id == part.Node.Form)))
            .Where(part => part.Form != null && string.IsNullOrEmpty(part.Node.ActiveFromMarker) && string.IsNullOrEmpty(part.Node.ActiveUntilMarker) &&
                !AbilityMatters.Resolve(part.Node.Matter, part.Node.State, "形变来源").HoldsShape &&
                plan.Nodes.Count(actual => actual.DeclarationPath == part.Path) == 1 &&
                plan.Nodes.Single(actual => actual.DeclarationPath == part.Path).FlowSettings == null).ToArray();
    }

    /// <summary>既可改变原有可变主体，也可在有限父结构上建立新的可变部位。</summary>
    /// <param name="definition">实际结构。</param><param name="resources">冻结输入。</param><param name="plan">已有展开。</param>
    private static bool CanVaryDeformation(AbilityDefinition definition, SynthesisResources resources, CompiledAbility plan) =>
        definition.Root != null && definition.Lifetime >= 1.2f &&
        (DeformationSites(definition, resources, plan).Length > 0 ||
         FormationParents(definition, plan).Any(parent => plan.Nodes.Single(actual => actual.DeclarationPath == parent.Path).FlowSettings == null));

    /// <summary>建立明确控制目标，已有成形则等待它真正完成，原步骤和原部位保持。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">当前固定选择。</param>
    /// <param name="resources">冻结控制输入。</param><param name="plan">同一原结构的编译结果。</param>
    private static AbilityDefinition VaryDeformation(AbilityDefinition definition, Random random, SynthesisResources resources, CompiledAbility plan)
    {
        var nodes = DerivationNodes(definition.Root).ToArray();
        var names = new HashSet<string>(nodes.Select(node => node.Id).Concat(definition.Steps.Select(step => step.Id))
            .Concat(nodes.SelectMany(node => node.Operations).Select(operation => operation.Id)), StringComparer.Ordinal);
        string id = Enumerable.Range(0, names.Count + 1).Select(index => "deforming_" + index)
            .First(name => !names.Contains(name) && !names.Contains(name + "_part") && !names.Contains(name + "_formation"));
        var sites = DeformationSites(definition, resources, plan);
        ConstructionNode node; string path; SynthesisForm form;
        if (sites.Length > 0)
        {
            var site = sites[random.Next(sites.Length)]; node = site.Node; path = site.Path; form = site.Form;
        }
        else
        {
            var parents = FormationParents(definition, plan).Where(parent => plan.Nodes.Single(actual => actual.DeclarationPath == parent.Path).FlowSettings == null).ToArray();
            var parent = parents[random.Next(parents.Length)];
            var forms = resources.Forms.Concat(resources.DeformationForms).Where(candidate => candidate.Deformable).ToArray();
            form = forms[random.Next(forms.Length)];
            node = new ConstructionNode
            {
                Id = id + "_part", Form = form.Id, Matter = parent.Node.Matter, State = parent.Node.State,
                Scale = (.8f + (float)random.NextDouble() * .6f) / form.Size / parent.Scale
            };
            if (AbilityMatters.Resolve(node.Matter, node.State, "形变新部位").HoldsShape) { node.Matter = "wood"; node.State = null; }
            parent.Node.Children = parent.Node.Children.Concat(new[] { node }).ToArray(); path = parent.Path + "/" + node.Id;
        }
        var shape = new ConstructionDeformation();
        shape.Spacing[random.Next(3)] = .3f + (float)random.NextDouble() * 1.4f;
        var control = new ConstructionControlTarget { Control = form.Controls[random.Next(form.Controls.Length)] };
        control.Rotation[random.Next(3)] = -25f + (float)random.NextDouble() * 50f;
        shape.Controls = new[] { control };
        var step = new ConstructionStep
        {
            Id = id, Operation = new ConstructionOperation { Kind = ConstructionOperationKind.Deform,
                At = .15f, Seconds = Mathf.Min(definition.Lifetime * .4f, .7f + (float)random.NextDouble() * .5f), Deformation = shape },
            Inputs = new() { ["part"] = new() { Node = path } }
        };
        var formation = node.Operations.FirstOrDefault(operation => operation.Kind == ConstructionOperationKind.Animate && form.Formations?.Contains(operation.Clip) == true);
        if (formation != null)
        {
            if (formation.Id == null) formation.Id = id + "_formation";
            step.Inputs["after"] = new() { Node = path, Operation = formation.Id, Output = "animation", Stage = ConstructionOutputStage.Completed };
        }
        definition.Steps = definition.Steps.Concat(new[] { step }).ToArray();
        return definition;
    }
}

/// <summary>旧请求保留原规则与全部输入，新请求才采集自由控制。</summary>
internal sealed class CreationV24ToV25 : ISaveMigration
{
    /// <summary>第二十四版正式人物区段。</summary>
    public int FromVersion => 24;
    /// <summary>原样保留旧等待请求。</summary><param name="data">旧完整输入。</param>
    public JObject Apply(JObject data) => data;
}

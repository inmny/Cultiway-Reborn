using System;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = System.Random;

namespace Cultiway.Content.Skills;

/// <summary>从真实生成部位创作结束后的去向，继续使用共同材料和支付份额。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>仅新规则补回自由结构的余量后段，已有流散、收束及留置选择保持。</summary>
    /// <param name="definition">当前独占草稿。</param><param name="seed">冻结种子。</param>
    private static AbilityDefinition GenerateRemainder(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x284d731b));
        if (random.NextDouble() >= .4d) return definition;
        var plan = AbilityCompiler.Compile(definition, false);
        return RemainderSites(definition, plan).Length == 0 ? definition : VaryRemainder(definition, random, plan);
    }

    /// <summary>只选择实际单份的生成末端，保留原事件依赖及已经声明的结束过程。</summary>
    /// <param name="definition">已有资料。</param><param name="plan">同一完整编译结果。</param>
    private static (ConstructionNode Node, string Path, float Scale, int Index)[] EndingSites(AbilityDefinition definition, CompiledAbility plan)
    {
        return ConstructionContactEdits.Parts(definition.Root).Select(part =>
                (part.Node, part.Path, part.Scale, Index: Array.FindIndex(plan.Nodes, actual => actual.DeclarationPath == part.Path)))
            .Where(part => part.Index >= 0 && plan.Nodes.Count(actual => actual.DeclarationPath == part.Path) == 1 &&
                part.Node.Children.Length == 0 && part.Node.Activation == null && plan.Nodes[part.Index].Resource.Meshes.Length > 0 &&
                !plan.Nodes[part.Index].HasRay && plan.Nodes[part.Index].Hold.Seconds == 0 &&
                plan.Nodes[part.Index].Presence.y - plan.Nodes[part.Index].Presence.x >= 1 &&
                plan.Nodes[part.Index].Recovery == null && plan.Nodes[part.Index].Residue == null &&
                !plan.Nodes[part.Index].EventOperations.Any(operation => operation.Kind is ConstructionOperationKind.Recover or ConstructionOperationKind.Leave) &&
                !plan.Steps.Any(step => step.Node == part.Index && step.Action.Kind is ConstructionOperationKind.Recover or ConstructionOperationKind.Leave))
            .ToArray();
    }

    /// <summary>余量需要真实攻击或屏障来源及新部位预算，装饰没有可分配力量。</summary>
    /// <param name="definition">当前资料。</param><param name="plan">已编译的同一结构。</param>
    private static (ConstructionNode Node, string Path, float Scale, int Index)[] RemainderSites(AbilityDefinition definition, CompiledAbility plan) =>
        plan.FormCount >= AbilityCompiler.MaxForms / 2 ? Array.Empty<(ConstructionNode, string, float, int)>() :
        EndingSites(definition, plan).Where(part => part.Node.ContactRole != ConstructionContactRole.Decoration &&
            part.Path.Count(character => character == '/') < ConstructionBudget.MaximumDepth &&
            plan.Nodes[part.Index].Presence.y > .5f && plan.Nodes[part.Index].Fracture == null &&
            !plan.Steps.Any(step => step.Node == part.Index && step.Action.Kind == ConstructionOperationKind.Fracture)).ToArray();

    /// <summary>一至两份独立后段只接受共同交接，尺寸按来源实际累计倍率确定。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">固定选择。</param><param name="plan">原编译结果。</param>
    private static AbilityDefinition VaryRemainder(AbilityDefinition definition, Random random, CompiledAbility plan)
    {
        var sites = RemainderSites(definition, plan); var site = sites[random.Next(sites.Length)];
        var source = site.Node; var presence = plan.Nodes[site.Index].Presence;
        source.DisappearAt = Mathf.Min(presence.y, Mathf.Max(presence.x + .5f, definition.Lifetime * .6f));
        // 保留原操作和命名引用，来源结束时尚未完成的操作继续按真实中断处理。
        source.Children = Enumerable.Range(0, random.Next(1, 3)).Select(index => new ConstructionNode
        {
            Id = "remnant_" + index, Form = source.Form, Matter = source.Matter, State = source.State,
            Surface = source.Surface, Tint = source.Tint == null ? null : (float[])source.Tint.Clone(),
            Scale = site.Scale * (.55f + (float)random.NextDouble() * .25f),
            Strength = 1 + random.Next(3), ContactRole = source.ContactRole, Impact = source.Impact,
            Interception = source.Interception, ReflectionLimit = source.ReflectionLimit,
            BlocksGroundActors = source.BlocksGroundActors, EnclosesActors = source.EnclosesActors, MediumChannel = source.MediumChannel,
            SourceLoss = source.SourceLoss,
            Activation = new ConstructionActivation { Trigger = ConstructionChangeTrigger.End, InheritRemainder = true }
        }).ToArray();
        return definition;
    }

    /// <summary>流散保留已有运动，缺少水平表达的投送及内部形变继续用各自的独立后段。</summary>
    /// <param name="definition">已有资料。</param><param name="plan">同一编译结果。</param>
    private static (ConstructionNode Node, string Path, float Scale, int Index)[] FlowCreationSites(AbilityDefinition definition, CompiledAbility plan) =>
        EndingSites(definition, plan).Where(part => plan.Nodes[part.Index].FlowSettings == null && plan.Nodes[part.Index].Fracture == null &&
            part.Node.Operations.All(FlowCreationOperation) &&
            plan.Steps.Where(step => step.Node == part.Index).All(step => FlowCreationOperation(definition.Steps.FirstOrDefault(declared => declared.Id == step.Id)?.Operation ??
                part.Node.Operations.First(operation => operation.Id == step.OperationId)))).ToArray();

    /// <summary>与实际水层相容的共同操作，完整三维投送保留原规则。</summary>
    /// <param name="operation">原操作。</param>
    private static bool FlowCreationOperation(ConstructionOperation operation) => operation.Kind is not
        (ConstructionOperationKind.Deform or ConstructionOperationKind.Fracture or ConstructionOperationKind.Flow) &&
        (operation.Kind != ConstructionOperationKind.Fly || operation.Path == null && operation.Vector[2] == 0 &&
            (operation.Acceleration == null || operation.Acceleration[2] == 0));

    /// <summary>在现有生成末端追加真实流散、收束或留置，旧部位及原步骤继续保留。</summary>
    /// <param name="definition">独立草稿。</param><param name="random">固定选择。</param>
    /// <param name="plan">原完整编译结果。</param><param name="kind">明确去向。</param>
    private static AbilityDefinition VaryEnding(AbilityDefinition definition, Random random, CompiledAbility plan, ConstructionOperationKind kind)
    {
        var sites = kind == ConstructionOperationKind.Flow ? FlowCreationSites(definition, plan) : EndingSites(definition, plan);
        var site = sites[random.Next(sites.Length)]; var presence = plan.Nodes[site.Index].Presence;
        float span = presence.y - presence.x, at = presence.x + span * .6f;
        var operation = new ConstructionOperation { Kind = kind, At = at };
        if (kind == ConstructionOperationKind.Flow)
        {
            operation.At = presence.x + span * .3f; operation.Flow = new ConstructionFlow { Cells = 16 };
            site.Node.Operations = site.Node.Operations.Concat(new[] { new ConstructionOperation
            {
                Kind = ConstructionOperationKind.ConvertMatter, At = presence.x + span * .2f, Matter = "water", State = "liquid"
            } }).ToArray();
        }
        if (kind == ConstructionOperationKind.Recover)
        {
            operation.Seconds = span * .25f;
            operation.Recovery = new ConstructionRecovery { Efficiency = .5f + (float)random.NextDouble() * .4f };
        }
        if (kind == ConstructionOperationKind.Leave)
            operation.Residue = new ConstructionResidue { Lifetime = 8 + random.Next(17) };
        site.Node.Operations = site.Node.Operations.Concat(new[] { operation }).ToArray();
        return definition;
    }
}

/// <summary>旧请求保持完整输入，当前新请求才使用结束去向改造。</summary>
internal sealed class CreationV25ToV26 : ISaveMigration
{
    /// <summary>已冻结自由形变的正式人物区段。</summary>
    public int FromVersion => 25;
    /// <summary>原样保留旧请求。</summary><param name="data">旧完整资料。</param>
    public JObject Apply(JObject data) => data;
}

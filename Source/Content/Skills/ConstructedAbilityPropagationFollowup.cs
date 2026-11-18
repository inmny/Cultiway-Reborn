using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>只在本次创作确实已有传播或讯号来源时，有机会在到达后改变主体。</summary>
    private static AbilityDefinition GeneratePropagationFollowup(AbilityDefinition definition, int seed)
    {
        var random = new Random(unchecked(seed ^ 0x42590ab1));
        if (random.Next(2) != 0) return definition;
        var choices = new List<(string Name, Func<AbilityDefinition> Apply)>();
        AddPropagationChoices(definition, ValidateDefinition(definition, cache: false), random, choices);
        return choices.Count == 0 ? definition : choices[0].Apply();
    }

    /// <summary>从真实存在的单一主体选到达条件，不替原术添加虚构的水路、落点或收件人。</summary>
    private static void AddPropagationChoices(AbilityDefinition draft, CompiledAbility plan, Random random,
        List<(string Name, Func<AbilityDefinition> Apply)> choices)
    {
        var arrivals = new List<(ConstructionNode Node, ConstructionChangeTrigger Trigger, float Threshold, string[] Materials)>();
        foreach (var part in ConstructionContactEdits.Parts(draft.Root))
        {
            CompiledForm node = plan.Nodes.FirstOrDefault(actual => actual.DeclarationPath == part.Path);
            if (node == null || node.Path != part.Path || plan.Nodes.Count(actual => actual.DeclarationPath == part.Path) != 1 ||
                node.Resource.Meshes.Length == 0 || part.Node.Operations.Length >= ConstructionBudget.MaximumNodeOperations ||
                node.EventOperations.Length >= ConstructionBudget.MaximumEventOperations ||
                plan.Budget.Steps >= ConstructionBudget.MaximumSteps) continue;
            string[] materials = new[] { "ice", "iron", "wood" }
                .Where(matter => matter != node.Matter.Id && !part.Node.Operations.Any(operation => operation.Matter == matter)).ToArray();
            if (materials.Length == 0) continue;
            foreach (CompiledEventOperation operation in node.EventOperations)
            {
                CompiledPropagation ground = operation.Propagation;
                if (ground == null) continue;
                float available = Math.Min(ground.Range,
                    (node.Presence.y - Math.Max(operation.At, node.Presence.x)) * ground.Speed);
                if (available >= 2f)
                    arrivals.Add((part.Node, ConstructionChangeTrigger.GroundRouteDistance, Math.Min(6f, available * .5f), materials));
            }
            bool water = node.Conduction != null || node.EventOperations.Any(operation => operation.Conduction != null);
            if (water) arrivals.Add((part.Node, ConstructionChangeTrigger.WaterRouteArrival, 0, materials));
            bool signal = node.Impact != null ? node.Impact.Signal != null : node.BodyWindow
                ? plan.Body.Stages.Any(stage => stage.Impact.Signal != null) : plan.Impact.Signal != null;
            if (signal && (node.AffectsActors || water))
                arrivals.Add((part.Node, ConstructionChangeTrigger.SignalDelivery, 0, materials));
        }
        if (arrivals.Count == 0) return;
        choices.Add(("传播接续", () =>
        {
            var arrival = arrivals[random.Next(arrivals.Count)];
            arrival.Node.Operations = arrival.Node.Operations.Append(new ConstructionOperation
            {
                Kind = ConstructionOperationKind.ConvertMatter,
                Matter = arrival.Materials[random.Next(arrival.Materials.Length)], State = "solid",
                When = new ConstructionCondition { Trigger = arrival.Trigger, Source = ".",
                    Threshold = arrival.Trigger == ConstructionChangeTrigger.GroundRouteDistance ? arrival.Threshold : null }
            }).ToArray();
            return draft;
        }));
    }
}

/// <summary>保留等待中请求的完整原术、版本、种子与冻结输入，由原规则处理。</summary>
internal sealed class CreationV33ToV34 : ISaveMigration
{
    public int FromVersion => 33;
    public JObject Apply(JObject source) => source;
}

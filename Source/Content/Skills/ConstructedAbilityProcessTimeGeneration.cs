using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private static AbilityDefinition GenerateProcessTime(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new Random(unchecked(seed ^ 0x44ac7f21));
        if (random.NextDouble() >= .45d) return definition;
        var choices = new List<(string Name, Func<AbilityDefinition> Apply)>();
        AddProcessTimeChoices(definition, ValidateDefinition(definition, cache: false), resources, random, choices);
        return choices.Count == 0 ? definition : choices[random.Next(choices.Count)].Apply();
    }

    /// <summary>给已经存在的侵蚀加时间安排，或添加明确筛选世界过程的步骤。</summary>
    private static void AddProcessTimeChoices(AbilityDefinition definition, CompiledAbility plan, SynthesisResources resources,
        Random random, List<(string Name, Func<AbilityDefinition> Apply)> choices)
    {
        var timed = new List<(ConstructionOperation Operation, float Seconds)>();
        var selectors = new List<string>();
        foreach (var part in ConstructionContactEdits.Parts(definition.Root))
        {
            int index = Array.FindIndex(plan.Nodes, node => node.DeclarationPath == part.Path);
            if (index < 0) continue;
            var form = plan.Nodes[index];
            foreach (var operation in SiteOperations(definition, part.Node, index, plan))
            {
                if (operation.Kind == ConstructionOperationKind.Erode && operation.Seconds >= .08f)
                    timed.Add((operation, operation.Seconds));
                if (operation.Kind == ConstructionOperationKind.Leave && operation.Residue?.Erosion != null &&
                    operation.Residue.ErosionSeconds >= .08f)
                    timed.Add((operation, operation.Residue.ErosionSeconds));
            }
            if (form.Resource.Meshes.Length > 0 && form.Volume > 0 && !form.BodyWindow && form.FragmentSource < 0 &&
                form.EventOperations.Length < ConstructionBudget.MaximumEventOperations &&
                plan.Nodes.Count(node => node.DeclarationPath == part.Path) == 1 &&
                plan.Steps.Count(step => step.Node == index && step.OperationId == null) + form.EventOperations.Length <
                ConstructionBudget.MaximumEventOperations)
                selectors.Add(part.Path);
        }
        if (timed.Count > 0) choices.Add(("过程时间", () =>
        {
            var (operation, seconds) = timed[random.Next(timed.Count)];
            var erosion = operation.Kind == ConstructionOperationKind.Leave
                ? operation.Residue.Erosion
                : operation.Erosion ??= new ConstructionErosion();
            erosion.TimeWindows = ProcessTimeFor(seconds, random);
            return definition;
        }));
        if (selectors.Count == 0 || plan.Steps.Length >= ConstructionBudget.MaximumSteps || resources.ErosionMaterials.Length == 0 ||
            definition.Steps.Any(step => step.Operation.Kind == ConstructionOperationKind.RetimeErosion)) return;
        choices.Add(("调整侵蚀节奏", () =>
        {
            var material = resources.ErosionMaterials[random.Next(resources.ErosionMaterials.Length)];
            string part = selectors[random.Next(selectors.Count)];
            var operation = new ConstructionOperation
            {
                Kind = ConstructionOperationKind.RetimeErosion,
                WorldMatter = new ConstructionWorldMatter
                {
                    FromMatter = material.Matter, FromState = material.State, OwnedOnly = random.Next(2) == 0,
                    MaximumVolume = 4 + random.Next(13)
                },
                TimeWindows = ProcessTimeFor(2f + (float)random.NextDouble(), random)
            };
            definition.Steps = definition.Steps.Append(new ConstructionStep
            {
                Id = NewId("retimeErosion", definition.Steps.Select(step => step.Id)), Operation = operation,
                Inputs = new() { ["part"] = new() { Node = part } }
            }).ToArray();
            return definition;
        }));
    }

    /// <summary>安排一次暂停和补偿提速；同一份余量走完后，以原完成条件收束。</summary>
    private static ConstructionProcessWindow[] ProcessTimeFor(float seconds, Random random)
    {
        float span = Math.Min(seconds, 4f);
        float start = span * (.06f + (float)random.NextDouble() * .08f);
        float pause = span * (.12f + (float)random.NextDouble() * .08f);
        return new[]
        {
            new ConstructionProcessWindow { At = start, Seconds = pause, Rate = 0f },
            new ConstructionProcessWindow { At = start + pause + span * .08f, Seconds = pause, Rate = 2f }
        };
    }
}

/// <summary>正式等待请求保留原规则、原术及材料输入，新请求才使用过程时间。</summary>
internal sealed class CreationV31ToV32 : ISaveMigration
{
    /// <summary>接入过程时间之前的正式人物区段。</summary>
    public int FromVersion => 31;

    /// <summary>原样保留等待请求。</summary>
    public JObject Apply(JObject data) => data;
}

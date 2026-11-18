using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>从当前真实可用形体衍生门和搬运步骤，不给旧创作改变随机序列。</summary>
    /// <param name="definition">已经生成的完整草稿。</param><param name="seed">受理时固定的种子。</param>
    /// <param name="resources">受理时冻结的形体目录。</param>
    private static AbilityDefinition GenerateMapping(AbilityDefinition definition, int seed, SynthesisResources resources)
    {
        var random = new System.Random(unchecked(seed ^ 0x21e46ac3));
        if (definition.Root == null || random.NextDouble() >= .32d) return definition;
        CompiledAbility plan = ValidateDefinition(definition, cache: false);
        var kinds = MappingChoices(definition, plan).ToArray();
        return kinds.Length == 0 ? definition : VaryMapping(definition, plan, kinds[random.Next(kinds.Length)], resources, random);
    }

    /// <summary>给现有术式增加可独立活动的一支；需要真实借用的法宝只在已有这种连接时出现。</summary>
    /// <param name="definition">保留原动作及引用的草稿。</param><param name="plan">原草稿的实际编译结果。</param>
    private static IEnumerable<ConstructionOperationKind> MappingChoices(AbilityDefinition definition, CompiledAbility plan)
    {
        ConstructionNode root = definition.Root;
        if (root == null || definition.Body != null || root.Activation != null || root.AppearAt > 0f ||
            definition.Binding?.ScaleByBody == true || definition.Steps.Length >= ConstructionBudget.MaximumSteps ||
            plan.FormCount > AbilityCompiler.MaxForms - 3 || plan.Nodes[0].HasRay || plan.Nodes[0].Presence.y < .65f ||
            !(root.Scale >= .08f && root.Scale <= 24f)) yield break;

        bool Has(ConstructionOperationKind kind) => definition.Steps.Any(step => step.Operation.Kind == kind);
        if (definition.Binding is { Anchor: AbilityAnchor.Carrier, Follow: true })
        {
            if (!Has(ConstructionOperationKind.MapArtifact)) yield return ConstructionOperationKind.MapArtifact;
            yield break;
        }
        var binding = definition.Binding;
        if (binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector or AbilityAnchor.Carrier) yield break;
        if (!Has(ConstructionOperationKind.MapSpace)) yield return ConstructionOperationKind.MapSpace;
        float height = (binding?.Height ?? 0f) + plan.Nodes[0].Offset.z;
        // 入口只放在脚边；落到更高处或需要专门身体支撑的技法仍可改为生成物搬运。
        if (Math.Abs(height) <= .7f && !Has(ConstructionOperationKind.MapActor)) yield return ConstructionOperationKind.MapActor;
    }

    /// <summary>复用共同门、接触和位置步骤。旧部位保留原次序、路径与行为。</summary>
    /// <param name="draft">独立草稿。</param><param name="plan">加入新部位前的实际姿态和材质。</param>
    /// <param name="kind">本次已选的双门用途。</param><param name="resources">冻结形体输入。</param>
    /// <param name="random">受理时确定的随机源。</param>
    private static AbilityDefinition VaryMapping(AbilityDefinition draft, CompiledAbility plan, ConstructionOperationKind kind,
        SynthesisResources resources, System.Random random)
    {
        bool actors = kind == ConstructionOperationKind.MapActor, artifact = kind == ConstructionOperationKind.MapArtifact;
        ConstructionNode root = draft.Root;
        CompiledForm rootForm = plan.Nodes[0];
        float orientation = (random.Next(2) == 0 ? -1f : 1f) * (35f + (float)random.NextDouble() * 75f);
        float distance = 3.5f + (float)random.NextDouble() * 3f;
        string stepName = NewId("crossGate", draft.Steps.Select(step => step.Id));
        var used = new HashSet<string>(root.Children.Select(node => node.Id), StringComparer.Ordinal);
        string entryId = NewId("gateEntry", used), exitId = NewId("gateExit", used), payloadId = NewId("gatePayload", used);
        float stage = actors && random.Next(2) == 0 ? .18f + (float)random.NextDouble() * .16f : 0f;
        Vector3 entrance = artifact ? Vector3.zero : new Vector3(1f, 0f,
            actors ? .26f - (draft.Binding?.Height ?? 0f) - rootForm.Offset.z : .25f);
        Vector3 end = entrance + Quaternion.AngleAxis(orientation, Vector3.forward) * Vector3.right * distance;
        Quaternion local = artifact ? Quaternion.identity : Quaternion.Inverse(rootForm.Rotation);
        float gateScale = Mathf.Clamp(1f / resources.Get("fireball").Size, .8f, 1.1f);
        var entry = MakePart(entryId, "fireball", entrance, .8f * gateScale, actors, 0f);
        var exit = MakePart(exitId, "fireball", end, .7f * gateScale, false, stage);
        exit.Rotation = Coordinates((local * Quaternion.AngleAxis(orientation, Vector3.forward)).eulerAngles);
        if (actors)
        {
            entry.Impact = new ConstructionImpact { DamageScale = 0f, Targets = plan.Impact.Targets };
            entry.Contact = new ConstructionContactPolicy { Mode = ConstructionContactMode.Interval, Interval = .12f };
        }
        var parts = new List<ConstructionNode> { entry, exit };
        var operation = new ConstructionOperation { Kind = kind, MappingRadius = 1.05f };
        string subject = artifact ? "root" : "root/" + (actors ? entryId : payloadId);
        if (actors)
        {
            operation.MappingCapacity = random.Next(1, 4);
            operation.MappingWaitSeconds = stage > 0f ? stage + .25f : null;
        }
        else operation.At = .16f;
        if (kind == ConstructionOperationKind.MapSpace)
        {
            // 搬运一枚独立生成物；通过之后的飞行仍是它自己的原动作。
            string form = new[] { "sword", "branch", "fireball" }[random.Next(3)];
            var payload = MakePart(payloadId, form, entrance, .8f / resources.Get(form).Size, true, 0f);
            payload.Matter = rootForm.Matter.Id;
            payload.State = rootForm.Matter.State;
            payload.Operations = payload.Operations.Append(new ConstructionOperation { Kind = ConstructionOperationKind.Fly, At = .20f,
                Vector = new[] { 3f + (float)random.NextDouble() * 3f, 0f, 0f } }).ToArray();
            parts.Add(payload);
        }
        root.Children = root.Children.Concat(parts).ToArray();
        var step = new ConstructionStep
        {
            Id = stepName, Operation = operation, Inputs = new()
            {
                ["part"] = new ConstructionOutputReference { Node = subject },
                ["exit"] = new ConstructionOutputReference { Node = "root/" + exitId }
            }
        };
        if (!actors) step.Inputs["entrance"] = new ConstructionOutputReference { Node = "root/" + entryId };
        draft.Steps = draft.Steps.Append(step).ToArray();
        return draft;

        ConstructionNode MakePart(string id, string shape, Vector3 offset, float scale, bool contact, float delay) => new()
        {
            Id = id, Form = shape, Offset = Coordinates(local * offset / root.Scale), Scale = scale / root.Scale,
            Rotation = Coordinates(local.eulerAngles), AppearAt = delay > 0f ? delay : null,
            DisappearAt = Math.Min(rootForm.Presence.y, 1.1f),
            ContactRole = contact ? ConstructionContactRole.Attack : ConstructionContactRole.Decoration,
            Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = delay } }
        };

        static float[] Coordinates(Vector3 value) => new[] { value.x, value.y, value.z };
    }

    private static string NewId(string baseId, IEnumerable<string> all)
    {
        string id = baseId;
        for (int suffix = 1; all.Contains(id); suffix++) id = baseId + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return id;
    }
}

/// <summary>原等待请求的规则、完整来源与资料原样保留。</summary>
internal sealed class CreationV30ToV31 : ISaveMigration
{
    /// <summary>旧映像创作区段。</summary>
    public int FromVersion => 30;

    /// <summary>原样迁移已经固定的请求。</summary><param name="data">完整持久化输入。</param>
    public JObject Apply(JObject data) => data;
}

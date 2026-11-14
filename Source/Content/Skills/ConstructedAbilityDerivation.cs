using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Newtonsoft.Json.Linq;

namespace Cultiway.Content.Skills;

/// <summary>从实际掌握的完整构造继续创作；变化仍由共用操作执行。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>在请求受理时固定一个原版本，不在排队后读取可能已改变的目录。</summary>
    /// <param name="actor">实际创造者。</param>
    /// <param name="seed">该次请求的确定种子。</param>
    /// <param name="sourceId">明确指定的已学原术；为空时自然选择。</param>
    private static AbilityDefinition CaptureCreationSource(ActorExtend actor, int seed, string sourceId)
    {
        string[] known = actor.E.TryGetComponent(out ConstructedKnowledge knowledge)
            ? knowledge.Ids
            : Array.Empty<string>();
        var random = new Random(unchecked(seed ^ 0x527c9a31));
        if (sourceId == null)
        {
            if (known.Length == 0 || random.NextDouble() >= .65d) return null;
            sourceId = known[random.Next(known.Length)];
        }

        if (!known.Contains(sourceId) || !definitions.TryGetValue(sourceId, out AbilityDefinition source))
            throw new ArgumentException("创作原术必须是此人真正掌握的构造", nameof(sourceId));
        return ConstructionEditing.Copy(source);
    }

    /// <summary>先列出当前实际可用的改变，固定选择一项，再形成独立来源及结果；失败不重抽。</summary>
    /// <param name="actor">此次独立创作的作者。</param>
    /// <param name="source">受理时保存的完整原术。</param>
    /// <param name="seed">可复现的选择种子。</param>
    /// <param name="inspiration">已经固定的角色倾向。</param>
    /// <param name="resources">受理时固定的实际身体与资源，旧请求为空。</param>
    private static AbilityDefinition Derive(ActorExtend actor, AbilityDefinition source, int seed,
        ConstructionInspiration inspiration, SynthesisResources resources)
    {
        CompiledAbility plan = ValidateDefinition(source, cache: false);
        AbilityDefinition draft = ConstructionEditing.Copy(source);
        var random = new Random(seed);
        var choices = new List<(string Name, Func<AbilityDefinition> Apply)>();
        ConstructionNode[] nodes = DerivationNodes(draft.Root).ToArray();
        ConstructionNode[] materialNodes = nodes.Where(n => n.ContactRole != ConstructionContactRole.Decoration &&
                                                            n.Operations.All(o =>
                                                                o.Kind != ConstructionOperationKind.Flow)).ToArray();
        if (materialNodes.Length > 0)
            choices.Add(("换材", () =>
            {
                ConstructionNode node = materialNodes[random.Next(materialNodes.Length)];
                string matter = inspiration.Matter(random);
                if (matter == node.Matter)
                {
                    string[] others = new[] { "iron", "wood", "water", "fire", "ice" }
                        .Where(value => value != node.Matter).ToArray();
                    matter = others[random.Next(others.Length)];
                }

                node.Matter = matter;
                node.State = null;
                node.Surface = null;
                return draft;
            }
            ));
        ConstructionNode[] replaceable = materialNodes.Where(n => n.Children.Length == 0 && n.Hold == null &&
                                                                  n.Operations.All(o =>
                                                                      o.Kind is not (ConstructionOperationKind.Animate
                                                                          or ConstructionOperationKind.Fracture)) &&
                                                                  n.Form is "sword" or "branch" or "hand").ToArray();
        if (replaceable.Length > 0)
            choices.Add(("换形", () =>
            {
                ConstructionNode node = replaceable[random.Next(replaceable.Length)];
                string[] others = new[] { "sword", "branch", "hand" }.Where(form => form != node.Form).ToArray();
                node.Form = others[random.Next(others.Length)];
                return draft;
            }
            ));
        ConstructionNode[] growers = nodes.Where(n => n.ContactRole == ConstructionContactRole.Attack &&
                                                      n.Children.All(c => c.Id != "contact_growth")).ToArray();
        if (growers.Length > 0 && draft.Lifetime >= 1.2f && plan.FormCount < AbilityCompiler.MaxForms / 2)
            choices.Add(("触生", () =>
            {
                AddContactGrowth(draft, growers[random.Next(growers.Length)]);
                return draft;
            }
            ));
        if (draft.Root != null)
        {
            if (draft.Binding?.Anchor != AbilityAnchor.Source || !draft.Binding.Follow ||
                draft.Root.Operations.Any(o =>
                    o.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Detach))
                choices.Add(("随身", () => ConstructionPlacementEdits.Follow(draft, AbilityAnchor.Source)));
            if (plan.FormCount < AbilityCompiler.MaxForms - 1)
                choices.Add(("环绕", () => ConstructionPlacementEdits.Orbit(draft, AbilityAnchor.Source,
                    1.5f + (float)random.NextDouble() * 2f, 2f + (float)random.NextDouble() * 3f)));
            if (plan.FormCount < AbilityCompiler.MaxForms / 2)
                choices.Add(("复生", () => ConstructionMagnitudeEdits.Repeat(draft, 2,
                    1f + (float)random.NextDouble(), .2f + (float)random.NextDouble() * .4f, reprice: inspiration.Rules >= 3)));
        }

        if (draft.Body != null && draft.Root == null && (inspiration.Rules <= 6 ? inspiration.CanProject :
                resources?.Bodies.Any(body => AbilityCompiler.Compile(body, cache: false).Body.Part.Id == plan.Body.Part.Id) == true))
            choices.Add(("显化", () => BodyConstructionEdits.Project(draft, new BodyProjection
            {
                Form = inspiration.Rules >= 7 && plan.Body.Part.Contact != BodyContactPart.Hand
                    ? resources.Forms.Where(form => form.Id != "hand").ElementAt(random.Next(resources.Forms.Length - 1)).Id : null,
                Resource = inspiration.Rules >= 7 ? resources.Resource : "wakan",
                Matter = inspiration.Matter(random),
                Speed = 7f + (float)random.NextDouble() * 3f,
                TurnRate = 180f + inspiration.Control * 180f,
                Cost = Math.Max(4f, draft.Cost)
            })));
        if (inspiration.Rules >= 5 && BodyConstructionEdits.RecoveryPoints(draft).Length > 0)
            choices.Add(("收势", () => GenerateBodyResponse(draft, random)));
        if (inspiration.Rules >= 6 && plan.Body?.Construction.MayContact == true &&
            draft.Body.ActiveUntil > draft.Body.ActiveFrom)
            choices.Add(("分段", () => GenerateBodyStages(draft, random)));
        if (inspiration.Rules >= 7 && plan.Body?.Part.HasPartRotation == true && resources != null)
            foreach (AbilityDefinition body in resources.Bodies)
            {
                string part = AbilityCompiler.Compile(body, cache: false).Body.Part.Id;
                if (new BodyPartSelection(part).HasPartRotation && part != plan.Body.Part.Id)
                    choices.Add(("改用实际部位", () => BodyConstructionEdits.RetargetPart(draft, part)));
            }
        if (choices.Count == 0) throw new InvalidDataException("原术当前没有已实现的可用结构改造");
        var selected = choices[random.Next(choices.Count)];
        AbilityDefinition result = ConstructionDefinitionLayers.FromSource(source, selected.Apply());
        if (inspiration.Rules >= 3)
            ConstructionPricing.Reprice(source, result, cost: result.Resource == source.Resource && result.Cost == source.Cost, cooldown: result.Cooldown == source.Cooldown);
        result.Id = "Cultiway.Created." + Guid.NewGuid().ToString("N");
        result.Revision = 1;
        result.Creator = actor.Base.id;
        result.Seed = seed;
        result.Parent = new AbilityParent
        {
            Id = source.Id,
            Revision = source.Revision,
            Creator = source.Creator,
            Change = selected.Name
        };
        ValidateDefinition(result, cache: false);
        return result;
    }

    /// <summary>按资料顺序遍历真实部位，不读取实例状态或图形对象。</summary>
    /// <param name="node">原术的当前结构；真实身体可以没有生成结构。</param>
    private static IEnumerable<ConstructionNode> DerivationNodes(ConstructionNode node)
    {
        if (node == null) yield break;
        yield return node;
        foreach (ConstructionNode child in node.Children)
            foreach (ConstructionNode descendant in DerivationNodes(child))
                yield return descendant;
    }
}

/// <summary>第二版请求未选择原术，按其原有独立创作继续，不从读档后的已学集合补选。</summary>
internal sealed class CreationV2ToV3 : ISaveMigration
{
    /// <summary>已有倾向输入、尚无原术选择的请求格式。</summary>
    public int FromVersion => 2;

    /// <summary>保留原有确定输入，明确本次请求没有单一原术。</summary>
    /// <param name="data">保存层提供的旧资料副本。</param>
    public JObject Apply(JObject data)
    {
        data["Source"] = JValue.CreateNull();
        return data;
    }
}

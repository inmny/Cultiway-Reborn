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
        if (inspiration.Rules >= 29)
        {
            var correspondenceSites = CorrespondenceSites(draft, plan);
            if (correspondenceSites.Length > 0)
                choices.Add(("映像传递", () => VaryCorrespondence(draft, plan, correspondenceSites, random)));
        }
        if (inspiration.Rules >= 28) AddErosionAndAgingChoices(draft, plan, resources, random, choices);
        if (inspiration.Rules >= 27 && MaterialTransferSites(draft, plan).Length > 0)
            choices.Add(("材料交出", () => VaryMaterialTransfer(draft, random, plan)));
        if (inspiration.Rules >= 26 && CanVaryInventory(draft, resources, plan))
            choices.Add(("库存材料", () => VaryInventory(draft, random, resources, plan)));
        if (inspiration.Rules >= 25)
        {
            if (RemainderSites(draft, plan).Length > 0)
                choices.Add(("余量续形", () => VaryRemainder(draft, random, plan)));
            if (FlowCreationSites(draft, plan).Length > 0)
                choices.Add(("液态流散", () => VaryEnding(draft, random, plan, ConstructionOperationKind.Flow)));
            if (EndingSites(draft, plan).Length > 0)
            {
                choices.Add(("收束回能", () => VaryEnding(draft, random, plan, ConstructionOperationKind.Recover)));
                choices.Add(("世界留置", () => VaryEnding(draft, random, plan, ConstructionOperationKind.Leave)));
            }
        }
        if (inspiration.Rules >= 24 && CanVaryDeformation(draft, resources, plan))
            choices.Add(("结构形变", () => VaryDeformation(draft, random, resources, plan)));
        if (inspiration.Rules >= 23)
        {
            var parents = FormationParents(draft, plan);
            if (parents.Length > 0)
                choices.Add(("逐段成形", () => VaryFormation(draft, random, resources, parents)));
        }
        bool preserveReferences = inspiration.Rules >= 16;
        bool preserveStarts = inspiration.Rules >= 22;
        bool placementAvailable = !preserveReferences || draft.Root != null &&
            ConstructionPlacementReferences.Rewrite(draft, plan, false, preserveStarts) == null;
        if (inspiration.Rules >= 21)
        {
            choices.Add(("存续规则", () => VaryPersistence(draft, random)));
            if (placementAvailable && draft.Root != null && draft.Body == null && !plan.Nodes[0].HasRay)
                choices.Add(("两点连接", () => VaryBetween(draft, random, preserveStarts)));
        }
        if (inspiration.Rules >= 20 && ClashCandidates(draft).Length > 0)
            choices.Add(("交锋规则", () => VaryClash(draft, random)));
        if (inspiration.Rules >= 19 && nodes.Any(node => !string.IsNullOrEmpty(node.Form)))
            choices.Add(("范围推拉", () => VaryRadialMotion(draft, random)));
        if (inspiration.Rules >= 18 && CreationContactSites(draft).Count > 0)
            choices.Add(("接触频率", () => VaryContactPolicy(draft, random)));
        if (inspiration.Rules >= 17 && resources.WorldActions.Length > 0 && CreationContactSites(draft).Count > 0)
            choices.Add(("世界接触", () => VaryWorldContacts(draft, random, resources)));
        if (inspiration.Rules >= 15 && CreationContactSites(draft).Any(site => site.Impact.Targets != Core.SkillLibV3.Usage.SkillUseTargetRelation.Self))
            choices.Add(("接触分送", () => VaryContactSpreads(draft, random, inspiration)));
        if (inspiration.Rules >= 14 && CreationContactSites(draft).Any(site => site.Impact.Targets != Core.SkillLibV3.Usage.SkillUseTargetRelation.Self))
            choices.Add(("接触结果", () => VaryContactResults(draft, random, inspiration)));
        if (inspiration.Rules >= 13 && CreationContactSites(draft).Any(site => site.Impact.Targets != Core.SkillLibV3.Usage.SkillUseTargetRelation.Self))
            choices.Add(("接触位移", () => VaryContactDisplacement(draft, random)));
        if (inspiration.Rules >= 12 && CreationContactSites(draft).Count > 0)
            choices.Add(("接触作用", () => VaryContactEffects(draft, random, inspiration)));
        if (inspiration.Rules >= 11)
        {
            ConstructionOperation[] flights = CreationFlights(draft, plan);
            if (flights.Length > 0)
                choices.Add(("飞行曲线", () =>
                {
                    GenerateFlightPath(flights[random.Next(flights.Length)], random);
                    return draft;
                }));
            ConstructionOperation[] goals = flights.Where(flight => flight.Path?.Space != ConstructionPathSpace.TargetLine).ToArray();
            if (goals.Length > 0)
                choices.Add(("飞行目标", () =>
                {
                    GenerateFlightGoal(goals[random.Next(goals.Length)], random);
                    return draft;
                }));
        }
        if (inspiration.Rules >= 10 && nodes.SelectMany(node => node.Operations).Concat(draft.Steps.Select(step => step.Operation)).Any(operation => operation.Ray != null))
            choices.Add(("射线变化", () => VaryRay(draft, random)));
        if (inspiration.Rules >= 10 && draft.Root != null && draft.Body == null && !plan.Nodes[0].HasRay && placementAvailable)
            choices.Add(("直接放置", () => ConstructionPlacementEdits.Place(draft, AbilityAnchor.Target, preserveReferences, preserveStarts)));
        if (inspiration.Rules >= 8 && nodes.Any(n => n.Operations.Any(o => o.Kind == ConstructionOperationKind.Propagate)))
            choices.Add(("传力变化", () => VaryGroundPropagation(draft, random)));
        if (inspiration.Rules >= 9 && nodes.Any(node => node.Distribution != null))
            choices.Add(("排布变化", () => VaryDistribution(draft, random)));
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
                                                                      o.Kind is not (ConstructionOperationKind.Animate or ConstructionOperationKind.Deform
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
        ConstructionNode[] growers = nodes.Where(n => (n.ContactRole == ConstructionContactRole.Attack ||
                                                       n.ContactRole == ConstructionContactRole.Barrier && n.Impact != null) &&
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
            if (placementAvailable && (draft.Binding?.Anchor != AbilityAnchor.Source || !draft.Binding.Follow ||
                draft.Root.Operations.Any(o =>
                    o.Kind is ConstructionOperationKind.Fly or ConstructionOperationKind.Detach)))
                choices.Add(("随身", () => ConstructionPlacementEdits.Follow(draft, AbilityAnchor.Source, preserveReferences, preserveStarts)));
            if (placementAvailable && plan.FormCount < AbilityCompiler.MaxForms - 1)
                choices.Add(("环绕", () => ConstructionPlacementEdits.Orbit(draft, AbilityAnchor.Source,
                    1.5f + (float)random.NextDouble() * 2f, 2f + (float)random.NextDouble() * 3f, preserveReferences, preserveStarts)));
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
            ConstructionPricing.Reprice(source, result, cost: result.Resource == source.Resource && result.Cost == source.Cost, cooldown: result.Cooldown == source.Cooldown, contactRules: inspiration.Rules);
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

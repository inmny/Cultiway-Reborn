using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>从共同形体和变化操作产生独立草稿，不在生成过程中登记半成品。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>固定输入创作走相同生成与编译入口，成功后才登记并授予。</summary>
    /// <param name="actor">真实创造者。</param>
    /// <param name="seed">可复现的创作种子。</param>
    /// <param name="inspiration">明确输入或受理时固定的倾向，省略时读取当前资料。</param>
    /// <param name="source">受理时固定的原术完整副本；为空时从基础规则创作。</param>
    /// <param name="resources">可复现的模型输入；自然请求已经固定，开发调用省略时读取当前资源。</param>
    public static string Create(ActorExtend actor, int seed, ConstructionInspiration inspiration = null,
        AbilityDefinition source = null, SynthesisResources resources = null)
    {
        if (!CanCreate(actor)) throw new InvalidOperationException("创造者失效或构造容量已满");
        AbilityDefinition definition = Generate(actor, seed, inspiration, source, resources);
        CompiledAbility plan = ValidateDefinition(definition, cache: false);
        string similar = FindSimilarConstruction(actor, definition, plan);
        if (similar != null) throw new System.IO.InvalidDataException("创作结果与已有构造近似，本次不新增：" + similar);
        Register(definition);
        Learn(actor, definition.Id);
        return definition.Id;
    }

    /// <summary>从共用结构与操作产生未登记的完整构造，保存实际选择而非重新抽取。</summary>
    /// <param name="actor">创造者。</param>
    /// <param name="seed">可复现的创作种子。</param>
    /// <param name="inspiration">明确输入或受理时固定的倾向，省略时读取当前资料。</param>
    /// <param name="source">已经固定的原术完整资料；不会修改该资料或当前正式目录。</param>
    /// <param name="resources">受理时固定的模型输入，省略时供当前资源下开发复现。</param>
    public static AbilityDefinition Generate(ActorExtend actor, int seed, ConstructionInspiration inspiration = null,
        AbilityDefinition source = null, SynthesisResources resources = null)
    {
        if (actor?.Base?.data == null) throw new ArgumentException("缺少真实创造者", nameof(actor));
        inspiration ??= ReadInspiration(actor);
        AbilityDefinition definition = GenerateDraft(actor, seed, inspiration, source, resources);
        definition.Name = ConstructionPresentation.Name(definition, ValidateDefinition(definition, cache: false));
        return definition;
    }

    /// <summary>只生成实际结构和过程，完成后由同一规则从结果生成名称。</summary>
    /// <param name="actor">真实创造者。</param>
    /// <param name="seed">确定种子。</param>
    /// <param name="inspiration">已经固定的角色倾向。</param>
    /// <param name="source">可选的完整原术。</param>
    /// <param name="resources">本次固定的模型输入。</param>
    private static AbilityDefinition GenerateDraft(ActorExtend actor, int seed, ConstructionInspiration inspiration,
        AbilityDefinition source, SynthesisResources resources)
    {
        if (inspiration.Rules >= 17)
        {
            resources ??= CaptureCreationResources(actor, seed, inspiration);
            if (resources.WorldActions == null) throw new ArgumentException("新创法请求缺少受理时的世界操作输入", nameof(resources));
            if (inspiration.Rules >= 23 && (resources.Forms.Any(form => form.Formations == null) ||
                !resources.Forms.Any(form => form.Formations.Length > 0)))
                throw new ArgumentException("新创法请求缺少受理时的逐段成形动作输入", nameof(resources));
            if (inspiration.Rules >= 26 && resources.InventoryMaterials == null)
                throw new ArgumentException("新创法请求缺少受理时的实际库存材料输入", nameof(resources));
            if (inspiration.Rules >= 30 && !resources.Forms.Any(form => form.Id == "fireball"))
                throw new ArgumentException("新创法请求缺少受理时的门形输入", nameof(resources));
            if (inspiration.Rules >= 28 && resources.ErosionMaterials == null)
                throw new ArgumentException("新创法请求缺少受理时的侵蚀材料输入", nameof(resources));
            if (inspiration.Rules >= 24 && (resources.DeformationForms == null || resources.Forms.Any(form => form.Controls == null)))
                throw new ArgumentException("新创法请求缺少受理时的结构与真实控制输入", nameof(resources));
        }
        if (source != null) return Derive(actor, source, seed, inspiration,
            resources ?? (inspiration.Rules >= 7 ? CaptureCreationResources(actor, seed, inspiration) : null));
        AbilityDefinition result = inspiration.Rules == 1
            ? GenerateLegacyDraft(actor, seed, inspiration)
            : Synthesize(actor, seed, inspiration, resources ?? CaptureCreationResources(actor, seed, inspiration));
        if (inspiration.Rules >= 12) result = GenerateContactEffects(result, seed, inspiration);
        if (inspiration.Rules >= 13) result = GenerateContactDisplacement(result, seed);
        if (inspiration.Rules >= 14) result = GenerateContactResults(result, seed, inspiration);
        if (inspiration.Rules >= 15) result = GenerateContactSpreads(result, seed, inspiration);
        if (inspiration.Rules >= 17) result = GenerateWorldContacts(result, seed, resources);
        if (inspiration.Rules >= 18) result = GenerateContactPolicy(result, seed);
        if (inspiration.Rules >= 19) result = GenerateRadialMotion(result, seed);
        if (inspiration.Rules >= 20) result = GenerateClash(result, seed);
        if (inspiration.Rules >= 21) result = GeneratePersistence(result, seed, inspiration.Rules);
        if (inspiration.Rules >= 23) result = GenerateFormation(result, seed, resources);
        if (inspiration.Rules >= 24) result = GenerateDeformation(result, seed, resources);
        if (inspiration.Rules >= 25) result = GenerateRemainder(result, seed);
        if (inspiration.Rules >= 26) result = GenerateInventory(result, seed, resources);
        if (inspiration.Rules >= 27) result = GenerateMaterialTransfer(result, seed);
        if (inspiration.Rules >= 28) result = GenerateErosionAndAging(result, seed, resources);
        if (inspiration.Rules >= 29) result = GenerateCorrespondence(result, seed);
        if (inspiration.Rules >= 30) result = GenerateMapping(result, seed, resources);
        return inspiration.Rules >= 3 ? ConstructionPricing.Price(result, contactRules: inspiration.Rules) : result;
    }

    /// <summary>仅为正式旧档中未完成的请求保留当时规则，当前活动不再进入三类预设骨架。</summary>
    /// <param name="actor">已保存请求的实际创造者。</param>
    /// <param name="seed">当时固定的种子。</param>
    /// <param name="inspiration">当时固定的数值输入。</param>
    private static AbilityDefinition GenerateLegacyDraft(ActorExtend actor, int seed,
        ConstructionInspiration inspiration)
    {
        var random = new System.Random(seed);
        int family = inspiration.Family(random);
        if (family == 0) return CreateGround(actor, random, seed, inspiration);
        if (family == 1)
        {
            AbilityDefinition action = BodyActionCatalog.All[random.Next(2)];
            AbilityDefinition projected = BodyConstructionEdits.Project(
                ConstructionMagnitudeEdits.Sustain(action, .05f + (float)random.NextDouble() * .15f, reprice: false), new BodyProjection
                {
                    Form = random.Next(2) == 0 ? "hand" : "sword",
                    Matter = inspiration.Matter(random),
                    Scale = 1.3f + (float)random.NextDouble() * 1.2f,
                    Speed = 6f + (float)random.NextDouble() * 5f,
                    Seconds = 1.2f + (float)random.NextDouble(),
                    TurnRate = 120f + random.Next(360),
                    Cost = 4f
                });
            projected.Id = "Cultiway.Created." + Guid.NewGuid().ToString("N");

            projected.Creator = actor.Base.id;
            projected.Seed = seed;
            projected.Impact = new ConstructionImpact
            { Force = random.NextDouble() < .4f + inspiration.Aggression * .5f ? 8f : -8f };
            if (random.Next(3) == 0) AddContactGrowth(projected, projected.Root);
            if (random.NextDouble() < .2f + inspiration.Control * .3f) AddContactStop(projected, projected.Root);
            if (random.NextDouble() < .2f + inspiration.Aggression * .3f) AddFracture(projected.Root);
            if (random.Next(3) == 0) AddRemainder(projected);
            return projected;
        }

        string id = "Cultiway.Created." + Guid.NewGuid().ToString("N");
        var children = new List<ConstructionNode>();
        int count = 1 + random.Next(3);
        for (int i = 0; i < count; i++)
        {
            float detach = 1.1f + i * .18f;
            string matter = inspiration.Matter(random);
            children.Add(new ConstructionNode
            {
                Id = "edge_" + i,
                Form = "sword",
                Anchor = count == 1 ? "tip_*" : "tip_" + i,
                Matter = matter,
                Scale = .25f + (float)random.NextDouble() * .15f,
                Rotation = new[] { (float)random.Next(-35, 36), (float)random.Next(-35, 36), 0f },
                ActiveFrom = detach,
                Operations = new[]
                {
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, At = .25f, Seconds = .6f },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = detach },
                    new ConstructionOperation
                    {
                        Kind = ConstructionOperationKind.Fly, At = detach, Vector = new[] { 0f, 0f, 7f },
                        TurnRate = 100f + random.Next(100)
                    }
                }
            });
        }

        var definition = new AbilityDefinition
        {
            Id = id,
            Creator = actor.Base.id,
            Seed = seed,
            Cost = 12f,
            Cooldown = 5f,
            Lifetime = 4f,
            Power = 1.2f,
            Root = new ConstructionNode
            {
                Id = "root",
                Form = "branch",
                Matter = "wood",
                Scale = 1.1f + (float)random.NextDouble() * .6f,
                ActiveFrom = .7f,
                ActiveUntil = 1.1f,
                Operations = new[]
                    { new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .8f } },
                Children = children.ToArray()
            }
        };
        if (random.Next(2) == 0)
        {
            foreach (ConstructionNode child in definition.Root.Children)
            {
                child.Offset = new[]
                {
                    (float)random.NextDouble() * 5f - 2.5f, (float)random.NextDouble() * 5f - 2.5f,
                    2f + (float)random.NextDouble() * 2f
                };
                child.Operations = child.Operations.Concat(new[]
                {
                    new ConstructionOperation
                    {
                        Kind = ConstructionOperationKind.Gather, At = .15f, Seconds = .7f, GatherRotation = new float[3]
                    }
                }).ToArray();
            }

            definition.Cost += 2f;
        }

        if (random.Next(3) == 0)
            foreach (ConstructionNode child in definition.Root.Children)
                AddFracture(child);
        int placement = random.NextDouble() < inspiration.Control * .6f ? 2 : random.Next(2);
        if (placement == 1) definition = ConstructionPlacementEdits.Follow(definition, AbilityAnchor.Source, preserveStarts: inspiration.Rules >= 22);
        if (placement == 2)
            definition = ConstructionPlacementEdits.Orbit(definition, AbilityAnchor.Source,
                1.5f + (float)random.NextDouble() * 2f, 3f + (float)random.NextDouble() * 2f, preserveStarts: inspiration.Rules >= 22);
        definition = ConstructionMagnitudeEdits.Resize(definition, .8f + (float)random.NextDouble() * .6f, reprice: false);
        definition = ConstructionMagnitudeEdits.Haste(definition, .8f + (float)random.NextDouble() * .8f, reprice: false);
        definition = ConstructionMagnitudeEdits.Duration(definition, .8f + (float)random.NextDouble() * .6f, reprice: false);
        definition = ConstructionMagnitudeEdits.Sustain(definition, .3f + (float)random.NextDouble() * .7f, reprice: false);
        if (random.Next(3) == 0) AddContactGrowth(definition, definition.Root.Children[0]);
        if (random.NextDouble() < .2f + inspiration.Control * .3f)
            AddContactStop(definition, definition.Root.Children[0]);
        if (random.Next(3) == 0) AddRemainder(definition);
        if (random.NextDouble() < .15f + inspiration.Aggression * .35f)
            definition = ConstructionMagnitudeEdits.Repeat(definition, 2, .7f, random.Next(2) * .3f, reprice: false);
        if (random.Next(3) == 0) definition.Impact = new ConstructionImpact { Force = random.Next(2) == 0 ? 6f : -6f };
        return definition;
    }

    /// <summary>从共同形体、材料与沿地运动创作，不另设一类技能执行器。</summary>
    /// <param name="actor">实际创造者。</param>
    /// <param name="random">此份创作使用的随机源。</param>
    /// <param name="seed">保存后不重新抽取的种子。</param>
    /// <param name="inspiration">已经固定的角色倾向。</param>
    private static AbilityDefinition CreateGround(ActorExtend actor, System.Random random, int seed,
        ConstructionInspiration inspiration)
    {
        string matter = inspiration.Matter(random);
        bool water = matter == "water";
        string form = matter switch
        {
            "water" => "water_flow",
            "wood" => "branch",
            "fire" => "fire_body",
            _ => "sword"
        };
        var definition = new AbilityDefinition
        {
            Id = "Cultiway.Created." + Guid.NewGuid().ToString("N"),
            Creator = actor.Base.id,
            Seed = seed,
            Cost = 5f,
            Cooldown = 4f,
            Lifetime = 3f,
            Power = 1f,
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
            Root = new ConstructionNode
            {
                Id = "ground",
                Form = form,
                Matter = matter,
                Volume = water ? 16f : (float?)null,
                Offset = new[] { 3f, 0f, 0f },
                Rotation = new[] { 0f, 0f, -90f },
                Scale = .5f + (float)random.NextDouble() * .5f,
                Operations = water
                    ? new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "run" } }
                    : Array.Empty<ConstructionOperation>()
            }
        };
        definition.Root.Operations = definition.Root.Operations.Concat(new[]
        {
            new ConstructionOperation
            {
                Kind = ConstructionOperationKind.Fly, Vector = new[] { 0f, 3f + (float)random.NextDouble() * 4f, 0f },
                InheritVelocity = false, Ground = ConstructionGroundResponse.Follow
            }
        }).ToArray();
        if (water)
            definition.Root.Operations = definition.Root.Operations.Concat(new[]
            {
                new ConstructionOperation
                    { Kind = ConstructionOperationKind.Flow, At = .15f, Flow = new ConstructionFlow() }
            }).ToArray();
        int ending = random.Next(3);
        if (ending == 1)
            definition.Root.Operations = definition.Root.Operations.Concat(new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Leave, At = 2f,
                    Residue = new ConstructionResidue { Lifetime = 20f }
                }
            }).ToArray();
        if (water && ending == 2)
            definition.Root.Operations = definition.Root.Operations.Concat(new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Recover, At = 2f, Seconds = .6f,
                    Recovery = new ConstructionRecovery()
                }
            }).ToArray();
        if (water && random.Next(4) == 0)
        {
            bool freeze = random.Next(2) == 0;

            definition.Root.Operations = definition.Root.Operations.Concat(new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.ConvertMatter, At = .5f, Matter = "water",
                    State = freeze ? "solid" : "liquid",
                    WorldMatter = new ConstructionWorldMatter
                    {
                        FromMatter = "water", FromState = freeze ? "liquid" : "solid",
                        MaximumVolume = 8f + (float)random.NextDouble() * 16f
                    }
                }
            }).ToArray();
        }

        if (!water && random.Next(4) == 0)
        {
            definition.Root.Operations = definition.Root.Operations.Concat(new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Conduct, At = .7f,
                    Conduction = new ConstructionConduction { Range = 12 + random.Next(24), Pulses = random.Next(1, 4) }
                }
            }).ToArray();
        }

        return definition;
    }

    /// <summary>为实际末端创作结束后独立继续的残留，接收真实余力而非新的初始威力。</summary>
    /// <param name="definition">已经完成主要改造的实际资料。</param>
    private static void AddRemainder(AbilityDefinition definition)
    {
        ConstructionNode source = definition.Root;
        while (source.Children.Length > 0) source = source.Children[0];
        source.DisappearAt = definition.Lifetime - .5f;
        source.Children = new[]
        {
            new ConstructionNode
            {
                Id = "remnant", Form = source.Form, Matter = source.Matter, State = source.State,
                Scale = source.Scale * .65f,
                Activation = new ConstructionActivation
                    { Trigger = ConstructionChangeTrigger.End, InheritRemainder = true },
                Operations = new[]
                {
                    new ConstructionOperation
                    {
                        Kind = ConstructionOperationKind.Fly, Vector = new[] { 0f, 0f, 4f },
                        TurnRate = 180f, InheritVelocity = false
                    }
                }
            }
        };
    }

    /// <summary>给已有生成主体加入真实损耗后的分件碎解，仍使用共同资料和运行。</summary>
    /// <param name="node">有真实多部件结构的主体。</param>
    private static void AddFracture(ConstructionNode node)
    {
        var resource = FormResources.Load(node.Form);
        node.Operations = node.Operations.Concat(new[]
        {
            new ConstructionOperation
            {
                Kind = ConstructionOperationKind.Fracture,
                When = new ConstructionCondition { Trigger = ConstructionChangeTrigger.ForceBelow, Threshold = .45f },
                Fracture = new ConstructionFracture
                {
                    Seconds = 1.2f,
                    ActiveParts = resource.Description.Parts
                        .Where((_, index) => resource.Description.Contacts.Any(contact => contact.Part == index))
                        .Select(part => part.Id).ToArray()
                }
            }
        }).ToArray();
    }

    /// <summary>通过共用条件操作创作接触后驻留的主体，不重新施放或重置接触。</summary>
    /// <param name="definition">完整创作资料。</param>
    /// <param name="source">真正接触的部位。</param>
    private static void AddContactStop(AbilityDefinition definition, ConstructionNode source)
    {
        source.OnContact = ConstructionContactResponse.Continue;
        source.Operations = source.Operations.Concat(new[]
        {
            new ConstructionOperation
                { Kind = ConstructionOperationKind.Stop, When = new ConstructionCondition() }
        }).ToArray();
        bool canGrow = !AbilityMatters.Resolve(source.Matter, source.State, "创作部位").HoldsShape;
        if (canGrow)
            source.Operations = source.Operations.Concat(new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Grow, Seconds = .65f, TargetScale = 1.6f,
                    When = new ConstructionCondition { Delay = .15f }
                }
            }).ToArray();
        if (canGrow)
            source.Operations = source.Operations.Concat(new[]
            {
                new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Spin, Vector = new[] { 0f, 0f, 60f },
                    When = new ConstructionCondition { Trigger = ConstructionChangeTrigger.SizeAbove, Threshold = 1.4f }
                }
            }).ToArray();
        definition.Cost += 1f;
    }

    /// <summary>创作时为实际来源补上一段接触后才开始的独立生长，仍使用普通部位及操作。</summary>
    /// <param name="definition">本次独立创作资料。</param>
    /// <param name="source">实际发出事件的部位。</param>
    private static void AddContactGrowth(AbilityDefinition definition, ConstructionNode source)
    {
        source.Children = source.Children.Concat(new[]
        {
            new ConstructionNode
            {
                Id = "contact_growth", Form = "branch", Matter = source.Matter, State = source.State,
                Scale = 1.2f, ActiveFrom = .3f, DisappearAt = 1.2f,
                Activation = new ConstructionActivation(),
                Operations = new[]
                    { new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .45f } }
            }
        }).ToArray();
        definition.Cost += 2f;
    }
}

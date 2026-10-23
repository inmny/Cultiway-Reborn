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
    public static string Create(ActorExtend actor, int seed, ConstructionInspiration inspiration = null,
        AbilityDefinition source = null)
    {
        if (!CanCreate(actor)) throw new InvalidOperationException("创造者失效或构造容量已满");
        AbilityDefinition definition = Generate(actor, seed, inspiration, source);
        Register(definition);
        Learn(actor, definition.Id);
        return definition.Id;
    }

    /// <summary>从共用结构与操作产生未登记的完整构造，保存实际选择而非重新抽取。</summary>
    /// <param name="actor">创造者。</param>
    /// <param name="seed">可复现的创作种子。</param>
    /// <param name="inspiration">明确输入或受理时固定的倾向，省略时读取当前资料。</param>
    /// <param name="source">已经固定的原术完整资料；不会修改该资料或当前正式目录。</param>
    public static AbilityDefinition Generate(ActorExtend actor, int seed, ConstructionInspiration inspiration = null,
        AbilityDefinition source = null)
    {
        if (actor?.Base?.data == null) throw new ArgumentException("缺少真实创造者", nameof(actor));
        inspiration ??= ReadInspiration(actor);
        if (source != null) return Derive(actor, source, seed, inspiration);
        var random = new System.Random(seed);
        int family = inspiration.Family(random);
        if (family == 0) return CreateGround(actor, random, seed, inspiration);
        if (family == 1)
        {
            AbilityDefinition action = BodyActionCatalog.All[random.Next(2)];
            AbilityDefinition projected = BodyConstructionEdits.Project(
                ConstructionMagnitudeEdits.Sustain(action, .05f + (float)random.NextDouble() * .15f), new BodyProjection
                {
                    Form = random.Next(2) == 0 ? "hand" : "sword",
                    Matter = inspiration.Matter(random),
                    Scale = 1.3f + (float)random.NextDouble() * 1.2f,
                    Speed = 6f + (float)random.NextDouble() * 5f,
                    Seconds = 1.2f + (float)random.NextDouble(),
                    TurnRate = 120f + random.Next(360), Cost = 4f
                });
            projected.Id = "Cultiway.Created." + Guid.NewGuid().ToString("N");
            projected.Name = "自创·" + action.Name + "显化";
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
            Name = "自创·枝上凝锋",
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

            definition.Name += "·聚锋";
            definition.Cost += 2f;
        }

        if (random.Next(3) == 0)
            foreach (ConstructionNode child in definition.Root.Children)
                AddFracture(child);
        int placement = random.NextDouble() < inspiration.Control * .6f ? 2 : random.Next(2);
        if (placement == 1) definition = ConstructionPlacementEdits.Follow(definition, AbilityAnchor.Source);
        if (placement == 2)
            definition = ConstructionPlacementEdits.Orbit(definition, AbilityAnchor.Source,
                1.5f + (float)random.NextDouble() * 2f, 3f + (float)random.NextDouble() * 2f);
        if (placement != 0) definition.Name += placement == 1 ? "·随身" : "·环绕";
        definition = ConstructionMagnitudeEdits.Resize(definition, .8f + (float)random.NextDouble() * .6f);
        definition = ConstructionMagnitudeEdits.Haste(definition, .8f + (float)random.NextDouble() * .8f);
        definition = ConstructionMagnitudeEdits.Duration(definition, .8f + (float)random.NextDouble() * .6f);
        definition = ConstructionMagnitudeEdits.Sustain(definition, .3f + (float)random.NextDouble() * .7f);
        if (random.Next(3) == 0) AddContactGrowth(definition, definition.Root.Children[0]);
        if (random.NextDouble() < .2f + inspiration.Control * .3f)
            AddContactStop(definition, definition.Root.Children[0]);
        if (random.Next(3) == 0) AddRemainder(definition);
        if (random.NextDouble() < .15f + inspiration.Aggression * .35f)
            definition = ConstructionMagnitudeEdits.Repeat(definition, 2, .7f, random.Next(2) * .3f);
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
            "water" => "water_flow", "wood" => "branch", "fire" => "fire_body", _ => "sword"
        };
        string name = matter switch
        {
            "water" => "逐地流", "wood" => "伏地枝", "fire" => "逐地焰", "ice" => "贴地冰锋", _ => "贴地金锋"
        };
        var definition = new AbilityDefinition
        {
            Id = "Cultiway.Created." + Guid.NewGuid().ToString("N"), Name = "自创·" + name,
            Creator = actor.Base.id, Seed = seed, Cost = 5f, Cooldown = 4f, Lifetime = 3f, Power = 1f,
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
            Root = new ConstructionNode
            {
                Id = "ground", Form = form, Matter = matter,
                Volume = water ? 16f : (float?)null,
                Offset = new[] { 3f, 0f, 0f }, Rotation = new[] { 0f, 0f, -90f },
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
            definition.Name += freeze ? "·凝水" : "·融冰";
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
            definition.Name += "·引雷";
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
        definition.Name += canGrow ? "·凝驻长成" : "·凝驻";
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
        definition.Name += "·触生";
    }
}
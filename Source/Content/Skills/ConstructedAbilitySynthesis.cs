using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>按部位及其连接逐步创作；所有结果仍由共同编译器和运行器解释。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>当前创作只组装普通资料，不登记整招类型或增加一套执行。</summary>
    /// <param name="actor">实际创造者。</param>
    /// <param name="seed">固定种子。</param>
    /// <param name="inspiration">受理时固定的数值输入。</param>
    /// <param name="resources">受理时固定的模型数值。</param>
    private static AbilityDefinition Synthesize(ActorExtend actor, int seed, ConstructionInspiration inspiration,
        SynthesisResources resources)
    {
        var builder = new ConstructionSynthesis(seed, inspiration, resources);
        return builder.Build(actor.Base.id, seed);
    }

    /// <summary>每份草稿独立持有随机源和剩余预算，按深度优先顺序固定生成结果。</summary>
    private sealed class ConstructionSynthesis
    {
        /// <summary>最多十二个原始部位，不用挂点通配符扩大展开数量。</summary>
        private const int MaximumNodes = 12;

        /// <summary>本次创作唯一随机源。</summary>
        private readonly System.Random random;

        /// <summary>受理时固定的倾向。</summary>
        private readonly ConstructionInspiration inspiration;

        /// <summary>已经固定的模型尺寸、坐标及稳定名称。</summary>
        private readonly SynthesisResources resources;

        /// <summary>已经生成的部位数量。</summary>
        private int count;

        /// <summary>创建独立草稿上下文。</summary>
        /// <param name="seed">固定种子。</param>
        /// <param name="inspiration">固定输入。</param>
        /// <param name="resources">本次固定的模型输入。</param>
        internal ConstructionSynthesis(int seed, ConstructionInspiration inspiration, SynthesisResources resources)
        {
            random = new System.Random(seed);
            this.inspiration = inspiration;
            this.resources = resources;
        }

        /// <summary>先建立真实树，再选择整体放置；成本依据实际生成部位，不按名称猜测。</summary>
        /// <param name="creator">实际创造者长期编号。</param>
        /// <param name="seed">记录在正式资料中的种子。</param>
        internal AbilityDefinition Build(long creator, int seed)
        {
            var definition = new AbilityDefinition
            {
                Id = "Cultiway.Created." + Guid.NewGuid().ToString("N"), Creator = creator, Seed = seed,
                Lifetime = 5f, Cooldown = 2f + Range(0f, 2f), Power = 1f,
                Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source, FaceTarget = true },
                Root = Node(null, 0, 1f, Quaternion.identity)
            };
            definition.Cost = 3f + count * 1.5f;
            if (inspiration.CanProject && Chance(.25f))
            {
                AbilityDefinition projected = BodyConstructionEdits.Project(
                    resources.Bodies[random.Next(resources.Bodies.Length)],
                    new BodyProjection
                    {
                        Matter = inspiration.Matter(random), Scale = Range(1.3f, 2.2f), Rotation = new float[3],
                        Speed = Range(6f, 10f), TurnRate = Range(120f, 360f), Seconds = definition.Lifetime,
                        Cost = definition.Cost + 4f
                    });
                // 显化手与新树各自使用明确地图尺寸，不把手形倍率再乘到整棵子树上。
                projected.Binding.ScaleByBody = false;
                projected.Root.Scale = Range(1.2f, 2f) / resources.Get(projected.Root.Form).Size;
                definition.Root.Scale /= projected.Root.Scale;
                projected.Root.OnContact = ConstructionContactResponse.Continue;
                float release = projected.Root.Operations.Single(operation =>
                    operation.Kind == ConstructionOperationKind.Detach).At;
                foreach (ConstructionOperation flight in definition.Root.Operations.Where(operation =>
                             operation.Kind == ConstructionOperationKind.Fly)) flight.At = release;
                definition.Root.Operations = definition.Root.Operations.Concat(new[]
                {
                    new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = release }
                }).ToArray();
                projected.Root.Children = new[] { definition.Root };
                projected.Id = definition.Id;
                projected.Creator = creator;
                projected.Seed = seed;
                projected.Cooldown = definition.Cooldown;
                if (inspiration.Rules >= 5 && BodyConstructionEdits.RecoveryPoints(projected).Length > 0)
                    projected = GenerateBodyResponse(projected, random);
                return projected;
            }

            if (Chance(inspiration.Control * .6f))
                definition = ConstructionPlacementEdits.Orbit(definition, AbilityAnchor.Source,
                    Range(2f, 4f), Range(2f, 5f));
            else if (Chance(.35f)) definition = ConstructionPlacementEdits.Follow(definition, AbilityAnchor.Source);
            return definition;
        }

        /// <summary>依次选择材料、形体、父子关系、局部过程和后代；最多四层、十二个部位。</summary>
        /// <param name="parent">真实父部位，根部为空。</param>
        /// <param name="depth">当前层数。</param>
        /// <param name="parentScale">父层累计尺寸，用来保持子部位的实际地图大小。</param>
        /// <param name="parentOrientation">父层制作姿态，子层不能重复叠加同一坐标换算。</param>
        private ConstructionNode Node(ConstructionNode parent, int depth, float parentScale,
            Quaternion parentOrientation)
        {
            string matter = inspiration.Matter(random);
            string form = Form(matter);
            SynthesisForm resource = resources.Get(form);
            bool flow = form == "water_flow" && matter == "water";
            float scale = (parent == null ? Range(2.5f, 4f) : Range(.8f, 2f)) / resource.Size;
            Quaternion orientation = Quaternion.Euler(0f, parent == null ? 0f : Range(-25f, 25f), 0f) * resource.Facing;
            Vector3 rotation = (Quaternion.Inverse(parentOrientation) * orientation).eulerAngles;
            var node = new ConstructionNode
            {
                Id = "part_" + count++, Form = form, Matter = matter,
                Scale = scale / parentScale,
                ActiveFrom = .25f,
                Rotation = new[] { rotation.x, rotation.y, rotation.z },
                Strength = Range(.8f, 1.6f)
            };
            var operations = new List<ConstructionOperation>
            {
                new() { Kind = ConstructionOperationKind.Grow, Seconds = Range(.2f, .65f) }
            };
            bool eventStart = false;
            float release = parent == null ? 0f : .7f + depth * .2f;
            if (parent != null)
            {
                // 连接使用资源已有的明确挂点；事件分支从真实发生位置独立开始。
                string[] anchors = resources.Get(parent.Form).Anchors;
                if (anchors.Length > 0) node.Anchor = anchors[random.Next(anchors.Length)];
                else node.Offset = new[] { Range(-2f, 2f), 0f, Range(1f, 3f) };
                int relation = random.Next(4);
                if (relation == 0)
                {
                    node.Activation = new ConstructionActivation
                    {
                        Trigger = parent.ContactRole == ConstructionContactRole.Barrier
                            ? ConstructionChangeTrigger.Clash
                            : ConstructionChangeTrigger.ActorContact,
                        Delay = Range(.05f, .2f)
                    };
                    eventStart = true;
                    release = 0f;
                }
                else if (relation == 1)
                {
                    node.AppearAt = Range(.3f, .8f);
                    release = Math.Max(release, node.AppearAt.Value + .3f);
                }

                if (!eventStart && relation != 2)
                    operations.Add(new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = release });
                else if (!eventStart) release = -1f;
            }

            if (release >= 0f)
            {
                Vector3 velocity = Quaternion.Inverse(orientation) * Vector3.forward * Range(5f, 9f);
                if (flow) velocity.z = 0f;
                var flight = new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Fly, At = release, InheritVelocity = false,
                    Vector = new[] { velocity.x, velocity.y, velocity.z },
                    TurnRate = Chance(.25f + inspiration.Control * .5f) ? Range(120f, 360f) : 0f,
                    Ground = flow ? ConstructionGroundResponse.Follow : ConstructionGroundResponse.Ignore
                };
                if (!flow && Chance(.25f))
                    flight.Path = new ConstructionPath
                    {
                        Keys = new[]
                        {
                            new ConstructionPathKey { Out = new[] { Range(-2f, 2f), Range(1f, 3f), 0f } },
                            new ConstructionPathKey { At = 1.2f, In = new[] { Range(-2f, 2f), Range(1f, 3f), 0f } }
                        }
                    };
                operations.Add(flight);
            }

            if (resource.Motions.Length > 0)
                operations.Add(new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Animate,
                    Clip = resource.Motions[random.Next(resource.Motions.Length)]
                });
            if (flow)
            {
                node.Volume = Range(6f, 12f);
                operations.Add(new ConstructionOperation
                    { Kind = ConstructionOperationKind.Flow, At = .35f, Flow = new ConstructionFlow() });
            }

            if (Chance(.25f))
                operations.Add(new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.ConvertMatter, At = 1.4f,
                    Matter = matter == "ice" ? "water" : inspiration.Matter(random)
                });
            if (!flow && Chance(.2f))
                operations.Add(new ConstructionOperation
                    { Kind = ConstructionOperationKind.Spin, Vector = new[] { 0f, 0f, Range(-90f, 90f) } });
            if (Chance(.15f + inspiration.Control * .25f))
                operations.Add(new ConstructionOperation
                    { Kind = ConstructionOperationKind.Stop, When = new ConstructionCondition() });
            if (matter != "fire" && Chance(.12f))
                operations.Add(new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Conduct, At = 1.6f,
                    Conduction = new ConstructionConduction { Pulses = random.Next(1, 4), Range = Range(12f, 30f) }
                });
            int ending = random.Next(4);
            if (ending == 1)
                operations.Add(new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Recover, At = 3.5f, Seconds = .5f,
                    Recovery = new ConstructionRecovery()
                });
            if (ending == 2)
                operations.Add(new ConstructionOperation
                {
                    Kind = ConstructionOperationKind.Leave, At = 4f,
                    Residue = new ConstructionResidue { Lifetime = 20f }
                });
            node.Operations = operations.ToArray();
            if (parent != null && Chance(inspiration.Control * .25f))
            {
                node.ContactRole = ConstructionContactRole.Barrier;
                foreach (ConstructionOperation operation in operations)
                    if (operation.When?.Trigger == ConstructionChangeTrigger.ActorContact)
                        operation.When.Trigger = ConstructionChangeTrigger.Clash;
            }
            else if (Chance(.3f))
            {
                // 正式旧请求保留原有随机取样次数与生成结果，新请求才选择新的接触作用。
                if (inspiration.Rules < 4)
                    node.Impact = new ConstructionImpact
                    {
                        Force = (Chance(.5f) ? -1 : 1) * Range(3f, 8f), Origin = ConstructionForceOrigin.Shape
                    };
                else
                {
                    node.Impact = new ConstructionImpact
                    {
                        Force = Chance(.65f) ? (Chance(.5f) ? -1 : 1) * Range(3f, 8f) : 0f,
                        Origin = ConstructionForceOrigin.Shape,
                        StaggerSeconds = Chance(.3f) ? Range(.1f, .45f) : 0f,
                        BindSeconds = Chance(.15f) ? Range(.3f, 1.2f) : 0f
                    };
                    if (node.Impact.Force != 0f && Chance(.4f)) node.Impact.SlideSeconds = Range(.15f, .6f);
                    else if (node.Impact.Force == 0f && Chance(.2f))
                    {
                        node.Impact.Launch = true;
                        node.Impact.Lift = Range(1.1f, 2f);
                    }
                }
            }

            // 后代可以继续分枝；数量和深度均由本次剩余预算约束，不通过失败后重抽修补。
            var children = new List<ConstructionNode>();
            int wanted = depth < 3 && Chance(depth == 0 ? .85f : .55f) ? random.Next(1, 4) : 0;
            for (int i = 0; i < wanted && count < MaximumNodes; i++)
                children.Add(Node(node, depth + 1, scale, orientation));
            node.Children = children.ToArray();
            return node;
        }

        /// <summary>材料影响可选形体，但不是完整法术模板；不同层分别选择。</summary>
        /// <param name="matter">本部位实际材料。</param>
        private string Form(string matter)
        {
            string[] forms = matter switch
            {
                "water" => new[] { "water_wave", "water_flow", "water_whale", "hand", "sword" },
                "wood" => new[] { "branch", "branch", "hand", "sword" },
                "fire" => new[] { "fire_body", "fire_body", "hand", "sword" },
                "ice" => new[] { "water_wave", "hand", "sword" },
                _ => new[] { "sword", "sword", "hand" }
            };
            return forms[random.Next(forms.Length)];
        }

        /// <summary>本次随机源的明确概率选择。</summary>
        /// <param name="probability">零至一概率。</param>
        private bool Chance(float probability) => random.NextDouble() < probability;

        /// <summary>生成实际范围内的连续数值。</summary>
        /// <param name="minimum">最小值。</param>
        /// <param name="maximum">最大值。</param>
        private float Range(float minimum, float maximum) => minimum + (float)random.NextDouble() * (maximum - minimum);
    }
}

/// <summary>旧请求继续按当时的生成规则完成；当前请求显式保存新的逐部位创作规则。</summary>
internal sealed class CreationV3ToV4 : ISaveMigration
{
    /// <summary>已有倾向及原术快照，但未保存生成规则号的格式。</summary>
    public int FromVersion => 3;

    /// <summary>不改写原有倾向或原术，只标明未完成请求属于旧规则。</summary>
    /// <param name="data">保存层提供的旧资料副本。</param>
    public JObject Apply(JObject data)
    {
        if (data["Inspiration"] is JObject inspiration) inspiration["Rules"] = 1;
        return data;
    }
}
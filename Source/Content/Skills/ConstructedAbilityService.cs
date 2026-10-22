using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.KnightCombat;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>人物正式掌握的构造编号，不保存图形资源和运行时实体编号。</summary>
public struct ConstructedKnowledge : IComponent
{
    /// <summary>按学习顺序保存的能力编号。</summary>
    public string[] Ids;
}

/// <summary>接入首批构造的学习、支付、施放、实际接触和正式保存。</summary>
public sealed partial class ConstructedAbilityService : BaseSystem, IActiveAbilityProvider
{
    /// <summary>正式能力来源的稳定编号。</summary>
    private const string ProviderId = "content.constructed";
    /// <summary>与系统自身编号分开的能力来源。</summary>
    string IActiveAbilityProvider.Id => ProviderId;
    /// <summary>本世界当前版本的完整构造。</summary>
    private static readonly Dictionary<string, AbilityDefinition> definitions = new(StringComparer.Ordinal);
    /// <summary>与当前构造对应的共享执行计划。</summary>
    private static readonly Dictionary<string, CompiledAbility> compiled = new(StringComparer.Ordinal);
    /// <summary>人物与能力独立的恢复时点。</summary>
    private static readonly Dictionary<(long Actor, string Ability), double> cooldowns = new();
    /// <summary>正在推进的独立施放，不限制多招并存。</summary>
    private static readonly List<RunningCast> casts = new();
    /// <summary>本进程内不重复的单次施放编号，不写进长期构造。</summary>
    private static long nextCastId;
    /// <summary>正式入口受理后发布，便于区别实际自主出手与脚本播放。</summary>
    public static event Action<ConstructedCastStarted> CastStarted;
    /// <summary>实际作用形状相交并进入正式结算后发布。</summary>
    public static event Action<ConstructedContact> ContactConfirmed;
    /// <summary>共用本帧显示缓冲。</summary>
    private readonly List<FormVisualInstance> visuals = new();
    /// <summary>登记两类正式保存。定义先于人物恢复，未知构造只丢弃对应学习关系。</summary>
    public static void Initialize()
    {
        ConstructedMatterCatalog.Initialize();
        var service = new ConstructedAbilityService();
        ConstructionEditing.Access = service;
        simulation = service;
        ModClass.I.GeneralLogicSystems.Add(service);
        ActiveAbilityService.Register(service);
        FormResources.ValidatingReload += ValidateReloadedResources;
        FormResources.Reloaded += _ => compiled.Clear();
        PatchMapBox.RegisterActionOnClearWorld(Clear);
        PatchActor.RegisterHideHandItemPredicate(IsWeaponBorrowed);
        foreach (AbilityDefinition body in BodyActionCatalog.All) Register(body);
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, DefinitionSave>("ability_constructions", "自创能力构造", 1,
            _ => new DefinitionSave { Definitions = definitions.Values.ToArray() }, (_, data) =>
            {
                foreach (AbilityDefinition definition in data.Definitions)
                    try { Register(definition); }
                    catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException or JsonException)
                    { ModClass.I.WorldSaves.LastReport.Skip(definition?.Id ?? "未知构造", "自创能力构造", error.Message); }
            }, data =>
            {
                if (data.Definitions == null || data.Definitions.Length > 100000)
                    throw new InvalidDataException("构造集合缺失或超过世界保存预算");
            })
        { BeforeActors = true });
        ModClass.I.WorldSaves.Actors.Register(new SaveSection<ActorExtend, KnowledgeSave>("ability_knowledge", "自创能力掌握", 1,
            actor => actor.E.TryGetComponent(out ConstructedKnowledge known) ? new KnowledgeSave
            {
                Ids = known.Ids,
                Cooldowns = known.Ids.ToDictionary(id => id, id => Mathf.Max(0f,
                    (float)(cooldowns.TryGetValue((actor.Base.id, id), out double until) ? until - World.world.getCurWorldTime() : 0)))
            } : null,
            (actor, data) =>
            {
                var restored = new List<string>();
                foreach (string id in data.Ids.Distinct())
                {
                    if (id == null || !definitions.ContainsKey(id))
                    { ModClass.I.WorldSaves.LastReport.Skip("人物 " + actor.Base.id, "自创能力掌握", "缺少构造：" + id); continue; }
                    restored.Add(id);
                    if (data.Cooldowns.TryGetValue(id, out float remaining))
                        cooldowns[(actor.Base.id, id)] = World.world.getCurWorldTime() + remaining;
                }
                actor.E.AddComponent(new ConstructedKnowledge { Ids = restored.ToArray() });
            }, data =>
            {
                if (data.Ids == null || data.Ids.Length > 128 || data.Cooldowns == null ||
                    data.Cooldowns.Values.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value < 0))
                    throw new InvalidDataException("已学构造或恢复时间无效");
            }));
    }

    /// <summary>当前请求共用同版计划，资源变化后只重建尚未运行的请求。</summary>
    /// <param name="id">正式构造编号。</param>
    private static CompiledAbility PlanFor(string id)
    {
        if (!compiled.TryGetValue(id, out CompiledAbility plan) || plan.ResourceVersion != FormResources.Version)
        { plan = ValidateDefinition(definitions[id]); compiled[id] = plan; }
        return plan;
    }

    /// <summary>资源提交前验证完整正式目录，不改写定义、学习和正在运行的施放。</summary>
    private static void ValidateReloadedResources()
    {
        foreach (AbilityDefinition definition in definitions.Values) ValidateDefinition(definition);
    }

    /// <summary>在创建边界复制完整定义，编译成功后才登记。</summary>
    /// <param name="definition">外部当前构造。</param>
    public static void Register(AbilityDefinition definition)
    {
        CompiledAbility plan = ValidateDefinition(definition);
        AbilityDefinition copy = JsonConvert.DeserializeObject<AbilityDefinition>(JsonConvert.SerializeObject(definition));
        definitions[copy.Id] = copy; compiled[copy.Id] = plan;
    }

    /// <summary>创建、编辑预览和正式登记使用同一资料及来源规则，不在界面另推支付限制。</summary>
    /// <param name="definition">当前资料。</param>
    private static CompiledAbility ValidateDefinition(AbilityDefinition definition)
    {
        CompiledAbility plan = AbilityCompiler.Compile(definition);
        foreach (string matter in plan.Matters) Composition(matter);
        if (plan.Body != null) BodyActionBinding.ValidateResources(plan.Body);
        if (definition.Resource is not ("none" or "wakan" or "vigor" or "mana" or "stamina") ||
            definition.Resource == "none" && definition.Cost != 0f ||
            definition.Resource is "mana" or "stamina" && (definition.Cost != Mathf.Floor(definition.Cost) || (double)definition.Cost > int.MaxValue))
            throw new InvalidDataException("支付资源、费用或整数资源范围无效：" + definition.Resource);
        return plan;
    }

    /// <summary>授予已经存在的构造，复制编号集合避免修改共用资料。</summary>
    /// <param name="actor">学习者。</param>
    /// <param name="id">稳定构造编号。</param>
    public static void Learn(ActorExtend actor, string id)
    {
        if (!definitions.ContainsKey(id)) throw new ArgumentException("尚未登记构造：" + id, nameof(id));
        string[] old = actor.E.TryGetComponent(out ConstructedKnowledge known) ? known.Ids : Array.Empty<string>();
        if (old.Contains(id)) return;
        if (old.Length >= 128) throw new InvalidOperationException("此人的构造掌握已达128项");
        actor.E.AddComponent(new ConstructedKnowledge { Ids = old.Concat(new[] { id }).ToArray() });
    }

    /// <summary>从共用结构与操作产生未登记的完整构造，保存实际选择而非重新抽取。</summary>
    /// <param name="actor">创造者。</param>
    /// <param name="seed">可复现的创作种子。</param>
    public static string Create(ActorExtend actor, int seed)
    {
        var random = new System.Random(seed);
        if (random.Next(6) == 0) return CreateGround(actor, random, seed);
        if (random.Next(3) == 0 && BodyActionBinding.CanBind(actor, AbilityCompiler.Compile(BodyActionCatalog.Palm).Body))
        {
            AbilityDefinition action = BodyActionCatalog.All[random.Next(2)];
            AbilityDefinition projected = BodyConstructionEdits.Project(ConstructionMagnitudeEdits.Sustain(action, .05f + (float)random.NextDouble() * .15f), new BodyProjection
            {
                Form = random.Next(2) == 0 ? "hand" : "sword",
                Matter = random.Next(2) == 0 ? "ice" : "iron",
                Scale = 1.3f + (float)random.NextDouble() * 1.2f,
                Speed = 6f + (float)random.NextDouble() * 5f,
                Seconds = 1.2f + (float)random.NextDouble(),
                TurnRate = 120f + random.Next(360), Cost = 4f
            });
            projected.Id = "Cultiway.Created." + Guid.NewGuid().ToString("N");
            projected.Name = "自创·" + action.Name + "显化"; projected.Creator = actor.Base.id; projected.Seed = seed;
            projected.Impact = new ConstructionImpact { Force = random.Next(2) == 0 ? 8f : -8f };
            if (random.Next(3) == 0) AddContactGrowth(projected, projected.Root);
            if (random.Next(3) == 0) AddContactStop(projected, projected.Root);
            if (random.Next(3) == 0) AddFracture(projected.Root);
            if (random.Next(3) == 0) AddRemainder(projected);
            Register(projected); Learn(actor, projected.Id); return projected.Id;
        }
        string id = "Cultiway.Created." + Guid.NewGuid().ToString("N");
        var children = new List<ConstructionNode>();
        int count = 1 + random.Next(3);
        for (int i = 0; i < count; i++)
        {
            float detach = 1.1f + i * .18f;
            bool ice = random.Next(2) == 0;
            children.Add(new ConstructionNode
            {
                Id = "edge_" + i,
                Form = "sword",
                Anchor = count == 1 ? "tip_*" : "tip_" + i,
                Surface = ice ? "ice" : "metal",
                Matter = ice ? "ice" : "iron",
                Scale = .25f + (float)random.NextDouble() * .15f,
                Rotation = new[] { (float)random.Next(-35, 36), (float)random.Next(-35, 36), 0f },
                ActiveFrom = detach,
                Operations = new[]
                {
                    new ConstructionOperation { Kind = ConstructionOperationKind.Grow, At = .25f, Seconds = .6f },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Detach, At = detach },
                    new ConstructionOperation { Kind = ConstructionOperationKind.Fly, At = detach, Vector = new[] { 0f, 0f, 7f }, TurnRate = 100f + random.Next(100) }
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
                Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .8f } },
                Children = children.ToArray()
            }
        };
        if (random.Next(2) == 0)
        {
            foreach (ConstructionNode child in definition.Root.Children)
            {
                child.Offset = new[] { (float)random.NextDouble() * 5f - 2.5f, (float)random.NextDouble() * 5f - 2.5f, 2f + (float)random.NextDouble() * 2f };
                child.Operations = child.Operations.Concat(new[] { new ConstructionOperation
                { Kind = ConstructionOperationKind.Gather, At = .15f, Seconds = .7f, GatherRotation = new float[3] } }).ToArray();
            }
            definition.Name += "·聚锋"; definition.Cost += 2f;
        }
        if (random.Next(3) == 0) foreach (ConstructionNode child in definition.Root.Children) AddFracture(child);
        int placement = random.Next(3);
        if (placement == 1) definition = ConstructionPlacementEdits.Follow(definition, AbilityAnchor.Source);
        if (placement == 2) definition = ConstructionPlacementEdits.Orbit(definition, AbilityAnchor.Source,
            1.5f + (float)random.NextDouble() * 2f, 3f + (float)random.NextDouble() * 2f);
        if (placement != 0) definition.Name += placement == 1 ? "·随身" : "·环绕";
        definition = ConstructionMagnitudeEdits.Resize(definition, .8f + (float)random.NextDouble() * .6f);
        definition = ConstructionMagnitudeEdits.Haste(definition, .8f + (float)random.NextDouble() * .8f);
        definition = ConstructionMagnitudeEdits.Duration(definition, .8f + (float)random.NextDouble() * .6f);
        definition = ConstructionMagnitudeEdits.Sustain(definition, .3f + (float)random.NextDouble() * .7f);
        if (random.Next(3) == 0) AddContactGrowth(definition, definition.Root.Children[0]);
        if (random.Next(3) == 0) AddContactStop(definition, definition.Root.Children[0]);
        if (random.Next(3) == 0) AddRemainder(definition);
        if (random.Next(3) == 0) definition = ConstructionMagnitudeEdits.Repeat(definition, 2, .7f, random.Next(2) * .3f);
        if (random.Next(3) == 0) definition.Impact = new ConstructionImpact { Force = random.Next(2) == 0 ? 6f : -6f };
        Register(definition);
        Learn(actor, id);
        return id;
    }

    /// <summary>从共同形体、材料与沿地运动创作，不另设一类技能执行器。</summary>
    /// <param name="actor">实际创造者。</param>
    /// <param name="random">此份创作使用的随机源。</param>
    /// <param name="seed">保存后不重新抽取的种子。</param>
    private static string CreateGround(ActorExtend actor, System.Random random, int seed)
    {
        bool water = random.Next(2) == 0;
        var definition = new AbilityDefinition
        {
            Id = "Cultiway.Created." + Guid.NewGuid().ToString("N"), Name = water ? "自创·逐地流" : "自创·伏地枝",
            Creator = actor.Base.id, Seed = seed, Cost = 5f, Cooldown = 4f, Lifetime = 3f, Power = 1f,
            Binding = new ConstructionBinding { Anchor = AbilityAnchor.Source },
            Root = new ConstructionNode
            {
                Id = "ground", Form = water ? "water_flow" : "branch", Matter = water ? "water" : "wood", Volume = water ? 16f : (float?)null,
                Offset = new[] { 3f, 0f, 0f }, Rotation = new[] { 0f, 0f, -90f }, Scale = .5f + (float)random.NextDouble() * .5f,
                Operations = water ? new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Animate, Clip = "run" } } : Array.Empty<ConstructionOperation>()
            }
        };
        definition.Root.Operations = definition.Root.Operations.Concat(new[] { new ConstructionOperation
        { Kind = ConstructionOperationKind.Fly, Vector = new[] { 0f, 3f + (float)random.NextDouble() * 4f, 0f }, InheritVelocity = false, Ground = ConstructionGroundResponse.Follow } }).ToArray();
        if (water) definition.Root.Operations = definition.Root.Operations.Concat(new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Flow, At = .15f, Flow = new ConstructionFlow() } }).ToArray();
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

        Register(definition);
        Learn(actor, definition.Id);
        return definition.Id;
    }

    /// <summary>为实际末端创作结束后独立继续的残留，接收真实余力而非新的初始威力。</summary>
    /// <param name="definition">已经完成主要改造的实际资料。</param>
    private static void AddRemainder(AbilityDefinition definition)
    {
        ConstructionNode source = definition.Root;
        while (source.Children.Length > 0) source = source.Children[0];
        source.DisappearAt = definition.Lifetime - .5f;
        source.Children = new[] { new ConstructionNode
        {
            Id = "remnant", Form = source.Form, Matter = source.Matter, State = source.State, Scale = source.Scale * .65f,
            Activation = new ConstructionActivation { Trigger = ConstructionChangeTrigger.End, InheritRemainder = true },
            Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Fly, Vector = new[] { 0f, 0f, 4f },
                TurnRate = 180f, InheritVelocity = false } }
        } };
    }

    /// <summary>给已有生成主体加入真实损耗后的分件碎解，仍使用共同资料和运行。</summary>
    /// <param name="node">有真实多部件结构的主体。</param>
    private static void AddFracture(ConstructionNode node)
    {
        var resource = FormResources.Load(node.Form);
        node.Operations = node.Operations.Concat(new[] { new ConstructionOperation
        {
            Kind = ConstructionOperationKind.Fracture,
            When = new ConstructionCondition { Trigger = ConstructionChangeTrigger.ForceBelow, Threshold = .45f },
            Fracture = new ConstructionFracture { Seconds = 1.2f,
                ActiveParts = resource.Description.Parts.Where((_, index) => resource.Description.Contacts.Any(contact => contact.Part == index)).Select(part => part.Id).ToArray() }
        } }).ToArray();
    }

    /// <summary>通过共用条件操作创作接触后驻留的主体，不重新施放或重置接触。</summary>
    /// <param name="definition">完整创作资料。</param>
    /// <param name="source">真正接触的部位。</param>
    private static void AddContactStop(AbilityDefinition definition, ConstructionNode source)
    {
        source.OnContact = ConstructionContactResponse.Continue;
        source.Operations = source.Operations.Concat(new[] { new ConstructionOperation
        { Kind = ConstructionOperationKind.Stop, When = new ConstructionCondition() } }).ToArray();
        bool canGrow = !AbilityMatters.Resolve(source.Matter, source.State, "创作部位").HoldsShape;
        if (canGrow) source.Operations = source.Operations.Concat(new[] { new ConstructionOperation
        { Kind = ConstructionOperationKind.Grow, Seconds = .65f, TargetScale = 1.6f,
            When = new ConstructionCondition { Delay = .15f } } }).ToArray();
        if (canGrow) source.Operations = source.Operations.Concat(new[] { new ConstructionOperation
        { Kind = ConstructionOperationKind.Spin, Vector = new[] { 0f,0f,60f },
            When = new ConstructionCondition { Trigger = ConstructionChangeTrigger.SizeAbove, Threshold = 1.4f } } }).ToArray();
        definition.Cost += 1f; definition.Name += canGrow ? "·凝驻长成" : "·凝驻";
    }

    /// <summary>创作时为实际来源补上一段接触后才开始的独立生长，仍使用普通部位及操作。</summary>
    /// <param name="definition">本次独立创作资料。</param>
    /// <param name="source">实际发出事件的部位。</param>
    private static void AddContactGrowth(AbilityDefinition definition, ConstructionNode source)
    {
        source.Children = source.Children.Concat(new[] { new ConstructionNode
        {
            Id = "contact_growth", Form = "branch", Matter = source.Matter, State = source.State,
            Scale = 1.2f, ActiveFrom = .3f, DisappearAt = 1.2f,
            Activation = new ConstructionActivation(),
            Operations = new[] { new ConstructionOperation { Kind = ConstructionOperationKind.Grow, Seconds = .45f } }
        } }).ToArray();
        definition.Cost += 2f; definition.Name += "·触生";
    }

    /// <summary>按既有月度修炼触发小概率创造，首轮每位修士最多自然创出一项。</summary>
    /// <param name="actor">本轮修炼者。</param>
    internal static void TryCreateDuringCultivation(ActorExtend actor)
    {
        if (!GeneralSettings.EnableSkillSystems || actor.GetCultisys<Xian>().level < 2 ||
            actor.E.TryGetComponent(out ConstructedKnowledge known) && known.Ids.Length > 0 || !Randy.randomChance(.015f)) return;
        Create(actor, unchecked((int)actor.Base.id ^ (int)World.world.getCurWorldTime()));
    }

    /// <summary>向现有玩家与自主出手入口公开真正已经学会的能力。</summary>
    /// <param name="caster">当前修士。</param>
    /// <param name="output">统一能力集合。</param>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!GeneralSettings.EnableSkillSystems || !caster.E.TryGetComponent(out ConstructedKnowledge known)) return;
        foreach (string id in known.Ids)
            if (definitions.ContainsKey(id)) output.Add(new ActiveAbilityHandle(ProviderId, caster.E, id));
    }

    /// <summary>首轮构造提供战斗用途。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) => ActiveAbilityChannel.Combat;
    /// <summary>保留统一玩家和自主施放，不建立构造专用界面。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle) => new(
        definitions[handle.EntryId].Name, SpriteTextureLoader.getSprite("cultiway/icons/skills/knight/repulse"),
        ActiveAbilityChannel.Combat, PlanFor(handle.EntryId).Impact.Targets == SkillUseTargetRelation.Self ? ActiveAbilityTargetMode.Self : ActiveAbilityTargetMode.Object, ActiveAbilityActivationMode.Instant,
        PlanFor(handle.EntryId).Body == null ? ActiveAbilityCastMobility.Mobile : ActiveAbilityCastMobility.StationaryDuringRecovery,
        targetRelation: PlanFor(handle.EntryId).Impact.Targets,
        canCastConcurrently: PlanFor(handle.EntryId).Body == null);
    /// <summary>按本能力独立冷却及实际真元报告可用状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!Owns(caster, handle)) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (PlanFor(handle.EntryId).Body != null && caster.Base.attack_timer > 0f)
            return new(ActiveAbilityControlBlockReason.Cooldown, caster.Base.attack_timer);
        if (!BodyReady(caster, PlanFor(handle.EntryId))) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (cooldowns.TryGetValue((caster.Base.id, handle.EntryId), out double until) && until > LogicTime)
            return new(ActiveAbilityControlBlockReason.Cooldown, (float)(until - LogicTime));
        return CanPay(caster, definitions[handle.EntryId]) ? ActiveAbilityControlState.Ready : new(ActiveAbilityControlBlockReason.InsufficientResource);
    }
    /// <summary>检查真实知识及来源状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        GeneralSettings.EnableSkillSystems && caster.Base.isAlive() && caster.Base.hasHealth() && ResolveControlState(caster, handle).CanUse &&
        (PlanFor(handle.EntryId).Body == null || caster.Base.isAttackPossible());
    /// <summary>选取和实际接触共用明确的目标关系，仍需实际活体及选择距离。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">本次选择。</param>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target) =>
        CanPrepare(caster, handle, target.Object) && target.Object?.isActor() == true && target.Object.isAlive() && target.Object.a.hasHealth() &&
        SkillTargetRelationResolver.Matches(PlanFor(handle.EntryId).Impact.Targets, caster.Base, target.Object.a) &&
        StartingDistance(caster.Base, target.Object.a) <= ResolveRange(caster, handle, target.Object);
    /// <summary>作为独立法术参与已有自主释放选择。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 12;
    /// <summary>向既有决策报告威力与实际消耗。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        CompiledAbility plan = PlanFor(handle.EntryId);
        bool hostile = plan.Impact.Targets == SkillUseTargetRelation.Hostile;
        return new(hostile && plan.ContactDamageScale > 0f ? 1f : 0f, plan.HasBarrier ? 1f : 0f, hostile ? 0f : 1f, plan.ContactForce > 0f ? 1f : 0f,
            Mathf.Max(caster.Base.stats["damage"] * definitions[handle.EntryId].Power * Mathf.Max(plan.ContactDamageScale, plan.HasBarrier ? 1f : 0f), plan.ContactForce), definitions[handle.EntryId].Cost, 1f);
    }
    /// <summary>首轮生长构造的目标放置距离，不代替作用形状。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => Range(caster, handle.EntryId, target);
    /// <summary>本构造没有固定圆形伤害区，使用实际部位接触。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) => 0f;
    /// <summary>受理后一次支付，独立运行；失败不创建形体或启动冷却。</summary>
    /// <param name="caster">施法者。</param>
    /// <param name="handle">已经学会的构造。</param>
    /// <param name="target">实际敌方。</param>
    /// <param name="origin">玩家、脚本或自主选择。</param>
    public bool TryUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        return Start(caster, handle.EntryId, target.Object.a, origin);
    }

    /// <summary>原有身体能力的入口沿用统一执行，不要求每个人预先学会一份临时容器。</summary>
    /// <param name="caster">真实身体。</param>
    /// <param name="id">已经登记的构造。</param>
    /// <param name="target">实际目标。</param>
    /// <param name="origin">实际调用方式。</param>
    internal static bool Start(ActorExtend caster, string id, Actor target, ActiveAbilityUseOrigin origin)
    {
        CompiledAbility plan = PlanFor(id); AbilityDefinition definition = definitions[id];
        if (!BodyReady(caster, plan) || !CanPay(caster, definition) ||
            !WorldMaterialService.CanReserve(plan)) return false;
        Vector2 from = caster.Base.current_position, to = target.current_position;
        if (simulation.pending == null)
            simulation.CaptureTerrain(Rect.MinMaxRect(Mathf.Min(from.x, to.x), Mathf.Min(from.y, to.y), Mathf.Max(from.x, to.x), Mathf.Max(from.y, to.y)));
        double startedAt = LogicTime;
        StartingPose(caster.Base, out Vector3 source, out float sourceHeight);
        StartingPose(target, out Vector3 point, out float targetHeight);
        Vector3 direction = point - source;
        direction.z = 0f; direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.right;
        BodyActionBinding body = null;
        BodyFrame? frame = null;
        if (plan.Body != null)
        {
            body = new BodyActionBinding(caster.Base, plan.Body, direction, point, targetHeight); frame = body.Frame(source, sourceHeight);
        }
        var inputs = new AbilityInputs(source, point, direction, sourceHeight);
        GetHitEvent damage = CombatDamageEffects.PrepareDamage(caster.Base, caster.Base.stats["damage"] * definition.Power, ElementComposition.Static.empty);
        var cast = new AbilityCast(plan, Matrix4x4.TRS(point, Quaternion.identity, Vector3.one), frame, inputs, damage.Damage, simulation.terrain, PaymentBudget(caster, definition));
        if (plan.Body != null && !BodyControlState.TryAcquire(caster.Base.id, plan.Body.Occupancy)) return false;
        var running = new RunningCast
        {
            Id = ++nextCastId, Resource = definition.Resource,
            ReservedResidues = plan.ResidueCount,
            ReservedMaterialCells = plan.ResidueCells,
            Cast = cast,
            Source = caster.Base,
            SourceId = caster.Base.id,
            Target = target,
            TargetId = target.id,
            LastTarget = point + Vector3.forward * targetHeight * .5f,
            Power = definition.Power,
            Body = body,
            BodyControlled = body != null,
            Direction = direction,
            LastTargetFoot = point,
            LastWorldTime = startedAt
        };
        running.InitialOwner = new CastAuthority(caster.Base, damage, target);
        running.Owners = new CastAuthority[plan.FormCount];
        for (int i = 0; i < running.Owners.Length; i++) running.Owners[i] = running.InitialOwner;
        running.LastSourceFoot = inputs.Source; running.LastSourceHeight = inputs.SourceHeight;
        if (casts.Count == 0) simulation.RememberActors(startedAt);
        WorldMaterialService.Reserve(running.ReservedResidues, running.ReservedMaterialCells);
        casts.Add(running);
        if (body != null && simulation.actorsSampled) NotifyActorChanged(caster.Base);
        Pay(caster, definition);
        cooldowns[(caster.Base.id, id)] = startedAt + definition.Cooldown;
        if (body != null)
        {
            caster.Base.lookTowardsPosition(caster.Base.current_position + (Vector2)body.Direction);
            caster.Base.startAttackCooldown();
            caster.Base.attack_timer = Mathf.Max(caster.Base.attack_timer, plan.Body.Duration);
        }
        CastStarted?.Invoke(new ConstructedCastStarted(running.Id, id, caster.Base.id, origin, startedAt));
        return true;
    }

    /// <summary>身体动作的真实触达范围，仅用于选择目标；命中仍使用实际掌面或器物。</summary>
    /// <param name="caster">真实身体。</param>
    /// <param name="id">当前构造。</param>
    /// <param name="target">候选对象。</param>
    internal static float Range(ActorExtend caster, string id, BaseSimObject target)
    {
        CompiledAbility plan = PlanFor(id);
        if (plan.TargetRange.HasValue) return plan.TargetRange.Value;
        if (plan.Body == null) return 12f;
        StartingPose(caster.Base, out _, out float height);
        float targetHeight = 0f;
        if (target?.isActor() == true) StartingPose(target.a, out _, out targetHeight);
        float reach = height * (plan.Body.WeaponForm == null ? .65f : 1.9f) + targetHeight * .24f;
        foreach (CompiledForm node in plan.Nodes)
            if (node.FlyAt >= 0f) reach = Mathf.Max(reach, node.Velocity.magnitude * (plan.Lifetime - node.FlyAt));
        return reach;
    }

    /// <summary>不同来源共用身体准入，实际部位不存在时不凭空生成人类手臂。</summary>
    /// <param name="caster">实际来源。</param>
    /// <param name="plan">当前计划。</param>
    private static bool BodyReady(ActorExtend caster, CompiledAbility plan) => plan.Body == null ||
        BodyActionBinding.CanBind(caster, plan.Body) && !BodyControlState.IsBusy(caster.Base.id, plan.Body.Occupancy) &&
        !KnightTechniqueRuntimeService.IsBusy(caster) && !caster.Base.stats.hasTag("frozen_ai") && !caster.Base.stats.hasTag("immovable");

    /// <summary>原有徒手入口可用性，不借用另一人物或已经失效的身体。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="id">已登记的基础动作。</param>
    internal static bool CanStartBody(ActorExtend caster, string id) => GeneralSettings.EnableSkillSystems && caster != null &&
        !caster.Base.isRekt() && caster.Base.isAlive() && caster.Base.hasHealth() && BodyReady(caster, PlanFor(id));

    /// <summary>是否具有动作需要的真实身体，不因动作正在运行就让能力栏消失。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="id">已经登记的动作。</param>
    internal static bool HasBodyFor(ActorExtend caster, string id) => caster != null && !caster.Base.isRekt() &&
        caster.Base.isAlive() && PlanFor(id).Body != null && BodyActionBinding.CanBind(caster, PlanFor(id).Body);

    /// <summary>按来源自己的资源事实判断，不为骑士生成真元组件。</summary>
    /// <param name="caster">实际来源。</param>
    /// <param name="definition">已登记构造。</param>
    private static bool CanPay(ActorExtend caster, AbilityDefinition definition) => definition.Resource switch
    {
        "none" => true,
        "wakan" => caster.HasCultisys<Xian>() && caster.GetCultisys<Xian>().wakan >= definition.Cost,
        "vigor" => caster.HasCultisys<Knight>() && caster.GetCultisys<Knight>().vigor >= definition.Cost,
        "mana" => (double)caster.Base.getMana() >= definition.Cost,
        "stamina" => (double)caster.Base.data.stamina >= definition.Cost,
        _ => false
    };
    /// <summary>按即将写入的实际资源值确定可回收份额，不虚构浮点精度之外的支付。</summary>
    /// <param name="caster">已确认可支付且本次尚未让出主线程的来源。</param>
    /// <param name="definition">同次正式支付采用的资料。</param>
    private static double PaymentBudget(ActorExtend caster, AbilityDefinition definition)
    {
        if (definition.Resource is not ("wakan" or "vigor")) return definition.Cost;
        float before = definition.Resource == "wakan" ? caster.GetCultisys<Xian>().wakan : caster.GetCultisys<Knight>().vigor;
        float after = before - definition.Cost;
        return Math.Min(definition.Cost, Math.Max(0, (double)before - after));
    }

    /// <summary>能力正式受理后只支付一次。</summary>
    /// <param name="caster">实际来源。</param>
    /// <param name="definition">已经确认可支付的构造。</param>
    private static void Pay(ActorExtend caster, AbilityDefinition definition)
    {
        switch (definition.Resource)
        {
            case "wakan": caster.GetCultisys<Xian>().wakan -= definition.Cost; break;
            case "vigor": caster.GetCultisys<Knight>().vigor -= definition.Cost; break;
            case "mana": caster.Base.setMana(caster.Base.getMana() - (int)definition.Cost, false); break;
            case "stamina": caster.Base.data.stamina -= (int)definition.Cost; break;
        }
    }

    /// <summary>不接受其他人物或伪造来源的句柄。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">当前能力。</param>
    private bool Owns(ActorExtend caster, ActiveAbilityHandle handle) => handle.ProviderId == ProviderId && handle.Source == caster.E &&
        definitions.ContainsKey(handle.EntryId) && caster.E.TryGetComponent(out ConstructedKnowledge known) && known.Ids.Contains(handle.EntryId);

    /// <summary>实际材料接到已有元素抗性；这不代表冻结或材料相撞等反应已经完成。</summary>
    /// <param name="matter">由构造明确指定的实际材料。</param>
    private static ElementComposition Composition(string matter) => matter switch
    {
        "physical" => ElementComposition.Static.empty,
        "wood" => ElementComposition.Static.Wood,
        "iron" => ElementComposition.Static.Iron,
        "molten_iron" => ElementComposition.Static.IronFire,
        "water" => ElementComposition.Static.Water,
        "ice" => ElementComposition.Static.Ice,
        _ => throw new InvalidDataException("尚未提供实际材料规则：" + matter)
    };
    /// <summary>换世界时回收施放、缓存及恢复时点，不跨世界保留知识。</summary>
    private static void Clear()
    {
        simulation.WorldClearing?.Invoke();
        worldGeneration++;
        simulation?.ResetSimulation(cancel: true);
        simulation.terrain = null; simulation.terrainNeedsRefresh = false;
        presentation = new PresentedFrame(0d, Array.Empty<FormVisualInstance>(), new());
        foreach (RunningCast running in casts) ReleaseMaterialReservation(running);
        foreach (RunningCast running in casts)
            if (running.BodyControlled && running.Source.data?.id == running.SourceId) running.Source.target_angle.z = 0f;
        casts.Clear(); definitions.Clear(); compiled.Clear(); cooldowns.Clear(); BodyControlState.Clear(); AbilityCompiler.Clear(); FormVisualScene.Clear();
        foreach (AbilityDefinition body in BodyActionCatalog.All) Register(body);
    }

    /// <summary>无论正常收势还是中断，解除本次控制，不销毁借用身体和器物。</summary>
    /// <param name="index">实际施放索引。</param>
    /// <param name="running">结束的施放。</param>
    private static void Finish(int index, RunningCast running)
    {
        ReleaseBody(running);
        ReleaseMaterialReservation(running);
        casts.RemoveAt(index);
    }

    /// <summary>身体收势或中断只释放本次身体控制，已经离手的形体继续运行。</summary>
    /// <param name="running">本次施放。</param>
    private static void ReleaseBody(RunningCast running)
    {
        if (!running.BodyControlled) return;
        BodyControlState.Release(running.SourceId, running.Cast.Plan.Body.Occupancy);
        if (running.Source.data?.id == running.SourceId) running.Source.target_angle.z = 0f;
        running.BodyControlled = false;
    }

    /// <summary>单次施放的主线程对象绑定与去重记录。</summary>
    private sealed class RunningCast
    {
        /// <summary>一次施放的稳定运行编号。</summary>
        internal long Id;

        /// <summary>已经受理但尚未交给世界的记录与水格额度。</summary>
        internal int ReservedResidues, ReservedMaterialCells;
        /// <summary>开始时确定的原支付资源，不随反射或目录更新改变。</summary>
        internal string Resource;
        /// <summary>主线程已经确认过的累计份额和材料转化量。</summary>
        internal double ConfirmedRecovery, ConfirmedVolume;
        /// <summary>因原资源精度暂未表示的小数；容量不足和施放结束不保留此数。</summary>
        internal double RecoveryFraction;
        /// <summary>纯数值运行状态。</summary>
        internal AbilityCast Cast;
        /// <summary>实际归属及目标。</summary>
        internal Actor Source, Target;
        /// <summary>对象池重用人物对象时仍能识别原始归属和目标。</summary>
        internal long SourceId, TargetId;
        /// <summary>借用实际身体和器物的绑定，非身体能力为空。</summary>
        internal BodyActionBinding Body;
        /// <summary>本次是否仍实际占用身体，收势后独立形体不再占用。</summary>
        internal bool BodyControlled;
        /// <summary>锁定的数值朝向。</summary>
        internal Vector3 Direction;
        /// <summary>最后有效的目标脚下，用于连接失去目标后的处理。</summary>
        internal Vector3 LastTargetFoot;
        /// <summary>最后有效目标点。</summary>
        internal Vector3 LastTarget;
        /// <summary>开始时确定的作用倍率。</summary>
        internal float Power;
        /// <summary>上次完整推进的实际世界时间；不使用可能未包含加速倍率的调度步长。</summary>
        internal double LastWorldTime;
        /// <summary>同组、当前归属及目标的实际接触次数和时间，身体与离手交接共用。</summary>
        internal readonly Dictionary<(int Group, long Owner, int Reflection, long Target), ContactMemory> Hits = new();
        /// <summary>整次施放每组的总次数，不因反射重置。</summary>
        internal readonly Dictionary<int, int> GroupHits = new();
        /// <summary>最初身份及各部位当前控制者，仍附着的部位共享交接。</summary>
        internal CastAuthority InitialOwner;
        /// <summary>生成部位的实际归属。</summary>
        internal CastAuthority[] Owners;
        /// <summary>曾发生归属交接，来源失效时逐部位处理。</summary>
        internal bool HasTransfers;
        /// <summary>来源失效后独立部位仍可使用的最后数值输入。</summary>
        internal Vector3 LastSourceFoot;
        /// <summary>最后实际来源体高。</summary>
        internal float LastSourceHeight;
        /// <summary>身体保留最初控制者，生成部位读取各自归属。</summary>
        /// <param name="node">实际部位。</param>
        internal CastAuthority Owner(int node) => node < 0 ? InitialOwner : Owners[Cast.AuthorityNode(node)];
        /// <summary>归属或反射次数变化后允许新的实际接触，不重用旧阵营的去重记录。</summary>
        /// <param name="node">实际部位。</param>
        /// <param name="target">目标身份。</param>
        internal (int, long, int, long) HitKey(int node, long target) => (Cast.ContactGroup(node), Owner(node).Id, Cast.Reflections(node), target);
        /// <summary>本步前后的作用形状，预测及回退时复用。</summary>
        internal readonly List<AbilityCapsule> Before = new(), After = new();
        /// <summary>身体上一步的实际位移，用于运动目标的共同接触。</summary>
        internal Vector3 BeforeTorso;
    }
    /// <summary>世界保存的完整构造集合。</summary>
    public sealed class DefinitionSave
    {
        /// <summary>保存实际构造而非注册模板引用。</summary>
        public AbilityDefinition[] Definitions;
    }
    /// <summary>人物掌握和必要恢复时间。</summary>
    public sealed class KnowledgeSave
    {
        /// <summary>已学编号。</summary>
        public string[] Ids;
        /// <summary>以剩余秒数保存，避免世界时钟差异。</summary>
        public Dictionary<string, float> Cooldowns;
    }
}
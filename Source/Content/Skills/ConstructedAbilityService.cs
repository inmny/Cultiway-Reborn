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
        var service = new ConstructedAbilityService();
        simulation = service;
        ModClass.I.GeneralLogicSystems.Add(service);
        ActiveAbilityService.Register(service);
        PatchMapBox.RegisterActionOnClearWorld(Clear);
        PatchActor.RegisterHideHandItemPredicate(IsWeaponBorrowed);
        foreach (AbilityDefinition body in BodyActionCatalog.All) Register(body);
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, DefinitionSave>("ability_constructions", "自创能力构造", 1,
            _ => new DefinitionSave { Definitions = definitions.Values.ToArray() }, (_, data) =>
            {
                foreach (AbilityDefinition definition in data.Definitions)
                    try { Register(definition); }
                    catch (Exception error) when (error is IOException or ArgumentException or JsonException)
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

    /// <summary>在创建边界复制完整定义，编译成功后才登记。</summary>
    /// <param name="definition">外部当前构造。</param>
    public static void Register(AbilityDefinition definition)
    {
        CompiledAbility plan = AbilityCompiler.Compile(definition);
        foreach (string matter in plan.Matters) Composition(matter);
        if (plan.Body != null) BodyActionBinding.ValidateResources(plan.Body);
        if (definition.Resource is not ("none" or "wakan" or "vigor" or "mana" or "stamina") ||
            definition.Resource == "none" && definition.Cost != 0f ||
            definition.Resource is "mana" or "stamina" && definition.Cost != Mathf.Floor(definition.Cost))
            throw new InvalidDataException("尚未提供此资源支付，或无支付能力仍要求扣费：" + definition.Resource);
        AbilityDefinition copy = JsonConvert.DeserializeObject<AbilityDefinition>(JsonConvert.SerializeObject(definition));
        definitions[copy.Id] = copy; compiled[copy.Id] = plan;
    }

    /// <summary>授予已经存在的构造，复制编号集合避免修改共用资料。</summary>
    /// <param name="actor">学习者。</param>
    /// <param name="id">稳定构造编号。</param>
    public static void Learn(ActorExtend actor, string id)
    {
        if (!definitions.ContainsKey(id)) throw new ArgumentException("尚未登记构造：" + id, nameof(id));
        string[] old = actor.E.TryGetComponent(out ConstructedKnowledge known) ? known.Ids : Array.Empty<string>();
        if (old.Contains(id)) return;
        actor.E.AddComponent(new ConstructedKnowledge { Ids = old.Concat(new[] { id }).ToArray() });
    }

    /// <summary>从共用结构与操作产生未登记的完整构造，保存实际选择而非重新抽取。</summary>
    /// <param name="actor">创造者。</param>
    /// <param name="seed">可复现的创作种子。</param>
    public static string Create(ActorExtend actor, int seed)
    {
        var random = new System.Random(seed);
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
        Register(new AbilityDefinition
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
        });
        Learn(actor, id);
        return id;
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
        ActiveAbilityChannel.Combat, ActiveAbilityTargetMode.Object, ActiveAbilityActivationMode.Instant,
        compiled[handle.EntryId].Body == null ? ActiveAbilityCastMobility.Mobile : ActiveAbilityCastMobility.StationaryDuringRecovery,
        canCastConcurrently: compiled[handle.EntryId].Body == null);
    /// <summary>按本能力独立冷却及实际真元报告可用状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!Owns(caster, handle)) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (compiled[handle.EntryId].Body != null && caster.Base.attack_timer > 0f)
            return new(ActiveAbilityControlBlockReason.Cooldown, caster.Base.attack_timer);
        if (!BodyReady(caster, compiled[handle.EntryId])) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (cooldowns.TryGetValue((caster.Base.id, handle.EntryId), out double until) && until > World.world.getCurWorldTime())
            return new(ActiveAbilityControlBlockReason.Cooldown, (float)(until - World.world.getCurWorldTime()));
        return CanPay(caster, definitions[handle.EntryId]) ? ActiveAbilityControlState.Ready : new(ActiveAbilityControlBlockReason.InsufficientResource);
    }
    /// <summary>检查真实知识及来源状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        GeneralSettings.EnableSkillSystems && caster.Base.isAlive() && caster.Base.hasHealth() && ResolveControlState(caster, handle).CanUse &&
        (compiled[handle.EntryId].Body == null || caster.Base.isAttackPossible());
    /// <summary>只接受能力距离内的实际敌对活体。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">本次选择。</param>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target) =>
        CanPrepare(caster, handle, target.Object) && target.Object?.isActor() == true && target.Object.isAlive() && target.Object.a.hasHealth() &&
        SkillTargetRelationResolver.IsHostile(caster.Base, target.Object.a) &&
        Vector2.Distance(caster.Base.current_position, target.Object.a.current_position) <= ResolveRange(caster, handle, target.Object);
    /// <summary>作为独立法术参与已有自主释放选择。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 12;
    /// <summary>向既有决策报告威力与实际消耗。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        new(1f, 0f, 0f, 0f, caster.Base.stats["damage"] * definitions[handle.EntryId].Power, definitions[handle.EntryId].Cost, 1f);
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
        CompiledAbility plan = compiled[id]; AbilityDefinition definition = definitions[id];
        if (!BodyReady(caster, plan) || !CanPay(caster, definition)) return false;
        Vector3 point = SpatialActorPose.Foot(target);
        Vector3 direction = target.current_position - caster.Base.current_position;
        direction.z = 0f; direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.right;
        BodyActionBinding body = null;
        BodyFrame? frame = null;
        if (plan.Body != null)
        {
            body = new BodyActionBinding(caster.Base, plan.Body, direction, target); frame = body.Frame(caster.Base);
        }
        var inputs = new AbilityInputs(SpatialActorPose.Foot(caster.Base), point, direction, SpatialActorPose.Height(caster.Base));
        GetHitEvent damage = CombatDamageEffects.PrepareDamage(caster.Base, caster.Base.stats["damage"] * definition.Power, ElementComposition.Static.empty);
        var cast = new AbilityCast(plan, Matrix4x4.TRS(point, Quaternion.identity, Vector3.one), frame, inputs, damage.Damage);
        if (plan.Body != null && !BodyControlState.TryAcquire(caster.Base.id, plan.Body.Occupancy)) return false;
        var running = new RunningCast
        {
            Id = ++nextCastId,
            Cast = cast,
            Source = caster.Base,
            SourceId = caster.Base.id,
            Target = target,
            TargetId = target.id,
            LastTarget = point + Vector3.forward * SpatialActorPose.Height(target) * .5f,
            Power = definition.Power,
            Damage = damage,
            Body = body,
            BodyControlled = body != null,
            Direction = direction,
            LastTargetFoot = point,
            LastWorldTime = World.world.getCurWorldTime()
        };
        if (casts.Count == 0) simulation.RememberActors(World.world.getCurWorldTime());
        casts.Add(running);
        Pay(caster, definition);
        cooldowns[(caster.Base.id, id)] = World.world.getCurWorldTime() + definition.Cooldown;
        if (body != null)
        {
            caster.Base.lookTowardsPosition(caster.Base.current_position + (Vector2)body.Direction);
            caster.Base.startAttackCooldown();
            caster.Base.attack_timer = Mathf.Max(caster.Base.attack_timer, plan.Body.Duration);
        }
        CastStarted?.Invoke(new ConstructedCastStarted(running.Id, id, caster.Base.id, origin));
        return true;
    }

    /// <summary>身体动作的真实触达范围，仅用于选择目标；命中仍使用实际掌面或器物。</summary>
    /// <param name="caster">真实身体。</param>
    /// <param name="id">当前构造。</param>
    /// <param name="target">候选对象。</param>
    internal static float Range(ActorExtend caster, string id, BaseSimObject target)
    {
        CompiledAbility plan = compiled[id];
        if (plan.Body == null) return 12f;
        float reach = SpatialActorPose.Height(caster.Base) * (plan.Body.WeaponForm == null ? .65f : 1.9f) +
            (target?.isActor() == true ? SpatialActorPose.Height(target.a) * .24f : 0f);
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
        !caster.Base.isRekt() && caster.Base.isAlive() && caster.Base.hasHealth() && BodyReady(caster, compiled[id]);

    /// <summary>是否具有动作需要的真实身体，不因动作正在运行就让能力栏消失。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="id">已经登记的动作。</param>
    internal static bool HasBodyFor(ActorExtend caster, string id) => caster != null && !caster.Base.isRekt() &&
        caster.Base.isAlive() && compiled[id].Body != null && BodyActionBinding.CanBind(caster, compiled[id].Body);

    /// <summary>按来源自己的资源事实判断，不为骑士生成真元组件。</summary>
    /// <param name="caster">实际来源。</param>
    /// <param name="definition">已登记构造。</param>
    private static bool CanPay(ActorExtend caster, AbilityDefinition definition) => definition.Resource switch
    {
        "none" => true,
        "wakan" => caster.HasCultisys<Xian>() && caster.GetCultisys<Xian>().wakan >= definition.Cost,
        "vigor" => caster.HasCultisys<Knight>() && caster.GetCultisys<Knight>().vigor >= definition.Cost,
        "mana" => caster.Base.getMana() >= definition.Cost,
        "stamina" => caster.Base.data.stamina >= definition.Cost,
        _ => false
    };
    /// <summary>能力正式受理后只支付一次。</summary>
    /// <param name="caster">实际来源。</param>
    /// <param name="definition">已经确认可支付的构造。</param>
    private static void Pay(ActorExtend caster, AbilityDefinition definition)
    {
        switch (definition.Resource)
        {
            case "wakan": caster.GetCultisys<Xian>().wakan -= definition.Cost; break;
            case "vigor": caster.GetCultisys<Knight>().vigor -= definition.Cost; break;
            case "mana": caster.Base.setMana(Mathf.FloorToInt(caster.Base.getMana() - definition.Cost)); break;
            case "stamina": caster.Base.data.stamina -= Mathf.CeilToInt(definition.Cost); break;
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
        "water" => ElementComposition.Static.Water,
        "ice" => ElementComposition.Static.Ice,
        _ => throw new InvalidDataException("尚未提供实际材料规则：" + matter)
    };
    /// <summary>换世界时回收施放、缓存及恢复时点，不跨世界保留知识。</summary>
    private static void Clear()
    {
        simulation?.ResetSimulation(cancel: true);
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

    /// <summary>旧身体显示和三维适配只读取共同姿态，不另建动作时钟。</summary>
    /// <param name="actor">身体编号。</param>
    /// <param name="pose">当前完整姿态。</param>
    public static bool TryGetBodyPose(long actor, out BodyPose pose)
    {
        foreach (RunningCast running in casts)
            if (running.SourceId == actor && running.BodyControlled && running.Cast.BodyPose.HasValue)
            { pose = running.Cast.BodyPose.Value; return true; }
        pose = default; return false;
    }

    /// <summary>原版和第一人称隐藏当前由动作接管的静态持物，持有关系不改变。</summary>
    /// <param name="actor">当前身体。</param>
    public static bool IsWeaponBorrowed(Actor actor) => actor != null && casts.Any(running => running.SourceId == actor.id && running.BodyControlled && running.Body?.Weapon != null);

    /// <summary>单次施放的主线程对象绑定与去重记录。</summary>
    private sealed class RunningCast
    {
        /// <summary>一次施放的稳定运行编号。</summary>
        internal long Id;
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
        /// <summary>受理时确定的实际来源、层次、归属及缩放后的伤害模板。</summary>
        internal GetHitEvent Damage;
        /// <summary>上次完整推进的实际世界时间；不使用可能未包含加速倍率的调度步长。</summary>
        internal double LastWorldTime;
        /// <summary>每个共用接触组对每个人物只确认一次，包含身体及离手的交接。</summary>
        internal readonly HashSet<(int, long)> Hits = new();
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

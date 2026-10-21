using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Persistence;
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
public sealed class ConstructedAbilityService : BaseSystem, IActiveAbilityProvider
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
    /// <summary>共用本帧显示缓冲。</summary>
    private readonly List<FormVisualInstance> visuals = new();
    /// <summary>共用当前步的胶囊缓冲。</summary>
    private readonly List<AbilityCapsule> contacts = new();
    /// <summary>主线程采集的候选身体球。</summary>
    private readonly List<(Actor Actor, Vector3 Centre, float Radius)> candidates = new();

    /// <summary>登记两类正式保存。定义先于人物恢复，未知构造只丢弃对应学习关系。</summary>
    public static void Initialize()
    {
        var service = new ConstructedAbilityService();
        ModClass.I.GeneralLogicSystems.Add(service);
        ActiveAbilityService.Register(service);
        PatchMapBox.RegisterActionOnClearWorld(Clear);
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
            }) { BeforeActors = true });
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
                Id = "edge_" + i, Form = "sword", Anchor = count == 1 ? "tip_*" : "tip_" + i,
                Surface = ice ? "ice" : "metal", Matter = ice ? "ice" : "iron", Scale = .25f + (float)random.NextDouble() * .15f,
                Rotation = new[] { (float)random.Next(-35, 36), (float)random.Next(-35, 36), 0f }, ActiveFrom = detach,
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
            Id = id, Name = "自创·枝上凝锋", Creator = actor.Base.id, Seed = seed, Cost = 12f, Cooldown = 5f,
            Lifetime = 4f, Power = 1.2f,
            Root = new ConstructionNode
            {
                Id = "root", Form = "branch", Matter = "wood", Scale = 1.1f + (float)random.NextDouble() * .6f,
                ActiveFrom = .7f, ActiveUntil = 1.1f,
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
        ActiveAbilityChannel.Combat, ActiveAbilityTargetMode.Object, ActiveAbilityActivationMode.Instant, canCastConcurrently: true);
    /// <summary>按本能力独立冷却及实际真元报告可用状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!Owns(caster, handle) || !caster.HasCultisys<Xian>()) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (cooldowns.TryGetValue((caster.Base.id, handle.EntryId), out double until) && until > World.world.getCurWorldTime())
            return new(ActiveAbilityControlBlockReason.Cooldown, (float)(until - World.world.getCurWorldTime()));
        return caster.GetCultisys<Xian>().wakan < definitions[handle.EntryId].Cost
            ? new(ActiveAbilityControlBlockReason.InsufficientResource) : ActiveAbilityControlState.Ready;
    }
    /// <summary>检查真实知识及来源状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        GeneralSettings.EnableSkillSystems && caster.Base.isAlive() && caster.Base.isAttackPossible() && ResolveControlState(caster, handle).CanUse;
    /// <summary>只接受能力距离内的实际敌对活体。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">本次选择。</param>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target) =>
        CanPrepare(caster, handle, target.Object) && target.Object?.isActor() == true && target.Object.isAlive() &&
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
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 12f;
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
        AbilityDefinition definition = definitions[handle.EntryId];
        Vector3 point = SpatialActorPose.Foot(target.Object.a);
        var cast = new AbilityCast(compiled[handle.EntryId], Matrix4x4.TRS(point, Quaternion.identity, Vector3.one));
        casts.Add(new RunningCast
        {
            Cast = cast, Source = caster.Base, SourceId = caster.Base.id, Target = target.Object.a,
            TargetId = target.Object.a.id, LastTarget = point + Vector3.forward * SpatialActorPose.Height(target.Object.a) * .5f,
            Power = definition.Power
        });
        caster.GetCultisys<Xian>().wakan -= definition.Cost;
        cooldowns[(caster.Base.id, handle.EntryId)] = World.world.getCurWorldTime() + definition.Cooldown;
        return true;
    }

    /// <summary>主线程采集对象，数值状态推进后按相同姿态确认接触，再发布显示。</summary>
    protected override void OnUpdateGroup()
    {
        visuals.Clear();
        for (int index = casts.Count - 1; index >= 0; index--)
        {
            RunningCast running = casts[index];
            if (running.Source.id != running.SourceId || !running.Source.isAlive()) { casts.RemoveAt(index); continue; }
            Vector3 target = running.Target.id == running.TargetId && running.Target.isAlive()
                ? SpatialActorPose.Foot(running.Target) + Vector3.forward * SpatialActorPose.Height(running.Target) * .5f : running.LastTarget;
            running.LastTarget = target;
            candidates.Clear();
            CombatTargeting.ForEachHostile(running.Source, target, 48f, actor =>
            {
                float height = SpatialActorPose.Height(actor);
                candidates.Add((actor, SpatialActorPose.Foot(actor) + Vector3.forward * height * .5f, height * .4f));
            });
            float remaining = Tick.deltaTime;
            while (remaining > 0f && !running.Cast.Finished)
            {
                float step = Mathf.Min(remaining, 1f / 60f); running.Cast.Advance(step, target); remaining -= step;
                contacts.Clear(); running.Cast.AppendContacts(contacts);
                foreach (AbilityCapsule capsule in contacts)
                    foreach (var body in candidates)
                    {
                        var key = (capsule.Node, body.Actor.id);
                        if (running.Hits.Contains(key) || !body.Actor.isAlive() || !capsule.Intersects(body.Centre, body.Radius)) continue;
                        running.Hits.Add(key);
                        CombatDamageEffects.DealDamage(running.Source, body.Actor, running.Source.stats["damage"] * running.Power, Composition(capsule.Matter));
                    }
            }
            if (running.Cast.Finished) casts.RemoveAt(index); else running.Cast.AppendVisuals(visuals);
        }
        FormVisualScene.Publish(ProviderId, visuals.ToArray());
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
    { casts.Clear(); definitions.Clear(); compiled.Clear(); cooldowns.Clear(); AbilityCompiler.Clear(); FormVisualScene.Clear(); }

    /// <summary>单次施放的主线程对象绑定与去重记录。</summary>
    private sealed class RunningCast
    {
        /// <summary>纯数值运行状态。</summary>
        internal AbilityCast Cast;
        /// <summary>实际归属及目标。</summary>
        internal Actor Source, Target;
        /// <summary>对象池重用人物对象时仍能识别原始归属和目标。</summary>
        internal long SourceId, TargetId;
        /// <summary>最后有效目标点。</summary>
        internal Vector3 LastTarget;
        /// <summary>开始时确定的作用倍率。</summary>
        internal float Power;
        /// <summary>每个形体对每个人物只发生一次首轮接触。</summary>
        internal readonly HashSet<(int, long)> Hits = new();
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.KnightCombat;
using Cultiway.Content.WeaponControl;
using Cultiway.Core;
using Cultiway.Core.Persistence;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
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

    /// <summary>本次施放已经退出运行并解除控制后发布；世界清理不重放完成。</summary>
    public static event Action<ConstructedCastEnded> CastEnded;

    /// <summary>实际作用形状相交并进入正式结算后发布。</summary>
    public static event Action<ConstructedContact> ContactConfirmed;

    /// <summary>共用本帧显示缓冲。</summary>
    private readonly List<FormVisualInstance> visuals = new();

    /// <summary>登记两类正式保存。定义先于人物恢复，未知构造只丢弃对应学习关系。</summary>
    public static void Initialize()
    {
        SkillModifiers.RegisterConstructionWorldEffects();
        Core.SkillLibV3.Manager.ConstructionEmission = UtilityConstructionMigration.Emit;
        ConstructedRainClouds.Initialize();
        ConstructedMatterCatalog.Initialize();
        Combat.GroundWeaponService.Initialize();
        var service = new ConstructedAbilityService();
        ConstructionEditing.Access = service;
        simulation = service;
        ModClass.I.GeneralLogicSystems.Add(service);
        ActiveAbilityService.Register(service);
        FormResources.ValidatingReload += ValidateReloadedResources;
        FormResources.Reloaded += _ =>
        {
            compiled.Clear();
            RebuildConstructionIndex();
        };
        PatchMapBox.RegisterActionOnClearWorld(Clear);
        PatchActor.RegisterHideHandItemPredicate(IsWeaponBorrowed);
        foreach (AbilityDefinition body in BodyActionCatalog.All) Register(body);
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, DefinitionSave>("ability_constructions", "自创能力构造",
                7,
                _ => new DefinitionSave { Definitions = definitions.Values.ToArray() }, (_, data) =>
                {
                    foreach (AbilityDefinition definition in data.Definitions)
                        try
                        {
                            Register(definition);
                        }
                        catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException
                                                          or JsonException)
                        {
                            ModClass.I.WorldSaves.LastReport.Skip(definition?.Id ?? "未知构造", "自创能力构造", error.Message);
                        }
                }, data =>
                {
                    if (data.Definitions == null || data.Definitions.Length > 100000)
                        throw new InvalidDataException("构造集合缺失或超过世界保存预算");
                },
                migrations: new ISaveMigration[]
                {
                    new ConstructionsV1ToV2(), new ConstructionsV2ToV3(), new ConstructionsV3ToV4(),
                    new ConstructionsV4ToV5(), new ConstructionsV5ToV6(), new ConstructionsV6ToV7()
                })
            { BeforeActors = true });
        ModClass.I.WorldSaves.Actors.Register(new SaveSection<ActorExtend, KnowledgeSave>("ability_knowledge", "自创能力掌握",
            1,
            actor => actor.E.TryGetComponent(out ConstructedKnowledge known)
                ? new KnowledgeSave
                {
                    Ids = known.Ids,
                    Cooldowns = known.Ids.ToDictionary(id => id, id => Mathf.Max(0f,
                        (float)(cooldowns.TryGetValue((actor.Base.id, id), out double until)
                            ? until - World.world.getCurWorldTime()
                            : 0)))
                }
                : null,
            (actor, data) =>
            {
                var restored = new List<string>();
                foreach (string id in data.Ids.Distinct())
                {
                    if (id == null || !definitions.ContainsKey(id))
                    {
                        ModClass.I.WorldSaves.LastReport.Skip("人物 " + actor.Base.id, "自创能力掌握", "缺少构造：" + id);
                        continue;
                    }

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
        InitializeCreation();
        InitializeWorldNeeds();
    }

    /// <summary>当前请求共用同版计划，资源变化后只重建尚未运行的请求。</summary>
    /// <param name="id">正式构造编号。</param>
    private static CompiledAbility PlanFor(string id)
    {
        if (!compiled.TryGetValue(id, out CompiledAbility plan) || plan.ResourceVersion != FormResources.Version)
        {
            plan = ValidateDefinition(definitions[id]);
            compiled[id] = plan;
        }

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
        if (definitions.Count >= MaximumDefinitions && !definitions.ContainsKey(definition.Id))
            throw new InvalidDataException("世界构造目录已达保存上限");
        CompiledAbility plan = ValidateDefinition(definition);
        AbilityDefinition copy =
            JsonConvert.DeserializeObject<AbilityDefinition>(JsonConvert.SerializeObject(definition));
        definitions[copy.Id] = copy;
        compiled[copy.Id] = plan;
        IndexConstruction(copy, plan);
    }

    /// <summary>创建、编辑预览和正式登记使用同一资料及来源规则，不在界面另推支付限制。</summary>
    /// <param name="definition">当前资料。</param>
    /// <param name="cache">正式验证共用计划，创法中间资料不留长期缓存。</param>
    private static CompiledAbility ValidateDefinition(AbilityDefinition definition, bool cache = true)
    {
        CompiledAbility plan = AbilityCompiler.Compile(definition, cache);
        foreach (string matter in plan.Matters) Composition(matter);
        if (plan.Body != null) BodyActionBinding.ValidateResources(plan.Body);
        ConstructionResources.Validate(definition, plan.HasRecovery || plan.HasWorldMaterials);
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
        CompiledAbility plan = PlanFor(id);
        if ((plan.Use.Channels & ActiveAbilityChannel.World) != 0 && plan.HasRecovery &&
            WorldMaterialService.HasTemporaryNeed(actor.Base.id)) NotifyWorldNeed(actor.Base.id);
    }

    /// <summary>向现有玩家与自主出手入口公开真正已经学会的能力。</summary>
    /// <param name="caster">当前修士。</param>
    /// <param name="output">统一能力集合。</param>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!GeneralSettings.EnableSkillSystems || !caster.E.TryGetComponent(out ConstructedKnowledge known)) return;
        foreach (string id in known.Ids)
            if (definitions.ContainsKey(id))
                output.Add(new ActiveAbilityHandle(ProviderId, caster.E, id));
    }

    /// <summary>公开当前构造实际声明的战斗或世界用途。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) =>
        PlanFor(handle.EntryId).Use.Channels;

    /// <summary>保留统一玩家和自主施放，不建立构造专用界面。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle) => new(
        definitions[handle.EntryId].Name, SpriteTextureLoader.getSprite("cultiway/icons/skills/knight/repulse"),
        PlanFor(handle.EntryId).Use.Channels, PlanFor(handle.EntryId).Use.Target, ActiveAbilityActivationMode.Instant,
        PlanFor(handle.EntryId).Body?.ControlsStance != true
            ? ActiveAbilityCastMobility.Mobile
            : ActiveAbilityCastMobility.StationaryDuringRecovery,
        targetRelation: PlanFor(handle.EntryId).Impact.Targets,
        canCastConcurrently: PlanFor(handle.EntryId).Body?.ControlsStance != true);

    /// <summary>按本能力独立冷却及实际真元报告可用状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!Owns(caster, handle)) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (PlanFor(handle.EntryId).Body?.ControlsStance == true && caster.Base.attack_timer > 0f)
            return new(ActiveAbilityControlBlockReason.Cooldown, caster.Base.attack_timer);
        if (!BodyReady(caster, PlanFor(handle.EntryId))) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (cooldowns.TryGetValue((caster.Base.id, handle.EntryId), out double until) && until > LogicTime)
            return new(ActiveAbilityControlBlockReason.Cooldown, (float)(until - LogicTime));
        return CanPay(caster, definitions[handle.EntryId])
            ? ActiveAbilityControlState.Ready
            : new(ActiveAbilityControlBlockReason.InsufficientResource);
    }

    /// <summary>检查真实知识及来源状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        GeneralSettings.EnableSkillSystems && caster.Base.isAlive() && caster.Base.hasHealth() &&
        ResolveControlState(caster, handle).CanUse &&
        (PlanFor(handle.EntryId).Body?.ControlsStance != true || caster.Base.isAttackPossible());

    /// <summary>选取和实际接触共用明确的目标关系，仍需实际活体及选择距离。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">本次选择。</param>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target) =>
        CanPrepare(caster, handle, target.Object) &&
        (target.WorldScope == null || target.WorldScope.IsValid()) &&
        TryResolveUseTarget(caster, PlanFor(handle.EntryId), target, out Actor actor, out Vector3 point, out _) &&
        StartingDistance(caster.Base, point) <= Range(caster, handle.EntryId, actor);

    /// <summary>作为独立法术参与已有自主释放选择。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 12;

    /// <summary>向既有决策报告威力与实际消耗。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(ActorExtend caster, ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        CompiledAbility plan = PlanFor(handle.EntryId);
        bool hostile = plan.Impact.Targets == SkillUseTargetRelation.Hostile;
        return new(hostile && plan.ContactDamageScale > 0f ? 1f : 0f, plan.HasBarrier ? 1f : 0f, hostile ? 0f : 1f,
            plan.ContactControl > 0f ? 1f : 0f,
            Mathf.Max(
                caster.Base.stats["damage"] * definitions[handle.EntryId].Power *
                Mathf.Max(plan.ContactDamageScale, plan.HasBarrier ? 1f : 0f), plan.ContactControl),
            definitions[handle.EntryId].Cost, 1f);
    }

    /// <summary>首轮生长构造的目标放置距离，不代替作用形状。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        Range(caster, handle.EntryId, target);

    /// <summary>本构造没有固定圆形伤害区，使用实际部位接触。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) => 0f;

    /// <summary>受理后一次支付，独立运行；失败不创建形体或启动冷却。</summary>
    /// <param name="caster">施法者。</param>
    /// <param name="handle">已经学会的构造。</param>
    /// <param name="target">实际敌方。</param>
    /// <param name="origin">玩家、脚本或自主选择。</param>
    public bool TryUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        return Start(caster, handle.EntryId, target, origin);
    }

    /// <summary>原有身体能力的入口沿用统一执行，不要求每个人预先学会一份临时容器。</summary>
    /// <param name="caster">真实身体。</param>
    /// <param name="id">已经登记的构造。</param>
    /// <param name="target">实际目标。</param>
    /// <param name="origin">实际调用方式。</param>
    internal static bool Start(ActorExtend caster, string id, in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        return Start(caster, definitions[id], PlanFor(id), target, origin);
    }

    /// <summary>已经由实际来源收费的功能步骤直接进入共同执行，不授予另一份知识或再次支付。</summary>
    /// <param name="caster">实际归属人物。</param><param name="definition">本步骤的完整纯资料。</param>
    /// <param name="target">已经确定的目标及任务边界。</param><param name="sourcePosition">真实出生位置。</param>
    internal static bool StartPrepaid(ActorExtend caster, AbilityDefinition definition,
        in ActiveAbilityTarget target, Vector3 sourcePosition, SkillCastPayment payment = null) =>
        Start(caster, definition, ValidateDefinition(definition), target, target.RuntimeData.Origin, sourcePosition,
            true, payment);

    /// <summary>普通已学构造和已经收费的来源共用创建、接触、结束及清理。</summary>
    /// <param name="caster">实际归属人物。</param><param name="definition">当前完整资料。</param>
    /// <param name="plan">本次已编译计划。</param><param name="target">实际目标及边界。</param>
    /// <param name="origin">实际调用方式。</param><param name="sourcePosition">来源明确提供的出生点，空值读取实际身体。</param>
    private static bool Start(ActorExtend caster, AbilityDefinition definition, CompiledAbility plan,
        in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin, Vector3? sourcePosition = null,
        bool prepaid = false, SkillCastPayment sourcePayment = null)
    {
        string id = definition.Id;
        if (!TryResolveUseTarget(caster, plan, target, out Actor targetActor, out Vector3 point,
                out float targetHeight) ||
            StartingDistance(caster.Base, point) > Range(caster, plan, targetActor)) return false;
        if (plan.Body?.Construction.HasControlRelations == true && (targetActor == null || targetActor == caster.Base))
            return false;
        if (!BodyReady(caster, plan) ||
            target.WorldScope != null && !target.WorldScope.IsValid() ||
            !WorldMaterialService.CanReserve(plan)) return false;
        Vector2 from = caster.Base.current_position, to = point;
        if (simulation.pending == null)
            simulation.CaptureTerrain(Rect.MinMaxRect(Mathf.Min(from.x, to.x), Mathf.Min(from.y, to.y),
                Mathf.Max(from.x, to.x), Mathf.Max(from.y, to.y)), plan.Body?.HasSupportMotion == true);
        if (targetActor == null) point.z += simulation.terrain?.Sample(point.x, point.y) ?? 0f;
        else StartingPose(targetActor, out point, out targetHeight);
        double startedAt = LogicTime;
        StartingPose(caster.Base, out Vector3 source, out float sourceHeight);
        if (sourcePosition.HasValue)
        {
            source = sourcePosition.Value;
            source.z += simulation.terrain?.Sample(source.x, source.y) ?? 0f;
        }

        Vector3 direction = point - source;
        direction.z = 0f;
        direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.right;
        BodyActionBinding body = null;
        BodyLocomotion movement = null;
        BodyFrame? frame = null;
        if (plan.Body != null)
        {
            body = BodyActionBinding.TryCreate(caster.Base, plan.Body, direction, point, targetHeight);
            if (body == null) return false;
            frame = body.Frame(source, sourceHeight);
            if (plan.Body.ControlsStance)
            {
                movement = new BodyLocomotion(caster.Base, plan.Body, frame.Value, simulation.terrain);
                if (!movement.CanStart(simulation.terrain)) return false;
                BodyLocomotion.Pose pose = movement.Sample(0f, sourceHeight, simulation.terrain);
                frame = body.Frame(pose.Root, sourceHeight, pose.Support);
            }
        }

        var inputs = new AbilityInputs(source, point, direction, sourceHeight);
        GetHitEvent damage = CombatDamageEffects.PrepareDamage(caster.Base,
            caster.Base.stats["damage"] * definition.Power, ElementComposition.Static.empty);
        long sourceId = caster.Base.id, generation = worldGeneration;
        BodyControlLease bodyLease =
            plan.Body == null
                ? null
                : BodyControlState.TryAcquire(sourceId, plan.Body.Occupancy, body.Facing,
                    plan.Body.ControlsStance);
        if (plan.Body != null && bodyLease == null) return false;
        WorldMaterialService.Reserve(plan.ResidueCount, plan.ResidueCells);
        bool accepted = false;
        try
        {
            using var ownPayment = prepaid ? null : ConstructionResources.TryBegin(caster, definition);
            SkillCastPayment payment = sourcePayment ?? ownPayment;
            if (!prepaid && payment == null || caster.Base.isRekt() || caster.Base.id != sourceId ||
                generation != worldGeneration) return false;
            if (plan.Body?.Construction.HasControlRelations == true)
            {
                if (!PrepareBodySurface(targetActor)) return false;
                BodySurfaceBinding surface = bodySurfaces[targetActor.id];
                surface.Capture(point, targetHeight, startedAt);
                CompiledBodyRelation aim = default;
                float first = float.PositiveInfinity;
                foreach (var relation in plan.Body.Construction.Relations)
                    if (relation.Kind is BodyRelationKind.Brace or BodyRelationKind.Grab or BodyRelationKind.Deflect or BodyRelationKind.Disarm)
                    {
                        if (!BodySurfaceAt(targetActor.id, startedAt).Supports(relation.To.Part))
                        {
                            if (!BodySurfaceInUse(targetActor.id)) bodySurfaces.Remove(targetActor.id);
                            return false;
                        }

                        if (relation.FromWorldTime < first)
                        {
                            first = relation.FromWorldTime;
                            aim = relation;
                        }
                    }

                if (plan.Body.MatchDistance)
                {
                    BodyFrame current = frame.Value;
                    BodyFrame reference = current;
                    if (movement != null)
                    {
                        var predicted = movement.Sample((aim.FromWorldTime + aim.UntilWorldTime) * .5f,
                            sourceHeight, simulation.terrain);
                        reference = body.Frame(predicted.Root, sourceHeight, predicted.Support);
                    }

                    body.AimAt(BodySurfaceAt(targetActor.id, startedAt), reference, aim);
                    frame = body.Frame(current.Foot, current.Height, current.Support);
                }
            }

            double principal = 0;
            MaterialFunding funding = payment == null
                ? new MaterialFunding(Array.Empty<MaterialFundingEntry>())
                : MaterialFunding.FromPayment(payment, out principal);
            var cast = new AbilityCast(plan, Matrix4x4.TRS(point, Quaternion.identity, Vector3.one), frame, inputs,
                damage.Damage, simulation.terrain, principal);
            var running = new RunningCast
            {
                Id = ++nextCastId,
                Funding = funding,
                Credits = new MaterialRecoveryBook(),
                ReservedResidues = plan.ResidueCount,
                ReservedMaterialCells = plan.ResidueCells,
                Cast = cast,
                WorldScope = target.WorldScope,
                CorrelationId = target.RuntimeData.CorrelationId,
                WorldHits = plan.HasWorldEffects ? new() : null,
                WorldTotals = plan.HasWorldEffects ? new() : null,
                Source = caster.Base,
                SourceId = caster.Base.id,
                Target = targetActor,
                TargetId = targetActor?.id ?? 0,
                LastTarget = point + Vector3.forward * targetHeight * .5f,
                Power = definition.Power,
                Body = body,
                Movement = movement,
                BodyControlled = body != null,
                ForceResponseRemaining = plan.Body?.ForceResponse?.Capacity ?? 0f,
                BodyLease = bodyLease,
                Direction = direction,
                LastTargetFoot = point,
                LastWorldTime = startedAt,
                StartedAt = startedAt
            };
            running.InitialOwner = new CastAuthority(caster.Base, damage, targetActor);
            running.Owners = new CastAuthority[plan.FormCount];
            for (int i = 0; i < running.Owners.Length; i++) running.Owners[i] = running.InitialOwner;
            running.LastSourceFoot = inputs.Source;
            running.LastSourceHeight = inputs.SourceHeight;
            if (casts.Count == 0) simulation.RememberActors(startedAt);
            casts.Add(running);
            if (movement != null) Cultiway.Content.Persistence.ActorPositionSaveService.Remember(caster);
            if (payment != null && !payment.IsCommitted) payment.Commit();
            accepted = true;
            TrackWorldRecovery(running, true);
            if (body != null && simulation.actorsSampled) NotifyActorChanged(caster.Base);
            RecordInspirationUse(caster, plan);
            if (definition.Cooldown > 0f) cooldowns[(caster.Base.id, id)] = startedAt + definition.Cooldown;
            if (plan.Body?.ControlsStance == true)
            {
                caster.Base.lookTowardsPosition(caster.Base.current_position + (Vector2)body.Direction);
                caster.Base.startAttackCooldown();
                caster.Base.attack_timer = Mathf.Max(caster.Base.attack_timer, plan.Body.Duration);
            }

            CastStarted?.Invoke(new ConstructedCastStarted(running.Id, id, caster.Base.id, origin, startedAt,
                running.CorrelationId));
            if (generation == worldGeneration) PublishActionEvents(running);
            return true;
        }
        finally
        {
            if (!accepted)
            {
                bodyLease?.Dispose();
                if (generation == worldGeneration) WorldMaterialService.Release(plan.ResidueCount, plan.ResidueCells);
            }
        }
    }

    /// <summary>身体动作的真实触达范围，仅用于选择目标；命中仍使用实际掌面或器物。</summary>
    /// <param name="caster">真实身体。</param>
    /// <param name="id">当前构造。</param>
    /// <param name="target">候选对象。</param>
    internal static float Range(ActorExtend caster, string id, BaseSimObject target)
        => Range(caster, PlanFor(id), target);

    /// <summary>普通目录与外部来源共用已编译范围，不要求外部步骤重复登记为已学能力。</summary>
    /// <param name="caster">真实人物。</param><param name="plan">本次计划。</param><param name="target">实际目标。</param>
    private static float Range(ActorExtend caster, CompiledAbility plan, BaseSimObject target)
    {
        StartingPose(caster.Base, out Vector3 sourceFoot, out float height);
        float targetHeight = 0f;
        Vector3 targetFoot = default;
        if (target?.isActor() == true) StartingPose(target.a, out targetFoot, out targetHeight);
        float? organReach = plan.Body?.Part.IsOrgan == true
            ? BodyOrganBinding.Reach(caster.Base, plan.Body.Part.OrganId)
            : null;
        float range = plan.Reach.Selection(height, targetHeight * .24f, organReach);
        if (target?.isActor() != true || plan.Body?.Construction.HasControlRelations != true ||
            Vector2.Distance(sourceFoot, targetFoot) <= range) return range;
        BodySurfacePose surface = BodySelectionSurface(target.a);
        if (surface == null) return range;
        float radius = targetHeight * .24f;
        foreach (CompiledBodyRelation relation in plan.Body.Construction.Relations)
            if (relation.Kind is BodyRelationKind.Grab or BodyRelationKind.Brace or BodyRelationKind.Deflect or BodyRelationKind.Disarm)
                radius = Mathf.Max(radius, surface.Radius(relation.To.Part));
        return plan.Reach.Selection(height, radius, organReach);
    }

    /// <summary>不同来源共用身体准入，实际部位不存在时不凭空生成人类手臂。</summary>
    /// <param name="caster">实际来源。</param>
    /// <param name="plan">当前计划。</param>
    private static bool BodyReady(ActorExtend caster, CompiledAbility plan) => plan.Body == null ||
                                                                               BodyActionBinding.CanBind(caster,
                                                                                   plan.Body) &&
                                                                               !BodyControlState.IsBusy(caster.Base.id,
                                                                                   plan.Body.Occupancy) &&
                                                                               (plan.Body.WeaponForm == null ||
                                                                                   !WeaponControlRuntimeService
                                                                                       .IsCasting(caster)) &&
                                                                               !KnightTechniqueRuntimeService.IsBusy(
                                                                                   caster) &&
                                                                               !caster.Base.stats.hasTag("frozen_ai") &&
                                                                               !caster.Base.stats.hasTag("immovable");

    /// <summary>原有徒手入口可用性，不借用另一人物或已经失效的身体。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="id">已登记的基础动作。</param>
    internal static bool CanStartBody(ActorExtend caster, string id) =>
        GeneralSettings.EnableSkillSystems && caster != null &&
        !caster.Base.isRekt() && caster.Base.isAlive() && caster.Base.hasHealth() && BodyReady(caster, PlanFor(id));

    /// <summary>是否具有动作需要的真实身体，不因动作正在运行就让能力栏消失。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="id">已经登记的动作。</param>
    internal static bool HasBodyFor(ActorExtend caster, string id) => caster != null && !caster.Base.isRekt() &&
                                                                      caster.Base.isAlive() &&
                                                                      PlanFor(id).Body != null &&
                                                                      BodyActionBinding.CanBind(caster,
                                                                          PlanFor(id).Body);

    /// <summary>按来源自己的资源事实判断，不为骑士生成真元组件。</summary>
    /// <param name="caster">实际来源。</param>
    /// <param name="definition">已登记构造。</param>
    private static bool CanPay(ActorExtend caster, AbilityDefinition definition) =>
        ConstructionResources.CanPay(caster, definition);

    /// <summary>不接受其他人物或伪造来源的句柄。</summary>
    /// <param name="caster">当前人物。</param>
    /// <param name="handle">当前能力。</param>
    private bool Owns(ActorExtend caster, ActiveAbilityHandle handle) =>
        handle.ProviderId == ProviderId && handle.Source == caster.E &&
        definitions.ContainsKey(handle.EntryId) && caster.E.TryGetComponent(out ConstructedKnowledge known) &&
        known.Ids.Contains(handle.EntryId);

    /// <summary>实际材料接到已有元素抗性；这不代表冻结或材料相撞等反应已经完成。</summary>
    /// <param name="matter">由构造明确指定的实际材料。</param>
    private static ElementComposition Composition(string matter) => matter switch
    {
        "physical" => ElementComposition.Static.empty,
        "fire" => ElementComposition.Static.Fire,
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
        ClearCreation();
        ClearConstructionIndex();
        simulation.WorldClearing?.Invoke();
        worldGeneration++;
        steam.Clear();
        quenchingShapes.Clear();
        evaporatedSources.Clear();
        simulation?.ResetSimulation(cancel: true);
        simulation.terrain = null;
        simulation.terrainNeedsRefresh = false;
        BodyOrganNativeDisplay.ClearWorldState();
        BodyOrganBinding.ClearWorldState();
        presentation = new PresentedFrame(0d, Array.Empty<FormVisualInstance>(), new());
        foreach (RunningCast running in casts) ReleaseMaterialReservation(running);
        foreach (RunningCast running in casts)
        {
            ReleaseBodyConnection(running, "WorldChanged", fade: false, time: running.LastWorldTime);
            running.BodyLease?.Dispose();

        }

        worldNeedQueue.Clear();
        worldNeeds.Clear();
        worldRecoveryCounts.Clear();
        worldNeedSelection.Clear();
        casts.Clear();
        definitions.Clear();
        compiled.Clear();
        cooldowns.Clear();
        BodyControlState.Clear();
        bodySurfaces.Clear();
        selectionSurfaces.Clear();
        selectionSurfaceFrame = -1;
        presentedSurfaces = new();
        ConstructedActorImpulse.Clear();
        AbilityCompiler.Clear();
        FormVisualScene.Clear();
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
        long generation = worldGeneration;
        PublishActionEvents(running);
        if (generation != worldGeneration) return;
        bool cancelled = running.Cast.Phase == AbilityCastPhase.Interrupted;
        CastEnded?.Invoke(new ConstructedCastEnded(running.Id, running.Cast.Plan.Id, running.SourceId,
            running.CorrelationId, cancelled ? LogicTime : running.LastWorldTime, running.Cast.Age,
            cancelled, running.WorldChanges));
    }

    /// <summary>身体收势或中断只释放本次身体控制，已经离手的形体继续运行。</summary>
    /// <param name="running">本次施放。</param>
    private static void ReleaseBody(RunningCast running)
    {
        ReleaseBodyConnection(running, "OwnerBodyEnded", fade: false);
        if (!running.BodyControlled) return;
        bool owns = running.Source.data?.id == running.SourceId && running.Source.isAlive() &&
                    running.Body.IsCurrent(running.Source) && running.Movement?.OwnsPosition != false &&
                    !running.Source.under_forces;
        if (owns && running.Movement != null && running.Source.position_height > 0f)
        {
            if (OnSurface(running.Source))
                ConstructedActorImpulse.RestoreSurface(running.Source, running.Movement.Root.z, simulation.terrain);
            else ConstructedActorImpulse.BeginDirected(running.Source, running.Movement.Velocity, 0f, simulation.terrain);
        }

        running.BodyLease.Dispose();
        running.BodyLease = null;
        if (owns && running.Cast.Plan.Body.ControlsStance)
        {
            if (!running.Source.under_forces)
            {
                Vector3 forward = running.Cast.BodyPose.Value.Forward;
                running.Source.lookTowardsPosition(running.Source.current_position + (Vector2)forward);
                BodyActionBinding.FacingReleased?.Invoke(running.Source, forward);
            }

            running.Source.target_angle.z = 0f;
            // 三维模式不绘制原版精灵，不能等待其绘制回调才同步收势后的当前倾角。
            running.Source.updateRotation();
        }

        running.BodyControlled = false;
        if (!BodySurfaceInUse(running.SourceId)) bodySurfaces.Remove(running.SourceId);
        if (!BodySurfaceInUse(running.TargetId)) bodySurfaces.Remove(running.TargetId);
        if (running.Cast.Plan.Body.HasSupportMotion && running.Source.data?.id == running.SourceId)
            NotifyActorChanged(running.Source);
    }

    /// <summary>单次施放的主线程对象绑定与去重记录。</summary>
    private sealed class RunningCast
    {
        /// <summary>本次已发布的实际烧损累计，纯数值恢复不发布旧结果。</summary>
        internal double[] ReportedBurned;

        /// <summary>一次施放的稳定运行编号。</summary>
        internal long Id;

        /// <summary>由任务给出的关联编号，不作为存档或全局施放编号。</summary>
        internal long CorrelationId;

        /// <summary>本次世界行动边界，只允许主线程读取。</summary>
        internal ConstructionWorldScope WorldScope;

        /// <summary>已经实际改变世界的提交次数，不是不同地块数或预估收益。</summary>
        internal int WorldChanges;

        /// <summary>已经受理但尚未交给世界的记录与水格额度。</summary>
        internal int ReservedResidues, ReservedMaterialCells;

        /// <summary>开始时确定的原支付资源，不随反射或目录更新改变。</summary>
        internal MaterialFunding Funding;

        /// <summary>各真实资源已经确认的回收及精度余额。</summary>
        internal MaterialRecoveryBook Credits;

        /// <summary>主线程已经确认过的累计份额和材料转化量。</summary>
        internal double ConfirmedRecovery, ConfirmedVolume;

        /// <summary>纯数值运行状态。</summary>
        internal AbilityCast Cast;

        /// <summary>实际归属及目标。</summary>
        internal Actor Source, Target;

        /// <summary>本次已计入世界回收的占用，结束时只移除一次。</summary>
        internal bool WorldRecoveryTracked;

        /// <summary>对象池重用人物对象时仍能识别原始归属和目标。</summary>
        internal long SourceId, TargetId;

        /// <summary>借用实际身体和器物的绑定，非身体能力为空。</summary>
        internal BodyActionBinding Body;

        /// <summary>全身动作共用实际足底锚点、支撑与中断速度；局部动作为空。</summary>
        internal BodyLocomotion Movement;

        /// <summary>本次是否仍实际占用身体，收势后独立形体不再占用。</summary>
        internal bool BodyControlled;

        /// <summary>已确认的双方连接或解除中的手部归还。</summary>
        internal BodyConnection Connection;

        /// <summary>另一件真实兵器对本动作的约束；每步直接读取，不遍历全部施放。</summary>
        internal BodyConnection WeaponConstraint;

        /// <summary>每条关系只在真实接触处尝试一次，不重复夺取控制。</summary>
        internal HashSet<int> ConnectionAttempts;

        /// <summary>本次身体动作尚未用掉的承力额度，只在真实接触确认时扣除。</summary>
        internal float ForceResponseRemaining;

        /// <summary>本次取得的真实身体控制，结束只释放此凭据，不能释放后来的动作。</summary>
        internal BodyControlLease BodyLease;

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

        /// <summary>本次实际受理的世界时间，动作事实采用同一原点。</summary>
        internal double StartedAt;

        /// <summary>同组、当前归属及目标的实际接触次数和时间，身体与离手交接共用。</summary>
        internal readonly Dictionary<(int Group, long Owner, int Reflection, long Target), ContactMemory> Hits = new();

        /// <summary>地块接触与人物分别维护，操作和部位之间不串次数。</summary>
        internal Dictionary<(int Node, int Effect, int Tile, long Owner, int Reflection), ContactMemory>
            WorldHits;

        /// <summary>各世界操作已接受的不同地块及总次数。</summary>
        internal Dictionary<(int Node, int Effect, long Owner, int Reflection), WorldContactTotals>
            WorldTotals;

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
        internal (int, long, int, long) HitKey(int node, long target) =>
            (Cast.ContactGroup(node), Owner(node).Id, Cast.Reflections(node), target);

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

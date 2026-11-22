using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.KnightCombat;
using Cultiway.Content.Libraries;
using Cultiway.Content.WeaponControl;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Persistence;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Visuals;
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
public sealed partial class ConstructedAbilityService : BaseSystem, IActiveAbilityProvider, ISkillBarrierBridge
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
        Core.SkillLibV3.Manager.ConstructionEmission = emission =>
        {
            SkillEmissionResult result = FoxfireBreathConstruction.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = SealRelayConstruction.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = UtilityConstructionMigration.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = ProjectileConstructionMigration.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = GroundSpikeConstructionMigration.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = RiftConstructionMigration.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = FieldConstructionMigration.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = BarrierConstructionMigration.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = SupportConstructionMigration.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = FormationSelfConstruction.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = FormationAreaConstruction.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = FormationCueConstruction.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = FormationTargetConstruction.Emit(emission);
            if (result == SkillEmissionResult.Unhandled) result = BeamConstructionMigration.Emit(emission);
            return result == SkillEmissionResult.Unhandled ? ChainConstructionMigration.Emit(emission) : result;
        };
        Core.SkillLibV3.Wanfa.WanfaTestCastSession.ConstructionPreview = UtilityConstructionMigration.Emit;
        ConstructedInventoryInputs.Initialize();
        ConstructedRainClouds.Initialize();
        ConstructedMatterCatalog.Initialize();
        ActorExtend.RegisterActionOnDeath(ReleaseDeceasedShadowTarget);
        Combat.GroundWeaponService.Initialize();
        var service = new ConstructedAbilityService();
        ActorExtend.RegisterActionOnFinalDamage(FinalDamageStage.Transfer, ShareSoulPactDamage);
        ConstructionEditing.Access = service;
        simulation = service;
        Core.SkillLibV3.Manager.Barriers = service;
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
        BodyRecoveryFloor.Height = (field, point, maximum, radius) => BodyLocomotion.Floor(field, point, maximum, radius);
        foreach (AbilityDefinition body in BodyActionCatalog.All) Register(body);
        ValidateDefinition(BodyActionCatalog.KnightCounterRiposte);
        ModClass.I.WorldSaves.World.Register(new SaveSection<MapBox, DefinitionSave>("ability_constructions", "自创能力构造",
                16,
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
                    new ConstructionsV4ToV5(), new ConstructionsV5ToV6(), new ConstructionsV6ToV7(), new ConstructionsV7ToV8(), new ConstructionsV8ToV9(), new ConstructionsV9ToV10(), new ConstructionsV10ToV11(), new ConstructionsV11ToV12(), new ConstructionsV12ToV13(), new ConstructionsV13ToV14(), new ConstructionsV14ToV15(), new ConstructionsV15ToV16()
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

    private static CompiledAbility palmExecution;

    /// <summary>徒手入口与新世界目录使用同一制作动作；读档恢复的已学版本保留自己的资料。</summary>
    internal static CompiledAbility PalmExecution => palmExecution?.ResourceVersion == FormResources.Version
        ? palmExecution : palmExecution = ValidateDefinition(BodyActionCatalog.Palm);

    /// <summary>资源提交前验证完整正式目录，不改写定义、学习和正在运行的施放。</summary>
    private static void ValidateReloadedResources()
    {
        foreach (AbilityDefinition definition in definitions.Values) ValidateDefinition(definition);
        ValidateDefinition(BodyActionCatalog.Palm);
        ValidateDefinition(BodyActionCatalog.KnightCounterRiposte);
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
        if (KnightTechniqueStatuses.RequiredTechnique(plan.Use.SelfStatus?.Asset) != null && plan.Body?.WeaponForm == null)
            throw new InvalidDataException("Use/SelfStatus：这项架势需要实际持握兵器的身体动作");
        ConstructedInventoryInputs.Validate(plan);
        ConstructionResources.Validate(plan);
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
        if (!GeneralSettings.EnableSkillSystems) return;
        caster.E.TryGetComponent(out ConstructedKnowledge known);
        if (known.Ids != null)
            foreach (string id in known.Ids)
                if (definitions.ContainsKey(id))
                {
                    if (PlanFor(id).Binding?.Anchor == AbilityAnchor.Carrier)
                        foreach (Entity artifact in ConstructedArtifactCarrier.Equipped(caster))
                            output.Add(new ActiveAbilityHandle(ProviderId, artifact, id));
                    else output.Add(new ActiveAbilityHandle(ProviderId, caster.E, id));
                }

        if (!ArtifactCastingLink.TryResolve(caster, out ArtifactCastingLink link) ||
            !link.Owner.E.TryGetComponent(out ConstructedKnowledge borrowed)) return;
        foreach (string id in borrowed.Ids)
            if (known.Ids?.Contains(id) != true && definitions.ContainsKey(id) && AllowsArtifactCasting(PlanFor(id)))
                output.Add(new ActiveAbilityHandle(ProviderId, link.Owner.E, id));
    }

    /// <summary>公开当前构造实际声明的战斗或世界用途。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) =>
        PlanFor(handle.EntryId).Use.Channels;

    /// <summary>保留统一玩家和自主施放，不建立构造专用界面。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        CompiledAbility plan = PlanFor(handle.EntryId);
        bool carried = plan.Binding?.Anchor == AbilityAnchor.Carrier;
        bool mobile = plan.Body?.ControlsStance != true;
        string name = definitions[handle.EntryId].Name;
        Sprite icon = SpriteTextureLoader.getSprite("cultiway/icons/skills/knight/repulse");
        if (carried)
        {
            name += " · " + (handle.Source.HasName ? handle.Source.Name.value : handle.Source.GetComponent<ItemShape>().Type.id);
            icon = handle.Source.GetComponent<ItemShape>().Type.GetIcon?.Invoke(handle.Source) ?? icon;
        }
        return new(name, icon, plan.Use.Channels, plan.Use.Target, ActiveAbilityActivationMode.Instant,
            mobile ? ActiveAbilityCastMobility.Mobile : ActiveAbilityCastMobility.StationaryDuringRecovery,
            plan.Impact.Targets, mobile, plan);
    }

    /// <summary>按本能力独立冷却及实际真元报告可用状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle) =>
        ResolveUseControl(caster, handle, out _, out _, out _, out _);

    /// <summary>共用前置条件，保留本次真正的归属、出手载体和已取得的位置。</summary>
    /// <param name="caster">主动入口收到的施法者。</param>
    /// <param name="handle">本次使用的能力。</param>
    /// <param name="context">实际归属及人物载体。</param>
    /// <param name="plan">本次能力的共同计划。</param>
    /// <param name="node">若借用分念等非人物来源，记录原节点。</param>
    /// <param name="position">原节点的本次选取位置。</param>
    private ActiveAbilityControlState ResolveUseControl(ActorExtend caster, ActiveAbilityHandle handle,
        out SkillCasterContext context, out CompiledAbility plan, out YuanshenNodeHandle node, out Vector3 position)
    {
        node = default;
        position = default;
        if (!TryResolveUseContext(caster, handle, out context, out plan)) return new(ActiveAbilityControlBlockReason.Unavailable);
        caster = context.Owner;
        if (context.EffectScale <= 0f || ActiveAbilityService.IsSilenced(in context) ||
            !caster.Base.isAlive() || !caster.Base.hasHealth() || caster.HasComponent<YuanshenBodilessTransitState>() ||
            !context.Carrier.Base.isAlive() || !context.Carrier.Base.hasHealth() ||
            plan.Body != null && (context.Kind != SkillCarrierKind.Physical || !context.HasPhysicalBody) ||
            !YuanshenNodeCastingService.TryResolveConstruction(in context, plan, out node, out position))
            return new(ActiveAbilityControlBlockReason.Unavailable);
        if (plan.Binding?.Anchor == AbilityAnchor.Carrier &&
            !ConstructedArtifactCarrier.CanBorrow(caster, handle.Source))
            return new(ActiveAbilityControlBlockReason.Unavailable);
        if (plan.Body?.ControlsStance == true && caster.Base.attack_timer > 0f)
            return new(ActiveAbilityControlBlockReason.Cooldown, caster.Base.attack_timer);
        if (!BodyReady(caster, plan) || !CanApplyWeaponStance(caster, plan) ||
            !ConstructedInventoryInputs.CanAcquire(caster, plan)) return new(ActiveAbilityControlBlockReason.Unavailable);
        if (cooldowns.TryGetValue((caster.Base.id, handle.EntryId), out double until) && until > LogicTime)
            return new(ActiveAbilityControlBlockReason.Cooldown, (float)(until - LogicTime));
        return ConstructionResources.CanPay(caster, plan)
            ? ActiveAbilityControlState.Ready
            : new(ActiveAbilityControlBlockReason.InsufficientResource);
    }

    /// <summary>检查真实知识及来源状态。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        GeneralSettings.EnableSkillSystems && ResolveUseControl(caster, handle, out SkillCasterContext context,
            out CompiledAbility plan, out _, out _).CanUse &&
        (plan.Body?.ControlsStance != true || context.Owner.Base.isAttackPossible());

    /// <summary>选取和实际接触共用明确的目标关系，仍需实际活体及选择距离。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">本次选择。</param>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target) =>
        CanUseFrom(caster, handle, target, out _, out _, out _);

    /// <summary>选择距离与正式受理共同解析同一个源节点。</summary>
    /// <param name="caster">持有法术的修士。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">本次选择。</param>
    /// <param name="node">通过检查后准备借用的节点。</param>
    /// <param name="context">实际人物归属和空间载体。</param>
    /// <param name="plan">正式受理使用的同一份计划。</param>
    private bool CanUseFrom(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target,
        out YuanshenNodeHandle node, out SkillCasterContext context, out CompiledAbility plan)
    {
        ActiveAbilityControlState control = ResolveUseControl(caster, handle, out context,
            out plan, out node, out Vector3 origin);
        if (!GeneralSettings.EnableSkillSystems || !control.CanUse ||
            plan.Body?.ControlsStance == true && !context.Owner.Base.isAttackPossible() ||
            target.WorldScope != null && !target.WorldScope.IsValid())
            return false;
        caster = context.Owner;
        Actor carrier = context.Carrier.Base;
        return TryResolveUseTarget(caster, plan, target, out Actor actor, out Vector3 point, out _, self: carrier) &&
               (plan.SoulPact == null || CanBeginSoulPact(caster.Base, actor, plan.SoulPact)) &&
               (node.OwnerActorId != 0 ? Vector2.Distance(origin, point) : StartingDistance(carrier, point)) <=
               Range(caster, plan, actor, handle.Source);
    }

    /// <summary>作为独立法术参与已有自主释放选择。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolveUseContext(caster, handle, out SkillCasterContext context, out CompiledAbility plan)) return 0;
        return IsPureAging(plan) && AgingCombatValue(context.Owner, plan, target) <= 0f ? 0 : 12;
    }

    /// <summary>向既有决策报告威力与实际消耗。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(ActorExtend caster, ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        if (!TryResolveUseContext(caster, handle, out SkillCasterContext context, out CompiledAbility plan)) return default;
        float aging = AgingCombatValue(context.Owner, plan, target);
        if (IsPureAging(plan) && aging <= 0f) return new(0f, 0f, 0f, 0f, 0f, plan.Cost, 1f);
        bool hostile = plan.Impact.Targets == SkillUseTargetRelation.Hostile;
        return new(hostile && (plan.ContactDamageScale > 0f || aging > 0f) ? 1f : 0f, plan.HasBarrier ? 1f : 0f, hostile ? 0f : 1f,
            plan.ContactControl > 0f ? 1f : 0f,
            Mathf.Max(
                context.Owner.Base.stats["damage"] * plan.Power * context.EffectScale *
                Mathf.Max(plan.ContactDamageScale, plan.HasBarrier ? 1f : 0f), Mathf.Max(plan.ContactControl, aging)),
            plan.Cost, 1f);
    }

    /// <summary>首轮生长构造的目标放置距离，不代替作用形状。</summary>
    /// <param name="caster">持有者。</param>
    /// <param name="handle">当前能力。</param>
    /// <param name="target">当前目标。</param>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        TryResolveUseContext(caster, handle, out SkillCasterContext context, out CompiledAbility plan)
            ? Range(context.Owner, plan, target, handle.Source) : 0f;

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
        if (!CanUseFrom(caster, handle, target, out YuanshenNodeHandle node, out SkillCasterContext context,
                out CompiledAbility plan)) return false;
        using var scope = SkillCasterContextService.Enter(in context);
        return Start(context.Owner, plan, target, origin, out _,
            carrierSource: handle.Source, spatialSource: context.Carrier.Base, effectScale: context.EffectScale,
            trackSource: node.OwnerActorId != 0 ? YuanshenNodeCastingService.TrackNode(node) : null);
    }

    /// <summary>原有身体能力的入口沿用统一执行，不要求每个人预先学会一份临时容器。</summary>
    /// <param name="caster">真实身体。</param>
    /// <param name="plan">本入口的制作计划。</param>
    /// <param name="target">实际目标。</param>
    /// <param name="origin">实际调用方式。</param>
    internal static bool Start(ActorExtend caster, CompiledAbility plan, in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin) => Start(caster, plan, target, origin, out _);

    /// <summary>已经由实际来源收费的功能步骤直接进入共同执行，不授予另一份知识或再次支付。</summary>
    /// <param name="caster">实际归属人物。</param><param name="definition">本步骤的完整纯资料。</param>
    /// <param name="target">已经确定的目标及任务边界。</param>
    /// <param name="sourcePosition">明确的出生位置，空值使用真实身体的脚下。</param>
    /// <param name="payment">原来源本次实际支付。</param><param name="damage">来源已经确定的绝对伤害及功率。</param>
    /// <param name="direction">来源确定的完整出手方向，空值保留普通身体的水平朝向。</param>
    /// <param name="sourceInstance">原来源的运行容器编号，仅用于本世界并存计数，不写入能力资料。</param>
    /// <param name="spatialSource">实际提供出生及随动位置的人物载体，省略时使用归属人物。</param>
    /// <param name="sourceIsWorld">已从同轮身体表面取得绝对坐标，无需再叠加地表。</param>
    /// <param name="legacyArea">已学法术原有的范围地块与建筑规则。</param>
    /// <param name="groundTravel">已学法术原有的行进掠地规则。</param>
    /// <param name="groundContact">已学法术原有的命中地块规则。</param>
    /// <param name="buildingContact">已学法术原来允许接触建筑的规则。</param>
    /// <param name="explicitReactionTarget">延续旧反应的点名规则，仅对已经确定的原人物生效。</param>
    /// <param name="formationTarget">点名修炼效果在实际接触时产生的作用。</param>
    /// <param name="selectionAlreadyChecked">已从原入口按原射程核对选择，实际作用范围仍由构造接触决定。</param>
    /// <param name="artifactArea">原法宝圆面确认接触时兑现的状态、伤害或净化。</param>
    /// <param name="commitEffect">资料和位置通过核对后，提交必须成功的来源动作。</param>
    /// <param name="utilityTravel">原功能术的连接与行进作用，缺省使用普通构造规则。</param>
    /// <param name="reportPreview">试放窗口接收本次执行的实际存续状态。</param>
    internal static bool StartPrepaid(ActorExtend caster, AbilityDefinition definition,
        in ActiveAbilityTarget target, Vector3? sourcePosition, SkillCastPayment payment = null,
        GetHitEvent? damage = null, Vector3? direction = null, int sourceInstance = 0, BaseSimObject spatialSource = null,
        bool sourceIsWorld = false, LegacyAreaImpact legacyArea = null, LegacyGroundTravel groundTravel = null,
        SkillVfxElementAsset groundContact = null, LegacyBuildingContact buildingContact = null,
        string formationFamily = null, FormationAreaContact? formationArea = null, bool explicitReactionTarget = false,
        FormationTargetContact formationTarget = default, bool selectionAlreadyChecked = false,
        ArtifactAreaContact? artifactArea = null, Func<bool> commitEffect = null, LegacyUtilityTravel utilityTravel = default,
        Action<Func<bool>> reportPreview = null)
    {
        if (spatialSource != null && (!spatialSource.isActor() || spatialSource.isRekt())) return false;
        CompiledAbility plan = ValidateDefinition(definition);
        if (plan.Body?.ContactHostileBuildings == true)
            buildingContact = LegacyBuildingContact.ForWeapon(target.Object as Building, sweep: true);
        // 原发射已核对点名建筑。共同空间输入使用它的位置，建筑身份仍由本次接触规则校验。
        ActiveAbilityTarget aim = target;
        if (buildingContact is { SelectedBuilding: > 0 } && target.Object?.isBuilding() == true &&
            !target.Object.isRekt() && target.Object.getID() == buildingContact.SelectedBuilding)
            aim = new ActiveAbilityTarget(null, target.Position, target.SelectionArea, target.ExplicitTargets,
                target.AttackKingdom, target.RuntimeData, target.WorldScope);
        bool accepted = Start(caster, plan, aim, target.RuntimeData.Origin, out long castId, sourcePosition,
            true, payment, sourceDamage: damage, sourceDirection: direction, sourceInstance: sourceInstance,
            spatialSource: spatialSource?.a, sourceIsWorld: sourceIsWorld, legacyArea: legacyArea,
            groundTravel: groundTravel, groundContact: groundContact, buildingContact: buildingContact,
            formationFamily: formationFamily, formationArea: formationArea, explicitReactionTarget: explicitReactionTarget,
            formationTarget: formationTarget, selectionAlreadyChecked: selectionAlreadyChecked,
            artifactArea: artifactArea, commitEffect: commitEffect, utilityTravel: utilityTravel,
            preview: reportPreview != null);
        if (accepted && reportPreview != null)
        {
            long id = castId;
            reportPreview(() => casts.Exists(running => running.Id == id));
        }
        return accepted;
    }

    /// <summary>固定环境位置直接进入共同执行，零编号表示未受理，不借人物提供身份或资源。</summary>
    /// <param name="definition">环境发出的普通构造。</param><param name="target">实际指定的人物或地点。</param>
    /// <param name="position">环境出生位置。</param><param name="damage">已经确定的无攻击者伤害及境界。</param>
    /// <param name="sourceInstance">原环境容器，供同来源并存计数。</param>
    /// <param name="sourceIsWorld">该出生位置是否已包含地形高度。</param>
    /// <param name="legacyArea">旧来源按落地中心结算的额外作用。</param>
    /// <param name="utilityTravel">原功能术的连接与行进作用。</param>
    internal static long StartWorld(AbilityDefinition definition, in ActiveAbilityTarget target,
        Vector3 position, GetHitEvent damage, int sourceInstance = 0, bool sourceIsWorld = false,
        LegacyAreaImpact legacyArea = null, LegacyUtilityTravel utilityTravel = default)
    {
        CompiledAbility plan = ValidateDefinition(definition);
        if (plan.Body != null || plan.Binding?.Anchor == AbilityAnchor.Carrier || damage.HasAttacker) return 0;
        return Start(null, plan, target, target.RuntimeData.Origin, out long castId,
            position, prepaid: true, sourceDamage: damage, sourceInstance: sourceInstance,
            sourceIsWorld: sourceIsWorld, legacyArea: legacyArea, utilityTravel: utilityTravel) ? castId : 0;
    }

    /// <summary>发起来源主动撤销自己保存的施放；实际退出和额度归还仍由共同推进完成。</summary>
    /// <param name="castId">本次受理返回的编号。</param>
    internal static void Cancel(long castId)
    {
        RunningCast running = casts.Find(cast => cast.Id == castId);
        if (running == null) return;
        running.Cast.Cancel();
        running.Inventory?.Dispose();
        ReleaseBody(running);
    }

    /// <summary>来源物件的活动结束时，一并退出仍在播放的共同启动。</summary>
    /// <param name="ownerId">原持有者编号。</param><param name="itemId">本世界的原物件编号。</param>
    /// <param name="abilityId">该物件中正在结束的能力。</param>
    internal static void CancelFromItem(long ownerId, int itemId, string abilityId)
    {
        RunningCast running = casts.Find(cast => cast.SourceId == ownerId && cast.SourceInstance == itemId &&
            cast.Cast.Plan.Id == abilityId && !cast.Cast.Finished);
        if (running != null) Cancel(running.Id);
    }

    /// <summary>核对某次确实在运行的动作是否仍占用原身体。</summary>
    /// <param name="actorId">实际来源编号。</param><param name="castId">已受理的动作编号。</param>
    internal static bool IsBodyControlledBy(long actorId, long castId)
    {
        if (castId == 0) return false;
        foreach (RunningCast running in casts)
            if (running.Id == castId && running.SourceId == actorId && running.BodyControlled) return true;
        return false;
    }

    /// <summary>普通已学构造和已经收费的来源共用创建、接触、结束及清理。</summary>
    /// <param name="caster">实际归属人物。</param>
    /// <param name="plan">本次已编译计划。</param><param name="target">实际目标及边界。</param>
    /// <param name="castId">成功受理的实际施放编号。</param>
    /// <param name="origin">实际调用方式。</param><param name="sourcePosition">来源明确提供的出生点，空值读取实际身体。</param>
    /// <param name="prepaid">是否沿用原来源的支付。</param><param name="sourcePayment">原来源这次发射的交易。</param>
    /// <param name="carrierSource">实际借用的持物。</param><param name="sourceDamage">原来源确定的绝对伤害与功率。</param>
    /// <param name="sourceDirection">原来源确定的完整方向，空值保留普通身体的水平朝向。</param>
    /// <param name="sourceInstance">原运行容器的并存分组，普通构造为零。</param>
    /// <param name="spatialSource">本次真实人物载体，与资源及伤害归属分开保存。</param>
    /// <param name="practice">是否由实际训练任务练习姿态而不产生战斗接触。</param>
    /// <param name="sourceIsWorld">明确出生点已经采集世界高度。</param>
    /// <param name="legacyArea">已学法术原有的范围地块与建筑规则。</param>
    /// <param name="groundTravel">已学法术原有的行进掠地规则。</param>
    /// <param name="groundContact">已学法术原有的命中地块规则。</param>
    /// <param name="buildingContact">已学法术原来允许接触建筑的规则。</param>
    /// <param name="formationFamily">实际修炼来源携带的自身法相，在首个生效时点提交。</param>
    /// <param name="formationArea">范围法相确认到人物后使用的原修炼规则。</param>
    /// <param name="explicitReactionTarget">延续旧反应的点名规则，仅对已经确定的原人物生效。</param>
    /// <param name="formationTarget">点名修炼效果在实际接触时产生的作用。</param>
    /// <param name="selectionAlreadyChecked">已从正式来源核对选择距离，仍要求实际形体接触。</param>
    /// <param name="artifactArea">原法宝圆面确认接触时兑现的状态、伤害或净化。</param>
    /// <param name="commitEffect">共同施放发布前在主线程确认来源动作。</param>
    /// <param name="effectScale">直接施放时当前载体的心神效果倍率。</param>
    /// <param name="trackSource">可随动的原空间载体，省略时读取已受理发射带来的载体。</param>
    private static bool Start(ActorExtend caster, CompiledAbility plan,
        in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin, out long castId, Vector3? sourcePosition = null,
        bool prepaid = false, SkillCastPayment sourcePayment = null, Entity carrierSource = default,
        GetHitEvent? sourceDamage = null, Vector3? sourceDirection = null, int sourceInstance = 0, Actor spatialSource = null,
        bool practice = false, bool sourceIsWorld = false, LegacyAreaImpact legacyArea = null,
        LegacyGroundTravel groundTravel = null, SkillVfxElementAsset groundContact = null,
        LegacyBuildingContact buildingContact = null, string formationFamily = null,
        FormationAreaContact? formationArea = null, bool explicitReactionTarget = false,
        FormationTargetContact formationTarget = default, bool selectionAlreadyChecked = false,
        ArtifactAreaContact? artifactArea = null, Func<bool> commitEffect = null, float effectScale = 1f,
        Func<Vector3?> trackSource = null, LegacyUtilityTravel utilityTravel = default, bool preview = false)
    {
        castId = 0;
        Actor sourceActor = caster?.Base;
        if (!practice && plan.Body?.ContactHostileBuildings == true)
            buildingContact ??= LegacyBuildingContact.ForWeapon(null, sweep: true);
        spatialSource ??= sourceActor;
        if (spatialSource != null && (!spatialSource.isAlive() || !spatialSource.hasHealth())) return false;
        long spatialSourceId = spatialSource?.id ?? 0;
        Func<bool> sourceAvailable = null;
        if (spatialSource != null && spatialSource.TryGetExtend(out ActorExtend spatialActor) &&
            ArtifactCastingLink.IsManifestation(spatialActor))
        {
            if (!ArtifactCastingLink.TryResolve(spatialActor, out ArtifactCastingLink link)) return false;
            sourceAvailable = link.IsCurrent;
        }
        if (prepaid && caster != null) trackSource ??= target.RuntimeData.TrackSource;
        if (trackSource != null)
        {
            sourcePosition = trackSource();
            if (!sourcePosition.HasValue || plan.Body != null) return false;
            sourceIsWorld = false;
        }
        string id = plan.Id;
        Actor targetActor = null;
        Vector3 point = target.Position;
        float targetHeight = 0f;
        if (!practice && !TryResolveUseTarget(caster, plan, target, out targetActor, out point, out targetHeight,
                explicitReactionTarget, self: prepaid ? null : spatialSource))
            return false;
        if (plan.SoulPact != null && (practice || !CanBeginSoulPact(sourceActor, targetActor, plan.SoulPact))) return false;
        if (plan.WorldCondition != null && (practice || sourceActor == null || !sourceActor.isAlive())) return false;
        if (plan.HasCorrespondences && plan.Nodes.Any(node => node.Correspondence != null &&
            (node.Correspondence.Target == ConstructionCorrespondenceTarget.Source ? sourceActor : targetActor) == null)) return false;
        if (caster != null && !selectionAlreadyChecked &&
            (trackSource != null ? Vector2.Distance(sourcePosition.Value, point) : StartingDistance(spatialSource, point)) >
            Range(caster, plan, targetActor, carrierSource) ||
            caster == null && Vector2.Distance(sourcePosition.Value, point) > (plan.TargetRange ?? 10000f)) return false;
        if (plan.Body?.Construction.HasControlRelations == true && (targetActor == null || targetActor == caster.Base))
            return false;
        if (caster != null && (!BodyReady(caster, plan) || !CanApplyWeaponStance(caster, plan)) ||
            target.WorldScope != null && !target.WorldScope.IsValid() ||
            !WorldMaterialService.CanReserve(plan) || !ConstructedInventoryInputs.CanAcquire(caster, plan)) return false;
        Vector2 from = sourcePosition ?? spatialSource.GetSimPos(), to = point;
        float propagationRange = plan.EventOperations.Where(operation => operation.Propagation != null)
            .Select(operation => operation.Propagation.Range * plan.Nodes[operation.Node].Scale).DefaultIfEmpty(0f).Max();
        if (simulation.pending == null)
            simulation.CaptureTerrain(Rect.MinMaxRect(Mathf.Min(from.x, to.x) - propagationRange, Mathf.Min(from.y, to.y) - propagationRange,
                Mathf.Max(from.x, to.x) + propagationRange, Mathf.Max(from.y, to.y) + propagationRange), plan.Body?.HasSupportMotion == true);
        if (targetActor == null) point.z += simulation.terrain?.Sample(point.x, point.y) ?? 0f;
        else StartingPose(targetActor, out point, out targetHeight);
        double startedAt = LogicTime;
        Vector3 source = sourcePosition.GetValueOrDefault();
        float sourceHeight = 0f;
        if (spatialSource != null && trackSource == null) StartingPose(spatialSource, out source, out sourceHeight);
        if (sourcePosition.HasValue)
        {
            source = sourcePosition.Value;
            if (!sourceIsWorld) source.z += simulation.terrain?.Sample(source.x, source.y) ?? 0f;
        }

        Vector3 direction = sourceDirection ?? point - source;
        if (!sourceDirection.HasValue) direction.z = 0f;
        direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector3.right;
        BodyActionBinding body = null;
        BodyLocomotion movement = null;
        BodyFrame? frame = null;
        if (plan.Body != null)
        {
            body = BodyActionBinding.TryCreate(caster.Base, plan.Body, direction, point, targetHeight);
            if (body == null) return false;
            body.WeaponInConstruction = plan.UsesHeldWeapon;
            frame = body.Frame(source, sourceHeight);
            if (!body.CanStart(frame.Value)) return false;
            if (plan.Body.ControlsStance && (!plan.Body.Part.HasPartRotation || plan.Body.HasNamedSupportMotion))
            {
                movement = new BodyLocomotion(caster.Base, plan.Body, frame.Value, simulation.terrain, body.Joint);
                if (!movement.CanStart(simulation.terrain)) return false;
                BodyLocomotion.Pose pose = movement.Sample(0f, sourceHeight, simulation.terrain);
                frame = body.Frame(pose.Root, sourceHeight, pose.Support);
            }
        }

        var inputs = new AbilityInputs(source, point, direction, sourceHeight);
        // 原版肉身武技已经在伤害入口按残留心神结算。
        float power = plan.Body == null ? plan.Power * effectScale : plan.Power;
        GetHitEvent damage = sourceDamage ?? CombatDamageEffects.PrepareDamage(caster.Base,
            caster.Base.stats["damage"] * power, ElementComposition.Static.empty);
        if (!sourceDamage.HasValue) damage.DamageOrigin = target.RuntimeData.DamageOrigin;
        long sourceId = sourceActor?.id ?? 0, generation = worldGeneration;
        BodyControlLease bodyLease =
            plan.Body == null
                ? null
                : BodyControlState.TryAcquire(sourceId, body.RequiredOccupancy, body.Facing,
                    movement != null, plan.Body.Part.ControlPath);
        if (plan.Body != null && bodyLease == null) return false;
        WorldMaterialService.Reserve(plan.ResidueCount, plan.ResidueCells);
        bool accepted = false;
        ConstructedArtifactCarrier carrier = null;
        ConstructedInventoryInputs inventory = null;
        try
        {
            if (plan.Binding?.Anchor == AbilityAnchor.Carrier)
            {
                carrier = ConstructedArtifactCarrier.TryAcquire(caster, carrierSource);
                if (carrier == null) return false;
                inputs = new AbilityInputs(source, point, direction, sourceHeight, carrier.Pose);
            }
            if (plan.Inventory.Length > 0)
            {
                inventory = ConstructedInventoryInputs.TryAcquire(caster, plan);
                if (inventory == null) return false;
            }
            using var ownPayment = prepaid ? null : ConstructionResources.TryBegin(caster, plan);
            SkillCastPayment payment = sourcePayment ?? ownPayment;
            if (!prepaid && payment == null || sourceActor != null && (sourceActor.isRekt() || sourceActor.id != sourceId) ||
                spatialSource != null && (spatialSource.isRekt() || spatialSource.id != spatialSourceId ||
                                          !spatialSource.isAlive() || !spatialSource.hasHealth()) ||
                sourceAvailable != null && !sourceAvailable() ||
                trackSource != null && !trackSource().HasValue ||
                generation != worldGeneration || inventory != null && !inventory.IsValid) return false;
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
                damage.Damage, simulation.terrain, principal, plan.HasPropagation ? PropagationGroundMap.Capture(simulation.terrain) : null);
            var running = new RunningCast
            {
                Id = ++nextCastId,
                SourceInstance = sourceInstance,
                Funding = funding,
                Credits = new MaterialRecoveryBook(),
                ReservedResidues = plan.ResidueCount,
                ReservedMaterialCells = plan.ResidueCells,
                Cast = cast,
                AreaImpact = legacyArea,
                AreaImpactNode = legacyArea == null ? -1 : Array.FindIndex(plan.Nodes, node => node.Path == legacyArea.Path),
                GroundTravel = groundTravel,
                GroundContact = groundContact,
                BuildingContact = buildingContact,
                FormationFamily = formationFamily,
                FormationArea = formationArea,
                FormationTarget = formationTarget,
                ArtifactArea = artifactArea,
                ExplicitReactionTarget = explicitReactionTarget,
                CorrespondenceEnded = plan.HasCorrespondences ? new bool[plan.FormCount] : null,
                WorldScope = target.WorldScope,
                Practice = practice,
                CorrelationId = target.RuntimeData.CorrelationId,
                WorldHits = plan.HasWorldEffects ? new() : null,
                WorldTotals = plan.HasWorldEffects ? new() : null,
                Source = sourceActor,
                SourceId = sourceId,
                SpatialSource = spatialSource,
                SpatialSourceId = spatialSourceId,
                TrackSource = trackSource,
                UtilityTravel = utilityTravel,
                UtilityGravityGround = utilityTravel.Gravity.Strength <= 0f ? 0f : plan.Binding?.Anchor == AbilityAnchor.Source
                    ? source.z - sourcePosition.GetValueOrDefault().z : point.z - target.Position.z,
                Target = targetActor,
                TargetId = targetActor?.id ?? 0,
                LastTarget = point + Vector3.forward * targetHeight * .5f,
                Power = power,
                Body = body,
                Movement = movement,
                BodyControlled = body != null,
                ForceResponseRemaining = plan.Body?.ForceResponse?.Capacity ?? 0f,
                BodyLease = bodyLease,
                Carrier = carrier,
                Inventory = inventory,
                CarrierPose = inputs.Carrier,
                Direction = direction,
                LastTargetFoot = point,
                LastWorldTime = startedAt,
                StartedAt = startedAt,
                BodyGuardRemaining = practice ? 0f : (plan.Body?.Guard.Capacity ?? 0f) * plan.Power
            };
            running.InitialOwner = new CastAuthority(sourceActor, damage, targetActor, target.AttackKingdom,
                spatialSource, trackSource, sourceAvailable);
            running.InitialOwner.LastFoot = inputs.Source;
            running.Owners = new CastAuthority[plan.FormCount];
            for (int i = 0; i < running.Owners.Length; i++) running.Owners[i] = running.InitialOwner;
            running.LastSourceFoot = inputs.Source;
            running.LastSourceHeight = inputs.SourceHeight;
            if (plan.SoulPact != null && !CanBeginSoulPact(sourceActor, targetActor, plan.SoulPact)) return false;
            if (commitEffect != null && (!commitEffect() || generation != worldGeneration)) return false;
            if (casts.Count == 0) simulation.RememberActors(startedAt);
            if (inventory != null) inventory.CastId = running.Id;
            casts.Add(running);
            if (plan.SoulPact != null) RegisterSoulPact(running, plan.SoulPact);
            if (plan.WorldCondition != null) RegisterWorldCondition(running, point);
            if (movement != null) Cultiway.Content.Persistence.ActorPositionSaveService.Remember(caster);
            if (payment != null && !payment.IsCommitted) payment.Commit();
            accepted = true;
            ReplaceExcessInstances(running);
            PublishBarriers();
            TrackWorldRecovery(running, true);
            if (body != null && simulation.actorsSampled) NotifyActorChanged(caster.Base);
            if (caster != null && !practice && !preview) RecordInspirationUse(caster, plan);
            if (caster != null && plan.Cooldown > 0f) cooldowns[(sourceId, id)] = startedAt + plan.Cooldown;
            if (plan.Body?.ControlsStance == true)
            {
                caster.Base.lookTowardsPosition(caster.Base.current_position + (Vector2)body.Direction);
                caster.Base.startAttackCooldown();
                caster.Base.attack_timer = Mathf.Max(caster.Base.attack_timer, plan.Body.Duration);
            }

            castId = running.Id;
            if (!practice && plan.Use.SelfStatus is { } stance)
            {
                Entity status = ApplyStatus(targetActor, running.InitialOwner, stance, 1f, power);
                KnightTechniqueAsset technique = KnightTechniqueStatuses.RequiredTechnique(stance.Asset);
                if (!status.IsNull && technique != null)
                    KnightTechniqueStatuses.BindStance(status, technique, body.Weapon, running.Id);
            }
            CastStarted?.Invoke(new ConstructedCastStarted(running.Id, id, sourceId, origin, startedAt,
                running.CorrelationId));
            if (generation == worldGeneration) PublishActionEvents(running);
            if (generation == worldGeneration && !prepaid && !practice && damage.DamageOrigin == DamageOrigin.Primary &&
                ownPayment.IsCommitted && ownPayment.Entries.Any(entry => entry.Spent > 0) &&
                !sourceActor.isRekt() && sourceActor.id == sourceId && sourceActor.isAlive())
                ArtifactAbilityRuntimeBridge.ConstructedCastCommitted(caster);
            return true;
        }
        finally
        {
            if (!accepted)
            {
                bodyLease?.Dispose();
                carrier?.Dispose();
                inventory?.Dispose();
                if (generation == worldGeneration) WorldMaterialService.Release(plan.ResidueCount, plan.ResidueCells);
            }
        }
    }

    /// <summary>普通目录与外部来源共用已编译范围，不要求外部步骤重复登记为已学能力。</summary>
    /// <param name="caster">真实人物。</param><param name="plan">本次计划。</param><param name="target">实际目标。</param>
    internal static float Range(ActorExtend caster, CompiledAbility plan, BaseSimObject target, Entity carrier = default)
    {
        StartingPose(caster.Base, out Vector3 sourceFoot, out float height);
        float targetHeight = 0f;
        Vector3 targetFoot = default;
        if (target?.isActor() == true) StartingPose(target.a, out targetFoot, out targetHeight);
        float? organReach = plan.Body?.Part.IsJoint == true ? BodyJointBinding.TryCreate(caster.Base, plan.Body.Part.Id)?.Reach : plan.Body?.Part.IsOrgan == true
            ? BodyOrganBinding.Reach(caster.Base, plan.Body.Part.OrganPath)
            : null;
        float range = plan.Reach.Selection(height, targetHeight * .24f, organReach);
        if (plan.Binding?.Anchor == AbilityAnchor.Carrier && !carrier.IsNull &&
            Cultiway.Content.Artifacts.ArtifactAppearanceRenderer.TryGetSpatialPose(carrier, out var pose))
            range += Vector2.Distance(sourceFoot, pose.Position);
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
        BodyActionBinding.CanBind(caster, plan.Body) && BodyAvailable(caster, plan.Body);

    /// <summary>原有武器架势在支付前核对实际兵器和流派。</summary>
    /// <param name="caster">当前真实人物。</param><param name="plan">已编译的完整计划。</param>
    private static bool CanApplyWeaponStance(ActorExtend caster, CompiledAbility plan) =>
        KnightTechniqueStatuses.RequiredTechnique(plan.Use.SelfStatus?.Asset) is not { } technique ||
        KnightTechniqueAccessService.TryResolveCurrentWeapon(caster, technique, out _, out _);

    /// <summary>暂时不能接管身体时，与缺少身体资料分别提示。</summary>
    /// <param name="caster">实际施法者。</param><param name="body">本次身体动作。</param>
    private static bool BodyAvailable(ActorExtend caster, CompiledBodyAction body) =>
        !BodyControlState.IsBusy(caster.Base.id, BodyActionBinding.Occupancy(caster.Base, body), body.Part.ControlPath) &&
        (body.WeaponForm == null || !WeaponControlRuntimeService.IsCasting(caster) &&
         !WeaponControlFlight.IsBorrowed(caster.Base) && !HasReleasedWeapon(caster.Base.id)) &&
        !caster.Base.stats.hasTag("frozen_ai") &&
        !caster.Base.stats.hasTag("immovable");

    /// <summary>原有徒手入口可用性，不借用另一人物或已经失效的身体。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="plan">本入口的制作计划。</param>
    internal static bool CanStartBody(ActorExtend caster, CompiledAbility plan) =>
        GeneralSettings.EnableSkillSystems && caster != null &&
        !caster.Base.isRekt() && caster.Base.isAlive() && caster.Base.hasHealth() && BodyReady(caster, plan);

    /// <summary>是否具有动作需要的真实身体，不因动作正在运行就让能力栏消失。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="plan">本入口的制作计划。</param>
    internal static bool HasBodyFor(ActorExtend caster, CompiledAbility plan) => caster != null &&
        !caster.Base.isRekt() && caster.Base.isAlive() && plan.Body != null && BodyActionBinding.CanBind(caster, plan.Body);

    /// <summary>实际材料接到已有元素抗性；这不代表冻结或材料相撞等反应已经完成。</summary>
    /// <param name="matter">由构造明确指定的实际材料。</param>
    private static ElementComposition Composition(string matter) => matter switch
    {
        "physical" => ElementComposition.Static.empty,
        "spirit" => new ElementComposition(),
        "earth" => ElementComposition.Static.Earth,
        "entropy" => new ElementComposition(entropy: 1f),
        "yin" => new ElementComposition(neg: 1f),
        "yang" => new ElementComposition(pos: 1f),
        "lightning" => ElementComposition.Static.Lightning,
        "poison" => ElementComposition.Static.Poison,
        "fire" => ElementComposition.Static.Fire,
        "wind" => ElementComposition.Static.Wind,
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
        windScars.Clear();
        ClearFrostCoatings();
        yinTransfers.Clear();
        balancedWakan.Clear();
        lifeRepairs.Clear();
        terrainChanges.Clear();
        worldUtilityVisuals.Clear();
        timeWear.Clear();
        CropGrowthPacing.Clear();
        ClearShadows();
        soulPacts.Clear();
        worldFireConditions.Clear();
        regionalFireConditions.Clear();
        soulPactTransfers.Clear();
        simulation.observedSignals.Clear();
        ClearConductionVisuals();
        ClearContactDebug();
        contactVolumes.Clear();
        mediumLinks.Clear();
        quenchingShapes.Clear();
        evaporatedSources.Clear();
        simulation?.ResetSimulation(cancel: true);
        simulation.terrain = null;
        simulation.terrainNeedsRefresh = false;
        PropagationGroundMap.Clear();
        BodyOrganNativeDisplay.ClearWorldState();
        BodyOrganBinding.ClearWorldState();
        presentation = new PresentedFrame(0d, Array.Empty<FormVisualInstance>(), new());
        foreach (RunningCast running in casts) ReleaseMaterialReservation(running);
        foreach (RunningCast running in casts)
        {
            ReleaseActorTransferHolds(running);
            ReleaseSpatialViews(running);
            ReleaseLandscapeSlices(running);
            ReleaseMaterialVortices(running);
            ReleaseWeaponChambers(running);
            ReleaseMirrorImages(running, worldChanged: true);
            ReleaseSupportGrips(running, "WorldChanged");
            ReleaseShadowHold(running, "WorldChanged", running.LastWorldTime);
            ReleaseBodyConnection(running, "WorldChanged", fade: false, time: running.LastWorldTime);
            running.BodyLease?.Dispose();
            running.Carrier?.Dispose();
            running.Inventory?.Dispose();
        }

        ClearShadowHolds();
        ClearFallingMirrors();
        ConstructedInventoryInputs.Clear();
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
        castingSurfaces.Clear();
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
        ReleaseActorTransferHolds(running);
        ReleaseSupportGrips(running, "Finished");
        ReleaseShadowHold(running, "Finished", running.LastWorldTime);
        ReleaseSpatialViews(running);
        ReleaseLandscapeSlices(running);
        ReleaseMaterialVortices(running);
        ReleaseWeaponChambers(running);
        ReleaseMirrorImages(running);
        RemoveSoulPact(running);
        RemoveWorldCondition(running);
        ReleaseBody(running);
        ReleaseMaterialReservation(running);
        running.Inventory?.Dispose();
        casts.RemoveAt(index);
        long generation = worldGeneration;
        PublishActionEvents(running);
        if (generation != worldGeneration) return;
        bool cancelled = running.Cast.Phase == AbilityCastPhase.Interrupted;
        if (running.Practice && !cancelled && running.Cast.BodyResult == null &&
            running.Cast.Age >= running.Cast.Plan.Body.Duration &&
            running.Source.data?.id == running.SourceId && running.Source.isAlive())
            RecordInspirationUse(running.Source.GetExtend(), running.Cast.Plan);
        CastEnded?.Invoke(new ConstructedCastEnded(running.Id, running.Cast.Plan.Id, running.SourceId,
            running.CorrelationId, cancelled ? LogicTime : running.LastWorldTime, running.Cast.Age,
            cancelled, running.WorldChanges));
    }

    /// <summary>新施放成功受理后，按同人、同能力、同来源替换最老实例；标记不随预测回退撤销。</summary>
    /// <param name="accepted">已经成功受理的本次施放。</param>
    private static void ReplaceExcessInstances(RunningCast accepted)
    {
        int limit = accepted.Cast.Plan.ConcurrentLimit;
        if (limit == 0) return;
        int count = 0;
        for (int i = casts.Count - 1; i >= 0; i--)
        {
            RunningCast running = casts[i];
            if (running.Replaced || running.Cast.Finished || running.SourceId != accepted.SourceId ||
                running.SourceInstance != accepted.SourceInstance || running.Cast.Plan.Id != accepted.Cast.Plan.Id) continue;
            if (++count <= limit) continue;
            running.Replaced = true;
            ReleaseSupportGrips(running, "Replaced");
            ReleaseShadowHold(running, "Replaced", LogicTime);
            ReleaseActorTransferHolds(running);
            running.Inventory?.Dispose();
            running.Cast.Cancel();
            ReleaseBody(running);
        }
    }

    /// <summary>身体收势或中断只释放本次身体控制，已经离手的形体继续运行。</summary>
    /// <param name="running">本次施放。</param>
    private static void ReleaseBody(RunningCast running)
    {
        ReleaseBodyConnection(running, "OwnerBodyEnded", fade: false);
        running.Carrier?.Dispose();
        running.Carrier = null;
        if (!running.BodyControlled) return;
        bool ownsMotion = running.Source.data?.id == running.SourceId && running.Source.isAlive() &&
                          running.Body.IsCurrent(running.Source, requireWeapon: false) &&
                          running.Movement?.OwnsPosition != false && !running.Source.under_forces;
        if (ownsMotion && running.Movement != null && running.Source.position_height > 0f)
        {
            if (OnSurface(running.Source))
                ConstructedActorImpulse.RestoreSurface(running.Source, running.Movement.Root.z, simulation.terrain);
            else ConstructedActorImpulse.BeginDirected(running.Source, running.Movement.Velocity, 0f, simulation.terrain);
        }

        running.BodyLease.Dispose();
        running.BodyLease = null;
        if (ownsMotion && running.Cast.Plan.Body.ControlsStance &&
            (running.Cast.Plan.Body.WeaponForm == null || running.Body.IsWeaponCurrent(running.Source)))
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
        /// <summary>在本次出手生效后提交一次的修炼来源。</summary>
        internal string FormationFamily;

        /// <summary>本次实际承受者在受护者名下的独立份额。</summary>
        internal SoulPact Pact;

        /// <summary>随本次正式施放释放的世界条件。</summary>
        internal ActiveWorldCondition WorldCondition;

        /// <summary>范围法相的后续人物结算，只由共用确认接触驱动。</summary>
        internal FormationAreaContact? FormationArea;
        /// <summary>法宝在真正到达作用边界后产生的原有结果。</summary>
        internal ArtifactAreaContact? ArtifactArea;
        /// <summary>仅在本轮已有实际接触时安排一次合计返还。</summary>
        internal Action CompleteArtifactPulse;
        /// <summary>本次扇面专有网格，收束或换世界时释放。</summary>
        internal ArtifactSectorView ArtifactSectorView;

        /// <summary>由点名修炼反应在真实接触时提交的效果。</summary>
        internal FormationTargetContact FormationTarget;

        /// <summary>原修炼反应在真实接触后只对原事件点名的人物结算。</summary>
        internal bool ExplicitReactionTarget;

        /// <summary>入口当前持有的原人物，沿真实接触顺序确认。</summary>
        internal List<ActorTransferHold> ActorTransferHolds;

        /// <summary>围合后曾走过正常抗性结算的人物，不因同一圈持续而重复刷新状态。</summary>
        internal HashSet<(int Node, long Actor)> EnclosedActors;

        /// <summary>只在带有实际配对的折景门时建立暂用地貌。</summary>
        internal SpatialDestinationRelief[] SpatialViews;

        /// <summary>同一步骤只让原人物暂存一次，避免重复接触延长控制。</summary>
        internal HashSet<(int Step, long Actor)> StagedActors;

        /// <summary>原已学容器的临时并存分组；不进入长期资料。</summary>
        internal int SourceInstance;

        /// <summary>已被后一次成功施放替换，预测回退不能恢复其作用。</summary>
        internal bool Replaced;

        /// <summary>本次已发布的实际烧损累计，纯数值恢复不发布旧结果。</summary>
        internal double[] ReportedBurned;

        /// <summary>一次施放的稳定运行编号。</summary>
        internal long Id;

        /// <summary>由任务给出的关联编号，不作为存档或全局施放编号。</summary>
        internal long CorrelationId;

        /// <summary>本次世界行动边界，只允许主线程读取。</summary>
        internal ConstructionWorldScope WorldScope;

        /// <summary>只在原训练任务中播放已学身体动作，接触形状不参与交锋。</summary>
        internal bool Practice;

        /// <summary>已经实际改变世界的提交次数，不是不同地块数或预估收益。</summary>
        internal int WorldChanges;

        /// <summary>本次施放等待的真实世界侵蚀，只保存运行关系。</summary>
        internal List<WorldErosionWatch> ErosionWatches;

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

        /// <summary>尚未确认的旧范围地块、建筑作用，只在共同爆发部位生效时使用一次。</summary>
        internal LegacyAreaImpact AreaImpact;
        internal int AreaImpactNode;
        /// <summary>本次施放实际允许的原掠地作用，只在已经确认的行进中读取。</summary>
        internal LegacyGroundTravel GroundTravel;
        /// <summary>每次真实接触都可以生效的原地块作用，由共用接触规则控制频次。</summary>
        internal SkillVfxElementAsset GroundContact;
        /// <summary>原施放明确允许建筑接触时保存的关系和作用规则。</summary>
        internal LegacyBuildingContact BuildingContact;
        /// <summary>链雷已选中的各段建筑目标，只在真的跳到建筑时分配。</summary>
        internal Dictionary<int, long> ChainBuildings;

        /// <summary>实际归属及目标。</summary>
        internal Actor Source, Target;

        /// <summary>原人物失效后该部位的对应终止，不借用复活或对象池替身重新接续。</summary>
        internal bool[] CorrespondenceEnded;
        internal ShadowHold ShadowHold;
        /// <summary>实际出现的镜内映照，普通施放不分配。</summary>
        internal Dictionary<int, MirrorReflection> MirrorImages;
        /// <summary>仅景物构造保存实际带状取景。</summary>
        internal LandscapeSnapshot[] Landscapes;
        /// <summary>只有真实深口才保存散件牵引及临时厚面。</summary>
        internal VortexInstance[] Vortices;
        /// <summary>由实际击镜建立的有限裂势，普通施放不分配。</summary>
        internal Dictionary<int, MirrorCrack> MirrorCracks;

        /// <summary>本次已计入世界回收的占用，结束时只移除一次。</summary>
        internal bool WorldRecoveryTracked;

        /// <summary>对象池重用人物对象时仍能识别原始归属和目标。</summary>
        internal long SourceId, TargetId;

        /// <summary>实际采样位置的稳定人物载体，不代替归属人物。</summary>
        internal Actor SpatialSource;

        /// <summary>载体原身份，防止对象池替身接续随动。</summary>
        internal long SpatialSourceId;

        /// <summary>元神等独立的非人物载体，失效不回落到肉身。</summary>
        internal Func<Vector3?> TrackSource;

        /// <summary>原功能术独立跟随目标或在行进时施加引力。</summary>
        internal LegacyUtilityTravel UtilityTravel;

        internal Func<Vector3> TrackTarget => UtilityTravel.TrackTarget;

        /// <summary>已提交的引力时点数，接触拆步不会重复施力。</summary>
        internal int UtilityGravityTicks;

        /// <summary>独立引力中心从原执行的离地坐标换成共同坐标时增加的地面高度。</summary>
        internal float UtilityGravityGround;

        /// <summary>实际载体仍是原来源且能够提供位置。</summary>
        internal bool SpatialSourceLive => InitialOwner.SpatialLive;

        /// <summary>借用实际身体和器物的绑定，非身体能力为空。</summary>
        internal BodyActionBinding Body;

        /// <summary>本次借用的原法宝；解除后不删除物件或继续占用。</summary>
        internal ConstructedArtifactCarrier Carrier;

        /// <summary>本次从原库存借到的实际材料，结束只归还未用部分。</summary>
        internal ConstructedInventoryInputs Inventory;

        /// <summary>最后确认的载体数值姿态，独立离手后保留原输入而不重借法宝。</summary>
        internal Matrix4x4? CarrierPose;

        /// <summary>全身动作共用实际足底锚点、支撑与中断速度；局部动作为空。</summary>
        internal BodyLocomotion Movement;

        /// <summary>一次实际防御动作共享的剩余作用量。</summary>
        internal float BodyGuardRemaining;

        /// <summary>最近一次被架挡的近身攻击段与留下的力度比例。</summary>
        private int guardedBodyStage = -1;
        private float guardedBodyScale;

        /// <summary>后续独立攻击段不继承前一段的格挡损耗。</summary>
        /// <param name="stage">实际攻击段。</param>
        internal float BodyStrikeScale(int stage) => guardedBodyStage == stage ? guardedBodyScale : 1f;

        /// <summary>同一段连续遭遇多个实际防御面时，从已剩下的力度继续扣除。</summary>
        /// <param name="stage">实际攻击段。</param><param name="fraction">这次挡下后实际保留的比例。</param>
        internal void ReduceBodyStrike(int stage, float fraction)
        {
            guardedBodyScale = BodyStrikeScale(stage) * fraction;
            guardedBodyStage = stage;
        }

        /// <summary>预测推进前的实际防御面朝向。</summary>
        internal Vector3 BeforeGuardNormal;

        /// <summary>本次是否仍实际占用身体，收势后独立形体不再占用。</summary>
        internal bool BodyControlled;
        /// <summary>实际停顿或束缚的已受理截止时间，只允许本次收势与原状态共同存在。</summary>
        internal double BodyReactionUntil;

        /// <summary>已确认的双方连接或解除中的手部归还。</summary>
        internal BodyConnection Connection;

        /// <summary>独立部位取得的实际支撑抓持；每个分支本次只取得一次。</summary>
        internal List<SupportedGrip> SupportGrips;
        internal HashSet<int> UsedGripBranches;

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

        /// <summary>范围运动已经处理的共同整点；只有实际声明范围运动的施放才分配。</summary>
        internal int[] RadialTicks;

        /// <summary>每个槽携带的原物编号；结束时物件仍由原地面系统推进。</summary>
        internal List<long>[] ChannelItems;

        /// <summary>索引对应已编译的实际器口部位。</summary>
        internal Combat.GroundWeaponService.Chamber[] WeaponChambers;

        /// <summary>本次实际受理的世界时间，动作事实采用同一原点。</summary>
        internal double StartedAt;

        /// <summary>同组、当前归属及目标的实际接触次数和时间，身体与离手交接共用。</summary>
        internal readonly Dictionary<(int Group, long Owner, int Reflection, long Target), ContactMemory> Hits = new();

        /// <summary>地块接触与人物分别维护，部位、身体作用实例和操作之间不串次数。</summary>
        internal Dictionary<(int Node, int Stage, int Effect, int Tile, long Owner, int Reflection), ContactMemory>
            WorldHits;

        /// <summary>各世界操作已接受的不同地块及总次数。</summary>
        internal Dictionary<(int Node, int Stage, int Effect, long Owner, int Reflection), WorldContactTotals>
            WorldTotals;

        /// <summary>整次施放每组的总次数，不因反射重置。</summary>
        internal readonly Dictionary<int, int> GroupHits = new();

        /// <summary>实际已使用的介质通道，预测回退及反射不会重置。</summary>
        internal readonly HashSet<string> UsedMediumChannels = new();

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

        /// <summary>预测跨身体作用边界时读取原实例的真实去重记录。</summary>
        /// <param name="node">实际部位。</param><param name="target">实际目标。</param><param name="stage">实际作用实例。</param>
        internal (int, long, int, long) HitKeyAtStage(int node, long target, int stage) =>
            (Cast.ContactGroupAtStage(node, stage), Owner(node).Id, Cast.Reflections(node), target);

        /// <summary>本步前后的作用形状，预测及回退时复用。</summary>
        internal readonly List<AbilityCapsule> Before = new(), After = new();

        /// <summary>身体上一步的实际位移，用于运动目标的共同接触。</summary>
        internal Vector3 BeforeTorso;

        /// <summary>同一步原骨段的真实前态，精确接触不使用粗筛球结算。</summary>
        internal BodyJointPose BeforeJoint;
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

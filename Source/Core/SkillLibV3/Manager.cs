using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Cultiway.Core.Components;
using Cultiway.Core.Components.AnimOverwrite;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Motions;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Systems.Logic;
using Cultiway.Core.Systems.Render;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public class Manager
{
    /// <summary>执行体配置完成、作用范围初始化之前的通知；内容层可统一应用真实成长规则。</summary>
    public static event Action<Entity> ExecutionConfigured;

    /// <summary>已完成原收费和步骤安排后的构造执行入口；返回真表示已经接管，不再生成旧执行体。</summary>
    public static Func<SkillEmission, SkillEmissionResult> ConstructionEmission;

    /// <summary>清世界后旧请求及旧发射不能进入新世界。</summary>
    private int castGeneration;
    /// <summary>供队列及实际执行核对的当前世界代数。</summary>
    internal int CastGeneration => Volatile.Read(ref castGeneration);

    private readonly ConcurrentQueue<QueuedSkillCast> queuedSkillCasts = new();
    private readonly System.Collections.Generic.Dictionary<SkillCastReservationKey, Entity> activeSkillCasts = new();

    public WorldboxGame Game { get; private set; }

    public EntityStore World { get; }
    public SkillEntityLibrary SkillLib { get; } = new SkillEntityLibrary();
    public SkillModifierLibrary ModifierLib { get; } = new();
    public TrajectoryLibrary TrajLib { get; } = new TrajectoryLibrary();
    public SkillVfxElementLibrary VfxElementLib { get; } = new();
    public SkillMotionProfileLibrary MotionProfileLib { get; } = new();
    public SkillImpactProfileLibrary ImpactProfileLib { get; } = new();
    public SkillUseProfileLibrary UseProfileLib { get; } = new();
    public WanfaPavilionPolicyLibrary WanfaPolicyLib { get; } = new();
    public SkillCastResourceLibrary CastResourceLib { get; } = new();
    public SkillCastBudgetRuleLibrary CastBudgetRuleLib { get; } = new();
    public SystemGroup SkillLogicSystemGroup { get; } = new SystemGroup("SkillLibV3");

    internal Manager(WorldboxGame game)
    {
        Game = game;
        World = ModClass.I.W;

        ModClass.I.LogicPrepareRecycleSystemGroup.Add(new PrepareSkillAnimationDissipationSystem());
        //ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleSkillContainerSystem());
        ModClass.I.LogicPrepareRecycleSystemGroup.Add(new ReleaseSkillExecutionBodiesSystem());
        ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleNonMasteredSkillContainerSystem());
        ModClass.I.GeneralLogicSystems.Add(SkillLogicSystemGroup);
        ModClass.I.GeneralRenderSystems.Add(new RenderSkillWorldVisualSystem());
        ActiveAbilityService.Register(new LearnedSkillActiveAbilityProvider());

        SkillLogicSystemGroup.Add(new LogicQueuedSkillCastSystem());
        SkillLogicSystemGroup.Add(new LogicSkillCastSequenceSystem());
        SkillLogicSystemGroup.Add(new SkillCooldownSystem());
        SkillLogicSystemGroup.Add(new LogicSkillAnimationLifecycleSystem());
        SkillLogicSystemGroup.Add(new LogicTrajectorySystem());
        SkillLogicSystemGroup.Add(new LogicSkillPositionImpactSystem());
        SkillLogicSystemGroup.Add(new LogicSkillPeriodicEffectSystem());
        SkillLogicSystemGroup.Add(new LogicSkillExecutionBodySyncSystem());
        SkillLogicSystemGroup.Add(new LogicSkillGroundFxRecordSystem());
        SkillLogicSystemGroup.Add(new LogicSkillTravelSystem());
        SkillLogicSystemGroup.Add(new LogicSkillPersistentSystem(World));
        SkillLogicSystemGroup.Add(new LogicActorCollisionSystem());
    }

    internal void Init()
    {
        AssetManager._instance.add(SkillLib, "cultiway.skills");
        AssetManager._instance.add(ModifierLib, "cultiway.skill_modifiers");
        AssetManager._instance.add(TrajLib, "cultiway.trajectories");
        AssetManager._instance.add(VfxElementLib, "cultiway.skill_vfx_elements");
        AssetManager._instance.add(MotionProfileLib, "cultiway.skill_motion_profiles");
        AssetManager._instance.add(ImpactProfileLib, "cultiway.skill_impact_profiles");
        AssetManager._instance.add(UseProfileLib, "cultiway.skill_use_profiles");
        AssetManager._instance.add(WanfaPolicyLib, "cultiway.wanfa_pavilion_policy");
        AssetManager._instance.add(CastResourceLib, "cultiway.skill_cast_resources");
        AssetManager._instance.add(CastBudgetRuleLib, "cultiway.skill_cast_budget_rules");
    }

    /// <summary>
    /// 把可能来自并行战斗回调的标准施法请求提交到技能系统主线程阶段。
    /// 返回值只表示请求已进入队列，实际资源校验和序列创建仍由
    /// <see cref="StartSkillSequence"/> 完成。
    /// </summary>
    public bool QueueSkillSequence(
        ActorExtend caster,
        Entity skillContainer,
        SkillCastPlan plan,
        float strength,
        float? powerLevel = null,
        SkillCastFundingSource fundingSource = SkillCastFundingSource.CasterResources,
        Kingdom attackKingdom = null,
        SkillCastRuntimeData runtimeData = default,
        SkillCastSequenceOptions options = null)
    {
        int generation = CastGeneration;
        SkillCasterContext casterContext = SkillCasterContextService.Resolve(caster);
        if (!casterContext.IsValid || skillContainer.IsNull ||
            plan == null || plan.Steps.Count == 0) return false;
        queuedSkillCasts.Enqueue(new QueuedSkillCast(
            casterContext,
            skillContainer,
            plan,
            strength,
            powerLevel,
            fundingSource,
            attackKingdom,
            runtimeData,
            options, generation));
        return true;
    }

    /// <summary>在技能系统的无查询阶段消费当前全部标准施法请求。</summary>
    internal void FlushQueuedSkillSequences()
    {
        while (queuedSkillCasts.TryDequeue(out QueuedSkillCast request))
        {
            if (request.Generation != CastGeneration || !request.Context.IsValid ||
                request.Context.Owner.Base.id != request.OwnerId || request.Context.Carrier.Base.id != request.CarrierId) continue;
            StartSkillSequence(
                request.Caster,
                request.SkillContainer,
                request.Plan,
                request.Strength,
                request.PowerLevel,
                request.FundingSource,
                request.AttackKingdom,
                request.RuntimeData,
                request.Options,
                request.Context.Carrier,
                request.Context);
        }
    }

    /// <summary>切换世界时丢弃尚未进入施法序列的旧世界请求。</summary>
    internal void ClearQueuedSkillSequences()
    {
        Interlocked.Increment(ref castGeneration);
        while (queuedSkillCasts.TryDequeue(out _))
        {
        }

        activeSkillCasts.Clear();
    }

    public Entity SpawnAnim(string path, Vector3 pos, Vector3 rot, float scale = 0.1f, Color? tint = null,
        float frameInterval = 0.1f, bool loop = false, float? lifeTime = null, VisualRotation? visualRotation = null)
    {
        var frames = SkillEntityAsset.LoadOrderedFrames(path);
        return SpawnAnim(frames, pos, rot, scale, tint, frameInterval, loop, lifeTime, visualRotation);
    }

    public Entity SpawnAnim(Sprite[] frames, Vector3 pos, Vector3 rot, float scale = 0.1f, Color? tint = null,
        float frameInterval = 0.1f, bool loop = false, float? lifeTime = null, VisualRotation? visualRotation = null)
    {
        var entity = SkillEntityLibrary.RawAnim.NewEntity();
        if (frames == null || frames.Length == 0)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
            return entity;
        }

        var data = entity.Data;
        data.Get<Position>().value = pos;
        data.Get<Rotation>().value = rot;
        data.Get<Scale>().value = scale * Vector3.one;
        data.Get<AnimData>().frames = frames;
        data.Get<AnimData>().frame_idx = 0;
        data.Get<AnimData>().frame_timer = 0f;
        data.Get<AnimController>().meta.frame_interval = Mathf.Max(0.01f, frameInterval);
        data.Get<AnimController>().meta.loop = loop;
        data.Get<AliveTimer>().value = 0f;
        data.Get<AliveTimeLimit>().value = lifeTime ?? frames.Length * data.Get<AnimController>().meta.frame_interval;

        if (tint.HasValue)
        {
            entity.AddComponent(new AnimTint(tint.Value));
        }

        if (visualRotation.HasValue)
        {
            entity.AddComponent(visualRotation.Value);
        }

        return entity;
    }

    public bool StartSkillSequence(ActorExtend caster, Entity skill_container, SkillCastPlan plan, float strength,
        float? power_level = null, SkillCastFundingSource funding_source = SkillCastFundingSource.CasterResources,
        Kingdom attack_kingdom = null, SkillCastRuntimeData runtime_data = default,
        SkillCastSequenceOptions options = null, ActorExtend carrier = null,
        SkillCasterContext? frozen_context = null)
    {
        if (caster == null || plan == null || plan.Steps.Count == 0) return false;
        options = SkillCastSequenceOptions.Copy(options);
        SkillCasterContext resolvedContext = frozen_context ?? SkillCasterContextService.Resolve(caster);
        if (carrier != null)
        {
            resolvedContext = new SkillCasterContext(
                resolvedContext.Owner,
                carrier,
                resolvedContext.EffectScale,
                resolvedContext.Kind,
                resolvedContext.HasPhysicalBody);
        }

        if (!resolvedContext.IsValid) return false;
        using SkillCasterContextService.Scope contextScope =
            SkillCasterContextService.Enter(in resolvedContext);
        ActorExtend owner = resolvedContext.Owner;
        ActorExtend spatialCarrier = resolvedContext.Carrier;
        var reservationKey = new SkillCastReservationKey(owner, skill_container);
        if (!reservationKey.IsValid || IsSkillReserved(reservationKey)) return false;
        int generation = CastGeneration;
        long ownerId = owner.Base.id, carrierId = spatialCarrier.Base.id;
        var frozenPlan = new SkillCastPlan(); frozenPlan.Steps.AddRange(plan.Steps);
        float resolvedPowerLevel = power_level ?? owner.GetPowerLevel();
        runtime_data.EffectScale = runtime_data.ResolveEffectScale() * resolvedContext.EffectScale;
        var startContext = new SkillCastSequenceStartContext(
            owner,
            skill_container,
            frozenPlan,
            strength,
            resolvedPowerLevel,
            funding_source,
            runtime_data);
        if (options?.Hooks != null && !options.Hooks.CanStart(startContext)) return false;

        if (generation != CastGeneration || runtime_data.WorldScope != null && !runtime_data.WorldScope.IsValid()) return false;
        SkillCastPaymentTiming paymentTiming = options?.PaymentTiming ?? SkillCastPaymentTiming.Upfront;
        using SkillCastPayment payment = paymentTiming == SkillCastPaymentTiming.Upfront
            ? SkillCastCost.TryBegin(owner, skill_container, frozenPlan, funding_source) : null;
        if (paymentTiming == SkillCastPaymentTiming.Upfront ? payment == null : !SkillCastCost.CanPayStep(owner, skill_container, funding_source)) return false;
        if (generation != CastGeneration || !resolvedContext.IsValid || owner.Base.id != ownerId || spatialCarrier.Base.id != carrierId ||
            runtime_data.WorldScope != null && !runtime_data.WorldScope.IsValid()) return false;
        Entity sequenceEntity = default;
        bool accepted = false;
        try
        {
            sequenceEntity = World.CreateEntity(new SkillCastSequence
            {
                Generation = generation,
                OwnerId = ownerId,
                CarrierId = carrierId,
                Caster = owner,
                CasterContext = resolvedContext,
                Carrier = spatialCarrier,
                SkillContainer = skill_container,
                Steps = frozenPlan.Steps.ToArray(),
                AttackKingdom = attack_kingdom,
                FundingSource = funding_source,
                Strength = strength,
                PowerLevel = resolvedPowerLevel,
                RuntimeData = runtime_data,
                MaxEmitPerTick = Math.Max(1, options?.MaxEmitPerTick ?? 8),
                Options = options
            });
            sequenceEntity.AddRelation(new SkillMasterRelation { SkillContainer = skill_container });
            activeSkillCasts[reservationKey] = sequenceEntity;
            payment?.Commit(); accepted = true;
            options?.Hooks?.OnStarted(startContext);
            return true;
        }
        finally
        {
            if (!accepted && generation == CastGeneration)
            {
                activeSkillCasts.Remove(reservationKey);
                if (!sequenceEntity.IsNull) sequenceEntity.DeleteEntity();
            }
        }
    }

    /// <summary>
    /// 启动由世界规则驱动的无施法者技能序列。owner 只负责序列失效判断，
    /// 所有步骤生成的技能上下文均不绑定来源，也不消耗 owner 的资源。
    /// </summary>
    public Entity StartSourcelessSkillSequence(
        ActorExtend owner,
        Entity skill_container,
        SkillCastPlan plan,
        float strength,
        float power_level,
        Kingdom attack_kingdom = null,
        SkillCastRuntimeData runtime_data = default,
        SkillCastSequenceOptions options = null)
    {
        if (owner?.Base == null || owner.Base.isRekt() || skill_container.IsNull ||
            plan == null || plan.Steps.Count == 0) return default;

        if (runtime_data.WorldScope != null && !runtime_data.WorldScope.IsValid()) return default;
        options = SkillCastSequenceOptions.Copy(options);
        var startContext = new SkillCastSequenceStartContext(
            owner,
            skill_container,
            plan,
            strength,
            power_level,
            SkillCastFundingSource.Prepaid,
            runtime_data);
        if (options?.Hooks != null && !options.Hooks.CanStart(startContext)) return default;

        Entity sequenceEntity = World.CreateEntity(new SkillCastSequence
        {
            Caster = owner,
            Generation = CastGeneration,
            OwnerId = owner.Base.id,
            CarrierId = owner.Base.id,
            Sourceless = true,
            SkillContainer = skill_container,
            Steps = plan.Steps.ToArray(),
            AttackKingdom = attack_kingdom,
            FundingSource = SkillCastFundingSource.Prepaid,
            Strength = strength,
            PowerLevel = power_level,
            RuntimeData = runtime_data,
            MaxEmitPerTick = Math.Max(1, options?.MaxEmitPerTick ?? 8),
            Options = options,
        });
        sequenceEntity.AddRelation(new SkillMasterRelation
        {
            SkillContainer = skill_container
        });
        options?.Hooks?.OnStarted(startContext);
        return sequenceEntity;
    }

    /// <summary>检查技能占用，并在关联序列已意外失效时清除旧占用。</summary>
    private bool IsSkillReserved(SkillCastReservationKey key)
    {
        if (!activeSkillCasts.TryGetValue(key, out Entity sequence)) return false;
        if (sequence.IsAvailable() && sequence.HasComponent<SkillCastSequence>()) return true;
        activeSkillCasts.Remove(key);
        return false;
    }

    /// <summary>释放一条已经结束的标准技能序列所持有的共享占用。</summary>
    internal void ReleaseSkillReservation(ActorExtend owner, Entity skillContainer)
    {
        activeSkillCasts.Remove(new SkillCastReservationKey(owner, skillContainer));
    }

    /// <summary>同一原人物与同一技能容器在释放期间使用的唯一键。</summary>
    private readonly struct SkillCastReservationKey : IEquatable<SkillCastReservationKey>
    {
        private readonly long ownerActorId;
        private readonly int skillEntityId;

        internal SkillCastReservationKey(ActorExtend owner, Entity skillContainer)
        {
            ownerActorId = owner?.Base?.data?.id ?? 0L;
            skillEntityId = skillContainer.IsNull ? 0 : skillContainer.Id;
        }

        internal bool IsValid => ownerActorId > 0L && skillEntityId > 0;

        public bool Equals(SkillCastReservationKey other)
        {
            return ownerActorId == other.ownerActorId && skillEntityId == other.skillEntityId;
        }

        public override bool Equals(object obj)
        {
            return obj is SkillCastReservationKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)(ownerActorId ^ (ownerActorId >> 32)) * 397) ^ skillEntityId;
            }
        }
    }

    /// <summary>跨线程队列持有的一次不可变标准施法请求。</summary>
    private readonly struct QueuedSkillCast
    {
        public readonly SkillCasterContext Context;
        /// <summary>入队时冻结的世界与原人物身份。</summary>
        public readonly int Generation;
        /// <summary>原支付者与空间载体编号。</summary>
        public readonly long OwnerId, CarrierId;
        public ActorExtend Caster => Context.Owner;
        public readonly Entity SkillContainer;
        public readonly SkillCastPlan Plan;
        public readonly float Strength;
        public readonly float? PowerLevel;
        public readonly SkillCastFundingSource FundingSource;
        public readonly Kingdom AttackKingdom;
        public readonly SkillCastRuntimeData RuntimeData;
        public readonly SkillCastSequenceOptions Options;

        /// <summary>保存启动施法序列所需的完整参数。</summary>
        public QueuedSkillCast(
            SkillCasterContext context,
            Entity skillContainer,
            SkillCastPlan plan,
            float strength,
            float? powerLevel,
            SkillCastFundingSource fundingSource,
            Kingdom attackKingdom,
            SkillCastRuntimeData runtimeData,
            SkillCastSequenceOptions options, int generation)
        {
            Generation = generation; OwnerId = context.Owner.Base.id; CarrierId = context.Carrier.Base.id;
            Context = context;
            SkillContainer = skillContainer;
            Plan = new SkillCastPlan(); Plan.Steps.AddRange(plan.Steps);
            Strength = strength;
            PowerLevel = powerLevel;
            FundingSource = fundingSource;
            AttackKingdom = attackKingdom;
            RuntimeData = runtimeData;
            Options = SkillCastSequenceOptions.Copy(options);
        }
    }

    /// <summary>真正发射的共同入口；构造明确拒绝后不落回旧执行，受理后才提交逐发费用。</summary>
    /// <param name="request">同一次实际步骤及其未提交支付。</param>
    internal bool EmitSkill(SkillEmission request)
    {
        if (request.RuntimeData.WorldScope != null && !request.RuntimeData.WorldScope.IsValid()) return false;
        SkillEmissionResult result = ConstructionEmission?.Invoke(request) ?? SkillEmissionResult.Unhandled;
        if (result != SkillEmissionResult.Unhandled) return result == SkillEmissionResult.Accepted;
        Entity execution = request.Sourceless
            ? SpawnSourcelessSkill(request.SkillContainer, request.SourcePos, request.Target, request.TargetPos, request.Strength,
                request.PowerLevel, initial_angle_offset_degrees: request.InitialAngleOffsetDegrees, attack_kingdom: request.AttackKingdom, runtime_data: request.RuntimeData)
            : SpawnSkillAtPosition(request.SkillContainer, request.Source, request.SourcePos, request.Target, request.TargetPos,
                request.Strength, request.PowerLevel, request.InitialAngleOffsetDegrees, request.AttackKingdom, request.RuntimeData, request.SpatialSource);
        return !execution.IsNull;
    }

    public Entity SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, float strength,
        float delay = 0f, float? power_level = null, float initial_angle_offset_degrees = 0f,
        SkillCastRuntimeData runtime_data = default)
    {
        BaseSimObject spatialSource = ResolveSpatialSource(source);
        var target_pos = target == null || target.isRekt()
            ? spatialSource.GetSimPos() + Vector3.right
            : target.GetSimPos();
        return SpawnSkill(skill_container, source, spatialSource, target, target_pos, strength, delay, power_level,
            initial_angle_offset_degrees, runtime_data: runtime_data);
    }

    public Entity SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject spatial_source,
        BaseSimObject target, Vector3 target_pos, float strength, float delay = 0f, float? power_level = null,
        float initial_angle_offset_degrees = 0f, Kingdom attack_kingdom = null,
        SkillCastRuntimeData runtime_data = default)
    {
        return SpawnSkillFromPosition(skill_container, source, spatial_source, spatial_source.GetSimPos(), target,
            target_pos, strength, delay, power_level, initial_angle_offset_degrees, attack_kingdom, runtime_data);
    }

    public Entity SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, Vector3 target_pos,
        float strength, float delay = 0f, float? power_level = null, float initial_angle_offset_degrees = 0f,
        Kingdom attack_kingdom = null, SkillCastRuntimeData runtime_data = default)
    {
        BaseSimObject spatialSource = ResolveSpatialSource(source);
        return SpawnSkillFromPosition(skill_container, source, spatialSource, spatialSource.GetSimPos(), target,
            target_pos, strength, delay, power_level, initial_angle_offset_degrees, attack_kingdom, runtime_data);
    }

    /// <summary>保留施法者归属，但从指定世界坐标生成技能实体。</summary>
    /// <param name="skillContainer">技能容器。</param>
    /// <param name="source">资源、冷却、关系和击杀归属人物。</param>
    /// <param name="sourcePosition">技能实体出生坐标。</param>
    /// <param name="target">对象目标；可为空。</param>
    /// <param name="targetPosition">目标世界坐标。</param>
    /// <param name="strength">技能强度。</param>
    /// <param name="powerLevel">攻击功率层级。</param>
    /// <param name="initialAngleOffsetDegrees">初始方向偏角。</param>
    /// <param name="attackKingdom">本次攻击使用的阵营。</param>
    /// <param name="runtimeData">本次施法运行参数。</param>
    /// <returns>新建技能实体。</returns>
    public Entity SpawnSkillAtPosition(
        Entity skillContainer,
        BaseSimObject source,
        Vector3 sourcePosition,
        BaseSimObject target,
        Vector3 targetPosition,
        float strength,
        float powerLevel,
        float initialAngleOffsetDegrees = 0f,
        Kingdom attackKingdom = null,
        SkillCastRuntimeData runtimeData = default,
        BaseSimObject spatialSource = null)
    {
        return SpawnSkillFromPosition(
            skillContainer,
            source,
            spatialSource,
            sourcePosition,
            target,
            targetPosition,
            strength,
            0f,
            powerLevel,
            initialAngleOffsetDegrees,
            attackKingdom,
            runtimeData);
    }

    /// <summary>从给定世界坐标生成没有施法者的技能实体。</summary>
    public Entity SpawnSourcelessSkill(Entity skill_container, Vector3 source_pos, BaseSimObject target,
        Vector3 target_pos, float strength, float power_level, float delay = 0f,
        float initial_angle_offset_degrees = 0f, Kingdom attack_kingdom = null,
        SkillCastRuntimeData runtime_data = default)
    {
        return SpawnSkillFromPosition(skill_container, null, null, source_pos, target, target_pos, strength, delay,
            power_level, initial_angle_offset_degrees, attack_kingdom, runtime_data);
    }

    private Entity SpawnSkillFromPosition(Entity skill_container, BaseSimObject source,
        BaseSimObject spatial_source, Vector3 source_pos, BaseSimObject target, Vector3 target_pos, float strength,
        float delay, float? power_level, float initial_angle_offset_degrees, Kingdom attack_kingdom,
        SkillCastRuntimeData runtime_data)
    {
        ref var container = ref skill_container.GetComponent<SkillContainer>();

        var base_dir = target_pos - source_pos;
        if (base_dir.sqrMagnitude < 0.0001f)
        {
            base_dir = Vector3.right;
        }
        else
        {
            base_dir.Normalize();
        }

        var initial_dir = ApplyInitialAngleOffset(base_dir, initial_angle_offset_degrees);

        var animation = container.Asset.GetAnimation(container.AnimationIndex);
        var entity = container.Asset.NewEntity(container.AnimationIndex);
        int generation = CastGeneration;
        bool configured = false;
        try
        {
            entity.GetComponent<Scale>().value *= runtime_data.ResolveEffectScale();
            entity.AddRelation(new SkillMasterRelation()
            {
                SkillContainer = skill_container
            });
            var data = entity.Data;
            ref var context = ref data.Get<SkillContext>();
            context.Strength = strength;
            context.PowerLevel = power_level ?? ((source?.isActor() ?? false) && !source.isRekt()
                ? source.a.GetExtend().GetPowerLevel()
                : 0f);
            context.BindSource(source, spatial_source);
            context.TargetObj = target == null || target.isRekt() ? null : target;
            context.AttackKingdom = attack_kingdom;
            if (skill_container.TryGetComponent(out Settings.SkillSettings skillSettings))
            {
                context.Strength *= skillSettings.PowerMultiplier;
                if (!skillSettings.ContactEnabled) entity.GetComponent<ColliderConfig>().Enabled = false;
                if (skillSettings.NatureId != null)
                {
                    runtime_data.HasElementOverride = true;
                    runtime_data.ElementOverride = Settings.SkillSettingsCompiler.Element(skillSettings.NatureId);
                }
            }

            context.RuntimeData = runtime_data;
            ref var skill_entity = ref data.Get<SkillEntity>();
            skill_entity.SkillContainer = skill_container;
            skill_entity.VfxElement = skill_container.TryGetComponent(out Settings.SkillSettings visualSettings) &&
                                      visualSettings.NatureId != null
                ? container.VfxElement
                : runtime_data.HasElementOverride
                    ? VfxElementLib.Resolve(runtime_data.ElementOverride)
                    : container.VfxElement;
            skill_entity.ColorPalette = container.ColorPalette;
            context.TargetPos = target_pos;
            context.TargetDir = base_dir;
            ref var pos = ref data.Get<Position>();
            pos.value = source_pos;
            SyncRuntimeFxStartPosition(entity, source_pos);
            ref var rot = ref data.Get<Rotation>();
            rot.value = initial_dir;

            ApplyMotionProfile(entity, container.MotionProfile, source_pos, target_pos);

            if (delay > 0f)
            {
                entity.Add(new DelayActive()
                {
                    LeftTime = delay
                }, Tags.Get<TagInactive>());
            }

            if (container.OnSetup != null)
            {
                container.OnSetup(entity);
            }

            if (container.EffectPipeline?.HasPeriodicEffects == true)
            {
                float interval = container.EffectPipeline.MinimumPeriodicInterval;
                entity.AddComponent(new SkillPeriodicEffectState
                {
                    Interval = interval,
                    NextTick = interval
                });
            }

            if (skill_container.TryGetComponent(out Settings.SkillSettings areaSettings))
            {
                entity.GetComponent<EffectRadiusScale>().Value *= areaSettings.AreaMultiplier;
                entity.GetComponent<Scale>().value *= areaSettings.AreaMultiplier;
            }

            ExecutionConfigured?.Invoke(entity);
            SkillImpactRuntime.Initialize(entity);
            ref AliveTimeLimit lifetime = ref entity.GetComponent<AliveTimeLimit>();
            lifetime.value = container.Asset.ResolveRuntimeLifetime(skill_container, lifetime.value);
            if (skill_container.TryGetComponent(out Settings.SkillSettings lifetimeSettings) &&
                lifetimeSettings.Duration > 0f)
                entity.GetComponent<AliveTimeLimit>().value = lifetimeSettings.Duration;
            ApplyTrajectoryAfterimage(entity, container.MotionProfile);
            SkillAnimationLifecycle.Initialize(
                entity,
                animation,
                container.MotionProfile.FrameInterval,
                container.Asset.DefaultAnimationLoop);
            configured = true;
            return entity;
        }
        finally
        {
            if (!configured && generation == CastGeneration && !entity.IsNull) entity.DeleteEntity();
        }
    }

    private static void ApplyMotionProfile(Entity entity, SkillMotionProfileAsset profile, Vector3 sourcePos,
        Vector3 targetPos)
    {
        var distance = Vector2.Distance(new Vector2(sourcePos.x, sourcePos.y), new Vector2(targetPos.x, targetPos.y));
        SetOrAdd(entity, new MotionParams
        {
            Velocity = profile.ResolveSpeed(distance),
            TurnRate = profile.TurnRate,
            RampStart = profile.LaunchMultiplier,
            RampEnd = profile.CruiseMultiplier,
            RampDuration = profile.RampDuration,
            HasRamp = true
        });

        ref var controller = ref entity.GetComponent<AnimController>();
        controller.meta.frame_interval = profile.FrameInterval;
    }

    private static void ApplyTrajectoryAfterimage(Entity entity, SkillMotionProfileAsset profile)
    {
        if (!entity.TryGetComponent(out Trajectory trajectory)) return;

        var trajectoryAsset = trajectory.Asset;
        const SkillTrajectoryDomain movingDomains = SkillTrajectoryDomain.FlyingBody |
                                                    SkillTrajectoryDomain.FlyingWave |
                                                    SkillTrajectoryDomain.Ballistic |
                                                    SkillTrajectoryDomain.GroundTravel |
                                                    SkillTrajectoryDomain.MobileField |
                                                    SkillTrajectoryDomain.Melee;
        if ((trajectoryAsset.Domains & movingDomains) == SkillTrajectoryDomain.None)
        {
            if (entity.HasComponent<AnimAfterimage>())
            {
                entity.RemoveComponent<AnimAfterimage>();
            }

            return;
        }

        var speed = entity.GetComponent<MotionParams>().Velocity;
        var afterimage = profile.ResolveAfterimage(speed);
        if (entity.HasComponent<AnimAfterimage>())
        {
            ref var current = ref entity.GetComponent<AnimAfterimage>();
            current = afterimage;
        }
        else
        {
            entity.AddComponent(afterimage);
        }

        if (!entity.TryGetComponent(out AnimAfterimageOverride runtimeOverride)) return;
        if (entity.HasComponent<AnimAfterimage>())
        {
            entity.GetComponent<AnimAfterimage>() = runtimeOverride.Value;
        }
        else
        {
            entity.AddComponent(runtimeOverride.Value);
        }
    }

    private static void SetOrAdd<TComponent>(Entity entity, TComponent component)
        where TComponent : struct, IComponent
    {
        if (entity.HasComponent<TComponent>())
        {
            ref var current = ref entity.GetComponent<TComponent>();
            current = component;
            return;
        }

        entity.AddComponent(component);
    }

    private static void SyncRuntimeFxStartPosition(Entity entity, Vector3 sourcePos)
    {
        var planePos = new Vector2(sourcePos.x, sourcePos.y);
        if (entity.HasComponent<PrevPosition>())
        {
            ref var prevPosition = ref entity.GetComponent<PrevPosition>();
            prevPosition.Value = planePos;
        }

        if (entity.HasComponent<SkillGroundFxState>())
        {
            ref var groundFxState = ref entity.GetComponent<SkillGroundFxState>();
            groundFxState.DistanceAccumulator = 0f;
            groundFxState.LastX = sourcePos.x;
            groundFxState.LastY = sourcePos.y;
        }
    }

    /// <summary>在当前能力调用中解析真实空间载体。</summary>
    private static BaseSimObject ResolveSpatialSource(BaseSimObject source)
    {
        if (source?.isActor() == true && !source.isRekt() &&
            SkillCasterContextService.TryGetCurrent(source.a.GetExtend(), out SkillCasterContext context))
            return context.Carrier.Base;
        return source;
    }

    private static Vector3 ApplyInitialAngleOffset(Vector3 base_dir, float angle_degrees)
    {
        if (Mathf.Abs(angle_degrees) < 0.01f) return base_dir;

        var rotated = Quaternion.AngleAxis(angle_degrees, Vector3.forward) * base_dir;
        return rotated.sqrMagnitude < 0.0001f ? base_dir : rotated.normalized;
    }
}
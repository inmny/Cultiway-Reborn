using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Cultiway.Content.Systems.Logic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Motions;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Systems.Logic;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public class Manager
{
    /// <summary>按原收费和步骤安排提交给共同执行。只有实际受理才推进本次发射。</summary>
    public static Func<SkillEmission, SkillEmissionResult> ConstructionEmission;

    /// <summary>新构造提供的真实屏障边界；旧技能在伤害前统一查询。</summary>
    internal static ISkillBarrierBridge Barriers;

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

        //ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleSkillContainerSystem());
        ModClass.I.LogicPrepareRecycleSystemGroup.Add(new ReleaseSkillExecutionBodiesSystem());
        ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleNonMasteredSkillContainerSystem());
        ModClass.I.GeneralLogicSystems.Add(SkillLogicSystemGroup);
        ActiveAbilityService.Register(new LearnedSkillActiveAbilityProvider());

        SkillLogicSystemGroup.Add(new LogicQueuedSkillCastSystem());
        SkillLogicSystemGroup.Add(new LogicSkillCastSequenceSystem());
        SkillLogicSystemGroup.Add(new SkillCooldownSystem());
        SkillLogicSystemGroup.Add(new ArtifactBladeFlightSystem());
        SkillLogicSystemGroup.Add(new WeaponControlStrikeSystem());
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
                request.Context.Owner.Base.id != request.OwnerId ||
                request.Context.Carrier.Base.id != request.CarrierId) continue;
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
        var frozenPlan = new SkillCastPlan();
        frozenPlan.Steps.AddRange(plan.Steps);
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

        if (generation != CastGeneration ||
            runtime_data.WorldScope != null && !runtime_data.WorldScope.IsValid()) return false;
        SkillCastPaymentTiming paymentTiming = options?.PaymentTiming ?? SkillCastPaymentTiming.Upfront;
        using SkillCastPayment payment = paymentTiming == SkillCastPaymentTiming.Upfront
            ? SkillCastCost.TryBegin(owner, skill_container, frozenPlan, funding_source)
            : null;
        if (paymentTiming == SkillCastPaymentTiming.Upfront
                ? payment == null
                : !SkillCastCost.CanPayStep(owner, skill_container, funding_source)) return false;
        if (generation != CastGeneration || !resolvedContext.IsValid || owner.Base.id != ownerId ||
            spatialCarrier.Base.id != carrierId ||
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
            payment?.Commit();
            accepted = true;
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
            Generation = generation;
            OwnerId = context.Owner.Base.id;
            CarrierId = context.Carrier.Base.id;
            Context = context;
            SkillContainer = skillContainer;
            Plan = new SkillCastPlan();
            Plan.Steps.AddRange(plan.Steps);
            Strength = strength;
            PowerLevel = powerLevel;
            FundingSource = fundingSource;
            AttackKingdom = attackKingdom;
            RuntimeData = runtimeData;
            Options = SkillCastSequenceOptions.Copy(options);
        }
    }

    /// <summary>正式发射统一进入共同执行，受理后才提交逐发费用。</summary>
    /// <param name="request">同一次实际步骤及其未提交支付。</param>
    internal bool EmitSkill(SkillEmission request)
    {
        if (request.RuntimeData.WorldScope != null && !request.RuntimeData.WorldScope.IsValid()) return false;
        return ConstructionEmission?.Invoke(request) == SkillEmissionResult.Accepted;
    }

    /// <summary>原执行与迁移后的实际地块作用共用本次元素选择。</summary>
    internal SkillVfxElementAsset ResolveGroundEffectElement(Entity skill, in SkillCastRuntimeData runtime) =>
        runtime.HasElementOverride && (!skill.TryGetComponent(out Settings.SkillSettings settings) || settings.NatureId == null)
            ? VfxElementLib.Resolve(runtime.ElementOverride)
            : skill.GetComponent<SkillContainer>().VfxElement;

    /// <summary>在当前能力调用中解析真实空间载体。</summary>
    internal static BaseSimObject ResolveSpatialSource(BaseSimObject source)
    {
        if (source?.isActor() == true && !source.isRekt() &&
            SkillCasterContextService.TryGetCurrent(source.a.GetExtend(), out SkillCasterContext context))
            return context.Carrier.Base;
        return source;
    }

    /// <summary>按正式发射的角度偏差旋转出手方向。</summary>
    /// <param name="base_dir">已经归一的基础方向。</param>
    /// <param name="angle_degrees">实际偏角，以度计。</param>
    internal static Vector3 ApplyInitialAngleOffset(Vector3 base_dir, float angle_degrees)
    {
        if (Mathf.Abs(angle_degrees) < 0.01f) return base_dir;

        var rotated = Quaternion.AngleAxis(angle_degrees, Vector3.forward) * base_dir;
        return rotated.sqrMagnitude < 0.0001f ? base_dir : rotated.normalized;
    }
}

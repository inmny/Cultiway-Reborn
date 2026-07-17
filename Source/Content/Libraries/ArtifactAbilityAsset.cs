using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Semantics;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Libraries;

/// <summary>
/// 法器能力参数或运行状态中的一个字段规格。
/// </summary>
public struct ArtifactAbilityValueSpec
{
    public string key;
    public ArtifactAbilityValueKind kind;
    public bool required;
}

/// <summary>
/// 炼器阶段为能力评分和生成参数提供的上下文。
/// </summary>
public sealed class ArtifactAbilityComposeContext
{
    public ArtifactRecipeContext recipe;
    public ArtifactShapeAsset shape;
    public ArtifactAtomSelection[] atoms = [];
    public string composition_key;
    public ArtifactAbilityScales scales;

    public float GetTrait(string key)
    {
        return recipe.GetTrait(key);
    }

    public float GetAtomStrength(ArtifactAtomAsset atom)
    {
        for (int i = 0; i < atoms.Length; i++)
        {
            if (atoms[i].Atom == atom) return atoms[i].Strength;
        }
        return 0f;
    }
}

/// <summary>
/// 一次能力事件分发时不随事件类型变化的法器上下文。
/// </summary>
public readonly struct ArtifactAbilityExecutionContext
{
    public readonly Entity controller;
    public readonly Entity artifact;
    public readonly ArtifactControlState control_state;

    public ArtifactAbilityExecutionContext(
        Entity controller,
        Entity artifact,
        ArtifactControlState controlState)
    {
        this.controller = controller;
        this.artifact = artifact;
        control_state = controlState;
    }
}

/// <summary>
/// 某种领域事件到达法器能力时执行的同步处理器。
/// 事件应使用可变引用类型，使多个能力能够依次修改同一个上下文。
/// </summary>
public delegate void ArtifactAbilityEventHandler<TEvent>(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ref ArtifactAbilityRuntimeEntry runtime,
    TEvent evt)
    where TEvent : class;

/// <summary>
/// 判断一个能力实例当前是否可以响应指定领域事件。该判断同时用于 AI 决策和正式分发，必须无副作用。
/// </summary>
public delegate bool ArtifactAbilityEventCondition<TEvent>(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ArtifactAbilityRuntimeEntry runtime,
    TEvent evt)
    where TEvent : class;

public delegate bool ArtifactActiveAbilityPrepareCondition(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ArtifactAbilityRuntimeEntry runtime,
    BaseSimObject target);

public delegate bool ArtifactActiveAbilityCondition(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ArtifactAbilityRuntimeEntry runtime,
    in ActiveAbilityTarget target);

public delegate bool ArtifactActiveAbilityHandler(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ref ArtifactAbilityRuntimeEntry runtime,
    in ActiveAbilityTarget target,
    ActiveAbilityUseOrigin origin);

public delegate void ArtifactAbilityLifecycleHandler(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ref ArtifactAbilityRuntimeEntry runtime);

public delegate void ArtifactAbilityControlStateHandler(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ref ArtifactAbilityRuntimeEntry runtime,
    ArtifactControlState previousState);

public delegate void ArtifactAbilityTickHandler(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ref ArtifactAbilityRuntimeEntry runtime,
    float elapsedSeconds);

/// <summary>
/// 判断本轮周期回调是否有实际执行需求。该判断发生在维持资源检查和扣除之前，且不得产生副作用。
/// </summary>
public delegate bool ArtifactAbilityTickCondition(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ArtifactAbilityRuntimeEntry runtime);

public delegate void ArtifactAbilityStatsHandler(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ArtifactAbilityRuntimeEntry runtime,
    BaseStats stats);

public delegate void ArtifactAbilityEndedHandler(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    ref ArtifactAbilityRuntimeEntry runtime,
    ArtifactAbilityEndReason reason);

public delegate float ArtifactAbilityValueResolver(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability);

public delegate int ArtifactAbilityChargeResolver(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability);

/// <summary>
/// 检查或扣除一次能力资源消耗。consume 为 false 时只能查询，不得修改资源。
/// </summary>
public delegate bool ArtifactAbilityResourceHandler(
    ArtifactAbilityExecutionContext context,
    ArtifactAbilityInstance ability,
    float amount,
    bool consume);

/// <summary>
/// 能力资产共用的生命周期协议。能力只填写需要的回调和数值解析器，运行状态由统一系统维护。
/// </summary>
public sealed class ArtifactAbilityLifecycleProfile
{
    /// <summary>领域事件可被响应的最低控制状态。</summary>
    public ArtifactControlState event_minimum_state = ArtifactControlState.Cold;

    /// <summary>主动能力可被启动的最低控制状态。</summary>
    public ArtifactControlState active_minimum_state = ArtifactControlState.Ready;

    /// <summary>持续活动得以维持的最低控制状态。</summary>
    public ArtifactControlState sustain_minimum_state = ArtifactControlState.Operating;

    /// <summary>周期回调可被推进的最低控制状态。</summary>
    public ArtifactControlState tick_minimum_state = ArtifactControlState.Operating;

    /// <summary>常驻属性可生效的最低控制状态。</summary>
    public ArtifactControlState stats_minimum_state = ArtifactControlState.Operating;

    /// <summary>周期回调间隔，单位为世界秒；0 表示不启用周期回调。</summary>
    public float tick_interval;

    /// <summary>是否仅在能力存在持续活动时推进周期回调。</summary>
    public bool tick_requires_activity;

    /// <summary>领域事件成功处理后是否消耗充能、冷却和启动资源。</summary>
    public bool event_consumes_trigger;

    /// <summary>是否允许主动能力在已有持续活动时再次启动。</summary>
    public bool allow_active_reentry;

    /// <summary>控制状态低于维持要求时是否立即结束持续活动。</summary>
    public bool interrupt_activity_on_state_loss = true;

    /// <summary>过载状态下启动和维持资源消耗的倍率。</summary>
    public float overload_resource_multiplier = 1.5f;

    /// <summary>过载状态下触发冷却的倍率。</summary>
    public float overload_cooldown_multiplier = 1.15f;

    /// <summary>解析最大充能数；未配置时能力不使用充能。</summary>
    public ArtifactAbilityChargeResolver ResolveMaxCharges;

    /// <summary>解析一次成功触发后的冷却时长。</summary>
    public ArtifactAbilityValueResolver ResolveCooldown;

    /// <summary>解析恢复一点充能所需时长；未配置时沿用冷却时长。</summary>
    public ArtifactAbilityValueResolver ResolveRecharge;

    /// <summary>解析主动活动的持续时长；0 表示由活动载体自行决定结束时机。</summary>
    public ArtifactAbilityValueResolver ResolveDuration;

    /// <summary>解析能力成功启动一次所需的资源。</summary>
    public ArtifactAbilityValueResolver ResolveActivationCost;

    /// <summary>解析每次有效周期回调所需的维持资源。</summary>
    public ArtifactAbilityValueResolver ResolveMaintenanceCost;

    /// <summary>查询或扣除能力使用的通用资源。</summary>
    public ArtifactAbilityResourceHandler Resource;

    /// <summary>法器能力首次接入驾驭者时执行。</summary>
    public ArtifactAbilityLifecycleHandler OnAttached;

    /// <summary>法器能力脱离驾驭者时执行。</summary>
    public ArtifactAbilityLifecycleHandler OnDetached;

    /// <summary>法器控制状态变化后执行，并接收变化前状态。</summary>
    public ArtifactAbilityControlStateHandler OnControlStateChanged;

    /// <summary>判断本轮周期是否需要执行；返回 false 时不会扣除维持资源。</summary>
    public ArtifactAbilityTickCondition CanTick;

    /// <summary>周期到期且资源支付成功后执行。</summary>
    public ArtifactAbilityTickHandler OnTick;

    /// <summary>构建驾驭者缓存属性时贡献常驻属性。</summary>
    public ArtifactAbilityStatsHandler ContributeStats;

    /// <summary>持续活动结束并清理通用运行状态后执行。</summary>
    public ArtifactAbilityEndedHandler OnActivityEnded;
}

/// <summary>
/// 法器能力可选的主动释放入口。它只描述如何被选择和启动，持续效果由 SkillExecution 驱动。
/// </summary>
public sealed class ArtifactActiveAbilityProfile
{
    public ActiveAbilityChannel channels = ActiveAbilityChannel.Combat;
    public ActiveAbilityTargetMode target_mode = ActiveAbilityTargetMode.Object;
    public ActiveAbilityActivationMode activation_mode = ActiveAbilityActivationMode.Instant;
    public int ai_weight = 1;
    public Func<ArtifactAbilityExecutionContext, ArtifactAbilityInstance, float> ResolveRange;
    public Func<ArtifactAbilityExecutionContext, ArtifactAbilityInstance, float> ResolveEffectRadius;
    public ArtifactActiveAbilityPrepareCondition CanPrepare;
    public ArtifactActiveAbilityCondition CanUse;
    public ArtifactActiveAbilityHandler TryUse;
}

/// <summary>
/// 注册式法器能力。每个能力拥有独立参数规格、运行状态、调度元数据和事件处理器。
/// </summary>
public class ArtifactAbilityAsset : Asset
{
    public string name_key;
    public SemanticDescriptor semantics = new();
    public ArtifactAbilityExclusivityKey exclusivity;
    /// <summary>
    /// 能力在成品中稳定显化所占用的承载量。它不是固定槽位，最终数量由整件法器的材料语义决定。
    /// </summary>
    public float manifestation_cost = 1f;

    /// <summary>已经选中的能力具有这些标签时，提高本能力在剩余候选中的优先级。</summary>
    public SemanticQueryExpression[] synergy_conditions = [];

    /// <summary>本能力不会与具有这些标签的能力同时显化。</summary>
    public SemanticQueryExpression[] conflict_conditions = [];

    public float minimum_score;
    public ArtifactUseProfile use_profile;
    public float control_complexity;
    public int thread_cost;
    public ArtifactAbilityValueSpec[] parameter_schema = [];
    public ArtifactAbilityValueSpec[] state_schema = [];
    public Func<ArtifactAbilityComposeContext, float> ScoreRecipe;
    public Func<ArtifactAbilityComposeContext, ArtifactAbilityValue[]> ComposeParameters;
    public Func<ArtifactAbilityComposeContext, ArtifactAbilityValue[]> ComposeInitialState;
    public Func<ArtifactAbilityInstance, string> DescribeInstance;
    public ArtifactActiveAbilityProfile active_use;
    public ArtifactAbilityLifecycleProfile lifecycle = new();
    public ArtifactAbilityVisualProfile visual;
    public ArtifactVehicleAbilityProfile vehicle_use;
    public ArtifactSectAbilityProfile sect_use;
    public ArtifactSpiritAbilityProfile spirit_use;

    private readonly Dictionary<Type, IEventHandler> _handlers = new();

    public float ScoreFor(ArtifactAbilityComposeContext context)
    {
        return Mathf.Max(0f, ScoreRecipe?.Invoke(context) ?? 0f);
    }

    public ArtifactAbilityAsset SetSemantics(params SemanticAsset[] values)
    {
        semantics = SemanticDescriptor.Of(values);
        return this;
    }

    public ArtifactAbilityAsset AddSynergies(params SemanticAsset[] values)
    {
        synergy_conditions = values.Select(SemanticQueryExpression.Has).ToArray();
        return this;
    }

    public ArtifactAbilityAsset AddConflicts(params SemanticQueryExpression[] conditions)
    {
        conflict_conditions = conditions ?? [];
        return this;
    }

    public string GetName()
    {
        return !string.IsNullOrEmpty(name_key) && LM.Has(name_key) ? LM.Get(name_key) : id;
    }

    public string GetDescription(ArtifactAbilityInstance ability)
    {
        return DescribeInstance?.Invoke(ability) ?? string.Empty;
    }

    public ArtifactAbilityAsset Activate(ArtifactActiveAbilityProfile profile)
    {
        active_use = profile ?? throw new ArgumentNullException(nameof(profile));
        if (profile.TryUse == null) throw new ArgumentException($"法器主动能力 {id} 缺少执行入口");
        return this;
    }

    public ArtifactAbilityAsset ConfigureLifecycle(ArtifactAbilityLifecycleProfile profile)
    {
        lifecycle = profile ?? throw new ArgumentNullException(nameof(profile));
        return this;
    }

    public ArtifactAbilityAsset Visualize(ArtifactAbilityVisualProfile profile)
    {
        visual = profile ?? throw new ArgumentNullException(nameof(profile));
        return this;
    }

    public ArtifactAbilityAsset ProvideVehicle(ArtifactVehicleAbilityProfile profile)
    {
        vehicle_use = profile ?? throw new ArgumentNullException(nameof(profile));
        return this;
    }

    public ArtifactAbilityAsset SupportSect(ArtifactSectAbilityProfile profile)
    {
        sect_use = profile ?? throw new ArgumentNullException(nameof(profile));
        return this;
    }

    public ArtifactAbilityAsset AwakenSpirit(ArtifactSpiritAbilityProfile profile)
    {
        spirit_use = profile ?? throw new ArgumentNullException(nameof(profile));
        return this;
    }

    public ArtifactAbilityAsset Handle<TEvent>(ArtifactAbilityEventHandler<TEvent> handler)
        where TEvent : class
    {
        return Handle<TEvent>(null, handler);
    }

    /// <summary>
    /// 注册带可执行条件的领域事件处理器。条件不通过时，该能力不会计为已处理事件。
    /// </summary>
    public ArtifactAbilityAsset Handle<TEvent>(
        ArtifactAbilityEventCondition<TEvent> condition,
        ArtifactAbilityEventHandler<TEvent> handler)
        where TEvent : class
    {
        _handlers[typeof(TEvent)] = new EventHandler<TEvent>(condition, handler);
        return this;
    }

    internal bool Supports<TEvent>() where TEvent : class
    {
        return _handlers.ContainsKey(typeof(TEvent));
    }

    internal bool CanHandle<TEvent>(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        TEvent evt)
        where TEvent : class
    {
        return _handlers.TryGetValue(typeof(TEvent), out IEventHandler handler) &&
               ArtifactAbilityLifecycle.CanHandleEvent(this, context, ability, runtime) &&
               handler.CanInvoke(context, ability, runtime, evt);
    }

    internal bool CanPrepareActive(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        BaseSimObject target)
    {
        return ArtifactAbilityLifecycle.CanStartActive(this, context, ability, runtime) &&
               (active_use.CanPrepare?.Invoke(context, ability, runtime, target) ?? true);
    }

    internal bool CanUseActive(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target)
    {
        return ArtifactAbilityLifecycle.CanStartActive(this, context, ability, runtime) &&
               (active_use.CanUse?.Invoke(context, ability, runtime, target) ?? true);
    }

    internal bool TryUseActive(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        ArtifactAbilityLifecycle.EnsureInitialized(this, context, ability, ref runtime);
        if (!CanUseActive(context, ability, runtime, target)) return false;
        if (!active_use.TryUse(context, ability, ref runtime, target, origin)) return false;
        ArtifactAbilityLifecycle.CommitActive(this, context, ability, ref runtime);
        Vector3 targetPosition = target.Object != null && !target.Object.isRekt()
            ? target.Object.GetSimPos()
            : target.Position;
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Trigger,
            targetPosition,
            runtime.has_activity_direction ? runtime.activity_direction : default,
            target: target.Object);
        return true;
    }

    internal ArtifactAbilityInstance ComposeInstance(ArtifactAbilityComposeContext context)
    {
        ArtifactAbilityValue[] parameters = ComposeParameters?.Invoke(context) ?? [];
        ArtifactAbilityValue[] initialState = ComposeInitialState?.Invoke(context) ?? [];
        ValidateValues(parameters, parameter_schema, "参数");
        ValidateValues(initialState, state_schema, "运行状态");
        return new ArtifactAbilityInstance
        {
            instance_id = id,
            ability_id = id,
            parameters = parameters,
            initial_state = initialState,
        };
    }

    internal void ValidateInstance(ArtifactAbilityInstance ability)
    {
        ValidateValues(ability.parameters ?? [], parameter_schema, "参数");
        ValidateValues(ability.initial_state ?? [], state_schema, "运行状态");
    }

    internal bool TryHandle<TEvent>(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        TEvent evt)
        where TEvent : class
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out IEventHandler handler)) return false;
        ArtifactAbilityLifecycle.EnsureInitialized(this, context, ability, ref runtime);
        if (!ArtifactAbilityLifecycle.CanHandleEvent(this, context, ability, runtime)) return false;
        if (!handler.CanInvoke(context, ability, runtime, evt)) return false;
        handler.Invoke(context, ability, ref runtime, evt);
        ArtifactAbilityLifecycle.CommitEvent(this, context, ability, ref runtime);
        return true;
    }

    private void ValidateValues(
        ArtifactAbilityValue[] values,
        ArtifactAbilityValueSpec[] schema,
        string valueGroup)
    {
        HashSet<string> present = new(StringComparer.Ordinal);
        for (int i = 0; i < values.Length; i++)
        {
            ArtifactAbilityValue value = values[i];
            if (!present.Add(value.key))
            {
                throw new InvalidOperationException($"法器能力 {id} 的{valueGroup}重复定义 {value.key}");
            }

            int specIndex = FindSpec(schema, value.key);
            if (specIndex < 0 || schema[specIndex].kind != value.kind)
            {
                throw new InvalidOperationException($"法器能力 {id} 的{valueGroup} {value.key} 不符合规格");
            }
        }

        for (int i = 0; i < schema.Length; i++)
        {
            if (schema[i].required && !present.Contains(schema[i].key))
            {
                throw new InvalidOperationException($"法器能力 {id} 的{valueGroup}缺少 {schema[i].key}");
            }
        }
    }

    private static int FindSpec(ArtifactAbilityValueSpec[] schema, string key)
    {
        for (int i = 0; i < schema.Length; i++)
        {
            if (schema[i].key == key) return i;
        }
        return -1;
    }

    private interface IEventHandler
    {
        bool CanInvoke(
            ArtifactAbilityExecutionContext context,
            ArtifactAbilityInstance ability,
            ArtifactAbilityRuntimeEntry runtime,
            object evt);

        void Invoke(
            ArtifactAbilityExecutionContext context,
            ArtifactAbilityInstance ability,
            ref ArtifactAbilityRuntimeEntry runtime,
            object evt);
    }

    private sealed class EventHandler<TEvent> : IEventHandler where TEvent : class
    {
        private readonly ArtifactAbilityEventCondition<TEvent> _condition;
        private readonly ArtifactAbilityEventHandler<TEvent> _handler;

        public EventHandler(
            ArtifactAbilityEventCondition<TEvent> condition,
            ArtifactAbilityEventHandler<TEvent> handler)
        {
            _condition = condition;
            _handler = handler;
        }

        public bool CanInvoke(
            ArtifactAbilityExecutionContext context,
            ArtifactAbilityInstance ability,
            ArtifactAbilityRuntimeEntry runtime,
            object evt)
        {
            return _condition?.Invoke(context, ability, runtime, (TEvent)evt) ?? true;
        }

        public void Invoke(
            ArtifactAbilityExecutionContext context,
            ArtifactAbilityInstance ability,
            ref ArtifactAbilityRuntimeEntry runtime,
            object evt)
        {
            _handler(context, ability, ref runtime, (TEvent)evt);
        }
    }
}

/// <summary>
/// 法器能力技术互斥组的强类型键，防止它和语义资产混用。
/// </summary>
public readonly struct ArtifactAbilityExclusivityKey : IEquatable<ArtifactAbilityExclusivityKey>
{
    private readonly string value;

    public bool IsEmpty => string.IsNullOrEmpty(value);

    public ArtifactAbilityExclusivityKey(string value)
    {
        this.value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(ArtifactAbilityExclusivityKey other) => value == other.value;
    public override bool Equals(object obj) => obj is ArtifactAbilityExclusivityKey other && Equals(other);
    public override int GetHashCode() => value?.GetHashCode() ?? 0;
    public override string ToString() => value ?? string.Empty;
}

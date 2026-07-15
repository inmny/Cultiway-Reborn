using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
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
    public ArtifactAtomAsset[] atoms = [];
    public string composition_key;
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
    public string[] tags = [];
    public string exclusive_group;
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

    private readonly Dictionary<Type, IEventHandler> _handlers = new();

    public float ScoreFor(ArtifactAbilityComposeContext context)
    {
        return Mathf.Max(0f, ScoreRecipe?.Invoke(context) ?? 0f);
    }

    public bool HasTag(string tag)
    {
        return Array.IndexOf(tags, tag) >= 0;
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
               handler.CanInvoke(context, ability, runtime, evt);
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
        if (!handler.CanInvoke(context, ability, runtime, evt)) return false;
        handler.Invoke(context, ability, ref runtime, evt);
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

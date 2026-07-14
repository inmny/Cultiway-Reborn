using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Friflo.Engine.ECS;
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
    public string seed;
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
/// 注册式法器能力。每个能力拥有独立参数规格、运行状态、调度元数据和事件处理器。
/// </summary>
public class ArtifactAbilityAsset : Asset
{
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

    private readonly Dictionary<Type, IEventHandler> _handlers = new();

    public float ScoreFor(ArtifactAbilityComposeContext context)
    {
        return Mathf.Max(0f, ScoreRecipe?.Invoke(context) ?? 0f);
    }

    public bool HasTag(string tag)
    {
        return Array.IndexOf(tags, tag) >= 0;
    }

    public ArtifactAbilityAsset Handle<TEvent>(ArtifactAbilityEventHandler<TEvent> handler)
        where TEvent : class
    {
        _handlers[typeof(TEvent)] = new EventHandler<TEvent>(handler);
        return this;
    }

    internal ArtifactAbilityInstance ComposeInstance(ArtifactAbilityComposeContext context)
    {
        ArtifactAbilityValue[] parameters = ComposeParameters?.Invoke(context) ?? [];
        ValidateValues(parameters, parameter_schema, "参数");
        return new ArtifactAbilityInstance
        {
            instance_id = id,
            ability_id = id,
            parameters = parameters,
        };
    }

    internal ArtifactAbilityRuntimeEntry ComposeRuntime(ArtifactAbilityComposeContext context)
    {
        ArtifactAbilityValue[] values = ComposeInitialState?.Invoke(context) ?? [];
        ValidateValues(values, state_schema, "运行状态");
        return new ArtifactAbilityRuntimeEntry
        {
            instance_id = id,
            values = values,
        };
    }

    internal bool TryHandle<TEvent>(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        TEvent evt)
        where TEvent : class
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out IEventHandler handler)) return false;
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
        void Invoke(
            ArtifactAbilityExecutionContext context,
            ArtifactAbilityInstance ability,
            ref ArtifactAbilityRuntimeEntry runtime,
            object evt);
    }

    private sealed class EventHandler<TEvent> : IEventHandler where TEvent : class
    {
        private readonly ArtifactAbilityEventHandler<TEvent> _handler;

        public EventHandler(ArtifactAbilityEventHandler<TEvent> handler)
        {
            _handler = handler;
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

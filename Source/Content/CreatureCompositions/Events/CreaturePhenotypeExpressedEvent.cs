using System;
using Cultiway.Content.CreatureCompositions.Components;

namespace Cultiway.Content.CreatureCompositions.Events;

/// <summary>一次身体表达已经全部提交后发出的只读事件。</summary>
public readonly struct CreaturePhenotypeExpressedEvent
{
    /// <summary>创建一条身体表达完成事件。</summary>
    /// <param name="actor">完成表达的生物单位。</param>
    /// <param name="phenotype">刚提交的当前身体。</param>
    /// <param name="previous">提交前的身体；首次表达时为默认值。</param>
    /// <param name="firstExpression">这次是否是该单位的第一次身体表达。</param>
    /// <param name="morphChanged">固定形态对应的生物单位模板是否发生了切换。</param>
    public CreaturePhenotypeExpressedEvent(
        Cultiway.Core.ActorExtend actor,
        CreaturePhenotype phenotype,
        CreaturePhenotype previous,
        bool firstExpression,
        bool morphChanged)
    {
        Actor = actor;
        Phenotype = phenotype;
        Previous = previous;
        FirstExpression = firstExpression;
        MorphChanged = morphChanged;
    }

    /// <summary>完成表达的生物单位。</summary>
    public Core.ActorExtend Actor { get; }

    /// <summary>刚提交的当前身体。</summary>
    public CreaturePhenotype Phenotype { get; }

    /// <summary>提交前的身体；首次表达时为默认值。</summary>
    public CreaturePhenotype Previous { get; }

    /// <summary>这次是否是该单位的第一次身体表达。</summary>
    public bool FirstExpression { get; }

    /// <summary>固定形态对应的生物单位模板是否发生了切换。</summary>
    public bool MorphChanged { get; }
}

/// <summary>身体表达事件的观察入口，供需要在安全时机响应身体变化的玩法注册。</summary>
public static class CreaturePhenotypeEvents
{
    private static Action<CreaturePhenotypeExpressedEvent> expressed;

    /// <summary>注册一次身体表达完成观察者；同一委托不会重复注册。</summary>
    public static void RegisterExpressed(Action<CreaturePhenotypeExpressedEvent> handler)
    {
        if (handler == null) return;
        expressed += handler;
    }

    /// <summary>通知全部观察者；仅由表达服务在提交完成后调用。</summary>
    internal static void PublishExpressed(CreaturePhenotypeExpressedEvent evt)
    {
        var handlers = expressed;
        handlers?.Invoke(evt);
    }
}

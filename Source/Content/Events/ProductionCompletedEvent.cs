using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Events;

/// <summary>生产成品完成全部质量、数量与正式组件结算后发布的只读事件。</summary>
public readonly struct ProductionCompletedEvent
{
    public ProductionCompletedEvent(
        ActorExtend producer,
        string process,
        object recipe,
        Entity product,
        ItemLevel finalLevel,
        int outputCount)
    {
        Producer = producer;
        Process = process;
        Recipe = recipe;
        Product = product;
        FinalLevel = finalLevel;
        OutputCount = outputCount;
    }

    public ActorExtend Producer { get; }
    public string Process { get; }
    public object Recipe { get; }
    public Entity Product { get; }
    public ItemLevel FinalLevel { get; }
    public int OutputCount { get; }
}

/// <summary>同步通知最终生产结果，确保观察者读取到的成品已经定型。</summary>
public static class ProductionLifecycle
{
    private static readonly List<Action<ProductionCompletedEvent>> completedHandlers = new();

    public static void RegisterCompleted(Action<ProductionCompletedEvent> handler)
    {
        if (handler == null || completedHandlers.Contains(handler)) return;
        completedHandlers.Add(handler);
    }

    internal static void PublishCompleted(ProductionCompletedEvent evt)
    {
        for (var i = 0; i < completedHandlers.Count; i++)
        {
            completedHandlers[i](evt);
        }
    }
}

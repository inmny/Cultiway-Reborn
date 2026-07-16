using System;
using Cultiway.Abstract;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>生产系统向已装备法器发布通用工序事件的统一入口。</summary>
public static class ArtifactProductionService
{
    public static ArtifactProductionStepEvent DispatchStep(
        ActorExtend producer,
        string process,
        object recipe,
        Entity product,
        float duration)
    {
        ArtifactProductionStepEvent evt = new(producer, process, recipe, product, duration);
        ArtifactAbilityDispatcher.Dispatch(producer.E, evt);
        return evt;
    }

    public static ArtifactProductionResultEvent DispatchResult(
        ActorExtend producer,
        string process,
        object recipe,
        Entity product)
    {
        ArtifactProductionResultEvent evt = new(producer, process, recipe, product);
        ArtifactAbilityDispatcher.Dispatch(producer.E, evt);
        return evt;
    }

    /// <summary>把连续产量倍率解析为本次实际产出数；小数部分按概率产生额外一件。</summary>
    public static int ResolveOutputCount(float yieldMultiplier)
    {
        float normalized = Mathf.Clamp(yieldMultiplier, 1f, 16f);
        int count = Mathf.FloorToInt(normalized);
        float remainder = normalized - count;
        return remainder > 0f && Randy.randomChance(remainder) ? count + 1 : count;
    }

    /// <summary>复制已定型成品，并修复实体自引用组件。</summary>
    public static Entity CloneProduct(Entity product)
    {
        Entity clone = product.Store.CloneEntity(product);
        if (clone.HasComponent<SpecialItem>()) clone.GetComponent<SpecialItem>().self = clone;
        return clone;
    }

    /// <summary>把原件及其副产物交给同一接收者。</summary>
    public static void AddOutputs(
        IHasInventory receiver,
        Entity product,
        int outputCount,
        Action<Entity> configureClone = null)
    {
        receiver.AddSpecialItem(product);
        for (int i = 1; i < outputCount; i++)
        {
            Entity clone = CloneProduct(product);
            configureClone?.Invoke(clone);
            receiver.AddSpecialItem(clone);
        }
    }
}

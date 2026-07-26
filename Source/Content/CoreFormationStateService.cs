using System;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 管理形成技能授予的普通共享状态。形成机制数值存放在状态实体上，
/// 因而持续时间、净化、驱散、图标和动画均沿用 Status 规则。
/// </summary>
internal static class CoreFormationStateService
{
    /// <summary>没有主动时限的机制状态使用的统一持久秒数。</summary>
    internal const float PersistentDuration = 100000000f;

    /// <summary>取得现有形成状态；不存在时返回 false。</summary>
    internal static bool TryGet(
        ActorExtend owner,
        in CoreFormationResolvedEffect effect,
        out Entity status,
        out CoreFormationEffectState state)
    {
        if (owner != null &&
            CombatStatusEffects.TryGetStatus(
                owner.Base,
                effect.Definition.StateStatus,
                owner.Base,
                out status) &&
            status.TryGetComponent(out state))
        {
            return true;
        }
        status = default;
        state = default;
        return false;
    }

    /// <summary>取得现有状态，或以持久时长创建一个新的形成状态。</summary>
    internal static Entity GetOrCreate(
        ActorExtend owner,
        in CoreFormationResolvedEffect effect,
        out CoreFormationEffectState state)
    {
        return GetOrCreate(owner, effect, PersistentDuration, false, out state);
    }

    /// <summary>创建或刷新一个主动形成状态，并按需重置机制数据。</summary>
    internal static Entity Activate(
        ActorExtend owner,
        in CoreFormationResolvedEffect effect,
        float duration,
        bool reset,
        out CoreFormationEffectState state)
    {
        return GetOrCreate(owner, effect, duration, reset, out state);
    }

    /// <summary>把修改后的形成机制数据写回状态实体。</summary>
    internal static void Save(Entity status, in CoreFormationEffectState state)
    {
        if (!status.IsNull && status.HasComponent<CoreFormationEffectState>())
            status.GetComponent<CoreFormationEffectState>() = state;
    }

    /// <summary>移除当前效果对应的形成状态。</summary>
    internal static void Remove(ActorExtend owner, in CoreFormationResolvedEffect effect)
    {
        if (owner == null) return;
        CombatStatusEffects.RemoveStatus(
            owner.Base,
            effect.Definition.StateStatus,
            owner.Base);
    }

    /// <summary>取得当前效果状态的剩余秒数；持久状态返回一个很大的正数。</summary>
    internal static float GetRemaining(
        ActorExtend owner,
        in CoreFormationResolvedEffect effect)
    {
        if (!TryGet(owner, effect, out Entity status, out _) ||
            !status.TryGetComponent(out AliveTimeLimit limit)) return 0f;
        float elapsed = status.TryGetComponent(out AliveTimer timer) ? timer.value : 0f;
        return Mathf.Max(0f, limit.value - elapsed);
    }

    /// <summary>移除角色身上全部形成状态，但不影响这些状态施加到其他目标的负面效果。</summary>
    internal static void ClearSelfStates(ActorExtend owner)
    {
        if (owner == null) return;
        foreach (Entity status in owner.GetStatuses())
        {
            if (!status.HasComponent<CoreFormationEffectState>()) continue;
            owner.RemoveSharedStatus(status);
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(status.Id);
        }
    }

    /// <summary>按状态资产创建或刷新状态，并返回可直接修改的数据副本。</summary>
    private static Entity GetOrCreate(
        ActorExtend owner,
        in CoreFormationResolvedEffect effect,
        float duration,
        bool reset,
        out CoreFormationEffectState state)
    {
        state = default;
        if (owner == null || effect.Definition.StateStatus == null) return default;
        float visualScaleMultiplier = effect.Potency;
        Entity status = CombatStatusEffects.ApplyStateStatus(
            owner.Base,
            effect.Definition.StateStatus,
            Mathf.Max(0.1f, duration),
            owner.Base,
            entity =>
            {
                ref CoreFormationEffectState current =
                    ref entity.GetComponent<CoreFormationEffectState>();
                if (reset) current = default;
                if (entity.HasComponent<StatusAnimationState>())
                {
                    entity.GetComponent<StatusAnimationState>().ScaleMultiplier = visualScaleMultiplier;
                }
            });
        if (!status.IsNull) state = status.GetComponent<CoreFormationEffectState>();
        return status;
    }
}

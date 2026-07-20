using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.ActiveAbilities;

/// <summary>把元婴显化和蜕变提供的主动形态接入统一主动能力系统。</summary>
internal sealed class CoreFormationActiveAbilityProvider : IActiveAbilityProvider
{
    /// <summary>统一主动能力系统使用的稳定 Provider ID。</summary>
    public const string ProviderId = "content.core_formation";

    /// <summary>返回稳定 Provider ID。</summary>
    public string Id => ProviderId;

    /// <summary>枚举角色当前所有带主动配置的合并效果族。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        using var effects = new ListPool<CoreFormationResolvedEffect>();
        CoreFormationEffectResolver.Resolve(caster, effects);
        if (!CoreFormationEffectResolver.Synchronize(caster, effects)) return;
        for (var i = 0; i < effects.Count; i++)
        {
            CoreFormationEffectDefinition definition = effects[i].Definition;
            if (definition.active != null)
                output.Add(new ActiveAbilityHandle(Id, caster.E, definition.family_id));
        }
    }

    /// <summary>形成主动能力只参与战斗通道。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out _, out _)
            ? ActiveAbilityChannel.Combat
            : ActiveAbilityChannel.None;
    }

    /// <summary>生成玩家控制界面使用的主动能力描述。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        ResolvedActive active = Resolve(caster, handle);
        Sprite icon = string.IsNullOrEmpty(active.Profile.icon_path)
            ? null
            : SpriteTextureLoader.getSprite(active.Profile.icon_path);
        return new ActiveAbilityDescriptor(
            active.Profile.GetName(),
            icon,
            ActiveAbilityChannel.Combat,
            active.Profile.target_mode,
            active.Profile.activation_mode);
    }

    /// <summary>检查冷却、固定灵气消耗和定义提供的战斗环境条件。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out ResolvedActive active, out CoreFormationEffectRuntimeEntry runtime))
            return false;
        return runtime.active_cooldown_remaining <= 0f && CanAfford(caster, active.Profile.wakan_cost) &&
               (active.Profile.CanPrepare?.Invoke(active.Effect, caster, runtime, target) ?? true);
    }

    /// <summary>检查能力准备条件、目标模式和实际作用距离。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!TryResolve(caster, handle, out ResolvedActive active, out CoreFormationEffectRuntimeEntry runtime) ||
            runtime.active_cooldown_remaining > 0f || !CanAfford(caster, active.Profile.wakan_cost)) return false;
        if (active.Profile.target_mode == ActiveAbilityTargetMode.Self) return true;
        Vector3 center = !target.Object.isRekt() ? target.Object.GetSimPos() : target.Position;
        float range = Mathf.Max(0f, active.Profile.range);
        return range <= 0f || (center - caster.Base.GetSimPos()).sqrMagnitude <= range * range;
    }

    /// <summary>按基础权重和当前生命压力调整 AI 释放倾向。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out ResolvedActive active, out _)) return 0;
        int weight = Mathf.Max(0, active.Profile.ai_weight);
        float healthRatio = caster.Base.stats[strings.S.health] <= 0f
            ? 1f
            : caster.Base.data.health / caster.Base.stats[strings.S.health];
        if (healthRatio < 0.5f) weight += 12;
        return weight;
    }

    /// <summary>返回主动配置声明的选择距离。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return TryResolve(caster, handle, out ResolvedActive active, out _)
            ? active.Profile.range
            : 0f;
    }

    /// <summary>返回主动配置声明的实际影响半径。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out ResolvedActive active, out _)
            ? active.Profile.radius
            : 0f;
    }

    /// <summary>支付固定灵气、执行定义委托，并在成功后写入主动冷却。</summary>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target) ||
            !TryResolve(caster, handle, out ResolvedActive active, out _)) return false;
        if (!CombatResourceEffects.TrySpendWakan(caster.Base, active.Profile.wakan_cost)) return false;

        CoreFormationEffectRuntime runtime = caster.E.GetComponent<CoreFormationEffectRuntime>();
        int index = runtime.FindIndex(active.Effect.Definition.family_id);
        if (index < 0 || active.Profile.Use == null)
        {
            CombatResourceEffects.RestoreWakan(caster.Base, active.Profile.wakan_cost);
            return false;
        }
        bool used = active.Profile.Use(active.Effect, caster, ref runtime.entries[index], target, origin);
        if (!used)
        {
            CombatResourceEffects.RestoreWakan(caster.Base, active.Profile.wakan_cost);
            return false;
        }
        runtime.entries[index].active_cooldown_remaining = active.Profile.cooldown;
        caster.E.GetComponent<CoreFormationEffectRuntime>() = runtime;
        return true;
    }

    /// <summary>判断角色当前是否拥有足够灵气支付固定能力消耗。</summary>
    private static bool CanAfford(ActorExtend caster, float cost)
    {
        return cost <= 0f || caster.HasCultisys<Xian>() && caster.GetCultisys<Xian>().wakan + 0.0001f >= cost;
    }

    /// <summary>解析主动能力句柄，不存在时抛出明确异常。</summary>
    private static ResolvedActive Resolve(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (TryResolve(caster, handle, out ResolvedActive active, out _)) return active;
        throw new InvalidOperationException($"核心形成主动能力不存在: {handle.EntryId}");
    }

    /// <summary>验证句柄归属，并解析效果定义及其当前运行时状态。</summary>
    private static bool TryResolve(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        out ResolvedActive active,
        out CoreFormationEffectRuntimeEntry runtimeEntry)
    {
        active = default;
        runtimeEntry = default;
        if (caster == null || handle.Source != caster.E || string.IsNullOrEmpty(handle.EntryId) ||
            !CoreFormationEffectResolver.TryResolveFamily(caster, handle.EntryId,
                out CoreFormationResolvedEffect effect) || effect.Definition.active == null) return false;
        if (!caster.E.TryGetComponent(out CoreFormationEffectRuntime runtime)) return false;
        int index = runtime.FindIndex(handle.EntryId);
        if (index < 0) return false;
        active = new ResolvedActive(effect);
        runtimeEntry = runtime.entries[index];
        return true;
    }

    /// <summary>主动定义与解析倍率组成的不可变内部结果。</summary>
    private readonly struct ResolvedActive
    {
        /// <summary>当前合并后的效果解析结果。</summary>
        public readonly CoreFormationResolvedEffect Effect;

        /// <summary>效果携带的主动配置。</summary>
        public CoreFormationActiveProfile Profile => Effect.Definition.active;

        /// <summary>创建一份主动能力解析结果。</summary>
        public ResolvedActive(CoreFormationResolvedEffect effect)
        {
            Effect = effect;
        }
    }
}

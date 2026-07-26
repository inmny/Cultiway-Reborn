using System;
using System.Collections.Generic;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
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
                output.Add(new ActiveAbilityHandle(
                    Id,
                    definition.active.SkillContainer,
                    definition.family_id));
        }
    }

    /// <summary>形成主动能力只参与战斗通道。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out _)
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
        if (!TryResolve(caster, handle, out ResolvedActive active)) return false;
        SkillCastPlan plan = CreatePlan(caster, active.Profile, target, caster.Base.GetSimPos());
        return SkillCooldownService.IsReady(caster, active.Profile.SkillContainer) &&
               SkillCastCost.CanPay(caster, active.Profile.SkillContainer, plan) &&
               (active.Profile.CanPrepare?.Invoke(active.Effect, caster, target) ?? true);
    }

    /// <summary>检查能力准备条件、目标模式和实际作用距离。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!TryResolve(caster, handle, out ResolvedActive active) ||
            !SkillCooldownService.IsReady(caster, active.Profile.SkillContainer)) return false;
        SkillCastPlan plan = CreatePlan(caster, active.Profile, target.Object, target.Position);
        if (!SkillCastCost.CanPay(caster, active.Profile.SkillContainer, plan)) return false;
        if (active.Profile.target_mode == ActiveAbilityTargetMode.Self) return true;
        Vector3 center = !target.Object.isRekt() ? target.Object.GetSimPos() : target.Position;
        float range = Mathf.Max(0f, active.Profile.range);
        return range <= 0f || (center - caster.Base.GetSimPos()).sqrMagnitude <= range * range;
    }

    /// <summary>按基础权重和当前生命压力调整 AI 释放倾向。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out ResolvedActive active)) return 0;
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
        return TryResolve(caster, handle, out ResolvedActive active)
            ? active.Profile.range
            : 0f;
    }

    /// <summary>返回主动配置声明的实际影响半径。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out ResolvedActive active)
            ? active.Profile.radius
            : 0f;
    }

    /// <summary>通过来源授予技能支付灵气并启动标准一步式施法，成功后写入通用技能冷却。</summary>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target) ||
            !TryResolve(caster, handle, out ResolvedActive active)) return false;
        SkillCastPlan plan = CreatePlan(caster, active.Profile, target.Object, target.Position);
        if (plan.Steps.Count == 0) return false;
        SkillCastRuntimeData runtimeData =
            SkillCastRuntimeData.Create(active.Effect.Potency, DamageOrigin.Primary);
        bool used = ModClass.I.SkillV3.StartSkillSequence(
            caster,
            active.Profile.SkillContainer,
            plan,
            caster.Base.stats[S.damage],
            caster.GetPowerLevel(),
            SkillCastFundingSource.CasterResources,
            target.AttackKingdom,
            runtimeData);
        if (!used) return false;
        SkillCooldownService.Start(caster, active.Profile.SkillContainer, active.Profile.cooldown);
        return true;
    }

    /// <summary>按主动目标模式构造恰好包含一个释放步骤的计划。</summary>
    private static SkillCastPlan CreatePlan(
        ActorExtend caster,
        CoreFormationActiveProfile profile,
        BaseSimObject target,
        Vector3 targetPosition)
    {
        var plan = new SkillCastPlan();
        if (caster?.Base == null || caster.Base.isRekt()) return plan;
        if (profile.target_mode == ActiveAbilityTargetMode.Self)
        {
            plan.Steps.Add(new SkillCastStep(caster.Base, 0f));
        }
        else if (!target.isRekt())
        {
            plan.Steps.Add(new SkillCastStep(target, 0f));
        }
        else
        {
            plan.Steps.Add(new SkillCastStep(targetPosition, 0f));
        }
        return plan;
    }

    /// <summary>解析主动能力句柄，不存在时抛出明确异常。</summary>
    private static ResolvedActive Resolve(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (TryResolve(caster, handle, out ResolvedActive active)) return active;
        throw new InvalidOperationException($"核心形成主动能力不存在: {handle.EntryId}");
    }

    /// <summary>验证句柄归属，并解析效果定义及其当前来源授予技能。</summary>
    private static bool TryResolve(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        out ResolvedActive active)
    {
        active = default;
        if (caster == null || string.IsNullOrEmpty(handle.EntryId) ||
            !CoreFormationEffectResolver.TryResolveFamily(caster, handle.EntryId,
                out CoreFormationResolvedEffect effect) || effect.Definition.active == null) return false;
        if (handle.Source != effect.Definition.active.SkillContainer) return false;
        active = new ResolvedActive(effect);
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

using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using strings;
using UnityEngine;

namespace Cultiway.Content.ActiveAbilities;

/// <summary>向统一主动能力系统公开临时命魂可用的基础神念攻击。</summary>
internal sealed class YuanshenSoulActiveAbilityProvider : IActiveAbilityProvider
{
    /// <summary>主动能力系统使用的稳定来源编号。</summary>
    public const string ProviderId = "content.yuanshen_soul";

    /// <summary>基础神念攻击条目编号。</summary>
    private const string Strike = "strike";

    /// <summary>返回稳定来源编号。</summary>
    public string Id => ProviderId;

    /// <summary>仅为有效临时命魂提供基础神念攻击。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!IsSoulCarrier(caster) || caster.HasComponent<YuanshenBodilessTransitState>() ||
            !YuanshenNodeCombatService.CanUseSoulAbilities(caster)) return;
        output.Add(new ActiveAbilityHandle(Id, caster.E, Strike));
    }

    /// <summary>基础神念攻击同时参与战斗和玩家世界控制。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsValid(caster, handle)
            ? ActiveAbilityChannel.Combat | ActiveAbilityChannel.World
            : ActiveAbilityChannel.None;
    }

    /// <summary>生成基础神念攻击的名称、图标和人物目标方式。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return new ActiveAbilityDescriptor(
            "Cultiway.Yuanshen.Ability.Strike".Localize(),
            SpriteTextureLoader.getSprite("cultiway/icons/artifact_atoms/soul_binding_script"),
            ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
            ActiveAbilityTargetMode.Object,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            SkillUseTargetRelation.Hostile);
    }

    /// <summary>按有效性、共享冷却和原人物灵气返回控制状态。</summary>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!IsValid(caster, handle))
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        float cooldown = YuanshenNodeCombatService.GetSoulStrikeCooldownRemaining(caster);
        if (cooldown > 0f)
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Cooldown, cooldown);
        return YuanshenNodeCombatService.CanPaySoulStrike(caster)
            ? ActiveAbilityControlState.Ready
            : new ActiveAbilityControlState(ActiveAbilityControlBlockReason.InsufficientResource);
    }

    /// <summary>检查一个明确敌方人物是否可由当前命魂攻击。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return ResolveControlState(caster, handle).CanUse && target?.isActor() == true &&
               YuanshenNodeCombatService.CanSoulStrike(caster, target.a);
    }

    /// <summary>检查本次人物目标是否仍满足神念攻击条件。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        return target.Object?.isActor() == true && CanPrepare(caster, handle, target.Object);
    }

    /// <summary>返回基础神念攻击在普通战斗规划中的选择权重。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return CanPrepare(caster, handle, target) ? 6 : 0;
    }

    /// <summary>返回单体、近距、纯神魂攻击的战斗画像。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        return IsValid(caster, handle)
            ? new ActiveAbilityTacticalProfile(
                offensive: 1f,
                defensive: 0f,
                support: 0f,
                control: 0f,
                power: 1f,
                resourceDemand: 0.2f,
                expectedTargets: 1f,
                impactKind: SkillImpactKind.Projectile,
                normalizedResourceCost: 0.02f)
            : default;
    }

    /// <summary>返回基础神念攻击距离。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return IsValid(caster, handle) ? YuanshenNodeCombatService.BasicStrikeRange : 0f;
    }

    /// <summary>基础神念攻击没有范围预览。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) => 0f;

    /// <summary>由临时命魂向明确人物提交基础神念攻击。</summary>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        return CanUse(caster, handle, target) &&
               YuanshenNodeCombatService.TrySoulStrike(caster, target.Object.a);
    }

    /// <summary>判断当前主动能力调用是否由魂体载体发起。</summary>
    private static bool IsSoulCarrier(ActorExtend caster)
    {
        SkillCasterContext context = SkillCasterContextService.TryGetCurrent(caster, out SkillCasterContext current)
            ? current
            : SkillCasterContextService.Resolve(caster);
        return context.IsValid && context.Kind == SkillCarrierKind.Soul;
    }

    /// <summary>校验能力来源人物和唯一条目编号。</summary>
    private static bool IsValid(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return caster != null && IsSoulCarrier(caster) &&
               !caster.HasComponent<YuanshenBodilessTransitState>() &&
               YuanshenNodeCombatService.CanUseSoulAbilities(caster) &&
               handle.ProviderId == ProviderId && handle.Source == caster.E && handle.EntryId == Strike;
    }
}

using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Combat;

/// <summary>保留原有徒手能力入口；动作、接触和显示全部交给共用构造执行。</summary>
internal sealed class PalmStrikeCombat : IActiveAbilityProvider
{
    /// <summary>原有能力来源编号。</summary>
    internal const string ProviderId = "content.palm_strike";
    /// <summary>原有主动来源。</summary>
    string IActiveAbilityProvider.Id => ProviderId;
    /// <summary>真实徒手身体保留原有入口。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="output">统一能力集合。</param>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (GeneralSettings.EnableSkillSystems && ConstructedAbilityService.HasBodyFor(caster, BodyActionCatalog.PalmId))
            output.Add(new ActiveAbilityHandle(ProviderId, caster.E));
    }
    /// <summary>现有掌击仍为战斗能力。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle) => ActiveAbilityChannel.Combat;
    /// <summary>沿用名称、图标及原地发力规则。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle) => new(
        "Cultiway.PalmStrike".Localize(), SpriteTextureLoader.getSprite("cultiway/icons/skills/knight/repulse"),
        ActiveAbilityChannel.Combat, ActiveAbilityTargetMode.Object, ActiveAbilityActivationMode.Instant,
        ActiveAbilityCastMobility.StationaryDuringRecovery);
    /// <summary>沿用普通徒手攻击恢复，身体占用由共用执行维护。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle) =>
        !ConstructedAbilityService.HasBodyFor(caster, BodyActionCatalog.PalmId) ? new(ActiveAbilityControlBlockReason.Unavailable) :
        caster.Base.attack_timer > 0f ? new(ActiveAbilityControlBlockReason.Cooldown, caster.Base.attack_timer) :
        ConstructedAbilityService.CanStartBody(caster, BodyActionCatalog.PalmId) ? ActiveAbilityControlState.Ready : new(ActiveAbilityControlBlockReason.Unavailable);
    /// <summary>受理前核对真实身体及恢复状态。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">实际目标。</param>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        handle.ProviderId == ProviderId && handle.Source == caster.E && ResolveControlState(caster, handle).CanUse && caster.Base.isAttackPossible();
    /// <summary>只选取身边实际可接触的活体敌人；挥掌后是否命中由路径决定。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">实际目标。</param>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target) =>
        CanPrepare(caster, handle, target.Object) && target.Object?.isActor() == true && target.Object.isAlive() &&
        !target.Object.a.isFlying() && SkillTargetRelationResolver.IsHostile(caster.Base, target.Object) &&
        Vector2.Distance(caster.Base.current_position, target.Object.current_position) <= ResolveRange(caster, handle, target.Object) &&
        Mathf.Abs(caster.Base.position_height - target.Object.a.position_height) <= SpatialActorPose.Height(caster.Base) * .4f;
    /// <summary>沿用徒手自主出手权重。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">实际目标。</param>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 5;
    /// <summary>按实际徒手伤害报告。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">实际目标。</param>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) =>
        new(1f, 0f, 0f, 0f, caster.Base.stats["damage"], 0f, 1f);
    /// <summary>沿用身体尺寸决定的触达选择。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">实际目标。</param>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => ConstructedAbilityService.Range(caster, BodyActionCatalog.PalmId, target);
    /// <summary>没有独立圆形伤害范围。</summary>
    /// <param name="caster">当前身体。</param>
    /// <param name="handle">能力句柄。</param>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle) => 0f;
    /// <summary>只有共用执行系统推进姿态和结算接触。</summary>
    /// <param name="caster">实际身体。</param>
    /// <param name="handle">能力句柄。</param>
    /// <param name="target">实际目标。</param>
    /// <param name="origin">实际调用方式。</param>
    public bool TryUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin) =>
        CanUse(caster, handle, target) && ConstructedAbilityService.Start(caster, BodyActionCatalog.PalmId, target.Object.a, origin);
}

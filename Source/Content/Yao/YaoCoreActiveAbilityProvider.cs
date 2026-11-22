using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     把妖丹方向的核心神通公开给战斗决策与技能详情界面；
///     每项神通都有妖力、冷却与稳定度代价，经现有技能系统施放。
/// </summary>
internal sealed class YaoCoreActiveAbilityProvider : IActiveAbilityProvider, ISourceGrantedSkillProvider
{
    private const string ProviderId = "content.yao_core";
    private const int MaximumCoreAbilities = 2;

    /// <summary>稳定的提供者编号。</summary>
    public string Id => ProviderId;

    /// <summary>按妖丹方向收集核心神通展示。</summary>
    public void Collect(ActorExtend actor, ICollection<SourceGrantedSkillPresentation> output)
    {
        foreach ((Entity container, string patternId, _) in ResolveCoreSkills(actor))
        {
            output.Add(new SourceGrantedSkillPresentation(
                container, $"Cultiway.Yao.CorePattern.{patternId}.SkillDetail"));
        }
    }

    /// <summary>按妖丹方向收集核心神通句柄；每只妖兽最多两个。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        foreach ((Entity container, string patternId, int index) in ResolveCoreSkills(caster))
        {
            if (index >= MaximumCoreAbilities) break;
            output.Add(new ActiveAbilityHandle(Id, container, $"{patternId}/{index}"));
        }
    }

    /// <summary>妖丹神通只参与战斗决策。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsAvailable(caster, handle) ? ActiveAbilityChannel.Combat : ActiveAbilityChannel.None;
    }

    /// <summary>返回玩家控制和战术规划使用的神通名称、图标与目标方式。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!IsAvailable(caster, handle)) return default;
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        return new ActiveAbilityDescriptor(
            asset == null ? handle.EntryId : asset.id.Localize(),
            asset?.Icon,
            ActiveAbilityChannel.Combat,
            ActiveAbilityTargetMode.Object,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            SkillUseTargetRelation.Hostile);
    }

    /// <summary>检查神通存在、冷却完成并且妖力足够支付第一步。</summary>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!IsAvailable(caster, handle))
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        float cooldown = SkillCooldownService.GetRemaining(caster, handle.Source);
        if (cooldown > 0f)
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Cooldown, cooldown);
        if (!SkillCastCost.CanPayStep(caster, handle.Source))
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.InsufficientResource);
        return ActiveAbilityControlState.Ready;
    }

    /// <summary>准备条件：神通可用且目标是敌对对象。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!IsAvailable(caster, handle)) return false;
        if (!SkillCooldownService.IsReady(caster, handle.Source)) return false;
        if (!SkillCastCost.CanPayStep(caster, handle.Source)) return false;
        if (target == null || target.isRekt()) return false;
        return SkillTargetRelationResolver.IsHostile(caster.Base, target);
    }

    /// <summary>检查目标是否进入妖丹神通的使用距离。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!CanPrepare(caster, handle, target.Object)) return false;
        float range = ResolveRange(caster, handle, target.Object) + target.Object.stats[S.size];
        return (target.Object.current_position - caster.Base.current_position).sqrMagnitude <= range * range;
    }

    /// <summary>按妖丹强度与生命状态给出使用意愿。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        if (asset == null || target == null) return 0;
        if (!caster.E.TryGetComponent(out YaoCore core)) return 0;

        float range = ResolveRange(caster, handle, target);
        if (range <= 0f ||
            (target.current_position - caster.Base.current_position).sqrMagnitude > range * range)
            return 0;
        return Mathf.Clamp(10 + Mathf.RoundToInt(core.Strength), 6, 28);
    }

    /// <summary>公开以输出为主的战术画像。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        if (asset == null) return default;
        float power = Mathf.Max(1f, caster.Base.stats[S.damage]) *
                      (asset.ImpactProfile != null ? asset.ImpactProfile.DamageMultiplier : 0.5f) *
                      CoreStrength(caster);
        return new ActiveAbilityTacticalProfile(power, 0f, 0f, 0f, power, 1f, 1f, SkillImpactKind.Projectile);
    }

    /// <summary>妖丹神通的使用距离按妖丹强度小幅成长。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return 7f + CoreStrength(caster) * 0.5f;
    }

    /// <summary>神通没有固定范围预览，按约定返回 0。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return 0f;
    }

    /// <summary>通过现有技能系统开始施放。</summary>
    public bool TryUse(
        ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin)
    {
        if (!GeneralSettings.EnableSkillSystems || !CanUse(caster, handle, target)) return false;
        var plan = SkillCastPlanner.CreatePlan(caster, handle.Source, target.Object,
            SkillCastCost.GetAffordableStepLimit(caster, handle.Source));
        SkillCastRuntimeData runtime = target.RuntimeData;
        runtime.Origin = origin;
        runtime.WorldScope = target.WorldScope;
        return ModClass.I.SkillV3.StartSkillSequence(caster, handle.Source, plan, SkillContext.DefaultStrength,
            attack_kingdom: target.AttackKingdom, runtime_data: runtime,
            options: new SkillCastSequenceOptions { Hooks = new CoreCast(this, handle) });
    }

    private static float CoreStrength(ActorExtend caster)
    {
        return caster.E.TryGetComponent(out YaoCore core) ? core.Strength : 1f;
    }

    /// <summary>读取人物当前妖丹方向，可以与器官共用同一技能容器。</summary>
    private static YaoCorePatternAsset ResolvePattern(ActorExtend actor) =>
        actor?.Base != null && !actor.Base.isRekt() && actor.HasCultisys<Yao>() &&
        actor.TryGetComponent(out YaoCore core)
            ? YaoCorePatterns.Get(core.CorePatternId)
            : null;

    private static IEnumerable<(Entity container, string patternId, int index)> ResolveCoreSkills(ActorExtend actor)
    {
        YaoCorePatternAsset pattern = ResolvePattern(actor);
        if (pattern?.SkillIds == null) yield break;
        for (int i = 0; i < pattern.SkillIds.Length; i++)
        {
            if (CreatureCompositions.ActiveAbilities.CreatureOrganSkillRegistry.TryGetContainer(pattern.SkillIds[i], out Entity container))
                yield return (container, pattern.Id, i);
        }
    }

    /// <summary>共用容器仍存在时，也要确认当前妖丹仍授予这项神通。</summary>
    private bool IsAvailable(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (handle.ProviderId != Id || handle.Source.IsNull ||
            !handle.Source.TryGetComponent(out SkillContainer skill)) return false;
        YaoCorePatternAsset pattern = ResolvePattern(caster);
        if (pattern?.SkillIds == null) return false;
        int index = Array.IndexOf(pattern.SkillIds, skill.SkillEntityAssetID);
        return index >= 0 && index < MaximumCoreAbilities && handle.EntryId == $"{pattern.Id}/{index}" &&
               CreatureCompositions.ActiveAbilities.CreatureOrganSkillRegistry.TryGetContainer(skill.SkillEntityAssetID, out Entity current) &&
               current == handle.Source;
    }

    /// <summary>保留受理时付款，失去来源时只取消尚未发出的步骤。</summary>
    private sealed class CoreCast(YaoCoreActiveAbilityProvider provider, ActiveAbilityHandle handle) : ISkillCastSequenceHooks
    {
        public bool CanStart(in SkillCastSequenceStartContext context) => provider.IsAvailable(context.Caster, handle);
        public void OnStarted(in SkillCastSequenceStartContext context) {}
        public void OnEnded(in SkillCastSequenceResult result) {}
        public SkillCastStepDecision PrepareStep(in SkillCastSequenceStepContext context, in SkillCastStep step) =>
            provider.IsAvailable(context.Caster, handle) ? SkillCastStepDecision.Emit(step) : SkillCastStepDecision.Cancel();
    }

    private static SkillEntityAsset ResolveSkillAsset(ActiveAbilityHandle handle)
    {
        return handle.Source.IsNull ? null : handle.Source.GetComponent<SkillContainer>().Asset;
    }
}

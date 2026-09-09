using System;
using System.Collections.Generic;
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
        foreach ((Entity container, string patternId) in ResolveCoreSkills(actor))
        {
            output.Add(new SourceGrantedSkillPresentation(
                container, $"Cultiway.Yao.CorePattern.{patternId}.SkillDetail"));
        }
    }

    /// <summary>按妖丹方向收集核心神通句柄；每只妖兽最多两个。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        int count = 0;
        foreach ((Entity container, string patternId) in ResolveCoreSkills(caster))
        {
            if (count >= MaximumCoreAbilities) break;
            output.Add(new ActiveAbilityHandle(Id, container, $"{patternId}/{count}"));
            count++;
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
        if (!CanUse(caster, handle, target)) return false;
        return caster.CastSkillV3(handle.Source, target.Object);
    }

    private static float CoreStrength(ActorExtend caster)
    {
        return caster.E.TryGetComponent(out YaoCore core) ? core.Strength : 1f;
    }

    /// <summary>读取妖丹方向登记的核心神通容器；方向没有神通时为空。</summary>
    private static IEnumerable<(Entity container, string patternId)> ResolveCoreSkills(ActorExtend actor)
    {
        var result = new List<(Entity, string)>();
        if (actor?.Base == null || actor.Base.isRekt()) return result;
        if (!actor.HasCultisys<Yao>()) return result;
        if (!actor.E.TryGetComponent(out YaoCore core) || string.IsNullOrEmpty(core.CorePatternId)) return result;

        YaoCorePatternAsset pattern = YaoCorePatterns.Get(core.CorePatternId);
        if (pattern == null) return result;

        foreach (string skillId in pattern.SkillIds ?? Array.Empty<string>())
        {
            if (!CreatureCompositions.ActiveAbilities.CreatureOrganSkillRegistry.TryGetContainer(skillId, out Entity container)) continue;
            result.Add((container, pattern.Id));
        }

        return result;
    }

    private bool IsAvailable(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return handle.ProviderId == Id && !handle.Source.IsNull &&
               handle.Source.HasComponent<SkillContainer>() && !caster.Base.isRekt();
    }

    private static SkillEntityAsset ResolveSkillAsset(ActiveAbilityHandle handle)
    {
        return handle.Source.IsNull ? null : handle.Source.GetComponent<SkillContainer>().Asset;
    }
}

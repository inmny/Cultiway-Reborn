using System;
using System.Collections.Generic;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Core;
using Cultiway.Core.Components;
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

namespace Cultiway.Content.CreatureCompositions.ActiveAbilities;

/// <summary>
///     把当前身体整理结果中的器官技能同时公开给战斗决策与技能详情界面。
///     同一技能被多个器官引用时只保留最高等级来源；能力只能经现有技能系统施放。
/// </summary>
internal sealed class CreaturePhenotypeActiveAbilityProvider : IActiveAbilityProvider, ISourceGrantedSkillProvider
{
    private struct OrganSkill
    {
        internal Entity Container;
        internal int Rank;
        internal string SlotId;
        internal string OrganId;
    }

    /// <summary>与语义贡献者共用的稳定编号。</summary>
    public string Id => Presentation.CreaturePhenotypeSemanticContributor.ContributorId;

    /// <summary>收集当前身体提供的全部来源技能展示，供技能详情界面读取。</summary>
    public void Collect(ActorExtend actor, ICollection<SourceGrantedSkillPresentation> output)
    {
        foreach (KeyValuePair<string, OrganSkill> skill in ResolveOrganSkills(actor))
        {
            output.Add(new SourceGrantedSkillPresentation(
                skill.Value.Container,
                $"Cultiway.CreatureOrgan.{skill.Value.OrganId}.SkillDetail"));
        }
    }

    /// <summary>收集当前身体提供的全部战斗主动能力句柄。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        foreach (KeyValuePair<string, OrganSkill> skill in ResolveOrganSkills(caster))
        {
            output.Add(new ActiveAbilityHandle(
                Id, skill.Value.Container, $"{skill.Value.SlotId}/{skill.Value.OrganId}/{skill.Key}"));
        }
    }

    /// <summary>器官技能只参与战斗决策，不作为世界工具释放。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsAvailable(caster, handle) ? ActiveAbilityChannel.Combat : ActiveAbilityChannel.None;
    }

    /// <summary>返回玩家控制和战术规划使用的技能名称、图标与目标方式。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        bool isAttack = asset != null && asset.Type == SkillEntityType.Attack;
        return new ActiveAbilityDescriptor(
            asset == null ? handle.EntryId : asset.id.Localize(),
            asset?.Icon,
            ActiveAbilityChannel.Combat,
            isAttack ? ActiveAbilityTargetMode.Object : ActiveAbilityTargetMode.Point,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            isAttack ? SkillUseTargetRelation.Hostile : SkillUseTargetRelation.WorldTile);
    }

    /// <summary>检查技能存在、冷却完成并且当前资源足够支付第一步。</summary>
    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!IsAvailable(caster, handle))
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        Entity container = handle.Source;
        float cooldown = SkillCooldownService.GetRemaining(caster, container);
        if (cooldown > 0f)
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Cooldown, cooldown);
        if (!SkillCastCost.CanPayStep(caster, container))
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.InsufficientResource);
        return ActiveAbilityControlState.Ready;
    }

    /// <summary>检查技能存在、冷却完成并且当前资源足够支付第一步。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!IsAvailable(caster, handle)) return false;
        Entity container = handle.Source;
        if (!SkillCooldownService.IsReady(caster, container)) return false;
        if (!SkillCastCost.CanPayStep(caster, container)) return false;
        if (target == null || target.isRekt()) return false;
        return ResolveSkillAsset(handle)?.Type != SkillEntityType.Attack ||
               SkillTargetRelationResolver.IsHostile(caster.Base, target);
    }

    /// <summary>在准备条件之外检查目标是否进入该技能的实际使用距离。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!CanPrepare(caster, handle, target.Object)) return false;
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        if (asset == null || asset.Type != SkillEntityType.Attack) return true;
        float range = ResolveRange(caster, handle, target.Object) + target.Object.stats[S.size];
        return (target.Object.current_position - caster.Base.current_position).sqrMagnitude <= range * range;
    }

    /// <summary>按战术画像和目标状态给出使用意愿；进攻技能要求目标在射程内，防御技能在生命偏低时加权。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        if (asset == null || target == null) return 0;

        float healthRatio = caster.Base.getHealthRatio();
        if (asset.Type == SkillEntityType.Attack)
        {
            float range = ResolveRange(caster, handle, target);
            if (range <= 0f ||
                (target.current_position - caster.Base.current_position).sqrMagnitude > range * range)
                return 0;
            return Mathf.Clamp(8 + Mathf.RoundToInt(6 * (1f - healthRatio)), 4, 24);
        }

        // 非攻击技能按防御与资源取向处理：生命越低越愿意使用保命、护体类能力。
        return healthRatio < 0.6f
            ? Mathf.Clamp(Mathf.RoundToInt(18 * (1f - healthRatio)), 6, 18)
            : 4;
    }

    /// <summary>根据技能类型公开输出、防御取向的战术画像。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        if (asset == null) return default;
        float power = Mathf.Max(1f, caster.Base.stats[S.damage]) *
                      (asset.ImpactProfile != null ? asset.ImpactProfile.DamageMultiplier : 0.5f);
        return asset.Type == SkillEntityType.Attack
            ? new ActiveAbilityTacticalProfile(power, 0f, 0f, 0f, power, 1f, 1f, SkillImpactKind.Projectile)
            : new ActiveAbilityTacticalProfile(0f, power, 0f, 0f, 0f, 1f, 1f, SkillImpactKind.Wave);
    }

    /// <summary>返回器官技能独立于原版近战的使用距离；缺失配置时不参与远程决策。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        if (asset?.ImpactProfile == null) return 0f;
        return asset.Type == SkillEntityType.Attack ? 6f : 0f;
    }

    /// <summary>器官技能没有固定范围预览，按约定返回 0。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return 0f;
    }

    /// <summary>通过现有技能系统开始施放，不直接生成投射物或结算伤害。</summary>
    public bool TryUse(
        ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target, ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target)) return false;
        return caster.CastSkillV3(handle.Source, target.Object);
    }

    /// <summary>整理当前身体的器官技能：同一技能只保留最高等级来源。</summary>
    private static Dictionary<string, OrganSkill> ResolveOrganSkills(ActorExtend actor)
    {
        var result = new Dictionary<string, OrganSkill>(StringComparer.Ordinal);
        if (actor?.Base == null || actor.Base.isRekt()) return result;
        if (!actor.TryGetComponent(out CreaturePhenotype phenotype) || !phenotype.IsValid) return result;
        if (!CreaturePhenotypeCompiler.TryGetCompiled(
                phenotype.CompiledIndex, phenotype.Signature, out CompiledCreaturePhenotype compiled))
            return result;

        foreach (CompiledCreatureOrgan organ in compiled.OrderedOrgans)
        {
            string[] skillIds = organ.Rank.SkillContainerIds ?? Array.Empty<string>();
            for (int i = 0; i < skillIds.Length; i++)
            {
                string skillId = skillIds[i];
                if (!CreatureOrganSkillRegistry.TryGetContainer(skillId, out Entity container)) continue;
                if (result.TryGetValue(skillId, out OrganSkill existing))
                {
                    if (organ.Rank.Rank <= existing.Rank) continue;
                }

                result[skillId] = new OrganSkill
                {
                    Container = container,
                    Rank = organ.Rank.Rank,
                    SlotId = organ.Entry.SlotId,
                    OrganId = organ.Organ.id,
                };
            }
        }

        return result;
    }

    /// <summary>句柄必须属于本提供者且技能容器真实存在。</summary>
    private bool IsAvailable(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return handle.ProviderId == Id && !handle.Source.IsNull &&
               handle.Source.HasComponent<SkillContainer>() && !caster.Base.isRekt();
    }

    /// <summary>按句柄来源容器读回技能资产，用于名称、图标与类型判断。</summary>
    private static SkillEntityAsset ResolveSkillAsset(ActiveAbilityHandle handle)
    {
        return handle.Source.IsNull ? null : handle.Source.GetComponent<SkillContainer>().Asset;
    }
}

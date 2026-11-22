using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.CreatureCompositions.Components;
using Cultiway.Content.CreatureCompositions.Models;
using Cultiway.Content.CreatureCompositions.Services;
using Cultiway.Content.Skills;
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
    /// <summary>与语义贡献者共用的稳定编号。</summary>
    public string Id => Presentation.CreaturePhenotypeSemanticContributor.ContributorId;

    /// <summary>收集当前身体提供的全部来源技能展示，供技能详情界面读取。</summary>
    public void Collect(ActorExtend actor, ICollection<SourceGrantedSkillPresentation> output)
    {
        if (!TryGetBody(actor, out var body)) return;
        foreach (string skill in body.ActiveAbilities)
        {
            if (!body.TryGetSkillSource(skill, out var source) ||
                !CreatureOrganSkillRegistry.TryGetContainer(skill, out var container)) continue;
            output.Add(new SourceGrantedSkillPresentation(
                container,
                $"Cultiway.CreatureOrgan.{source.Organ.Organ.id}.SkillDetail"));
        }
    }

    /// <summary>收集当前身体提供的全部战斗主动能力句柄。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!HasOwnBody(caster) || !TryGetBody(caster, out var body)) return;
        foreach (string skill in body.ActiveAbilities)
        {
            if (body.TryGetSkillSource(skill, out var source) &&
                CreatureOrganSkillRegistry.TryGetContainer(skill, out var container))
                output.Add(new ActiveAbilityHandle(Id, container, source.EntryId));
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
        if (!IsAvailable(caster, handle)) return default;
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        bool isAttack = asset != null && asset.Type == SkillEntityType.Attack;
        return new ActiveAbilityDescriptor(
            asset == null ? handle.EntryId : asset.id.Localize(),
            asset?.Icon,
            ActiveAbilityChannel.Combat,
            isAttack ? ActiveAbilityTargetMode.Object : ActiveAbilityTargetMode.Point,
            ActiveAbilityActivationMode.Instant,
            ActiveAbilityCastMobility.Mobile,
            isAttack ? SkillUseTargetRelation.Hostile : SkillUseTargetRelation.WorldTile,
            canCastConcurrently: asset != null && asset.CarrierRequirement != SkillCarrierRequirement.PhysicalBody);
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
        SkillEntityAsset asset = ResolveSkillAsset(handle);
        if (asset == SkillEntities.FoxfireBreath && !FoxfireBreathConstruction.CanBreathe(caster, target)) return false;
        return asset?.Type != SkillEntityType.Attack ||
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
        if (!GeneralSettings.EnableSkillSystems || !CanUse(caster, handle, target)) return false;
        var plan = SkillCastPlanner.CreatePlan(caster, handle.Source, target.Object,
            SkillCastCost.GetAffordableStepLimit(caster, handle.Source));
        SkillCastRuntimeData runtime = target.RuntimeData;
        runtime.Origin = origin; runtime.WorldScope = target.WorldScope;
        return ModClass.I.SkillV3.StartSkillSequence(caster, handle.Source, plan, SkillContext.DefaultStrength,
            attack_kingdom: target.AttackKingdom, runtime_data: runtime, options: new SkillCastSequenceOptions
            {
                PaymentTiming = SkillCastPaymentTiming.PerEmission,
                Hooks = new OrganCast(this, handle)
            });
    }

    private static bool TryGetBody(ActorExtend actor, out CompiledCreaturePhenotype body)
    {
        body = null;
        return actor?.Base != null && !actor.Base.isRekt() && actor.Base.isAlive() &&
               actor.TryGetComponent(out CreaturePhenotype phenotype) && phenotype.IsValid &&
               CreaturePhenotypeCompiler.TryGetCompiled(phenotype.CompiledIndex, phenotype.Signature, out body);
    }

    private static bool HasOwnBody(ActorExtend caster)
    {
        var context = SkillCasterContextService.Resolve(caster);
        return context.IsValid && context.HasPhysicalBody && context.Kind == SkillCarrierKind.Physical &&
               context.Carrier == caster;
    }

    /// <summary>确认能力仍由当前肉身的同一部位和等级授予，共用技能容器本身不能证明来源。</summary>
    private bool IsAvailable(ActorExtend caster, ActiveAbilityHandle handle) =>
        TryResolveSource(caster, handle, out _, out _);

    private bool TryResolveSource(ActorExtend caster, ActiveAbilityHandle handle, out CompiledCreaturePhenotype body,
        out CompiledCreatureOrgan organ)
    {
        body = null;
        organ = default;
        if (handle.ProviderId != Id || handle.Source.IsNull ||
            !handle.Source.TryGetComponent(out SkillContainer skill) || !HasOwnBody(caster) ||
            !TryGetBody(caster, out body) || !body.TryGetSkillSource(skill.SkillEntityAssetID, out var source) ||
            !CreatureOrganSkillRegistry.TryGetContainer(skill.SkillEntityAssetID, out var registered) ||
            registered != handle.Source || source.EntryId != handle.EntryId) return false;
        organ = source.Organ;
        return true;
    }

    /// <summary>在实际发射和收费前确认原部位仍可出手。</summary>
    private sealed class OrganCast(CreaturePhenotypeActiveAbilityProvider provider, ActiveAbilityHandle handle)
        : ISkillCastSequenceHooks
    {
        public bool CanStart(in SkillCastSequenceStartContext context) => provider.IsAvailable(context.Caster, handle);
        public void OnStarted(in SkillCastSequenceStartContext context) {}
        public void OnEnded(in SkillCastSequenceResult result) {}

        public SkillCastStepDecision PrepareStep(in SkillCastSequenceStepContext context, in SkillCastStep step)
        {
            return GeneralSettings.EnableSkillSystems && SkillCastPlanner.CanCast(context.Caster, handle.Source) &&
                   provider.TryResolveSource(context.Caster, handle, out var body, out var organ) &&
                   CreatureOrganCastingPosition.TryResolveWorld(context.Caster.Base, body, in organ, out var point)
                ? SkillCastStepDecision.Emit(step.WithWorldSource(point))
                : SkillCastStepDecision.Cancel();
        }
    }

    /// <summary>按句柄来源容器读回技能资产，用于名称、图标与类型判断。</summary>
    private static SkillEntityAsset ResolveSkillAsset(ActiveAbilityHandle handle)
    {
        return handle.Source.IsNull ? null : handle.Source.GetComponent<SkillContainer>().Asset;
    }
}

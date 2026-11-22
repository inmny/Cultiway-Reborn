using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts.ActiveAbilities;

/// <summary>
/// 将封存 SkillContainer 的一次性物品适配为统一主动能力。具体载体只负责权限、参数和消耗表现。
/// </summary>
internal abstract class ConsumableSkillActiveAbilityProvider : IActiveAbilityProvider, IActiveAbilityTargetAdvisor
{
    public abstract string Id { get; }

    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!GeneralSettings.EnableSkillSystems || !CanUseCarrier(caster)) return;

        foreach (Entity item in caster.GetItems())
        {
            if (IsAvailable(item) && TryResolvePayload(item, out _)) output.Add(new ActiveAbilityHandle(Id, item));
        }
    }

    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out SkillPayload payload)
            ? payload.Skill.GetComponent<SkillContainer>().Asset.UseProfile.Channels
            : ActiveAbilityChannel.None;
    }

    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!TryResolve(caster, handle, out SkillPayload payload)) return default;
        Entity item = handle.Source;
        string name = item.HasName
            ? item.Name.value
            : payload.Skill.GetComponent<SkillContainer>().SkillEntityAssetID.Localize();
        Sprite icon = item.TryGetComponent(out SpecialItem specialItem) ? specialItem.GetSprite() : null;
        SkillUseProfileAsset useProfile = payload.Skill.GetComponent<SkillContainer>().Asset.UseProfile;
        return new ActiveAbilityDescriptor(
            name,
            icon,
            useProfile.Channels,
            useProfile.TargetMode,
            ActiveAbilityActivationMode.Instant,
            targetRelation: useProfile.TargetRelation);
    }

    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out _)
            ? ActiveAbilityControlState.Ready
            : new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
    }

    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out SkillPayload payload)) return false;
        if (target != null && (target.isRekt() || !target.isAlive())) return false;
        SkillUseProfileAsset useProfile = payload.Skill.GetComponent<SkillContainer>().Asset.UseProfile;
        if (useProfile.Placement != SkillUsePlacement.CasterSelf && target != null &&
            !SkillTargetRelationResolver.Matches(useProfile.TargetRelation, caster.Base, target)) return false;
        return SkillCastCost.GetAffordableStepLimit(caster, payload.Skill, SkillCastFundingSource.Prepaid) > 0;
    }

    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        return TryResolve(caster, handle, out SkillPayload payload) &&
               TryCreatePlan(caster, payload.Skill, target, out _);
    }

    public abstract int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target);

    /// <summary>使用卷轴或符箓内封存技能的评估结果生成战术画像。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out SkillPayload payload))
            return new ActiveAbilityTacticalProfile(0f, 0f, 0f, 0f, 0f, 0f, 1f);

        return ActiveAbilityTacticalProfile.FromSkill(payload.Skill);
    }

    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out SkillPayload payload)) return 0f;
        SkillUseProfileAsset useProfile = payload.Skill.GetComponent<SkillContainer>().Asset.UseProfile;
        return caster.GetSkillCastRange(target) * useProfile.RangeMultiplier;
    }

    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolve(caster, handle, out SkillPayload payload)
            ? ResolveEffectRadius(caster, payload.Skill)
            : 0f;
    }

    /// <summary>让辅助卷轴和符箓复用封存法术的真实边际收益选择友军目标。</summary>
    public bool TryResolvePreferredTarget(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        IReadOnlyList<Actor> nearbyAllies,
        out BaseSimObject target)
    {
        target = null;
        if (!TryResolve(caster, handle, out SkillPayload payload)) return false;
        SkillUseProfileAsset profile = payload.Skill.GetComponent<SkillContainer>().Asset.UseProfile;
        if (profile.TargetRelation != SkillUseTargetRelation.Friendly) return false;
        return SkillEffectResolver.TryResolveBestFriendlyTarget(
            caster,
            payload.Skill,
            nearbyAllies,
            ResolveEffectRadius(caster, payload.Skill),
            profile.TargetMode == ActiveAbilityTargetMode.Area,
            out target);
    }

    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!TryResolve(caster, handle, out SkillPayload payload) ||
            !TryCreatePlan(caster, payload.Skill, target, out SkillCastPlan plan)) return false;

        SkillCastRuntimeData runtime = target.RuntimeData;
        runtime.Origin = origin;
        runtime.WorldScope = target.WorldScope;
        var use = new PendingUse(this, handle.Source, payload, plan);
        return ModClass.I.SkillV3.StartSkillSequence(caster, payload.Skill, plan, ResolveStrength(caster, payload),
            payload.PowerLevel, SkillCastFundingSource.Prepaid, target.AttackKingdom, runtime,
            new SkillCastSequenceOptions
            {
                PaymentTiming = SkillCastPaymentTiming.PerEmission,
                Hooks = use,
                FirstEmissionAccepted = use.Consume
            });
    }

    protected abstract bool CanUseCarrier(ActorExtend caster);

    protected abstract bool TryResolvePayload(Entity item, out SkillPayload payload);

    protected virtual float ResolveStrength(ActorExtend caster, SkillPayload payload) => payload.Strength;

    /// <summary>尚未发射只占用纸张，实际第一发确认后释放同一物品的空间表现。</summary>
    private sealed class PendingUse : ISkillCastSequenceHooks
    {
        private readonly ConsumableSkillActiveAbilityProvider provider;
        private readonly Entity item;
        private readonly SkillPayload payload;
        private readonly SkillUseProfileAsset profile;
        private readonly long[] targetIds;
        private bool consumed;

        internal PendingUse(ConsumableSkillActiveAbilityProvider provider, Entity item, SkillPayload payload, SkillCastPlan plan)
        {
            this.provider = provider;
            this.item = item;
            this.payload = payload;
            profile = payload.Skill.GetComponent<SkillContainer>().Asset.UseProfile;
            targetIds = new long[plan.Steps.Count];
            for (int i = 0; i < targetIds.Length; i++)
                targetIds[i] = plan.Steps[i].Target?.getID() ?? 0L;
        }

        /// <summary>启动前仍要由当前人物持有可用的原纸张。</summary>
        public bool CanStart(in SkillCastSequenceStartContext context) =>
            IsAvailable(item) && ContainsItem(context.Caster, item);

        /// <summary>短暂占用不会进入正式存档，也不会从库存移走原件。</summary>
        public void OnStarted(in SkillCastSequenceStartContext context) => item.AddTag<TagPendingConsumable>();

        /// <summary>逐发核对实际载体、持有关系、目标身份和射程。</summary>
        public SkillCastStepDecision PrepareStep(in SkillCastSequenceStepContext context, in SkillCastStep step)
        {
            ActorExtend caster = context.Caster;
            var carrier = SkillCasterContextService.Resolve(caster);
            if (!GeneralSettings.EnableSkillSystems || !provider.CanUseCarrier(caster) || !caster.Base.isAlive() ||
                !carrier.IsValid || !carrier.Carrier.Base.isAlive() || ActiveAbilityService.IsSilenced(in carrier) ||
                !SkillCastPlanner.CanCast(caster, payload.Skill) ||
                !consumed && (item.IsNull || item.Tags.HasAny(Tags.Get<TagConsumed, TagRecycle>()) ||
                    !ContainsItem(caster, item) || !provider.TryResolvePayload(item, out var current) || current.Skill != payload.Skill))
                return SkillCastStepDecision.Cancel();

            Vector3 origin = ResolveCastOrigin(caster);
            if (profile.Placement == SkillUsePlacement.CasterSelf)
                return SkillCastStepDecision.Emit(new SkillCastStep(origin, step.Delay, step.InitialAngleOffsetDegrees));
            if (step.TrackTarget)
            {
                if (!CanTarget(caster, profile, step.Target, origin) || targetIds[context.StepIndex] != step.Target.getID())
                    return SkillCastStepDecision.Skip();
            }
            else if (!CanTargetPoint(caster, payload.Skill, profile, step.TargetPos, origin))
                return SkillCastStepDecision.Skip();
            return SkillCastStepDecision.Emit(step);
        }

        /// <summary>正式受理第一发时只扣一次，表现异常也不能留下已花掉的原纸张。</summary>
        internal void Consume(SkillEmission emission)
        {
            consumed = true;
            item.AddTag<TagConsumed>();
            try
            {
                TalismanVfxManager.QueueActivation(emission.SpatialSource, item, payload.Skill,
                    emission.TargetPos - emission.SourcePos, emission.PowerLevel, emission.Strength);
            }
            finally { if (!item.IsNull) item.DeleteEntity(); }
        }

        /// <summary>第一发未成功便结束时，原纸张重新可用。</summary>
        public void OnEnded(in SkillCastSequenceResult result)
        {
            if (!consumed && !item.IsNull) item.RemoveTag<TagPendingConsumable>();
        }
    }

    private bool TryResolve(ActorExtend caster, ActiveAbilityHandle handle, out SkillPayload payload)
    {
        payload = default;
        if (!GeneralSettings.EnableSkillSystems || !CanUseCarrier(caster) || !IsAvailable(handle.Source) ||
            !ContainsItem(caster, handle.Source)) return false;
        return TryResolvePayload(handle.Source, out payload) &&
               !payload.Skill.IsNull && payload.Skill.HasComponent<SkillContainer>();
    }

    /// <summary>已占用、已支付或正在回收的纸张不会再出现在施法候选中。</summary>
    private static bool IsAvailable(Entity item) => !item.IsNull &&
        !item.Tags.HasAny(Tags.Get<TagPendingConsumable, TagConsumed, TagRecycle>());

    /// <summary>读取当前主动能力实际使用的人物载体位置。</summary>
    protected static Vector3 ResolveCastOrigin(ActorExtend caster)
    {
        return SkillCasterContextService.TryGetCurrent(caster, out SkillCasterContext context)
            ? context.Carrier.Base.GetSimPos()
            : caster.Base.GetSimPos();
    }

    private static bool ContainsItem(ActorExtend caster, Entity expected)
    {
        foreach (Entity item in caster.GetItems())
        {
            if (item == expected) return true;
        }
        return false;
    }

    private static bool TryCreatePlan(
        ActorExtend caster,
        Entity skill,
        in ActiveAbilityTarget target,
        out SkillCastPlan plan)
    {
        plan = null;
        if (target.WorldScope != null && !target.WorldScope.IsValid()) return false;
        int stepLimit = SkillCastCost.GetAffordableStepLimit(caster, skill, SkillCastFundingSource.Prepaid);
        if (stepLimit <= 0) return false;
        SkillUseProfileAsset useProfile = skill.GetComponent<SkillContainer>().Asset.UseProfile;

        Vector3 castOrigin = ResolveCastOrigin(caster);
        if (useProfile.Placement == SkillUsePlacement.CasterSelf)
        {
            plan = SkillCastPlanner.CreatePointPlan(caster, skill, castOrigin, stepLimit);
        }
        else if (target.Object != null)
        {
            if (!CanTarget(caster, useProfile, target.Object, castOrigin)) return false;
            plan = SkillCastPlanner.CreatePlan(caster, skill, target.Object, stepLimit,
                target.ExplicitTargets, target.SelectionArea.Active);
        }
        else
        {
            if (!CanTargetPoint(caster, skill, useProfile, target.Position, castOrigin)) return false;
            plan = SkillCastPlanner.CreatePointPlan(caster, skill, target.Position, stepLimit);
        }

        return plan.Steps.Count > 0 && SkillCastCost.CanPay(caster, skill, plan, SkillCastFundingSource.Prepaid);
    }

    /// <summary>指定对象必须在当前载体的射程内，仍符合封存法术的目标关系。</summary>
    private static bool CanTarget(ActorExtend caster, SkillUseProfileAsset profile, BaseSimObject target, Vector3 origin)
    {
        if (target == null || target.isRekt() || !target.isAlive() ||
            !SkillTargetRelationResolver.Matches(profile.TargetRelation, caster.Base, target)) return false;
        float range = caster.GetSkillCastRange(target) * profile.RangeMultiplier + target.stats[strings.S.size];
        return Toolbox.SquaredDistVec2Float(origin, target.current_position) <= range * range;
    }

    /// <summary>点选位置遵守原射程及地块限制，要求实际对象的法术没有落地替代入口。</summary>
    private static bool CanTargetPoint(ActorExtend caster, Entity skill, SkillUseProfileAsset profile, Vector3 point, Vector3 origin)
    {
        if (profile.TargetMode == ActiveAbilityTargetMode.Object ||
            profile.TargetRelation is SkillUseTargetRelation.Friendly or SkillUseTargetRelation.Self) return false;
        float range = caster.GetSkillCastRange(null) * profile.RangeMultiplier;
        return Toolbox.SquaredDistVec2Float(origin, point) <= range * range &&
               (profile.TargetRelation != SkillUseTargetRelation.WorldTile ||
                SkillEffectResolver.HasApplicableTile(caster, skill, point, ResolveEffectRadius(caster, skill)));
    }

    /// <summary>解析封存技能经过本体调谐和词条缩放后的真实效果半径。</summary>
    private static float ResolveEffectRadius(ActorExtend caster, Entity skill)
    {
        SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
        return SkillEffectRadius.ResolveForCaster(
            caster, skill,
            asset.ImpactProfile.EffectRadius * asset.ImpactTuning.EffectRadiusMultiplier);
    }

    protected readonly struct SkillPayload
    {
        public readonly Entity Skill;
        public readonly float Strength;
        public readonly float PowerLevel;

        public SkillPayload(Entity skill, float strength, float powerLevel)
        {
            Skill = skill;
            Strength = strength;
            PowerLevel = powerLevel;
        }
    }
}

internal sealed class TalismanActiveAbilityProvider : ConsumableSkillActiveAbilityProvider
{
    public const string ProviderId = "content.talisman";

    public override string Id => ProviderId;

    public override int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 10;

    protected override bool CanUseCarrier(ActorExtend caster) => caster.HasComponent<Xian>();

    protected override bool TryResolvePayload(Entity item, out SkillPayload payload)
    {
        if (!item.TryGetComponent(out Talisman talisman))
        {
            payload = default;
            return false;
        }

        payload = new SkillPayload(talisman.SkillContainer, talisman.Strength, talisman.PowerLevel);
        return true;
    }

    protected override float ResolveStrength(ActorExtend caster, SkillPayload payload)
    {
        float strength = payload.Strength;
        float casterPowerLevel = caster.GetPowerLevel();
        if (payload.PowerLevel > casterPowerLevel)
        {
            strength *= Mathf.Pow(2f, payload.PowerLevel - casterPowerLevel);
        }
        return strength;
    }


}

internal sealed class MagicScrollActiveAbilityProvider : ConsumableSkillActiveAbilityProvider
{
    public const string ProviderId = "content.magic_scroll";

    public override string Id => ProviderId;

    public override int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        // 卷轴保留为个人法术不可用时的后备手段，避免 AI 无谓消耗一次性物品。
        if (!TryResolvePayload(handle.Source, out SkillPayload payload)) return 0;
        if (payload.Skill.GetComponent<SkillContainer>().Asset.Type != SkillEntityType.Attack) return 10;
        foreach (Entity skill in caster.all_attack_skills)
        {
            if (caster.CanUseSkillContainerAtCurrentDistance(skill, target)) return 0;
        }
        return 10;
    }

    protected override bool CanUseCarrier(ActorExtend caster) => caster.HasCultisys<Magic>();

    protected override bool TryResolvePayload(Entity item, out SkillPayload payload)
    {
        if (!item.TryGetComponent(out MagicScroll scroll))
        {
            payload = default;
            return false;
        }

        payload = new SkillPayload(scroll.SkillContainer, scroll.Strength, scroll.PowerLevel);
        return true;
    }
}

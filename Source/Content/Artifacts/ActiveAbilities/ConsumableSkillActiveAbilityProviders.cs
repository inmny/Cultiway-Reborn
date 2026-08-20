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
            if (TryResolvePayload(item, out _)) output.Add(new ActiveAbilityHandle(Id, item));
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
        TryResolve(caster, handle, out SkillPayload payload);
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
        if (target != null && target.isRekt()) return false;
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
            ? ResolveEffectRadius(payload.Skill)
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
            ResolveEffectRadius(payload.Skill),
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

        float strength = ResolveStrength(caster, payload);
        if (!ModClass.I.SkillV3.StartSkillSequence(
                caster,
                payload.Skill,
                plan,
                strength,
                payload.PowerLevel,
                SkillCastFundingSource.Prepaid,
                target.AttackKingdom)) return false;

        OnActivated(caster, handle.Source, payload, target, strength);
        handle.Source.DeleteEntity();
        return true;
    }

    protected abstract bool CanUseCarrier(ActorExtend caster);

    protected abstract bool TryResolvePayload(Entity item, out SkillPayload payload);

    protected virtual float ResolveStrength(ActorExtend caster, SkillPayload payload) => payload.Strength;

    protected virtual void OnActivated(
        ActorExtend caster,
        Entity item,
        SkillPayload payload,
        in ActiveAbilityTarget target,
        float strength)
    {
    }

    private bool TryResolve(ActorExtend caster, ActiveAbilityHandle handle, out SkillPayload payload)
    {
        payload = default;
        if (!GeneralSettings.EnableSkillSystems || !CanUseCarrier(caster) || handle.Source.IsNull ||
            !ContainsItem(caster, handle.Source)) return false;
        return TryResolvePayload(handle.Source, out payload) &&
               !payload.Skill.IsNull && payload.Skill.HasComponent<SkillContainer>();
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
        int stepLimit = SkillCastCost.GetAffordableStepLimit(caster, skill, SkillCastFundingSource.Prepaid);
        if (stepLimit <= 0) return false;
        SkillUseProfileAsset useProfile = skill.GetComponent<SkillContainer>().Asset.UseProfile;

        if (useProfile.Placement == SkillUsePlacement.CasterSelf)
        {
            plan = SkillCastPlanner.CreatePointPlan(caster, skill, caster.Base.GetSimPos(), stepLimit);
        }
        else if (target.Object != null && !target.Object.isRekt())
        {
            if (!SkillTargetRelationResolver.Matches(
                    useProfile.TargetRelation,
                    caster.Base,
                    target.Object)) return false;
            float range = caster.GetSkillCastRange(target.Object) * useProfile.RangeMultiplier +
                          target.Object.stats[strings.S.size];
            if (Toolbox.SquaredDistVec2Float(caster.Base.current_position, target.Object.current_position) >
                range * range) return false;
            plan = SkillCastPlanner.CreatePlan(
                caster,
                skill,
                target.Object,
                stepLimit,
                target.ExplicitTargets,
                target.SelectionArea.Active);
        }
        else
        {
            if (useProfile.TargetRelation is SkillUseTargetRelation.Friendly or SkillUseTargetRelation.Self)
                return false;
            float range = caster.GetSkillCastRange(null) * useProfile.RangeMultiplier;
            if (Toolbox.SquaredDistVec2Float(caster.Base.current_position, target.Position) > range * range)
                return false;
            plan = SkillCastPlanner.CreatePointPlan(caster, skill, target.Position, stepLimit);
        }

        if (plan.Steps.Count == 0 ||
            !SkillCastCost.CanPay(caster, skill, plan, SkillCastFundingSource.Prepaid)) return false;
        return useProfile.TargetRelation != SkillUseTargetRelation.WorldTile ||
               SkillEffectResolver.HasApplicableTile(
                   caster,
                   skill,
                   target.Position,
                   ResolveEffectRadius(skill));
    }

    /// <summary>解析封存技能经过本体调谐和词条缩放后的真实效果半径。</summary>
    private static float ResolveEffectRadius(Entity skill)
    {
        SkillEntityAsset asset = skill.GetComponent<SkillContainer>().Asset;
        return SkillEffectRadius.ResolveContainer(
            skill,
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

    protected override void OnActivated(
        ActorExtend caster,
        Entity item,
        SkillPayload payload,
        in ActiveAbilityTarget target,
        float strength)
    {
        Vector3 direction = (target.Object?.GetSimPos() ?? target.Position) - caster.Base.GetSimPos();
        TalismanVfxManager.QueueActivation(
            caster.Base,
            item,
            payload.Skill,
            direction,
            payload.PowerLevel,
            strength);
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

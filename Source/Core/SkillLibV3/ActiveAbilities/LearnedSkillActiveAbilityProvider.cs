using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;

namespace Cultiway.Core.SkillLibV3.ActiveAbilities;

/// <summary>
/// 将角色已经掌握的 SkillContainer 适配为统一主动能力。
/// </summary>
internal sealed class LearnedSkillActiveAbilityProvider : IActiveAbilityProvider
{
    public const string ProviderId = "core.learned_skill";

    public string Id => ProviderId;

    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!GeneralSettings.EnableSkillSystems) return;
        IReadOnlyList<Entity> learnedSkills = caster.GetLearnedSkillsInOrder();
        for (int i = 0; i < learnedSkills.Count; i++)
        {
            Entity skill = learnedSkills[i];
            if (!skill.IsNull && skill.HasComponent<SkillContainer>() &&
                skill.GetComponent<SkillContainer>().Asset.Type is SkillEntityType.Attack or SkillEntityType.Defense)
            {
                output.Add(new ActiveAbilityHandle(Id, skill));
            }
        }
    }

    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return ActiveAbilityChannel.Combat;
    }

    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        Entity skill = handle.Source;
        string name = skill.HasName
            ? skill.Name.value
            : skill.GetComponent<SkillContainer>().SkillEntityAssetID.Localize();
        return new ActiveAbilityDescriptor(
            name,
            null,
            ActiveAbilityChannel.Combat,
            skill.GetComponent<SkillContainer>().Asset.UseProfile.TargetMode,
            ActiveAbilityActivationMode.Instant);
    }

    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        Entity skill = handle.Source;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;
        SkillUseProfileAsset useProfile = skill.GetComponent<SkillContainer>().Asset.UseProfile;
        if (useProfile.Placement == SkillUsePlacement.CasterSelf)
        {
            return SkillCastCost.GetAffordableStepLimit(caster, skill) > 0;
        }
        if (target != null && !target.isRekt()) return caster.CanPrepareSkillContainer(skill, target);
        return SkillCastCost.GetAffordableStepLimit(caster, skill) > 0;
    }

    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        Entity skill = handle.Source;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;
        SkillUseProfileAsset useProfile = skill.GetComponent<SkillContainer>().Asset.UseProfile;
        if (useProfile.Placement == SkillUsePlacement.CasterSelf)
        {
            int selfStepLimit = SkillCastCost.GetAffordableStepLimit(caster, skill);
            SkillCastPlan selfPlan = SkillCastPlanner.CreatePointPlan(
                caster, skill, caster.Base.GetSimPos(), selfStepLimit);
            return SkillCastCost.CanPay(caster, skill, selfPlan);
        }
        if (target.Object != null && !target.Object.isRekt())
        {
            return caster.CanUseSkillContainerAtCurrentDistance(skill, target.Object);
        }

        float range = ResolveRange(caster, handle, null);
        if (Toolbox.SquaredDistVec2Float(caster.Base.current_position, target.Position) > range * range) return false;
        int stepLimit = SkillCastCost.GetAffordableStepLimit(caster, skill);
        SkillCastPlan plan = SkillCastPlanner.CreatePointPlan(caster, skill, target.Position, stepLimit);
        return SkillCastCost.CanPay(caster, skill, plan);
    }

    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillEntityAsset asset = handle.Source.GetComponent<SkillContainer>().Asset;
        SkillUseProfileAsset useProfile = asset.UseProfile;
        int weight = useProfile.BaseAiWeight;
        if (useProfile.ThreatenedAiWeight > 0 &&
            caster.Base.data.health <= caster.Base.stats[strings.S.health] * 0.5f)
        {
            weight += useProfile.ThreatenedAiWeight;
        }
        if (asset.ImpactProfile.IsField && target != null && !target.isRekt())
        {
            int nearbyEnemies = 0;
            foreach (BaseSimObject _ in SkillUtils.IterEnemyInSphere(
                         target.current_position, asset.ImpactProfile.EffectRadius * 2f, caster.Base))
            {
                nearbyEnemies++;
                if (nearbyEnemies >= 3) break;
            }
            weight += nearbyEnemies;
        }
        return weight;
    }

    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        SkillUseProfileAsset profile = handle.Source.GetComponent<SkillContainer>().Asset.UseProfile;
        return caster.GetSkillCastRange(target) * profile.RangeMultiplier;
    }

    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        Entity skill = handle.Source;
        SkillImpactProfileAsset profile = skill.GetComponent<SkillContainer>().Asset.ImpactProfile;
        return SkillEffectRadius.ResolveContainer(skill, profile.EffectRadius);
    }

    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        Entity skill = handle.Source;
        int stepLimit = SkillCastCost.GetAffordableStepLimit(caster, skill);
        SkillCastPlan plan;
        SkillUseProfileAsset useProfile = skill.GetComponent<SkillContainer>().Asset.UseProfile;
        if (useProfile.Placement == SkillUsePlacement.CasterSelf)
        {
            plan = SkillCastPlanner.CreatePointPlan(caster, skill, caster.Base.GetSimPos(), stepLimit);
        }
        else if (target.Object != null && !target.Object.isRekt())
        {
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
            plan = SkillCastPlanner.CreatePointPlan(caster, skill, target.Position, stepLimit);
        }
        if (plan.Steps.Count == 0) return false;

        return ModClass.I.SkillV3.StartSkillSequence(
            caster,
            skill,
            plan,
            SkillContext.DefaultStrength,
            caster.GetPowerLevel(),
            SkillCastFundingSource.CasterResources,
            target.AttackKingdom);
    }
}

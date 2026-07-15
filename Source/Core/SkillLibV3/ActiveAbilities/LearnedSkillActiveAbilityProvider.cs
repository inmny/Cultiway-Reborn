using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3.Components;
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
        if (!GeneralSettings.EnableSkillSystems || caster.all_attack_skills == null) return;
        for (int i = 0; i < caster.all_attack_skills.Count; i++)
        {
            Entity skill = caster.all_attack_skills[i];
            if (!skill.IsNull && skill.HasComponent<SkillContainer>())
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
            ActiveAbilityTargetMode.ObjectOrPoint,
            ActiveAbilityActivationMode.Instant);
    }

    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        Entity skill = handle.Source;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;
        if (target != null && !target.isRekt()) return caster.CanPrepareSkillContainer(skill, target);
        return SkillCastCost.GetAffordableStepLimit(caster, skill) > 0;
    }

    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        Entity skill = handle.Source;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;
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

    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target) => 1;

    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return caster.GetSkillCastRange(target);
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
        if (target.Object != null && !target.Object.isRekt())
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

using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts.ActiveAbilities;

/// <summary>
/// 将封存 SkillContainer 的一次性物品适配为统一主动能力。具体载体只负责权限、参数和消耗表现。
/// </summary>
internal abstract class ConsumableSkillActiveAbilityProvider : IActiveAbilityProvider
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
        return ActiveAbilityChannel.Combat;
    }

    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        TryResolve(caster, handle, out SkillPayload payload);
        Entity item = handle.Source;
        string name = item.HasName
            ? item.Name.value
            : payload.Skill.GetComponent<SkillContainer>().SkillEntityAssetID.Localize();
        Sprite icon = item.TryGetComponent(out SpecialItem specialItem) ? specialItem.GetSprite() : null;
        return new ActiveAbilityDescriptor(
            name,
            icon,
            ActiveAbilityChannel.Combat,
            ActiveAbilityTargetMode.ObjectOrPoint,
            ActiveAbilityActivationMode.Instant);
    }

    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolve(caster, handle, out SkillPayload payload)) return false;
        if (target != null && target.isRekt()) return false;
        return SkillCastCost.GetAffordableStepLimit(caster, payload.Skill, SkillCastFundingSource.Prepaid) > 0;
    }

    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        return TryResolve(caster, handle, out SkillPayload payload) &&
               TryCreatePlan(caster, payload.Skill, target, out _);
    }

    public abstract int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target);

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

        if (target.Object != null && !target.Object.isRekt())
        {
            float range = caster.GetSkillCastRange(target.Object) + target.Object.stats[strings.S.size];
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
            float range = caster.GetSkillCastRange(null);
            if (Toolbox.SquaredDistVec2Float(caster.Base.current_position, target.Position) > range * range)
                return false;
            plan = SkillCastPlanner.CreatePointPlan(caster, skill, target.Position, stepLimit);
        }

        return plan.Steps.Count > 0 && SkillCastCost.CanPay(caster, skill, plan, SkillCastFundingSource.Prepaid);
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

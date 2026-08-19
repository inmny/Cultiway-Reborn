using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>通过专用执行逻辑向玩家和 AI 公开角色已经学会的骑士战技。</summary>
internal sealed class KnightTechniqueAbilityProvider : IActiveAbilityProvider
{
    internal const string ProviderId = "content.knight_techniques";

    public string Id => ProviderId;

    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!GeneralSettings.EnableSkillSystems) return;
        IReadOnlyList<Entity> learnedSkills = caster.GetLearnedSkillsInOrder();
        for (var i = 0; i < learnedSkills.Count; i++)
        {
            Entity skill = learnedSkills[i];
            if (skill.IsNull || !skill.HasComponent<SkillContainer>() ||
                !skill.HasComponent<SpecializedActiveAbility>()) continue;
            SpecializedActiveAbility specialized = skill.GetComponent<SpecializedActiveAbility>();
            if (specialized.ProviderId != Id) continue;
            KnightTechniqueAsset technique = KnightTechniqueCatalog.Get(specialized.EntryId);
            if (technique.ActiveUse == null) continue;
            output.Add(new ActiveAbilityHandle(Id, skill, technique.id));
        }
    }

    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out _) &&
               KnightTechniqueAccessService.TryResolveCurrentWeapon(caster, technique, out _, out _)
            ? technique.ActiveUse.Channels
            : ActiveAbilityChannel.None;
    }

    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out _);
        KnightTechniqueActiveUseProfile profile = technique.ActiveUse;
        return new ActiveAbilityDescriptor(
            technique.ResolveName(),
            technique.ResolveIcon(),
            profile.Channels,
            profile.TargetMode,
            profile.ActivationMode,
            profile.CastMobility,
            profile.TargetRelation);
    }

    public ActiveAbilityControlState ResolveControlState(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out Entity container) ||
            !KnightTechniqueAccessService.TryResolveCurrentWeapon(caster, technique, out _, out _) ||
            KnightTechniqueRuntimeService.IsBusy(caster) || !KnightTechniqueRuntimeService.CanAct(caster))
        {
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Unavailable);
        }

        float cooldown = SkillCooldownService.GetRemaining(caster, container);
        if (cooldown > 0f)
            return new ActiveAbilityControlState(ActiveAbilityControlBlockReason.Cooldown, cooldown);
        return SkillCastCost.CanPayStep(caster, container)
            ? ActiveAbilityControlState.Ready
            : new ActiveAbilityControlState(ActiveAbilityControlBlockReason.InsufficientResource);
    }

    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out Entity container) ||
            KnightTechniqueRuntimeService.IsBusy(caster) ||
            !KnightTechniqueRuntimeService.CanAct(caster) ||
            !SkillCooldownService.IsReady(caster, container) ||
            !SkillCastCost.CanPayStep(caster, container)) return false;
        var activeTarget = new ActiveAbilityTarget(
            target,
            target?.GetSimPos() ?? caster.Base.GetSimPos());
        if (!TryResolveContext(caster, technique, activeTarget, false, out KnightTechniqueContext context))
            return false;
        return technique.ActiveUse.PrepareCondition?.Invoke(context) ?? true;
    }

    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out Entity container) ||
            KnightTechniqueRuntimeService.IsBusy(caster) ||
            !KnightTechniqueRuntimeService.CanAct(caster) ||
            !SkillCooldownService.IsReady(caster, container) ||
            !SkillCastCost.CanPayStep(caster, container) ||
            !TryResolveContext(caster, technique, target, true, out KnightTechniqueContext context)) return false;
        KnightTechniqueActiveUseProfile profile = technique.ActiveUse;
        return (profile.PrepareCondition?.Invoke(context) ?? true) &&
               (profile.UseCondition?.Invoke(context) ?? true);
    }

    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out _)) return 0;
        var activeTarget = new ActiveAbilityTarget(
            target,
            target?.GetSimPos() ?? caster.Base.GetSimPos());
        if (!TryResolveContext(caster, technique, activeTarget, false, out KnightTechniqueContext context)) return 0;
        KnightTechniqueActiveUseProfile profile = technique.ActiveUse;
        if ((profile.PrepareCondition?.Invoke(context) ?? true) == false ||
            (profile.UseCondition?.Invoke(context) ?? true) == false ||
            profile.ResolveAiWeight == null) return 0;
        return Mathf.Clamp(profile.ResolveAiWeight(context), 0, 10);
    }

    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        if (!TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out _)) return default;
        var activeTarget = new ActiveAbilityTarget(
            target,
            target?.GetSimPos() ?? caster.Base.GetSimPos());
        if (!TryResolveContext(caster, technique, activeTarget, false, out KnightTechniqueContext context))
            return default;
        ActiveAbilityTacticalProfile profile = technique.ActiveUse.ResolveTacticalProfile?.Invoke(context) ?? default;
        float attackDamage = KnightTechniqueAiRules.ResolveAttackDamage(caster);
        float multiplier = technique.ActiveUse.ResolveAiDamageMultiplier?.Invoke(context) ??
                           technique.AiDamageMultiplier;
        float segments = Mathf.Max(1, technique.AiAttackSegments);
        float primaryPower = attackDamage * Mathf.Max(0f, multiplier) * segments;
        float expectedTargets = Mathf.Max(1f, profile.ExpectedTargets);
        if (technique.AiSecondaryDamageMultiplier > 0f)
        {
            expectedTargets = Mathf.Max(expectedTargets, 1f + profile.ExpectedTargets - 1f);
            primaryPower += attackDamage * technique.AiSecondaryDamageMultiplier *
                            Mathf.Max(0f, expectedTargets - 1f);
        }

        float maxVigor = caster.Base.stats[BaseStatses.MaxVigor.id];
        float availableVigor = caster.GetCultisys<Knight>().vigor;
        float resourceCost = maxVigor > 0f ? technique.VigorCost / maxVigor : 1f;
        float availableRatio = maxVigor > 0f ? availableVigor / maxVigor : 0f;
        return new ActiveAbilityTacticalProfile(
            profile.Offensive,
            profile.Defensive,
            profile.Support,
            profile.Control,
            Mathf.Max(profile.Power, primaryPower),
            technique.VigorCost,
            expectedTargets,
            profile.ImpactKind,
            Mathf.Max(profile.Utility, primaryPower * 0.08f),
            resourceCost,
            availableRatio,
            technique.AiIgnoreResourceReserveWhenCritical);
    }

    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out _)) return 0f;
        var activeTarget = new ActiveAbilityTarget(
            target,
            target?.GetSimPos() ?? caster.Base.GetSimPos());
        return TryResolveContext(caster, technique, activeTarget, false, out KnightTechniqueContext context)
            ? Mathf.Max(0f, technique.ActiveUse.ResolveRange?.Invoke(context) ?? 0f)
            : 0f;
    }

    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        if (!TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out _) ||
            !TryResolveContext(caster, technique, default, false, out KnightTechniqueContext context)) return 0f;
        return Mathf.Max(0f, technique.ActiveUse.ResolveEffectRadius?.Invoke(context) ?? 0f);
    }

    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target) ||
            !TryResolveHandle(caster, handle, out KnightTechniqueAsset technique, out Entity container) ||
            !TryResolveContext(caster, technique, target, true, out KnightTechniqueContext context) ||
            !SkillCastCost.TryPayStep(caster, container)) return false;
        if (!technique.ActiveUse.TryUse(context, origin)) return false;
        SkillCooldownService.Start(caster, container, technique.Cooldown);
        return true;
    }

    private bool TryResolveHandle(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        out KnightTechniqueAsset technique,
        out Entity container)
    {
        technique = null;
        container = default;
        if (handle.ProviderId != Id || handle.Source.IsNull ||
            !caster.OwnsLearnedSkill(handle.Source) ||
            !handle.Source.HasComponent<SkillContainer>() ||
            !handle.Source.HasComponent<SpecializedActiveAbility>()) return false;
        SpecializedActiveAbility specialized = handle.Source.GetComponent<SpecializedActiveAbility>();
        if (specialized.ProviderId != Id || specialized.EntryId != handle.EntryId) return false;
        container = handle.Source;
        technique = KnightTechniqueCatalog.Get(handle.EntryId);
        return technique.ActiveUse != null;
    }

    private static bool TryResolveContext(
        ActorExtend caster,
        KnightTechniqueAsset technique,
        in ActiveAbilityTarget requestedTarget,
        bool requireConcreteTarget,
        out KnightTechniqueContext context)
    {
        context = default;
        if (!KnightTechniqueAccessService.TryResolveCurrentWeapon(
                caster, technique, out Item weapon, out EquipmentAsset weaponAsset)) return false;
        KnightTechniqueActiveUseProfile profile = technique.ActiveUse;
        BaseSimObject target = profile.TargetMode == ActiveAbilityTargetMode.Self
            ? caster.Base
            : requestedTarget.Object;
        if (requireConcreteTarget && target == null) return false;
        if (target != null && target != caster.Base &&
            !SkillTargetRelationResolver.Matches(
                profile.TargetRelation,
                caster.Base,
                target,
                requestedTarget.AttackKingdom)) return false;
        if (target?.isActor() == true && target != caster.Base && target.a.isFlying()) return false;
        Vector3 position = target?.GetSimPos() ?? requestedTarget.Position;
        var normalized = new ActiveAbilityTarget(
            target,
            position,
            requestedTarget.SelectionArea,
            requestedTarget.ExplicitTargets,
            requestedTarget.AttackKingdom,
            requestedTarget.RuntimeData);
        return KnightTechniqueAccessService.TryCreateContext(
            caster,
            technique,
            weapon,
            weaponAsset,
            target,
            normalized,
            out context);
    }

}

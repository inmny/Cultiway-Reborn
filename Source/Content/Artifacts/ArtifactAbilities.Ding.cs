using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Usage;
using NeoModLoader.General;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>可部署火炼法域；周期性造成火行伤害并施加燃烧。</summary>
    public static ArtifactAbilityAsset DingFireRefinement { get; private set; }

    /// <summary>可部署吞噬法域；伤害、抽灵并牵引敌人，将所得转化为生命和部分灵气。</summary>
    public static ArtifactAbilityAsset DevouringReturn { get; private set; }

    /// <summary>炼器生产被动；增加工序进度、缩短耗时，并提高法器品质与产量。</summary>
    public static ArtifactAbilityAsset HundredRefinementCore { get; private set; }

    private static void ConfigureDingFireRefinement()
    {
        DingFireRefinement.name_key = "Cultiway.ArtifactAbility.DingFireRefinement";
        DingFireRefinement.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Delivery.Field,
            ArtifactSemantics.Element.Fire,
            ArtifactSemantics.Effect.Transformation);
        DingFireRefinement.exclusivity = ArtifactAbilityExclusivity.RefiningFlameField;
        DingFireRefinement.manifestation_cost = 1.35f;
        DingFireRefinement.AddSynergies(ArtifactSemantics.Craft.Alchemy, ArtifactSemantics.Effect.Amplification);
        DingFireRefinement.AddConflicts(ArtifactSemanticRules.ColdField);
        DingFireRefinement.minimum_score = 1f;
        DingFireRefinement.use_profile = new ArtifactUseProfile { offensive = 0.85f, production = 0.2f };
        DingFireRefinement.control_complexity = 0.46f;
        DingFireRefinement.thread_cost = 1;
        DingFireRefinement.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(EffectRadius),
            NumberSpec(EffectDuration),
            NumberSpec(DamageMultiplier),
            NumberSpec(StatusStrength),
            NumberSpec(StatusDuration),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        DingFireRefinement.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Alchemy) +
             context.GetTrait(ArtifactMaterialTraits.Transformation) * 0.65f) *
            (0.5f + context.GetTrait(ArtifactMaterialTraits.Fire) * 0.5f +
             context.GetTrait(ArtifactMaterialTraits.Amplification) * 0.22f);
        DingFireRefinement.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7.5f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 2.7f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 5f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.15f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusStrength, 0.08f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 2.5f * context.scales.Duration),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 13f, 4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.42f)),
        ];
        DingFireRefinement.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.DingFireRefinement.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(DamageMultiplier),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(EffectDuration));
        DingFireRefinement.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.5f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = SkillCastResources.Wakan,
            OnTick = ApplyDingFireRefinement,
            OnActivityEnded = EndArtifactConstruction,
        });
        DingFireRefinement.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployDingFireRefinement,
        });
    }

    private static bool DeployDingFireRefinement(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        return DeployAtTarget(
            context,
            ability,
            ref runtime,
            target,
            ArtifactBodyAnchorRef.Appearance("core", "center"));
    }

    private static void ApplyDingFireRefinement(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float interval)
    {
        float burn = SkillContext.DefaultStrength * ability.GetNumber(StatusStrength);
        float duration = ability.GetNumber(StatusDuration);
        var caster = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, target) =>
        {
            using var scope = SkillCasterContextService.Enter(in caster);
            CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Burn, duration, burn,
                ElementComposition.Static.Fire, source);
        }, damageFirst: false);
        StartPaidArtifactArea(context, DingFireRefinement, ability, interval, "ding_fire_rim", contact,
            ElementComposition.Static.Fire, SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier));
    }

    private static void ConfigureDevouringReturn()
    {
        DevouringReturn.name_key = "Cultiway.ArtifactAbility.DevouringReturn";
        DevouringReturn.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Delivery.Field,
            ArtifactSemantics.Element.Neg,
            ArtifactSemantics.Effect.Devouring,
            ArtifactSemantics.Effect.Recovery);
        DevouringReturn.exclusivity = ArtifactAbilityExclusivity.DevouringField;
        DevouringReturn.manifestation_cost = 1.5f;
        DevouringReturn.AddSynergies(ArtifactSemantics.Effect.Storage, ArtifactSemantics.Effect.Recovery);
        DevouringReturn.AddConflicts(ArtifactSemanticRules.Purification);
        DevouringReturn.minimum_score = 1f;
        DevouringReturn.use_profile = new ArtifactUseProfile { offensive = 0.7f, defensive = 0.3f, cultivate = 0.25f };
        DevouringReturn.control_complexity = 0.56f;
        DevouringReturn.thread_cost = 2;
        DevouringReturn.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(EffectRadius),
            NumberSpec(EffectDuration),
            NumberSpec(DamageMultiplier),
            NumberSpec(DrainAmount),
            NumberSpec(RestoreRatio),
            NumberSpec(ForceStrength),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        DevouringReturn.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Devouring) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Capacity) * 0.35f +
             context.GetTrait(ArtifactMaterialTraits.Storage) * 0.4f +
             context.GetTrait(ArtifactMaterialTraits.Neg) * 0.18f);
        DevouringReturn.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 3f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 5f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.12f * context.scales.Potency),
            ArtifactAbilityValue.Number(DrainAmount, 2f * context.scales.Potency),
            ArtifactAbilityValue.Number(RestoreRatio, Mathf.Clamp(0.3f * context.scales.Efficiency, 0.3f, 0.8f)),
            ArtifactAbilityValue.Number(ForceStrength, 0.38f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 15f, 4.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4.8f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.55f)),
        ];
        DevouringReturn.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.DevouringReturn.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(DamageMultiplier),
            ability.GetNumber(DrainAmount),
            ability.GetNumber(EffectDuration));
        DevouringReturn.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.4f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = SkillCastResources.Wakan,
            OnTick = ApplyDevouringReturn,
            OnActivityEnded = EndArtifactConstruction,
        });
        DevouringReturn.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 6,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployDevouringReturn,
        });
    }

    private static bool DeployDevouringReturn(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        return DeployAtTarget(
            context,
            ability,
            ref runtime,
            target,
            ArtifactBodyAnchorRef.Appearance("vessel", "center"));
    }

    private static void ApplyDevouringReturn(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float interval)
    {
        float damage = SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier);
        float fraction = ability.GetNumber(RestoreRatio);
        float recovery = damage * fraction * context.effect_scale;
        float drain = ability.GetNumber(DrainAmount) * context.effect_scale;
        float force = ability.GetNumber(ForceStrength) * context.effect_scale;
        var caster = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        Actor controller = Controller(context);
        float totalRecovery = 0f, totalWakan = 0f;
        var contact = new ArtifactAreaContact((_, target) =>
        {
            totalRecovery += recovery;
            totalWakan += CombatResourceEffects.DrainWakan(target, drain);
        }, damageFirst: false, force: -force, afterPulse: () =>
        {
            using var scope = SkillCasterContextService.Enter(in caster);
            RestoreCarrierHealth(context, totalRecovery);
            CombatResourceEffects.RestoreWakan(controller, totalWakan * fraction);
        });
        StartPaidArtifactArea(context, DevouringReturn, ability, interval, "ding_devouring_funnels", contact,
            SoulComposition, damage, spin: -62f);
    }

    private static void ConfigureHundredRefinementCore()
    {
        HundredRefinementCore.name_key = "Cultiway.ArtifactAbility.HundredRefinementCore";
        HundredRefinementCore.SetSemantics(ArtifactSemantics.Craft.Refinement, ArtifactSemantics.Effect.Transformation);
        HundredRefinementCore.exclusivity = ArtifactAbilityExclusivity.GeneralRefinementAssist;
        HundredRefinementCore.manifestation_cost = 0.9f;
        HundredRefinementCore.AddSynergies(ArtifactSemantics.Role.Production, ArtifactSemantics.Material.Stability);
        HundredRefinementCore.minimum_score = 1f;
        HundredRefinementCore.use_profile = new ArtifactUseProfile { production = 1f };
        HundredRefinementCore.control_complexity = 0.22f;
        HundredRefinementCore.parameter_schema =
        [
            IntegerSpec(ProgressBonus),
            NumberSpec(DurationMultiplier),
            IntegerSpec(QualityBonus),
            NumberSpec(YieldMultiplier),
        ];
        HundredRefinementCore.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Alchemy) +
             context.GetTrait(ArtifactMaterialTraits.Transformation) * 0.6f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.35f) *
            (0.6f + context.GetTrait(ArtifactMaterialTraits.Stability) * 0.55f);
        HundredRefinementCore.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Integer(ProgressBonus, Mathf.Clamp(Mathf.FloorToInt(context.scales.Potency), 1, 8)),
            ArtifactAbilityValue.Number(DurationMultiplier, Mathf.Clamp(1f / context.scales.Efficiency, 0.35f, 0.95f)),
            ArtifactAbilityValue.Integer(QualityBonus,
                Mathf.Clamp(Mathf.FloorToInt(context.scales.Precision * 0.65f), 1, 6)),
            ArtifactAbilityValue.Number(YieldMultiplier,
                1f + Mathf.Clamp((context.scales.Capacity - 1f) * 0.08f, 0.02f, 0.45f)),
        ];
        HundredRefinementCore.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.HundredRefinementCore.Description"),
            ability.GetInteger(ProgressBonus),
            ability.GetNumber(DurationMultiplier),
            ability.GetInteger(QualityBonus),
            ability.GetNumber(YieldMultiplier));
        HundredRefinementCore.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Operating,
        });
        HundredRefinementCore.Handle<ArtifactProductionStepEvent>(ApplyHundredRefinementStep);
        HundredRefinementCore.Handle<ArtifactProductionResultEvent>(ApplyHundredRefinementResult);
    }

    private static void ApplyHundredRefinementStep(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactProductionStepEvent evt)
    {
        evt.ProgressGain += ability.GetInteger(ProgressBonus);
        evt.Duration *= ability.GetNumber(DurationMultiplier);
        evt.Feedback.Record(context, ability, runtime, ability.GetInteger(ProgressBonus));
    }

    private static void ApplyHundredRefinementResult(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactProductionResultEvent evt)
    {
        evt.QualityBonus += ability.GetInteger(QualityBonus);
        evt.YieldMultiplier *= ability.GetNumber(YieldMultiplier);
        evt.Feedback.Record(context, ability, runtime, ability.GetInteger(QualityBonus));
    }

    private static void ConfigureDingAbilityVisuals()
    {
        ConfigureDingFireRefinementVisuals();
        HundredRefinementCore.Visualize(ProductionVisuals());
    }

    private static void ConfigureDingFireRefinementVisuals()
    {
        var flame = new ArtifactLocalVisualCue(ArtifactLocalVisualKind.Flame)
        {
            anchor = ArtifactVisualAnchorRef.Appearance("core", "center"),
            color_role = ArtifactVisualColorRole.Glow,
            scale = .075f,
            alpha = .86f,
            loop = true,
        };
        DingFireRefinement.Visualize(Theme(SkillVfxElements.Fire.AccentColor).Loop(
            "ding_fire", flame, IsDeploymentActive));
    }

}

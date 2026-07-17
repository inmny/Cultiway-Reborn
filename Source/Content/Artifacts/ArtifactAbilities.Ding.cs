using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
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
            Resource = UseWakan,
            OnTick = ApplyDingFireRefinement,
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
        float __)
    {
        Actor controller = Controller(context);
        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector2 position = ArtifactManifestationTools.ResolveWorldAnchor(
            context.artifact,
            deployment.ResolveBodyAnchor());
        ArtifactTargeting.ForEachHostile(controller, position, ability.GetNumber(EffectRadius), target =>
        {
            ArtifactDamageEffects.DealDamage(
                controller,
                target,
                SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
                ElementComposition.Static.Fire);
            ArtifactStatusEffects.ApplyTickingStatus(
                target,
                StatusEffects.Burn,
                ability.GetNumber(StatusDuration),
                SkillContext.DefaultStrength * ability.GetNumber(StatusStrength),
                ElementComposition.Static.Fire,
                controller);
        });
    }

    private static void ConfigureDevouringReturn()
    {
        DevouringReturn.name_key = "Cultiway.ArtifactAbility.DevouringReturn";
        DevouringReturn.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Delivery.Field,
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
            Resource = UseWakan,
            OnTick = ApplyDevouringReturn,
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
        float __)
    {
        Actor controller = Controller(context);
        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector2 position = ArtifactManifestationTools.ResolveWorldAnchor(
            context.artifact,
            deployment.ResolveBodyAnchor());
        float totalRecovery = 0f;
        float totalWakan = 0f;
        ArtifactTargeting.ForEachHostile(controller, position, ability.GetNumber(EffectRadius), target =>
        {
            float damage = SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier);
            ArtifactDamageEffects.DealDamage(controller, target, damage, SoulComposition);
            totalRecovery += damage * ability.GetNumber(RestoreRatio);
            totalWakan += ArtifactResourceEffects.DrainWakan(target, ability.GetNumber(DrainAmount));
            ArtifactForceEffects.ApplyRadialForce(
                controller,
                target,
                position,
                ability.GetNumber(ForceStrength),
                pull: true);
        });
        ArtifactResourceEffects.RestoreHealth(controller, totalRecovery);
        ArtifactResourceEffects.RestoreWakan(controller, totalWakan * ability.GetNumber(RestoreRatio));
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
            ArtifactAbilityValue.Integer(QualityBonus, Mathf.Clamp(Mathf.FloorToInt(context.scales.Precision * 0.65f), 1, 6)),
            ArtifactAbilityValue.Number(YieldMultiplier, 1f + Mathf.Clamp((context.scales.Capacity - 1f) * 0.08f, 0.02f, 0.45f)),
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
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.CraftStep,
            intensity: ability.GetInteger(ProgressBonus),
            duration: evt.Duration + 0.2f);
    }

    private static void ApplyHundredRefinementResult(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactProductionResultEvent evt)
    {
        evt.QualityBonus += ability.GetInteger(QualityBonus);
        evt.YieldMultiplier *= ability.GetNumber(YieldMultiplier);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.CraftResult,
            intensity: ability.GetInteger(QualityBonus));
    }

    private static void ConfigureDingAbilityVisuals()
    {
        ConfigureDingFireRefinementVisuals();
        ConfigureDevouringReturnVisuals();
        ConfigureHundredRefinementCoreVisuals();
    }

    private static void ConfigureDingFireRefinementVisuals()
    {
        ArtifactAreaVisualCue fireField = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.48f,
            0.08f,
            ArtifactVfxStyles.Fire
        );
        fireField.pulse_speed = 4.5f;
        ArtifactAnimVisualCue flame = new ArtifactAnimVisualCue("effects/fx_status_burning_t_3")
        {
            anchor = ArtifactVisualAnchorRef.Appearance("core", "center"),
            color_role = ArtifactVisualColorRole.Glow,
            scale = 0.075f,
            frame_interval = 0.08f,
            alpha = 0.86f,
            loop = true,
        };
        ArtifactParticleVisualCue embers = Burst(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            ArtifactVisualColorRole.Primary,
            3,
            0.4f
        );
        embers.emission_interval = 0.14f;
        DingFireRefinement.Visualize(
            Theme(SkillVfxElements.Fire.AccentColor)
                .Loop(
                    "ding_fire",
                    new ArtifactCompositeVisualCue(fireField, flame, embers),
                    IsDeploymentActive
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    Burst(
                        ArtifactVisualAnchorKind.DeploymentOrigin,
                        ArtifactVisualColorRole.Glow,
                        4,
                        0.45f
                    ),
                    0.3f,
                    "artifact.ding_fire.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureDevouringReturnVisuals()
    {
        ArtifactGlyphVisualCue devouringGlyph = Glyph(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Secondary,
            ArtifactVfxStyles.Devouring
        );
        devouringGlyph.rotation_speed = -32f;
        devouringGlyph.counter_rotation_ratio = 0.7f;
        ArtifactAreaVisualCue devouringField = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.42f,
            0.07f,
            ArtifactVfxStyles.Devouring
        );
        devouringField.inner_rotation_speed = 38f;
        ArtifactParticleVisualCue inward = Burst(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            ArtifactVisualColorRole.Glow,
            3,
            0.5f
        );
        inward.emission_interval = 0.12f;
        inward.directional_speed = -0.2f;
        DevouringReturn.Visualize(
            Theme(SkillVfxElements.Entropy.AccentColor)
                .Loop(
                    "devouring_return",
                    new ArtifactCompositeVisualCue(
                        devouringField,
                        devouringGlyph,
                        inward,
                        Pulse(1f, 1f, 0.09f)
                    ),
                    IsDeploymentActive
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    Burst(
                        ArtifactVisualAnchorKind.DeploymentOrigin,
                        ArtifactVisualColorRole.Primary,
                        3
                    ),
                    0.25f,
                    "artifact.devouring.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureHundredRefinementCoreVisuals()
    {
        ArtifactAnimVisualCue refinementFlame = new ArtifactAnimVisualCue(
            "effects/fx_status_burning_t_3"
        )
        {
            anchor = ArtifactVisualAnchorRef.Appearance("core", "center"),
            color_role = ArtifactVisualColorRole.Primary,
            scale = 0.055f,
            frame_interval = 0.1f,
            alpha = 0.7f,
            loop = true,
        };
        ArtifactAreaVisualCue result = Area(
            ArtifactVisualAnchorKind.Artifact,
            context => 0.75f * ArtifactAbilityVisuals.ResolveActorScale(context),
            ArtifactVisualColorRole.Glow,
            0.7f,
            0.04f,
            ArtifactVfxStyles.Fire
        );
        result.start_scale = 0.15f;
        result.end_scale = 1.2f;
        result.fade_out = true;
        HundredRefinementCore.Visualize(
            Theme(SkillVfxElements.Fire.AccentColor)
                .Signal(
                    ArtifactVisualChannels.CraftStep,
                    refinementFlame,
                    0.8f,
                    "artifact.hundred_refinement.step",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
                .Signal(
                    ArtifactVisualChannels.CraftResult,
                    new ArtifactCompositeVisualCue(
                        result,
                        Sparkle(ArtifactVisualAnchorKind.Artifact, 0.08f, 1f, loop: false),
                        Pulse(1.25f, 1f, 0.16f)
                    ),
                    0.6f,
                    "artifact.hundred_refinement.result",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }
}

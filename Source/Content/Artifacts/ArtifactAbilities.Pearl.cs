using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
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
    private const string PulsesRemaining = "pulses_remaining";
    private const string ResonanceRestore = "resonance_restore";

    /// <summary>充能护体被动；维持可自动恢复的护盾池，吸收持有者受到的伤害。</summary>
    public static ArtifactAbilityAsset CelestialPearlGuard { get; private set; }
    /// <summary>持续脉冲主动；在活动期间多次对目标区域造成范围元素伤害。</summary>
    public static ArtifactAbilityAsset FivePhasePearlStrike { get; private set; }
    /// <summary>施法共鸣被动；角色施法后按实际发射数量恢复灵气，并受计数上限与冷却限制。</summary>
    public static ArtifactAbilityAsset LinkedPearlResonance { get; private set; }

    private static void ConfigureCelestialPearlGuard()
    {
        CelestialPearlGuard.name_key = "Cultiway.ArtifactAbility.CelestialPearlGuard";
        CelestialPearlGuard.SetSemantics(
            ArtifactSemantics.Effect.Shield,
            ArtifactSemantics.Motion.Orbit,
            ArtifactSemantics.Effect.Resonance);
        CelestialPearlGuard.exclusivity = ArtifactAbilityExclusivity.RechargingShield;
        CelestialPearlGuard.manifestation_cost = 1.05f;
        CelestialPearlGuard.AddSynergies(
            ArtifactSemantics.Effect.Ward,
            ArtifactSemantics.Effect.Resonance,
            ArtifactSemantics.Form.Sustain);
        CelestialPearlGuard.AddConflicts(ArtifactSemanticRules.BodyDeployment);
        CelestialPearlGuard.minimum_score = 1f;
        CelestialPearlGuard.use_profile = new ArtifactUseProfile { defensive = 1f, support = 0.2f };
        CelestialPearlGuard.control_complexity = 0.28f;
        CelestialPearlGuard.parameter_schema = [NumberSpec(ShieldCapacity), NumberSpec(ShieldRegen)];
        CelestialPearlGuard.state_schema =
        [
            new ArtifactAbilityValueSpec
            {
                key = ShieldCurrent,
                kind = ArtifactAbilityValueKind.Number,
                required = true,
            },
        ];
        CelestialPearlGuard.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Ward) +
             context.GetTrait(ArtifactMaterialTraits.Resonance) * 0.55f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.3f +
             context.GetTrait(ArtifactMaterialTraits.Mobility) * 0.2f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.14f);
        CelestialPearlGuard.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(ShieldCapacity, 22f * context.scales.Capacity * context.scales.Potency),
            ArtifactAbilityValue.Number(ShieldRegen, 2.6f * context.scales.Efficiency * context.scales.Duration),
        ];
        CelestialPearlGuard.ComposeInitialState = context =>
        [
            ArtifactAbilityValue.Number(
                ShieldCurrent,
                22f * context.scales.Capacity * context.scales.Potency),
        ];
        CelestialPearlGuard.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.CelestialPearlGuard.Description"),
            ability.GetNumber(ShieldCapacity), ability.GetNumber(ShieldRegen));
        CelestialPearlGuard.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.5f,
            OnAttached = ClampCelestialPearlGuard,
            CanTick = (_, ability, runtime) =>
                runtime.GetNumber(ShieldCurrent) < ability.GetNumber(ShieldCapacity),
            OnTick = RechargeCelestialPearlGuard,
        });
        CelestialPearlGuard.Handle<ArtifactIncomingDamageEvent>(
            (_, _, runtime, evt) => evt.Damage > 0f && runtime.GetNumber(ShieldCurrent) > 0f,
            AbsorbWithCelestialPearlGuard);
    }

    private static void ClampCelestialPearlGuard(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime)
    {
        runtime.SetNumber(
            ShieldCurrent,
            Mathf.Clamp(runtime.GetNumber(ShieldCurrent), 0f, ability.GetNumber(ShieldCapacity)));
    }

    private static void RechargeCelestialPearlGuard(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        float elapsed)
    {
        runtime.SetNumber(
            ShieldCurrent,
            Mathf.Min(
                ability.GetNumber(ShieldCapacity),
                runtime.GetNumber(ShieldCurrent) + ability.GetNumber(ShieldRegen) * elapsed));
    }

    private static void AbsorbWithCelestialPearlGuard(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        float shield = runtime.GetNumber(ShieldCurrent);
        float absorbed = ArtifactDamageEffects.AbsorbDamage(evt, ref shield);
        runtime.SetNumber(ShieldCurrent, shield);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Guard,
            target: evt.Attacker,
            intensity: absorbed / Mathf.Max(1f, ability.GetNumber(ShieldCapacity)));
    }

    private static void ConfigureFivePhasePearlStrike()
    {
        FivePhasePearlStrike.name_key = "Cultiway.ArtifactAbility.FivePhasePearlStrike";
        FivePhasePearlStrike.SetSemantics(
            ArtifactSemantics.Delivery.Projection,
            ArtifactSemantics.Theme.Elemental,
            ArtifactSemantics.Effect.MultiHit);
        FivePhasePearlStrike.exclusivity = ArtifactAbilityExclusivity.ElementalBarrage;
        FivePhasePearlStrike.manifestation_cost = 1.25f;
        FivePhasePearlStrike.AddSynergies(
            ArtifactSemantics.Effect.Resonance,
            ArtifactSemantics.Delivery.Projection,
            ArtifactSemantics.Theme.Elemental);
        FivePhasePearlStrike.AddConflicts(ArtifactSemanticRules.SingleHeavyStrike);
        FivePhasePearlStrike.minimum_score = 1f;
        FivePhasePearlStrike.use_profile = new ArtifactUseProfile { offensive = 0.95f };
        FivePhasePearlStrike.control_complexity = 0.42f;
        FivePhasePearlStrike.thread_cost = 1;
        FivePhasePearlStrike.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectRadius), NumberSpec(DamageMultiplier),
            IntegerSpec(PulseCount), NumberSpec(EffectDuration), NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        FivePhasePearlStrike.state_schema =
        [
            new ArtifactAbilityValueSpec
            {
                key = PulsesRemaining,
                kind = ArtifactAbilityValueKind.Integer,
                required = true,
            },
        ];
        FivePhasePearlStrike.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Projection) +
             context.GetTrait(ArtifactMaterialTraits.Resonance) * 0.52f) *
            (0.52f + context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.24f +
             context.GetTrait(ArtifactMaterialTraits.Volatility) * 0.22f);
        FivePhasePearlStrike.ComposeParameters = context =>
        {
            int pulses = Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity) + 2, 3, 8);
            return
            [
                ArtifactAbilityValue.Number(AttackRange, 9f * context.scales.Range),
                ArtifactAbilityValue.Number(EffectRadius, 1.35f * context.scales.Range),
                ArtifactAbilityValue.Number(DamageMultiplier, 0.32f * context.scales.Potency),
                ArtifactAbilityValue.Integer(PulseCount, pulses),
                ArtifactAbilityValue.Number(EffectDuration, pulses * 0.22f + 0.15f),
                ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 9.5f, 2.8f)),
                ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3.5f)),
            ];
        };
        FivePhasePearlStrike.ComposeInitialState = _ =>
            [ArtifactAbilityValue.Integer(PulsesRemaining, 0)];
        FivePhasePearlStrike.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.FivePhasePearlStrike.Description"),
            ability.GetNumber(AttackRange), ability.GetInteger(PulseCount),
            ability.GetNumber(DamageMultiplier), ability.GetNumber(EffectRadius));
        FivePhasePearlStrike.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.22f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
            CanTick = (_, _, runtime) => runtime.GetInteger(PulsesRemaining) > 0,
            OnTick = PulseFivePhasePearlStrike,
            OnActivityEnded = EndFivePhasePearlStrike,
        });
        FivePhasePearlStrike.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.ObjectOrPoint,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 8,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanUse = CanUseWorldTargetInRange,
            TryUse = BeginFivePhasePearlStrike,
        });
    }

    private static bool BeginFivePhasePearlStrike(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Vector3 point = TargetPosition(target);
        runtime.SetInteger(PulsesRemaining, ability.GetInteger(PulseCount));
        ArtifactAbilityLifecycle.BeginTimedActivity(
            ref runtime,
            point,
            point - (Vector3)Controller(context).current_position);
        return true;
    }

    private static void PulseFivePhasePearlStrike(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        float _)
    {
        Actor controller = Controller(context);
        ArtifactDamageEffects.DealAreaDamage(
            controller,
            runtime.activity_position,
            ability.GetNumber(EffectRadius),
            SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
            MaterialComposition(context));
        runtime.SetInteger(PulsesRemaining, runtime.GetInteger(PulsesRemaining) - 1);
    }

    private static void EndFivePhasePearlStrike(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance __,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactAbilityEndReason ___)
    {
        runtime.SetInteger(PulsesRemaining, 0);
    }

    private static void ConfigureLinkedPearlResonance()
    {
        LinkedPearlResonance.name_key = "Cultiway.ArtifactAbility.LinkedPearlResonance";
        LinkedPearlResonance.SetSemantics(ArtifactSemantics.Effect.Resonance, ArtifactSemantics.Resource.Reserve);
        LinkedPearlResonance.exclusivity = ArtifactAbilityExclusivity.SkillCastResonance;
        LinkedPearlResonance.manifestation_cost = 0.9f;
        LinkedPearlResonance.AddSynergies(
            ArtifactSemantics.Effect.Resonance,
            ArtifactSemantics.Resource.Spirituality,
            ArtifactSemantics.Form.Sustain);
        LinkedPearlResonance.minimum_score = 1f;
        LinkedPearlResonance.use_profile = new ArtifactUseProfile { support = 0.65f, cultivate = 0.55f };
        LinkedPearlResonance.control_complexity = 0.2f;
        LinkedPearlResonance.parameter_schema =
        [
            NumberSpec(ResonanceRestore), IntegerSpec(EffectCount), NumberSpec(Cooldown),
        ];
        LinkedPearlResonance.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Resonance) *
            (0.6f + context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.32f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.2f +
             context.GetTrait(ArtifactMaterialTraits.Capacity) * 0.12f);
        LinkedPearlResonance.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(ResonanceRestore, 1.6f * context.scales.Efficiency),
            ArtifactAbilityValue.Integer(EffectCount, Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity) + 1, 2, 8)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 2.4f, 0.7f)),
        ];
        LinkedPearlResonance.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.LinkedPearlResonance.Description"),
            ability.GetNumber(ResonanceRestore), ability.GetInteger(EffectCount),
            ability.GetNumber(Cooldown));
        LinkedPearlResonance.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            event_consumes_trigger = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
        });
        LinkedPearlResonance.Handle<ArtifactSkillCastEvent>(
            (_, _, _, evt) => evt.EmittedCount > 0,
            ApplyLinkedPearlResonance);
    }

    private static void ApplyLinkedPearlResonance(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactSkillCastEvent evt)
    {
        int counted = Mathf.Min(evt.EmittedCount, ability.GetInteger(EffectCount));
        float restored = ArtifactResourceEffects.RestoreWakan(
            Controller(context),
            ability.GetNumber(ResonanceRestore) * Mathf.Sqrt(counted));
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Trigger,
            intensity: Mathf.Max(0.25f, restored));
    }

    private static void ConfigurePearlAbilityVisuals()
    {
        ConfigureCelestialPearlGuardVisuals();
        ConfigureFivePhasePearlStrikeVisuals();
        ConfigureLinkedPearlResonanceVisuals();
    }

    private static void ConfigureCelestialPearlGuardVisuals()
    {
        ArtifactOrbitProjectionVisualCue pearls = new ArtifactOrbitProjectionVisualCue
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            ResolveCount = context =>
                Mathf.Clamp(
                    Mathf.CeilToInt(
                        3f
                            * context.runtime.GetNumber(ShieldCurrent)
                            / Mathf.Max(1f, context.ability.GetNumber(ShieldCapacity))
                    ),
                    1,
                    3
                ),
            radius = 0.72f,
            vertical_ratio = 0.42f,
            angular_speed = 104f,
            rotation_offset = 0f,
            alpha = 0.72f,
            pulse_amplitude = 0.07f,
            radial_facing = false,
        };
        ArtifactAreaVisualCue shield = Area(
            ArtifactVisualAnchorKind.Controller,
            context => 0.68f * ArtifactAbilityVisuals.ResolveActorScale(context),
            ArtifactVisualColorRole.Glow,
            0.28f,
            0.018f,
            ArtifactVfxStyles.Pearl
        );
        CelestialPearlGuard.Visualize(
            Theme(SkillVfxElements.Water.AccentColor)
                .Loop(
                    "celestial_pearl_guard",
                    new ArtifactCompositeVisualCue(pearls, shield),
                    context =>
                        context.runtime.GetNumber(ShieldCurrent) > 0f
                        && ArtifactAbilityLifecycle.MeetsState(
                            context.control_state,
                            ArtifactControlState.Ready
                        ),
                    "artifact.celestial_pearl_guard",
                    ArtifactVisualStackPolicy.Strongest,
                    context =>
                        context.runtime.GetNumber(ShieldCurrent)
                        / Mathf.Max(1f, context.ability.GetNumber(ShieldCapacity))
                )
                .Signal(
                    ArtifactVisualChannels.Guard,
                    new ArtifactCompositeVisualCue(
                        ExpandingArea(
                            ArtifactVisualAnchorKind.Controller,
                            (ArtifactAbilityVisualContext _) => 0.8f,
                            ArtifactVisualColorRole.Glow,
                            ArtifactVfxStyles.Pearl
                        ),
                        Burst(
                            ArtifactVisualAnchorKind.Controller,
                            ArtifactVisualColorRole.Primary,
                            6
                        )
                    ),
                    0.38f,
                    "artifact.celestial_pearl_guard.hit",
                    ArtifactVisualStackPolicy.Strongest
                )
        );
    }

    private static void ConfigureFivePhasePearlStrikeVisuals()
    {
        ArtifactOrbitProjectionVisualCue barrage = new ArtifactOrbitProjectionVisualCue
        {
            anchor = ArtifactVisualAnchorKind.Point,
            ResolveCount = context => context.runtime.GetInteger(PulsesRemaining),
            radius = 0.46f,
            vertical_ratio = 0.62f,
            angular_speed = 150f,
            rotation_offset = 0f,
            alpha = 0.7f,
            pulse_amplitude = 0.1f,
            radial_facing = false,
            match_actor_scale = false,
        };
        ArtifactGlyphVisualCue targetGlyph = ActivityGlyph(
            ArtifactVisualAnchorKind.Point,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Secondary,
            5,
            42f,
            ArtifactVfxStyles.Pearl
        );
        FivePhasePearlStrike.Visualize(
            Theme(SkillVfxElements.Pos.AccentColor)
                .Loop(
                    "five_phase_pearl_strike",
                    new ArtifactCompositeVisualCue(barrage, targetGlyph),
                    context =>
                        IsActivityActive(context) && context.runtime.GetInteger(PulsesRemaining) > 0
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    new ArtifactCompositeVisualCue(
                        ExpandingArea(
                            ArtifactVisualAnchorKind.Point,
                            context => context.ability.GetNumber(EffectRadius),
                            ArtifactVisualColorRole.Primary,
                            ArtifactVfxStyles.Pearl
                        ),
                        Burst(ArtifactVisualAnchorKind.Point, ArtifactVisualColorRole.Glow, 8, 0.4f)
                    ),
                    0.3f,
                    "artifact.five_phase_pearl.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureLinkedPearlResonanceVisuals()
    {
        ArtifactOrbitProjectionVisualCue resonance = new ArtifactOrbitProjectionVisualCue
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            count = 5,
            radius = 0.6f,
            vertical_ratio = 0.5f,
            angular_speed = 180f,
            rotation_offset = 0f,
            alpha = 0.62f,
            pulse_amplitude = 0.12f,
            radial_facing = false,
        };
        LinkedPearlResonance.Visualize(
            Theme(SkillVfxElements.Pos.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactCompositeVisualCue(
                        resonance,
                        ExpandingArea(
                            ArtifactVisualAnchorKind.Controller,
                            (ArtifactAbilityVisualContext _) => 1.05f,
                            ArtifactVisualColorRole.Glow,
                            ArtifactVfxStyles.Pearl
                        ),
                        Burst(
                            ArtifactVisualAnchorKind.Controller,
                            ArtifactVisualColorRole.Primary,
                            10
                        ),
                        Pulse(1.2f, 1f, 0.15f)
                    ),
                    0.58f,
                    "artifact.linked_pearl_resonance",
                    ArtifactVisualStackPolicy.SinglePerController
                )
        );
    }
}

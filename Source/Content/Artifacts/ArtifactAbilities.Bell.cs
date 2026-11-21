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
using NeoModLoader.General;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>范围主动能力；以钟声伤害并眩晕周围敌人，同时将其震退。</summary>
    public static ArtifactAbilityAsset SoulShakingChime { get; private set; }

    /// <summary>范围净化主动；清除友军负面状态，并驱散敌人的正面状态。</summary>
    public static ArtifactAbilityAsset PurifyingChime { get; private set; }

    /// <summary>限时护罩主动；展开金钟护盾池，在持续期间吸收持有者受到的伤害。</summary>
    public static ArtifactAbilityAsset GoldenBellBarrier { get; private set; }

    private static void ConfigureSoulShakingChime()
    {
        SoulShakingChime.name_key = "Cultiway.ArtifactAbility.SoulShakingChime";
        SoulShakingChime.SetSemantics(
            ArtifactSemantics.Theme.Sound,
            ArtifactSemantics.Theme.Soul,
            ArtifactSemantics.Element.Neg,
            ArtifactSemantics.Effect.Impact,
            ArtifactSemantics.Effect.Control);
        SoulShakingChime.exclusivity = ArtifactAbilityExclusivity.SoundBurst;
        SoulShakingChime.manifestation_cost = 1.2f;
        SoulShakingChime.AddSynergies(
            ArtifactSemantics.Theme.Sound,
            ArtifactSemantics.Effect.Impact,
            ArtifactSemantics.Theme.Soul);
        SoulShakingChime.AddConflicts(ArtifactSemanticRules.SilenceAura);
        SoulShakingChime.minimum_score = 1f;
        SoulShakingChime.use_profile = new ArtifactUseProfile { offensive = 0.85f, defensive = 0.2f };
        SoulShakingChime.control_complexity = 0.38f;
        SoulShakingChime.thread_cost = 1;
        SoulShakingChime.parameter_schema =
        [
            NumberSpec(EffectRadius), NumberSpec(DamageMultiplier), NumberSpec(ForceStrength),
            NumberSpec(StatusDuration), NumberSpec(EffectDuration), NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        SoulShakingChime.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Sound) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Soul) * 0.35f +
             context.GetTrait(ArtifactMaterialTraits.Impact) * 0.4f +
             context.GetTrait(ArtifactMaterialTraits.Resonance) * 0.2f);
        SoulShakingChime.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(EffectRadius, 3.6f * context.scales.Range),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.7f * context.scales.Potency),
            ArtifactAbilityValue.Number(ForceStrength, 0.75f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 0.7f * context.scales.Duration),
            ArtifactAbilityValue.Number(EffectDuration, 0.65f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 9f, 2.8f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3.5f)),
        ];
        SoulShakingChime.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SoulShakingChime.Description"),
            ability.GetNumber(EffectRadius), ability.GetNumber(DamageMultiplier),
            ability.GetNumber(StatusDuration), ability.GetNumber(Cooldown));
        SoulShakingChime.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = SkillCastResources.Wakan,
        });
        SoulShakingChime.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Self,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 7,
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            TryUse = RingSoulShakingChime,
        });
    }

    private static bool RingSoulShakingChime(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        var contact = new ArtifactAreaContact(ability.GetNumber(StatusDuration) * context.effect_scale,
            ability.GetNumber(ForceStrength) * context.effect_scale);
        if (!ArtifactAreaConstruction.Start(context, SoulShakingChime, target, origin, ControllerPosition(context),
                ability.GetNumber(EffectRadius), ability.GetNumber(EffectDuration), contact, "pressure_wave", SoulComposition,
                SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier))) return false;
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void ConfigurePurifyingChime()
    {
        PurifyingChime.name_key = "Cultiway.ArtifactAbility.PurifyingChime";
        PurifyingChime.SetSemantics(
            ArtifactSemantics.Theme.Sound,
            ArtifactSemantics.Effect.Purification,
            ArtifactSemantics.Effect.Dispel);
        PurifyingChime.exclusivity = ArtifactAbilityExclusivity.PurificationBurst;
        PurifyingChime.manifestation_cost = 1.05f;
        PurifyingChime.AddSynergies(
            ArtifactSemantics.Role.Support,
            ArtifactSemantics.Theme.Sound,
            ArtifactSemantics.Effect.Ward);
        PurifyingChime.AddConflicts(ArtifactSemanticRules.CurseField);
        PurifyingChime.minimum_score = 1f;
        PurifyingChime.use_profile = new ArtifactUseProfile { defensive = 0.55f, support = 1f };
        PurifyingChime.control_complexity = 0.3f;
        PurifyingChime.parameter_schema =
        [
            NumberSpec(EffectRadius), IntegerSpec(EffectCount), NumberSpec(EffectDuration),
            NumberSpec(Cooldown), NumberSpec(ActivationCost),
        ];
        PurifyingChime.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Purification) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Sound) * 0.4f +
             context.GetTrait(ArtifactMaterialTraits.Ward) * 0.28f +
             context.GetTrait(ArtifactMaterialTraits.Pos) * 0.15f);
        PurifyingChime.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(EffectRadius, 4f * context.scales.Range),
            ArtifactAbilityValue.Integer(EffectCount, Mathf.Clamp(Mathf.FloorToInt(context.scales.Precision), 1, 6)),
            ArtifactAbilityValue.Number(EffectDuration, 0.7f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 12f, 3.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3f)),
        ];
        PurifyingChime.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.PurifyingChime.Description"),
            ability.GetNumber(EffectRadius), ability.GetInteger(EffectCount), ability.GetNumber(Cooldown));
        PurifyingChime.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = SkillCastResources.Wakan,
        });
        PurifyingChime.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
            target_mode = ActiveAbilityTargetMode.Self,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 5,
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            TryUse = RingPurifyingChime,
        });
    }

    private static bool RingPurifyingChime(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        var contact = new ArtifactAreaContact(ability.GetInteger(EffectCount));
        if (!ArtifactAreaConstruction.Start(context, PurifyingChime, target, origin, ControllerPosition(context),
                ability.GetNumber(EffectRadius), ability.GetNumber(EffectDuration), contact, "purifying_curtain",
                ElementComposition.Static.empty)) return false;
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void ConfigureGoldenBellBarrier()
    {
        GoldenBellBarrier.name_key = "Cultiway.ArtifactAbility.GoldenBellBarrier";
        GoldenBellBarrier.SetSemantics(
            ArtifactSemantics.Effect.Shield,
            ArtifactSemantics.Theme.Sound,
            ArtifactSemantics.Effect.Ward);
        GoldenBellBarrier.exclusivity = ArtifactAbilityExclusivity.ActiveBarrier;
        GoldenBellBarrier.manifestation_cost = 1.2f;
        GoldenBellBarrier.AddSynergies(
            ArtifactSemantics.Role.Defensive,
            ArtifactSemantics.Effect.Ward,
            ArtifactSemantics.Form.Sustain);
        GoldenBellBarrier.AddConflicts(ArtifactSemanticRules.DamageConversion);
        GoldenBellBarrier.minimum_score = 1f;
        GoldenBellBarrier.use_profile = new ArtifactUseProfile { defensive = 1f, support = 0.25f };
        GoldenBellBarrier.control_complexity = 0.4f;
        GoldenBellBarrier.thread_cost = 1;
        GoldenBellBarrier.parameter_schema =
        [
            NumberSpec(ShieldCapacity), NumberSpec(EffectDuration), NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        GoldenBellBarrier.state_schema =
        [
            new ArtifactAbilityValueSpec
            {
                key = ShieldCurrent,
                kind = ArtifactAbilityValueKind.Number,
                required = true,
            },
        ];
        GoldenBellBarrier.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Ward) +
             context.GetTrait(ArtifactMaterialTraits.Sound) * 0.45f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.35f +
             context.GetTrait(ArtifactMaterialTraits.Hardness) * 0.22f);
        GoldenBellBarrier.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(ShieldCapacity, 34f * context.scales.Capacity * context.scales.Potency),
            ArtifactAbilityValue.Number(EffectDuration, 5f * context.scales.Duration),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 14f, 4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4f)),
        ];
        GoldenBellBarrier.ComposeInitialState = _ => [ArtifactAbilityValue.Number(ShieldCurrent, 0f)];
        GoldenBellBarrier.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.GoldenBellBarrier.Description"),
            ability.GetNumber(ShieldCapacity), ability.GetNumber(EffectDuration), ability.GetNumber(Cooldown));
        GoldenBellBarrier.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Operating,
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = SkillCastResources.Wakan,
            OnActivityEnded = EndGoldenBellBarrier,
        });
        GoldenBellBarrier.Handle<ArtifactIncomingDamageEvent>(
            (context, _, runtime, evt) => ArtifactAbilityLifecycle.IsActivityCarrier(context, in runtime) &&
                                          runtime.activity_kind != ArtifactAbilityActivityKind.None &&
                                          runtime.GetNumber(ShieldCurrent) > 0f && evt.Damage > 0f,
            AbsorbWithGoldenBellBarrier);
        GoldenBellBarrier.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Self,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 6,
            TryUse = RaiseGoldenBellBarrier,
        });
    }

    private static bool RaiseGoldenBellBarrier(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!GoldenBellActivation.Start(context, target, origin)) return false;
        runtime.SetNumber(ShieldCurrent, ability.GetNumber(ShieldCapacity) * context.effect_scale);
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void EndGoldenBellBarrier(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactAbilityEndReason _)
    {
        runtime.SetNumber(ShieldCurrent, 0f);
        if (!context.controller.IsNull && context.controller.TryGetComponent(out ActorBinder binder))
            ConstructedAbilityService.CancelFromItem(binder.ID, context.artifact.Id, ability.ability_id);
    }

    private static void AbsorbWithGoldenBellBarrier(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        float shield = runtime.GetNumber(ShieldCurrent);
        float absorbed = CombatDamageEffects.AbsorbDamage(ref evt.Damage, ref shield, evt.Target);
        runtime.SetNumber(ShieldCurrent, shield);
        if (absorbed <= 0f) return;
        Vector3 direction = evt.Attacker != null && !evt.Attacker.isRekt()
            ? evt.Attacker.GetSimPos() - evt.Target.Base.GetSimPos()
            : Vector3.zero;
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Guard,
            target: evt.Attacker,
            direction: direction,
            intensity: absorbed / Mathf.Max(1f, ability.GetNumber(ShieldCapacity)),
            resourceDepleted: shield <= 0f,
            contactPoint: evt.ContactPoint);
    }

    private static void ConfigureBellAbilityVisuals()
    {
        ConfigureSoulShakingChimeVisuals();
        ConfigurePurifyingChimeVisuals();
        ConfigureGoldenBellBarrierVisuals();
    }

    private static void ConfigureSoulShakingChimeVisuals()
    {
        SoulShakingChime.Visualize(
            Theme(SkillVfxElements.Neg.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactCompositeVisualCue(
                        new ArtifactAudioVisualCue
                        {
                            anchor = ArtifactVisualAnchorKind.Artifact,
                            sound = "event:/SFX/HIT/HitMetal",
                        },
                        ChimeBodyPulse(5f)
                    ),
                    0.24f
                )
        );
    }

    private static void ConfigurePurifyingChimeVisuals()
    {
        PurifyingChime.Visualize(
            Theme(SkillVfxElements.Pos.AccentColor)
                .Signal(ArtifactVisualChannels.Trigger, ChimeBodyPulse(2.8f), 0.24f)
        );
    }

    /// <summary>原钟共用同一段短促摆动，衰减后回到实际朝向。</summary>
    /// <param name="yaw">本次钟响的最大摆动角。</param>
    private static ArtifactModelPulseVisualCue ChimeBodyPulse(float yaw) => new()
    {
        pulse_amplitude = 0f,
        pulse_speed = 52f,
        yaw_amplitude = yaw,
        tint_blend = 0.18f,
        fade_out = true
    };

    private static void ConfigureGoldenBellBarrierVisuals()
    {
        ArtifactAbilityVisualProfile profile = Theme(SkillVfxElements.Earth.AccentColor);
        profile.spatial = true;
        GoldenBellBarrier.Visualize(profile
            .Loop("golden_bell_barrier", new GoldenBellSpatialVisualCue(false),
                context => IsActivityActive(context) && context.runtime.GetNumber(ShieldCurrent) > 0f,
                "artifact.golden_bell_barrier", ArtifactVisualStackPolicy.Strongest,
                context => context.runtime.GetNumber(ShieldCurrent) /
                           Mathf.Max(1f, context.ability.GetNumber(ShieldCapacity)))
            .Signal(ArtifactVisualChannels.Guard, new GoldenBellSpatialVisualCue(true), 0.38f,
                "artifact.golden_bell_barrier.hit", ArtifactVisualStackPolicy.SinglePerController));
    }
}

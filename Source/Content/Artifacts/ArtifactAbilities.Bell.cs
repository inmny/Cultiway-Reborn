using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
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
    /// <summary>范围主动能力；以钟声伤害并眩晕周围敌人，同时将其震退。</summary>
    public static ArtifactAbilityAsset SoulShakingChime { get; private set; }
    /// <summary>范围净化主动；清除友军负面状态，并驱散敌人的正面状态。</summary>
    public static ArtifactAbilityAsset PurifyingChime { get; private set; }
    /// <summary>限时护罩主动；展开金钟护盾池，在持续期间吸收持有者受到的伤害。</summary>
    public static ArtifactAbilityAsset GoldenBellBarrier { get; private set; }

    private static void ConfigureSoulShakingChime()
    {
        SoulShakingChime.name_key = "Cultiway.ArtifactAbility.SoulShakingChime";
        SoulShakingChime.tags = ["active", "offensive", "sound", "soul", "impact", "control"];
        SoulShakingChime.exclusive_group = "sound_burst";
        SoulShakingChime.manifestation_cost = 1.2f;
        SoulShakingChime.synergy_tags = ["sound", "impact", "soul"];
        SoulShakingChime.conflict_tags = ["silence_aura"];
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
            Resource = UseWakan,
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
        in ActiveAbilityTarget _,
        ActiveAbilityUseOrigin __)
    {
        Actor controller = Controller(context);
        Vector2 center = controller.current_position;
        ArtifactTargeting.ForEachHostile(controller, center, ability.GetNumber(EffectRadius), target =>
        {
            ArtifactDamageEffects.DealDamage(
                controller,
                target,
                SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
                SoulComposition);
            ArtifactStatusEffects.ApplyStatus(target, StatusEffects.Daze, ability.GetNumber(StatusDuration), controller);
            ArtifactForceEffects.ApplyRadialForce(
                controller,
                target,
                center,
                ability.GetNumber(ForceStrength),
                pull: false);
        });
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void ConfigurePurifyingChime()
    {
        PurifyingChime.name_key = "Cultiway.ArtifactAbility.PurifyingChime";
        PurifyingChime.tags = ["active", "support", "sound", "purification", "dispel"];
        PurifyingChime.exclusive_group = "purification_burst";
        PurifyingChime.manifestation_cost = 1.05f;
        PurifyingChime.synergy_tags = ["support", "sound", "ward"];
        PurifyingChime.conflict_tags = ["curse_field"];
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
            Resource = UseWakan,
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
        in ActiveAbilityTarget _,
        ActiveAbilityUseOrigin __)
    {
        Actor controller = Controller(context);
        int count = ability.GetInteger(EffectCount);
        float radius = ability.GetNumber(EffectRadius);
        int changed = 0;
        ArtifactTargeting.ForEachFriendly(controller, controller.current_position, radius, target =>
            changed += ArtifactStatusEffects.CleanseNegativeStatuses(target, count));
        ArtifactTargeting.ForEachHostile(controller, controller.current_position, radius, target =>
            changed += ArtifactStatusEffects.DispelPositiveStatuses(target, count));
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Cleanse,
            controller.current_position,
            intensity: Mathf.Max(1f, changed));
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void ConfigureGoldenBellBarrier()
    {
        GoldenBellBarrier.name_key = "Cultiway.ArtifactAbility.GoldenBellBarrier";
        GoldenBellBarrier.tags = ["active", "defensive", "shield", "sound", "ward"];
        GoldenBellBarrier.exclusive_group = "active_barrier";
        GoldenBellBarrier.manifestation_cost = 1.2f;
        GoldenBellBarrier.synergy_tags = ["defensive", "ward", "sustain"];
        GoldenBellBarrier.conflict_tags = ["damage_conversion"];
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
            Resource = UseWakan,
            OnActivityEnded = EndGoldenBellBarrier,
        });
        GoldenBellBarrier.Handle<ArtifactIncomingDamageEvent>(
            (_, _, runtime, evt) => runtime.activity_kind != ArtifactAbilityActivityKind.None &&
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
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget __,
        ActiveAbilityUseOrigin ___)
    {
        runtime.SetNumber(ShieldCurrent, ability.GetNumber(ShieldCapacity));
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void EndGoldenBellBarrier(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance __,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactAbilityEndReason ___)
    {
        runtime.SetNumber(ShieldCurrent, 0f);
    }

    private static void AbsorbWithGoldenBellBarrier(
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

    private static void ConfigureBellAbilityVisuals()
    {
        ConfigureSoulShakingChimeVisuals();
        ConfigurePurifyingChimeVisuals();
        ConfigureGoldenBellBarrierVisuals();
    }

    private static void ConfigureSoulShakingChimeVisuals()
    {
        ArtifactAreaVisualCue wave = ExpandingArea(
            ArtifactVisualAnchorKind.Controller,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            ArtifactVfxStyles.Soul
        );
        wave.show_inner_ring = true;
        wave.inner_radius_ratio = 0.55f;
        SoulShakingChime.Visualize(
            Theme(SkillVfxElements.Neg.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactCompositeVisualCue(
                        wave,
                        Burst(
                            ArtifactVisualAnchorKind.Controller,
                            ArtifactVisualColorRole.Glow,
                            12,
                            0.55f
                        ),
                        new ArtifactImpactVisualCue
                        {
                            anchor = ArtifactVisualAnchorKind.Controller,
                            sound = "event:/SFX/HIT/HitMetal",
                            shake_intensity = 0.13f,
                        },
                        Pulse(1.3f, 1f, 0.16f)
                    ),
                    0.62f
                )
        );
    }

    private static void ConfigurePurifyingChimeVisuals()
    {
        ArtifactAreaVisualCue cleanse = ExpandingArea(
            ArtifactVisualAnchorKind.Controller,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Glow,
            ArtifactVfxStyles.Purification
        );
        ArtifactGlyphVisualCue glyph = Glyph(
            ArtifactVisualAnchorKind.Controller,
            context => context.ability.GetNumber(EffectRadius) * 0.72f,
            ArtifactVisualColorRole.Primary,
            ArtifactVfxStyles.Purification
        );
        glyph.sides = 12;
        glyph.fade_out = true;
        glyph.rotation_speed = 20f;
        PurifyingChime.Visualize(
            Theme(SkillVfxElements.Pos.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Cleanse,
                    new ArtifactCompositeVisualCue(
                        cleanse,
                        glyph,
                        Burst(
                            ArtifactVisualAnchorKind.Controller,
                            ArtifactVisualColorRole.Glow,
                            14,
                            0.58f
                        )
                    ),
                    0.7f,
                    "artifact.purifying_chime",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureGoldenBellBarrierVisuals()
    {
        ArtifactAreaVisualCue barrier = Area(
            ArtifactVisualAnchorKind.Controller,
            context => 0.78f * ArtifactAbilityVisuals.ResolveActorScale(context),
            ArtifactVisualColorRole.Glow,
            0.42f,
            0.035f,
            ArtifactVfxStyles.Ward
        );
        barrier.show_inner_ring = true;
        barrier.inner_rotation_speed = 18f;
        ArtifactProjectionVisualCue bellShadow = new ArtifactProjectionVisualCue
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            color_role = ArtifactVisualColorRole.Glow,
            scale = 1.35f,
            alpha = 0.22f,
            start_scale = 1f,
            end_scale = 1f,
            fade_out = false,
            match_actor_scale = true,
            pulse_amplitude = 0.025f,
        };
        GoldenBellBarrier.Visualize(
            Theme(SkillVfxElements.Earth.AccentColor)
                .Loop(
                    "golden_bell_barrier",
                    new ArtifactCompositeVisualCue(barrier, bellShadow),
                    context =>
                        IsActivityActive(context) && context.runtime.GetNumber(ShieldCurrent) > 0f,
                    "artifact.golden_bell_barrier",
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
                            (ArtifactAbilityVisualContext _) => 0.9f,
                            ArtifactVisualColorRole.Glow,
                            ArtifactVfxStyles.Ward
                        ),
                        Burst(
                            ArtifactVisualAnchorKind.Controller,
                            ArtifactVisualColorRole.Primary,
                            6
                        ),
                        new ArtifactAudioVisualCue
                        {
                            anchor = ArtifactVisualAnchorKind.Controller,
                            sound = "event:/SFX/HIT/HitMetal",
                        }
                    ),
                    0.38f,
                    "artifact.golden_bell_barrier.hit",
                    ArtifactVisualStackPolicy.Strongest
                )
        );
    }
}

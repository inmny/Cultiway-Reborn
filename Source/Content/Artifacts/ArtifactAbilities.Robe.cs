using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using NeoModLoader.General;
using strings;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>充能护体被动；维持可自动恢复的云袍护盾池，吸收持有者受到的伤害。</summary>
    public static ArtifactAbilityAsset CloudRobeShield { get; private set; }
    /// <summary>持续隐匿主动；提供隐匿、移速和减伤，受击或活动结束时解除。</summary>
    public static ArtifactAbilityAsset HeavenlyConcealment { get; private set; }
    /// <summary>受击卸力被动；转移部分所受伤害反击攻击者，并对其施加推拉力。</summary>
    public static ArtifactAbilityAsset DamageDiversion { get; private set; }

    private static void ConfigureCloudRobeShield()
    {
        CloudRobeShield.name_key = "Cultiway.ArtifactAbility.CloudRobeShield";
        CloudRobeShield.SetSemantics(ArtifactSemantics.Effect.Shield, ArtifactSemantics.Form.Sustain);
        CloudRobeShield.exclusivity = ArtifactAbilityExclusivity.RechargingShield;
        CloudRobeShield.manifestation_cost = 1.1f;
        CloudRobeShield.AddSynergies(ArtifactSemantics.Role.Defensive, ArtifactSemantics.Effect.Recovery);
        CloudRobeShield.AddConflicts(ArtifactSemanticRules.Brittle);
        CloudRobeShield.minimum_score = 1f;
        CloudRobeShield.use_profile = new ArtifactUseProfile { defensive = 1f, support = 0.2f };
        CloudRobeShield.control_complexity = 0.25f;
        CloudRobeShield.parameter_schema = [NumberSpec(ShieldCapacity), NumberSpec(ShieldRegen)];
        CloudRobeShield.state_schema =
        [
            new ArtifactAbilityValueSpec
            {
                key = ShieldCurrent,
                kind = ArtifactAbilityValueKind.Number,
                required = true,
            },
        ];
        CloudRobeShield.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Ward) +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.65f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Flexibility) * 0.4f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.12f);
        CloudRobeShield.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(ShieldCapacity, 24f * context.scales.Capacity * context.scales.Potency),
            ArtifactAbilityValue.Number(ShieldRegen, 2.2f * context.scales.Efficiency * context.scales.Duration),
        ];
        CloudRobeShield.ComposeInitialState = context =>
        [
            ArtifactAbilityValue.Number(
                ShieldCurrent,
                24f * context.scales.Capacity * context.scales.Potency),
        ];
        CloudRobeShield.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.CloudRobeShield.Description"),
            ability.GetNumber(ShieldCapacity),
            ability.GetNumber(ShieldRegen));
        CloudRobeShield.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 1f,
            CanTick = (_, ability, runtime) => runtime.GetNumber(ShieldCurrent) < ability.GetNumber(ShieldCapacity),
            OnTick = RechargeCloudRobeShield,
            OnAttached = ClampCloudRobeShield,
        });
        CloudRobeShield.Handle<ArtifactIncomingDamageEvent>(
            (_, _, runtime, evt) => evt.Damage > 0f && runtime.GetNumber(ShieldCurrent) > 0f,
            AbsorbWithCloudRobeShield);
    }

    private static void ClampCloudRobeShield(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime)
    {
        runtime.SetNumber(
            ShieldCurrent,
            Mathf.Clamp(runtime.GetNumber(ShieldCurrent), 0f, ability.GetNumber(ShieldCapacity)));
    }

    private static void RechargeCloudRobeShield(
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

    private static void AbsorbWithCloudRobeShield(
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

    private static void ConfigureHeavenlyConcealment()
    {
        HeavenlyConcealment.name_key = "Cultiway.ArtifactAbility.HeavenlyConcealment";
        HeavenlyConcealment.SetSemantics(ArtifactSemantics.Effect.Concealment, ArtifactSemantics.Effect.Movement);
        HeavenlyConcealment.exclusivity = ArtifactAbilityExclusivity.ConcealmentState;
        HeavenlyConcealment.manifestation_cost = 1.05f;
        HeavenlyConcealment.AddSynergies(ArtifactSemantics.Effect.Movement, ArtifactSemantics.Role.Defensive);
        HeavenlyConcealment.AddConflicts(ArtifactSemanticRules.Revealing);
        HeavenlyConcealment.minimum_score = 1f;
        HeavenlyConcealment.use_profile = new ArtifactUseProfile { defensive = 0.7f, support = 0.35f };
        HeavenlyConcealment.control_complexity = 0.38f;
        HeavenlyConcealment.thread_cost = 1;
        HeavenlyConcealment.parameter_schema =
        [
            NumberSpec(EffectDuration),
            NumberSpec(DamageReduction),
            NumberSpec(SpeedBonus),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        HeavenlyConcealment.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Concealment) +
             context.GetTrait(ArtifactMaterialTraits.Space) * 0.55f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Flexibility) * 0.3f +
             context.GetTrait(ArtifactMaterialTraits.Mobility) * 0.28f);
        HeavenlyConcealment.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(EffectDuration, 4.5f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageReduction, Mathf.Clamp(0.16f * context.scales.Precision, 0.16f, 0.55f)),
            ArtifactAbilityValue.Number(SpeedBonus, 2.5f * context.scales.Range),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 12f, 3.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.25f)),
        ];
        HeavenlyConcealment.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.HeavenlyConcealment.Description"),
            ability.GetNumber(EffectDuration),
            ability.GetNumber(DamageReduction),
            ability.GetNumber(SpeedBonus),
            ability.GetNumber(Cooldown));
        HeavenlyConcealment.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Operating,
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.5f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = UseWakan,
            OnActivityEnded = EndHeavenlyConcealment,
            ContributeStats = (_, ability, runtime, stats) =>
            {
                if (runtime.activity_kind != ArtifactAbilityActivityKind.None)
                {
                    stats[S.speed] += ability.GetNumber(SpeedBonus);
                }
            },
        });
        HeavenlyConcealment.Handle<ArtifactIncomingDamageEvent>(
            (_, _, runtime, evt) => runtime.activity_kind != ArtifactAbilityActivityKind.None && evt.Damage > 0f,
            ApplyHeavenlyConcealmentReduction);
        HeavenlyConcealment.Handle<ArtifactDamageDealtEvent>(
            (_, _, runtime, evt) => runtime.activity_kind != ArtifactAbilityActivityKind.None && evt.Damage > 0f,
            BreakHeavenlyConcealment);
        HeavenlyConcealment.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
            target_mode = ActiveAbilityTargetMode.Self,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 5,
            TryUse = BeginHeavenlyConcealment,
        });
    }

    private static bool BeginHeavenlyConcealment(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget _,
        ActiveAbilityUseOrigin __)
    {
        Actor controller = Controller(context);
        ArtifactStatusEffects.ApplyStatus(
            controller,
            StatusEffects.Concealed,
            ability.GetNumber(EffectDuration),
            controller);
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static void BreakHeavenlyConcealment(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactDamageDealtEvent _)
    {
        ArtifactAbilityLifecycle.InterruptActivity(context, ability, ref runtime);
    }

    private static void EndHeavenlyConcealment(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance _,
        ref ArtifactAbilityRuntimeEntry __,
        ArtifactAbilityEndReason ___)
    {
        Actor controller = Controller(context);
        ArtifactStatusEffects.RemoveStatus(controller, StatusEffects.Concealed, controller);
    }

    private static void ApplyHeavenlyConcealmentReduction(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        float reduction = ability.GetNumber(DamageReduction);
        evt.Damage *= 1f - reduction;
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Guard,
            target: evt.Attacker,
            intensity: reduction);
    }

    private static void ConfigureDamageDiversion()
    {
        DamageDiversion.name_key = "Cultiway.ArtifactAbility.DamageDiversion";
        DamageDiversion.SetSemantics(ArtifactSemantics.Effect.Counter, ArtifactSemantics.Effect.Transformation);
        DamageDiversion.exclusivity = ArtifactAbilityExclusivity.DamageDiversion;
        DamageDiversion.manifestation_cost = 1f;
        DamageDiversion.AddSynergies(ArtifactSemantics.Role.Defensive, ArtifactSemantics.Effect.Counter);
        DamageDiversion.AddConflicts(ArtifactSemanticRules.RigidGuard);
        DamageDiversion.minimum_score = 1f;
        DamageDiversion.use_profile = new ArtifactUseProfile { offensive = 0.35f, defensive = 0.9f };
        DamageDiversion.control_complexity = 0.36f;
        DamageDiversion.parameter_schema =
        [
            NumberSpec(DamageReduction),
            NumberSpec(ForceStrength),
            NumberSpec(Cooldown),
            IntegerSpec(MaxCharges),
            NumberSpec(Recharge),
        ];
        DamageDiversion.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Transformation) +
             context.GetTrait(ArtifactMaterialTraits.Reflection) * 0.45f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Flexibility) * 0.5f);
        DamageDiversion.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(DamageReduction, Mathf.Clamp(0.18f * context.scales.Precision, 0.18f, 0.58f)),
            ArtifactAbilityValue.Number(ForceStrength, 0.45f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 1.8f, 0.5f)),
            ArtifactAbilityValue.Integer(MaxCharges, Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity), 1, 4)),
            ArtifactAbilityValue.Number(Recharge, ScaledCooldown(context, 7f, 2f)),
        ];
        DamageDiversion.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.DamageDiversion.Description"),
            ability.GetNumber(DamageReduction),
            ability.GetInteger(MaxCharges),
            ability.GetNumber(Recharge));
        DamageDiversion.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            event_consumes_trigger = true,
            ResolveMaxCharges = (_, ability) => ability.GetInteger(MaxCharges),
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveRecharge = (_, ability) => ability.GetNumber(Recharge),
        });
        DamageDiversion.Handle<ArtifactIncomingDamageEvent>(
            (_, _, _, evt) => evt.Damage > 0f && !evt.IsRetaliation &&
                              evt.Attacker != null && !evt.Attacker.isRekt() && evt.Attacker.isActor(),
            ApplyDamageDiversion);
    }

    private static void ApplyDamageDiversion(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        Actor controller = Controller(context);
        Actor attacker = evt.Attacker.a;
        float diverted = evt.Damage * ability.GetNumber(DamageReduction);
        evt.Damage -= diverted;
        ArtifactDamageEffects.DealRetaliationDamage(
            controller,
            attacker,
            diverted,
            evt.DamageComposition,
            evt.IgnoreDamageReduction);
        ArtifactForceEffects.ApplyRadialForce(
            controller,
            attacker,
            controller.current_position,
            ability.GetNumber(ForceStrength),
            pull: false);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Counter,
            target: attacker,
            intensity: diverted);
    }

    private static void ConfigureRobeAbilityVisuals()
    {
        ConfigureCloudRobeShieldVisuals();
        ConfigureHeavenlyConcealmentVisuals();
        ConfigureDamageDiversionVisuals();
    }

    private static void ConfigureCloudRobeShieldVisuals()
    {
        ArtifactAreaVisualCue shieldField = Area(
            ArtifactVisualAnchorKind.Controller,
            context => 0.75f * ArtifactAbilityVisuals.ResolveActorScale(context),
            ArtifactVisualColorRole.Glow,
            0.34f,
            0.025f,
            ArtifactVfxStyles.Ward
        );
        shieldField.inner_rotation_speed = 12f;
        ArtifactAnimVisualCue shieldHit = new ArtifactAnimVisualCue("effects/fx_status_shield_t")
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            color_role = ArtifactVisualColorRole.Glow,
            offset = new Vector3(0f, 0.08f, 0.2f),
            scale = 0.1f,
            frame_interval = 0.075f,
            alpha = 0.9f,
            loop = false,
        };
        CloudRobeShield.Visualize(
            Theme(SkillVfxElements.Water.AccentColor)
                .Loop(
                    "cloud_robe_shield",
                    shieldField,
                    context =>
                        context.runtime.GetNumber(ShieldCurrent) > 0f
                        && ArtifactAbilityLifecycle.MeetsState(
                            context.control_state,
                            ArtifactControlState.Ready
                        ),
                    "artifact.cloud_robe_shield",
                    ArtifactVisualStackPolicy.Strongest,
                    context =>
                        context.runtime.GetNumber(ShieldCurrent)
                        / Mathf.Max(1f, context.ability.GetNumber(ShieldCapacity))
                )
                .Signal(
                    ArtifactVisualChannels.Guard,
                    new ArtifactCompositeVisualCue(
                        shieldHit,
                        Burst(ArtifactVisualAnchorKind.Controller, ArtifactVisualColorRole.Glow, 4)
                    ),
                    0.4f,
                    "artifact.cloud_robe_shield.hit",
                    ArtifactVisualStackPolicy.Strongest
                )
        );
    }

    private static void ConfigureHeavenlyConcealmentVisuals()
    {
        ArtifactRibbonVisualCue veil = new ArtifactRibbonVisualCue
        {
            style_key = ArtifactVfxStyles.Cloth,
            anchor = ArtifactVisualAnchorKind.Controller,
            color_role = ArtifactVisualColorRole.Secondary,
            width = 0.12f,
            alpha = 0.38f,
            history = 0.5f,
            min_distance = 0.035f,
            max_points = 18,
            match_actor_scale = true,
        };
        ArtifactParticleVisualCue mist = Burst(
            ArtifactVisualAnchorKind.Controller,
            ArtifactVisualColorRole.Glow,
            2,
            0.32f
        );
        mist.emission_interval = 0.18f;
        HeavenlyConcealment.Visualize(
            Theme(SkillVfxElements.Wind.AccentColor)
                .Loop(
                    "heavenly_concealment",
                    new ArtifactCompositeVisualCue(veil, mist, Pulse(1f, 1f, 0.08f)),
                    IsActivityActive,
                    "artifact.heavenly_concealment",
                    ArtifactVisualStackPolicy.Strongest
                )
                .Signal(
                    ArtifactVisualChannels.Guard,
                    Burst(
                        ArtifactVisualAnchorKind.Controller,
                        ArtifactVisualColorRole.Glow,
                        5,
                        0.4f
                    ),
                    0.35f
                )
        );
    }

    private static void ConfigureDamageDiversionVisuals()
    {
        ArtifactTetherVisualCue diversion = new ArtifactTetherVisualCue
        {
            style_key = ArtifactVfxStyles.Wind,
            from = ArtifactVisualAnchorKind.Controller,
            to = ArtifactVisualAnchorKind.Target,
            color_role = ArtifactVisualColorRole.Secondary,
            width = 0.055f,
            curvature = 0.22f,
            wave_amplitude = 0.06f,
            match_actor_scale = false,
        };
        DamageDiversion.Visualize(
            Theme(SkillVfxElements.Wind.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Counter,
                    new ArtifactCompositeVisualCue(
                        diversion,
                        Burst(ArtifactVisualAnchorKind.Controller, ArtifactVisualColorRole.Glow, 6),
                        Pulse(1.18f, 0.96f, 0.14f)
                    ),
                    0.42f
                )
        );
    }
}

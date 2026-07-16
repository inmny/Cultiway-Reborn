using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using NeoModLoader.General;
using strings;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>坠印部署主动；法印落地后造成范围伤害、眩晕和冲击力。</summary>
    public static ArtifactAbilityAsset MountainSealFall { get; private set; }
    /// <summary>可部署禁法法域；周期性沉默范围内敌人并抽取其灵气。</summary>
    public static ArtifactAbilityAsset SpellBanningEdict { get; private set; }
    /// <summary>单体封脉主动；对目标施加减速与弱化，并抽取灵气。</summary>
    public static ArtifactAbilityAsset MeridianSealing { get; private set; }

    private static void ConfigureMountainSealFall()
    {
        MountainSealFall.name_key = "Cultiway.ArtifactAbility.MountainSealFall";
        MountainSealFall.tags = ["active", "offensive", "deployment", "impact", "control"];
        MountainSealFall.exclusive_group = "heavy_impact";
        MountainSealFall.manifestation_cost = 1.45f;
        MountainSealFall.synergy_tags = ["suppression", "field"];
        MountainSealFall.conflict_tags = ["lightweight"];
        MountainSealFall.minimum_score = 1f;
        MountainSealFall.use_profile = new ArtifactUseProfile { offensive = 0.9f, defensive = 0.15f };
        MountainSealFall.control_complexity = 0.46f;
        MountainSealFall.thread_cost = 1;
        MountainSealFall.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(EffectRadius),
            NumberSpec(DamageMultiplier),
            NumberSpec(ForceStrength),
            NumberSpec(StatusDuration),
            NumberSpec(ImpactDelay),
            NumberSpec(EffectDuration),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        MountainSealFall.state_schema =
        [
            new ArtifactAbilityValueSpec
            {
                key = ImpactDone,
                kind = ArtifactAbilityValueKind.Boolean,
                required = true,
            },
        ];
        MountainSealFall.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Impact) +
             context.GetTrait(ArtifactMaterialTraits.Hardness) * 0.75f +
             context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.35f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Earth) * 0.35f);
        MountainSealFall.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 8f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 2.2f * context.scales.Range),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.85f * context.scales.Potency),
            ArtifactAbilityValue.Number(ForceStrength, 0.8f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 0.55f * context.scales.Duration),
            ArtifactAbilityValue.Number(ImpactDelay, Mathf.Clamp(0.5f / context.scales.Precision, 0.18f, 0.5f)),
            ArtifactAbilityValue.Number(EffectDuration, 1.1f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 10f, 3.2f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4.5f)),
        ];
        MountainSealFall.ComposeInitialState = _ => [ArtifactAbilityValue.Boolean(ImpactDone, false)];
        MountainSealFall.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.MountainSealFall.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(DamageMultiplier),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(Cooldown));
        MountainSealFall.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.1f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
            OnTick = ApplyMountainSealImpact,
        });
        MountainSealFall.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 8,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployMountainSeal,
        });
    }

    private static bool DeployMountainSeal(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        runtime.SetBoolean(ImpactDone, false);
        return DeployAtTarget(
            context,
            ability,
            ref runtime,
            target,
            ArtifactBodyAnchorRef.Appearance("face", "focus", ArtifactBodyAnchorKind.ForwardTip),
            180f);
    }

    private static void ApplyMountainSealImpact(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        float _)
    {
        if (runtime.GetBoolean(ImpactDone) ||
            World.world.getCurWorldTime() - runtime.activity_started_at < ability.GetNumber(ImpactDelay)) return;

        runtime.SetBoolean(ImpactDone, true);
        Actor controller = Controller(context);
        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector2 position = ArtifactManifestationTools.ResolveWorldAnchor(
            context.artifact,
            deployment.ResolveBodyAnchor());
        float radius = ability.GetNumber(EffectRadius);
        ArtifactTargeting.ForEachHostile(controller, position, radius, target =>
        {
            ArtifactDamageEffects.DealDamage(
                controller,
                target,
                SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
                ElementComposition.Static.Earth);
            ArtifactStatusEffects.ApplyStatus(
                target,
                StatusEffects.Daze,
                ability.GetNumber(StatusDuration),
                controller);
            ArtifactForceEffects.ApplyRadialForce(
                controller,
                target,
                position,
                ability.GetNumber(ForceStrength),
                pull: false);
        });
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Impact,
            position,
            intensity: ability.GetNumber(DamageMultiplier));
    }

    private static void ConfigureSpellBanningEdict()
    {
        SpellBanningEdict.name_key = "Cultiway.ArtifactAbility.SpellBanningEdict";
        SpellBanningEdict.tags = ["active", "offensive", "deployment", "field", "silence"];
        SpellBanningEdict.exclusive_group = "spell_banning_field";
        SpellBanningEdict.manifestation_cost = 1.35f;
        SpellBanningEdict.synergy_tags = ["suppression", "soul"];
        SpellBanningEdict.conflict_tags = ["skill_amplification"];
        SpellBanningEdict.minimum_score = 1f;
        SpellBanningEdict.use_profile = new ArtifactUseProfile { offensive = 0.6f, support = 0.55f };
        SpellBanningEdict.control_complexity = 0.5f;
        SpellBanningEdict.thread_cost = 1;
        SpellBanningEdict.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(EffectRadius),
            NumberSpec(EffectDuration),
            NumberSpec(StatusDuration),
            NumberSpec(DrainAmount),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        SpellBanningEdict.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Sealing) +
             context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.7f +
             context.GetTrait(ArtifactMaterialTraits.Binding) * 0.25f) *
            (0.5f + context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.45f);
        SpellBanningEdict.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 8f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 2.6f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 5f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusDuration, 1.1f * context.scales.Duration),
            ArtifactAbilityValue.Number(DrainAmount, 1.8f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 14f, 4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.45f)),
        ];
        SpellBanningEdict.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SpellBanningEdict.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(EffectDuration),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(DrainAmount));
        SpellBanningEdict.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
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
            OnTick = ApplySpellBanningField,
        });
        SpellBanningEdict.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeploySpellBanningEdict,
        });
    }

    private static bool DeploySpellBanningEdict(
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
            ArtifactBodyAnchorRef.Appearance("face", "focus"));
    }

    private static void ApplySpellBanningField(
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
            ArtifactStatusEffects.ApplyStatus(
                target,
                StatusEffects.Silence,
                ability.GetNumber(StatusDuration),
                controller);
            ArtifactResourceEffects.DrainWakan(target, ability.GetNumber(DrainAmount));
        });
    }

    private static void ConfigureMeridianSealing()
    {
        MeridianSealing.name_key = "Cultiway.ArtifactAbility.MeridianSealing";
        MeridianSealing.tags = ["active", "offensive", "control", "sealing"];
        MeridianSealing.exclusive_group = "single_target_sealing";
        MeridianSealing.manifestation_cost = 0.95f;
        MeridianSealing.synergy_tags = ["debuff", "soul"];
        MeridianSealing.minimum_score = 1f;
        MeridianSealing.use_profile = new ArtifactUseProfile { offensive = 0.75f, support = 0.25f };
        MeridianSealing.control_complexity = 0.34f;
        MeridianSealing.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(StatusDuration),
            NumberSpec(StatusStrength),
            NumberSpec(DrainAmount),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        MeridianSealing.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Sealing) +
             context.GetTrait(ArtifactMaterialTraits.Binding) * 0.75f +
             context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.3f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Neg) * 0.28f);
        MeridianSealing.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7f * context.scales.Range),
            ArtifactAbilityValue.Number(StatusDuration, 2.8f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusStrength, Mathf.Clamp(0.16f * context.scales.Potency, 0.16f, 0.65f)),
            ArtifactAbilityValue.Number(DrainAmount, 8f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 8f, 2.4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 2.5f)),
        ];
        MeridianSealing.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.MeridianSealing.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(StatusStrength),
            ability.GetNumber(DrainAmount),
            ability.GetNumber(Cooldown));
        MeridianSealing.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
        });
        MeridianSealing.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Object,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 6,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanTargetHostileActor,
            TryUse = ApplyMeridianSealing,
        });
    }

    private static bool ApplyMeridianSealing(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = Controller(context);
        Actor victim = target.Object.a;
        float strength = ability.GetNumber(StatusStrength);
        float duration = ability.GetNumber(StatusDuration);
        ArtifactStatusEffects.ApplyStatus(victim, StatusEffects.Slow, duration, S.multiplier_speed, -strength, controller);
        ArtifactStatusEffects.ApplyStatus(victim, StatusEffects.Weaken, duration, S.multiplier_damage, -strength, controller);
        ArtifactResourceEffects.DrainWakan(victim, ability.GetNumber(DrainAmount));
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Drain,
            victim.current_position,
            target: victim,
            intensity: strength);
        return true;
    }

    private static void ConfigureSealAbilityVisuals()
    {
        ConfigureMountainSealFallVisuals();
        ConfigureSpellBanningEdictVisuals();
        ConfigureMeridianSealingVisuals();
    }

    private static void ConfigureMountainSealFallVisuals()
    {
        ArtifactGlyphVisualCue landing = Glyph(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Secondary,
            ArtifactVfxStyles.Earth
        );
        landing.alpha = 0.5f;
        landing.rotation_speed = 8f;
        landing.start_scale = 0.3f;
        ArtifactAreaVisualCue impactArea = Area(
            ArtifactVisualAnchorKind.Point,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.85f,
            0.1f,
            ArtifactVfxStyles.Earth
        );
        impactArea.start_scale = 0.12f;
        impactArea.end_scale = 1.25f;
        impactArea.fade_out = true;
        MountainSealFall.Visualize(
            Theme(SkillVfxElements.Earth.AccentColor)
                .Loop("mountain_landing", landing, IsDeploymentActive)
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactCompositeVisualCue(
                        Pulse(1.35f, 1f, 0.08f),
                        Burst(
                            ArtifactVisualAnchorKind.DeploymentOrigin,
                            ArtifactVisualColorRole.Secondary,
                            4
                        )
                    ),
                    0.42f
                )
                .Signal(
                    ArtifactVisualChannels.Impact,
                    new ArtifactCompositeVisualCue(
                        impactArea,
                        Burst(
                            ArtifactVisualAnchorKind.Point,
                            ArtifactVisualColorRole.Glow,
                            12,
                            0.5f
                        ),
                        new ArtifactImpactVisualCue
                        {
                            anchor = ArtifactVisualAnchorKind.Point,
                            sound = "event:/SFX/WEAPONS/WeaponRockLand",
                            shake_intensity = 0.15f,
                        },
                        Pulse(1.45f, 0.92f, 0.1f)
                    ),
                    0.55f
                )
        );
    }

    private static void ConfigureSpellBanningEdictVisuals()
    {
        ArtifactGlyphVisualCue glyph = Glyph(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            ArtifactVfxStyles.Seal
        );
        glyph.sides = 8;
        glyph.rotation_speed = -18f;
        ArtifactAreaVisualCue field = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Secondary,
            0.45f,
            0.055f,
            ArtifactVfxStyles.Seal
        );
        SpellBanningEdict.Visualize(
            Theme(SkillVfxElements.Neg.AccentColor)
                .Loop(
                    "spell_banning_edict",
                    new ArtifactCompositeVisualCue(field, glyph),
                    IsDeploymentActive
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    Burst(
                        ArtifactVisualAnchorKind.DeploymentOrigin,
                        ArtifactVisualColorRole.Glow,
                        3
                    ),
                    0.3f,
                    "artifact.spell_banning.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureMeridianSealingVisuals()
    {
        ArtifactTetherVisualCue tether = new ArtifactTetherVisualCue
        {
            style_key = ArtifactVfxStyles.Seal,
            from = ArtifactVisualAnchorKind.Artifact,
            to = ArtifactVisualAnchorKind.Target,
            color_role = ArtifactVisualColorRole.Primary,
            width = 0.04f,
            wave_amplitude = 0.04f,
            match_actor_scale = false,
        };
        ArtifactDecalVisualCue seal = new ArtifactDecalVisualCue
        {
            style_key = ArtifactVfxStyles.Seal,
            anchor = ArtifactVisualAnchorKind.Target,
            color_role = ArtifactVisualColorRole.Glow,
            radius = 0.45f,
            sides = 8,
            alpha = 0.82f,
            match_actor_scale = true,
        };
        MeridianSealing.Visualize(
            Theme(SkillVfxElements.Neg.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Drain,
                    new ArtifactCompositeVisualCue(tether, seal),
                    0.55f
                )
        );
    }
}

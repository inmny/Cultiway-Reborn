using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
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
    private static readonly ElementComposition GaleComposition =
        new(wood: 0.42f, pos: 0.38f, entropy: 0.2f, normalize: true);

    /// <summary>扇形攻击主动；对前方敌人造成伤害并以罡风击退。</summary>
    public static ArtifactAbilityAsset GaleFanSweep { get; private set; }
    /// <summary>扇形火攻主动；对前方敌人造成火行伤害并施加持续燃烧。</summary>
    public static ArtifactAbilityAsset DepartingFireFan { get; private set; }
    /// <summary>扇形净化主动；清除友军负面状态，并驱散敌人的正面状态。</summary>
    public static ArtifactAbilityAsset CleansingWindFan { get; private set; }

    private static void ConfigureGaleFanSweep()
    {
        GaleFanSweep.name_key = "Cultiway.ArtifactAbility.GaleFanSweep";
        GaleFanSweep.tags = ["active", "offensive", "cone", "wind", "force"];
        GaleFanSweep.exclusive_group = "wind_cone";
        GaleFanSweep.manifestation_cost = 1.05f;
        GaleFanSweep.synergy_tags = ["mobility", "impact", "projection"];
        GaleFanSweep.conflict_tags = ["immovable_field"];
        GaleFanSweep.minimum_score = 1f;
        GaleFanSweep.use_profile = new ArtifactUseProfile { offensive = 0.9f, defensive = 0.2f };
        GaleFanSweep.control_complexity = 0.3f;
        GaleFanSweep.thread_cost = 1;
        GaleFanSweep.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(ConeAngle), NumberSpec(DamageMultiplier),
            NumberSpec(ForceStrength), NumberSpec(EffectDuration), NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        GaleFanSweep.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Projection) +
             context.GetTrait(ArtifactMaterialTraits.Mobility) * 0.55f) *
            (0.48f + context.GetTrait(ArtifactMaterialTraits.Impact) * 0.4f +
             context.GetTrait(ArtifactMaterialTraits.Flexibility) * 0.18f);
        GaleFanSweep.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 6.8f * context.scales.Range),
            ArtifactAbilityValue.Number(ConeAngle, Mathf.Clamp(72f * context.scales.Precision, 46f, 126f)),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.64f * context.scales.Potency),
            ArtifactAbilityValue.Number(ForceStrength, 0.92f * context.scales.Potency),
            ArtifactAbilityValue.Number(EffectDuration, 0.52f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 7.5f, 2.1f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 2.8f)),
        ];
        GaleFanSweep.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.GaleFanSweep.Description"),
            ability.GetNumber(AttackRange), ability.GetNumber(ConeAngle),
            ability.GetNumber(DamageMultiplier), ability.GetNumber(ForceStrength));
        GaleFanSweep.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
        });
        GaleFanSweep.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.ObjectOrPoint,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanUseWorldTargetInRange,
            TryUse = SweepGaleFan,
        });
    }

    private static bool SweepGaleFan(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = Controller(context);
        Vector2 center = controller.current_position;
        Vector2 direction = DirectionToTarget(context, target);
        ArtifactTargeting.ForEachActorInSector(
            controller,
            center,
            direction,
            ability.GetNumber(AttackRange),
            ability.GetNumber(ConeAngle),
            ArtifactTargeting.TargetDisposition.Hostile,
            victim =>
            {
                ArtifactDamageEffects.DealDamage(
                    controller,
                    victim,
                    SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
                    GaleComposition);
                ArtifactForceEffects.ApplyRadialForce(
                    controller,
                    victim,
                    center,
                    ability.GetNumber(ForceStrength),
                    pull: false);
            });
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime, center, direction);
        return true;
    }

    private static void ConfigureDepartingFireFan()
    {
        DepartingFireFan.name_key = "Cultiway.ArtifactAbility.DepartingFireFan";
        DepartingFireFan.tags = ["active", "offensive", "cone", "fire", "status"];
        DepartingFireFan.exclusive_group = "fire_cone";
        DepartingFireFan.manifestation_cost = 1.12f;
        DepartingFireFan.synergy_tags = ["fire", "volatility", "projection"];
        DepartingFireFan.conflict_tags = ["water_field"];
        DepartingFireFan.minimum_score = 1f;
        DepartingFireFan.use_profile = new ArtifactUseProfile { offensive = 1f };
        DepartingFireFan.control_complexity = 0.34f;
        DepartingFireFan.thread_cost = 1;
        DepartingFireFan.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(ConeAngle), NumberSpec(DamageMultiplier),
            NumberSpec(StatusStrength), NumberSpec(StatusDuration), NumberSpec(EffectDuration),
            NumberSpec(Cooldown), NumberSpec(ActivationCost),
        ];
        DepartingFireFan.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Fire) *
            (0.58f + context.GetTrait(ArtifactMaterialTraits.Volatility) * 0.42f +
             context.GetTrait(ArtifactMaterialTraits.Projection) * 0.32f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.12f);
        DepartingFireFan.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 6.2f * context.scales.Range),
            ArtifactAbilityValue.Number(ConeAngle, Mathf.Clamp(66f * context.scales.Precision, 42f, 112f)),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.7f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusStrength, 0.16f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 3.2f * context.scales.Duration),
            ArtifactAbilityValue.Number(EffectDuration, 0.62f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 8.5f, 2.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3.2f)),
        ];
        DepartingFireFan.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.DepartingFireFan.Description"),
            ability.GetNumber(AttackRange), ability.GetNumber(ConeAngle),
            ability.GetNumber(DamageMultiplier), ability.GetNumber(StatusDuration));
        DepartingFireFan.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
        });
        DepartingFireFan.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.ObjectOrPoint,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 8,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanUseWorldTargetInRange,
            TryUse = SweepDepartingFire,
        });
    }

    private static bool SweepDepartingFire(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = Controller(context);
        Vector2 center = controller.current_position;
        Vector2 direction = DirectionToTarget(context, target);
        float damage = SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier);
        ElementComposition fire = new(fire: 1f);
        ArtifactTargeting.ForEachActorInSector(
            controller,
            center,
            direction,
            ability.GetNumber(AttackRange),
            ability.GetNumber(ConeAngle),
            ArtifactTargeting.TargetDisposition.Hostile,
            victim =>
            {
                ArtifactDamageEffects.DealDamage(controller, victim, damage, fire);
                ArtifactStatusEffects.ApplyTickingStatus(
                    victim,
                    StatusEffects.Burn,
                    ability.GetNumber(StatusDuration),
                    damage * ability.GetNumber(StatusStrength),
                    fire,
                    controller);
            });
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime, center, direction);
        return true;
    }

    private static void ConfigureCleansingWindFan()
    {
        CleansingWindFan.name_key = "Cultiway.ArtifactAbility.CleansingWindFan";
        CleansingWindFan.tags = ["active", "support", "cone", "purification", "dispel"];
        CleansingWindFan.exclusive_group = "cleansing_cone";
        CleansingWindFan.manifestation_cost = 0.95f;
        CleansingWindFan.synergy_tags = ["purification", "mobility", "support"];
        CleansingWindFan.conflict_tags = ["curse_field"];
        CleansingWindFan.minimum_score = 1f;
        CleansingWindFan.use_profile = new ArtifactUseProfile { defensive = 0.55f, support = 1f };
        CleansingWindFan.control_complexity = 0.28f;
        CleansingWindFan.thread_cost = 1;
        CleansingWindFan.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(ConeAngle), IntegerSpec(EffectCount),
            NumberSpec(EffectDuration), NumberSpec(Cooldown), NumberSpec(ActivationCost),
        ];
        CleansingWindFan.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Purification) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Mobility) * 0.34f +
             context.GetTrait(ArtifactMaterialTraits.Projection) * 0.25f +
             context.GetTrait(ArtifactMaterialTraits.Pos) * 0.16f);
        CleansingWindFan.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7f * context.scales.Range),
            ArtifactAbilityValue.Number(ConeAngle, Mathf.Clamp(84f * context.scales.Precision, 52f, 138f)),
            ArtifactAbilityValue.Integer(EffectCount, Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity), 1, 7)),
            ArtifactAbilityValue.Number(EffectDuration, 0.58f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 10f, 3f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 2.8f)),
        ];
        CleansingWindFan.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.CleansingWindFan.Description"),
            ability.GetNumber(AttackRange), ability.GetNumber(ConeAngle),
            ability.GetInteger(EffectCount), ability.GetNumber(Cooldown));
        CleansingWindFan.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
        });
        CleansingWindFan.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
            target_mode = ActiveAbilityTargetMode.ObjectOrPoint,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 5,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanUseWorldTargetInRange,
            TryUse = SweepCleansingWind,
        });
    }

    private static bool SweepCleansingWind(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = Controller(context);
        Vector2 center = controller.current_position;
        Vector2 direction = DirectionToTarget(context, target);
        int maxCount = ability.GetInteger(EffectCount);
        int changed = 0;
        ArtifactTargeting.ForEachActorInSector(
            controller,
            center,
            direction,
            ability.GetNumber(AttackRange),
            ability.GetNumber(ConeAngle),
            ArtifactTargeting.TargetDisposition.Friendly,
            actor => changed += ArtifactStatusEffects.CleanseNegativeStatuses(actor, maxCount));
        ArtifactTargeting.ForEachActorInSector(
            controller,
            center,
            direction,
            ability.GetNumber(AttackRange),
            ability.GetNumber(ConeAngle),
            ArtifactTargeting.TargetDisposition.Hostile,
            actor => changed += ArtifactStatusEffects.DispelPositiveStatuses(actor, maxCount));
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Cleanse,
            center,
            direction,
            intensity: Mathf.Max(1f, changed));
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime, center, direction);
        return true;
    }

    private static void ConfigureFanAbilityVisuals()
    {
        ConfigureGaleFanSweepVisuals();
        ConfigureDepartingFireFanVisuals();
        ConfigureCleansingWindFanVisuals();
    }

    private static void ConfigureGaleFanSweepVisuals()
    {
        ArtifactSectorVisualCue cone = AbilitySector(
            ArtifactVisualColorRole.Primary,
            ArtifactVfxStyles.Wind
        );
        ArtifactParticleVisualCue gust = Burst(
            ArtifactVisualAnchorKind.Point,
            ArtifactVisualColorRole.Glow,
            6,
            0.42f
        );
        gust.emission_interval = 0.075f;
        gust.directional_speed = 0.72f;
        ArtifactRibbonVisualCue ribbon = new ArtifactRibbonVisualCue
        {
            style_key = ArtifactVfxStyles.Wind,
            anchor = ArtifactVisualAnchorRef.Appearance("leaf", "edge"),
            color_role = ArtifactVisualColorRole.Primary,
            width = 0.1f,
            alpha = 0.48f,
            history = 0.34f,
            match_actor_scale = false,
        };
        GaleFanSweep.Visualize(
            Theme(SkillVfxElements.Wind.AccentColor)
                .Loop(
                    "gale_fan_sweep",
                    new ArtifactCompositeVisualCue(cone, gust, ribbon, Pulse(1.18f, 1f, 0.1f)),
                    IsActivityActive
                )
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactCompositeVisualCue(
                        AbilitySector(
                            ArtifactVisualColorRole.Glow,
                            ArtifactVfxStyles.Wind,
                            transient: true
                        ),
                        Burst(
                            ArtifactVisualAnchorKind.Artifact,
                            ArtifactVisualColorRole.Glow,
                            8,
                            0.45f
                        )
                    ),
                    0.5f
                )
        );
    }

    private static void ConfigureDepartingFireFanVisuals()
    {
        ArtifactSectorVisualCue flameCone = AbilitySector(
            ArtifactVisualColorRole.Primary,
            ArtifactVfxStyles.Fire
        );
        flameCone.fill_alpha = 0.14f;
        ArtifactAnimVisualCue flame = new ArtifactAnimVisualCue("effects/fx_status_burning_t_3")
        {
            anchor = ArtifactVisualAnchorRef.Appearance("leaf", "focus"),
            color_role = ArtifactVisualColorRole.Glow,
            scale = 0.07f,
            frame_interval = 0.07f,
            alpha = 0.9f,
            loop = true,
        };
        ArtifactParticleVisualCue embers = Burst(
            ArtifactVisualAnchorKind.Point,
            ArtifactVisualColorRole.Glow,
            7,
            0.38f
        );
        embers.emission_interval = 0.08f;
        embers.directional_speed = 0.58f;
        DepartingFireFan.Visualize(
            Theme(SkillVfxElements.Fire.AccentColor)
                .Loop(
                    "departing_fire_fan",
                    new ArtifactCompositeVisualCue(
                        flameCone,
                        flame,
                        embers,
                        Pulse(1.2f, 1f, 0.12f)
                    ),
                    IsActivityActive
                )
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactCompositeVisualCue(
                        AbilitySector(
                            ArtifactVisualColorRole.Glow,
                            ArtifactVfxStyles.Fire,
                            transient: true
                        ),
                        new ArtifactAudioVisualCue
                        {
                            anchor = ArtifactVisualAnchorKind.Artifact,
                            sound = "event:/SFX/WEAPONS/WeaponFireballStart",
                        }
                    ),
                    0.6f
                )
        );
    }

    private static void ConfigureCleansingWindFanVisuals()
    {
        ArtifactSectorVisualCue breeze = AbilitySector(
            ArtifactVisualColorRole.Glow,
            ArtifactVfxStyles.Purification
        );
        breeze.fill_alpha = 0.055f;
        ArtifactParticleVisualCue motes = Burst(
            ArtifactVisualAnchorKind.Point,
            ArtifactVisualColorRole.Glow,
            5,
            0.5f
        );
        motes.emission_interval = 0.1f;
        motes.directional_speed = 0.42f;
        CleansingWindFan.Visualize(
            Theme(SkillVfxElements.Pos.AccentColor)
                .Loop(
                    "cleansing_wind_fan",
                    new ArtifactCompositeVisualCue(breeze, motes),
                    IsActivityActive
                )
                .Signal(
                    ArtifactVisualChannels.Cleanse,
                    new ArtifactCompositeVisualCue(
                        AbilitySector(
                            ArtifactVisualColorRole.Primary,
                            ArtifactVfxStyles.Purification,
                            transient: true
                        ),
                        Burst(
                            ArtifactVisualAnchorKind.Point,
                            ArtifactVisualColorRole.Glow,
                            12,
                            0.62f
                        )
                    ),
                    0.62f,
                    "artifact.cleansing_wind",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }
}

using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
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
    private const string ArmorBonus = "armor_bonus";

    /// <summary>可部署镇狱法域；周期性囚禁范围内敌人并造成伤害。</summary>
    public static ArtifactAbilityAsset TreasureTowerPrison { get; private set; }
    /// <summary>可部署多层结界；持续为范围内友军施加层界守护状态。</summary>
    public static ArtifactAbilityAsset LayeredRealmWard { get; private set; }
    /// <summary>可部署塔影法域；周期性伤害范围内敌人并施加减速。</summary>
    public static ArtifactAbilityAsset TowerShadowProjection { get; private set; }

    private static void ConfigureTreasureTowerPrison()
    {
        TreasureTowerPrison.name_key = "Cultiway.ArtifactAbility.TreasureTowerPrison";
        TreasureTowerPrison.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Effect.Imprisonment,
            ArtifactSemantics.Delivery.Field);
        TreasureTowerPrison.exclusivity = ArtifactAbilityExclusivity.ImprisonmentField;
        TreasureTowerPrison.manifestation_cost = 1.65f;
        TreasureTowerPrison.AddSynergies(
            ArtifactSemantics.Effect.Sealing,
            ArtifactSemantics.Theme.Space,
            ArtifactSemantics.Effect.Suppression);
        TreasureTowerPrison.AddConflicts(ArtifactSemanticRules.MobilityField);
        TreasureTowerPrison.minimum_score = 1f;
        TreasureTowerPrison.use_profile = new ArtifactUseProfile { offensive = 0.85f, defensive = 0.3f };
        TreasureTowerPrison.control_complexity = 0.62f;
        TreasureTowerPrison.thread_cost = 2;
        TreasureTowerPrison.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectRadius), NumberSpec(EffectDuration),
            NumberSpec(StatusDuration), NumberSpec(DamageMultiplier), NumberSpec(Cooldown),
            NumberSpec(ActivationCost), NumberSpec(MaintenanceCost),
        ];
        TreasureTowerPrison.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Sealing) +
             context.GetTrait(ArtifactMaterialTraits.Space) * 0.5f) *
            (0.52f + context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.4f +
             context.GetTrait(ArtifactMaterialTraits.Hardness) * 0.2f +
             context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.24f);
        TreasureTowerPrison.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7.5f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 2.7f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 5.2f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusDuration, 0.9f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.11f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 17f, 5.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4.8f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.52f)),
        ];
        TreasureTowerPrison.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.TreasureTowerPrison.Description"),
            ability.GetNumber(EffectRadius), ability.GetNumber(StatusDuration),
            ability.GetNumber(DamageMultiplier), ability.GetNumber(EffectDuration));
        TreasureTowerPrison.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.45f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = UseWakan,
            OnTick = ApplyTreasureTowerPrison,
        });
        TreasureTowerPrison.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 8,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployTreasureTowerPrison,
        });
    }

    private static bool DeployTreasureTowerPrison(
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
            ArtifactBodyAnchorRef.Appearance("base", "center", ArtifactBodyAnchorKind.Center));
    }

    private static void ApplyTreasureTowerPrison(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float __)
    {
        Actor controller = Controller(context);
        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector2 center = ArtifactManifestationTools.ResolveWorldAnchor(
            context.artifact,
            deployment.ResolveBodyAnchor());
        ArtifactTargeting.ForEachHostile(controller, center, ability.GetNumber(EffectRadius), target =>
        {
            ArtifactStatusEffects.ApplyImprisonment(target, ability.GetNumber(StatusDuration), controller);
            ArtifactDamageEffects.DealDamage(
                controller,
                target,
                SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
                MaterialComposition(context));
        });
    }

    private static void ConfigureLayeredRealmWard()
    {
        LayeredRealmWard.name_key = "Cultiway.ArtifactAbility.LayeredRealmWard";
        LayeredRealmWard.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Effect.Ward,
            ArtifactSemantics.Delivery.Field);
        LayeredRealmWard.exclusivity = ArtifactAbilityExclusivity.LayeredWardField;
        LayeredRealmWard.manifestation_cost = 1.35f;
        LayeredRealmWard.AddSynergies(
            ArtifactSemantics.Effect.Ward,
            ArtifactSemantics.Form.Sustain,
            ArtifactSemantics.Material.Hardness);
        LayeredRealmWard.AddConflicts(ArtifactSemanticRules.Concealment);
        LayeredRealmWard.minimum_score = 1f;
        LayeredRealmWard.use_profile = new ArtifactUseProfile { defensive = 0.9f, support = 0.85f };
        LayeredRealmWard.control_complexity = 0.45f;
        LayeredRealmWard.thread_cost = 1;
        LayeredRealmWard.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectRadius), NumberSpec(EffectDuration),
            NumberSpec(StatusDuration), NumberSpec(ArmorBonus), NumberSpec(Cooldown),
            NumberSpec(ActivationCost), NumberSpec(MaintenanceCost),
        ];
        LayeredRealmWard.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Ward) *
            (0.58f + context.GetTrait(ArtifactMaterialTraits.Hardness) * 0.3f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.32f +
             context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.18f);
        LayeredRealmWard.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 3.2f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 7f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusDuration, 1.35f * context.scales.Duration),
            ArtifactAbilityValue.Number(ArmorBonus, Mathf.Clamp(0.9f * context.scales.Potency, 0.8f, 6f)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 16f, 5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.4f)),
        ];
        LayeredRealmWard.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.LayeredRealmWard.Description"),
            ability.GetNumber(EffectRadius), ability.GetNumber(ArmorBonus),
            ability.GetNumber(EffectDuration), ability.GetNumber(Cooldown));
        LayeredRealmWard.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
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
            OnTick = ApplyLayeredRealmWard,
        });
        LayeredRealmWard.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 5,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployLayeredRealmWard,
        });
    }

    private static bool DeployLayeredRealmWard(
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
            ArtifactBodyAnchorRef.Appearance("base", "center", ArtifactBodyAnchorKind.Center));
    }

    private static void ApplyLayeredRealmWard(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float __)
    {
        Actor controller = Controller(context);
        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector2 center = ArtifactManifestationTools.ResolveWorldAnchor(
            context.artifact,
            deployment.ResolveBodyAnchor());
        ArtifactTargeting.ForEachFriendly(controller, center, ability.GetNumber(EffectRadius), target =>
            ArtifactStatusEffects.ApplyStatus(
                target,
                StatusEffects.LayeredWard,
                ability.GetNumber(StatusDuration),
                S.armor,
                ability.GetNumber(ArmorBonus),
                controller));
    }

    private static void ConfigureTowerShadowProjection()
    {
        TowerShadowProjection.name_key = "Cultiway.ArtifactAbility.TowerShadowProjection";
        TowerShadowProjection.SetSemantics(
            ArtifactSemantics.Delivery.Projection,
            ArtifactSemantics.Delivery.Field,
            ArtifactSemantics.Effect.Suppression);
        TowerShadowProjection.exclusivity = ArtifactAbilityExclusivity.RemoteProjection;
        TowerShadowProjection.manifestation_cost = 1.4f;
        TowerShadowProjection.AddSynergies(
            ArtifactSemantics.Delivery.Projection,
            ArtifactSemantics.Theme.Space,
            ArtifactSemantics.Effect.Suppression);
        TowerShadowProjection.AddConflicts(ArtifactSemanticRules.BodyDeployment);
        TowerShadowProjection.minimum_score = 1f;
        TowerShadowProjection.use_profile = new ArtifactUseProfile { offensive = 0.75f, defensive = 0.3f };
        TowerShadowProjection.control_complexity = 0.56f;
        TowerShadowProjection.thread_cost = 2;
        TowerShadowProjection.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectRadius), NumberSpec(EffectDuration),
            NumberSpec(DamageMultiplier), NumberSpec(StatusDuration), NumberSpec(Cooldown),
            NumberSpec(ActivationCost), NumberSpec(MaintenanceCost),
        ];
        TowerShadowProjection.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Projection) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Space) * 0.36f +
             context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.3f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.15f);
        TowerShadowProjection.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 10f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 2.5f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 5.5f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.14f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 1.1f * context.scales.Duration),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 15f, 4.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4.2f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.45f)),
        ];
        TowerShadowProjection.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.TowerShadowProjection.Description"),
            ability.GetNumber(AttackRange), ability.GetNumber(EffectRadius),
            ability.GetNumber(DamageMultiplier), ability.GetNumber(EffectDuration));
        TowerShadowProjection.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
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
            OnTick = ApplyTowerShadowProjection,
        });
        TowerShadowProjection.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanUse = CanUseWorldTargetInRange,
            TryUse = ProjectTowerShadow,
        });
    }

    private static bool ProjectTowerShadow(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance _,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin __)
    {
        Vector3 point = TargetPosition(target);
        ArtifactAbilityLifecycle.BeginTimedActivity(
            ref runtime,
            point,
            point - (Vector3)Controller(context).current_position);
        return true;
    }

    private static void ApplyTowerShadowProjection(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        float _)
    {
        Actor controller = Controller(context);
        Vector2 center = runtime.activity_position;
        ArtifactTargeting.ForEachHostile(controller, center, ability.GetNumber(EffectRadius), target =>
        {
            ArtifactDamageEffects.DealDamage(
                controller,
                target,
                SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
                MaterialComposition(context));
            ArtifactStatusEffects.ApplyStatus(
                target,
                StatusEffects.Slow,
                ability.GetNumber(StatusDuration),
                controller);
        });
    }

    private static void ConfigureTowerAbilityVisuals()
    {
        ConfigureTreasureTowerPrisonVisuals();
        ConfigureLayeredRealmWardVisuals();
        ConfigureTowerShadowProjectionVisuals();
    }

    private static void ConfigureTreasureTowerPrisonVisuals()
    {
        ArtifactAreaVisualCue prison = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.62f,
            0.09f,
            ArtifactVfxStyles.Prison
        );
        prison.show_inner_ring = true;
        prison.inner_radius_ratio = 0.48f;
        prison.inner_rotation_speed = -30f;
        ArtifactGlyphVisualCue seal = ActivityGlyph(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius) * 0.82f,
            ArtifactVisualColorRole.Secondary,
            8,
            18f,
            ArtifactVfxStyles.Prison
        );
        ArtifactParticleVisualCue fragments = Burst(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            ArtifactVisualColorRole.Glow,
            3,
            0.46f
        );
        fragments.emission_interval = 0.18f;
        TreasureTowerPrison.Visualize(
            Theme(SkillVfxElements.Entropy.AccentColor)
                .Loop(
                    "treasure_tower_prison",
                    new ArtifactCompositeVisualCue(prison, seal, fragments, Pulse(1f, 1f, 0.07f)),
                    IsDeploymentActive
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    Burst(
                        ArtifactVisualAnchorKind.DeploymentOrigin,
                        ArtifactVisualColorRole.Glow,
                        4
                    ),
                    0.3f,
                    "artifact.treasure_tower_prison.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    new ArtifactImpactVisualCue
                    {
                        anchor = ArtifactVisualAnchorKind.Point,
                        sound = "event:/SFX/WEAPONS/WeaponRockLand",
                        shake_intensity = 0.11f,
                    },
                    0.3f
                )
        );
    }

    private static void ConfigureLayeredRealmWardVisuals()
    {
        ArtifactAreaVisualCue outer = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.52f,
            0.035f,
            ArtifactVfxStyles.Ward
        );
        outer.show_inner_ring = true;
        outer.inner_radius_ratio = 0.72f;
        outer.inner_rotation_speed = 22f;
        ArtifactAreaVisualCue inner = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius) * 0.48f,
            ArtifactVisualColorRole.Glow,
            0.36f,
            0.02f,
            ArtifactVfxStyles.Ward
        );
        inner.inner_rotation_speed = -28f;
        ArtifactGlyphVisualCue ward = ActivityGlyph(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius) * 0.86f,
            ArtifactVisualColorRole.Secondary,
            9,
            10f,
            ArtifactVfxStyles.Ward
        );
        LayeredRealmWard.Visualize(
            Theme(SkillVfxElements.Earth.AccentColor)
                .Loop(
                    "layered_realm_ward",
                    new ArtifactCompositeVisualCue(outer, inner, ward),
                    IsDeploymentActive
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    Burst(
                        ArtifactVisualAnchorKind.DeploymentOrigin,
                        ArtifactVisualColorRole.Glow,
                        3
                    ),
                    0.24f,
                    "artifact.layered_realm_ward.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureTowerShadowProjectionVisuals()
    {
        ArtifactProjectionVisualCue shadow = new ArtifactProjectionVisualCue
        {
            anchor = ArtifactVisualAnchorKind.Point,
            color_role = ArtifactVisualColorRole.Glow,
            scale = 1.45f,
            alpha = 0.48f,
            start_scale = 1f,
            end_scale = 1f,
            fade_out = false,
            pulse_amplitude = 0.075f,
            pulse_speed = 4.5f,
            match_actor_scale = false,
        };
        ArtifactAreaVisualCue field = Area(
            ArtifactVisualAnchorKind.Point,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.48f,
            0.065f,
            ArtifactVfxStyles.Suppression
        );
        field.inner_rotation_speed = -32f;
        ArtifactGlyphVisualCue glyph = ActivityGlyph(
            ArtifactVisualAnchorKind.Point,
            context => context.ability.GetNumber(EffectRadius) * 0.78f,
            ArtifactVisualColorRole.Secondary,
            8,
            -20f,
            ArtifactVfxStyles.Suppression
        );
        TowerShadowProjection.Visualize(
            Theme(SkillVfxElements.Neg.AccentColor)
                .Loop(
                    "tower_shadow_projection",
                    new ArtifactCompositeVisualCue(shadow, field, glyph),
                    IsActivityActive
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    new ArtifactCompositeVisualCue(
                        Burst(ArtifactVisualAnchorKind.Point, ArtifactVisualColorRole.Glow, 5),
                        ExpandingArea(
                            ArtifactVisualAnchorKind.Point,
                            context => context.ability.GetNumber(EffectRadius),
                            ArtifactVisualColorRole.Primary,
                            ArtifactVfxStyles.Suppression
                        )
                    ),
                    0.34f,
                    "artifact.tower_shadow.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }
}

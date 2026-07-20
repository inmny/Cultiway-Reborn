using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using strings;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private const string ReleasePerTick = "release_per_tick";
    private const string StoredPowerBonus = "stored_power_bonus";

    /// <summary>可部署吞摄法域；伤害、抽灵并牵引敌人，同时把吸收的力量储入葫芦。</summary>
    public static ArtifactAbilityAsset HeavenSwallowingGourd { get; private set; }
    /// <summary>灵气缓冲被动；储存持有者的过量灵气，并在低于阈值时自动返还。</summary>
    public static ArtifactAbilityAsset SpiritGourdReserve { get; private set; }
    /// <summary>扇形倾泻主动；消耗已储力量造成元素伤害，并按材料构成附加燃烧、毒、减速、眩晕或破甲。</summary>
    public static ArtifactAbilityAsset FiveEssenceOutpouring { get; private set; }

    private static void ConfigureHeavenSwallowingGourd()
    {
        HeavenSwallowingGourd.name_key = "Cultiway.ArtifactAbility.HeavenSwallowingGourd";
        HeavenSwallowingGourd.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Effect.Devouring,
            ArtifactSemantics.Effect.Storage);
        HeavenSwallowingGourd.exclusivity = ArtifactAbilityExclusivity.DevouringField;
        HeavenSwallowingGourd.manifestation_cost = 1.45f;
        HeavenSwallowingGourd.AddSynergies(
            ArtifactSemantics.Effect.Devouring,
            ArtifactSemantics.Effect.Storage,
            ArtifactSemantics.Theme.Space);
        HeavenSwallowingGourd.AddConflicts(ArtifactSemanticRules.PurificationField);
        HeavenSwallowingGourd.minimum_score = 1f;
        HeavenSwallowingGourd.use_profile = new ArtifactUseProfile { offensive = 0.8f, support = 0.3f };
        HeavenSwallowingGourd.control_complexity = 0.52f;
        HeavenSwallowingGourd.thread_cost = 2;
        HeavenSwallowingGourd.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(EffectRadius), NumberSpec(EffectDuration),
            NumberSpec(DamageMultiplier), NumberSpec(DrainAmount), NumberSpec(ForceStrength),
            NumberSpec(StorageCapacity), NumberSpec(StorePerTrigger), NumberSpec(Cooldown),
            NumberSpec(ActivationCost), NumberSpec(MaintenanceCost),
        ];
        HeavenSwallowingGourd.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Devouring) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Storage) * 0.38f +
             context.GetTrait(ArtifactMaterialTraits.Space) * 0.3f +
             context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.22f);
        HeavenSwallowingGourd.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7.5f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 3.3f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 6f * context.scales.Duration),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.12f * context.scales.Potency),
            ArtifactAbilityValue.Number(DrainAmount, 1.4f * context.scales.Potency),
            ArtifactAbilityValue.Number(ForceStrength, 0.46f * context.scales.Potency),
            ArtifactAbilityValue.Number(StorageCapacity, 12f * context.scales.Capacity),
            ArtifactAbilityValue.Number(StorePerTrigger, 0.65f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 16f, 5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 4.2f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.44f)),
        ];
        HeavenSwallowingGourd.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.HeavenSwallowingGourd.Description"),
            ability.GetNumber(EffectRadius), ability.GetNumber(DrainAmount),
            ability.GetNumber(StorageCapacity), ability.GetNumber(EffectDuration));
        HeavenSwallowingGourd.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
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
            OnAttached = ConfigureAbsorbedPowerStorage,
            OnTick = ApplyHeavenSwallowingField,
        });
        HeavenSwallowingGourd.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployHeavenSwallowingGourd,
        });
    }

    private static bool DeployHeavenSwallowingGourd(
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
            ArtifactBodyAnchorRef.Appearance("mouth", "focus", ArtifactBodyAnchorKind.ForwardTip));
    }

    private static void ConfigureAbsorbedPowerStorage(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _)
    {
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        ArtifactStorageOperations.Configure(
            ref storage,
            ArtifactStorageOperations.AbsorbedPower,
            ability.GetNumber(StorageCapacity));
    }

    private static void ApplyHeavenSwallowingField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        float _)
    {
        Actor controller = Controller(context);
        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector2 center = ArtifactManifestationTools.ResolveWorldAnchor(
            context.artifact,
            deployment.ResolveBodyAnchor());
        float absorbedPower = 0f;
        BaseSimObject lastTarget = null;
        CombatTargeting.ForEachHostile(controller, center, ability.GetNumber(EffectRadius), target =>
        {
            CombatDamageEffects.DealDamage(
                controller,
                target,
                SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier),
                MaterialComposition(context));
            float drained = CombatResourceEffects.DrainWakan(target, ability.GetNumber(DrainAmount));
            absorbedPower += Mathf.Max(drained, ability.GetNumber(StorePerTrigger));
            lastTarget = target;
            CombatForceEffects.ApplyRadialForce(
                controller,
                target,
                center,
                ability.GetNumber(ForceStrength),
                pull: true);
        });
        if (absorbedPower <= 0f) return;
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        float stored = ArtifactStorageOperations.Store(
            ref storage,
            ArtifactStorageOperations.AbsorbedPower,
            absorbedPower);
        if (stored <= 0f) return;
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Drain,
            center,
            target: lastTarget,
            intensity: stored);
    }

    private static void ConfigureSpiritGourdReserve()
    {
        SpiritGourdReserve.name_key = "Cultiway.ArtifactAbility.SpiritGourdReserve";
        SpiritGourdReserve.SetSemantics(ArtifactSemantics.Effect.Storage, ArtifactSemantics.Resource.Reserve);
        SpiritGourdReserve.exclusivity = ArtifactAbilityExclusivity.WakanBuffer;
        SpiritGourdReserve.manifestation_cost = 0.85f;
        SpiritGourdReserve.AddSynergies(
            ArtifactSemantics.Effect.Storage,
            ArtifactSemantics.Form.Sustain,
            ArtifactSemantics.Resource.Reserve);
        SpiritGourdReserve.minimum_score = 1f;
        SpiritGourdReserve.use_profile = new ArtifactUseProfile { support = 0.55f, cultivate = 0.8f };
        SpiritGourdReserve.control_complexity = 0.18f;
        SpiritGourdReserve.parameter_schema =
        [
            NumberSpec(StorageCapacity), NumberSpec(StorePerTrigger), NumberSpec(ReleasePerTick),
            NumberSpec(RestoreThreshold),
        ];
        SpiritGourdReserve.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Storage) *
            (0.6f + context.GetTrait(ArtifactMaterialTraits.Capacity) * 0.32f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.22f +
             context.GetTrait(ArtifactMaterialTraits.Sustain) * 0.18f);
        SpiritGourdReserve.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(StorageCapacity, 18f * context.scales.Capacity),
            ArtifactAbilityValue.Number(StorePerTrigger, 1.6f * context.scales.Efficiency),
            ArtifactAbilityValue.Number(ReleasePerTick, 2.2f * context.scales.Efficiency),
            ArtifactAbilityValue.Number(RestoreThreshold, Mathf.Clamp(0.36f * context.scales.Precision, 0.3f, 0.65f)),
        ];
        SpiritGourdReserve.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SpiritGourdReserve.Description"),
            ability.GetNumber(StorageCapacity), ability.GetNumber(StorePerTrigger),
            ability.GetNumber(ReleasePerTick), ability.GetNumber(RestoreThreshold));
        SpiritGourdReserve.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            tick_minimum_state = ArtifactControlState.Ready,
            tick_interval = 0.5f,
            OnAttached = ConfigureWakanStorage,
            CanTick = CanBalanceSpiritGourdReserve,
            OnTick = BalanceSpiritGourdReserve,
        });
    }

    private static void ConfigureWakanStorage(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _)
    {
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        ArtifactStorageOperations.Configure(ref storage, ArtifactStorageOperations.Wakan, ability.GetNumber(StorageCapacity));
    }

    private static bool CanBalanceSpiritGourdReserve(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry _)
    {
        ActorExtend controller = context.controller.GetComponent<ActorBinder>().AE;
        if (!controller.HasCultisys<Xian>()) return false;
        ref Xian xian = ref controller.GetCultisys<Xian>();
        float capacity = Mathf.Max(1f, controller.Base.stats[BaseStatses.MaxWakan.id]);
        ArtifactStorageState storage = context.artifact.GetComponent<ArtifactStorageState>();
        float amount = ArtifactStorageOperations.GetAmount(storage, ArtifactStorageOperations.Wakan);
        float storedCapacity = ArtifactStorageOperations.GetCapacity(storage, ArtifactStorageOperations.Wakan);
        return xian.wakan > capacity * 0.78f && amount < storedCapacity ||
               xian.wakan < capacity * ability.GetNumber(RestoreThreshold) && amount > 0f;
    }

    private static void BalanceSpiritGourdReserve(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float __)
    {
        ActorExtend controller = context.controller.GetComponent<ActorBinder>().AE;
        ref Xian xian = ref controller.GetCultisys<Xian>();
        float capacity = Mathf.Max(1f, controller.Base.stats[BaseStatses.MaxWakan.id]);
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        if (xian.wakan > capacity * 0.78f)
        {
            float available = xian.wakan - capacity * 0.78f;
            float moved = ArtifactStorageOperations.Store(
                ref storage,
                ArtifactStorageOperations.Wakan,
                Mathf.Min(available, ability.GetNumber(StorePerTrigger)));
            xian.wakan -= moved;
            return;
        }

        float required = capacity * ability.GetNumber(RestoreThreshold) - xian.wakan;
        float taken = ArtifactStorageOperations.Take(
            ref storage,
            ArtifactStorageOperations.Wakan,
            Mathf.Min(required, ability.GetNumber(ReleasePerTick)));
        float restored = CombatResourceEffects.RestoreWakan(controller.Base, taken);
        if (restored < taken)
        {
            ArtifactStorageOperations.Store(ref storage, ArtifactStorageOperations.Wakan, taken - restored);
        }
    }

    private static void ConfigureFiveEssenceOutpouring()
    {
        FiveEssenceOutpouring.name_key = "Cultiway.ArtifactAbility.FiveEssenceOutpouring";
        FiveEssenceOutpouring.SetSemantics(
            ArtifactSemantics.Form.Cone,
            ArtifactSemantics.Theme.Elemental,
            ArtifactSemantics.Effect.Release);
        FiveEssenceOutpouring.exclusivity = ArtifactAbilityExclusivity.ElementalCone;
        FiveEssenceOutpouring.manifestation_cost = 1.15f;
        FiveEssenceOutpouring.AddSynergies(
            ArtifactSemantics.Effect.Storage,
            ArtifactSemantics.Theme.Elemental,
            ArtifactSemantics.Effect.Release);
        FiveEssenceOutpouring.minimum_score = 1f;
        FiveEssenceOutpouring.use_profile = new ArtifactUseProfile { offensive = 0.9f };
        FiveEssenceOutpouring.control_complexity = 0.36f;
        FiveEssenceOutpouring.thread_cost = 1;
        FiveEssenceOutpouring.parameter_schema =
        [
            NumberSpec(AttackRange), NumberSpec(ConeAngle), NumberSpec(DamageMultiplier),
            NumberSpec(StatusDuration), NumberSpec(StorageCapacity), NumberSpec(StoredResourceCost),
            NumberSpec(StoredPowerBonus), NumberSpec(EffectDuration), NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        FiveEssenceOutpouring.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Projection) +
             context.GetTrait(ArtifactMaterialTraits.Storage) * 0.42f) *
            (0.52f + context.GetTrait(ArtifactMaterialTraits.Volatility) * 0.32f +
             context.GetTrait(ArtifactMaterialTraits.Spirituality) * 0.16f);
        FiveEssenceOutpouring.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 6.5f * context.scales.Range),
            ArtifactAbilityValue.Number(ConeAngle, Mathf.Clamp(62f * context.scales.Precision, 42f, 118f)),
            ArtifactAbilityValue.Number(DamageMultiplier, 0.72f * context.scales.Potency),
            ArtifactAbilityValue.Number(StatusDuration, 2.2f * context.scales.Duration),
            ArtifactAbilityValue.Number(StorageCapacity, 8f * context.scales.Capacity),
            ArtifactAbilityValue.Number(StoredResourceCost, 3f),
            ArtifactAbilityValue.Number(StoredPowerBonus, 0.16f * context.scales.Potency),
            ArtifactAbilityValue.Number(EffectDuration, 0.55f),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 8.5f, 2.4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3.2f)),
        ];
        FiveEssenceOutpouring.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.FiveEssenceOutpouring.Description"),
            ability.GetNumber(AttackRange), ability.GetNumber(ConeAngle),
            ability.GetNumber(DamageMultiplier), ability.GetNumber(StoredPowerBonus));
        FiveEssenceOutpouring.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
            OnAttached = ConfigureAbsorbedPowerStorage,
        });
        FiveEssenceOutpouring.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.ObjectOrPoint,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanUseWorldTargetInRange,
            TryUse = ReleaseFiveEssence,
        });
    }

    private static bool ReleaseFiveEssence(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = Controller(context);
        Vector2 center = controller.current_position;
        Vector2 direction = DirectionToTarget(context, target);
        ref ArtifactStorageState storage = ref context.artifact.GetComponent<ArtifactStorageState>();
        float stored = ArtifactStorageOperations.Take(
            ref storage,
            ArtifactStorageOperations.AbsorbedPower,
            ability.GetNumber(StoredResourceCost));
        float damage = SkillContext.DefaultStrength *
                       (ability.GetNumber(DamageMultiplier) + stored * ability.GetNumber(StoredPowerBonus));
        ElementComposition composition = MaterialComposition(context);
        ArtifactMaterialData material = context.artifact.GetComponent<ArtifactMaterialData>();
        CombatTargeting.ForEachActorInSector(
            controller,
            center,
            direction,
            ability.GetNumber(AttackRange),
            ability.GetNumber(ConeAngle),
            CombatTargeting.TargetDisposition.Hostile,
            victim =>
            {
                CombatDamageEffects.DealDamage(controller, victim, damage, composition);
                ApplyFiveEssenceStatus(controller, victim, material, ability.GetNumber(StatusDuration), damage * 0.16f);
            });
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime, center, direction);
        return true;
    }

    private static void ApplyFiveEssenceStatus(
        Actor source,
        Actor target,
        ArtifactMaterialData material,
        float duration,
        float tickDamage)
    {
        float fire = material.GetTrait(ArtifactMaterialTraits.Fire);
        float wood = material.GetTrait(ArtifactMaterialTraits.Wood);
        float water = material.GetTrait(ArtifactMaterialTraits.Water);
        float earth = material.GetTrait(ArtifactMaterialTraits.Earth);
        float iron = material.GetTrait(ArtifactMaterialTraits.Iron);
        float maximum = Mathf.Max(fire, wood, water, earth, iron);
        if (maximum <= 0f) return;
        if (maximum == fire)
        {
            CombatStatusEffects.ApplyTickingStatus(
                target,
                StatusEffects.Burn,
                duration,
                tickDamage,
                new ElementComposition(fire: 1f),
                source);
        }
        else if (maximum == wood)
        {
            CombatStatusEffects.ApplyTickingStatus(
                target,
                StatusEffects.Poison,
                duration,
                tickDamage,
                new ElementComposition(wood: 1f),
                source);
        }
        else if (maximum == water)
        {
            CombatStatusEffects.ApplyStatus(target, StatusEffects.Slow, duration, source);
        }
        else if (maximum == earth)
        {
            CombatStatusEffects.ApplyStatus(target, StatusEffects.Daze, duration * 0.35f, source);
        }
        else
        {
            CombatStatusEffects.ApplyStatus(target, StatusEffects.ArmorBreak, duration, source);
        }
    }

    private static void ConfigureGourdAbilityVisuals()
    {
        ConfigureHeavenSwallowingGourdVisuals();
        ConfigureSpiritGourdReserveVisuals();
        ConfigureFiveEssenceOutpouringVisuals();
    }

    private static void ConfigureHeavenSwallowingGourdVisuals()
    {
        ArtifactAreaVisualCue vortex = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.48f,
            0.08f,
            ArtifactVfxStyles.Devouring
        );
        vortex.inner_rotation_speed = -46f;
        vortex.pulse_speed = 5f;
        ArtifactGlyphVisualCue glyph = ActivityGlyph(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius) * 0.78f,
            ArtifactVisualColorRole.Secondary,
            7,
            -38f,
            ArtifactVfxStyles.Devouring
        );
        ArtifactParticleVisualCue inward = Burst(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            ArtifactVisualColorRole.Glow,
            3,
            0.56f
        );
        inward.emission_interval = 0.12f;
        inward.directional_speed = -0.22f;
        ArtifactTetherVisualCue drain = new ArtifactTetherVisualCue
        {
            style_key = ArtifactVfxStyles.Devouring,
            from = ArtifactVisualAnchorRef.Appearance(
                "mouth",
                "focus",
                ArtifactBodyAnchorKind.ForwardTip
            ),
            to = ArtifactVisualAnchorKind.Target,
            color_role = ArtifactVisualColorRole.Glow,
            width = 0.055f,
            curvature = -0.28f,
            wave_amplitude = 0.07f,
            wave_speed = -6f,
            match_actor_scale = false,
        };
        HeavenSwallowingGourd.Visualize(
            Theme(SkillVfxElements.Entropy.AccentColor)
                .Loop(
                    "heaven_swallowing_gourd",
                    new ArtifactCompositeVisualCue(vortex, glyph, inward, Pulse(1f, 1f, 0.1f)),
                    IsDeploymentActive
                )
                .Signal(
                    ArtifactVisualChannels.Drain,
                    new ArtifactCompositeVisualCue(
                        drain,
                        Burst(ArtifactVisualAnchorKind.Target, ArtifactVisualColorRole.Primary, 5)
                    ),
                    0.42f,
                    "artifact.heaven_swallowing.drain",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureSpiritGourdReserveVisuals()
    {
        ArtifactGlyphVisualCue reserve = ActivityGlyph(
            ArtifactVisualAnchorKind.Artifact,
            context => 0.42f + ResolveStoredRatio(context, ArtifactStorageOperations.Wakan) * 0.28f,
            ArtifactVisualColorRole.Glow,
            8,
            24f,
            ArtifactVfxStyles.Spirit
        );
        SpiritGourdReserve.Visualize(
            Theme(SkillVfxElements.Water.AccentColor)
                .Loop(
                    "spirit_gourd_reserve",
                    reserve,
                    context => ResolveStoredRatio(context, ArtifactStorageOperations.Wakan) > 0.02f,
                    "artifact.spirit_gourd_reserve",
                    ArtifactVisualStackPolicy.Strongest,
                    context => ResolveStoredRatio(context, ArtifactStorageOperations.Wakan)
                )
                .Signal(
                    ArtifactVisualChannels.Tick,
                    Burst(
                        ArtifactVisualAnchorKind.Artifact,
                        ArtifactVisualColorRole.Glow,
                        3,
                        0.25f
                    ),
                    0.25f,
                    "artifact.spirit_gourd_reserve.tick",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureFiveEssenceOutpouringVisuals()
    {
        ArtifactSectorVisualCue cone = AbilitySector(
            ArtifactVisualColorRole.Primary,
            ArtifactVfxStyles.Fire
        );
        ArtifactParticleVisualCue spray = Burst(
            ArtifactVisualAnchorKind.Point,
            ArtifactVisualColorRole.Glow,
            6,
            0.36f
        );
        spray.emission_interval = 0.09f;
        spray.directional_speed = 0.48f;
        FiveEssenceOutpouring.Visualize(
            Theme(SkillVfxElements.Fire.AccentColor)
                .Loop(
                    "five_essence_outpouring",
                    new ArtifactCompositeVisualCue(cone, spray, Pulse(1.15f, 1f, 0.12f)),
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
                        Burst(ArtifactVisualAnchorKind.Artifact, ArtifactVisualColorRole.Primary, 8)
                    ),
                    0.55f
                )
        );
    }
}

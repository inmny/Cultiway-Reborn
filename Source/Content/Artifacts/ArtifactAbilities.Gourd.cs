using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Usage;
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
            Resource = SkillCastResources.Wakan,
            OnAttached = ConfigureAbsorbedPowerStorage,
            OnTick = ApplyHeavenSwallowingField,
            OnActivityEnded = EndArtifactConstruction,
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
        ref ArtifactAbilityRuntimeEntry _,
        float interval)
    {
        var artifact = context.artifact;
        float drain = ability.GetNumber(DrainAmount) * context.effect_scale;
        float minimum = ability.GetNumber(StorePerTrigger) * context.effect_scale;
        float force = ability.GetNumber(ForceStrength) * context.effect_scale;
        var carrier = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, target) =>
        {
            using var scope = SkillCasterContextService.Enter(in carrier);
            float taken = CombatResourceEffects.DrainWakan(target, drain);
            ref ArtifactStorageState storage = ref artifact.GetComponent<ArtifactStorageState>();
            ArtifactStorageOperations.Store(ref storage, ArtifactStorageOperations.AbsorbedPower, Mathf.Max(taken, minimum));
        }, damageFirst: false, force: -force);
        StartPaidArtifactArea(context, HeavenSwallowingGourd, ability, interval, "gourd_vortex", contact,
            MaterialComposition(context), SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier), spin: -84f);
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
        ArtifactStorageOperations.Configure(ref storage, ArtifactStorageOperations.Wakan,
            ability.GetNumber(StorageCapacity));
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
        ref ArtifactAbilityRuntimeEntry runtime,
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
                Mathf.Min(available, ability.GetNumber(StorePerTrigger) * context.effect_scale));
            float spent = WakanResourceService.Spend(controller, ref xian, moved);
            if (spent > 0f)
                ArtifactAbilityVisuals.Emit(context, ability, runtime, ArtifactVisualChannels.Store,
                    intensity: spent / ability.GetNumber(StorePerTrigger));
            return;
        }

        float required = capacity * ability.GetNumber(RestoreThreshold) - xian.wakan;
        float taken = ArtifactStorageOperations.Take(
            ref storage,
            ArtifactStorageOperations.Wakan,
            Mathf.Min(required, ability.GetNumber(ReleasePerTick) * context.effect_scale));
        float restored = CombatResourceEffects.RestoreWakan(controller.Base, taken);
        if (restored < taken)
        {
            ArtifactStorageOperations.Store(ref storage, ArtifactStorageOperations.Wakan, taken - restored);
        }
        if (restored > 0f)
            ArtifactAbilityVisuals.Emit(context, ability, runtime, ArtifactVisualChannels.Restore,
                intensity: restored / ability.GetNumber(ReleasePerTick));
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
            Resource = SkillCastResources.Wakan,
            OnAttached = ConfigureAbsorbedPowerStorage,
            OnActivityEnded = EndArtifactConstruction,
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
        ActiveAbilityUseOrigin origin)
    {
        var artifact = context.artifact;
        if (!ArtifactAppearanceRenderer.TryResolveSpatialAnchor(artifact, "mouth", "focus", out Vector3 mouth) &&
            !ArtifactAppearanceRenderer.TryResolveSpatialAnchor(artifact, "mouth", "center", out mouth))
            mouth = ControllerPosition(context);
        var storage = artifact.GetComponent<ArtifactStorageState>();
        float stored = Mathf.Max(0f, Mathf.Min(ability.GetNumber(StoredResourceCost),
            ArtifactStorageOperations.GetAmount(in storage, ArtifactStorageOperations.AbsorbedPower)));
        float damage = SkillContext.DefaultStrength *
                       (ability.GetNumber(DamageMultiplier) + stored * ability.GetNumber(StoredPowerBonus));
        float duration = ability.GetNumber(StatusDuration);
        EssenceElement element = ResolveFiveEssence(artifact.GetComponent<ArtifactMaterialData>());
        string surface = element switch
        {
            EssenceElement.Fire => "outpouring_fire",
            EssenceElement.Wood => "outpouring_wood",
            EssenceElement.Water => "outpouring_water",
            EssenceElement.Earth => "outpouring_earth",
            EssenceElement.Iron => "outpouring_iron",
            _ => "spirit_core"
        };
        var sector = new ArtifactAreaSector(mouth, (Vector2)(TargetPosition(target) - mouth),
            ability.GetNumber(ConeAngle), surface);
        var caster = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, victim) =>
        {
            using var scope = SkillCasterContextService.Enter(in caster);
            ApplyFiveEssenceStatus(source, victim, element, duration, damage * .16f);
        }, damageFirst: false, sector: sector);

        if (!ArtifactAreaConstruction.Start(context, FiveEssenceOutpouring, target, origin, mouth,
                ability.GetNumber(AttackRange), ability.GetNumber(EffectDuration), contact, "gourd_vortex",
                MaterialComposition(context), damage, strike: .10f, commitEffect: SpendStoredPower)) return false;
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime, mouth, sector.Direction);
        return true;

        bool SpendStoredPower()
        {
            if (stored <= 0f) return true;
            ref ArtifactStorageState current = ref artifact.GetComponent<ArtifactStorageState>();
            if (ArtifactStorageOperations.GetAmount(in current, ArtifactStorageOperations.AbsorbedPower) < stored) return false;
            ArtifactStorageOperations.Take(ref current, ArtifactStorageOperations.AbsorbedPower, stored);
            return true;
        }
    }

    private enum EssenceElement { None, Fire, Wood, Water, Earth, Iron }

    /// <summary>使用原材料元素的优先顺序，状态和外观读取同一个结果。</summary>
    private static EssenceElement ResolveFiveEssence(ArtifactMaterialData material)
    {
        float fire = material.GetTrait(ArtifactMaterialTraits.Fire);
        float wood = material.GetTrait(ArtifactMaterialTraits.Wood);
        float water = material.GetTrait(ArtifactMaterialTraits.Water);
        float earth = material.GetTrait(ArtifactMaterialTraits.Earth);
        float iron = material.GetTrait(ArtifactMaterialTraits.Iron);
        float maximum = Mathf.Max(fire, wood, water, earth, iron);
        if (maximum <= 0f) return EssenceElement.None;
        if (maximum == fire) return EssenceElement.Fire;
        if (maximum == wood) return EssenceElement.Wood;
        if (maximum == water) return EssenceElement.Water;
        return maximum == earth ? EssenceElement.Earth : EssenceElement.Iron;
    }

    private static void ApplyFiveEssenceStatus(
        Actor source, Actor target, EssenceElement element, float duration, float tickDamage)
    {
        switch (element)
        {
            case EssenceElement.Fire:
                CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Burn, duration, tickDamage,
                    new ElementComposition(fire: 1f), source);
                break;
            case EssenceElement.Wood:
                CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Poison, duration, tickDamage,
                    new ElementComposition(wood: 1f), source);
                break;
            case EssenceElement.Water:
                CombatStatusEffects.ApplyStatus(target, StatusEffects.Slow, duration, source);
                break;
            case EssenceElement.Earth:
                CombatStatusEffects.ApplyStatus(target, StatusEffects.Daze, duration * .35f, source);
                break;
            case EssenceElement.Iron:
                CombatStatusEffects.ApplyStatus(target, StatusEffects.ArmorBreak, duration, source);
                break;
        }
    }

    private static void ConfigureGourdAbilityVisuals() => ConfigureSpiritGourdReserveVisuals();

    private static void ConfigureSpiritGourdReserveVisuals()
    {
        ArtifactLocalVisualCue reserve = new(ArtifactLocalVisualKind.GatheredWakan)
        {
            anchor = ArtifactVisualAnchorKind.Artifact,
            offset = new Vector3(0f, 0f, .21f),
            scale = .058f,
            alpha = .78f,
        };
        ArtifactLocalVisualCue intake = new(ArtifactLocalVisualKind.GatheredWakan)
        {
            anchor = ArtifactVisualAnchorKind.Artifact,
            color_role = ArtifactVisualColorRole.Primary,
            offset = new Vector3(0f, 0f, .24f),
            scale = .088f,
            alpha = .92f,
            loop = false,
        };
        ArtifactLocalVisualCue release = new(ArtifactLocalVisualKind.GatheredWakan)
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            color_role = ArtifactVisualColorRole.Glow,
            offset = new Vector3(0f, 0f, .05f),
            scale = .083f,
            alpha = .86f,
            loop = false,
        };
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
                .Signal(ArtifactVisualChannels.Store, intake, .25f)
                .Signal(ArtifactVisualChannels.Restore, release, .34f)
        );
    }
}

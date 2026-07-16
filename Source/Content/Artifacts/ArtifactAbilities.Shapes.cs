using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>器形能力共用的参数键、组合尺度和释放边界。</summary>
public partial class ArtifactAbilities
{
    private const string EffectRadius = "effect_radius";
    private const string EffectDuration = "effect_duration";
    private const string EffectCount = "effect_count";
    private const string CounterMultiplier = "counter_multiplier";
    private const string ForceStrength = "force_strength";
    private const string ImpactDelay = "impact_delay";
    private const string ImpactDone = "impact_done";
    private const string ShieldCapacity = "shield_capacity";
    private const string ShieldCurrent = "shield_current";
    private const string ShieldRegen = "shield_regen";
    private const string ReflectRatio = "reflect_ratio";
    private const string SpeedBonus = "speed_bonus";
    private const string DrainAmount = "drain_amount";
    private const string RestoreRatio = "restore_ratio";
    private const string StatusStrength = "status_strength";
    private const string YieldMultiplier = "yield_multiplier";
    private const string ConeAngle = "cone_angle";
    private const string StorageCapacity = "storage_capacity";
    private const string StorePerTrigger = "store_per_trigger";
    private const string StoredResourceCost = "stored_resource_cost";
    private const string SummonCount = "summon_count";
    private const string DamageBonus = "damage_bonus";
    private const string RestoreThreshold = "restore_threshold";
    private const string PulseCount = "pulse_count";

    private static readonly ElementComposition SoulComposition =
        new(neg: 0.55f, pos: 0.15f, entropy: 0.3f, normalize: true);

    private static void ConfigureShapeAbilities()
    {
        ConfigureSplittingSwordArray();
        ConfigureReturningBladeGuard();
        ConfigureArmorPiercingSwordAura();
        ConfigureMountainSealFall();
        ConfigureSpellBanningEdict();
        ConfigureMeridianSealing();
        ConfigureCloudRobeShield();
        ConfigureHeavenlyConcealment();
        ConfigureDamageDiversion();
        ConfigureSpellReflection();
        ConfigureTruthRevealingMirror();
        ConfigureSoulCapturingStasis();
        ConfigureDingFireRefinement();
        ConfigureDevouringReturn();
        ConfigureHundredRefinementCore();
        ConfigureMyriadSoulBannerArray();
        ConfigureCommandingBannerField();
        ConfigureSpiritHostManifestation();
        ConfigureSoulShakingChime();
        ConfigurePurifyingChime();
        ConfigureGoldenBellBarrier();
        ConfigureHeavenSwallowingGourd();
        ConfigureSpiritGourdReserve();
        ConfigureFiveEssenceOutpouring();
        ConfigureGaleFanSweep();
        ConfigureDepartingFireFan();
        ConfigureCleansingWindFan();
        ConfigureTreasureTowerPrison();
        ConfigureLayeredRealmWard();
        ConfigureTowerShadowProjection();
        ConfigureCelestialPearlGuard();
        ConfigureFivePhasePearlStrike();
        ConfigureLinkedPearlResonance();
    }

    private static Actor Controller(ArtifactAbilityExecutionContext context)
    {
        return context.controller.GetComponent<ActorBinder>().Actor;
    }

    private static float ScaledCooldown(ArtifactAbilityComposeContext context, float baseCooldown, float minimum)
    {
        return Mathf.Max(minimum, baseCooldown / Mathf.Max(0.5f, context.scales.Efficiency));
    }

    private static float ScaledCost(ArtifactAbilityComposeContext context, float baseCost)
    {
        return baseCost / Mathf.Max(0.5f, context.scales.Efficiency);
    }

    private static Vector3 TargetPosition(in ActiveAbilityTarget target)
    {
        return target.Object != null && !target.Object.isRekt()
            ? target.Object.GetSimPos()
            : target.Position;
    }

    private static bool CanPrepareFreeBody(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance _,
        ArtifactAbilityRuntimeEntry __,
        BaseSimObject ___)
    {
        return !context.artifact.HasComponent<ArtifactIndependentMotion>() &&
               !context.artifact.HasComponent<SkillExecutionBodyLease>();
    }

    private static bool CanDeployInRange(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry _,
        in ActiveAbilityTarget target)
    {
        if (!CanPrepareFreeBody(context, ability, default, target.Object)) return false;
        float range = ability.GetNumber(AttackRange);
        return Toolbox.SquaredDistVec2Float(Controller(context).current_position, TargetPosition(target)) <=
               range * range;
    }

    private static bool CanTargetHostileActor(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry _,
        in ActiveAbilityTarget target)
    {
        if (target.Object == null || target.Object.isRekt() || !target.Object.isActor()) return false;
        Actor controller = Controller(context);
        Actor victim = target.Object.a;
        if (!controller.canAttackTarget(victim)) return false;
        float range = ability.GetNumber(AttackRange) + victim.stats[strings.S.size];
        return Toolbox.SquaredDistVec2Float(controller.current_position, victim.current_position) <= range * range;
    }

    private static bool BeginTimedActivity(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance __,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget ___,
        ActiveAbilityUseOrigin ____)
    {
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime);
        return true;
    }

    private static bool DeployAtTarget(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ArtifactBodyAnchorRef anchor,
        float? rotation = null)
    {
        return ArtifactAbilityLifecycle.Deploy(
            context,
            ability,
            ref runtime,
            TargetPosition(target),
            anchor,
            rotation);
    }

    private static bool CanUseWorldTargetInRange(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry _,
        in ActiveAbilityTarget target)
    {
        float range = ability.GetNumber(AttackRange);
        return Toolbox.SquaredDistVec2Float(
                   Controller(context).current_position,
                   TargetPosition(target)) <= range * range;
    }

    private static Vector2 DirectionToTarget(
        ArtifactAbilityExecutionContext context,
        in ActiveAbilityTarget target)
    {
        Vector2 direction = (Vector2)TargetPosition(target) - Controller(context).current_position;
        return direction.sqrMagnitude < 0.0001f ? Vector2.up : direction.normalized;
    }

    private static ElementComposition MaterialComposition(ArtifactAbilityExecutionContext context)
    {
        return ArtifactMaterialEffects.ResolveMaterialComposition(
            context.artifact.GetComponent<ArtifactMaterialData>());
    }

    private static void ConfigureShapeAbilityVisuals()
    {
        ConfigureSwordAbilityVisuals();
        ConfigureSealAbilityVisuals();
        ConfigureRobeAbilityVisuals();
        ConfigureMirrorAbilityVisuals();
        ConfigureDingAbilityVisuals();
        ConfigureBannerAbilityVisuals();
        ConfigureBellAbilityVisuals();
        ConfigureGourdAbilityVisuals();
        ConfigureFanAbilityVisuals();
        ConfigureTowerAbilityVisuals();
        ConfigurePearlAbilityVisuals();
    }
}

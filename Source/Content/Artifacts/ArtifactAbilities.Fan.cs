using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Content.Skills;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Usage;
using NeoModLoader.General;
using UnityEngine;

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
        GaleFanSweep.SetSemantics(ArtifactSemantics.Form.Cone, ArtifactSemantics.Element.Wind,
            ArtifactSemantics.Effect.Force);
        GaleFanSweep.exclusivity = ArtifactAbilityExclusivity.WindCone;
        GaleFanSweep.manifestation_cost = 1.05f;
        GaleFanSweep.AddSynergies(
            ArtifactSemantics.Effect.Mobility,
            ArtifactSemantics.Effect.Impact,
            ArtifactSemantics.Delivery.Projection);
        GaleFanSweep.AddConflicts(ArtifactSemanticRules.ImmovableField);
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
            Resource = SkillCastResources.Wakan,
            OnActivityEnded = EndArtifactConstruction,
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
        ActiveAbilityUseOrigin origin)
    {
        var contact = new ArtifactAreaContact(0f, ability.GetNumber(ForceStrength) * context.effect_scale, damageFirst: true);
        return StartFanSector(context, GaleFanSweep, ability, ref runtime, target, origin, contact,
            "fan_gale", GaleProfile, GaleComposition, SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier));
    }

    private static void ConfigureDepartingFireFan()
    {
        DepartingFireFan.name_key = "Cultiway.ArtifactAbility.DepartingFireFan";
        DepartingFireFan.SetSemantics(ArtifactSemantics.Form.Cone, ArtifactSemantics.Element.Fire,
            ArtifactSemantics.Effect.Status);
        DepartingFireFan.exclusivity = ArtifactAbilityExclusivity.FireCone;
        DepartingFireFan.manifestation_cost = 1.12f;
        DepartingFireFan.AddSynergies(
            ArtifactSemantics.Element.Fire,
            ArtifactSemantics.Material.Volatility,
            ArtifactSemantics.Delivery.Projection);
        DepartingFireFan.AddConflicts(ArtifactSemanticRules.WaterField);
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
            Resource = SkillCastResources.Wakan,
            OnActivityEnded = EndArtifactConstruction,
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
        ActiveAbilityUseOrigin origin)
    {
        float damage = SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier);
        float burn = damage * ability.GetNumber(StatusStrength);
        float duration = ability.GetNumber(StatusDuration);
        ElementComposition fire = new(fire: 1f);
        var caster = ArtifactAbilityLifecycle.ResolveSkillContext(context);
        var contact = new ArtifactAreaContact((source, victim) =>
        {
            using var scope = SkillCasterContextService.Enter(in caster);
            CombatStatusEffects.ApplyTickingStatus(victim, StatusEffects.Burn, duration, burn, fire, source);
        }, damageFirst: false);
        return StartFanSector(context, DepartingFireFan, ability, ref runtime, target, origin, contact,
            "fan_flame", FlameProfile, fire, damage);
    }

    private static void ConfigureCleansingWindFan()
    {
        CleansingWindFan.name_key = "Cultiway.ArtifactAbility.CleansingWindFan";
        CleansingWindFan.SetSemantics(
            ArtifactSemantics.Form.Cone,
            ArtifactSemantics.Effect.Purification,
            ArtifactSemantics.Effect.Dispel);
        CleansingWindFan.exclusivity = ArtifactAbilityExclusivity.CleansingCone;
        CleansingWindFan.manifestation_cost = 0.95f;
        CleansingWindFan.AddSynergies(
            ArtifactSemantics.Effect.Purification,
            ArtifactSemantics.Effect.Mobility,
            ArtifactSemantics.Role.Support);
        CleansingWindFan.AddConflicts(ArtifactSemanticRules.CurseField);
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
            Resource = SkillCastResources.Wakan,
            OnActivityEnded = EndArtifactConstruction,
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
        ActiveAbilityUseOrigin origin)
    {
        var contact = new ArtifactAreaContact(ability.GetInteger(EffectCount));
        return StartFanSector(context, CleansingWindFan, ability, ref runtime, target, origin, contact,
            "fan_cleanse", CleansingProfile, ElementComposition.Static.empty);
    }

    private static bool StartFanSector(ArtifactAbilityExecutionContext context, ArtifactAbilityAsset asset,
        ArtifactAbilityInstance ability, ref ArtifactAbilityRuntimeEntry runtime, in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin, ArtifactAreaContact contact, string surface, Vector2[] profile,
        ElementComposition element, float damage = 0f)
    {
        Vector3 center = ControllerPosition(context);
        var sector = new ArtifactAreaSector(center, TargetPosition(target) - center, ability.GetNumber(ConeAngle),
            surface, profile);
        if (!ArtifactAreaConstruction.Start(context, asset, target, origin, center, ability.GetNumber(AttackRange),
                ability.GetNumber(EffectDuration), contact.WithSector(sector), "gourd_vortex", element, damage,
                strike: .10f)) return false;
        ArtifactAbilityLifecycle.BeginTimedActivity(ref runtime, (Vector2)center, sector.Direction);
        return true;
    }

    private static readonly Vector2[] GaleProfile =
    [
        new(0f, .012f), new(.21f, .019f), new(.38f, .031f), new(.46f, .105f), new(.51f, .053f),
        new(.69f, .033f), new(.85f, .087f), new(.91f, .092f), new(1f, .014f), new(.96f, .006f), new(0f, .003f)
    ];

    private static readonly Vector2[] FlameProfile =
    [
        new(0f, .012f), new(.19f, .03f), new(.47f, .101f), new(.55f, .042f), new(.68f, .158f),
        new(.74f, .071f), new(.85f, .221f), new(.93f, .127f), new(1f, .029f), new(.98f, .007f), new(0f, .003f)
    ];

    private static readonly Vector2[] CleansingProfile =
    [
        new(0f, .013f), new(.17f, .019f), new(.42f, .035f), new(.53f, .018f), new(.67f, .044f),
        new(.74f, .186f), new(.77f, .192f), new(.83f, .062f), new(1f, .013f), new(.97f, .006f), new(0f, .003f)
    ];
}

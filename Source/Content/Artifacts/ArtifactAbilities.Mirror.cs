using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using NeoModLoader.General;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>法术反射被动；削减非武器伤害，并将其中一部分按原元素构成返还攻击者。</summary>
    public static ArtifactAbilityAsset SpellReflection { get; private set; }
    /// <summary>可部署破妄法域；净化友军、揭露隐匿敌人并驱散其正面状态。</summary>
    public static ArtifactAbilityAsset TruthRevealingMirror { get; private set; }
    /// <summary>单体摄魂主动；抽取目标灵气返还持有者，并施加眩晕与沉默。</summary>
    public static ArtifactAbilityAsset SoulCapturingStasis { get; private set; }

    private static void ConfigureSpellReflection()
    {
        SpellReflection.name_key = "Cultiway.ArtifactAbility.SpellReflection";
        SpellReflection.SetSemantics(
            ArtifactSemantics.Effect.Counter,
            ArtifactSemantics.Effect.Reflection,
            ArtifactSemantics.Form.Spell);
        SpellReflection.exclusivity = ArtifactAbilityExclusivity.SpellReflection;
        SpellReflection.manifestation_cost = 1.2f;
        SpellReflection.AddSynergies(ArtifactSemantics.Effect.Perception, ArtifactSemantics.Effect.Counter);
        SpellReflection.AddConflicts(ArtifactSemanticRules.Absorption);
        SpellReflection.minimum_score = 1f;
        SpellReflection.use_profile = new ArtifactUseProfile { offensive = 0.4f, defensive = 0.9f };
        SpellReflection.control_complexity = 0.42f;
        SpellReflection.parameter_schema =
        [
            NumberSpec(ReflectRatio),
            NumberSpec(Cooldown),
            IntegerSpec(MaxCharges),
            NumberSpec(Recharge),
        ];
        SpellReflection.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Reflection) *
            (0.6f + context.GetTrait(ArtifactMaterialTraits.Perception) * 0.38f +
             context.GetTrait(ArtifactMaterialTraits.Ward) * 0.2f);
        SpellReflection.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(ReflectRatio, Mathf.Clamp(0.2f * context.scales.Precision, 0.2f, 0.72f)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 1.6f, 0.4f)),
            ArtifactAbilityValue.Integer(MaxCharges, Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity), 1, 5)),
            ArtifactAbilityValue.Number(Recharge, ScaledCooldown(context, 7.5f, 2f)),
        ];
        SpellReflection.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SpellReflection.Description"),
            ability.GetNumber(ReflectRatio),
            ability.GetInteger(MaxCharges),
            ability.GetNumber(Recharge));
        SpellReflection.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            event_consumes_trigger = true,
            ResolveMaxCharges = (_, ability) => ability.GetInteger(MaxCharges),
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveRecharge = (_, ability) => ability.GetNumber(Recharge),
        });
        SpellReflection.Handle<ArtifactIncomingDamageEvent>(
            (_, _, _, evt) => evt.Damage > 0f && evt.AttackType != AttackType.Weapon && !evt.IsRetaliation &&
                              evt.Attacker != null && !evt.Attacker.isRekt() && evt.Attacker.isActor(),
            ApplySpellReflection);
    }

    private static void ApplySpellReflection(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        Actor controller = Controller(context);
        Actor attacker = evt.Attacker.a;
        float reflected = evt.Damage * ability.GetNumber(ReflectRatio);
        evt.Damage -= reflected;
        ArtifactDamageEffects.DealRetaliationDamage(
            controller,
            attacker,
            reflected,
            evt.DamageComposition,
            evt.IgnoreDamageReduction);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Reflect,
            target: attacker,
            intensity: reflected);
    }

    private static void ConfigureTruthRevealingMirror()
    {
        TruthRevealingMirror.name_key = "Cultiway.ArtifactAbility.TruthRevealingMirror";
        TruthRevealingMirror.SetSemantics(
            ArtifactSemantics.Delivery.Deployment,
            ArtifactSemantics.Effect.Purification,
            ArtifactSemantics.Effect.Dispel);
        TruthRevealingMirror.exclusivity = ArtifactAbilityExclusivity.TruthRevealingField;
        TruthRevealingMirror.manifestation_cost = 1.3f;
        TruthRevealingMirror.AddSynergies(ArtifactSemantics.Effect.Perception, ArtifactSemantics.Role.Support);
        TruthRevealingMirror.AddConflicts(ArtifactSemanticRules.Concealment);
        TruthRevealingMirror.minimum_score = 1f;
        TruthRevealingMirror.use_profile = new ArtifactUseProfile { offensive = 0.25f, defensive = 0.45f, support = 0.95f };
        TruthRevealingMirror.control_complexity = 0.44f;
        TruthRevealingMirror.thread_cost = 1;
        TruthRevealingMirror.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(EffectRadius),
            NumberSpec(EffectDuration),
            IntegerSpec(EffectCount),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        TruthRevealingMirror.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Purification) +
             context.GetTrait(ArtifactMaterialTraits.Perception) * 0.45f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Reflection) * 0.35f +
             context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.2f);
        TruthRevealingMirror.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectRadius, 3f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 4.5f * context.scales.Duration),
            ArtifactAbilityValue.Integer(EffectCount, Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity), 1, 6)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 13f, 4f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3.5f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.35f)),
        ];
        TruthRevealingMirror.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.TruthRevealingMirror.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(EffectRadius),
            ability.GetInteger(EffectCount),
            ability.GetNumber(EffectDuration),
            ability.GetNumber(Cooldown));
        TruthRevealingMirror.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.75f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = UseWakan,
            OnTick = ApplyTruthRevealingField,
        });
        TruthRevealingMirror.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat | ActiveAbilityChannel.World,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 5,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            CanPrepare = CanPrepareFreeBody,
            CanUse = CanDeployInRange,
            TryUse = DeployTruthRevealingMirror,
        });
    }

    private static bool DeployTruthRevealingMirror(
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
            ArtifactBodyAnchorRef.Appearance("surface", "center"));
    }

    private static void ApplyTruthRevealingField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        float _)
    {
        Actor controller = Controller(context);
        ArtifactDeployment deployment = context.artifact.GetComponent<ArtifactDeployment>();
        Vector2 position = ArtifactManifestationTools.ResolveWorldAnchor(
            context.artifact,
            deployment.ResolveBodyAnchor());
        int maxCount = ability.GetInteger(EffectCount);
        int affected = 0;
        ArtifactTargeting.ForEachFriendly(controller, position, ability.GetNumber(EffectRadius), target =>
            affected += ArtifactStatusEffects.CleanseNegativeStatuses(target, maxCount));
        ArtifactTargeting.ForEachEnemyIncludingConcealed(
            controller,
            position,
            ability.GetNumber(EffectRadius),
            target =>
            {
                affected += ArtifactStatusEffects.RemoveStatus(target, StatusEffects.Concealed);
                affected += ArtifactStatusEffects.DispelPositiveStatuses(target, maxCount);
            });
        if (affected > 0)
        {
            ArtifactAbilityVisuals.Emit(
                context,
                ability,
                runtime,
                ArtifactVisualChannels.Cleanse,
                position,
                intensity: affected);
        }
    }

    private static void ConfigureSoulCapturingStasis()
    {
        SoulCapturingStasis.name_key = "Cultiway.ArtifactAbility.SoulCapturingStasis";
        SoulCapturingStasis.SetSemantics(
            ArtifactSemantics.Effect.Control,
            ArtifactSemantics.Theme.Soul,
            ArtifactSemantics.Effect.Drain);
        SoulCapturingStasis.exclusivity = ArtifactAbilityExclusivity.SoulStasis;
        SoulCapturingStasis.manifestation_cost = 1.05f;
        SoulCapturingStasis.AddSynergies(ArtifactSemantics.Effect.Sealing, ArtifactSemantics.Effect.Devouring);
        SoulCapturingStasis.AddConflicts(ArtifactSemanticRules.Purification);
        SoulCapturingStasis.minimum_score = 1f;
        SoulCapturingStasis.use_profile = new ArtifactUseProfile { offensive = 0.8f, support = 0.15f };
        SoulCapturingStasis.control_complexity = 0.4f;
        SoulCapturingStasis.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(StatusDuration),
            NumberSpec(DrainAmount),
            NumberSpec(RestoreRatio),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
        ];
        SoulCapturingStasis.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Soul) +
             context.GetTrait(ArtifactMaterialTraits.Perception) * 0.3f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Sealing) * 0.45f +
             context.GetTrait(ArtifactMaterialTraits.Binding) * 0.3f);
        SoulCapturingStasis.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(AttackRange, 7.5f * context.scales.Range),
            ArtifactAbilityValue.Number(StatusDuration, 1.6f * context.scales.Duration),
            ArtifactAbilityValue.Number(DrainAmount, 12f * context.scales.Potency),
            ArtifactAbilityValue.Number(RestoreRatio, Mathf.Clamp(0.35f * context.scales.Efficiency, 0.35f, 0.8f)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 9f, 2.5f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 2.8f)),
        ];
        SoulCapturingStasis.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SoulCapturingStasis.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(StatusDuration),
            ability.GetNumber(DrainAmount),
            ability.GetNumber(RestoreRatio),
            ability.GetNumber(Cooldown));
        SoulCapturingStasis.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            Resource = UseWakan,
        });
        SoulCapturingStasis.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Object,
            activation_mode = ActiveAbilityActivationMode.Instant,
            ai_weight = 7,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanUse = CanTargetHostileActor,
            TryUse = ApplySoulCapturingStasis,
        });
    }

    private static bool ApplySoulCapturingStasis(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = Controller(context);
        Actor victim = target.Object.a;
        float drained = ArtifactResourceEffects.DrainWakan(victim, ability.GetNumber(DrainAmount));
        ArtifactResourceEffects.RestoreWakan(controller, drained * ability.GetNumber(RestoreRatio));
        float duration = ability.GetNumber(StatusDuration);
        ArtifactStatusEffects.ApplyStatus(victim, StatusEffects.Daze, duration, controller);
        ArtifactStatusEffects.ApplyStatus(victim, StatusEffects.Silence, duration, controller);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Drain,
            victim.current_position,
            target: victim,
            intensity: drained);
        return true;
    }

    private static void ConfigureMirrorAbilityVisuals()
    {
        ConfigureSpellReflectionVisuals();
        ConfigureTruthRevealingMirrorVisuals();
        ConfigureSoulCapturingStasisVisuals();
    }

    private static void ConfigureSpellReflectionVisuals()
    {
        ArtifactBeamVisualCue reflection = Beam(
            ArtifactVisualAnchorRef.Appearance("surface", "center"),
            ArtifactVisualAnchorKind.Target,
            ArtifactVisualColorRole.Glow,
            ArtifactVfxStyles.Reflection
        );
        reflection.width = 0.075f;
        reflection.glow_width_multiplier = 3.4f;
        SpellReflection.Visualize(
            Theme(SkillVfxElements.Pos.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Reflect,
                    new ArtifactCompositeVisualCue(
                        reflection,
                        Sparkle(ArtifactVisualAnchorKind.Artifact, 0.065f, 0.95f, loop: false),
                        Pulse(1.22f, 1f, 0.2f)
                    ),
                    0.38f
                )
        );
    }

    private static void ConfigureTruthRevealingMirrorVisuals()
    {
        ArtifactGlyphVisualCue truthGlyph = Glyph(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Glow,
            ArtifactVfxStyles.Purification
        );
        truthGlyph.sides = 10;
        truthGlyph.rotation_speed = 16f;
        ArtifactAreaVisualCue truthField = Area(
            ArtifactVisualAnchorKind.DeploymentOrigin,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Primary,
            0.52f,
            0.035f,
            ArtifactVfxStyles.Purification
        );
        ArtifactAreaVisualCue cleanse = Area(
            ArtifactVisualAnchorKind.Point,
            context => context.ability.GetNumber(EffectRadius),
            ArtifactVisualColorRole.Glow,
            0.7f,
            0.04f,
            ArtifactVfxStyles.Purification
        );
        cleanse.start_scale = 0.2f;
        cleanse.end_scale = 1.1f;
        cleanse.fade_out = true;
        TruthRevealingMirror.Visualize(
            Theme(SkillVfxElements.Pos.AccentColor)
                .Loop(
                    "truth_revealing",
                    new ArtifactCompositeVisualCue(truthField, truthGlyph),
                    IsDeploymentActive
                )
                .Signal(
                    ArtifactVisualChannels.Cleanse,
                    new ArtifactCompositeVisualCue(
                        cleanse,
                        Burst(ArtifactVisualAnchorKind.Point, ArtifactVisualColorRole.Glow, 8)
                    ),
                    0.5f,
                    "artifact.truth_revealing.cleanse",
                    ArtifactVisualStackPolicy.MergeIntensity
                )
        );
    }

    private static void ConfigureSoulCapturingStasisVisuals()
    {
        ArtifactTetherVisualCue soulTether = new ArtifactTetherVisualCue
        {
            style_key = ArtifactVfxStyles.Soul,
            from = ArtifactVisualAnchorRef.Appearance("surface", "center"),
            to = ArtifactVisualAnchorKind.Target,
            color_role = ArtifactVisualColorRole.Primary,
            width = 0.05f,
            curvature = -0.25f,
            wave_amplitude = 0.055f,
            wave_speed = -4f,
            match_actor_scale = false,
        };
        ArtifactGlyphVisualCue stasis = Glyph(
            ArtifactVisualAnchorKind.Target,
            (ArtifactAbilityVisualContext _) => 0.55f,
            ArtifactVisualColorRole.Secondary,
            ArtifactVfxStyles.Soul,
            matchActorScale: true
        );
        stasis.rotation_speed = -28f;
        SoulCapturingStasis.Visualize(
            Theme(SkillVfxElements.Neg.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Drain,
                    new ArtifactCompositeVisualCue(
                        soulTether,
                        stasis,
                        Burst(ArtifactVisualAnchorKind.Target, ArtifactVisualColorRole.Glow, 5)
                    ),
                    0.7f
                )
        );
    }
}

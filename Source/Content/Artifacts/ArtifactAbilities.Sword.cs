using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;
using UnityEngine;
using System;
using Cultiway.Content.Visuals;
using Cultiway.Core.SkillLibV3.Visuals;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>持续剑阵主动；按能力强度生成环绕剑影，反复出阵穿刺不同目标后归阵。</summary>
    public static ArtifactAbilityAsset SplittingSwordArray { get; private set; }
    /// <summary>受击护主被动；降低本次伤害，并以反击伤害和冲击力回敬攻击者。</summary>
    public static ArtifactAbilityAsset ReturningBladeGuard { get; private set; }
    /// <summary>命中破甲被动；角色造成伤害时按目标护甲施加破甲状态。</summary>
    public static ArtifactAbilityAsset ArmorPiercingSwordAura { get; private set; }

    private static void ConfigureSplittingSwordArray()
    {
        SplittingSwordArray.name_key = "Cultiway.ArtifactAbility.SplittingSwordArray";
        SplittingSwordArray.SetSemantics(ArtifactSemantics.Delivery.Projection, ArtifactSemantics.Form.Array);
        SplittingSwordArray.exclusivity = ArtifactAbilityExclusivity.ProjectionArray;
        SplittingSwordArray.manifestation_cost = 1.4f;
        SplittingSwordArray.AddSynergies(ArtifactSemantics.Role.Offensive, ArtifactSemantics.Effect.Resonance);
        SplittingSwordArray.AddConflicts(ArtifactSemanticRules.SingularProjection);
        SplittingSwordArray.minimum_score = 1f;
        SplittingSwordArray.use_profile = new ArtifactUseProfile { offensive = 0.85f, defensive = 0.2f };
        SplittingSwordArray.control_complexity = 0.52f;
        SplittingSwordArray.thread_cost = 2;
        SplittingSwordArray.parameter_schema =
        [
            NumberSpec(DamageMultiplier),
            NumberSpec(EffectRadius),
            NumberSpec(EffectDuration),
            IntegerSpec(EffectCount),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        SplittingSwordArray.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Projection) +
             context.GetTrait(ArtifactMaterialTraits.FieldProjection) * 0.7f +
             context.GetTrait(ArtifactMaterialTraits.Quantity) * 0.25f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Edge) * 0.45f +
             context.GetTrait(ArtifactMaterialTraits.Resonance) * 0.18f);
        SplittingSwordArray.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(DamageMultiplier, 0.025f * context.scales.Potency),
            ArtifactAbilityValue.Number(EffectRadius, 3.4f * context.scales.Range),
            ArtifactAbilityValue.Number(EffectDuration, 14f * context.scales.Duration),
            ArtifactAbilityValue.Integer(
                EffectCount,
                Mathf.Clamp(
                    Mathf.RoundToInt((48f + context.scales.Potency * 16f) / 8f) * 8,
                    48,
                    ArtifactSwordArrayExecution.MaxBladeCount)),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 20f, 6f)),
            ArtifactAbilityValue.Number(ActivationCost, ScaledCost(context, 3.2f)),
            ArtifactAbilityValue.Number(MaintenanceCost, ScaledCost(context, 0.12f)),
        ];
        SplittingSwordArray.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SplittingSwordArray.Description"),
            ability.GetInteger(EffectCount),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(DamageMultiplier),
            ability.GetNumber(EffectDuration),
            ability.GetNumber(Cooldown));
        SplittingSwordArray.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.35f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = UseWakan,
        });
        SplittingSwordArray.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Self,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 7,
            ResolveEffectRadius = (_, ability) => ability.GetNumber(EffectRadius),
            TryUse = LaunchSplittingSwordArray,
        });
    }

    private static bool LaunchSplittingSwordArray(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = Controller(context);
        Entity execution = ArtifactSkillExecutions.SwordArray.NewEntity();
        ref SkillContext skillContext = ref execution.GetComponent<SkillContext>();
        skillContext.SourceObj = controller;
        skillContext.TargetPos = controller.GetSimPos();
        skillContext.TargetDir = Vector2.up;
        skillContext.AttackKingdom = target.AttackKingdom;
        skillContext.Strength = SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier);
        skillContext.PowerLevel = controller.GetExtend().GetPowerLevel();

        Vector2 origin = context.artifact.HasComponent<Position>()
            ? context.artifact.GetComponent<Position>().v2
            : controller.cur_transform_position;
        ArtifactSwordArrayExecution.Initialize(
            execution,
            controller,
            origin,
            ability.GetInteger(EffectCount),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(EffectDuration));
        ArtifactAbilityLifecycle.BindExecution(ref runtime, execution);
        return true;
    }

    private static void ConfigureReturningBladeGuard()
    {
        ReturningBladeGuard.name_key = "Cultiway.ArtifactAbility.ReturningBladeGuard";
        ReturningBladeGuard.SetSemantics(ArtifactSemantics.Effect.Counter, ArtifactSemantics.Form.Blade);
        ReturningBladeGuard.exclusivity = ArtifactAbilityExclusivity.BladeCounter;
        ReturningBladeGuard.manifestation_cost = 0.9f;
        ReturningBladeGuard.AddSynergies(ArtifactSemantics.Role.Defensive, ArtifactSemantics.Role.Offensive);
        ReturningBladeGuard.AddConflicts(ArtifactSemanticRules.Nonretaliation);
        ReturningBladeGuard.minimum_score = 1f;
        ReturningBladeGuard.use_profile = new ArtifactUseProfile { offensive = 0.45f, defensive = 0.85f };
        ReturningBladeGuard.control_complexity = 0.3f;
        ReturningBladeGuard.parameter_schema =
        [
            NumberSpec(DamageReduction),
            NumberSpec(CounterMultiplier),
            NumberSpec(ForceStrength),
            NumberSpec(Cooldown),
            IntegerSpec(MaxCharges),
            NumberSpec(Recharge),
        ];
        ReturningBladeGuard.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Edge) +
             context.GetTrait(ArtifactMaterialTraits.Mobility) * 0.45f) *
            (0.45f + context.GetTrait(ArtifactMaterialTraits.Ward) * 0.4f +
             context.GetTrait(ArtifactMaterialTraits.Binding) * 0.22f);
        ReturningBladeGuard.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(DamageReduction, Mathf.Clamp(0.12f * context.scales.Precision, 0.12f, 0.48f)),
            ArtifactAbilityValue.Number(CounterMultiplier, 0.28f * context.scales.Potency),
            ArtifactAbilityValue.Number(ForceStrength, 0.35f * context.scales.Potency),
            ArtifactAbilityValue.Number(Cooldown, ScaledCooldown(context, 1.3f, 0.35f)),
            ArtifactAbilityValue.Integer(MaxCharges, Mathf.Clamp(Mathf.FloorToInt(context.scales.Capacity), 1, 5)),
            ArtifactAbilityValue.Number(Recharge, ScaledCooldown(context, 6.5f, 1.8f)),
        ];
        ReturningBladeGuard.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.ReturningBladeGuard.Description"),
            ability.GetNumber(DamageReduction),
            ability.GetNumber(CounterMultiplier),
            ability.GetInteger(MaxCharges),
            ability.GetNumber(Recharge));
        ReturningBladeGuard.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            event_consumes_trigger = true,
            ResolveMaxCharges = (_, ability) => ability.GetInteger(MaxCharges),
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveRecharge = (_, ability) => ability.GetNumber(Recharge),
        });
        ReturningBladeGuard.Handle<ArtifactIncomingDamageEvent>(
            (_, _, _, evt) => evt.Damage > 0f && !evt.IsRetaliation &&
                              evt.Attacker != null && !evt.Attacker.isRekt() && evt.Attacker.isActor(),
            ApplyReturningBladeGuard);
    }

    private static void ApplyReturningBladeGuard(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        Actor controller = Controller(context);
        Actor attacker = evt.Attacker.a;
        float originalDamage = evt.Damage;
        evt.Damage *= 1f - ability.GetNumber(DamageReduction);
        ArtifactDamageEffects.DealRetaliationDamage(
            controller,
            attacker,
            SkillContext.DefaultStrength * ability.GetNumber(CounterMultiplier),
            ElementComposition.Static.Iron);
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
            intensity: originalDamage);
    }

    private static void ConfigureArmorPiercingSwordAura()
    {
        ArmorPiercingSwordAura.name_key = "Cultiway.ArtifactAbility.ArmorPiercingSwordAura";
        ArmorPiercingSwordAura.SetSemantics(ArtifactSemantics.Effect.Debuff, ArtifactSemantics.Effect.ArmorBreak);
        ArmorPiercingSwordAura.exclusivity = ArtifactAbilityExclusivity.ArmorBreakOnHit;
        ArmorPiercingSwordAura.manifestation_cost = 0.7f;
        ArmorPiercingSwordAura.AddSynergies(ArtifactSemantics.Role.Offensive, ArtifactSemantics.Effect.Hit);
        ArmorPiercingSwordAura.minimum_score = 1f;
        ArmorPiercingSwordAura.use_profile = new ArtifactUseProfile { offensive = 0.95f };
        ArmorPiercingSwordAura.control_complexity = 0.16f;
        ArmorPiercingSwordAura.parameter_schema = [NumberSpec(StatusDuration), NumberSpec(StatusStrength)];
        ArmorPiercingSwordAura.ScoreRecipe = context =>
            (context.GetTrait(ArtifactMaterialTraits.Edge) +
             context.GetTrait(ArtifactMaterialTraits.Impact) * 0.7f) *
            (0.55f + context.GetTrait(ArtifactMaterialTraits.Hardness) * 0.35f);
        ArmorPiercingSwordAura.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(StatusDuration, 2.4f * context.scales.Duration),
            ArtifactAbilityValue.Number(StatusStrength, Mathf.Clamp(0.08f * context.scales.Potency, 0.08f, 0.42f)),
        ];
        ArmorPiercingSwordAura.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.ArmorPiercingSwordAura.Description"),
            ability.GetNumber(StatusStrength),
            ability.GetNumber(StatusDuration));
        ArmorPiercingSwordAura.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Operating,
        });
        ArmorPiercingSwordAura.Handle<ArtifactDamageDealtEvent>(
            (_, _, _, evt) => evt.Damage > 0f && evt.Target != null && !evt.Target.isRekt() && evt.Target.isActor(),
            ApplyArmorPiercingSwordAura);
    }

    private static void ApplyArmorPiercingSwordAura(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactDamageDealtEvent evt)
    {
        Actor target = evt.Target.a;
        float armorLoss = Mathf.Max(1f, target.stats[S.armor] * ability.GetNumber(StatusStrength));
        ArtifactStatusEffects.ApplyStatus(
            target,
            StatusEffects.ArmorBreak,
            ability.GetNumber(StatusDuration),
            S.armor,
            -armorLoss,
            Controller(context));
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Hit,
            target.current_position,
            target: target,
            intensity: armorLoss);
    }

    private static void ConfigureSwordAbilityVisuals()
    {
        ConfigureSplittingSwordArrayVisuals();
        ConfigureReturningBladeGuardVisuals();
        ConfigureArmorPiercingSwordAuraVisuals();
    }

    private static void ConfigureSplittingSwordArrayVisuals()
    {
        ArtifactSwordArrayVisualCue projections = new ArtifactSwordArrayVisualCue
        {
            max_count = ArtifactSwordArrayExecution.MaxBladeCount,
            formation_size_ratio = 0.24f,
            attack_size_ratio = 0.95f,
            alpha = 0.84f,
        };
        ArtifactGlyphVisualCue arrayGlyph = Glyph(
            ArtifactVisualAnchorKind.Controller,
            context =>
                ArtifactSwordArrayExecution.ResolveRingRadius(
                    context.controller.GetComponent<ActorBinder>().Actor,
                    context.ability.GetInteger(EffectCount)
                ),
            ArtifactVisualColorRole.Secondary,
            ArtifactVfxStyles.Metal
        );
        arrayGlyph.alpha = 0.32f;
        arrayGlyph.rotation_speed = 34f;
        arrayGlyph.start_scale = 1f;
        SplittingSwordArray.Visualize(
            Theme(SkillVfxElements.Metal.AccentColor)
                .Loop(
                    "splitting_array",
                    new ArtifactCompositeVisualCue(projections, arrayGlyph),
                    IsActivityActive
                )
                .Signal(
                    ArtifactVisualChannels.Trigger,
                    Burst(
                        ArtifactVisualAnchorKind.Controller,
                        ArtifactVisualColorRole.Glow,
                        12,
                        0.5f
                    ),
                    0.5f
                )
                .Signal(
                    ArtifactVisualChannels.End,
                    Burst(
                        ArtifactVisualAnchorKind.Artifact,
                        ArtifactVisualColorRole.Glow,
                        8,
                        0.38f
                    ),
                    0.38f,
                    "artifact.splitting_array.end",
                    ArtifactVisualStackPolicy.SinglePerController
                )
        );
    }

    private static void ConfigureReturningBladeGuardVisuals()
    {
        ArtifactBeamVisualCue counter = Beam(
            ArtifactVisualAnchorRef.Appearance("blade", "tip", ArtifactBodyAnchorKind.ForwardTip),
            ArtifactVisualAnchorKind.Target,
            ArtifactVisualColorRole.Glow,
            ArtifactVfxStyles.Metal
        );
        counter.width = 0.06f;
        counter.glow_width_multiplier = 3f;
        ReturningBladeGuard.Visualize(
            Theme(SkillVfxElements.Metal.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Counter,
                    new ArtifactCompositeVisualCue(
                        counter,
                        Burst(ArtifactVisualAnchorKind.Target, ArtifactVisualColorRole.Primary, 5),
                        Pulse(1.12f, 1f, 0.18f)
                    ),
                    0.28f
                )
        );
    }

    private static void ConfigureArmorPiercingSwordAuraVisuals()
    {
        ArtifactDecalVisualCue fracture = new ArtifactDecalVisualCue
        {
            style_key = ArtifactVfxStyles.Metal,
            anchor = ArtifactVisualAnchorKind.Target,
            color_role = ArtifactVisualColorRole.Primary,
            radius = 0.36f,
            sides = 6,
            alpha = 0.8f,
            match_actor_scale = true,
            end_scale = 1.2f,
        };
        ArmorPiercingSwordAura.Visualize(
            Theme(SkillVfxElements.Metal.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Hit,
                    new ArtifactCompositeVisualCue(
                        fracture,
                        Burst(ArtifactVisualAnchorKind.Target, ArtifactVisualColorRole.Glow, 4)
                    ),
                    0.38f,
                    "artifact.armor_break",
                    ArtifactVisualStackPolicy.Strongest
                )
        );
    }
}

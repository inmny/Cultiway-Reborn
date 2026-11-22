using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core;
using Cultiway.Core.Combat;
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
    /// <summary>持续剑阵主动；环绕剑影按敌人方向反复直线横贯圆阵，并在对侧换位入阵。</summary>
    public static ArtifactAbilityAsset SplittingSwordArray { get; private set; }

    /// <summary>受击护主被动；降低本次伤害，并驱动法宝本体穿刺攻击者后立即归位。</summary>
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
            // 仅兼容旧百宝阁蓝图；分光剑阵不再生成或使用维持灵力参数。
            new ArtifactAbilityValueSpec
            {
                key = MaintenanceCost,
                kind = ArtifactAbilityValueKind.Number,
            },
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
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(EffectDuration),
            ResolveActivationCost = (context, ability) => ability.GetNumber(ActivationCost) * (context.controller.IsNull
                ? 1f
                : Mathf.Sqrt(
                    Combat.BattleSpellScale.Resolve(context.controller.GetComponent<ActorBinder>().AE))),
            Resource = SkillCastResources.Wakan,
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
        skillContext.BindSource(controller);
        skillContext.TargetPos = ArtifactYuanshenControlService.ResolveOrigin(controller, context.artifact);
        skillContext.TargetDir = Vector2.up;
        skillContext.AttackKingdom = target.AttackKingdom;
        skillContext.Strength = SkillContext.DefaultStrength * ability.GetNumber(DamageMultiplier);
        skillContext.PowerLevel = controller.GetExtend().GetPowerLevel();

        if (!ArtifactSwordArrayExecution.Initialize(
            execution,
            controller,
            context.artifact,
            ability.GetInteger(EffectCount),
            ability.GetNumber(EffectRadius),
            ability.GetNumber(EffectDuration)))
        {
            SkillExecutionLifecycle.RequestEnd(execution);
            return false;
        }
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
            sustain_minimum_state = ArtifactControlState.Ready,
            event_consumes_trigger = true,
            ResolveMaxCharges = (_, ability) => ability.GetInteger(MaxCharges),
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveRecharge = (_, ability) => ability.GetNumber(Recharge),
        });
        ReturningBladeGuard.Handle<ArtifactIncomingDamageEvent>(
            CanTriggerReturningBladeGuard,
            ApplyReturningBladeGuard);
    }

    private static bool CanTriggerReturningBladeGuard(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        return evt.Damage > 0f &&
               !evt.IsRetaliation &&
               evt.Attacker != null &&
               !evt.Attacker.isRekt() &&
               evt.Attacker.isActor() &&
               runtime.activity_kind == ArtifactAbilityActivityKind.None &&
               CanPrepareFreeBody(context, ability, runtime, evt.Attacker);
    }

    private static void ApplyReturningBladeGuard(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        Actor controller = Controller(context);
        Actor attacker = evt.Attacker.a;
        float forceStrength = ability.GetNumber(ForceStrength);
        float flightSpeed = 24f + forceStrength * 4f;
        float targetDistance = Vector2.Distance(
            ArtifactYuanshenControlService.ResolveOrigin(controller, context.artifact),
            attacker.current_position);
        if (!ArtifactSpatialAttackLauncher.TryLaunch(
                context,
                new ArtifactSpatialAttackLaunchRequest
                {
                    target = attacker,
                    mode = ArtifactSpatialAttackMode.StrikeAndReturn,
                    strength = SkillContext.DefaultStrength * ability.GetNumber(CounterMultiplier),
                    speed = flightSpeed,
                    turn_rate = 540f + forceStrength * 40f,
                    control_range = Mathf.Max(6f, targetDistance + attacker.stats[S.size] + 2f),
                    pierce_distance = 1.2f + forceStrength * 0.35f,
                    impact_force = forceStrength * context.effect_scale,
                    damage_origin = DamageOrigin.Reaction,
                    trail_tint = ArtifactAbilityVisuals.ResolveTheme(
                        context.artifact,
                        ReturningBladeGuard.visual).glow,
                },
                out Entity execution)) return;

        float effectiveReduction = ability.GetNumber(DamageReduction) * context.effect_scale;
        evt.Damage *= 1f - effectiveReduction;
        ArtifactAbilityLifecycle.BindExecution(ref runtime, execution);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Counter,
            target: attacker,
            intensity: effectiveReduction,
            contactPoint: evt.ContactPoint);
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
        ArmorPiercingSwordAura.Handle<ArtifactHealthDamageEvent>(
            (_, _, _, hit) => hit.Target?.isAlive() == true && hit.Target.hasHealth(),
            ApplyArmorPiercingSwordAura);
    }

    private static void ApplyArmorPiercingSwordAura(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactHealthDamageEvent evt)
    {
        Actor target = evt.Target;
        float armorLoss = Mathf.Max(1f, target.stats[S.armor] * ability.GetNumber(StatusStrength));
        Entity fracture = CombatStatusEffects.ApplyStatusWithStat(
            target,
            StatusEffects.ArmorBreak,
            ability.GetNumber(StatusDuration),
            S.armor,
            -armorLoss,
            Controller(context));
        if (fracture.IsNull) return;
        Vector3 direction = (Vector2)target.current_position - evt.Attacker.current_position;
        float height = SpatialActorPose.Height(target);
        Vector3 point = evt.ContactPoint ?? SpatialActorPose.Foot(target) +
            Vector3.forward * height * .5f - direction.normalized * (height * .23f);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Hit,
            point,
            direction: direction,
            target: target,
            intensity: armorLoss,
            contactPoint: evt.ContactPoint);
    }

    private static void ConfigureSwordAbilityVisuals()
    {
        ConfigureSplittingSwordArrayVisuals();
        ConfigureReturningBladeGuardVisuals();
        ConfigureArmorPiercingSwordAuraVisuals();
    }

    private static void ConfigureSplittingSwordArrayVisuals()
    {
        SplittingSwordArray.Visualize(
            Theme(SkillVfxElements.Metal.AccentColor)
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
        ReturningBladeGuard.Visualize(
            Theme(SkillVfxElements.Metal.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Counter,
                    ShieldImpact(.09f),
                    0.2f
                )
        );
    }

    private static void ConfigureArmorPiercingSwordAuraVisuals()
    {
        ArtifactLocalVisualCue fracture = new(ArtifactLocalVisualKind.ArmorFragments)
        {
            anchor = ArtifactVisualAnchorKind.Point,
            color_role = ArtifactVisualColorRole.Primary,
            scale = .086f,
            alpha = .96f,
            loop = false,
        };
        ArmorPiercingSwordAura.Visualize(
            Theme(SkillVfxElements.Metal.AccentColor)
                .Signal(
                    ArtifactVisualChannels.Hit,
                    fracture,
                    .36f,
                    "artifact.armor_break",
                    ArtifactVisualStackPolicy.Strongest
                )
        );
    }
}

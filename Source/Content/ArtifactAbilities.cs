using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Artifacts.Events;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 基础法器能力。能力只声明组合规则、参数和领域事件处理，持续空间运动由独立系统驱动。
/// </summary>
[Dependency(typeof(ArtifactAtoms))]
public class ArtifactAbilities : ExtendLibrary<ArtifactAbilityAsset, ArtifactAbilities>
{
    private const string DamageMultiplier = "damage_multiplier";
    private const string FlightSpeed = "flight_speed";
    private const string AttackRange = "attack_range";
    private const string TurnRate = "turn_rate";
    private const string PierceDistance = "pierce_distance";
    private const string Cooldown = "cooldown";
    private const string ReadyAt = "ready_at";
    private const string ProgressBonus = "progress_bonus";
    private const string DurationMultiplier = "duration_multiplier";
    private const string QualityBonus = "quality_bonus";

    public static ArtifactAbilityAsset FlyingSwordAttack { get; private set; }
    public static ArtifactAbilityAsset DingAlchemyAssist { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ArtifactAbility";

    protected override void OnInit()
    {
        ConfigureFlyingSwordAttack();
        ConfigureDingAlchemyAssist();
    }

    private static void ConfigureFlyingSwordAttack()
    {
        FlyingSwordAttack.name_key = "Cultiway.ArtifactAbility.FlyingSwordAttack";
        FlyingSwordAttack.tags = ["active", "offensive", "spatial"];
        FlyingSwordAttack.exclusive_group = "spatial_attack";
        FlyingSwordAttack.minimum_score = 1f;
        FlyingSwordAttack.use_profile = new ArtifactUseProfile { offensive = 1f };
        FlyingSwordAttack.control_complexity = 0.35f;
        FlyingSwordAttack.thread_cost = 1;
        FlyingSwordAttack.parameter_schema =
        [
            NumberSpec(DamageMultiplier),
            NumberSpec(FlightSpeed),
            NumberSpec(AttackRange),
            NumberSpec(TurnRate),
            NumberSpec(PierceDistance),
            NumberSpec(Cooldown),
        ];
        FlyingSwordAttack.state_schema = [NumberSpec(ReadyAt)];
        FlyingSwordAttack.ScoreRecipe = context => context.shape == ItemShapes.Sword ? 100f : 0f;
        FlyingSwordAttack.ComposeParameters = context =>
        {
            int quality = Quality(context);
            return
            [
                ArtifactAbilityValue.Number(DamageMultiplier, 0.9f + quality * 0.055f),
                ArtifactAbilityValue.Number(FlightSpeed, 20f + quality * 0.35f),
                ArtifactAbilityValue.Number(AttackRange, 18f + quality * 0.25f),
                ArtifactAbilityValue.Number(TurnRate, 180f + quality * 2.5f),
                ArtifactAbilityValue.Number(PierceDistance, 2.4f + quality * 0.04f),
                ArtifactAbilityValue.Number(Cooldown, Mathf.Max(0.8f, 3.2f - quality * 0.06f)),
            ];
        };
        FlyingSwordAttack.ComposeInitialState = _ => [ArtifactAbilityValue.Number(ReadyAt, 0f)];
        FlyingSwordAttack.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.FlyingSwordAttack.Description"),
            ability.GetNumber(DamageMultiplier),
            ability.GetNumber(FlightSpeed),
            ability.GetNumber(AttackRange),
            ability.GetNumber(TurnRate),
            ability.GetNumber(PierceDistance),
            ability.GetNumber(Cooldown));
        FlyingSwordAttack.Handle<ArtifactSpatialAttackEvent>(CanLaunchFlyingSword, LaunchFlyingSword);
    }

    private static void ConfigureDingAlchemyAssist()
    {
        DingAlchemyAssist.name_key = "Cultiway.ArtifactAbility.DingAlchemyAssist";
        DingAlchemyAssist.tags = ["passive", "production", "alchemy"];
        DingAlchemyAssist.exclusive_group = "alchemy_assist";
        DingAlchemyAssist.minimum_score = 1f;
        DingAlchemyAssist.use_profile = new ArtifactUseProfile { production = 1f };
        DingAlchemyAssist.control_complexity = 0.2f;
        DingAlchemyAssist.parameter_schema =
        [
            IntegerSpec(ProgressBonus),
            NumberSpec(DurationMultiplier),
            IntegerSpec(QualityBonus),
        ];
        DingAlchemyAssist.ScoreRecipe = context => context.shape == ItemShapes.Ding ? 100f : 0f;
        DingAlchemyAssist.ComposeParameters = context =>
        {
            int quality = Quality(context);
            return
            [
                ArtifactAbilityValue.Integer(ProgressBonus, 1 + quality / 9),
                ArtifactAbilityValue.Number(DurationMultiplier, Mathf.Max(0.35f, 0.82f - quality * 0.012f)),
                ArtifactAbilityValue.Integer(QualityBonus, 1 + quality / 12),
            ];
        };
        DingAlchemyAssist.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.DingAlchemyAssist.Description"),
            ability.GetInteger(ProgressBonus),
            ability.GetNumber(DurationMultiplier),
            ability.GetInteger(QualityBonus));
        DingAlchemyAssist.Handle<ElixirCraftStepEvent>(IsOperating<ElixirCraftStepEvent>, ApplyAlchemyStepAssist);
        DingAlchemyAssist.Handle<ElixirCraftResultEvent>(IsOperating<ElixirCraftResultEvent>, ApplyAlchemyResultAssist);
    }

    private static bool CanLaunchFlyingSword(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        ArtifactSpatialAttackEvent evt)
    {
        if (!IsOperating(context) || evt.Target.isRekt()) return false;
        if (context.artifact.HasComponent<ArtifactIndependentMotion>()) return false;
        if (runtime.GetNumber(ReadyAt) > World.world.getCurWorldTime()) return false;

        Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
        float range = ability.GetNumber(AttackRange) + evt.Target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(controller.current_position, evt.Target.current_position) <= range * range;
    }

    private static void LaunchFlyingSword(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactSpatialAttackEvent evt)
    {
        Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
        Entity artifact = context.artifact;
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        bool initialized = ArtifactManifestationTools.EnsureWorldComponents(artifact, shape.presentation.body_radius);
        float actorScale = Mathf.Max(controller.stats[S.scale], 0.1f) * 10f;

        ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
        manifestation.control_state = context.control_state;
        manifestation.visible = controller.is_visible;
        manifestation.world_size = actorScale * 0.82f;
        manifestation.flip_x = false;
        manifestation.sorting_order = 12;
        artifact.GetComponent<ArtifactBody>().radius = shape.presentation.body_radius * manifestation.world_size;
        if (initialized)
        {
            artifact.GetComponent<Position>().value = controller.cur_transform_position + Vector3.up * actorScale * 0.55f;
        }

        Vector2 direction = evt.Target.current_position - artifact.GetComponent<Position>().v2;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;

        artifact.AddComponent(new ArtifactIndependentMotion());
        artifact.AddComponent(new ArtifactSpatialAttackMotion
        {
            owner_actor_id = controller.data.id,
            target_id = evt.Target.getID(),
            target_is_actor = evt.Target.isActor(),
            direction = direction.normalized,
            speed = ability.GetNumber(FlightSpeed),
            turn_rate = ability.GetNumber(TurnRate),
            damage_multiplier = ability.GetNumber(DamageMultiplier),
            control_range = ability.GetNumber(AttackRange),
            hit_radius = shape.presentation.body_radius * manifestation.world_size,
            pierce_distance = ability.GetNumber(PierceDistance),
            repeat_cooldown = ability.GetNumber(Cooldown),
            orbit_sign = (artifact.Id & 1) == 0 ? 1f : -1f,
            hit_target_keys = new HashSet<long>(),
            phase = ArtifactSpatialAttackPhase.Pursuing,
        });
        runtime.SetNumber(ReadyAt, (float)World.world.getCurWorldTime() + ability.GetNumber(Cooldown));
    }

    private static bool IsOperating<TEvent>(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance _,
        ArtifactAbilityRuntimeEntry __,
        TEvent ___)
        where TEvent : class
    {
        return IsOperating(context);
    }

    private static bool IsOperating(ArtifactAbilityExecutionContext context)
    {
        return context.control_state is ArtifactControlState.Operating or ArtifactControlState.Overloaded;
    }

    private static void ApplyAlchemyStepAssist(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry __,
        ElixirCraftStepEvent evt)
    {
        evt.ProgressGain += ability.GetInteger(ProgressBonus);
        evt.Duration *= ability.GetNumber(DurationMultiplier);
    }

    private static void ApplyAlchemyResultAssist(
        ArtifactAbilityExecutionContext _,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry __,
        ElixirCraftResultEvent evt)
    {
        evt.QualityBonus += ability.GetInteger(QualityBonus);
    }

    private static int Quality(ArtifactAbilityComposeContext context)
    {
        return context.recipe.quality_stage * 9 + context.recipe.quality_level;
    }

    private static ArtifactAbilityValueSpec NumberSpec(string key)
    {
        return new ArtifactAbilityValueSpec { key = key, kind = ArtifactAbilityValueKind.Number, required = true };
    }

    private static ArtifactAbilityValueSpec IntegerSpec(string key)
    {
        return new ArtifactAbilityValueSpec { key = key, kind = ArtifactAbilityValueKind.Integer, required = true };
    }
}

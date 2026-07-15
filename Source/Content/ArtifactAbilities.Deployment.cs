using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;
using UnityEngine;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private const string FieldRadius = "field_radius";
    private const string FieldDuration = "field_duration";
    private const string StatusDuration = "status_duration";
    private const string ActivationCost = "activation_cost";

    public static ArtifactAbilityAsset SuppressionField { get; private set; }

    private static void ConfigureSuppressionField()
    {
        SuppressionField.name_key = "Cultiway.ArtifactAbility.SuppressionField";
        SuppressionField.tags = ["active", "offensive", "deployment", "field"];
        SuppressionField.exclusive_group = "suppression_field";
        SuppressionField.minimum_score = 1f;
        SuppressionField.use_profile = new ArtifactUseProfile { offensive = 0.55f, support = 0.45f };
        SuppressionField.control_complexity = 0.32f;
        SuppressionField.thread_cost = 1;
        SuppressionField.parameter_schema =
        [
            NumberSpec(AttackRange),
            NumberSpec(FieldRadius),
            NumberSpec(FieldDuration),
            NumberSpec(StatusDuration),
            NumberSpec(Cooldown),
            NumberSpec(ActivationCost),
            NumberSpec(MaintenanceCost),
        ];
        SuppressionField.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.FieldProjection) *
            (1f + context.GetTrait(ArtifactMaterialTraits.Suppression) * 0.17f +
             context.GetTrait(ArtifactMaterialTraits.Stability) * 0.06f);
        SuppressionField.ComposeParameters = context =>
        {
            int quality = Quality(context);
            float suppression = Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Suppression));
            float capacity = Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Capacity));
            return
            [
                ArtifactAbilityValue.Number(AttackRange, 9f + quality * 0.22f + capacity * 0.65f),
                ArtifactAbilityValue.Number(FieldRadius, 2.5f + quality * 0.055f + suppression * 0.3f),
                ArtifactAbilityValue.Number(FieldDuration, 5f + quality * 0.18f + capacity * 0.7f),
                ArtifactAbilityValue.Number(StatusDuration, 1.2f + suppression * 0.2f),
                ArtifactAbilityValue.Number(Cooldown, Mathf.Max(4f, 13f - quality * 0.16f)),
                ArtifactAbilityValue.Number(ActivationCost, 2f + quality * 0.12f + capacity * 0.25f),
                ArtifactAbilityValue.Number(MaintenanceCost, 0.2f + quality * 0.018f + suppression * 0.04f),
            ];
        };
        SuppressionField.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.SuppressionField.Description"),
            ability.GetNumber(AttackRange),
            ability.GetNumber(FieldRadius),
            ability.GetNumber(FieldDuration),
            ability.GetNumber(Cooldown),
            ability.GetNumber(ActivationCost),
            ability.GetNumber(MaintenanceCost));
        SuppressionField.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            active_minimum_state = ArtifactControlState.Operating,
            sustain_minimum_state = ArtifactControlState.Operating,
            tick_minimum_state = ArtifactControlState.Operating,
            tick_interval = 0.5f,
            tick_requires_activity = true,
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveDuration = (_, ability) => ability.GetNumber(FieldDuration),
            ResolveActivationCost = (_, ability) => ability.GetNumber(ActivationCost),
            ResolveMaintenanceCost = (_, ability) => ability.GetNumber(MaintenanceCost),
            Resource = UseWakan,
            OnTick = ApplySuppressionField,
        });
        SuppressionField.Activate(new ArtifactActiveAbilityProfile
        {
            channels = ActiveAbilityChannel.Combat,
            target_mode = ActiveAbilityTargetMode.Point,
            activation_mode = ActiveAbilityActivationMode.Sustained,
            ai_weight = 6,
            ResolveRange = (_, ability) => ability.GetNumber(AttackRange),
            CanPrepare = CanPrepareSuppressionField,
            CanUse = CanDeploySuppressionField,
            TryUse = DeploySuppressionField,
        });
    }

    private static bool CanPrepareSuppressionField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance _,
        ArtifactAbilityRuntimeEntry __,
        BaseSimObject ___)
    {
        return !context.artifact.HasComponent<ArtifactIndependentMotion>() &&
               !context.artifact.HasComponent<SkillExecutionBodyLease>();
    }

    private static bool CanDeploySuppressionField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry _,
        in ActiveAbilityTarget target)
    {
        if (context.artifact.HasComponent<ArtifactIndependentMotion>() ||
            context.artifact.HasComponent<SkillExecutionBodyLease>()) return false;
        Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
        Vector3 position = target.Object?.GetSimPos() ?? target.Position;
        float range = ability.GetNumber(AttackRange);
        return Toolbox.SquaredDistVec2Float(controller.current_position, position) <= range * range;
    }

    private static bool DeploySuppressionField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin _)
    {
        Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
        ArtifactShapeAsset shape = (ArtifactShapeAsset)context.artifact.GetComponent<ItemShape>().Type;
        float actorScale = Mathf.Max(controller.stats[S.scale], 0.1f) * 10f;
        Vector3 position = target.Object?.GetSimPos() ?? target.Position;
        return ArtifactAbilityLifecycle.Deploy(
            context,
            ability,
            ref runtime,
            position,
            actorScale * shape.presentation.active_world_size);
    }

    private static void ApplySuppressionField(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry _,
        float __)
    {
        Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
        Vector2 position = context.artifact.GetComponent<Position>().v2;
        WorldTile tile = World.world.GetTile(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        if (tile == null) return;

        float radius = ability.GetNumber(FieldRadius);
        float statusDuration = ability.GetNumber(StatusDuration);
        int chunkRadius = Mathf.Max(1, Mathf.CeilToInt(radius / 8f));
        foreach (Actor target in Finder.getUnitsFromChunk(tile, chunkRadius, radius))
        {
            if (target == controller || !controller.canAttackTarget(target)) continue;
            ApplyArtifactStatus(target, StatusEffects.Slow, statusDuration, S.multiplier_speed, -0.45f, controller);
            ApplyArtifactStatus(target, StatusEffects.Weaken, statusDuration, S.multiplier_damage, -0.12f, controller);
        }
    }

    private static void ApplyArtifactStatus(
        Actor target,
        StatusEffectAsset effect,
        float duration,
        string statId,
        float statValue,
        Actor source)
    {
        ActorExtend targetExtend = target.GetExtend();
        foreach (Entity status in targetExtend.GetStatuses())
        {
            StatusComponent component = status.GetComponent<StatusComponent>();
            if (component.Type != effect || component.Source != source) continue;
            SetArtifactStatus(status, duration, statId, statValue, source);
            return;
        }

        Entity created = effect.NewEntity();
        SetArtifactStatus(created, duration, statId, statValue, source);
        if (!targetExtend.AddSharedStatus(created))
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(created.Id);
        }
    }

    private static void SetArtifactStatus(
        Entity status,
        float duration,
        string statId,
        float statValue,
        Actor source)
    {
        status.GetComponent<AliveTimeLimit>().value = duration;
        status.GetComponent<AliveTimer>().value = 0f;
        BaseStats stats;
        if (status.HasComponent<StatusOverwriteStats>())
        {
            ref StatusOverwriteStats overwrite = ref status.GetComponent<StatusOverwriteStats>();
            stats = overwrite.stats ??= new BaseStats();
            stats.clear();
        }
        else
        {
            stats = new BaseStats();
            status.AddComponent(new StatusOverwriteStats { stats = stats });
        }
        stats[statId] = statValue;
        ref StatusComponent component = ref status.GetComponent<StatusComponent>();
        component.Source = source;
        component.SourcePowerLevel = source.GetExtend().GetPowerLevel();
    }
}

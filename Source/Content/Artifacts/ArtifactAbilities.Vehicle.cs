using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using NeoModLoader.General;
using strings;
using UnityEngine;
using Cultiway.Content.Visuals;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private const string VehicleSpeedMultiplier = "vehicle_speed_multiplier";
    private const string PassengerCapacity = "passenger_capacity";

    /// <summary>御器载运被动；将法器作为飞行载具，提供飞行速度倍率和可搭载人数。</summary>
    public static ArtifactAbilityAsset ArtifactRiding { get; private set; }

    private static void ConfigureVehicleAbilities()
    {
        ConfigureArtifactRiding();
    }

    private static void ConfigureArtifactRiding()
    {
        ArtifactRiding.name_key = "Cultiway.ArtifactAbility.ArtifactRiding";
        ArtifactRiding.SetSemantics(ArtifactSemantics.Effect.Movement, ArtifactSemantics.Role.Vehicle);
        ArtifactRiding.exclusivity = ArtifactAbilityExclusivity.ArtifactVehicle;
        ArtifactRiding.manifestation_cost = 0.9f;
        ArtifactRiding.AddSynergies(
            ArtifactSemantics.Effect.Mobility,
            ArtifactSemantics.Theme.Space,
            ArtifactSemantics.Delivery.Projection);
        ArtifactRiding.AddConflicts(ArtifactSemanticRules.ImmobileCore);
        ArtifactRiding.minimum_score = 1f;
        ArtifactRiding.use_profile = new ArtifactUseProfile { support = 0.65f };
        ArtifactRiding.control_complexity = 0.2f;
        ArtifactRiding.thread_cost = 1;
        ArtifactRiding.parameter_schema =
        [
            NumberSpec(VehicleSpeedMultiplier),
            IntegerSpec(PassengerCapacity),
        ];
        ArtifactRiding.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.Vehicle) *
            (0.65f + context.GetTrait(ArtifactMaterialTraits.Mobility) * 0.38f +
             context.GetTrait(ArtifactMaterialTraits.Space) * 0.16f +
             context.GetTrait(ArtifactMaterialTraits.Capacity) * 0.12f);
        ArtifactRiding.ComposeParameters = context =>
        [
            ArtifactAbilityValue.Number(
                VehicleSpeedMultiplier,
                1.12f + context.scales.Efficiency * 0.09f +
                Mathf.Min(8f, context.GetTrait(ArtifactMaterialTraits.Mobility)) * 0.055f),
            ArtifactAbilityValue.Integer(
                PassengerCapacity,
                Mathf.Max(1, Mathf.FloorToInt(context.scales.Capacity * 0.8f +
                                              context.GetTrait(ArtifactMaterialTraits.Capacity) * 0.4f))),
        ];
        ArtifactRiding.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.ArtifactRiding.Description"),
            ability.GetNumber(VehicleSpeedMultiplier),
            ability.GetInteger(PassengerCapacity));
        ArtifactRiding.ProvideVehicle(new ArtifactVehicleAbilityProfile
        {
            minimum_state = ArtifactControlState.Operating,
            ResolveSpeedMultiplier = ability => ability.GetNumber(VehicleSpeedMultiplier),
            ResolvePassengerCapacity = ability => ability.GetInteger(PassengerCapacity),
        });
    }

    private static void ConfigureVehicleAbilityVisuals()
    {
        ArtifactParticleVisualCue vehicleWind = Burst(
                    ArtifactVisualAnchorKind.Artifact,
                    ArtifactVisualColorRole.Glow,
                    2,
                    0.18f);
        vehicleWind.emission_interval = 0.1f;
        vehicleWind.directional_speed = -0.45f;
        ArtifactTrailVisualCue vehicleWake = new()
        {
            style_key = ArtifactVfxStyles.Vehicle,
            anchor = ArtifactVisualAnchorKind.Artifact,
            color_role = ArtifactVisualColorRole.Glow,
            width = 0.11f,
            alpha = 0.48f,
            history = 0.3f,
            min_distance = 0.025f,
            max_points = 28,
            match_actor_scale = false,
        };
        ArtifactRiding.Visualize(Theme(SkillVfxElements.Wind.AccentColor)
            .Loop(
                "vehicle_wake",
                new ArtifactCompositeVisualCue(vehicleWind, vehicleWake, Pulse(1f, 1f, 0.055f)),
                IsVehicleBody,
                "artifact.vehicle.active",
                ArtifactVisualStackPolicy.Independent,
                context => context.ability.GetNumber(VehicleSpeedMultiplier)));
    }

    private static bool IsVehicleBody(ArtifactAbilityVisualContext context)
    {
        foreach (Entity owner in context.artifact.GetIncomingLinks<ArtifactVehicleRelation>().Entities)
        {
            var relations = owner.GetRelations<ArtifactVehicleRelation>();
            for (int i = 0; i < relations.Length; i++)
            {
                if (relations[i].artifact == context.artifact &&
                    relations[i].ability_instance_id == context.ability.instance_id) return true;
            }
        }
        return false;
    }
}

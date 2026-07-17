using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Semantics;
using Cultiway.Core.Components;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private const string DamageReduction = "damage_reduction";
    private const string MaxCharges = "max_charges";
    private const string Recharge = "recharge";

    /// <summary>受击防护被动；消耗可恢复的守护充能，按比例降低本次受到的伤害。</summary>
    public static ArtifactAbilityAsset GuardianWard { get; private set; }

    private static void ConfigureGuardianWard()
    {
        GuardianWard.name_key = "Cultiway.ArtifactAbility.GuardianWard";
        GuardianWard.SetSemantics(ArtifactSemantics.Effect.Guardian, ArtifactSemantics.Effect.Ward);
        GuardianWard.exclusivity = ArtifactAbilityExclusivity.GuardianReaction;
        GuardianWard.minimum_score = 1f;
        GuardianWard.use_profile = new ArtifactUseProfile { defensive = 1f, support = 0.25f };
        GuardianWard.control_complexity = 0.2f;
        GuardianWard.parameter_schema =
        [
            NumberSpec(DamageReduction),
            NumberSpec(Cooldown),
            IntegerSpec(MaxCharges),
            NumberSpec(Recharge),
        ];
        GuardianWard.ScoreRecipe = context =>
            context.GetTrait(ArtifactMaterialTraits.GuardianWard) *
            (1f + context.GetTrait(ArtifactMaterialTraits.Ward) * 0.14f +
             context.GetTrait(ArtifactMaterialTraits.Flexibility) * 0.06f);
        GuardianWard.ComposeParameters = context =>
        {
            int quality = Quality(context);
            float ward = Mathf.Min(7f, context.GetTrait(ArtifactMaterialTraits.Ward));
            float hardness = Mathf.Min(7f, context.GetTrait(ArtifactMaterialTraits.Hardness));
            return
            [
                ArtifactAbilityValue.Number(
                    DamageReduction,
                    Mathf.Clamp(0.12f + quality * 0.006f + ward * 0.035f + hardness * 0.012f, 0.12f, 0.72f)),
                ArtifactAbilityValue.Number(Cooldown, Mathf.Max(0.35f, 1.25f - quality * 0.018f)),
                ArtifactAbilityValue.Integer(MaxCharges, 1 + quality / 12),
                ArtifactAbilityValue.Number(Recharge, Mathf.Max(2.5f, 8f - quality * 0.11f - ward * 0.25f)),
            ];
        };
        GuardianWard.DescribeInstance = ability => string.Format(
            LM.Get("Cultiway.ArtifactAbility.GuardianWard.Description"),
            ability.GetNumber(DamageReduction),
            ability.GetInteger(MaxCharges),
            ability.GetNumber(Recharge));
        GuardianWard.ConfigureLifecycle(new ArtifactAbilityLifecycleProfile
        {
            event_minimum_state = ArtifactControlState.Ready,
            event_consumes_trigger = true,
            ResolveMaxCharges = (_, ability) => ability.GetInteger(MaxCharges),
            ResolveCooldown = (_, ability) => ability.GetNumber(Cooldown),
            ResolveRecharge = (_, ability) => ability.GetNumber(Recharge),
        });
        GuardianWard.Handle<ArtifactIncomingDamageEvent>(
            (_, _, _, evt) => evt.Damage > 0f,
            ApplyGuardianWard);
    }

    private static void ApplyGuardianWard(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent evt)
    {
        float reduction = ability.GetNumber(DamageReduction);
        evt.Damage *= 1f - reduction;
        Actor defender = context.controller.GetComponent<ActorBinder>().Actor;
        Vector3 direction = evt.Attacker != null && !evt.Attacker.isRekt()
            ? evt.Attacker.current_position - defender.current_position
            : Vector3.up;
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.Guard,
            direction: direction,
            target: evt.Attacker,
            intensity: reduction);
    }
}

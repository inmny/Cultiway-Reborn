using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Content.Visuals;
using UnityEngine;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    private static void ClampRecoveringShield(
        ArtifactAbilityExecutionContext _, ArtifactAbilityInstance ability, ref ArtifactAbilityRuntimeEntry runtime)
    {
        runtime.SetNumber(ShieldCurrent, Mathf.Clamp(runtime.GetNumber(ShieldCurrent), 0f, ability.GetNumber(ShieldCapacity)));
    }

    private static void RechargeArtifactShield(
        ArtifactAbilityExecutionContext _, ArtifactAbilityInstance ability, ref ArtifactAbilityRuntimeEntry runtime, float elapsed)
    {
        runtime.SetNumber(ShieldCurrent, Mathf.Min(ability.GetNumber(ShieldCapacity),
            runtime.GetNumber(ShieldCurrent) + ability.GetNumber(ShieldRegen) * elapsed));
    }

    /// <summary>受击只扣当前载体能用的那一部分原护盾，余量仍归原件保存。</summary>
    private static void AbsorbWithRecoveringShield(
        ArtifactAbilityExecutionContext context, ArtifactAbilityInstance ability, ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactIncomingDamageEvent hit)
    {
        float stored = runtime.GetNumber(ShieldCurrent);
        float capacity = ability.GetNumber(ShieldCapacity);
        float accessible = Mathf.Min(stored, capacity * context.effect_scale);
        float spent = CombatDamageEffects.AbsorbDamage(ref hit.Damage, ref accessible, hit.Target);
        if (spent <= 0f) return;
        float remaining = Mathf.Max(0f, stored - spent);
        runtime.SetNumber(ShieldCurrent, remaining);
        ArtifactAbilityVisuals.Emit(context, ability, runtime, ArtifactVisualChannels.Guard,
            target: hit.Attacker, intensity: spent / Mathf.Max(1f, capacity),
            resourceDepleted: remaining <= 0f, contactPoint: hit.ContactPoint);
    }

    private static bool HasRecoveringShield(ArtifactAbilityVisualContext context) =>
        context.runtime.GetNumber(ShieldCurrent) > 0f &&
        ArtifactAbilityLifecycle.MeetsState(context.control_state, ArtifactControlState.Ready);

    private static float RecoveringShieldRatio(ArtifactAbilityVisualContext context) =>
        context.runtime.GetNumber(ShieldCurrent) / Mathf.Max(1f, context.ability.GetNumber(ShieldCapacity));

    private static ArtifactLocalVisualCue ShieldImpact(float size) =>
        new(ArtifactLocalVisualKind.ShieldImpact)
        {
            anchor = ArtifactVisualAnchorKind.Controller,
            scale = size,
            loop = false,
        };
}

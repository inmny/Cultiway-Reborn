using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

public partial class ArtifactAbilities
{
    /// <summary>按原比例分流来伤，目的地反馈等真正结算后再出现。</summary>
    private static void RedirectArtifactDamage(ArtifactAbilityExecutionContext context, ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime, ArtifactIncomingDamageEvent incoming, float ratio, string channel)
    {
        float amount = incoming.Damage * ratio;
        float removed = amount * context.effect_scale;
        if (removed <= 0f) return;
        incoming.Damage -= removed;
        Actor target = incoming.Attacker.a;
        Actor protectedActor = incoming.Target.Base;
        Vector3 origin = incoming.ContactPoint ?? SpatialActorPose.Foot(protectedActor) +
            Vector3.forward * SpatialActorPose.Height(protectedActor) * .55f;
        var hit = CombatDamageEffects.PrepareDamage(Controller(context), amount, incoming.DamageComposition,
            incoming.IgnoreDamageReduction, damageOrigin: DamageOrigin.Reaction);
        long targetId = target.id;
        hit.TargetID = targetId;
        hit.Observation = new DamageObservation(hit.AttackerID, targetId, outcome =>
        {
            if ((outcome.HealthLost <= 0 && outcome.ShieldAbsorbed <= 0f) || target.data?.id != targetId) return;
            Vector3 point = SpatialActorPose.Foot(target) + Vector3.forward * SpatialActorPose.Height(target) * .55f;
            ArtifactAbilityVisuals.Emit(context, ability, runtime, ArtifactVisualChannels.Hit, position: point,
                direction: point - origin, intensity: ratio, contactPoint: point);
        }) { Pending = true };
        EventSystemHub.Publish(hit);
        ArtifactAbilityVisuals.Emit(context, ability, runtime, channel, position: origin,
            target: target, intensity: ratio, contactPoint: origin);
    }
}

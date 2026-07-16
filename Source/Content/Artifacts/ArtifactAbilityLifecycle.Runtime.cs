using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 通用能力生命周期的运行状态推进部分。
/// </summary>
public static partial class ArtifactAbilityLifecycle
{
    private static bool CanTrigger(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime,
        bool rejectActive)
    {
        ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
        double now = Now;
        if (rejectActive && runtime.activity_kind != ArtifactAbilityActivityKind.None) return false;
        if (runtime.cooldown_until > now) return false;

        int maxCharges = ResolveMaxCharges(profile, context, ability);
        int charges = runtime.lifecycle_initialized ? runtime.charges : maxCharges;
        if (maxCharges > 0 && charges <= 0) return false;

        float cost = ResolveCost(profile.ResolveActivationCost, profile, context, ability);
        return cost <= 0f || profile.Resource?.Invoke(context, ability, cost, false) == true;
    }

    private static void CommitTrigger(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        bool beginActivity)
    {
        ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
        double now = Now;
        float activationCost = ResolveCost(profile.ResolveActivationCost, profile, context, ability);
        if (activationCost > 0f)
        {
            profile.Resource(context, ability, activationCost, true);
        }

        int maxCharges = ResolveMaxCharges(profile, context, ability);
        if (maxCharges > 0)
        {
            runtime.charges--;
            if (runtime.next_charge_at <= 0d)
            {
                runtime.next_charge_at = now + ResolveRecharge(profile, context, ability);
            }
        }

        float cooldown = Mathf.Max(0f, profile.ResolveCooldown?.Invoke(context, ability) ?? 0f);
        if (context.control_state == ArtifactControlState.Overloaded)
        {
            cooldown *= Mathf.Max(0f, profile.overload_cooldown_multiplier);
        }
        runtime.cooldown_until = now + cooldown;
        runtime.last_triggered_at = now;

        if (beginActivity)
        {
            float duration = Mathf.Max(0f, profile.ResolveDuration?.Invoke(context, ability) ?? 0f);
            if (duration > 0f && runtime.activity_kind == ArtifactAbilityActivityKind.None)
            {
                runtime.activity_kind = ArtifactAbilityActivityKind.Timed;
            }
            if (runtime.activity_kind != ArtifactAbilityActivityKind.None)
            {
                runtime.activity_started_at = now;
                runtime.activity_until = duration > 0f ? now + duration : 0d;
                if (runtime.activity_kind == ArtifactAbilityActivityKind.Deployment &&
                    context.artifact.TryGetComponent(out ArtifactDeployment deployment) &&
                    deployment.ability_instance_id == ability.instance_id)
                {
                    deployment.expires_at = runtime.activity_until;
                    context.artifact.GetComponent<ArtifactDeployment>() = deployment;
                }
            }
        }
        MarkStatsDirty(context.controller);
    }

    private static void Attach(
        Entity controller,
        Entity artifact,
        ArtifactControlState state,
        ArtifactAbilitySet abilitySet,
        ref ArtifactAbilityRuntime runtime)
    {
        runtime.controller = controller;
        runtime.control_state = state;
        runtime.attached = true;
        ArtifactAbilityExecutionContext context = new(controller, artifact, state);
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            EnsureInitialized(asset, context, ability, ref runtime.abilities[i]);
            asset.lifecycle.OnAttached?.Invoke(context, ability, ref runtime.abilities[i]);
        }
        MarkStatsDirty(controller);
    }

    private static void Detach(
        Entity artifact,
        ArtifactAbilitySet abilitySet,
        ref ArtifactAbilityRuntime runtime,
        ArtifactAbilityEndReason reason)
    {
        Entity controller = runtime.controller;
        ArtifactAbilityExecutionContext context = new(controller, artifact, runtime.control_state);
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            if (runtime.abilities[i].activity_kind != ArtifactAbilityActivityKind.None)
            {
                EndActivity(asset, context, ability, ref runtime.abilities[i], reason, true);
            }
            asset.lifecycle.OnDetached?.Invoke(context, ability, ref runtime.abilities[i]);
        }
        runtime.controller = default;
        runtime.control_state = ArtifactControlState.Cold;
        runtime.attached = false;
        MarkStatsDirty(controller);
    }

    private static void ChangeControlState(
        Entity controller,
        Entity artifact,
        ArtifactControlState state,
        ArtifactAbilitySet abilitySet,
        ref ArtifactAbilityRuntime runtime)
    {
        ArtifactControlState previousState = runtime.control_state;
        runtime.control_state = state;
        ArtifactAbilityExecutionContext context = new(controller, artifact, state);
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
            profile.OnControlStateChanged?.Invoke(context, ability, ref runtime.abilities[i], previousState);
            if (profile.interrupt_activity_on_state_loss &&
                runtime.abilities[i].activity_kind != ArtifactAbilityActivityKind.None &&
                !MeetsState(state, profile.sustain_minimum_state))
            {
                EndActivity(
                    asset,
                    context,
                    ability,
                    ref runtime.abilities[i],
                    ArtifactAbilityEndReason.ControlStateLost,
                    true);
            }
        }
        MarkStatsDirty(controller);
    }

    private static void Advance(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        double now)
    {
        ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
        EnsureInitialized(asset, context, ability, ref runtime);
        AdvanceCharges(profile, context, ability, ref runtime, now);

        if (runtime.activity_kind != ArtifactAbilityActivityKind.None)
        {
            if (profile.interrupt_activity_on_state_loss &&
                !MeetsState(context.control_state, profile.sustain_minimum_state))
            {
                EndActivity(
                    asset,
                    context,
                    ability,
                    ref runtime,
                    ArtifactAbilityEndReason.ControlStateLost,
                    true);
                return;
            }
            if (runtime.activity_until > 0d && now >= runtime.activity_until)
            {
                EndActivity(
                    asset,
                    context,
                    ability,
                    ref runtime,
                    ArtifactAbilityEndReason.DurationElapsed,
                    true);
                return;
            }
            if (runtime.activity_kind == ArtifactAbilityActivityKind.SkillExecution &&
                (runtime.active_execution.IsNull ||
                 !runtime.active_execution.HasComponent<SkillExecution>() ||
                 runtime.active_execution.GetComponent<SkillExecution>().end_requested))
            {
                EndActivity(asset, context, ability, ref runtime, ArtifactAbilityEndReason.Completed, false);
                return;
            }
            if (runtime.activity_kind == ArtifactAbilityActivityKind.Deployment &&
                (!context.artifact.TryGetComponent(out ArtifactDeployment deployment) ||
                 deployment.ability_instance_id != ability.instance_id))
            {
                EndActivity(asset, context, ability, ref runtime, ArtifactAbilityEndReason.Completed, false);
                return;
            }
        }

        if (profile.tick_interval <= 0f ||
            profile.OnTick == null && profile.ResolveMaintenanceCost == null ||
            !MeetsState(context.control_state, profile.tick_minimum_state) ||
            profile.tick_requires_activity && runtime.activity_kind == ArtifactAbilityActivityKind.None ||
            now < runtime.next_tick_at) return;

        runtime.next_tick_at = now + profile.tick_interval;
        if (profile.CanTick?.Invoke(context, ability, runtime) == false) return;

        float maintenanceCost = ResolveCost(profile.ResolveMaintenanceCost, profile, context, ability);
        if (maintenanceCost > 0f &&
            profile.Resource?.Invoke(context, ability, maintenanceCost, false) != true)
        {
            if (runtime.activity_kind != ArtifactAbilityActivityKind.None)
            {
                EndActivity(
                    asset,
                    context,
                    ability,
                    ref runtime,
                    ArtifactAbilityEndReason.ResourceDepleted,
                    true);
            }
            return;
        }
        if (maintenanceCost > 0f)
        {
            profile.Resource(context, ability, maintenanceCost, true);
        }
        if (profile.OnTick != null)
        {
            profile.OnTick(context, ability, ref runtime, profile.tick_interval);
            ArtifactAbilityVisuals.Emit(context, ability, runtime, ArtifactVisualChannels.Tick);
        }
    }

    private static void AdvanceCharges(
        ArtifactAbilityLifecycleProfile profile,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        double now)
    {
        int maxCharges = ResolveMaxCharges(profile, context, ability);
        if (maxCharges <= 0)
        {
            runtime.next_charge_at = 0d;
            return;
        }
        if (runtime.charges >= maxCharges)
        {
            runtime.charges = maxCharges;
            runtime.next_charge_at = 0d;
            return;
        }

        float recharge = ResolveRecharge(profile, context, ability);
        if (recharge <= 0f)
        {
            runtime.charges = maxCharges;
            runtime.next_charge_at = 0d;
            return;
        }
        if (runtime.next_charge_at <= 0d)
        {
            runtime.next_charge_at = now + recharge;
            return;
        }
        while (runtime.charges < maxCharges && now >= runtime.next_charge_at)
        {
            runtime.charges++;
            runtime.next_charge_at += recharge;
        }
        if (runtime.charges >= maxCharges) runtime.next_charge_at = 0d;
    }

    private static void EndActivity(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        ArtifactAbilityEndReason reason,
        bool requestExecutionEnd)
    {
        ArtifactAbilityActivityKind kind = runtime.activity_kind;
        Vector3? endPosition = null;
        if (kind == ArtifactAbilityActivityKind.SkillExecution && requestExecutionEnd &&
            !runtime.active_execution.IsNull && runtime.active_execution.HasComponent<SkillExecution>())
        {
            SkillExecutionLifecycle.RequestEnd(runtime.active_execution);
        }
        else if (kind == ArtifactAbilityActivityKind.Deployment &&
                 context.artifact.TryGetComponent(out ArtifactDeployment deployment) &&
                 deployment.ability_instance_id == ability.instance_id)
        {
            endPosition = deployment.origin;
            PendingDeploymentReleases.Add(new PendingDeploymentRelease(
                context.artifact,
                ability.instance_id));
        }

        runtime.activity_kind = ArtifactAbilityActivityKind.None;
        runtime.active_execution = default;
        runtime.activity_started_at = 0d;
        runtime.activity_until = 0d;
        asset.lifecycle.OnActivityEnded?.Invoke(context, ability, ref runtime, reason);
        ArtifactAbilityVisuals.Emit(
            context,
            ability,
            runtime,
            ArtifactVisualChannels.End,
            position: endPosition,
            endReason: reason);
        MarkStatsDirty(context.controller);
    }

    private static bool TryResolveController(
        Entity artifact,
        out Entity controller,
        out ArtifactControlState state)
    {
        foreach (Entity owner in artifact.GetIncomingLinks<EquippedArtifactRelation>().Entities)
        {
            Actor actor = owner.GetComponent<ActorBinder>().Actor;
            if (actor == null || !actor.isAlive()) continue;
            controller = owner;
            state = owner.GetRelation<EquippedArtifactRelation, Entity>(artifact).state;
            return true;
        }
        controller = default;
        state = ArtifactControlState.Cold;
        return false;
    }

    private static int ResolveMaxCharges(
        ArtifactAbilityLifecycleProfile profile,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability)
    {
        return Mathf.Max(0, profile.ResolveMaxCharges?.Invoke(context, ability) ?? 0);
    }

    private static float ResolveRecharge(
        ArtifactAbilityLifecycleProfile profile,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability)
    {
        return Mathf.Max(
            0f,
            profile.ResolveRecharge?.Invoke(context, ability) ??
            profile.ResolveCooldown?.Invoke(context, ability) ??
            0f);
    }

    private static float ResolveCost(
        ArtifactAbilityValueResolver resolver,
        ArtifactAbilityLifecycleProfile profile,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability)
    {
        float cost = Mathf.Max(0f, resolver?.Invoke(context, ability) ?? 0f);
        if (context.control_state == ArtifactControlState.Overloaded)
        {
            cost *= Mathf.Max(0f, profile.overload_resource_multiplier);
        }
        return cost;
    }

    private static void MarkStatsDirty(Entity controller)
    {
        if (controller.IsNull || !controller.HasComponent<ActorBinder>()) return;
        Actor actor = controller.GetComponent<ActorBinder>().Actor;
        if (actor != null) actor.GetExtend().MarkCultiwayStatsDirty();
    }

    private static double Now => World.world.getCurWorldTime();
}

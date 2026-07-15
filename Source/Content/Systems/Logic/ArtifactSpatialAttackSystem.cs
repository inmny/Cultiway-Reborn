using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 逐渲染帧驱动飞剑本体持续追击、穿刺、巡航和归位。
/// </summary>
public sealed class ArtifactSpatialAttackSystem
    : QuerySystem<Artifact, ArtifactSpatialAttackMotion, ArtifactManifestation, Position, Rotation>
{
    private const float ReacquireInterval = 0.12f;

    private readonly ArtifactSpatialTargeting _targeting = new();
    private readonly List<BaseSimObject> _frameHits = new();
    private readonly List<PendingHit> _pendingHits = new();

    protected override void OnUpdate()
    {
        if (ModClass.I.Game.IsPaused()) return;

        float deltaTime = Tick.deltaTime;
        float worldTime = (float)World.world.getCurWorldTime();
        _pendingHits.Clear();
        Query.ForEachEntity((
            ref Artifact _,
            ref ArtifactSpatialAttackMotion motion,
            ref ArtifactManifestation manifestation,
            ref Position position,
            ref Rotation rotation,
            Entity artifact) =>
        {
            Actor owner = World.world.units.get(motion.owner_actor_id);
            if (owner.isRekt())
            {
                FinishMotion(artifact);
                return;
            }

            manifestation.visible = owner.is_visible;
            if (motion.phase != ArtifactSpatialAttackPhase.Returning && !IsOperating(owner, artifact))
            {
                motion.phase = ArtifactSpatialAttackPhase.Returning;
            }

            switch (motion.phase)
            {
                case ArtifactSpatialAttackPhase.Pursuing:
                    UpdatePursuing(owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                    break;
                case ArtifactSpatialAttackPhase.Piercing:
                    UpdatePiercing(owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                    break;
                case ArtifactSpatialAttackPhase.Cruising:
                    UpdateCruising(owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                    break;
                case ArtifactSpatialAttackPhase.Returning:
                    UpdateReturning(owner, artifact, ref motion, ref position, ref rotation, deltaTime);
                    break;
            }
        });

        ResolvePendingHits();
    }

    private void UpdatePursuing(
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        float deltaTime,
        float worldTime)
    {
        BaseSimObject target = ArtifactSpatialTargeting.ResolveTarget(motion);
        if (!ArtifactSpatialTargeting.IsValidTarget(owner, target, motion.control_range))
        {
            if (!TryAcquireTarget(owner, ref motion, position.v2, worldTime))
            {
                motion.phase = ArtifactSpatialAttackPhase.Cruising;
                motion.reacquire_in = ReacquireInterval;
                UpdateCruising(owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                return;
            }
            target = ArtifactSpatialTargeting.ResolveTarget(motion);
        }

        Vector2 desiredDirection = ArtifactSpatialMotionTools.DirectionTo(
            position.v2,
            target.current_position,
            motion.direction);
        AdvanceAndResolveHits(
            owner,
            ref motion,
            ref position,
            ref rotation,
            desiredDirection,
            motion.turn_rate,
            deltaTime,
            worldTime);
    }

    private void UpdatePiercing(
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        float deltaTime,
        float worldTime)
    {
        float distance = AdvanceAndResolveHits(
            owner,
            ref motion,
            ref position,
            ref rotation,
            motion.direction,
            0f,
            deltaTime,
            worldTime);
        motion.pierce_remaining -= distance;
        if (motion.pierce_remaining > 0f) return;

        if (!TryAcquireTarget(owner, ref motion, position.v2, worldTime))
        {
            motion.phase = ArtifactSpatialAttackPhase.Cruising;
            motion.reacquire_in = ReacquireInterval;
        }
    }

    private void UpdateCruising(
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        float deltaTime,
        float worldTime)
    {
        float orbitRadius = Mathf.Clamp(motion.control_range * 0.45f, 7f, 12f);
        Vector2 desiredDirection = ArtifactSpatialMotionTools.ResolveCruiseDirection(
            owner.current_position,
            position.v2,
            motion.direction,
            orbitRadius,
            motion.orbit_sign);
        AdvanceAndResolveHits(
            owner,
            ref motion,
            ref position,
            ref rotation,
            desiredDirection,
            motion.turn_rate,
            deltaTime,
            worldTime);
        if (motion.phase == ArtifactSpatialAttackPhase.Piercing) return;

        motion.reacquire_in -= deltaTime;
        if (motion.reacquire_in > 0f) return;

        motion.reacquire_in = ReacquireInterval;
        TryAcquireTarget(owner, ref motion, position.v2, worldTime);
    }

    private void UpdateReturning(
        Actor owner,
        Entity artifact,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        float deltaTime)
    {
        float actorScale = Mathf.Max(owner.stats[S.scale], 0.1f) * 10f;
        Vector2 destination = owner.cur_transform_position + Vector3.up * actorScale * 0.55f;
        Vector2 start = position.v2;
        Vector2 desiredDirection = ArtifactSpatialMotionTools.DirectionTo(start, destination, motion.direction);
        ArtifactSpatialMotionTools.Advance(
            ref position,
            ref rotation,
            ref motion.direction,
            desiredDirection,
            motion.speed,
            motion.turn_rate * 1.5f,
            deltaTime);
        if (!ArtifactSpatialMotionTools.SegmentIntersectsCircle(start, position.v2, destination, 0.18f)) return;

        position.v2 = destination;
        FinishMotion(artifact);
    }

    private float AdvanceAndResolveHits(
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        Vector2 desiredDirection,
        float turnRate,
        float deltaTime,
        float worldTime)
    {
        Vector2 start = position.v2;
        float distance = ArtifactSpatialMotionTools.Advance(
            ref position,
            ref rotation,
            ref motion.direction,
            desiredDirection,
            motion.speed,
            turnRate,
            deltaTime);
        _targeting.CollectSweptHits(owner, start, position.v2, ref motion, _frameHits);
        if (_frameHits.Count == 0) return distance;

        float damage = Mathf.Max(1f, owner.stats[S.damage] * motion.damage_multiplier);
        float requiredPierceDistance = motion.pierce_distance;
        for (int i = 0; i < _frameHits.Count; i++)
        {
            BaseSimObject target = _frameHits[i];
            motion.last_target_key = ArtifactSpatialTargeting.GetTargetKey(target);
            motion.has_last_target = true;
            requiredPierceDistance = Mathf.Max(requiredPierceDistance, motion.pierce_distance + target.stats[S.size]);
            _pendingHits.Add(new PendingHit(owner, target, damage));
        }

        motion.repeat_ready_at = worldTime + motion.repeat_cooldown;
        motion.pierce_remaining = Mathf.Max(motion.pierce_remaining, requiredPierceDistance);
        motion.phase = ArtifactSpatialAttackPhase.Piercing;
        return distance;
    }

    private bool TryAcquireTarget(
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        Vector2 artifactPosition,
        float worldTime)
    {
        if (!_targeting.TrySelect(owner, artifactPosition, ref motion, worldTime, out BaseSimObject target))
        {
            return false;
        }

        motion.target_id = target.getID();
        motion.target_is_actor = target.isActor();
        motion.phase = ArtifactSpatialAttackPhase.Pursuing;
        return true;
    }

    private static bool IsOperating(Actor owner, Entity artifact)
    {
        var relations = owner.GetExtend().E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            EquippedArtifactRelation relation = relations[i];
            if (relation.artifact != artifact) continue;
            return relation.state is ArtifactControlState.Operating or ArtifactControlState.Overloaded;
        }
        return false;
    }

    private void FinishMotion(Entity artifact)
    {
        CommandBuffer.RemoveComponent<ArtifactSpatialAttackMotion>(artifact.Id);
        CommandBuffer.RemoveComponent<ArtifactIndependentMotion>(artifact.Id);
    }

    private void ResolvePendingHits()
    {
        for (int i = 0; i < _pendingHits.Count; i++)
        {
            PendingHit hit = _pendingHits[i];
            if (hit.Owner.isRekt() || hit.Target.isRekt()) continue;

            if (hit.Target.isActor())
            {
                EventSystemHub.Publish(new GetHitEvent
                {
                    TargetID = hit.Target.getID(),
                    Damage = hit.Damage,
                    Element = ElementComposition.Static.Iron,
                    Attacker = hit.Owner,
                    AttackerPowerLevel = hit.Owner.GetExtend().GetPowerLevel(),
                });
            }
            else
            {
                hit.Target.b.getHit(hit.Damage, pAttackType: AttackType.Weapon, pAttacker: hit.Owner,
                    pMetallicWeapon: true);
            }
        }
    }

    private readonly struct PendingHit
    {
        public readonly Actor Owner;
        public readonly BaseSimObject Target;
        public readonly float Damage;

        public PendingHit(Actor owner, BaseSimObject target, float damage)
        {
            Owner = owner;
            Target = target;
            Damage = damage;
        }
    }
}

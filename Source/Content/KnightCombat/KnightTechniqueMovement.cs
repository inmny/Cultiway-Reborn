using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

internal enum KnightMovementKind
{
    Charge,
    Skyfall,
}

internal struct KnightMovementState : IComponent
{
    public KnightMovementKind Kind;
    public KnightTechniqueAsset Technique;
    public Item Weapon;
    public Vector2 Start;
    public Vector2 End;
    public float Elapsed;
    public float Duration;
    public float ApexHeight;
    public BaseSimObject OriginalTarget;
    public Kingdom AttackKingdom;
}

/// <summary>启动并结算破阵冲锋与苍穹坠击。</summary>
internal static class KnightTechniqueMovement
{
    public static bool StartCharge(KnightTechniqueContext context)
    {
        Vector2 start = context.Caster.Base.current_position;
        Vector2 end = context.ActiveTarget.Position;
        if (end == Vector2.zero && context.Target != null) end = context.Target.current_position;
        if (!KnightTechniqueRuntimeService.IsStraightPathClear(start, end)) return false;
        return Start(context, KnightMovementKind.Charge, start, end, 0.65f, 0f);
    }

    public static bool StartSkyfall(KnightTechniqueContext context)
    {
        Vector2 start = context.Caster.Base.current_position;
        Vector2 end = context.ActiveTarget.Position;
        if (end == Vector2.zero && context.Target != null) end = context.Target.current_position;
        if (!KnightTechniqueRuntimeService.IsStraightPathClear(start, end)) return false;
        return Start(context, KnightMovementKind.Skyfall, start, end, 0.48f, 2.8f);
    }

    public static void CompleteCharge(ActorExtend caster, in KnightMovementState state, BaseSimObject hitTarget)
    {
        if (hitTarget == null || hitTarget.isRekt() ||
            !KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(caster, state.Technique, state.Weapon)) return;
        if (!TryCreateContext(caster, state, hitTarget, out KnightTechniqueContext context)) return;
        Vector2 impactCenter = hitTarget.current_position;
        KnightWeaponStrikeResolver.TryStrike(
            caster,
            state.Weapon,
            hitTarget,
            1.1f,
            state.AttackKingdom,
            onPositiveActorDamage: (primary, _) =>
            {
                CombatForceEffects.ApplyRadialForce(caster.Base, primary, caster.Base.current_position, 1.8f, false);
                CombatStatusEffects.ApplyStatus(primary, StatusEffects.Daze, 0.4f, caster.Base);
                CombatTargeting.ForEachHostile(caster.Base, impactCenter, 0.9f, sideTarget =>
                {
                    if (sideTarget == primary) return;
                    CombatForceEffects.ApplyRadialForce(caster.Base, sideTarget, impactCenter, 0.8f, false);
                });
            });
    }

    public static void CompleteSkyfall(ActorExtend caster, in KnightMovementState state)
    {
        if (!KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(caster, state.Technique, state.Weapon)) return;
        Item weapon = state.Weapon;
        Kingdom attackKingdom = state.AttackKingdom;
        Vector2 impactCenter = state.End;
        if (TryCreateContext(caster, state, state.OriginalTarget, out KnightTechniqueContext context))
        {
            KnightTechniqueVisuals.SpawnWeaponMotion(
                context,
                EquippedWeaponMotionKind.Crush,
                KnightTechniqueRuntimeService.ResolveDirection(caster.Base, state.OriginalTarget),
                0.22f,
                1.9f,
                -112f,
                24f,
                KnightTrailStyle.Lancer,
                1.42f);
        }

        BaseSimObject mainTarget = state.OriginalTarget;
        bool mainEligible = mainTarget != null && !mainTarget.isRekt() &&
                            (!mainTarget.isActor() || !mainTarget.a.isFlying()) &&
                            Vector2.Distance(mainTarget.current_position, impactCenter) <=
                            1.2f + mainTarget.stats[S.size];
        if (mainEligible)
            KnightWeaponStrikeResolver.TryStrike(
                caster,
                weapon,
                mainTarget,
                1.6f,
                attackKingdom);

        CombatTargeting.ForEachHostile(caster.Base, impactCenter, 2f, target =>
        {
            if (target.isFlying() || mainEligible && target == mainTarget) return;
            KnightWeaponStrikeResolver.TryStrike(
                caster,
                weapon,
                target,
                0.45f,
                attackKingdom,
                onPositiveActorDamage: (damaged, _) =>
                    CombatForceEffects.ApplyRadialForce(caster.Base, damaged, impactCenter, 1f, false));
        });
    }

    private static bool Start(
        KnightTechniqueContext context,
        KnightMovementKind kind,
        Vector2 start,
        Vector2 end,
        float duration,
        float apexHeight)
    {
        if (context.Caster.E.HasComponent<KnightMovementState>()) return false;
        Actor actor = context.Caster.Base;
        PathFinder.Instance.Cancel(actor);
        actor.stopMovement();
        context.Caster.E.AddComponent(new KnightMovementState
        {
            Kind = kind,
            Technique = context.Technique,
            Weapon = context.Weapon,
            Start = start,
            End = end,
            Duration = duration,
            ApexHeight = apexHeight,
            OriginalTarget = context.Target,
            AttackKingdom = context.ActiveTarget.AttackKingdom ?? actor.kingdom,
        });
        Vector3 direction = new Vector3(end.x - start.x, end.y - start.y).normalized;
        KnightTechniqueVisuals.SpawnWeaponMotion(
            context,
            EquippedWeaponMotionKind.ForwardAnchor,
            direction,
            duration,
            kind == KnightMovementKind.Skyfall ? 1.55f : 1.35f,
            0f,
            0f,
            KnightTrailStyle.Movement,
            kind == KnightMovementKind.Skyfall ? 1.28f : 1.16f);
        return true;
    }

    private static bool TryCreateContext(
        ActorExtend caster,
        in KnightMovementState state,
        BaseSimObject target,
        out KnightTechniqueContext context)
    {
        var activeTarget = new ActiveAbilityTarget(
            target,
            new Vector3(state.End.x, state.End.y),
            attackKingdom: state.AttackKingdom);
        return KnightTechniqueAccessService.TryCreateContext(
            caster,
            state.Technique,
            state.Weapon,
            caster.Base.getWeaponAsset(),
            target,
            activeTarget,
            out context);
    }
}

/// <summary>逐帧推进骑士位移，并在查询外完成结构修改和伤害结算。</summary>
internal sealed class KnightTechniqueMovementSystem : QuerySystem<ActorBinder, KnightMovementState>
{
    private readonly List<MovementCompletion> completions = new();

    public KnightTechniqueMovementSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        completions.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref KnightMovementState state, Entity entity) =>
        {
            Actor actor = binder.Actor;
            if (!actor.TryGetExtend(out ActorExtend extend) ||
                !KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(extend, state.Technique, state.Weapon))
            {
                completions.Add(new MovementCompletion(entity, extend, state, null, true));
                return;
            }

            Vector2 previous = actor.current_position;
            state.Elapsed += Tick.deltaTime;
            float progress = Mathf.Clamp01(state.Elapsed / Mathf.Max(0.05f, state.Duration));
            actor.current_position = Vector2.Lerp(state.Start, state.End, progress);
            actor.position_height = state.Kind == KnightMovementKind.Skyfall
                ? 4f * state.ApexHeight * progress * (1f - progress)
                : 0f;
            actor.dirty_current_tile = true;
            WorldTile tile = World.world.GetTile(
                Mathf.FloorToInt(actor.current_position.x),
                Mathf.FloorToInt(actor.current_position.y));
            if (tile != null) actor.setCurrentTile(tile);

            if (state.Kind == KnightMovementKind.Charge &&
                TryFindFirstHostile(actor, previous, actor.current_position, state.AttackKingdom, out BaseSimObject hit))
            {
                completions.Add(new MovementCompletion(entity, extend, state, hit, false));
            }
            else if (progress >= 1f)
            {
                completions.Add(new MovementCompletion(entity, extend, state, null, false));
            }
        });

        for (var i = 0; i < completions.Count; i++) Complete(completions[i]);
    }

    private static void Complete(in MovementCompletion completion)
    {
        ActorExtend extend = completion.Extend;
        if (extend == null || extend.Base.isRekt())
        {
            if (!completion.Entity.IsNull) completion.Entity.RemoveComponent<KnightMovementState>();
            return;
        }

        Actor actor = extend.Base;
        actor.position_height = 0f;
        WorldTile tile = World.world.GetTile(
            Mathf.FloorToInt(actor.current_position.x),
            Mathf.FloorToInt(actor.current_position.y));
        if (tile != null) actor.setCurrentTilePosition(tile);
        completion.Entity.RemoveComponent<KnightMovementState>();
        if (completion.Cancelled) return;
        if (completion.State.Kind == KnightMovementKind.Charge)
            KnightTechniqueMovement.CompleteCharge(extend, completion.State, completion.HitTarget);
        else
            KnightTechniqueMovement.CompleteSkyfall(extend, completion.State);
    }

    private static bool TryFindFirstHostile(
        Actor source,
        Vector2 start,
        Vector2 end,
        Kingdom attackKingdom,
        out BaseSimObject result)
    {
        result = null;
        BaseSimObject found = null;
        if (source.current_tile == null) return false;
        float bestProgress = float.MaxValue;
        foreach (Actor candidate in Finder.getUnitsFromChunk(source.current_tile, 1))
            Consider(candidate);
        foreach (Building candidate in Finder.getBuildingsFromChunk(source.current_tile, 1, 18))
            Consider(candidate);
        result = found;
        return found != null;

        void Consider(BaseSimObject candidate)
        {
            if (candidate == null || candidate.isRekt() || candidate == source ||
                candidate.isActor() && candidate.a.isFlying() ||
                !SkillTargetRelationResolver.IsHostile(source, candidate, attackKingdom)) return;
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            float progress = lengthSquared <= 0.0001f
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(candidate.current_position - start, segment) / lengthSquared);
            Vector2 closest = start + segment * progress;
            float radius = 0.58f + candidate.stats[S.size];
            if ((candidate.current_position - closest).sqrMagnitude > radius * radius || progress >= bestProgress)
                return;
            bestProgress = progress;
            found = candidate;
        }
    }

    private readonly struct MovementCompletion
    {
        public readonly Entity Entity;
        public readonly ActorExtend Extend;
        public readonly KnightMovementState State;
        public readonly BaseSimObject HitTarget;
        public readonly bool Cancelled;

        public MovementCompletion(
            Entity entity,
            ActorExtend extend,
            KnightMovementState state,
            BaseSimObject hitTarget,
            bool cancelled)
        {
            Entity = entity;
            Extend = extend;
            State = state;
            HitTarget = hitTarget;
            Cancelled = cancelled;
        }
    }
}

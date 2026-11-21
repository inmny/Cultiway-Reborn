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

internal struct KnightMovementState : IComponent
{
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

/// <summary>启动并结算苍穹坠击。</summary>
internal static class KnightTechniqueMovement
{
    public static bool StartSkyfall(KnightTechniqueContext context)
    {
        Vector2 start = context.Caster.Base.current_position;
        Vector2 end = context.ActiveTarget.Position;
        if (end == Vector2.zero && context.Target != null) end = context.Target.current_position;
        if (!KnightTechniqueRuntimeService.IsStraightPathClear(start, end)) return false;
        return Start(context, start, end, 0.48f, 2.8f);
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
            1.55f,
            0f,
            0f,
            KnightTrailStyle.Movement,
            1.28f);
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
                completions.Add(new MovementCompletion(entity, extend, state, true));
                return;
            }

            state.Elapsed += Tick.deltaTime;
            float progress = Mathf.Clamp01(state.Elapsed / Mathf.Max(0.05f, state.Duration));
            actor.current_position = Vector2.Lerp(state.Start, state.End, progress);
            actor.position_height = 4f * state.ApexHeight * progress * (1f - progress);
            actor.dirty_current_tile = true;
            WorldTile tile = World.world.GetTile(
                Mathf.FloorToInt(actor.current_position.x),
                Mathf.FloorToInt(actor.current_position.y));
            if (tile != null) actor.setCurrentTile(tile);

            if (progress >= 1f) completions.Add(new MovementCompletion(entity, extend, state, false));
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
        KnightTechniqueMovement.CompleteSkyfall(extend, completion.State);
    }

    private readonly struct MovementCompletion
    {
        public readonly Entity Entity;
        public readonly ActorExtend Extend;
        public readonly KnightMovementState State;
        public readonly bool Cancelled;

        public MovementCompletion(
            Entity entity,
            ActorExtend extend,
            KnightMovementState state,
            bool cancelled)
        {
            Entity = entity;
            Extend = extend;
            State = state;
            Cancelled = cancelled;
        }
    }
}

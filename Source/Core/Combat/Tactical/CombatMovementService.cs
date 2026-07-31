using System;
using ai;
using Cultiway.Core.Pathfinding;
using Cultiway.Core.SkillLibV3.Impacts;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 将高频战术规划转换为低频、可持续的移动订单，避免细小评分变化反复中断异步寻路。
/// </summary>
internal static class CombatMovementService
{
    private const float AdvanceGoalDrift = 2f;
    private const float RangedGoalDrift = 4f;
    private const float RegroupGoalDrift = 3f;
    private const float RetreatGoalDrift = 2.5f;

    /// <summary>
    /// 返回针对当前目标固定下来的交战方向。只有目标变化或移动状态被明确重置时才重新计算。
    /// </summary>
    internal static Vector2 ResolveEngagementBearing(
        Actor actor,
        CombatActorRuntime runtime,
        CombatantSnapshot target)
    {
        CombatMovementOrder movement = runtime.Movement;
        if (movement.HasEngagementBearing && movement.EngagementTargetId == target.Id)
            return movement.EngagementBearing;

        Vector2 direction = actor.current_position - target.Position;
        if (direction.sqrMagnitude < 0.01f)
            direction = ResolveFallbackDirection(actor.getID(), target.Id);
        else
            direction.Normalize();
        direction = Toolbox.rotateVector(direction, ResolveTacticalSlotAngle(actor.getID()));
        direction.Normalize();

        movement.EngagementTargetId = target.Id;
        movement.EngagementBearing = direction;
        movement.HasEngagementBearing = true;
        movement.ForceRefresh = true;
        return direction;
    }

    /// <summary>提交规划产生的期望站位；只有旧订单失效时才真正调用 goTo。</summary>
    internal static void Apply(
        Actor actor,
        CombatActorRuntime runtime,
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        double now)
    {
        CombatMovementOrder movement = runtime.Movement;
        if (!plan.HasPosition || plan.Position.Tile == null)
        {
            RequestSmoothStop(actor, runtime);
            return;
        }

        WorldTile desired = plan.Position.Tile;
        CombatMovementKind kind = ResolveMovementKind(plan);
        long targetId = plan.PrimaryEnemy.Id;
        movement.PendingStop = false;

        if (desired == actor.current_tile)
        {
            RequestSmoothStop(actor, runtime);
            return;
        }

        bool moving = actor.is_moving || actor.isUsingPath();
        bool force = movement.ForceRefresh ||
                     movement.GoalTile == null ||
                     movement.TargetId != targetId ||
                     movement.Kind != kind;
        if (!force && movement.GoalTile == desired && moving)
        {
            CombatDiagnostics.RecordMovementRetained(actor.getID(), desired, movement.GoalTile);
            return;
        }
        if (!force && ShouldRetainOrder(actor, movement, snapshot, plan, desired, kind, now))
        {
            CombatDiagnostics.RecordMovementRetained(actor.getID(), desired, movement.GoalTile);
            return;
        }

        ExecuteEvent result = actor.goTo(
            desired,
            pPathOnWater: actor.isWaterCreature());
        if (result != ExecuteEvent.True)
        {
            CombatWorldService.ReportPathFailure(actor);
            return;
        }

        movement.TargetId = targetId;
        movement.Kind = kind;
        movement.GoalTile = desired;
        movement.GoalIssuedAt = now;
        movement.RetargetAfter = now + ResolveMinimumLifetime(actor, plan.Role, kind, targetId);
        movement.ForceRefresh = false;
        CombatDiagnostics.RecordMovementIssued(actor.getID(), desired, force);
    }

    /// <summary>
    /// 在 b2 阶段完成延迟停步，并返回当前动作恢复是否要求阻止下一路径步。
    /// </summary>
    internal static bool Tick(Actor actor, CombatActorRuntime runtime, double now)
    {
        if (runtime.Movement.PendingStop && !actor.is_moving)
            CompleteStop(actor, runtime);
        if (runtime.MovementPausedUntil <= now)
        {
            runtime.MovementPausedUntil = 0d;
            return false;
        }
        return true;
    }

    /// <summary>判断当前动作是否仍要求暂停平滑位移。</summary>
    internal static bool ShouldPause(CombatActorRuntime runtime, double now)
    {
        return runtime.MovementPausedUntil > now;
    }

    /// <summary>让需要贴身表现的动作短暂停步，但不丢弃已经计算好的路径。</summary>
    internal static void PauseBriefly(
        Actor actor,
        CombatActorRuntime runtime,
        double now)
    {
        float duration = Mathf.Clamp(actor.attack_timer * 0.2f, 0.05f, 0.12f);
        runtime.MovementPausedUntil = Math.Max(runtime.MovementPausedUntil, now + duration);
    }

    /// <summary>标记现有移动订单不可继续复用，但让角色在新计划提交前沿旧路径移动。</summary>
    internal static void Invalidate(CombatActorRuntime runtime, bool clearBearing)
    {
        CombatMovementOrder movement = runtime.Movement;
        movement.ForceRefresh = true;
        movement.PendingStop = false;
        if (!clearBearing) return;
        movement.EngagementTargetId = 0;
        movement.EngagementBearing = default;
        movement.HasEngagementBearing = false;
    }

    /// <summary>释放战术移动状态，并按调用方要求终止仍属于该订单的路径。</summary>
    internal static void Clear(
        Actor actor,
        CombatActorRuntime runtime,
        bool stopMovement,
        bool clearBearing)
    {
        if (stopMovement && IsOwnedPath(actor, runtime.Movement)) StopPath(actor);
        runtime.Movement.Clear(clearBearing);
        runtime.MovementPausedUntil = 0d;
    }

    /// <summary>记录需要持续到公共攻击恢复结束的定身动作。</summary>
    internal static void LockUntilRecovery(
        Actor actor,
        CombatActorRuntime runtime,
        double now)
    {
        runtime.MovementPausedUntil = Math.Max(
            runtime.MovementPausedUntil,
            now + Mathf.Max(0.05f, actor.attack_timer));
    }

    /// <summary>
    /// 在平滑移动抵达当前格中心时完成待处理停步，避免同一帧继续消费下一段路径。
    /// </summary>
    internal static bool TryCompletePendingStopAtBoundary(
        Actor actor,
        CombatActorRuntime runtime)
    {
        if (!runtime.Movement.PendingStop) return false;
        if (!IsOwnedPath(actor, runtime.Movement))
        {
            runtime.Movement.Clear(clearBearing: false);
            return false;
        }
        CompleteStop(actor, runtime);
        return true;
    }

    private static bool ShouldRetainOrder(
        Actor actor,
        CombatMovementOrder movement,
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        WorldTile desired,
        CombatMovementKind kind,
        double now)
    {
        if ((actor.is_moving || actor.isUsingPath()) && now < movement.RetargetAfter)
            return true;
        if (IsCommittedGoalStillUseful(actor, movement, snapshot, plan, kind))
            return true;

        float drift = Vector2.Distance(movement.GoalTile.posV, desired.posV);
        return drift <= ResolveGoalDrift(plan.Role, kind);
    }

    private static bool IsCommittedGoalStillUseful(
        Actor actor,
        CombatMovementOrder movement,
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        CombatMovementKind kind)
    {
        if (movement.GoalTile == null) return false;
        Vector2 goal = movement.GoalTile.posV;
        if (kind == CombatMovementKind.Retreat)
        {
            float currentDistance = Vector2.Distance(actor.current_position, plan.PrimaryEnemy.Position);
            float goalDistance = Vector2.Distance(goal, plan.PrimaryEnemy.Position);
            return goalDistance >= currentDistance + 1.5f;
        }
        if (kind is CombatMovementKind.Regroup or CombatMovementKind.Reposition ||
            !plan.PositioningProfile.HasValue)
            return false;

        CombatActionProfile profile = plan.PositioningProfile.Value;
        float distance = Vector2.Distance(goal, plan.PrimaryEnemy.Position);
        float minRange = Mathf.Max(0f, profile.MinRange - 1f);
        float maxRange = profile.MaxRange + plan.PrimaryEnemy.Size + 1.5f;
        if (distance < minRange || distance > maxRange) return false;
        return !RequiresClearShot(profile) ||
               !CombatPlanner.IsShotBlocked(goal, plan.PrimaryEnemy.Position, snapshot.Obstacles);
    }

    private static void RequestSmoothStop(Actor actor, CombatActorRuntime runtime)
    {
        CombatMovementOrder movement = runtime.Movement;
        if (!IsOwnedPath(actor, movement))
        {
            movement.Clear(clearBearing: false);
            return;
        }
        movement.PendingStop = true;
        if (!actor.is_moving) CompleteStop(actor, runtime);
    }

    private static void CompleteStop(Actor actor, CombatActorRuntime runtime)
    {
        if (IsOwnedPath(actor, runtime.Movement)) StopPath(actor);
        runtime.Movement.Clear(clearBearing: false);
        CombatDiagnostics.RecordMovementStopped(actor.getID(), actor.current_tile);
    }

    private static void StopPath(Actor actor)
    {
        PathFinder.Instance.Cancel(actor);
        actor.stopMovement();
    }

    private static bool IsOwnedPath(Actor actor, CombatMovementOrder movement)
    {
        return movement.GoalTile != null && actor.tile_target == movement.GoalTile;
    }

    private static CombatMovementKind ResolveMovementKind(CombatPlan plan)
    {
        return plan.Intent switch
        {
            CombatIntent.Disengage => CombatMovementKind.Retreat,
            CombatIntent.Regroup => CombatMovementKind.Regroup,
            CombatIntent.Reposition => CombatMovementKind.Reposition,
            CombatIntent.Assist => CombatMovementKind.Assist,
            CombatIntent.Protect => CombatMovementKind.Protect,
            _ => CombatMovementKind.Advance
        };
    }

    private static float ResolveGoalDrift(CombatRole role, CombatMovementKind kind)
    {
        if (kind == CombatMovementKind.Retreat) return RetreatGoalDrift;
        if (kind is CombatMovementKind.Regroup
            or CombatMovementKind.Assist
            or CombatMovementKind.Protect)
            return RegroupGoalDrift;
        return role is CombatRole.Ranged
            or CombatRole.Skirmisher
            or CombatRole.Controller
            or CombatRole.Support
            ? RangedGoalDrift
            : AdvanceGoalDrift;
    }

    private static double ResolveMinimumLifetime(
        Actor actor,
        CombatRole role,
        CombatMovementKind kind,
        long targetId)
    {
        float min;
        float spread;
        if (kind is CombatMovementKind.Retreat
            or CombatMovementKind.Regroup
            or CombatMovementKind.Assist
            or CombatMovementKind.Protect)
        {
            min = 0.55f;
            spread = 0.25f;
        }
        else if (kind == CombatMovementKind.Reposition ||
                 role is CombatRole.Ranged
                     or CombatRole.Skirmisher
                     or CombatRole.Controller
                     or CombatRole.Support)
        {
            min = 1f;
            spread = 0.4f;
        }
        else
        {
            min = 0.7f;
            spread = 0.3f;
        }
        return min + StableRoll(actor.getID(), targetId) * spread;
    }

    private static Vector2 ResolveFallbackDirection(long actorId, long targetId)
    {
        float angle = StableRoll(actorId, targetId) * 360f;
        return Toolbox.rotateVector(Vector2.right, angle);
    }

    private static float ResolveTacticalSlotAngle(long actorId)
    {
        ulong hash = ResolveHash(actorId);
        return ((int)(hash % 9UL) - 4) * 12f;
    }

    private static float StableRoll(long actorId, long salt)
    {
        ulong hash = ResolveHash(actorId ^ salt * 31L);
        return (hash & 0xFFFFFFUL) / (float)0x1000000UL;
    }

    private static ulong ResolveHash(long value)
    {
        ulong hash = unchecked((ulong)value);
        hash ^= hash >> 30;
        hash *= 0xBF58476D1CE4E5B9UL;
        hash ^= hash >> 27;
        hash *= 0x94D049BB133111EBUL;
        hash ^= hash >> 31;
        return hash;
    }

    private static bool RequiresClearShot(CombatActionProfile profile)
    {
        return profile.ImpactKind is SkillImpactKind.Projectile
            or SkillImpactKind.Piercing
            or SkillImpactKind.Wave
            or SkillImpactKind.PulseBeam
            or SkillImpactKind.ChannelBeam;
    }
}

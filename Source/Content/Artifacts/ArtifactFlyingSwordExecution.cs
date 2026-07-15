using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 飞剑技能执行的持续轨迹。会话计算位姿，Core 随后将位姿同步到被借用的法器 Body。
/// </summary>
internal static class ArtifactFlyingSwordExecution
{
    /// <summary>执行层统一调校倍率，使已经生成的法器无需重组能力参数也能获得新的运动手感。</summary>
    internal const float BaseSpeedMultiplier = 1.4f;
    internal const float TurnRateMultiplier = 2.5f;
    internal const float LaunchSpeedRatio = 0.7f;

    private const float ReacquireInterval = 0.12f;
    private const float PursuitMinSpeedRatio = LaunchSpeedRatio;
    private const float PursuitMaxSpeedRatio = 2.5f;
    private const float CruiseSpeedRatio = 1.05f;
    private const float ReturnSpeedRatio = 1.5f;
    private const float AfterimageSpeedPerLayer = 11f;
    private static readonly ArtifactSpatialTargeting Targeting = new();

    public static void Update(
        ref SkillContext context,
        ref Position position,
        ref Rotation rotation,
        Entity execution,
        float deltaTime)
    {
        if (ModClass.I.Game.IsPaused()) return;
        if (!TryResolveOwnerAndBody(ref context, execution, out Actor owner, out Entity body))
        {
            SkillExecutionLifecycle.RequestEnd(execution);
            return;
        }

        ref ArtifactSpatialAttackMotion motion = ref execution.GetComponent<ArtifactSpatialAttackMotion>();

        ref ArtifactManifestation manifestation = ref body.GetComponent<ArtifactManifestation>();
        manifestation.visible = owner.is_visible;

        if (motion.phase != ArtifactSpatialAttackPhase.Returning && !IsOperating(owner, body))
        {
            BeginReturning(ref context, ref motion, execution);
        }

        float worldTime = (float)World.world.getCurWorldTime();
        switch (motion.phase)
        {
            case ArtifactSpatialAttackPhase.Pursuing:
                UpdatePursuing(ref context, owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                break;
            case ArtifactSpatialAttackPhase.Piercing:
                UpdatePiercing(ref context, owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                break;
            case ArtifactSpatialAttackPhase.Cruising:
                UpdateCruising(ref context, owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                break;
            case ArtifactSpatialAttackPhase.Returning:
                UpdateReturning(owner, execution, ref motion, ref position, ref rotation, deltaTime);
                break;
        }

        UpdateAfterimage(execution, motion.current_speed);
    }

    private static void UpdatePursuing(
        ref SkillContext context,
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        float deltaTime,
        float worldTime)
    {
        BaseSimObject target = context.TargetObj;
        if (!ArtifactSpatialTargeting.IsValidTarget(owner, target, motion.control_range, context.AttackKingdom))
        {
            if (!TryAcquireTarget(ref context, owner, ref motion, position.v2, worldTime))
            {
                motion.phase = ArtifactSpatialAttackPhase.Cruising;
                motion.reacquire_in = ReacquireInterval;
                UpdateCruising(ref context, owner, ref motion, ref position, ref rotation, deltaTime, worldTime);
                return;
            }
            target = context.TargetObj;
        }

        Vector2 desiredDirection = ArtifactSpatialMotionTools.DirectionTo(
            position.v2,
            target.current_position,
            motion.direction);
        context.TargetPos = target.GetSimPos();
        context.TargetDir = desiredDirection;
        float speed = ResolvePursuitSpeed(ref motion, desiredDirection, deltaTime);
        ArtifactSpatialMotionTools.Advance(
            ref position,
            ref rotation,
            ref motion.direction,
            desiredDirection,
            speed,
            motion.turn_rate,
            deltaTime);
    }

    private static void UpdatePiercing(
        ref SkillContext context,
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        float deltaTime,
        float worldTime)
    {
        float speed = ApproachSpeed(ref motion, PursuitMaxSpeedRatio, 12f, deltaTime);
        float distance = ArtifactSpatialMotionTools.Advance(
            ref position,
            ref rotation,
            ref motion.direction,
            motion.direction,
            speed,
            0f,
            deltaTime);
        context.TargetDir = motion.direction;
        motion.pierce_remaining -= distance;
        if (motion.pierce_remaining > 0f) return;

        if (!TryAcquireTarget(ref context, owner, ref motion, position.v2, worldTime))
        {
            motion.phase = ArtifactSpatialAttackPhase.Cruising;
            motion.reacquire_in = ReacquireInterval;
        }
    }

    private static void UpdateCruising(
        ref SkillContext context,
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
        float speed = ApproachSpeed(ref motion, CruiseSpeedRatio, 8f, deltaTime);
        ArtifactSpatialMotionTools.Advance(
            ref position,
            ref rotation,
            ref motion.direction,
            desiredDirection,
            speed,
            motion.turn_rate,
            deltaTime);
        context.TargetDir = motion.direction;

        motion.reacquire_in -= deltaTime;
        if (motion.reacquire_in > 0f) return;

        motion.reacquire_in = ReacquireInterval;
        TryAcquireTarget(ref context, owner, ref motion, position.v2, worldTime);
    }

    private static void UpdateReturning(
        Actor owner,
        Entity execution,
        ref ArtifactSpatialAttackMotion motion,
        ref Position position,
        ref Rotation rotation,
        float deltaTime)
    {
        float actorScale = Mathf.Max(owner.stats[S.scale], 0.1f) * 10f;
        Vector2 destination = owner.cur_transform_position + Vector3.up * actorScale * 0.55f;
        Vector2 start = position.v2;
        Vector2 desiredDirection = ArtifactSpatialMotionTools.DirectionTo(start, destination, motion.direction);
        float speed = ApproachSpeed(ref motion, ReturnSpeedRatio, 8f, deltaTime);
        ArtifactSpatialMotionTools.Advance(
            ref position,
            ref rotation,
            ref motion.direction,
            desiredDirection,
            speed,
            motion.turn_rate * 1.5f,
            deltaTime);
        if (!ArtifactSpatialMotionTools.SegmentIntersectsCircle(start, position.v2, destination, 0.18f)) return;

        position.v2 = destination;
        SkillExecutionLifecycle.RequestEnd(execution);
    }

    private static float ResolvePursuitSpeed(
        ref ArtifactSpatialAttackMotion motion,
        Vector2 desiredDirection,
        float deltaTime)
    {
        float alignment = Vector2.Dot(motion.direction, desiredDirection);
        float aligned = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.98f, alignment));
        float targetRatio = Mathf.Lerp(PursuitMinSpeedRatio, PursuitMaxSpeedRatio, aligned);
        float response = Mathf.Lerp(12f, 9f, aligned);
        return ApproachSpeed(ref motion, targetRatio, response, deltaTime);
    }

    private static float ApproachSpeed(
        ref ArtifactSpatialAttackMotion motion,
        float targetRatio,
        float response,
        float deltaTime)
    {
        motion.current_speed = Mathf.MoveTowards(
            motion.current_speed,
            motion.speed * targetRatio,
            motion.speed * response * deltaTime);
        return motion.current_speed;
    }

    private static void UpdateAfterimage(Entity execution, float speed)
    {
        ref AnimAfterimage afterimage = ref execution.GetComponent<AnimAfterimage>();
        afterimage.Count = Mathf.Clamp(Mathf.CeilToInt(speed / AfterimageSpeedPerLayer), 2, 7);
    }

    private static bool TryAcquireTarget(
        ref SkillContext context,
        Actor owner,
        ref ArtifactSpatialAttackMotion motion,
        Vector2 executionPosition,
        float worldTime)
    {
        if (!Targeting.TrySelect(
                owner,
                executionPosition,
                ref motion,
                worldTime,
                context.AttackKingdom,
                out BaseSimObject target))
        {
            context.TargetObj = null;
            return false;
        }

        context.TargetObj = target;
        context.TargetPos = target.GetSimPos();
        motion.phase = ArtifactSpatialAttackPhase.Pursuing;
        return true;
    }

    private static void BeginReturning(
        ref SkillContext context,
        ref ArtifactSpatialAttackMotion motion,
        Entity execution)
    {
        context.TargetObj = null;
        motion.phase = ArtifactSpatialAttackPhase.Returning;
        ref ColliderConfig collider = ref execution.GetComponent<ColliderConfig>();
        collider.Enabled = false;
    }

    private static bool TryResolveOwnerAndBody(
        ref SkillContext context,
        Entity execution,
        out Actor owner,
        out Entity body)
    {
        owner = context.SourceObj?.a;
        if (owner == null || owner.isRekt() ||
            !SkillExecutionLifecycle.TryGetPrimaryBody(execution, out body) ||
            body.IsNull ||
            !body.HasComponent<ArtifactManifestation>())
        {
            body = default;
            return false;
        }
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
}

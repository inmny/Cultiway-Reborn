using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Systems.Logic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

internal static partial class ArtifactSpatialAttackExecution
{
    internal static void InitializeBlade(Entity execution, Entity artifact)
    {
        ref var motion = ref execution.GetComponent<ArtifactSpatialAttackMotion>();
        ArtifactAppearanceRenderer.TryGetSpatialFlightPose(artifact, execution.GetComponent<Position>().value,
            execution.GetComponent<Rotation>(), out var pose);
        float radius = motion.geometry.Radius * pose.Scale.y;
        execution.GetComponent<ColliderSphere>().Radius = radius;
        execution.AddComponent(motion.geometry.Contact(pose));
    }

    private static void UpdateBlade(Entity execution, Entity artifact, in ArtifactSpatialAttackMotion motion,
        Vector3 position, Rotation rotation)
    {
        if (!ArtifactAppearanceRenderer.TryGetSpatialFlightPose(artifact, position, rotation, out var pose)) return;
        float radius = motion.geometry.Radius * pose.Scale.y;
        execution.GetComponent<ColliderSphere>().Radius = radius;
        execution.GetComponent<ColliderSweptBody>().Move(motion.geometry.Contact(pose));
    }

    private static void ExpandBlade(Entity artifact, Actor owner, ref ArtifactSpatialAttackMotion motion, float seconds)
    {
        motion.flight_age += seconds;
        float fullSize = ArtifactManifestationTools.ResolveActiveWorldSize(artifact, owner);
        SetSize(artifact, Mathf.SmoothStep(motion.launch_size, fullSize, motion.flight_age / .16f));
    }

    private static void SetSize(Entity artifact, float size)
    {
        ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
        if (Mathf.Approximately(manifestation.world_size, size)) return;
        manifestation.world_size = size;
        var shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        ArtifactManifestationTools.ApplyBodySize(artifact, shape.presentation.body_radius, size);
    }

    private static float TargetHeight(BaseSimObject target) => target.isActor()
        ? target.a.position_height + SpatialActorPose.Height(target.a) * .49f
        : target.getHeight() + .35f;

    private static void AimHeight(ref Position position, ref Rotation rotation, float desired, float speed, float seconds)
    {
        float height = Mathf.MoveTowards(position.z, desired, Mathf.Max(3f, speed * .48f) * seconds);
        float pitch = Mathf.Atan2(height - position.z, Mathf.Max(.001f, speed * seconds)) * Mathf.Rad2Deg;
        position.z = height;
        rotation.x = Mathf.MoveTowards(rotation.x, pitch, 280f * seconds);
    }

    /// <summary>让实际转弯半径小于与目标的距离，近处能够收准剑锋。</summary>
    private static float LimitTurnSpeed(float speed, float turnRate, float distance) =>
        Mathf.Min(speed, Mathf.Max(3f, turnRate * Mathf.Deg2Rad * distance * .5f));

    internal static bool ReturnsOnRecall(Entity execution) => !execution.IsNull &&
        execution.TryGetComponent(out ArtifactSpatialAttackMotion motion) && motion.spatial_model;

    internal static bool TryRecall(Entity execution)
    {
        if (!SkillExecutionLifecycle.TryGetPrimaryBody(execution, out _)) return false;
        ref var motion = ref execution.GetComponent<ArtifactSpatialAttackMotion>();
        if (motion.phase == ArtifactSpatialAttackPhase.Returning) return false;
        BeginReturning(ref execution.GetComponent<SkillContext>(), ref motion, execution);
        return true;
    }

    /// <summary>追上随身体移动的原随身槽，近处先减速并连续对齐姿态，再交还原本体。</summary>
    private static void ReturnToFollower(Actor owner, Entity artifact, Entity execution, ref ArtifactSpatialAttackMotion motion,
        ref Position position, ref Rotation rotation, float seconds)
    {
        if (!ArtifactManifestationSystem.TryResolveFollowerPose(owner, artifact, out var follower))
        {
            SkillExecutionLifecycle.RequestEnd(execution);
            return;
        }

        Vector3 destination = ArtifactAppearanceRenderer.FollowerSpatialPosition(artifact, owner, follower.position,
            out _, out _);
        Vector3 offset = destination - position.value;
        float distance = offset.magnitude;
        float speed = ApproachSpeed(ref motion, ReturnSpeedRatio, 8f, seconds);
        if (distance > 1.8f)
        {
            Vector2 direction = ArtifactSpatialMotionTools.DirectionTo(position.v2, destination, motion.direction);
            speed = LimitTurnSpeed(speed, motion.turn_rate * 1.5f, distance);
            motion.current_speed = speed;
            AimHeight(ref position, ref rotation, destination.z, speed, seconds);
            ArtifactSpatialMotionTools.Advance(ref position, ref rotation, ref motion.direction, direction,
                Mathf.Min(speed, distance / Mathf.Max(.001f, seconds)), motion.turn_rate * 1.5f, seconds);
            return;
        }

        float closing = Mathf.Min(speed, 2.8f + distance * 7f);
        position.value = Vector3.MoveTowards(position.value, destination, closing * seconds);
        rotation.z = Mathf.MoveTowardsAngle(rotation.z, follower.rotation, motion.turn_rate * 1.8f * seconds);
        rotation.x = Mathf.MoveTowards(rotation.x, 0f, 360f * seconds);
        SetSize(artifact, Mathf.SmoothStep(follower.world_size,
            ArtifactManifestationTools.ResolveActiveWorldSize(artifact, owner), distance / 1.8f));
        if ((position.value - destination).sqrMagnitude > .0025f ||
            Mathf.Abs(Mathf.DeltaAngle(rotation.z, follower.rotation)) > 3f || Mathf.Abs(rotation.x) > 3f) return;

        position.value = destination;
        rotation.value = new Vector3(0f, 0f, follower.rotation);
        var shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        artifact.GetComponent<ArtifactManifestation>().active_visual = false;
        ArtifactManifestationSystem.ApplyPose(artifact, follower, shape.presentation.body_radius);
        artifact.GetComponent<Rotation>().value = rotation.value;
        SkillExecutionLifecycle.RequestEnd(execution);
    }
}

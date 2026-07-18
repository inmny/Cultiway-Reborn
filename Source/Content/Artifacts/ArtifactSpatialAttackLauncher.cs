using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 一次借用法宝真实世界本体发动空间攻击所需的运行参数。
/// </summary>
internal struct ArtifactSpatialAttackLaunchRequest
{
    public BaseSimObject target;
    public Kingdom attack_kingdom;
    public ArtifactSpatialAttackMode mode;
    public float strength;
    public float speed;
    public float turn_rate;
    public float control_range;
    public float pierce_distance;
    public float repeat_cooldown;
    public float impact_force;
    public Color trail_tint;
}

/// <summary>
/// 为所有借用法宝本体飞行的能力统一建立显化、碰撞、残影和 Body 租约。
/// </summary>
internal static class ArtifactSpatialAttackLauncher
{
    public static bool TryLaunch(
        ArtifactAbilityExecutionContext context,
        in ArtifactSpatialAttackLaunchRequest request,
        out Entity execution)
    {
        Actor controller = context.controller.GetComponent<ActorBinder>().Actor;
        Entity artifact = context.artifact;
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        bool initialized = ArtifactManifestationTools.EnsureWorldComponents(
            artifact,
            shape.presentation.body_radius);
        float actorScale = Mathf.Max(controller.stats[S.scale], 0.1f) * 10f;

        ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
        manifestation.control_state = context.control_state;
        manifestation.visible = controller.is_visible;
        manifestation.flip_x = false;
        ArtifactManifestationTools.ApplyActiveWorldSize(artifact, controller);
        if (initialized)
        {
            artifact.GetComponent<Position>().value =
                controller.cur_transform_position + Vector3.up * actorScale * 0.55f;
        }

        Vector2 direction = request.target.current_position - artifact.GetComponent<Position>().v2;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;

        execution = ArtifactSkillExecutions.FlyingSword.NewEntity();
        ref SkillContext skillContext = ref execution.GetComponent<SkillContext>();
        skillContext.SourceObj = controller;
        skillContext.TargetObj = request.target;
        skillContext.TargetPos = request.target.GetSimPos();
        skillContext.TargetDir = direction.normalized;
        skillContext.AttackKingdom = request.attack_kingdom;
        skillContext.Strength = request.strength;
        skillContext.PowerLevel = controller.GetExtend().GetPowerLevel();

        execution.GetComponent<Position>().value = artifact.GetComponent<Position>().value;
        execution.GetComponent<Rotation>().value = artifact.GetComponent<Rotation>().value;
        execution.GetComponent<PrevPosition>().Value = execution.GetComponent<Position>().v2;
        ref SkillGroundFxState groundFxState = ref execution.GetComponent<SkillGroundFxState>();
        groundFxState.LastX = execution.GetComponent<Position>().x;
        groundFxState.LastY = execution.GetComponent<Position>().y;
        execution.GetComponent<AnimAfterimage>().Tint = request.trail_tint;

        ArtifactBody body = artifact.GetComponent<ArtifactBody>();
        float colliderRadius = body.radius;
        execution.GetComponent<ColliderSphere>().Radius = colliderRadius;
        execution.AddComponent(new ColliderLinearExtent
        {
            Forward = Mathf.Max(0f, body.forward_extent - colliderRadius),
            Backward = Mathf.Max(0f, body.backward_extent - colliderRadius),
        });

        float worldTime = (float)World.world.getCurWorldTime();
        float speed = Mathf.Max(0.01f, request.speed);
        float outboundDuration = Mathf.Clamp(
            direction.magnitude / (speed * ArtifactSpatialAttackExecution.LaunchSpeedRatio) + 0.8f,
            0.8f,
            3.5f);
        execution.AddComponent(new ArtifactSpatialAttackMotion
        {
            mode = request.mode,
            direction = direction.normalized,
            speed = speed,
            current_speed = speed * ArtifactSpatialAttackExecution.LaunchSpeedRatio,
            turn_rate = request.turn_rate,
            control_range = request.control_range,
            pierce_distance = request.pierce_distance,
            repeat_cooldown = request.repeat_cooldown,
            return_at = request.mode == ArtifactSpatialAttackMode.StrikeAndReturn
                ? worldTime + outboundDuration
                : 0f,
            impact_force = request.impact_force,
            orbit_sign = (artifact.Id & 1) == 0 ? 1f : -1f,
            hit_target_keys = new HashSet<long>(),
            phase = ArtifactSpatialAttackPhase.Pursuing,
        });

        if (SkillExecutionLifecycle.TryBorrowBody(execution, artifact)) return true;

        SkillExecutionLifecycle.RequestEnd(execution);
        return false;
    }
}

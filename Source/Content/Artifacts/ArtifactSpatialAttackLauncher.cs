using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Systems.Logic;
using Cultiway.Core.Combat;
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
    public DamageOrigin damage_origin;
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
        Actor spatialController = context.ResolveCarrierActor();
        Entity artifact = context.artifact;
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        bool followerLayout = ArtifactAppearanceRenderer.UsesFollowerLayout(artifact);
        execution = ArtifactSkillExecutions.FlyingSword.NewEntity();
        if (!SkillExecutionLifecycle.TryBorrowBody(execution, artifact))
        {
            SkillExecutionLifecycle.RequestEnd(execution);
            execution = default;
            return false;
        }

        bool ready = false;
        try
        {
            bool initialized = ArtifactManifestationTools.EnsureWorldComponents(
                artifact,
                shape.presentation.body_radius);
            float actorScale = Mathf.Max(controller.stats[S.scale], 0.1f) * 10f;

            ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
            manifestation.control_state = context.control_state;
            manifestation.visible = spatialController.is_visible;
            if (initialized)
            {
                if (ArtifactManifestationSystem.TryResolveFollowerPose(controller, artifact, out var follower))
                {
                    ArtifactManifestationSystem.ApplyPose(artifact, follower, shape.presentation.body_radius);
                    followerLayout = true;
                }
                else
                    artifact.GetComponent<Position>().value =
                        spatialController.cur_transform_position + Vector3.up * actorScale * 0.55f;
            }

            bool spatial = ArtifactAppearanceRenderer.TryGetBladeGeometry(artifact, out ArtifactFlightGeometry geometry);
            Vector3 position = artifact.GetComponent<Position>().value;
            float initialSize = manifestation.world_size;
            if (spatial)
            {
                position = followerLayout
                    ? ArtifactAppearanceRenderer.FollowerSpatialPosition(artifact, controller, position, out _, out _)
                    : new Vector3(position.x, position.y - position.z, position.z);
                artifact.GetComponent<Position>().value = position;
                manifestation.active_visual = true;
                if (initialSize <= 0f) initialSize = ArtifactManifestationTools.ResolveActiveWorldSize(artifact, controller);
                manifestation.world_size = initialSize;
            }
            else
            {
                manifestation.flip_x = false;
                ArtifactManifestationTools.ApplyActiveWorldSize(artifact, controller);
            }

            Vector2 direction = request.target.current_position - (Vector2)position;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;

            ref SkillContext skillContext = ref execution.GetComponent<SkillContext>();
            skillContext.BindSource(controller, spatialController);
            skillContext.TargetObj = request.target;
            skillContext.TargetPos = request.target.GetSimPos();
            skillContext.TargetDir = direction.normalized;
            skillContext.AttackKingdom = request.attack_kingdom;
            skillContext.Strength = request.strength;
            skillContext.RuntimeData.EffectScale = context.effect_scale;
            skillContext.RuntimeData.DamageOrigin = request.damage_origin;
            skillContext.PowerLevel = controller.GetExtend().GetPowerLevel();

            execution.GetComponent<Position>().value = position;
            execution.GetComponent<Rotation>().value = artifact.GetComponent<Rotation>().value;
            execution.GetComponent<PrevPosition>() = new PrevPosition { Value = position, Height = position.z };
            ArtifactBody body = artifact.GetComponent<ArtifactBody>();
            if (!spatial)
            {
                execution.GetComponent<AnimAfterimage>().Tint = request.trail_tint;
                execution.GetComponent<ColliderSphere>().Radius = body.radius;
                execution.AddComponent(new ColliderLinearExtent
                {
                    Forward = Mathf.Max(0f, body.forward_extent - body.radius),
                    Backward = Mathf.Max(0f, body.backward_extent - body.radius),
                });
            }

            float worldTime = (float)World.world.getCurWorldTime();
            float speed = Mathf.Max(0.01f, request.speed);
            float outboundDuration = Mathf.Clamp(
                direction.magnitude / (speed * ArtifactSpatialAttackExecution.LaunchSpeedRatio) + 0.8f,
                0.8f,
                3.5f);
            execution.AddComponent(new ArtifactSpatialAttackMotion
            {
                mode = request.mode,
                direction = spatial
                    ? (Vector2)(Quaternion.AngleAxis(execution.GetComponent<Rotation>().z, Vector3.forward) * Vector3.up)
                    : direction.normalized,
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
                geometry = geometry,
                launch_size = initialSize,
            });
            if (spatial)
            {
                execution.RemoveComponent<AnimAfterimage>();
                ArtifactSpatialAttackExecution.InitializeBlade(execution, artifact);
            }
            ready = true;
            return true;
        }
        finally
        {
            if (!ready && !execution.IsNull)
            {
                SkillExecutionLifecycle.ReleaseBodies(execution);
                SkillExecutionLifecycle.RequestEnd(execution);
                execution = default;
            }
        }
    }
}

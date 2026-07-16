using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 通用能力生命周期对法器本体持续部署的管理部分。
/// 所有结构变更延迟到查询循环结束后执行。
/// </summary>
public static partial class ArtifactAbilityLifecycle
{
    private static readonly List<PendingDeployment> PendingDeployments = new();
    private static readonly List<PendingDeploymentRelease> PendingDeploymentReleases = new();

    /// <summary>
    /// 将法器本体部署到指定世界位置。部署不创建常驻技能实体，结束后法器恢复装备跟随。
    /// </summary>
    public static bool Deploy(
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime,
        Vector3 origin,
        ArtifactBodyAnchorKind bodyAnchor = ArtifactBodyAnchorKind.Center,
        float? rotation = null)
    {
        Entity artifact = context.artifact;
        if (artifact.HasComponent<ArtifactIndependentMotion>() ||
            artifact.HasComponent<SkillExecutionBodyLease>()) return false;

        double now = Now;
        PendingDeployments.Add(new PendingDeployment(
            artifact,
            context.controller,
            context.control_state,
            ability.instance_id,
            origin,
            bodyAnchor,
            rotation));
        runtime.activity_kind = ArtifactAbilityActivityKind.Deployment;
        runtime.activity_started_at = now;
        runtime.activity_until = 0d;
        return true;
    }

    internal static void ApplyPendingDeploymentChanges()
    {
        ApplyPendingDeploymentReleases();
        for (int i = 0; i < PendingDeployments.Count; i++)
        {
            PendingDeployment pending = PendingDeployments[i];
            Entity artifact = pending.Artifact;
            if (artifact.IsNull || pending.Controller.IsNull ||
                !pending.Controller.HasComponent<ActorBinder>() ||
                artifact.HasComponent<ArtifactIndependentMotion>() ||
                artifact.HasComponent<SkillExecutionBodyLease>()) continue;

            ArtifactAbilityRuntime runtime = artifact.GetComponent<ArtifactAbilityRuntime>();
            ArtifactAbilityRuntimeEntry entry = default;
            bool active = false;
            for (int j = 0; j < runtime.abilities.Length; j++)
            {
                if (runtime.abilities[j].instance_id != pending.AbilityInstanceId) continue;
                entry = runtime.abilities[j];
                active = entry.activity_kind == ArtifactAbilityActivityKind.Deployment;
                break;
            }
            if (!active) continue;

            ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
            ArtifactManifestationTools.EnsureWorldComponents(artifact, shape.presentation.body_radius);
            artifact.GetComponent<Position>().value = pending.Origin;
            if (pending.Rotation.HasValue) artifact.GetComponent<Rotation>().z = pending.Rotation.Value;

            ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
            manifestation.control_state = pending.ControlState;
            Actor controller = pending.Controller.GetComponent<ActorBinder>().Actor;
            manifestation.visible = controller.is_visible;
            manifestation.flip_x = false;
            ArtifactManifestationTools.ApplyActiveWorldSize(artifact, controller);
            ArtifactManifestationTools.AlignWorldAnchor(artifact, pending.BodyAnchor, pending.Origin);

            artifact.AddComponent(new ArtifactIndependentMotion());
            artifact.AddComponent(new ArtifactDeployment
            {
                controller = pending.Controller,
                ability_instance_id = pending.AbilityInstanceId,
                started_at = entry.activity_started_at,
                expires_at = entry.activity_until,
                origin = pending.Origin,
                body_anchor = pending.BodyAnchor,
            });
        }
        PendingDeployments.Clear();
    }

    internal static void ApplyPendingDeploymentReleases()
    {
        for (int i = 0; i < PendingDeploymentReleases.Count; i++)
        {
            PendingDeploymentRelease pending = PendingDeploymentReleases[i];
            Entity artifact = pending.Artifact;
            if (artifact.IsNull ||
                !artifact.TryGetComponent(out ArtifactDeployment deployment) ||
                deployment.ability_instance_id != pending.AbilityInstanceId) continue;
            artifact.RemoveComponent<ArtifactDeployment>();
            artifact.RemoveComponent<ArtifactIndependentMotion>();
        }
        PendingDeploymentReleases.Clear();
    }

    private readonly struct PendingDeployment
    {
        public readonly Entity Artifact;
        public readonly Entity Controller;
        public readonly ArtifactControlState ControlState;
        public readonly string AbilityInstanceId;
        public readonly Vector3 Origin;
        public readonly ArtifactBodyAnchorKind BodyAnchor;
        public readonly float? Rotation;

        public PendingDeployment(
            Entity artifact,
            Entity controller,
            ArtifactControlState controlState,
            string abilityInstanceId,
            Vector3 origin,
            ArtifactBodyAnchorKind bodyAnchor,
            float? rotation)
        {
            Artifact = artifact;
            Controller = controller;
            ControlState = controlState;
            AbilityInstanceId = abilityInstanceId;
            Origin = origin;
            BodyAnchor = bodyAnchor;
            Rotation = rotation;
        }
    }

    private readonly struct PendingDeploymentRelease
    {
        public readonly Entity Artifact;
        public readonly string AbilityInstanceId;

        public PendingDeploymentRelease(Entity artifact, string abilityInstanceId)
        {
            Artifact = artifact;
            AbilityInstanceId = abilityInstanceId;
        }
    }
}

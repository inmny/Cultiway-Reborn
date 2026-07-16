using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using strings;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 将角色的装备关系同步成法器本体上的世界空间状态，并驱动默认跟随表现。
/// </summary>
public class ArtifactManifestationSystem : QuerySystem<ActorBinder, ArtifactLoadoutState>
{
    private readonly List<EquippedArtifactRelation> _relations = new();
    private readonly Dictionary<ArtifactPresentationAsset, int> _groupCounts = new();
    private readonly Dictionary<ArtifactPresentationAsset, int> _groupIndices = new();
    private readonly List<ManifestationUpdate> _updates = new();
    private Entity _vehicleArtifact;

    protected override void OnUpdate()
    {
        float time = Time.time;
        _updates.Clear();
        Query.ForEachEntity((ref ActorBinder binder, ref ArtifactLoadoutState _, Entity owner) =>
        {
            Actor actor = binder.Actor;
            if (actor == null || !actor.isAlive()) return;

            CollectRelations(owner);
            if (_relations.Count == 0) return;
            ResolveVehicle(owner);

            CountPresentationGroups();
            float actorScale = Mathf.Max(actor.stats[S.scale], 0.1f) * 10f;
            for (int i = 0; i < _relations.Count; i++)
            {
                EquippedArtifactRelation relation = _relations[i];
                Entity artifact = relation.artifact;
                ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
                ArtifactPresentationAsset presentation = shape.presentation;
                int groupIndex = _groupIndices[presentation];
                _groupIndices[presentation] = groupIndex + 1;

                bool followsOwner = !artifact.HasComponent<ArtifactIndependentMotion>() &&
                                    !artifact.HasComponent<SkillExecutionBodyLease>();
                bool vehicle = artifact == _vehicleArtifact;
                bool active = vehicle || !followsOwner || HasActiveAbility(artifact.GetComponent<ArtifactAbilityRuntime>());
                ArtifactPresentationPose pose = followsOwner
                    ? ResolvePose(
                        presentation,
                        new ArtifactPresentationContext(
                            actor,
                            relation.state,
                            groupIndex,
                            _groupCounts[presentation],
                            actorScale,
                            time),
                        vehicle)
                    : default;
                float activeWorldSize = active
                    ? ArtifactManifestationTools.ResolveActiveWorldSize(artifact, actor)
                    : 0f;
                _updates.Add(new ManifestationUpdate(
                    artifact,
                    relation.state,
                    presentation.body_radius,
                    actor.is_visible,
                    followsOwner,
                    active,
                    activeWorldSize,
                    pose));
            }
        });

        ApplyUpdates();
    }

    private static ArtifactPresentationPose ResolvePose(
        ArtifactPresentationAsset presentation,
        ArtifactPresentationContext context,
        bool vehicle)
    {
        return vehicle ? presentation.ResolveVehicle(context) : presentation.Resolve(context);
    }

    private void ApplyUpdates()
    {
        for (int i = 0; i < _updates.Count; i++)
        {
            ManifestationUpdate update = _updates[i];
            Entity artifact = update.artifact;
            ArtifactManifestationTools.EnsureWorldComponents(artifact, update.bodyRadius);

            ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
            manifestation.control_state = update.controlState;
            manifestation.visible = update.visible;
            if (update.followsOwner) ApplyPose(artifact, update.pose, update.bodyRadius);
            else manifestation.flip_x = false;
            if (!update.active) continue;

            manifestation.world_size = update.activeWorldSize;
            ArtifactManifestationTools.ApplyBodySize(artifact, update.bodyRadius, update.activeWorldSize);
            if (artifact.TryGetComponent(out ArtifactDeployment deployment))
            {
                ArtifactManifestationTools.AlignWorldAnchor(
                    artifact,
                    deployment.ResolveBodyAnchor(),
                    deployment.origin);
            }
        }
    }

    private static bool HasActiveAbility(ArtifactAbilityRuntime runtime)
    {
        for (int i = 0; i < runtime.abilities.Length; i++)
        {
            if (runtime.abilities[i].activity_kind != ArtifactAbilityActivityKind.None) return true;
        }
        return false;
    }

    private void CollectRelations(Entity owner)
    {
        _relations.Clear();
        var relations = owner.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].artifact.IsAvailable()) _relations.Add(relations[i]);
        }
        _relations.Sort((left, right) => left.artifact.Id.CompareTo(right.artifact.Id));
    }

    private void ResolveVehicle(Entity owner)
    {
        var relations = owner.GetRelations<ArtifactVehicleRelation>();
        _vehicleArtifact = relations.Length == 0 ? default : relations[0].artifact;
    }

    private void CountPresentationGroups()
    {
        _groupCounts.Clear();
        _groupIndices.Clear();
        for (int i = 0; i < _relations.Count; i++)
        {
            ArtifactShapeAsset shape = (ArtifactShapeAsset)_relations[i].artifact.GetComponent<ItemShape>().Type;
            ArtifactPresentationAsset presentation = shape.presentation;
            _groupCounts.TryGetValue(presentation, out int count);
            _groupCounts[presentation] = count + 1;
            _groupIndices[presentation] = 0;
        }
    }

    private static void ApplyPose(Entity artifact, ArtifactPresentationPose pose, float bodyRadius)
    {
        artifact.GetComponent<Position>().value = pose.position;
        artifact.GetComponent<Rotation>().z = pose.rotation;

        ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
        manifestation.world_size = pose.world_size;
        manifestation.flip_x = pose.flip_x;

        ArtifactManifestationTools.ApplyBodySize(artifact, bodyRadius, pose.world_size);
    }

    private readonly struct ManifestationUpdate
    {
        public readonly Entity artifact;
        public readonly ArtifactControlState controlState;
        public readonly float bodyRadius;
        public readonly bool visible;
        public readonly bool followsOwner;
        public readonly bool active;
        public readonly float activeWorldSize;
        public readonly ArtifactPresentationPose pose;

        public ManifestationUpdate(
            Entity artifact,
            ArtifactControlState controlState,
            float bodyRadius,
            bool visible,
            bool followsOwner,
            bool active,
            float activeWorldSize,
            ArtifactPresentationPose pose)
        {
            this.artifact = artifact;
            this.controlState = controlState;
            this.bodyRadius = bodyRadius;
            this.visible = visible;
            this.followsOwner = followsOwner;
            this.active = active;
            this.activeWorldSize = activeWorldSize;
            this.pose = pose;
        }
    }
}

/// <summary>
/// 回收既未装备、也没有被独立运动或部署系统接管的法器显化状态。
/// </summary>

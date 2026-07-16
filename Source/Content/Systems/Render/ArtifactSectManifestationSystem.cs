using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Render;

/// <summary>将正在供奉的法器本体稳定陈列在藏宝阁或宗门大殿周围。</summary>
public sealed class ArtifactSectManifestationSystem : QuerySystem<SectInventoryBinder>
{
    private readonly List<InstallationPose> poses = new();
    private readonly List<Entity> artifacts = new();

    public ArtifactSectManifestationSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        poses.Clear();
        Query.ForEachEntity((ref SectInventoryBinder binder, Entity inventory) =>
        {
            Sect sect = binder.Sect;
            artifacts.Clear();
            var relations = inventory.GetRelations<ArtifactSectInstallationRelation>();
            for (int i = 0; i < relations.Length; i++)
            {
                if (relations[i].artifact.IsAvailable()) artifacts.Add(relations[i].artifact);
            }
            artifacts.Sort((left, right) => left.Id.CompareTo(right.Id));

            Building building = sect == null || sect.isRekt()
                ? null
                : ArtifactSectService.ResolveInstallationBuilding(sect);
            if (building == null)
            {
                for (int i = 0; i < artifacts.Count; i++)
                {
                    poses.Add(new InstallationPose(artifacts[i], default, 0f, false));
                }
                return;
            }

            int count = artifacts.Count;
            for (int i = 0; i < count; i++)
            {
                float angle = Time.time * 0.35f + i * Mathf.PI * 2f / Math.Max(1, count);
                Vector3 position = building.cur_transform_position + new Vector3(
                    Mathf.Cos(angle) * (0.62f + count * 0.025f),
                    0.72f + Mathf.Sin(angle) * 0.12f,
                    -0.02f);
                poses.Add(new InstallationPose(
                    artifacts[i],
                    position,
                    Mathf.Sin(angle) * 4f,
                    building.is_visible));
            }
        });

        for (int i = 0; i < poses.Count; i++) ApplyPose(poses[i]);
    }

    private static void ApplyPose(InstallationPose pose)
    {
        Entity artifact = pose.artifact;
        ArtifactPresentationAsset presentation =
            ((ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type).presentation;
        ArtifactManifestationTools.EnsureWorldComponents(artifact, presentation.body_radius);

        artifact.GetComponent<Position>().value = pose.position;
        artifact.GetComponent<Rotation>().z = pose.rotation;
        ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
        manifestation.control_state = ArtifactControlState.Operating;
        manifestation.world_size = 0.86f;
        manifestation.flip_x = false;
        manifestation.visible = pose.visible;
        ArtifactManifestationTools.ApplyBodySize(artifact, presentation.body_radius, manifestation.world_size);
    }

    private readonly struct InstallationPose
    {
        public readonly Entity artifact;
        public readonly Vector3 position;
        public readonly float rotation;
        public readonly bool visible;

        public InstallationPose(Entity artifact, Vector3 position, float rotation, bool visible)
        {
            this.artifact = artifact;
            this.position = position;
            this.rotation = rotation;
            this.visible = visible;
        }
    }
}

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
public class ArtifactManifestationCleanupSystem : QuerySystem<Artifact, ArtifactManifestation>
{
    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref Artifact _, ref ArtifactManifestation _, Entity artifact) =>
        {
            if (artifact.HasComponent<ArtifactIndependentMotion>() ||
                artifact.HasComponent<SkillExecutionBodyLease>() ||
                HasSectInstallation(artifact) ||
                HasLiveController(artifact)) return;

            CommandBuffer.RemoveComponent<ArtifactManifestation>(artifact.Id);
            CommandBuffer.RemoveComponent<ArtifactBody>(artifact.Id);
            CommandBuffer.RemoveComponent<ArtifactBodyGeometry>(artifact.Id);
            CommandBuffer.RemoveComponent<Position>(artifact.Id);
            CommandBuffer.RemoveComponent<Rotation>(artifact.Id);
        });
    }

    private static bool HasLiveController(Entity artifact)
    {
        foreach (Entity owner in artifact.GetIncomingLinks<EquippedArtifactRelation>().Entities)
        {
            Actor actor = owner.GetComponent<ActorBinder>().Actor;
            if (actor != null && actor.isAlive()) return true;
        }
        return false;
    }

    private static bool HasSectInstallation(Entity artifact)
    {
        foreach (Entity _ in artifact.GetIncomingLinks<ArtifactSectInstallationRelation>().Entities)
        {
            return true;
        }
        return false;
    }
}

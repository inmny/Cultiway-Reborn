using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>
/// 观察装备关系和控制状态，并在查询结束后推进每件法器的通用能力生命周期。
/// </summary>
public sealed class ArtifactAbilityLifecycleSystem : QuerySystem<ArtifactAbilitySet, ArtifactAbilityRuntime>
{
    private readonly List<Entity> artifacts = new();

    public ArtifactAbilityLifecycleSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagUncompleted, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        artifacts.Clear();
        Query.ForEachEntity((ref ArtifactAbilitySet _, ref ArtifactAbilityRuntime __, Entity artifact) =>
        {
            artifacts.Add(artifact);
        });

        ArtifactAbilityLifecycle.ApplyPendingDeploymentChanges();
        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactAbilityLifecycle.Synchronize(artifacts[i]);
        }
        ArtifactAbilityLifecycle.ApplyPendingDeploymentReleases();
    }
}

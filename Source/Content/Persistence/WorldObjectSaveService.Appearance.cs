using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core.Components;

namespace Cultiway.Content.Persistence;

internal static partial class WorldObjectSaveService
{
    /// <summary>重建显示尺寸与碰撞派生值；正式旧档暂停加载时也立即使用当前世界尺寸规则。</summary>
    private static void RestoreArtifactPresentationSizes()
    {
        foreach (var artifact in ModClass.I.W.Query<ArtifactManifestation, ArtifactAbilityRuntime>().Entities)
        {
            var manifestation = artifact.GetComponent<ArtifactManifestation>();
            var runtime = artifact.GetComponent<ArtifactAbilityRuntime>();
            if (!manifestation.active_visual || runtime.controller.IsNull || !runtime.controller.HasComponent<ActorBinder>()) continue;
            Actor actor = runtime.controller.GetComponent<ActorBinder>().Actor;
            if (actor == null || !actor.isAlive()) continue;
            ArtifactManifestationTools.ApplyActiveWorldSize(artifact, actor);
            if (artifact.TryGetComponent(out ArtifactDeployment deployment))
                ArtifactManifestationTools.AlignWorldAnchor(artifact, deployment.ResolveBodyAnchor(), deployment.origin);
        }
    }
}

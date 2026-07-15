using Cultiway.Content.Components;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 法器能力和默认跟随系统共用的世界本体初始化入口。
/// </summary>
public static class ArtifactManifestationTools
{
    /// <returns>本次调用创建了世界组件时返回 true。</returns>
    public static bool EnsureWorldComponents(Entity artifact, float bodyRadius)
    {
        if (artifact.HasComponent<ArtifactManifestation>()) return false;

        artifact.AddComponent(new ArtifactManifestation());
        artifact.AddComponent(new Position());
        artifact.AddComponent(new Rotation());
        artifact.AddComponent(new ArtifactBody
        {
            radius = bodyRadius,
            targetable = true,
            collidable = true,
        });
        return true;
    }
}

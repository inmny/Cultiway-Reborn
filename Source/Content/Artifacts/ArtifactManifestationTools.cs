using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using UnityEngine;

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

    /// <summary>
    /// 计算法器激活后的最长边，使法器与驾驭者当前贴图的单像素占据相同世界尺寸。
    /// </summary>
    public static float ResolveActiveWorldSize(Entity artifact, Actor controller)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        Sprite artifactSprite = shape.GetWorldSprite(artifact);
        Sprite controllerSprite = controller.calculateMainSprite();
        float controllerPixelSize = controller.current_scale.y / controllerSprite.pixelsPerUnit;
        float artifactPixelSpan = Mathf.Max(artifactSprite.bounds.size.x, artifactSprite.bounds.size.y) *
                                  artifactSprite.pixelsPerUnit;
        return artifactPixelSpan * controllerPixelSize * shape.presentation.active_pixel_scale;
    }

    /// <summary>立即同步激活尺寸与对应碰撞半径。</summary>
    public static void ApplyActiveWorldSize(Entity artifact, Actor controller)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        float worldSize = ResolveActiveWorldSize(artifact, controller);
        artifact.GetComponent<ArtifactManifestation>().world_size = worldSize;
        artifact.GetComponent<ArtifactBody>().radius = shape.presentation.body_radius * worldSize;
    }

    /// <summary>取得法器本体锚点相对于 Position 的世界空间偏移。</summary>
    public static Vector3 ResolveWorldAnchorOffset(Entity artifact, ArtifactBodyAnchorKind anchor)
    {
        if (anchor == ArtifactBodyAnchorKind.Center) return Vector3.zero;
        if (anchor != ArtifactBodyAnchorKind.ForwardTip)
            throw new System.ArgumentOutOfRangeException(nameof(anchor), anchor, null);

        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        Sprite sprite = shape.GetWorldSprite(artifact);
        float spriteScale = artifact.GetComponent<ArtifactManifestation>().world_size /
                            Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float forwardDistance = sprite.bounds.max.y * spriteScale;
        float rotation = artifact.GetComponent<Rotation>().z;
        return Quaternion.Euler(0f, 0f, rotation) * (Vector3.up * forwardDistance);
    }

    /// <summary>取得法器本体锚点的世界坐标。</summary>
    public static Vector3 ResolveWorldAnchor(Entity artifact, ArtifactBodyAnchorKind anchor)
    {
        return artifact.GetComponent<Position>().value + ResolveWorldAnchorOffset(artifact, anchor);
    }

    /// <summary>移动法器本体，使指定锚点与世界作用点重合。</summary>
    public static void AlignWorldAnchor(Entity artifact, ArtifactBodyAnchorKind anchor, Vector3 origin)
    {
        artifact.GetComponent<Position>().value = origin - ResolveWorldAnchorOffset(artifact, anchor);
    }
}

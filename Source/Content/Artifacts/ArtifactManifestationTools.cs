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
        if (ArtifactAppearanceRenderer.TryResolveBodyGeometry(artifact, out ArtifactBodyGeometry geometry))
        {
            artifact.AddComponent(geometry);
        }
        return true;
    }

    /// <summary>按器形的世界尺寸和驾驭者体型显化，不再受烘焙贴图分辨率影响。</summary>
    /// <param name="artifact">实际显化的法宝。</param>
    /// <param name="controller">当前驾驭者。</param>
    public static float ResolveActiveWorldSize(Entity artifact, Actor controller)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        ArtifactMotionProfile motion = shape.presentation.active_motion ?? shape.presentation.idle_motion;
        return motion.world_size * Mathf.Max(0.03f, controller.actor_scale) * 10f;
    }

    /// <summary>立即同步激活尺寸与对应碰撞半径。</summary>
    public static void ApplyActiveWorldSize(Entity artifact, Actor controller)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        float worldSize = ResolveActiveWorldSize(artifact, controller);
        ref ArtifactManifestation manifestation = ref artifact.GetComponent<ArtifactManifestation>();
        manifestation.active_visual = true;
        manifestation.world_size = worldSize;
        ApplyBodySize(artifact, shape.presentation.body_radius, worldSize);
    }

    /// <summary>按显化状态取得器形提供的世界贴图，并兼容仅实现普通世界贴图的专属器形。</summary>
    public static Sprite ResolveWorldSprite(Entity artifact, bool active)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        return active && shape.GetActiveWorldSprite != null
            ? shape.GetActiveWorldSprite(artifact)
            : shape.GetWorldSprite(artifact);
    }

    /// <summary>取得世界尺寸和碰撞换算所用的本体贴图，不计外围辉光与阴影。</summary>
    public static Sprite ResolveWorldScaleSprite(Entity artifact, bool active)
    {
        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        if (shape.GetWorldSprites != null)
        {
            ArtifactWorldSpriteSet sprites = shape.GetWorldSprites(artifact, active);
            if (sprites.ScaleReference != null) return sprites.ScaleReference;
        }
        return ResolveWorldSprite(artifact, active);
    }

    /// <summary>按当前显化尺寸把模型空间边界转换为世界碰撞代理。</summary>
    public static void ApplyBodySize(Entity artifact, float fallbackRadius, float worldSize)
    {
        ref ArtifactBody body = ref artifact.GetComponent<ArtifactBody>();
        body.radius = fallbackRadius * worldSize;
        body.lateral_extent = body.radius;
        body.forward_extent = body.radius;
        body.backward_extent = body.radius;
        body.sort_pivot_y = -body.radius;
        if (!artifact.TryGetComponent(out ArtifactBodyGeometry geometry)) return;

        Vector3 size = geometry.local_max - geometry.local_min;
        float span = Mathf.Max(size.x, size.y, 0.001f);
        float scale = worldSize / span;
        body.lateral_extent = Mathf.Max(
            Mathf.Abs(geometry.local_min.x),
            Mathf.Abs(geometry.local_max.x)) * scale;
        body.forward_extent = Mathf.Max(0f, geometry.local_max.y * scale);
        body.backward_extent = Mathf.Max(0f, -geometry.local_min.y * scale);
        body.sort_pivot_y = geometry.local_min.y * scale;
        body.radius = Mathf.Max(body.radius, body.lateral_extent * 0.55f);
    }

    /// <summary>取得法器本体锚点相对于 Position 的世界空间偏移。</summary>
    public static Vector3 ResolveWorldAnchorOffset(Entity artifact, ArtifactBodyAnchorKind anchor)
    {
        return ResolveWorldAnchorOffset(artifact, new ArtifactBodyAnchorRef(anchor));
    }

    /// <summary>取得组合模型锚点相对于 Position 的世界空间偏移。</summary>
    public static Vector3 ResolveWorldAnchorOffset(Entity artifact, ArtifactBodyAnchorRef anchor)
    {
        if (anchor.UsesAppearanceAnchor && ArtifactAppearanceRenderer.UsesModelBody(artifact) &&
            TryResolveSpatialBodyAnchor(artifact, anchor, out Vector3 point))
        {
            // Position 保留原显化坐标，纵轴包含离地高度。
            return new Vector3(point.x, point.y + point.z, point.z) - artifact.GetComponent<Position>().value;
        }

        if (anchor.UsesAppearanceAnchor &&
            ArtifactAppearanceRenderer.TryResolveWorldAnchorOffset(
                artifact,
                anchor.slot,
                anchor.anchor,
                out Vector3 appearanceOffset))
        {
            return appearanceOffset;
        }

        if (anchor.fallback == ArtifactBodyAnchorKind.Center) return Vector3.zero;
        if (anchor.fallback != ArtifactBodyAnchorKind.ForwardTip)
            throw new System.ArgumentOutOfRangeException(nameof(anchor), anchor.fallback, null);

        ArtifactShapeAsset shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        ArtifactManifestation manifestation = artifact.GetComponent<ArtifactManifestation>();
        Sprite sprite = ResolveWorldScaleSprite(artifact, manifestation.active_visual);
        float spriteScale = manifestation.world_size /
                            Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float forwardDistance = sprite.bounds.max.y * spriteScale;
        float rotation = artifact.GetComponent<Rotation>().z;
        return Quaternion.Euler(0f, 0f, rotation) * (Vector3.up * forwardDistance);
    }

    /// <summary>在实际器身上解析部件位置，缺省位置沿用同一模型的中心或前缘。</summary>
    /// <param name="artifact">当前显化的法宝。</param>
    /// <param name="anchor">指定部件及缺省位置。</param>
    /// <param name="position">地图位置和独立离地高度。</param>
    internal static bool TryResolveSpatialBodyAnchor(Entity artifact, ArtifactBodyAnchorRef anchor, out Vector3 position)
    {
        if (anchor.UsesAppearanceAnchor &&
            ArtifactAppearanceRenderer.TryResolveSpatialAnchor(artifact, anchor.slot, anchor.anchor, out position)) return true;
        position = default;
        if (!ArtifactAppearanceRenderer.TryGetSpatialPose(artifact, out var pose)) return false;
        position = anchor.fallback == ArtifactBodyAnchorKind.ForwardTip
            ? pose.TransformPoint(new Vector3(0f, pose.Model.Bounds.max.y, 0f))
            : pose.Position;
        return true;
    }

    /// <summary>取得法器本体锚点的世界坐标。</summary>
    public static Vector3 ResolveWorldAnchor(Entity artifact, ArtifactBodyAnchorKind anchor)
    {
        return artifact.GetComponent<Position>().value + ResolveWorldAnchorOffset(artifact, anchor);
    }

    public static Vector3 ResolveWorldAnchor(Entity artifact, ArtifactBodyAnchorRef anchor)
    {
        return artifact.GetComponent<Position>().value + ResolveWorldAnchorOffset(artifact, anchor);
    }

    /// <summary>移动法器本体，使指定锚点与世界作用点重合。</summary>
    public static void AlignWorldAnchor(Entity artifact, ArtifactBodyAnchorKind anchor, Vector3 origin)
    {
        artifact.GetComponent<Position>().value = origin - ResolveWorldAnchorOffset(artifact, anchor);
    }

    public static void AlignWorldAnchor(Entity artifact, ArtifactBodyAnchorRef anchor, Vector3 origin)
    {
        artifact.GetComponent<Position>().value = origin - ResolveWorldAnchorOffset(artifact, anchor);
    }

    /// <summary>将部署原件的实际印面对齐预定触地点；其他部署继续使用原锚点。</summary>
    /// <param name="artifact">正在部署的原器物。</param><param name="deployment">活动的锚点和原点。</param>
    internal static void AlignDeploymentAnchor(Entity artifact, in ArtifactDeployment deployment)
    {
        Vector3 origin = ArtifactAbilities.ResolveDeploymentAnchor(artifact, deployment);
        ArtifactBodyAnchorRef anchor = deployment.ResolveBodyAnchor();
        if (!ArtifactAbilities.IsMountainSealDeployment(artifact) ||
            !ArtifactAppearanceRenderer.TryResolveSpatialAnchor(artifact, anchor.slot, anchor.anchor, out Vector3 face))
        {
            AlignWorldAnchor(artifact, anchor, origin);
            return;
        }

        Vector3 delta = new Vector3(origin.x, origin.y - origin.z, origin.z) - face;
        artifact.GetComponent<Position>().value += new Vector3(delta.x, delta.y + delta.z, delta.z);
    }
}

using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Utils;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.Visuals;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 将法器外观 Instance 的组合模型烘焙为图标、静置世界表现和激活世界表现。
/// 三种输出共享模型与锚点，只允许模板中的视角、光照和画布规格不同。
/// </summary>
public static class ArtifactAppearanceRenderer
{
    private static readonly Dictionary<string, ArtifactAppearanceMesh> MeshCache = new();
    private static readonly Dictionary<string, RenderedAppearance> RenderCache = new();
    /// <summary>与二维烘焙共用外观编号和颜色解析的三维模型缓存。</summary>
    private static readonly Dictionary<string, SpatialVisualMesh> SpatialCache = new();

    public static void ClearCache()
    {
        foreach (RenderedAppearance rendered in RenderCache.Values)
        {
            rendered.Destroy();
        }
        RenderCache.Clear();
        MeshCache.Clear();
        SpatialCache.Clear();
    }

    /// <summary>取得用于物品栏和编辑器的法器图标。</summary>
    public static Sprite GetIconSprite(Entity item)
    {
        return GetRendered(item, ArtifactAppearanceRenderKind.Icon)?.Sprites.Main;
    }

    /// <summary>取得法器当前显化状态对应的扁平世界贴图。</summary>
    public static Sprite GetWorldSprite(Entity item)
    {
        ArtifactAppearanceRenderKind kind = item.TryGetComponent(out ArtifactManifestation manifestation) &&
                                            manifestation.active_visual
            ? ArtifactAppearanceRenderKind.WorldActive
            : ArtifactAppearanceRenderKind.WorldIdle;
        return GetRendered(item, kind)?.Sprites.Main;
    }

    /// <summary>取得法器施展能力时使用的扁平世界贴图。</summary>
    public static Sprite GetActiveWorldSprite(Entity item)
    {
        return GetRendered(item, ArtifactAppearanceRenderKind.WorldActive)?.Sprites.Main;
    }

    /// <summary>取得世界视图使用的本体、辉光、阴影和兼容扁平贴图。</summary>
    public static ArtifactWorldSpriteSet GetWorldSprites(Entity item, bool active)
    {
        ArtifactAppearanceRenderKind kind = active
            ? ArtifactAppearanceRenderKind.WorldActive
            : ArtifactAppearanceRenderKind.WorldIdle;
        return GetRendered(item, kind)?.Sprites ?? default;
    }

    /// <summary>取得完整组合模型的局部边界；结果按 Instance 缓存。</summary>
    internal static bool TryResolveBodyGeometry(Entity item, out ArtifactBodyGeometry geometry)
    {
        geometry = default;
        if (!TryGetMesh(item, out ArtifactAppearanceMesh mesh)) return false;
        geometry = new ArtifactBodyGeometry
        {
            local_min = mesh.Min,
            local_max = mesh.Max,
        };
        return true;
    }

    /// <summary>
    /// 将 Instance 中某个 slot 的 variant 锚点转换成当前世界贴图上的偏移。
    /// 换算复用世界贴图的实际投影，因此模板切换视角或画布后仍与可见模型一致。
    /// </summary>
    internal static bool TryResolveWorldAnchorOffset(
        Entity item,
        string slotKey,
        string anchorKey,
        out Vector3 worldOffset)
    {
        worldOffset = default;
        if (string.IsNullOrEmpty(slotKey) || string.IsNullOrEmpty(anchorKey) ||
            !item.TryGetComponent(out ArtifactAppearance appearance) ||
            !item.TryGetComponent(out ArtifactManifestation manifestation) ||
            !item.HasComponent<Rotation>())
        {
            return false;
        }

        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        if (!catalog.Templates.TryGetValue(appearance.template_key, out ArtifactAppearanceTemplateDef template))
        {
            return false;
        }
        ArtifactAppearanceRenderKind kind = manifestation.active_visual
            ? ArtifactAppearanceRenderKind.WorldActive
            : ArtifactAppearanceRenderKind.WorldIdle;
        RenderedAppearance rendered = GetRendered(appearance, template, catalog, kind);
        if (rendered == null || rendered.Sprites.ScaleReference == null ||
            !ArtifactAppearanceGeometry.TryResolveAnchorPoint(
                appearance,
                template,
                catalog,
                slotKey,
                anchorKey,
                out Vector3 anchorPoint))
        {
            return false;
        }

        ArtifactAppearanceProjection projection = rendered.Projection;
        Vector3 cameraPoint = ArtifactAppearanceMath.RotateEuler(
            anchorPoint - projection.Target,
            projection.Rotation);
        Sprite sprite = rendered.Sprites.ScaleReference;
        float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        if (spriteSize <= 0f || manifestation.world_size <= 0f) return false;

        float viewScale = manifestation.world_size / spriteSize;
        Vector3 localOffset = new(
            cameraPoint.x * projection.Scale / sprite.pixelsPerUnit * viewScale,
            cameraPoint.y * projection.Scale / sprite.pixelsPerUnit * viewScale,
            0f);
        worldOffset = Quaternion.Euler(0f, 0f, item.GetComponent<Rotation>().z) * localOffset;
        return true;
    }

    private static RenderedAppearance GetRendered(Entity item, ArtifactAppearanceRenderKind kind)
    {
        if (!item.TryGetComponent(out ArtifactAppearance appearance)) return null;
        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        return catalog.Templates.TryGetValue(appearance.template_key, out ArtifactAppearanceTemplateDef template)
            ? GetRendered(appearance, template, catalog, kind)
            : null;
    }

    private static RenderedAppearance GetRendered(
        ArtifactAppearance appearance,
        ArtifactAppearanceTemplateDef template,
        ArtifactAppearanceCatalog catalog,
        ArtifactAppearanceRenderKind kind)
    {
        string appearanceKey = appearance.GetCacheKey();
        string renderKey = $"{kind}|{appearanceKey}";
        if (RenderCache.TryGetValue(renderKey, out RenderedAppearance rendered)) return rendered;

        ArtifactAppearanceMesh mesh = GetMesh(appearance, template, catalog, appearanceKey);
        if (mesh == null) return null;
        ArtifactAppearancePixelFrame frame = ArtifactAppearanceRasterizer.Render(
            mesh,
            appearance,
            template,
            catalog,
            kind);
        rendered = new RenderedAppearance(CreateSprites(frame, renderKey, kind), frame.Projection);
        RenderCache.Add(renderKey, rendered);
        return rendered;
    }

    /// <summary>直接取得原始组合模型及材质色，不反推已经烘焙的图片。</summary>
    /// <param name="item">组合法宝实体。</param>
    /// <param name="model">返回未投影的共用模型。</param>
    /// <param name="span">返回原始模型的三个方向跨度。</param>
    internal static bool TryGetSpatialMesh(Entity item, out SpatialVisualMesh model, out Vector3 span)
    {
        model = null;
        span = default;
        if (!TryGetMesh(item, out ArtifactAppearanceMesh mesh)) return false;
        ArtifactAppearance appearance = item.GetComponent<ArtifactAppearance>();
        string key = appearance.GetCacheKey();
        span = mesh.Max - mesh.Min;
        if (SpatialCache.TryGetValue(key, out model)) return true;
        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        SpatialVisualMeshBuilder builder = new();
        foreach (ArtifactAppearanceMeshFace face in mesh.Faces)
        {
            Color32[] ramp = ArtifactAppearanceRasterizer.ResolveMaterialRamp(
                appearance, catalog, face.Slot, face.Material);
            builder.AddFace(face.Points, ramp[ramp.Length / 2], face.Normals);
        }
        model = builder.Build(key);
        SpatialCache.Add(key, model);
        return true;
    }

    /// <summary>模型与锚点共同使用的三维姿态，只在显示边界拆开旧显化坐标。</summary>
    /// <param name="item">已经显化的法宝实体。</param>
    /// <param name="pose">返回三维姿态与颜色。</param>
    internal static bool TryGetSpatialPose(Entity item, out SpatialVisualMeshInstance pose)
    {
        pose = default;
        if (!item.TryGetComponent(out ArtifactManifestation manifestation) ||
            !TryGetSpatialMesh(item, out SpatialVisualMesh model, out Vector3 span)) return false;
        float size = manifestation.world_size / Mathf.Max(span.x, span.y, 0.001f);
        float pulse = 1f;
        double now = World.world.getCurWorldTime();
        Color tint = manifestation.control_state.GetStateColor((float)now);
        ArtifactModelPulseVisualState.Apply(item, now, ref pulse, ref tint);
        Vector3 position = item.GetComponent<Position>().value;
        Actor body = null;
        Vector3 groundAnchor = position;
        if (!item.HasComponent<ArtifactIndependentMotion>() && !item.HasComponent<SkillExecutionBodyLease>() &&
            item.TryGetComponent(out ArtifactAbilityRuntime runtime) && !runtime.controller.IsNull &&
            runtime.controller.TryGetComponent(out ActorBinder binder) && !binder.Actor.isRekt())
        {
            // 旧随身布局把悬浮高度写在画面纵轴，显示边界将其还原为独立高度。
            Actor actor = binder.Actor;
            Vector3 local = position - actor.cur_transform_position;
            if (ArtifactYuanshenControlService.TryResolveOrigin(actor, item, out Vector3 remote))
            {
                local -= remote - actor.GetSimPos();
                position = remote + new Vector3(local.x, local.z, local.y);
                groundAnchor = remote;
            }
            else
            {
                body = actor;
                groundAnchor = actor.current_position;
                position = Combat.SpatialActorPose.Foot(actor) + new Vector3(local.x, local.z, local.y);
            }
        }
        else
        {
            position.y -= position.z;
            foreach (Entity inventory in item.GetIncomingLinks<ArtifactSectInstallationRelation>().Entities)
            {
                Sect sect = inventory.GetComponent<SectInventoryBinder>().Sect;
                Building building = ArtifactSectService.ResolveInstallationBuilding(sect);
                if (building == null) continue;
                Vector3 displayed = item.GetComponent<Position>().value;
                groundAnchor = building.current_position;
                position = new Vector3(displayed.x, building.current_position.y, displayed.y - building.current_position.y);
                break;
            }
        }
        float yaw = item.GetComponent<Rotation>().z;
        GoldenBellSpatialVisualCue.ApplyBodyMotion(item, now, ref position, ref yaw);
        Vector3 scale = new Vector3(manifestation.flip_x ? -size : size, size, size) * pulse;
        ArtifactAppearance appearance = item.GetComponent<ArtifactAppearance>();
        ArtifactAppearanceTemplateDef template = ArtifactAppearanceCatalogLoader.Current.Templates[appearance.template_key];
        Quaternion rotation = Quaternion.Euler(0f, 0f, yaw) *
                              Quaternion.Euler(ArtifactAppearanceMath.Vec3(template.SpatialRotation, Vector3.zero));
        if (template.SpatialOriginAtBase)
        {
            float bottom = float.PositiveInfinity;
            Vector3 min = model.Bounds.min;
            Vector3 max = model.Bounds.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                bottom = Mathf.Min(bottom, (rotation * Vector3.Scale(corner, scale)).z);
            }
            position.z -= bottom;
        }
        pose = new SpatialVisualMeshInstance(model, position, rotation, scale, tint, groundAnchor: groundAnchor);
        if (body != null) pose = pose.WithBodyAnchor(body);
        return true;
    }

    /// <summary>从原始组合锚点得到三维作用点，绝不经过烘焙图片的投影。</summary>
    /// <param name="item">已经显化的法宝实体。</param>
    /// <param name="slot">组合部件位置名。</param>
    /// <param name="anchor">部件内的锚点名。</param>
    /// <param name="position">返回未经过图片投影的世界位置。</param>
    internal static bool TryResolveSpatialAnchor(Entity item, string slot, string anchor, out Vector3 position)
    {
        position = default;
        if (!TryGetSpatialPose(item, out SpatialVisualMeshInstance pose)) return false;
        ArtifactAppearance appearance = item.GetComponent<ArtifactAppearance>();
        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        ArtifactAppearanceTemplateDef template = catalog.Templates[appearance.template_key];
        if (!ArtifactAppearanceGeometry.TryResolveAnchorPoint(appearance, template, catalog, slot, anchor,
                out Vector3 local)) return false;
        position = pose.TransformPoint(local);
        return true;
    }

    private static bool TryGetMesh(Entity item, out ArtifactAppearanceMesh mesh)
    {
        mesh = null;
        if (!item.TryGetComponent(out ArtifactAppearance appearance)) return false;
        ArtifactAppearanceCatalog catalog = ArtifactAppearanceCatalogLoader.Current;
        if (!catalog.Templates.TryGetValue(appearance.template_key, out ArtifactAppearanceTemplateDef template))
        {
            return false;
        }
        mesh = GetMesh(appearance, template, catalog, appearance.GetCacheKey());
        return mesh != null;
    }

    private static ArtifactAppearanceMesh GetMesh(
        ArtifactAppearance appearance,
        ArtifactAppearanceTemplateDef template,
        ArtifactAppearanceCatalog catalog,
        string appearanceKey)
    {
        if (MeshCache.TryGetValue(appearanceKey, out ArtifactAppearanceMesh mesh)) return mesh;
        mesh = ArtifactAppearanceGeometry.Build(appearance, template, catalog);
        if (mesh != null) MeshCache.Add(appearanceKey, mesh);
        return mesh;
    }

    private static ArtifactWorldSpriteSet CreateSprites(
        ArtifactAppearancePixelFrame frame,
        string name,
        ArtifactAppearanceRenderKind kind)
    {
        bool trim = kind != ArtifactAppearanceRenderKind.Icon;
        Sprite composite = CreateSprite(frame.Composite, frame.Size, $"{name}.composite", trim);
        if (kind == ArtifactAppearanceRenderKind.Icon)
            return new ArtifactWorldSpriteSet(null, null, null, composite);
        return new ArtifactWorldSpriteSet(
            CreateSprite(frame.Body, frame.Size, $"{name}.body", true),
            CreateSprite(frame.Emission, frame.Size, $"{name}.emission", true),
            CreateSprite(frame.Shadow, frame.Size, $"{name}.shadow", true),
            composite);
    }

    private static Sprite CreateSprite(Color32[] source, int size, string name, bool trim)
    {
        if (!HasVisiblePixel(source)) return null;
        Rect rect = trim ? FindOpaqueRect(source, size) : new Rect(0f, 0f, size, size);
        Vector2 canvasCenter = new(size * 0.5f, size * 0.5f);
        Vector2 pivot = new(
            (canvasCenter.x - rect.x) / rect.width,
            (canvasCenter.y - rect.y) / rect.height);
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels32(ToUnityPixels(source, size));
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            rect,
            pivot);
        sprite.name = name;
        return sprite;
    }

    internal static bool HasVisiblePixel(Color32[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a != 0) return true;
        }
        return false;
    }

    internal static Rect FindOpaqueRect(Color32[] pixels, int size)
    {
        int minX = size;
        int minY = size;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (pixels[y * size + x].a == 0) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }
        int unityY = size - 1 - maxY;
        return new Rect(minX, unityY, maxX - minX + 1, maxY - minY + 1);
    }

    internal static Color32[] ToUnityPixels(Color32[] source, int size)
    {
        Color32[] result = new Color32[source.Length];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                result[(size - 1 - y) * size + x] = source[y * size + x];
            }
        }
        return result;
    }

    private sealed class RenderedAppearance
    {
        internal readonly ArtifactWorldSpriteSet Sprites;
        internal readonly ArtifactAppearanceProjection Projection;

        internal RenderedAppearance(
            ArtifactWorldSpriteSet sprites,
            ArtifactAppearanceProjection projection)
        {
            Sprites = sprites;
            Projection = projection;
        }

        internal void Destroy()
        {
            DestroySprite(Sprites.Body);
            DestroySprite(Sprites.Emission);
            DestroySprite(Sprites.Shadow);
            DestroySprite(Sprites.Composite);
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            Texture texture = sprite.texture;
            Object.Destroy(sprite);
            Object.Destroy(texture);
        }
    }
}

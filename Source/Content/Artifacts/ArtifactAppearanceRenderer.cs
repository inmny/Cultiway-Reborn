using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Components;
using Cultiway.Utils;
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

    public static void ClearCache()
    {
        foreach (RenderedAppearance rendered in RenderCache.Values)
        {
            rendered.Destroy();
        }
        RenderCache.Clear();
        MeshCache.Clear();
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Cultiway.Content.Components;
using Friflo.Engine.ECS;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 根据统一外观实例生成背包图标和世界贴图，并按外观与输出规格缓存结果。
/// </summary>
public static class ArtifactAppearanceRenderer
{
    private const int WorldSpriteSize = 56;
    private const float WorldSpriteFill = 0.86f;
    private static readonly Dictionary<string, Sprite> Cache = new();

    public static void ClearCache()
    {
        Cache.Clear();
    }

    public static Sprite GetIconSprite(Entity item)
    {
        ref ArtifactAppearance appearance = ref item.GetComponent<ArtifactAppearance>();
        return GetSprite(appearance, ArtifactAppearanceCatalogLoader.Current.Canvas, AppearanceOutput.Icon);
    }

    public static Sprite GetWorldSprite(Entity item)
    {
        ref ArtifactAppearance appearance = ref item.GetComponent<ArtifactAppearance>();
        return GetSprite(appearance, WorldSpriteSize, AppearanceOutput.World);
    }

    private static Sprite GetSprite(ArtifactAppearance appearance, int size, AppearanceOutput output)
    {
        var cacheKey = $"{output}|{appearance.GetCacheKey()}";
        if (Cache.TryGetValue(cacheKey, out var sprite)) return sprite;

        var catalog = ArtifactAppearanceCatalogLoader.Current;
        if (!catalog.Templates.TryGetValue(appearance.template_key, out var template)) return null;

        var texture = Render(appearance, template, size, catalog.Canvas, output);
        if (texture == null) return null;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        sprite = CreateSprite(texture, output);
        Cache[cacheKey] = sprite;
        return sprite;
    }

    private static Texture2D Render(
        ArtifactAppearance appearance,
        ArtifactAppearanceTemplateDef template,
        int size,
        int baseCanvas,
        AppearanceOutput output)
    {
        var faces = BuildWorldFaces(appearance, template);
        if (faces.Count == 0) return null;

        Projection projection = output == AppearanceOutput.Icon
            ? ResolveIconProjection(template, size, baseCanvas)
            : ResolveWorldProjection(faces, size);
        var light = LightDirection(template.Light);
        // 表面纹理只由完整外观数据决定，同一外观在任何时间和实体上都会得到相同像素。
        var surfacePattern = StableHash($"{appearance.GetCacheKey()}|{template.Key}|3d");

        var pixels = new Color32[size * size];
        var depth = new float[size * size];
        for (int i = 0; i < depth.Length; i++)
        {
            depth[i] = -1e9f;
            pixels[i] = new Color32(0, 0, 0, 0);
        }

        foreach (var face in faces)
        {
            var projected = ProjectFace(
                face,
                appearance,
                projection.Target,
                projection.Rotation,
                projection.Scale,
                size,
                light);
            if (projected.Points.Length < 3) continue;
            RasterProjectedFace(projected, pixels, depth, size, surfacePattern);
        }

        AddDepthEdges(pixels, depth, size);
        AddOuterOutline(pixels, size);
        ClearTransparentPixels(pixels);

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.SetPixels32(ToUnityPixels(pixels, size));
        texture.Apply();
        return texture;
    }

    private static Projection ResolveIconProjection(
        ArtifactAppearanceTemplateDef template,
        int size,
        int baseCanvas)
    {
        JObject camera = template.Camera;
        return new Projection(
            ReadVec3(camera["target"], Vector3.zero),
            new Vector3(
                -ReadFloat(camera, "pitch", 0f),
                -ReadFloat(camera, "yaw", 0f),
                -ReadFloat(camera, "roll", 0f)),
            ReadFloat(camera, "scale", 8f) * size / baseCanvas);
    }

    /// <summary>
    /// 世界贴图固定沿模型正面投影：屏幕上的局部 +Y 始终是法器逻辑前向，世界 Rotation 可直接表示真实朝向。
    /// </summary>
    private static Projection ResolveWorldProjection(IReadOnlyList<Face3D> faces, int size)
    {
        Vector3 min = faces[0].Points[0];
        Vector3 max = min;
        for (int i = 0; i < faces.Count; i++)
        {
            Vector3[] points = faces[i].Points;
            for (int j = 0; j < points.Length; j++)
            {
                min = Vector3.Min(min, points[j]);
                max = Vector3.Max(max, points[j]);
            }
        }

        float extent = Mathf.Max(max.x - min.x, max.y - min.y, 0.001f);
        return new Projection(
            (min + max) * 0.5f,
            Vector3.zero,
            size * WorldSpriteFill / extent);
    }

    private static Sprite CreateSprite(Texture2D texture, AppearanceOutput output)
    {
        Rect rect = output == AppearanceOutput.World
            ? FindOpaqueRect(texture)
            : new Rect(0f, 0f, texture.width, texture.height);
        var canvasCenter = new Vector2(texture.width * 0.5f, texture.height * 0.5f);
        var pivot = new Vector2(
            (canvasCenter.x - rect.x) / rect.width,
            (canvasCenter.y - rect.y) / rect.height);
        return Sprite.Create(texture, rect, pivot);
    }

    private static Rect FindOpaqueRect(Texture2D texture)
    {
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width;
        int minY = texture.height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a == 0) continue;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
        }

        return maxX < minX
            ? new Rect(0f, 0f, texture.width, texture.height)
            : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static List<Face3D> BuildWorldFaces(
        ArtifactAppearance appearance,
        ArtifactAppearanceTemplateDef template)
    {
        List<Face3D> faces = new();
        var catalog = ArtifactAppearanceCatalogLoader.Current;
        var placements = template.Placements
            .OrderBy(item => item.Z)
            .ToArray();
        foreach (var placement in placements)
        {
            ArtifactAppearanceModuleDef module = catalog.Modules[placement.Module];
            var slot = FindPart(appearance, placement);
            if (slot == null) continue;
            var variant = module.GetVariant(slot.Value.variant);
            if (variant == null) continue;
            ArtifactAppearanceAnchorDef anchor = variant.GetAnchor(placement.Anchor);

            var placementPosition = Vec3(placement.Position, Vector3.zero);
            var placementRotation = Vec3(placement.Rotation, Vector3.zero);
            var placementScale = Vec3(placement.Scale, Vector3.one);
            var anchorPosition = Vec3(anchor.Position, Vector3.zero);
            foreach (var part in variant.Parts)
            {
                foreach (var partFace in PartFaces(part))
                {
                    var points = new Vector3[partFace.Points.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        var point = partFace.Points[i] - anchorPosition;
                        point = Vector3.Scale(point, placementScale);
                        point = RotateEuler(point, placementRotation);
                        points[i] = point + placementPosition;
                    }
                    faces.Add(new Face3D(points, slot.Value.slot, partFace.Material));
                }
            }
        }
        return faces;
    }

    private static ArtifactAppearancePart? FindPart(
        ArtifactAppearance appearance,
        ArtifactAppearancePlacementDef placement)
    {
        for (int i = 0; i < appearance.parts.Length; i++)
        {
            var part = appearance.parts[i];
            if (part.slot == placement.Slot && part.module == placement.Module) return part;
        }
        for (int i = 0; i < appearance.parts.Length; i++)
        {
            var part = appearance.parts[i];
            if (part.slot == placement.Slot) return part;
        }
        return null;
    }

    private static IEnumerable<Face3D> PartFaces(JObject part)
    {
        var primitive = part.Value<string>("primitive") ?? part.Value<string>("type") ?? string.Empty;
        var material = part.Value<string>("material") ?? "main";
        List<Vector3[]> faces = primitive switch
        {
            "box" => BoxFaces(part),
            "poly_prism" => PolyPrismFaces(part),
            "blade" => BladeFaces(part),
            "cylinder" or "frustum" => FrustumFaces(part),
            "ellipsoid" => EllipsoidFaces(part),
            _ => [],
        };

        foreach (var face in faces)
        {
            var points = new Vector3[face.Length];
            for (int i = 0; i < face.Length; i++)
            {
                points[i] = TransformLocalPoint(face[i], part);
            }
            yield return new Face3D(points, string.Empty, material);
        }
    }

    private static List<Vector3[]> BoxFaces(JObject part)
    {
        var size = ReadVec3(part["size"], Vector3.one);
        var x = size.x / 2f;
        var y = size.y / 2f;
        var z = size.z / 2f;
        Vector3[] v =
        [
            new(-x, -y, -z), new(x, -y, -z), new(x, y, -z), new(-x, y, -z),
            new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z),
        ];
        return
        [
            [v[4], v[5], v[6], v[7]],
            [v[1], v[0], v[3], v[2]],
            [v[0], v[4], v[7], v[3]],
            [v[5], v[1], v[2], v[6]],
            [v[3], v[7], v[6], v[2]],
            [v[0], v[1], v[5], v[4]],
        ];
    }

    private static List<Vector3[]> PolyPrismFaces(JObject part)
    {
        var points = ReadPoints2(part["points"]);
        var depth = ReadFloat(part, "depth", 0.12f);
        List<Vector3[]> faces = new();
        if (points.Count < 3) return faces;

        var front = points.Select(point => new Vector3(point.x, point.y, depth / 2f)).ToArray();
        var back = points.Select(point => new Vector3(point.x, point.y, -depth / 2f)).Reverse().ToArray();
        faces.Add(front);
        faces.Add(back);
        for (int i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            faces.Add([
                new Vector3(points[i].x, points[i].y, depth / 2f),
                new Vector3(points[j].x, points[j].y, depth / 2f),
                new Vector3(points[j].x, points[j].y, -depth / 2f),
                new Vector3(points[i].x, points[i].y, -depth / 2f),
            ]);
        }
        return faces;
    }

    private static List<Vector3[]> BladeFaces(JObject part)
    {
        var length = ReadFloat(part, "length", 2.4f);
        var width = ReadFloat(part, "width", 0.32f);
        var depth = ReadFloat(part, "depth", 0.08f);
        var shoulder = ReadFloat(part, "shoulder", 0.13f);
        var baseWidth = ReadFloat(part, "base", width * 0.55f);
        List<Vector2> outline =
        [
            new(0f, length),
            new(width / 2f, shoulder),
            new(baseWidth / 2f, 0f),
            new(-baseWidth / 2f, 0f),
            new(-width / 2f, shoulder),
        ];

        var faces = PolyPrismFromOutline(outline, depth);
        faces.Add([
            new Vector3(0f, length * 0.94f, depth * 0.58f),
            new Vector3(width * 0.16f, shoulder, depth / 2f),
            new Vector3(0f, 0.02f, depth * 0.58f),
            new Vector3(-width * 0.16f, shoulder, depth / 2f),
        ]);
        return faces;
    }

    private static List<Vector3[]> FrustumFaces(JObject part)
    {
        var height = ReadFloat(part, "height", 1f);
        var segments = Mathf.Max(5, ReadInt(part, "segments", 12));
        var topRadius = RadiusPair(part["top_radius"] ?? part["radius"], new Vector2(0.5f, 0.5f));
        var bottomRadius = RadiusPair(part["bottom_radius"] ?? part["radius"], new Vector2(0.5f, 0.5f));
        var topY = height / 2f;
        var bottomY = -height / 2f;
        Vector3[] top = new Vector3[segments];
        Vector3[] bottom = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            var angle = Mathf.PI * 2f * i / segments;
            var ca = Mathf.Cos(angle);
            var sa = Mathf.Sin(angle);
            top[i] = new Vector3(ca * topRadius.x, topY, sa * topRadius.y);
            bottom[i] = new Vector3(ca * bottomRadius.x, bottomY, sa * bottomRadius.y);
        }

        List<Vector3[]> faces = new();
        for (int i = 0; i < segments; i++)
        {
            var j = (i + 1) % segments;
            faces.Add([bottom[i], bottom[j], top[j], top[i]]);
        }
        if (ReadBool(part, "cap_top", true)) faces.Add(top.Reverse().ToArray());
        if (ReadBool(part, "cap_bottom", true)) faces.Add(bottom);
        return faces;
    }

    private static List<Vector3[]> EllipsoidFaces(JObject part)
    {
        var radius = ReadVec3(part["radius"], new Vector3(0.5f, 0.5f, 0.5f));
        var segments = Mathf.Max(6, ReadInt(part, "segments", 10));
        var rings = Mathf.Max(3, ReadInt(part, "rings", 5));
        Vector3[][] rows = new Vector3[rings + 1][];
        for (int ring = 0; ring <= rings; ring++)
        {
            var phi = -Mathf.PI / 2f + Mathf.PI * ring / rings;
            var y = Mathf.Sin(phi) * radius.y;
            var r = Mathf.Cos(phi);
            rows[ring] = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                var angle = Mathf.PI * 2f * i / segments;
                rows[ring][i] = new Vector3(Mathf.Cos(angle) * r * radius.x, y, Mathf.Sin(angle) * r * radius.z);
            }
        }

        List<Vector3[]> faces = new();
        for (int ring = 0; ring < rings; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                var j = (i + 1) % segments;
                faces.Add([rows[ring][i], rows[ring][j], rows[ring + 1][j], rows[ring + 1][i]]);
            }
        }
        return faces;
    }

    private static List<Vector3[]> PolyPrismFromOutline(List<Vector2> outline, float depth)
    {
        List<Vector3[]> faces = new();
        var front = outline.Select(point => new Vector3(point.x, point.y, depth / 2f)).ToArray();
        var back = outline.Select(point => new Vector3(point.x, point.y, -depth / 2f)).Reverse().ToArray();
        faces.Add(front);
        faces.Add(back);
        for (int i = 0; i < outline.Count; i++)
        {
            var j = (i + 1) % outline.Count;
            faces.Add([
                new Vector3(outline[i].x, outline[i].y, depth / 2f),
                new Vector3(outline[j].x, outline[j].y, depth / 2f),
                new Vector3(outline[j].x, outline[j].y, -depth / 2f),
                new Vector3(outline[i].x, outline[i].y, -depth / 2f),
            ]);
        }
        return faces;
    }

    private static ProjectedFace ProjectFace(
        Face3D face,
        ArtifactAppearance appearance,
        Vector3 target,
        Vector3 cameraRotation,
        float scale,
        int size,
        Vector3 light)
    {
        var cameraPoints = new Vector3[face.Points.Length];
        for (int i = 0; i < face.Points.Length; i++)
        {
            cameraPoints[i] = RotateEuler(face.Points[i] - target, cameraRotation);
        }
        var normalWorld = FaceNormal(face.Points);
        var normalCamera = FaceNormal(cameraPoints);
        if (normalCamera.z < 0f) normalWorld = -normalWorld;

        var color = ResolveMaterialColor(face, appearance);
        color = ShadeColor(color, normalWorld, light, face.Material);
        var projected = new Vector3[cameraPoints.Length];
        for (int i = 0; i < cameraPoints.Length; i++)
        {
            var point = cameraPoints[i];
            projected[i] = new Vector3(size * 0.5f + point.x * scale, size * 0.5f - point.y * scale, point.z);
        }
        return new ProjectedFace(projected, face.Material, color);
    }

    private static Color32 ResolveMaterialColor(Face3D face, ArtifactAppearance appearance)
    {
        ArtifactAppearancePart? partValue = null;
        for (int i = 0; i < appearance.parts.Length; i++)
        {
            if (appearance.parts[i].slot == face.Slot)
            {
                partValue = appearance.parts[i];
                break;
            }
        }

        if (partValue.HasValue)
        {
            var part = partValue.Value;
            for (int i = 0; i < part.colors.Length; i++)
            {
                if (part.colors[i].material == face.Material)
                {
                    return ParseColor(part.colors[i].color_hex, new Color32(154, 160, 168, 255));
                }
            }
            if (!string.IsNullOrEmpty(part.color_scheme) &&
                ArtifactAppearanceCatalogLoader.Current.ColorSchemes.TryGetValue(part.color_scheme, out var scheme) &&
                scheme.Colors != null &&
                scheme.Colors.TryGetValue(face.Material, out var hex))
            {
                return ParseColor(hex, new Color32(154, 160, 168, 255));
            }
        }
        return new Color32(154, 160, 168, 255);
    }

    private static Color32 ShadeColor(Color32 color, Vector3 normal, Vector3 light, string material)
    {
        normal = Normalize(normal, Vector3.forward);
        var diffuse = Mathf.Max(0f, Vector3.Dot(normal, light));
        var sideDark = Mathf.Max(0f, -normal.x * 0.18f + -normal.z * 0.10f);
        var amount = 0.52f + diffuse * 0.56f - sideDark;
        if (IsBrightMaterial(material)) amount += 0.08f;
        if (IsGlowMaterial(material)) amount += 0.16f;
        amount = Mathf.Clamp(amount, 0.22f, 1.08f);

        Color32 result = amount >= 1f
            ? Lighten(color, (amount - 1f) * 0.75f)
            : Darken(color, 1f - amount);
        if (IsGlowMaterial(material) && diffuse > 0.45f)
        {
            result = Lighten(result, 0.14f);
        }
        return result;
    }

    private static void RasterProjectedFace(
        ProjectedFace face,
        Color32[] pixels,
        float[] depth,
        int size,
        int surfacePattern)
    {
        for (int i = 1; i < face.Points.Length - 1; i++)
        {
            RasterTriangle(face.Points[0], face.Points[i], face.Points[i + 1], face, pixels, depth, size,
                surfacePattern);
        }
    }

    private static void RasterTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        ProjectedFace face,
        Color32[] pixels,
        float[] depth,
        int size,
        int surfacePattern)
    {
        var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        var maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        var maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
        var area = Edge(a, b, c);
        if (Mathf.Abs(area) < 1e-6f) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var sample = new Vector3(x + 0.5f, y + 0.5f, 0f);
                var w0 = Edge(b, c, sample) / area;
                var w1 = Edge(c, a, sample) / area;
                var w2 = Edge(a, b, sample) / area;
                if (w0 < -1e-5f || w1 < -1e-5f || w2 < -1e-5f) continue;

                var z = a.z * w0 + b.z * w1 + c.z * w2;
                var index = y * size + x;
                if (z <= depth[index]) continue;
                pixels[index] = AddFaceTexture(face.Color, face.Material, x, y, surfacePattern);
                depth[index] = z;
            }
        }
    }

    private static void AddDepthEdges(Color32[] pixels, float[] depth, int size)
    {
        var source = (Color32[])pixels.Clone();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var index = y * size + x;
                if (source[index].a == 0) continue;
                var current = depth[index];
                var edgeAmount = 0f;
                if (x + 1 < size) edgeAmount = Mathf.Max(edgeAmount, DepthEdgeAmount(current, depth[y * size + x + 1]));
                if (y + 1 < size) edgeAmount = Mathf.Max(edgeAmount, DepthEdgeAmount(current, depth[(y + 1) * size + x]));
                if (edgeAmount > 0f) pixels[index] = Darken(source[index], edgeAmount);
            }
        }
    }

    private static float DepthEdgeAmount(float current, float other)
    {
        if (other <= -1e8f) return 0f;
        var diff = Mathf.Abs(current - other);
        return diff > 0.12f ? Mathf.Min(0.22f, diff * 0.13f) : 0f;
    }

    private static void AddOuterOutline(Color32[] pixels, int size)
    {
        var source = (Color32[])pixels.Clone();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var index = y * size + x;
                if (source[index].a != 0) continue;

                Color32 best = default;
                var hasBest = false;
                var bestLuma = float.MaxValue;
                for (int yy = y - 1; yy <= y + 1; yy++)
                {
                    for (int xx = x - 1; xx <= x + 1; xx++)
                    {
                        if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                        var color = source[yy * size + xx];
                        if (color.a == 0) continue;
                        var luma = Luma(color);
                        if (luma >= bestLuma) continue;
                        bestLuma = luma;
                        best = color;
                        hasBest = true;
                    }
                }
                if (hasBest) pixels[index] = Darken(best, 0.50f);
            }
        }
    }

    private static Color32 AddFaceTexture(Color32 color, string material, int x, int y, int surfacePattern)
    {
        var result = color;
        var value = ((x * 73) ^ (y * 151) ^ surfacePattern) & 0xFF;
        if (material is "cloth" or "stone" or "jade" or "copper" or "bronze" or "metal")
        {
            if ((x + y + surfacePattern) % 4 == 0)
            {
                result = Darken(result, 0.05f + value % 4 * 0.015f);
            }
            else if ((x * 2 - y + surfacePattern) % 7 == 0)
            {
                result = Lighten(result, 0.06f);
            }
        }
        if ((material is "glass" or "fire" or "core") && (x - y + surfacePattern) % 5 == 0)
        {
            result = Lighten(result, 0.12f);
        }
        return result;
    }

    private static Vector3 LightDirection(JObject light)
    {
        light ??= new JObject();
        var yaw = ReadFloat(light, "yaw", -35f) * Mathf.Deg2Rad;
        var pitch = ReadFloat(light, "pitch", 55f) * Mathf.Deg2Rad;
        return Normalize(new Vector3(Mathf.Cos(pitch) * Mathf.Sin(yaw), Mathf.Sin(pitch), Mathf.Cos(pitch) * Mathf.Cos(yaw)), Vector3.forward);
    }

    private static Vector3 TransformLocalPoint(Vector3 point, JObject part)
    {
        point = Vector3.Scale(point, ReadVec3(part["scale"], Vector3.one));
        point = RotateEuler(point, ReadVec3(part["rotation"] ?? part["rot"], Vector3.zero));
        return point + ReadVec3(part["offset"] ?? part["position"], Vector3.zero);
    }

    private static Vector3 RotateEuler(Vector3 point, Vector3 rotation)
    {
        var x = point.x;
        var y = point.y;
        var z = point.z;
        if (rotation.x != 0f)
        {
            var angle = rotation.x * Mathf.Deg2Rad;
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);
            var ny = y * c - z * s;
            var nz = y * s + z * c;
            y = ny;
            z = nz;
        }
        if (rotation.y != 0f)
        {
            var angle = rotation.y * Mathf.Deg2Rad;
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);
            var nx = x * c + z * s;
            var nz = -x * s + z * c;
            x = nx;
            z = nz;
        }
        if (rotation.z != 0f)
        {
            var angle = rotation.z * Mathf.Deg2Rad;
            var c = Mathf.Cos(angle);
            var s = Mathf.Sin(angle);
            var nx = x * c - y * s;
            var ny = x * s + y * c;
            x = nx;
            y = ny;
        }
        return new Vector3(x, y, z);
    }

    private static Vector3 FaceNormal(IReadOnlyList<Vector3> points)
    {
        if (points.Count < 3) return Vector3.forward;
        return Normalize(Vector3.Cross(points[1] - points[0], points[2] - points[0]), Vector3.forward);
    }

    private static Vector3 Normalize(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude <= 1e-8f ? fallback : value.normalized;
    }

    private static float Edge(Vector3 a, Vector3 b, Vector3 c)
    {
        return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
    }

    private static Vector3 Vec3(float[] value, Vector3 fallback)
    {
        if (value == null || value.Length == 0) return fallback;
        if (value.Length == 1) return new Vector3(value[0], value[0], value[0]);
        if (value.Length == 2) return new Vector3(value[0], value[1], fallback.z);
        return new Vector3(value[0], value[1], value[2]);
    }

    private static Vector3 ReadVec3(JToken token, Vector3 fallback)
    {
        if (token == null) return fallback;
        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
        {
            var scalar = token.Value<float>();
            return new Vector3(scalar, scalar, scalar);
        }
        if (token is not JArray array || array.Count == 0) return fallback;
        if (array.Count == 1)
        {
            var scalar = array[0].Value<float>();
            return new Vector3(scalar, scalar, scalar);
        }
        if (array.Count == 2)
        {
            return new Vector3(array[0].Value<float>(), array[1].Value<float>(), fallback.z);
        }
        return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
    }

    private static List<Vector2> ReadPoints2(JToken token)
    {
        List<Vector2> result = new();
        if (token is not JArray array) return result;
        foreach (var item in array)
        {
            if (item is not JArray point || point.Count < 2) continue;
            result.Add(new Vector2(point[0].Value<float>(), point[1].Value<float>()));
        }
        return result;
    }

    private static Vector2 RadiusPair(JToken token, Vector2 fallback)
    {
        if (token == null) return fallback;
        if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
        {
            var scalar = token.Value<float>();
            return new Vector2(scalar, scalar);
        }
        if (token is not JArray array || array.Count == 0) return fallback;
        if (array.Count == 1)
        {
            var scalar = array[0].Value<float>();
            return new Vector2(scalar, scalar);
        }
        return new Vector2(array[0].Value<float>(), array[1].Value<float>());
    }

    private static float ReadFloat(JObject data, string key, float fallback)
    {
        return data != null && data.TryGetValue(key, out var token) ? token.Value<float>() : fallback;
    }

    private static int ReadInt(JObject data, string key, int fallback)
    {
        return data != null && data.TryGetValue(key, out var token) ? token.Value<int>() : fallback;
    }

    private static bool ReadBool(JObject data, string key, bool fallback)
    {
        return data != null && data.TryGetValue(key, out var token) ? token.Value<bool>() : fallback;
    }

    private static Color32 ParseColor(string hex, Color32 fallback)
    {
        return ColorUtility.TryParseHtmlString(hex, out var color) ? (Color32)color : fallback;
    }

    private static Color32 Lighten(Color32 color, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color32(
            ClampByte(color.r + (255 - color.r) * amount),
            ClampByte(color.g + (255 - color.g) * amount),
            ClampByte(color.b + (255 - color.b) * amount),
            255);
    }

    private static Color32 Darken(Color32 color, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color32(
            ClampByte(color.r * (1f - amount)),
            ClampByte(color.g * (1f - amount)),
            ClampByte(color.b * (1f - amount)),
            255);
    }

    private static byte ClampByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
    }

    private static float Luma(Color32 color)
    {
        return color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
    }

    private static bool IsBrightMaterial(string material)
    {
        return material is "metal" or "rim" or "trim" or "gold" or "edge" or "fold" or "wrap" or "pommel";
    }

    private static bool IsGlowMaterial(string material)
    {
        return material is "glass" or "fire" or "core" or "glint";
    }

    private static void ClearTransparentPixels(Color32[] pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0) pixels[i] = new Color32(0, 0, 0, 0);
        }
    }

    private static Color32[] ToUnityPixels(Color32[] source, int size)
    {
        var result = new Color32[source.Length];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                result[(size - 1 - y) * size + x] = source[y * size + x];
            }
        }
        return result;
    }

    private static int StableHash(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));
        var value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        return unchecked((int)value);
    }

    private enum AppearanceOutput
    {
        Icon,
        World,
    }

    private readonly struct Projection
    {
        public Projection(Vector3 target, Vector3 rotation, float scale)
        {
            Target = target;
            Rotation = rotation;
            Scale = scale;
        }

        public readonly Vector3 Target;
        public readonly Vector3 Rotation;
        public readonly float Scale;
    }

    private readonly struct Face3D
    {
        public Face3D(Vector3[] points, string slot, string material)
        {
            Points = points;
            Slot = slot;
            Material = material;
        }

        public readonly Vector3[] Points;
        public readonly string Slot;
        public readonly string Material;
    }

    private readonly struct ProjectedFace
    {
        public ProjectedFace(Vector3[] points, string material, Color32 color)
        {
            Points = points;
            Material = material;
            Color = color;
        }

        public readonly Vector3[] Points;
        public readonly string Material;
        public readonly Color32 Color;
    }
}

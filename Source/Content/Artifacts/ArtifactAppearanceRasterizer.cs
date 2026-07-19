using System;
using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

internal enum ArtifactAppearanceRenderKind
{
    Icon,
    WorldIdle,
    WorldActive,
}

internal readonly struct ArtifactAppearanceProjection
{
    public readonly int Size;
    public readonly Vector3 Target;
    public readonly Vector3 Rotation;
    public readonly float Scale;
    public readonly Vector3 Light;

    public ArtifactAppearanceProjection(
        int size,
        Vector3 target,
        Vector3 rotation,
        float scale,
        Vector3 light)
    {
        Size = size;
        Target = target;
        Rotation = rotation;
        Scale = scale;
        Light = light;
    }
}

internal sealed class ArtifactAppearancePixelFrame
{
    public readonly int Size;
    public readonly Color32[] Body;
    public readonly Color32[] Emission;
    public readonly Color32[] Shadow;
    public readonly Color32[] Composite;
    public readonly ArtifactAppearanceProjection Projection;

    public ArtifactAppearancePixelFrame(
        int size,
        Color32[] body,
        Color32[] emission,
        Color32[] shadow,
        Color32[] composite,
        ArtifactAppearanceProjection projection)
    {
        Size = size;
        Body = body;
        Emission = emission;
        Shadow = shadow;
        Composite = composite;
        Projection = projection;
    }
}

/// <summary>将组合模型烘焙为主体、发光和阴影三个确定性像素层。</summary>
internal static class ArtifactAppearanceRasterizer
{
    private const float EmptyDepth = -1e9f;

    internal static ArtifactAppearancePixelFrame Render(
        ArtifactAppearanceMesh mesh,
        ArtifactAppearance appearance,
        ArtifactAppearanceTemplateDef template,
        ArtifactAppearanceCatalog catalog,
        ArtifactAppearanceRenderKind kind)
    {
        ArtifactAppearanceProjection projection = ResolveProjection(mesh, template, catalog.Canvas, kind);
        int sampleScale = kind switch
        {
            ArtifactAppearanceRenderKind.Icon => 2,
            ArtifactAppearanceRenderKind.WorldActive => 2,
            _ => 1,
        };
        List<BakeLayer> layers = BakeLayers(mesh, catalog, projection, sampleScale);
        return Compose(layers, appearance, catalog, projection, kind, sampleScale);
    }

    internal static ArtifactAppearanceProjection ResolveProjection(
        ArtifactAppearanceMesh mesh,
        ArtifactAppearanceTemplateDef template,
        int baseCanvas,
        ArtifactAppearanceRenderKind kind)
    {
        ArtifactAppearanceViewDef view = kind == ArtifactAppearanceRenderKind.Icon
            ? template.GetView("world_active")
            : template.GetView(ViewKey(kind));
        int defaultSize = kind switch
        {
            ArtifactAppearanceRenderKind.Icon => 56,
            ArtifactAppearanceRenderKind.WorldIdle => 24,
            ArtifactAppearanceRenderKind.WorldActive => 56,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        if (view == null)
        {
            return kind == ArtifactAppearanceRenderKind.Icon
                ? ResolveFixedProjection(
                    mesh,
                    template.Camera,
                    template.Light,
                    defaultSize,
                    baseCanvas)
                : ResolveAutoProjection(mesh, new JObject(), template.Light, defaultSize, kind == ArtifactAppearanceRenderKind.WorldIdle ? 2 : 3);
        }

        int size = view.Size > 0 ? view.Size : defaultSize;
        JObject light = view.Light.Count > 0 ? view.Light : template.Light;
        return view.AutoFrame
            ? ResolveAutoProjection(mesh, view.Camera, light, size, view.Margin)
            : ResolveFixedProjection(mesh, view.Camera, light, size, size);
    }

    private static ArtifactAppearanceProjection ResolveFixedProjection(
        ArtifactAppearanceMesh mesh,
        JObject camera,
        JObject light,
        int size,
        int baseCanvas)
    {
        Vector3 rotation = CameraRotation(camera);
        Vector3 target = ArtifactAppearanceMath.ReadVec3(camera["target"], (mesh.Min + mesh.Max) * 0.5f);
        float scale = ArtifactAppearanceMath.ReadFloat(camera, "scale", 8f) * size / Mathf.Max(1, baseCanvas);
        return new ArtifactAppearanceProjection(size, target, rotation, scale, LightDirection(light));
    }

    private static ArtifactAppearanceProjection ResolveAutoProjection(
        ArtifactAppearanceMesh mesh,
        JObject camera,
        JObject light,
        int size,
        int margin)
    {
        Vector3 rotation = CameraRotation(camera);
        Vector3 target = (mesh.Min + mesh.Max) * 0.5f;
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        for (int faceIndex = 0; faceIndex < mesh.Faces.Length; faceIndex++)
        {
            Vector3[] points = mesh.Faces[faceIndex].Points;
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                Vector3 point = ArtifactAppearanceMath.RotateEuler(points[pointIndex] - target, rotation);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }
        }
        float extent = Mathf.Max(maxX - minX, maxY - minY, 0.001f);
        float available = Mathf.Max(1f, size - Mathf.Max(0, margin) * 2f - 1f);
        return new ArtifactAppearanceProjection(size, target, rotation, available / extent, LightDirection(light));
    }

    private static List<BakeLayer> BakeLayers(
        ArtifactAppearanceMesh mesh,
        ArtifactAppearanceCatalog catalog,
        ArtifactAppearanceProjection projection,
        int sampleScale)
    {
        List<BakeLayer> layers = new();
        Dictionary<string, BakeLayer> bySlot = new(StringComparer.Ordinal);
        for (int faceIndex = 0; faceIndex < mesh.Faces.Length; faceIndex++)
        {
            ArtifactAppearanceMeshFace face = mesh.Faces[faceIndex];
            if (!bySlot.TryGetValue(face.Slot, out BakeLayer layer))
            {
                layer = new BakeLayer(projection.Size * sampleScale, face.Slot, face.Order);
                bySlot.Add(face.Slot, layer);
                layers.Add(layer);
            }
            RasterFace(face, layer, catalog, projection, sampleScale);
        }
        layers.Sort((left, right) => left.Order.CompareTo(right.Order));
        return layers;
    }

    private static void RasterFace(
        ArtifactAppearanceMeshFace face,
        BakeLayer layer,
        ArtifactAppearanceCatalog catalog,
        ArtifactAppearanceProjection projection,
        int sampleScale)
    {
        Vector3[] cameraPoints = new Vector3[face.Points.Length];
        Vector3[] worldNormals = new Vector3[face.Points.Length];
        Vector3[] cameraNormals = new Vector3[face.Points.Length];
        Vector3 fallbackNormal = ArtifactAppearanceMath.FaceNormal(face.Points);
        for (int i = 0; i < cameraPoints.Length; i++)
        {
            cameraPoints[i] = ArtifactAppearanceMath.RotateEuler(
                face.Points[i] - projection.Target,
                projection.Rotation);
            worldNormals[i] = face.Normals.Length == face.Points.Length
                ? face.Normals[i]
                : fallbackNormal;
            cameraNormals[i] = ArtifactAppearanceMath.Normalize(
                ArtifactAppearanceMath.RotateEuler(worldNormals[i], projection.Rotation),
                Vector3.forward);
        }
        if (ArtifactAppearanceMath.FaceNormal(cameraPoints).z < 0f)
        {
            for (int i = 0; i < worldNormals.Length; i++)
            {
                worldNormals[i] = -worldNormals[i];
                cameraNormals[i] = -cameraNormals[i];
            }
        }
        ArtifactAppearanceSurfaceStyleDef style = ResolveSurface(catalog, face.Surface);
        byte emission = (byte)Mathf.Clamp(Mathf.RoundToInt(style.EmissionLayer * 3f), 0, 3);
        Vector3 cameraLight = ArtifactAppearanceMath.Normalize(
            ArtifactAppearanceMath.RotateEuler(projection.Light, projection.Rotation),
            Vector3.forward);
        Vector3 cameraHalf = ArtifactAppearanceMath.Normalize(cameraLight + Vector3.forward, Vector3.forward);
        Vector3[] projected = new Vector3[cameraPoints.Length];
        float center = (layer.Size - 1) * 0.5f;
        for (int i = 0; i < projected.Length; i++)
        {
            projected[i] = new Vector3(
                center + cameraPoints[i].x * projection.Scale * sampleScale,
                center - cameraPoints[i].y * projection.Scale * sampleScale,
                cameraPoints[i].z);
        }
        for (int i = 1; i < projected.Length - 1; i++)
        {
            RasterTriangle(
                projected[0],
                projected[i],
                projected[i + 1],
                face.Points[0],
                face.Points[i],
                face.Points[i + 1],
                worldNormals[0],
                worldNormals[i],
                worldNormals[i + 1],
                cameraNormals[0],
                cameraNormals[i],
                cameraNormals[i + 1],
                projection.Light,
                cameraHalf,
                style,
                face.Material,
                face.Surface,
                face.ObjectName,
                emission,
                layer);
        }
    }

    private static float ResolveLight(
        Vector3 normalWorld,
        Vector3 normalCamera,
        Vector3 light,
        Vector3 cameraHalf,
        ArtifactAppearanceSurfaceStyleDef style)
    {
        normalWorld = ArtifactAppearanceMath.Normalize(normalWorld, Vector3.forward);
        normalCamera = ArtifactAppearanceMath.Normalize(normalCamera, Vector3.forward);
        float diffuse = Mathf.Max(0f, Vector3.Dot(normalWorld, light));
        float sideShadow = Mathf.Max(0f, -normalWorld.x * 0.18f - normalWorld.z * 0.10f);
        float facing = Mathf.Clamp01(normalCamera.z);
        float specular = Mathf.Pow(
            Mathf.Max(0f, Vector3.Dot(normalCamera, cameraHalf)),
            Mathf.Max(1f, style.SpecularPower)) * style.Specular;
        float rim = Mathf.Pow(1f - facing, 2f) * style.RimLight;
        float amount = 0.24f + diffuse * style.Diffuse - sideShadow * style.SideShadow * 0.18f +
                       style.Brightness + specular + rim;
        if (style.EmissionLayer > 0f) amount = Mathf.Max(amount, 0.74f);
        return Mathf.Clamp01(amount);
    }

    private static void RasterTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 positionA,
        Vector3 positionB,
        Vector3 positionC,
        Vector3 normalWorldA,
        Vector3 normalWorldB,
        Vector3 normalWorldC,
        Vector3 normalCameraA,
        Vector3 normalCameraB,
        Vector3 normalCameraC,
        Vector3 light,
        Vector3 cameraHalf,
        ArtifactAppearanceSurfaceStyleDef style,
        string material,
        string surface,
        string objectName,
        byte emission,
        BakeLayer layer)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int maxX = Mathf.Min(layer.Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int maxY = Mathf.Min(layer.Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
        float area = Edge(a, b, c);
        if (Mathf.Abs(area) < 1e-7f) return;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3 sample = new(x + 0.5f, y + 0.5f, 0f);
                float w0 = Edge(b, c, sample) / area;
                float w1 = Edge(c, a, sample) / area;
                float w2 = Edge(a, b, sample) / area;
                if (w0 < -1e-6f || w1 < -1e-6f || w2 < -1e-6f) continue;
                float depth = a.z * w0 + b.z * w1 + c.z * w2;
                int index = y * layer.Size + x;
                if (depth <= layer.Depth[index]) continue;
                Vector3 normalWorld = ArtifactAppearanceMath.Normalize(
                    normalWorldA * w0 + normalWorldB * w1 + normalWorldC * w2,
                    Vector3.forward);
                Vector3 normalCamera = ArtifactAppearanceMath.Normalize(
                    normalCameraA * w0 + normalCameraB * w1 + normalCameraC * w2,
                    Vector3.forward);
                Vector3 position = positionA * w0 + positionB * w1 + positionC * w2;
                layer.Depth[index] = depth;
                layer.Materials[index] = material;
                layer.Surfaces[index] = surface;
                layer.Objects[index] = objectName;
                layer.Positions[index] = position;
                layer.Lights[index] = ResolveLight(normalWorld, normalCamera, light, cameraHalf, style);
                layer.Emissions[index] = emission;
            }
        }
    }

    private static ArtifactAppearancePixelFrame Compose(
        IReadOnlyList<BakeLayer> layers,
        ArtifactAppearance appearance,
        ArtifactAppearanceCatalog catalog,
        ArtifactAppearanceProjection projection,
        ArtifactAppearanceRenderKind kind,
        int sampleScale)
    {
        SemanticBuffer semantics = MergeLayers(layers);
        if (sampleScale > 1) semantics = DownsampleSemantics(semantics, sampleScale, catalog);
        int size = semantics.Size;
        ApplySurfacePatterns(semantics, catalog);
        byte[] shades = QuantizeLights(semantics, catalog);

        ApplyDepthAccents(semantics.Materials, semantics.Objects, shades, semantics.Depth, size);
        ApplyRimHighlights(semantics.Materials, semantics.Surfaces, shades, catalog, size);
        Color32[] body = Colorize(
            semantics.Slots,
            semantics.Materials,
            semantics.Surfaces,
            shades,
            appearance,
            catalog);
        AddOuterOutline(body, semantics.Materials, size);
        Color32[] emission = BuildEmission(
            semantics.Slots,
            semantics.Materials,
            semantics.Surfaces,
            shades,
            semantics.Emissions,
            appearance,
            catalog,
            size);
        Color32[] shadow = kind == ArtifactAppearanceRenderKind.Icon
            ? new Color32[size * size]
            : BuildShadow(semantics.Materials, size);
        Color32[] composite = Composite(body, emission);
        return new ArtifactAppearancePixelFrame(size, body, emission, shadow, composite, projection);
    }

    private static SemanticBuffer MergeLayers(IReadOnlyList<BakeLayer> layers)
    {
        SemanticBuffer output = new(layers[0].Size);
        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            BakeLayer layer = layers[layerIndex];
            for (int index = 0; index < output.Materials.Length; index++)
            {
                if (layer.Materials[index] == null) continue;
                float candidate = layer.Depth[index];
                bool nearer = candidate > output.Depth[index] + 1e-7f;
                bool orderedTie = Mathf.Abs(candidate - output.Depth[index]) <= 1e-7f &&
                                  layer.Order >= output.Orders[index];
                if (!nearer && !orderedTie) continue;
                output.Depth[index] = candidate;
                output.Slots[index] = layer.Slot;
                output.Materials[index] = layer.Materials[index];
                output.Surfaces[index] = layer.Surfaces[index];
                output.Objects[index] = layer.Objects[index];
                output.Positions[index] = layer.Positions[index];
                output.Lights[index] = layer.Lights[index];
                output.Emissions[index] = layer.Emissions[index];
                output.Orders[index] = layer.Order;
            }
        }
        return output;
    }

    private static SemanticBuffer DownsampleSemantics(
        SemanticBuffer source,
        int sampleScale,
        ArtifactAppearanceCatalog catalog)
    {
        int size = source.Size / sampleScale;
        SemanticBuffer output = new(size);
        int sampleCount = sampleScale * sampleScale;
        Dictionary<SemanticKey, SampleGroup> groups = new();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                groups.Clear();
                int occupied = 0;
                for (int sampleY = 0; sampleY < sampleScale; sampleY++)
                {
                    for (int sampleX = 0; sampleX < sampleScale; sampleX++)
                    {
                        int sourceIndex = (y * sampleScale + sampleY) * source.Size +
                                          x * sampleScale + sampleX;
                        if (source.Materials[sourceIndex] == null) continue;
                        occupied++;
                        SemanticKey key = new(
                            source.Slots[sourceIndex],
                            source.Materials[sourceIndex],
                            source.Surfaces[sourceIndex],
                            source.Objects[sourceIndex],
                            source.Orders[sourceIndex]);
                        if (!groups.TryGetValue(key, out SampleGroup group))
                        {
                            group = new SampleGroup(key);
                            groups.Add(key, group);
                        }
                        group.Add(
                            source.Depth[sourceIndex],
                            source.Positions[sourceIndex],
                            source.Lights[sourceIndex],
                            source.Emissions[sourceIndex]);
                    }
                }
                if (occupied == 0) continue;

                SampleGroup selected = null;
                foreach (SampleGroup group in groups.Values)
                {
                    if (selected == null || group.IsPreferredTo(selected)) selected = group;
                }
                float coverage = occupied / (float)sampleCount;
                bool preserveDetail = selected.MaxEmission > 0 ||
                                      ResolveSurface(catalog, selected.Key.Surface).PreserveDetail;
                if (!preserveDetail && coverage < CoverageThreshold(x, y)) continue;

                int target = y * size + x;
                output.Depth[target] = selected.MaxDepth;
                output.Slots[target] = selected.Key.Slot;
                output.Materials[target] = selected.Key.Material;
                output.Surfaces[target] = selected.Key.Surface;
                output.Objects[target] = selected.Key.ObjectName;
                output.Positions[target] = selected.PositionSum / selected.Count;
                output.Lights[target] = selected.LightSum / selected.Count;
                output.Emissions[target] = selected.MaxEmission;
                output.Orders[target] = selected.Key.Order;
            }
        }
        return output;
    }

    private static float CoverageThreshold(int x, int y)
    {
        return (x & 1, y & 1) switch
        {
            (0, 0) => 0.125f,
            (1, 0) => 0.625f,
            (0, 1) => 0.875f,
            _ => 0.375f,
        };
    }

    private static void ApplySurfacePatterns(
        SemanticBuffer semantics,
        ArtifactAppearanceCatalog catalog)
    {
        int size = semantics.Size;
        for (int index = 0; index < semantics.Materials.Length; index++)
        {
            if (semantics.Materials[index] == null) continue;
            ArtifactAppearanceSurfaceStyleDef style = ResolveSurface(catalog, semantics.Surfaces[index]);
            if (string.IsNullOrEmpty(style.Pattern) || style.PatternStrength <= 0f || size < style.PatternMinSize)
                continue;
            uint hash = StableHash(semantics.Objects[index] ?? "mesh");
            float phase = (hash & 0xffff) / 65535f * Mathf.PI * 2f;
            float signal = SurfacePattern(style.Pattern, semantics.Positions[index], style.PatternScale, phase);
            semantics.Lights[index] = Mathf.Clamp01(
                semantics.Lights[index] + signal * style.PatternStrength);
        }
    }

    private static float SurfacePattern(string pattern, Vector3 position, float scale, float phase)
    {
        float x = position.x;
        float y = position.y;
        float z = position.z;
        scale = Mathf.Max(0.1f, scale);
        float tau = Mathf.PI * 2f;
        switch (pattern)
        {
            case "brushed":
            {
                float primary = Mathf.Sin((y * 1.7f + x * 0.35f - z * 0.25f) * tau * scale + phase);
                float envelope = Mathf.Sin((y * 0.41f + z * 0.23f) * tau * scale + phase * 0.37f);
                return primary * (0.72f + envelope * 0.28f);
            }
            case "patina":
            {
                float first = Mathf.Sin((x * 0.75f + z * 0.55f) * tau * scale + phase);
                float second = Mathf.Cos((y * 0.85f - z * 0.35f) * tau * scale * 0.73f + phase * 0.61f);
                float third = Mathf.Sin((x - y + z) * tau * scale * 0.31f - phase * 0.47f);
                float field = (first + second + third) / 3f;
                return -Mathf.Max(0f, field) + Mathf.Min(0f, field) * 0.20f;
            }
            case "jade_cloud":
            {
                float bend = Mathf.Sin(y * Mathf.PI * scale + phase) * 1.35f;
                return Mathf.Sin((x + z * 0.65f) * tau * scale + bend + phase * 0.43f) * 0.82f;
            }
            case "crystal_facet":
            {
                float first = Mathf.Sin((x + y * 0.72f) * tau * scale + phase);
                float second = Mathf.Sin((z - y * 0.58f) * tau * scale * 0.83f - phase * 0.70f);
                return Mathf.Max(first, second) * 0.72f + Mathf.Min(first, second) * 0.18f;
            }
            case "silk_weave":
            {
                float first = Mathf.Sin((x + y) * tau * scale + phase);
                float second = Mathf.Sin((x - y) * tau * scale - phase);
                return (first + second) * 0.34f;
            }
            case "stone_mottle":
            {
                float first = Mathf.Sin((x * 0.82f + z * 0.61f) * tau * scale + phase);
                float second = Mathf.Cos((y * 0.77f - x * 0.29f) * tau * scale * 0.79f - phase * 0.52f);
                return first * second * 0.78f;
            }
            case "wood_grain":
            {
                float radial = Mathf.Sqrt(x * x + z * z);
                float bend = Mathf.Sin(y * tau * scale * 0.33f + phase) * 0.55f;
                return Mathf.Sin((radial * 1.8f + y * 0.36f) * tau * scale + bend + phase) * 0.78f;
            }
            case "bone_grain":
            {
                float first = Mathf.Sin((y * 1.15f + x * 0.26f) * tau * scale + phase);
                float second = Mathf.Sin((z + y * 0.19f) * tau * scale * 0.37f - phase);
                return first * 0.52f + second * 0.22f;
            }
            default:
                return 0f;
        }
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint result = 2166136261;
            for (int index = 0; index < value.Length; index++)
            {
                result ^= value[index];
                result *= 16777619;
            }
            return result;
        }
    }

    private static byte[] QuantizeLights(SemanticBuffer semantics, ArtifactAppearanceCatalog catalog)
    {
        byte[] shades = new byte[semantics.Materials.Length];
        for (int index = 0; index < shades.Length; index++)
        {
            if (semantics.Materials[index] == null) continue;
            ArtifactAppearanceSurfaceStyleDef style = ResolveSurface(catalog, semantics.Surfaces[index]);
            float[] thresholds = style.ShadeThresholds;
            if (thresholds == null || thresholds.Length != 5)
                thresholds = [0.10f, 0.28f, 0.47f, 0.67f, 0.87f];
            byte shade = 0;
            float amount = Mathf.Clamp01(semantics.Lights[index]);
            for (int thresholdIndex = 0; thresholdIndex < thresholds.Length; thresholdIndex++)
            {
                if (amount >= thresholds[thresholdIndex]) shade++;
            }
            shades[index] = shade;
        }
        return shades;
    }

    private static void ApplyDepthAccents(
        string[] materials,
        string[] objects,
        byte[] shades,
        float[] depth,
        int size)
    {
        byte[] source = (byte[])shades.Clone();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                if (materials[index] == null) continue;
                byte shade = source[index];
                for (int direction = 0; direction < 4; direction++)
                {
                    int dx = direction switch { 0 => -1, 2 => 1, _ => 0 };
                    int dy = direction switch { 1 => -1, 3 => 1, _ => 0 };
                    int otherX = x + dx;
                    int otherY = y + dy;
                    if (otherX < 0 || otherY < 0 || otherX >= size || otherY >= size) continue;
                    int other = otherY * size + otherX;
                    if (materials[other] == null) continue;
                    float delta = depth[other] - depth[index];
                    if (delta > 0.13f ||
                        (objects[other] != objects[index] && (dx > 0 || dy > 0) && delta > -0.02f))
                    {
                        shade = (byte)Mathf.Max(0, source[index] - 1);
                    }
                }
                shades[index] = shade;
            }
        }
    }

    private static void ApplyRimHighlights(
        string[] materials,
        string[] surfaces,
        byte[] shades,
        ArtifactAppearanceCatalog catalog,
        int size)
    {
        byte[] source = (byte[])shades.Clone();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                if (materials[index] == null || !ResolveSurface(catalog, surfaces[index]).PixelRim) continue;
                bool exposed = x == 0 || materials[index - 1] == null || y == 0 || materials[index - size] == null;
                if (exposed) shades[index] = (byte)Mathf.Min(5, source[index] + 1);
            }
        }
    }

    private static Color32[] Colorize(
        string[] slots,
        string[] materials,
        string[] surfaces,
        byte[] shades,
        ArtifactAppearance appearance,
        ArtifactAppearanceCatalog catalog)
    {
        Color32[] pixels = new Color32[materials.Length];
        Dictionary<string, Color32[]> ramps = new(StringComparer.Ordinal);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (materials[i] == null) continue;
            string key = $"{slots[i]}\n{materials[i]}\n{surfaces[i]}";
            if (!ramps.TryGetValue(key, out Color32[] ramp))
            {
                ramp = ResolveMaterialRamp(appearance, catalog, slots[i], materials[i]);
                ramps.Add(key, ramp);
            }
            pixels[i] = ramp[shades[i]];
        }
        return pixels;
    }

    private static void AddOuterOutline(Color32[] pixels, string[] materials, int size)
    {
        Color32[] source = (Color32[])pixels.Clone();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                if (materials[index] != null) continue;
                if (TryFindOutlineSource(source, materials, size, x, y, false, out Color32 color, out _))
                {
                    pixels[index] = Darken(color, 0.58f);
                    continue;
                }
                if (TryFindOutlineSource(source, materials, size, x, y, true, out color, out int count) &&
                    count >= 2)
                {
                    pixels[index] = Darken(color, 0.58f);
                }
            }
        }
    }

    private static bool TryFindOutlineSource(
        Color32[] pixels,
        string[] materials,
        int size,
        int x,
        int y,
        bool diagonal,
        out Color32 darkest,
        out int count)
    {
        darkest = default;
        count = 0;
        float darkestLuma = float.MaxValue;
        for (int direction = 0; direction < 4; direction++)
        {
            int dx = diagonal
                ? direction is 0 or 2 ? -1 : 1
                : direction switch { 0 => -1, 2 => 1, _ => 0 };
            int dy = diagonal
                ? direction < 2 ? -1 : 1
                : direction switch { 1 => -1, 3 => 1, _ => 0 };
            int otherX = x + dx;
            int otherY = y + dy;
            if (otherX < 0 || otherY < 0 || otherX >= size || otherY >= size) continue;
            int other = otherY * size + otherX;
            if (materials[other] == null) continue;
            count++;
            Color32 candidate = pixels[other];
            float luma = Luma(candidate);
            if (luma >= darkestLuma) continue;
            darkest = candidate;
            darkestLuma = luma;
        }
        return count > 0;
    }

    private static Color32[] BuildEmission(
        string[] slots,
        string[] materials,
        string[] surfaces,
        byte[] shades,
        byte[] strengths,
        ArtifactAppearance appearance,
        ArtifactAppearanceCatalog catalog,
        int size)
    {
        Color32[] pixels = new Color32[materials.Length];
        for (int index = 0; index < materials.Length; index++)
        {
            if (materials[index] == null || strengths[index] == 0) continue;
            Color32[] ramp = ResolveMaterialRamp(appearance, catalog, slots[index], materials[index]);
            Color32 color = ramp[Mathf.Max(4, shades[index])];
            color.a = (byte)Mathf.Min(220, 70 + strengths[index] * 45);
            pixels[index] = color;
        }
        Color32[] sources = (Color32[])pixels.Clone();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color32 source = sources[y * size + x];
                if (source.a == 0) continue;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int xx = x + dx;
                        int yy = y + dy;
                        if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        int target = yy * size + xx;
                        byte alpha = (byte)(source.a * 0.16f / distance);
                        if (alpha <= pixels[target].a) continue;
                        pixels[target] = new Color32(source.r, source.g, source.b, alpha);
                    }
                }
            }
        }
        return pixels;
    }

    private static Color32[] BuildShadow(string[] materials, int size)
    {
        int minX = size;
        int maxX = -1;
        int maxY = -1;
        for (int index = 0; index < materials.Length; index++)
        {
            if (materials[index] == null) continue;
            int x = index % size;
            int y = index / size;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }
        Color32[] pixels = new Color32[materials.Length];
        if (maxX < minX) return pixels;
        float centerX = (minX + maxX) * 0.5f;
        float radiusX = Mathf.Max(2f, (maxX - minX + 1) * 0.30f);
        float centerY = Mathf.Min(size - 2f, maxY + 0.65f);
        float radiusY = Mathf.Max(1f, radiusX * 0.20f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Pow((x - centerX) / radiusX, 2f) +
                                 Mathf.Pow((y - centerY) / radiusY, 2f);
                if (distance > 1f) continue;
                pixels[y * size + x] = new Color32(12, 15, 18, (byte)((1f - distance) * 58f));
            }
        }
        return pixels;
    }

    private static Color32[] Composite(Color32[] body, Color32[] emission)
    {
        Color32[] pixels = new Color32[body.Length];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = AlphaOver(body[i], emission[i]);
        return pixels;
    }

    private static Color32 AlphaOver(Color32 destination, Color32 source)
    {
        if (source.a == 0) return destination;
        if (destination.a == 0) return source;
        float sourceAlpha = source.a / 255f;
        float destinationAlpha = destination.a / 255f;
        float alpha = sourceAlpha + destinationAlpha * (1f - sourceAlpha);
        return new Color32(
            ClampByte((source.r * sourceAlpha + destination.r * destinationAlpha * (1f - sourceAlpha)) / alpha),
            ClampByte((source.g * sourceAlpha + destination.g * destinationAlpha * (1f - sourceAlpha)) / alpha),
            ClampByte((source.b * sourceAlpha + destination.b * destinationAlpha * (1f - sourceAlpha)) / alpha),
            ClampByte(alpha * 255f));
    }

    private static Color32[] ResolveMaterialRamp(
        ArtifactAppearance appearance,
        ArtifactAppearanceCatalog catalog,
        string slot,
        string material)
    {
        for (int partIndex = 0; partIndex < appearance.parts.Length; partIndex++)
        {
            ArtifactAppearancePart part = appearance.parts[partIndex];
            if (part.slot != slot) continue;
            catalog.TryGetColorRole(material, out ArtifactAppearanceColorRoleDef role);
            ArtifactAppearanceColorSchemeDef scheme = ResolveColorScheme(appearance, part, catalog, role);
            ArtifactAppearanceColor[] colors = part.colors ?? [];
            for (int colorIndex = 0; colorIndex < colors.Length; colorIndex++)
            {
                if (colors[colorIndex].material == material)
                {
                    Color32 color = ParseColor(
                        colors[colorIndex].color_hex,
                        new Color32(154, 160, 168, 255));
                    return CreateRamp(color, scheme?.VisualTheme);
                }
            }
            if (scheme != null &&
                (scheme.Colors.TryGetValue(material, out string hex) ||
                 role != null && scheme.Colors.TryGetValue(role.FallbackChannel, out hex)))
            {
                Color32 color = ParseColor(hex, new Color32(154, 160, 168, 255));
                return CreateRamp(color, scheme.VisualTheme);
            }
            break;
        }
        return CreateRamp(new Color32(154, 160, 168, 255), null);
    }

    private static ArtifactAppearanceColorSchemeDef ResolveColorScheme(
        ArtifactAppearance appearance,
        ArtifactAppearancePart part,
        ArtifactAppearanceCatalog catalog,
        ArtifactAppearanceColorRoleDef role)
    {
        ArtifactAppearanceColorRole[] selections = appearance.color_roles ?? [];
        if (role != null)
        {
            for (int i = 0; i < selections.Length; i++)
            {
                if (selections[i].role == role.Key &&
                    catalog.ColorSchemes.TryGetValue(
                        selections[i].color_scheme,
                        out ArtifactAppearanceColorSchemeDef selected))
                {
                    return selected;
                }
            }
        }
        return !string.IsNullOrEmpty(part.color_scheme) &&
               catalog.ColorSchemes.TryGetValue(part.color_scheme, out ArtifactAppearanceColorSchemeDef fallback)
            ? fallback
            : null;
    }

    private static ArtifactAppearanceSurfaceStyleDef ResolveSurface(
        ArtifactAppearanceCatalog catalog,
        string key)
    {
        if (!string.IsNullOrEmpty(key) && catalog.SurfaceStyles.TryGetValue(key, out ArtifactAppearanceSurfaceStyleDef style))
            return style;
        return catalog.SurfaceStyles.TryGetValue("neutral", out style)
            ? style
            : new ArtifactAppearanceSurfaceStyleDef { Key = "neutral" };
    }

    private static Color32[] CreateRamp(Color32 color, ArtifactAppearanceVisualThemeDef theme)
    {
        Color32 secondary = ParseColor(theme?.Secondary, color);
        Color32 glow = ParseColor(theme?.Glow, new Color32(255, 255, 255, 255));
        return
        [
            Mix(Darken(color, 0.80f), Darken(secondary, 0.72f), 0.34f),
            Mix(Darken(color, 0.58f), Darken(secondary, 0.50f), 0.25f),
            Mix(Darken(color, 0.30f), Darken(secondary, 0.30f), 0.12f),
            color,
            Mix(color, glow, 0.38f),
            Mix(color, glow, 0.72f),
        ];
    }

    private static Color32 ParseColor(string hex, Color32 fallback)
    {
        return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color color)
            ? (Color32)color
            : fallback;
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

    private static Color32 Mix(Color32 left, Color32 right, float amount)
    {
        amount = Mathf.Clamp01(amount);
        return new Color32(
            ClampByte(Mathf.Lerp(left.r, right.r, amount)),
            ClampByte(Mathf.Lerp(left.g, right.g, amount)),
            ClampByte(Mathf.Lerp(left.b, right.b, amount)),
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

    private static string ViewKey(ArtifactAppearanceRenderKind kind)
    {
        return kind switch
        {
            ArtifactAppearanceRenderKind.Icon => "icon",
            ArtifactAppearanceRenderKind.WorldIdle => "world_idle",
            ArtifactAppearanceRenderKind.WorldActive => "world_active",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    private static Vector3 CameraRotation(JObject camera)
    {
        return new Vector3(
            -ArtifactAppearanceMath.ReadFloat(camera, "pitch", 0f),
            -ArtifactAppearanceMath.ReadFloat(camera, "yaw", 0f),
            -ArtifactAppearanceMath.ReadFloat(camera, "roll", 0f));
    }

    private static Vector3 LightDirection(JObject light)
    {
        float yaw = ArtifactAppearanceMath.ReadFloat(light, "yaw", -35f) * Mathf.Deg2Rad;
        float pitch = ArtifactAppearanceMath.ReadFloat(light, "pitch", 55f) * Mathf.Deg2Rad;
        return ArtifactAppearanceMath.Normalize(
            new Vector3(
                Mathf.Cos(pitch) * Mathf.Sin(yaw),
                Mathf.Sin(pitch),
                Mathf.Cos(pitch) * Mathf.Cos(yaw)),
            Vector3.forward);
    }

    private static float Edge(Vector3 a, Vector3 b, Vector3 c)
    {
        return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
    }

    private sealed class BakeLayer
    {
        internal readonly int Size;
        internal readonly string Slot;
        internal readonly int Order;
        internal readonly float[] Depth;
        internal readonly string[] Materials;
        internal readonly string[] Surfaces;
        internal readonly string[] Objects;
        internal readonly Vector3[] Positions;
        internal readonly float[] Lights;
        internal readonly byte[] Emissions;

        internal BakeLayer(int size, string slot, int order)
        {
            Size = size;
            Slot = slot;
            Order = order;
            int count = size * size;
            Depth = new float[count];
            Materials = new string[count];
            Surfaces = new string[count];
            Objects = new string[count];
            Positions = new Vector3[count];
            Lights = new float[count];
            Emissions = new byte[count];
            for (int i = 0; i < count; i++) Depth[i] = EmptyDepth;
        }
    }

    private sealed class SemanticBuffer
    {
        internal readonly int Size;
        internal readonly float[] Depth;
        internal readonly string[] Slots;
        internal readonly string[] Materials;
        internal readonly string[] Surfaces;
        internal readonly string[] Objects;
        internal readonly Vector3[] Positions;
        internal readonly float[] Lights;
        internal readonly byte[] Emissions;
        internal readonly int[] Orders;

        internal SemanticBuffer(int size)
        {
            Size = size;
            int count = size * size;
            Depth = new float[count];
            Slots = new string[count];
            Materials = new string[count];
            Surfaces = new string[count];
            Objects = new string[count];
            Positions = new Vector3[count];
            Lights = new float[count];
            Emissions = new byte[count];
            Orders = new int[count];
            for (int i = 0; i < count; i++)
            {
                Depth[i] = EmptyDepth;
                Orders[i] = int.MinValue;
            }
        }
    }

    private readonly struct SemanticKey : IEquatable<SemanticKey>
    {
        internal readonly string Slot;
        internal readonly string Material;
        internal readonly string Surface;
        internal readonly string ObjectName;
        internal readonly int Order;

        internal SemanticKey(string slot, string material, string surface, string objectName, int order)
        {
            Slot = slot;
            Material = material;
            Surface = surface;
            ObjectName = objectName;
            Order = order;
        }

        public bool Equals(SemanticKey other)
        {
            return Slot == other.Slot && Material == other.Material && Surface == other.Surface &&
                   ObjectName == other.ObjectName && Order == other.Order;
        }

        public override bool Equals(object obj)
        {
            return obj is SemanticKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Slot?.GetHashCode() ?? 0;
                hash = hash * 397 ^ (Material?.GetHashCode() ?? 0);
                hash = hash * 397 ^ (Surface?.GetHashCode() ?? 0);
                hash = hash * 397 ^ (ObjectName?.GetHashCode() ?? 0);
                return hash * 397 ^ Order;
            }
        }
    }

    private sealed class SampleGroup
    {
        internal readonly SemanticKey Key;
        internal int Count;
        internal float LightSum;
        internal Vector3 PositionSum;
        internal float MaxDepth = EmptyDepth;
        internal byte MaxEmission;

        internal SampleGroup(SemanticKey key)
        {
            Key = key;
        }

        internal void Add(float depth, Vector3 position, float light, byte emission)
        {
            Count++;
            PositionSum += position;
            LightSum += light;
            MaxDepth = Mathf.Max(MaxDepth, depth);
            MaxEmission = (byte)Mathf.Max(MaxEmission, emission);
        }

        internal bool IsPreferredTo(SampleGroup other)
        {
            if (Count != other.Count) return Count > other.Count;
            if (MaxEmission != other.MaxEmission) return MaxEmission > other.MaxEmission;
            if (Mathf.Abs(MaxDepth - other.MaxDepth) > 1e-7f) return MaxDepth > other.MaxDepth;
            int comparison = string.CompareOrdinal(Key.Slot, other.Key.Slot);
            if (comparison != 0) return comparison > 0;
            comparison = string.CompareOrdinal(Key.Material, other.Key.Material);
            if (comparison != 0) return comparison > 0;
            comparison = string.CompareOrdinal(Key.Surface, other.Key.Surface);
            if (comparison != 0) return comparison > 0;
            comparison = string.CompareOrdinal(Key.ObjectName, other.Key.ObjectName);
            return comparison != 0 ? comparison > 0 : Key.Order > other.Key.Order;
        }
    }
}

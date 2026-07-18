using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

internal readonly struct ArtifactAppearanceLocalFace
{
    public readonly Vector3[] Points;
    public readonly Vector3[] Normals;
    public readonly string Material;
    public readonly string Surface;
    public readonly string ObjectName;

    public ArtifactAppearanceLocalFace(Vector3[] points, string material, string surface)
        : this(
            points,
            Enumerable.Repeat(ArtifactAppearanceMath.FaceNormal(points), points.Length).ToArray(),
            material,
            surface,
            "primitive")
    {
    }

    public ArtifactAppearanceLocalFace(
        Vector3[] points,
        Vector3[] normals,
        string material,
        string surface,
        string objectName)
    {
        Points = points;
        Normals = normals;
        Material = material;
        Surface = surface;
        ObjectName = objectName;
    }
}

internal readonly struct ArtifactAppearanceMeshFace
{
    public readonly Vector3[] Points;
    public readonly Vector3[] Normals;
    public readonly string Slot;
    public readonly string Material;
    public readonly string Surface;
    public readonly string ObjectName;
    public readonly int Order;

    public ArtifactAppearanceMeshFace(
        Vector3[] points,
        Vector3[] normals,
        string slot,
        string material,
        string surface,
        string objectName,
        int order)
    {
        Points = points;
        Normals = normals;
        Slot = slot;
        Material = material;
        Surface = surface;
        ObjectName = objectName;
        Order = order;
    }
}

/// <summary>一个 ArtifactAppearance Instance 完整组合后的确定性模型。</summary>
internal sealed class ArtifactAppearanceMesh
{
    public readonly ArtifactAppearanceMeshFace[] Faces;
    public readonly Vector3 Min;
    public readonly Vector3 Max;

    public ArtifactAppearanceMesh(ArtifactAppearanceMeshFace[] faces)
    {
        Faces = faces;
        Vector3 min = faces[0].Points[0];
        Vector3 max = min;
        for (int i = 0; i < faces.Length; i++)
        {
            for (int pointIndex = 0; pointIndex < faces[i].Points.Length; pointIndex++)
            {
                min = Vector3.Min(min, faces[i].Points[pointIndex]);
                max = Vector3.Max(max, faces[i].Points[pointIndex]);
            }
        }
        Min = min;
        Max = max;
    }
}

/// <summary>将模板放置、Instance variant 与模型源统一解析为世界逻辑方向下的模型。</summary>
internal static class ArtifactAppearanceGeometry
{
    internal static ArtifactAppearanceMesh Build(
        ArtifactAppearance appearance,
        ArtifactAppearanceTemplateDef template,
        ArtifactAppearanceCatalog catalog)
    {
        List<ArtifactAppearanceMeshFace> faces = new();
        ArtifactAppearancePlacementDef[] placements = template.Placements
            .OrderBy(placement => placement.Z)
            .ToArray();
        for (int placementIndex = 0; placementIndex < placements.Length; placementIndex++)
        {
            ArtifactAppearancePlacementDef placement = placements[placementIndex];
            ArtifactAppearancePart? part = FindPart(appearance, placement);
            if (!part.HasValue) continue;
            ArtifactAppearanceVariantDef variant = catalog.Modules[placement.Module]
                .GetVariant(part.Value.variant);
            if (variant == null) continue;

            Vector3 anchor = ArtifactAppearanceMath.Vec3(
                variant.GetAnchor(placement.Anchor).Position,
                Vector3.zero);
            Vector3 position = ArtifactAppearanceMath.Vec3(placement.Position, Vector3.zero);
            Vector3 rotation = ArtifactAppearanceMath.Vec3(placement.Rotation, Vector3.zero);
            Vector3 scale = ArtifactAppearanceMath.Vec3(placement.Scale, Vector3.one);
            foreach (ArtifactAppearanceLocalFace localFace in ReadVariantFaces(variant))
            {
                Vector3[] points = new Vector3[localFace.Points.Length];
                Vector3[] normals = new Vector3[localFace.Points.Length];
                for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
                {
                    Vector3 point = Vector3.Scale(localFace.Points[pointIndex] - anchor, scale);
                    points[pointIndex] = ArtifactAppearanceMath.RotateEuler(point, rotation) + position;
                    Vector3 normal = localFace.Normals.Length == localFace.Points.Length
                        ? localFace.Normals[pointIndex]
                        : ArtifactAppearanceMath.FaceNormal(localFace.Points);
                    normal = new Vector3(
                        Mathf.Abs(scale.x) > 1e-8f ? normal.x / scale.x : normal.x,
                        Mathf.Abs(scale.y) > 1e-8f ? normal.y / scale.y : normal.y,
                        Mathf.Abs(scale.z) > 1e-8f ? normal.z / scale.z : normal.z);
                    normals[pointIndex] = ArtifactAppearanceMath.Normalize(
                        ArtifactAppearanceMath.RotateEuler(normal, rotation),
                        Vector3.forward);
                }
                faces.Add(new ArtifactAppearanceMeshFace(
                    points,
                    normals,
                    part.Value.slot,
                    localFace.Material,
                    localFace.Surface,
                    $"{part.Value.slot}/{localFace.ObjectName}",
                    placement.Z));
            }
        }
        return faces.Count == 0 ? null : new ArtifactAppearanceMesh(faces.ToArray());
    }

    internal static bool TryResolveAnchorPoint(
        ArtifactAppearance appearance,
        ArtifactAppearanceTemplateDef template,
        ArtifactAppearanceCatalog catalog,
        string slotKey,
        string anchorKey,
        out Vector3 point)
    {
        point = default;
        ArtifactAppearancePlacementDef placement = null;
        for (int i = 0; i < template.Placements.Length; i++)
        {
            if (template.Placements[i].Slot != slotKey) continue;
            placement = template.Placements[i];
            break;
        }
        if (placement == null) return false;

        ArtifactAppearancePart? part = FindPart(appearance, placement);
        if (!part.HasValue) return false;
        ArtifactAppearanceVariantDef variant = catalog.Modules[placement.Module]
            .GetVariant(part.Value.variant);
        ArtifactAppearanceAnchorDef placementAnchor = variant?.GetAnchor(placement.Anchor);
        ArtifactAppearanceAnchorDef requestedAnchor = variant?.GetAnchor(anchorKey);
        if (placementAnchor == null || requestedAnchor == null) return false;

        point = ArtifactAppearanceMath.Vec3(requestedAnchor.Position, Vector3.zero) -
                ArtifactAppearanceMath.Vec3(placementAnchor.Position, Vector3.zero);
        point = Vector3.Scale(point, ArtifactAppearanceMath.Vec3(placement.Scale, Vector3.one));
        point = ArtifactAppearanceMath.RotateEuler(
            point,
            ArtifactAppearanceMath.Vec3(placement.Rotation, Vector3.zero));
        point += ArtifactAppearanceMath.Vec3(placement.Position, Vector3.zero);
        return true;
    }

    internal static ArtifactAppearancePart? FindPart(
        ArtifactAppearance appearance,
        ArtifactAppearancePlacementDef placement)
    {
        for (int i = 0; i < appearance.parts.Length; i++)
        {
            ArtifactAppearancePart part = appearance.parts[i];
            if (part.slot == placement.Slot && part.module == placement.Module) return part;
        }
        for (int i = 0; i < appearance.parts.Length; i++)
        {
            if (appearance.parts[i].slot == placement.Slot) return appearance.parts[i];
        }
        return null;
    }

    private static IEnumerable<ArtifactAppearanceLocalFace> ReadVariantFaces(
        ArtifactAppearanceVariantDef variant)
    {
        if (variant.ModelData != null)
        {
            for (int i = 0; i < variant.ModelData.Faces.Length; i++)
            {
                ArtifactAppearanceModelFace face = variant.ModelData.Faces[i];
                yield return new ArtifactAppearanceLocalFace(
                    face.Points,
                    face.Normals,
                    face.Material,
                    variant.ResolveSurface(face.Material, face.Surface),
                    face.ObjectName);
            }
            yield break;
        }

        for (int partIndex = 0; partIndex < variant.Parts.Length; partIndex++)
        {
            foreach (ArtifactAppearanceLocalFace face in
                     ArtifactAppearancePrimitiveMesh.Build(variant.Parts[partIndex]))
            {
                yield return face;
            }
        }
    }
}

internal static class ArtifactAppearanceMath
{
    internal static Vector3 RotateEuler(Vector3 point, Vector3 rotation)
    {
        float x = point.x;
        float y = point.y;
        float z = point.z;
        if (rotation.x != 0f)
        {
            float angle = rotation.x * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            (y, z) = (y * cosine - z * sine, y * sine + z * cosine);
        }
        if (rotation.y != 0f)
        {
            float angle = rotation.y * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            (x, z) = (x * cosine + z * sine, -x * sine + z * cosine);
        }
        if (rotation.z != 0f)
        {
            float angle = rotation.z * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            (x, y) = (x * cosine - y * sine, x * sine + y * cosine);
        }
        return new Vector3(x, y, z);
    }

    internal static Vector3 FaceNormal(IReadOnlyList<Vector3> points)
    {
        return points.Count < 3
            ? Vector3.forward
            : Normalize(Vector3.Cross(points[1] - points[0], points[2] - points[0]), Vector3.forward);
    }

    internal static Vector3 Normalize(Vector3 value, Vector3 fallback)
    {
        return value.sqrMagnitude <= 1e-8f ? fallback : value.normalized;
    }

    internal static Vector3 Vec3(float[] value, Vector3 fallback)
    {
        if (value == null || value.Length == 0) return fallback;
        if (value.Length == 1) return new Vector3(value[0], value[0], value[0]);
        if (value.Length == 2) return new Vector3(value[0], value[1], fallback.z);
        return new Vector3(value[0], value[1], value[2]);
    }

    internal static Vector3 ReadVec3(JToken token, Vector3 fallback)
    {
        if (token == null) return fallback;
        if (token.Type is JTokenType.Integer or JTokenType.Float)
        {
            float scalar = token.Value<float>();
            return new Vector3(scalar, scalar, scalar);
        }
        if (token is not JArray array || array.Count == 0) return fallback;
        if (array.Count == 1)
        {
            float scalar = array[0].Value<float>();
            return new Vector3(scalar, scalar, scalar);
        }
        if (array.Count == 2)
            return new Vector3(array[0].Value<float>(), array[1].Value<float>(), fallback.z);
        return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
    }

    internal static float ReadFloat(JObject data, string key, float fallback)
    {
        return data != null && data.TryGetValue(key, out JToken token) ? token.Value<float>() : fallback;
    }

    internal static int ReadInt(JObject data, string key, int fallback)
    {
        return data != null && data.TryGetValue(key, out JToken token) ? token.Value<int>() : fallback;
    }

    internal static bool ReadBool(JObject data, string key, bool fallback)
    {
        return data != null && data.TryGetValue(key, out JToken token) ? token.Value<bool>() : fallback;
    }

    internal static List<Vector2> ReadPoints2(JToken token)
    {
        List<Vector2> result = new();
        if (token is not JArray array) return result;
        foreach (JToken item in array)
        {
            if (item is JArray point && point.Count >= 2)
                result.Add(new Vector2(point[0].Value<float>(), point[1].Value<float>()));
        }
        return result;
    }

    internal static List<Vector3> ReadPoints3(JToken token)
    {
        List<Vector3> result = new();
        if (token is not JArray array) return result;
        foreach (JToken item in array) result.Add(ReadVec3(item, Vector3.zero));
        return result;
    }

    internal static Vector2 RadiusPair(JToken token, Vector2 fallback)
    {
        if (token == null) return fallback;
        if (token.Type is JTokenType.Integer or JTokenType.Float)
        {
            float scalar = token.Value<float>();
            return new Vector2(scalar, scalar);
        }
        if (token is not JArray array || array.Count == 0) return fallback;
        if (array.Count == 1)
        {
            float scalar = array[0].Value<float>();
            return new Vector2(scalar, scalar);
        }
        return new Vector2(array[0].Value<float>(), array[1].Value<float>());
    }
}

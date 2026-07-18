using System.Collections.Generic;
using System.Linq;
using Cultiway.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>旧 Content primitive 到统一局部模型面的兼容转换器。</summary>
internal static class ArtifactAppearancePrimitiveMesh
{
    internal static IEnumerable<ArtifactAppearanceLocalFace> Build(JObject part)
    {
        string primitive = part.Value<string>("primitive") ?? part.Value<string>("type") ?? string.Empty;
        if (primitive == "radial_repeat")
        {
            foreach (ArtifactAppearanceLocalFace face in RadialRepeatFaces(part)) yield return face;
            yield break;
        }

        string material = part.Value<string>("material") ?? "main";
        string surface = part.Value<string>("surface") ?? "neutral";
        List<Vector3[]> faces = primitive switch
        {
            "box" => BoxFaces(part),
            "beveled_box" => BeveledBoxFaces(part),
            "poly_prism" => PolyPrismFaces(part),
            "blade" => BladeFaces(part),
            "cylinder" or "frustum" => FrustumFaces(part),
            "ellipsoid" => EllipsoidFaces(part),
            "torus" => TorusFaces(part),
            "lathe" => LatheFaces(part),
            "capsule" => CapsuleFaces(part),
            "tube" => TubeFaces(part),
            "cloth_panel" => ClothPanelFaces(part),
            _ => [],
        };
        for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
        {
            Vector3[] points = new Vector3[faces[faceIndex].Length];
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
                points[pointIndex] = TransformLocalPoint(faces[faceIndex][pointIndex], part);
            yield return new ArtifactAppearanceLocalFace(points, material, surface);
        }
    }

    private static IEnumerable<ArtifactAppearanceLocalFace> RadialRepeatFaces(JObject part)
    {
        if (part["part"] is not JObject child) yield break;
        int count = Mathf.Max(1, ArtifactAppearanceMath.ReadInt(part, "count", 6));
        float radius = ArtifactAppearanceMath.ReadFloat(part, "radius", 0f);
        float startAngle = ArtifactAppearanceMath.ReadFloat(part, "start_angle", 0f);
        ArtifactAppearanceLocalFace[] childFaces = Build(child).ToArray();
        for (int repeat = 0; repeat < count; repeat++)
        {
            float angle = startAngle + 360f * repeat / count;
            for (int faceIndex = 0; faceIndex < childFaces.Length; faceIndex++)
            {
                ArtifactAppearanceLocalFace childFace = childFaces[faceIndex];
                Vector3[] points = new Vector3[childFace.Points.Length];
                for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
                {
                    Vector3 point = childFace.Points[pointIndex] + Vector3.right * radius;
                    point = ArtifactAppearanceMath.RotateEuler(point, new Vector3(0f, angle, 0f));
                    points[pointIndex] = TransformLocalPoint(point, part);
                }
                yield return new ArtifactAppearanceLocalFace(points, childFace.Material, childFace.Surface);
            }
        }
    }

    private static Vector3 TransformLocalPoint(Vector3 point, JObject part)
    {
        point = Vector3.Scale(point, ArtifactAppearanceMath.ReadVec3(part["scale"], Vector3.one));
        point = ArtifactAppearanceMath.RotateEuler(
            point,
            ArtifactAppearanceMath.ReadVec3(part["rotation"] ?? part["rot"], Vector3.zero));
        return point + ArtifactAppearanceMath.ReadVec3(part["offset"] ?? part["position"], Vector3.zero);
    }

    private static List<Vector3[]> BoxFaces(JObject part)
    {
        Vector3 size = ArtifactAppearanceMath.ReadVec3(part["size"], Vector3.one);
        float x = size.x * 0.5f;
        float y = size.y * 0.5f;
        float z = size.z * 0.5f;
        Vector3[] vertices =
        [
            new(-x, -y, -z), new(x, -y, -z), new(x, y, -z), new(-x, y, -z),
            new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z),
        ];
        return
        [
            [vertices[4], vertices[5], vertices[6], vertices[7]],
            [vertices[1], vertices[0], vertices[3], vertices[2]],
            [vertices[0], vertices[4], vertices[7], vertices[3]],
            [vertices[5], vertices[1], vertices[2], vertices[6]],
            [vertices[3], vertices[7], vertices[6], vertices[2]],
            [vertices[0], vertices[1], vertices[5], vertices[4]],
        ];
    }

    private static List<Vector3[]> BeveledBoxFaces(JObject part)
    {
        Vector3 size = ArtifactAppearanceMath.ReadVec3(part["size"], Vector3.one);
        float bevel = Mathf.Clamp(
            ArtifactAppearanceMath.ReadFloat(part, "bevel", Mathf.Min(size.x, size.y) * 0.16f),
            0f,
            Mathf.Min(size.x, size.y) * 0.49f);
        float x = size.x * 0.5f;
        float y = size.y * 0.5f;
        List<Vector2> outline =
        [
            new(-x + bevel, -y), new(x - bevel, -y), new(x, -y + bevel), new(x, y - bevel),
            new(x - bevel, y), new(-x + bevel, y), new(-x, y - bevel), new(-x, -y + bevel),
        ];
        return PolyPrismFromOutline(outline, size.z);
    }

    private static List<Vector3[]> PolyPrismFaces(JObject part)
    {
        List<Vector2> points = ArtifactAppearanceMath.ReadPoints2(part["points"]);
        return points.Count < 3
            ? []
            : PolyPrismFromOutline(points, ArtifactAppearanceMath.ReadFloat(part, "depth", 0.12f));
    }

    private static List<Vector3[]> BladeFaces(JObject part)
    {
        float length = ArtifactAppearanceMath.ReadFloat(part, "length", 2.4f);
        float width = ArtifactAppearanceMath.ReadFloat(part, "width", 0.32f);
        float depth = ArtifactAppearanceMath.ReadFloat(part, "depth", 0.08f);
        float shoulder = ArtifactAppearanceMath.ReadFloat(part, "shoulder", 0.13f);
        float baseWidth = ArtifactAppearanceMath.ReadFloat(part, "base", width * 0.55f);
        List<Vector3[]> faces = PolyPrismFromOutline(
            [
                new Vector2(0f, length),
                new Vector2(width * 0.5f, shoulder),
                new Vector2(baseWidth * 0.5f, 0f),
                new Vector2(-baseWidth * 0.5f, 0f),
                new Vector2(-width * 0.5f, shoulder),
            ],
            depth);
        faces.Add([
            new Vector3(0f, length * 0.94f, depth * 0.58f),
            new Vector3(width * 0.16f, shoulder, depth * 0.5f),
            new Vector3(0f, 0.02f, depth * 0.58f),
            new Vector3(-width * 0.16f, shoulder, depth * 0.5f),
        ]);
        return faces;
    }

    private static List<Vector3[]> FrustumFaces(JObject part)
    {
        float height = ArtifactAppearanceMath.ReadFloat(part, "height", 1f);
        int segments = Mathf.Max(5, ArtifactAppearanceMath.ReadInt(part, "segments", 12));
        Vector2 topRadius = ArtifactAppearanceMath.RadiusPair(
            part["top_radius"] ?? part["radius"],
            new Vector2(0.5f, 0.5f));
        Vector2 bottomRadius = ArtifactAppearanceMath.RadiusPair(
            part["bottom_radius"] ?? part["radius"],
            new Vector2(0.5f, 0.5f));
        Vector3[] top = new Vector3[segments];
        Vector3[] bottom = new Vector3[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            top[i] = new Vector3(cosine * topRadius.x, height * 0.5f, sine * topRadius.y);
            bottom[i] = new Vector3(cosine * bottomRadius.x, -height * 0.5f, sine * bottomRadius.y);
        }
        List<Vector3[]> faces = new();
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            faces.Add([bottom[i], bottom[next], top[next], top[i]]);
        }
        if (ArtifactAppearanceMath.ReadBool(part, "cap_top", true)) faces.Add(top.Reverse().ToArray());
        if (ArtifactAppearanceMath.ReadBool(part, "cap_bottom", true)) faces.Add(bottom);
        return faces;
    }

    private static List<Vector3[]> EllipsoidFaces(JObject part)
    {
        Vector3 radius = ArtifactAppearanceMath.ReadVec3(part["radius"], new Vector3(0.5f, 0.5f, 0.5f));
        int segments = Mathf.Max(6, ArtifactAppearanceMath.ReadInt(part, "segments", 10));
        int rings = Mathf.Max(3, ArtifactAppearanceMath.ReadInt(part, "rings", 5));
        Vector3[][] rows = new Vector3[rings + 1][];
        for (int ring = 0; ring <= rings; ring++)
        {
            float phi = -Mathf.PI * 0.5f + Mathf.PI * ring / rings;
            float y = Mathf.Sin(phi) * radius.y;
            float radial = Mathf.Cos(phi);
            rows[ring] = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                rows[ring][i] = new Vector3(
                    Mathf.Cos(angle) * radial * radius.x,
                    y,
                    Mathf.Sin(angle) * radial * radius.z);
            }
        }
        List<Vector3[]> faces = new();
        for (int ring = 0; ring < rings; ring++)
        {
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                faces.Add([rows[ring][i], rows[ring][next], rows[ring + 1][next], rows[ring + 1][i]]);
            }
        }
        return faces;
    }

    private static List<Vector3[]> TorusFaces(JObject part)
    {
        float majorRadius = ArtifactAppearanceMath.ReadFloat(part, "major_radius", 0.5f);
        float minorRadius = ArtifactAppearanceMath.ReadFloat(part, "minor_radius", 0.1f);
        int segments = Mathf.Max(6, ArtifactAppearanceMath.ReadInt(part, "segments", 16));
        int tubeSegments = Mathf.Max(4, ArtifactAppearanceMath.ReadInt(part, "tube_segments", 6));
        Vector3[][] rings = new Vector3[segments][];
        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            rings[i] = new Vector3[tubeSegments];
            for (int tube = 0; tube < tubeSegments; tube++)
            {
                float tubeAngle = Mathf.PI * 2f * tube / tubeSegments;
                float radial = majorRadius + Mathf.Cos(tubeAngle) * minorRadius;
                rings[i][tube] = new Vector3(
                    cosine * radial,
                    Mathf.Sin(tubeAngle) * minorRadius,
                    sine * radial);
            }
        }
        List<Vector3[]> faces = new();
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            for (int tube = 0; tube < tubeSegments; tube++)
            {
                int tubeNext = (tube + 1) % tubeSegments;
                faces.Add([rings[i][tube], rings[next][tube], rings[next][tubeNext], rings[i][tubeNext]]);
            }
        }
        return faces;
    }

    private static List<Vector3[]> LatheFaces(JObject part)
    {
        return LatheFromProfile(
            ArtifactAppearanceMath.ReadPoints2(part["profile"]),
            Mathf.Max(6, ArtifactAppearanceMath.ReadInt(part, "segments", 14)),
            ArtifactAppearanceMath.ReadBool(part, "cap_top", true),
            ArtifactAppearanceMath.ReadBool(part, "cap_bottom", true));
    }

    private static List<Vector3[]> CapsuleFaces(JObject part)
    {
        float radius = ArtifactAppearanceMath.ReadFloat(part, "radius", 0.3f);
        float height = Mathf.Max(radius * 2f, ArtifactAppearanceMath.ReadFloat(part, "height", 1.2f));
        int capRings = Mathf.Max(2, ArtifactAppearanceMath.ReadInt(part, "rings", 4));
        float cylinderHalf = height * 0.5f - radius;
        List<Vector2> profile = new();
        for (int i = 0; i <= capRings; i++)
        {
            float angle = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / capRings;
            profile.Add(new Vector2(Mathf.Cos(angle) * radius, -cylinderHalf + Mathf.Sin(angle) * radius));
        }
        for (int i = 1; i <= capRings; i++)
        {
            float angle = Mathf.PI * 0.5f * i / capRings;
            profile.Add(new Vector2(Mathf.Cos(angle) * radius, cylinderHalf + Mathf.Sin(angle) * radius));
        }
        return LatheFromProfile(
            profile,
            Mathf.Max(6, ArtifactAppearanceMath.ReadInt(part, "segments", 12)),
            false,
            false);
    }

    private static List<Vector3[]> LatheFromProfile(
        IReadOnlyList<Vector2> profile,
        int segments,
        bool capTop,
        bool capBottom)
    {
        if (profile.Count < 2) return [];
        Vector3[][] rings = new Vector3[profile.Count][];
        for (int row = 0; row < profile.Count; row++)
        {
            rings[row] = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                rings[row][i] = new Vector3(
                    Mathf.Cos(angle) * profile[row].x,
                    profile[row].y,
                    Mathf.Sin(angle) * profile[row].x);
            }
        }
        List<Vector3[]> faces = new();
        for (int row = 0; row < profile.Count - 1; row++)
        {
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                faces.Add([rings[row][i], rings[row][next], rings[row + 1][next], rings[row + 1][i]]);
            }
        }
        if (capBottom && profile[0].x > 0f) faces.Add(rings[0]);
        if (capTop && profile[profile.Count - 1].x > 0f)
            faces.Add(rings[profile.Count - 1].Reverse().ToArray());
        return faces;
    }

    private static List<Vector3[]> TubeFaces(JObject part)
    {
        List<Vector3> path = ArtifactAppearanceMath.ReadPoints3(part["points"]);
        if (path.Count < 2) return [];
        float radius = ArtifactAppearanceMath.ReadFloat(part, "radius", 0.08f);
        int segments = Mathf.Max(4, ArtifactAppearanceMath.ReadInt(part, "segments", 7));
        Vector3[][] rings = new Vector3[path.Count][];
        for (int row = 0; row < path.Count; row++)
        {
            Vector3 tangent = row == 0
                ? path[1] - path[0]
                : row == path.Count - 1
                    ? path[row] - path[row - 1]
                    : path[row + 1] - path[row - 1];
            tangent = ArtifactAppearanceMath.Normalize(tangent, Vector3.up);
            Vector3 normal = Vector3.Cross(tangent, Vector3.forward);
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.Cross(tangent, Vector3.right);
            normal.Normalize();
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            rings[row] = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                rings[row][i] = path[row] +
                                (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * radius;
            }
        }
        List<Vector3[]> faces = new();
        for (int row = 0; row < path.Count - 1; row++)
        {
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                faces.Add([rings[row][i], rings[row][next], rings[row + 1][next], rings[row + 1][i]]);
            }
        }
        if (ArtifactAppearanceMath.ReadBool(part, "cap_start", true)) faces.Add(rings[0].Reverse().ToArray());
        if (ArtifactAppearanceMath.ReadBool(part, "cap_end", true)) faces.Add(rings[path.Count - 1]);
        return faces;
    }

    private static List<Vector3[]> ClothPanelFaces(JObject part)
    {
        float width = ArtifactAppearanceMath.ReadFloat(part, "width", 1f);
        float height = ArtifactAppearanceMath.ReadFloat(part, "height", 1.4f);
        float depth = ArtifactAppearanceMath.ReadFloat(part, "depth", 0.035f);
        float flare = ArtifactAppearanceMath.ReadFloat(part, "flare", 0.15f);
        float curve = ArtifactAppearanceMath.ReadFloat(part, "curve", 0.08f);
        float wave = ArtifactAppearanceMath.ReadFloat(part, "wave", 0.04f);
        int columns = Mathf.Max(2, ArtifactAppearanceMath.ReadInt(part, "segments_x", 5));
        int rows = Mathf.Max(2, ArtifactAppearanceMath.ReadInt(part, "segments_y", 7));
        Vector3[,] front = new Vector3[rows + 1, columns + 1];
        Vector3[,] back = new Vector3[rows + 1, columns + 1];
        for (int row = 0; row <= rows; row++)
        {
            float v = row / (float)rows;
            float halfWidth = width * 0.5f * (1f + flare * v);
            for (int column = 0; column <= columns; column++)
            {
                float u = column / (float)columns * 2f - 1f;
                float x = u * halfWidth;
                float y = height * (0.5f - v);
                float z = curve * u * u + Mathf.Sin((u + v) * Mathf.PI * 2f) * wave;
                front[row, column] = new Vector3(x, y, z + depth * 0.5f);
                back[row, column] = new Vector3(x, y, z - depth * 0.5f);
            }
        }
        List<Vector3[]> faces = new();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                faces.Add([
                    front[row, column], front[row, column + 1],
                    front[row + 1, column + 1], front[row + 1, column],
                ]);
                faces.Add([
                    back[row, column], back[row + 1, column],
                    back[row + 1, column + 1], back[row, column + 1],
                ]);
            }
        }
        for (int column = 0; column < columns; column++)
        {
            faces.Add([front[0, column], back[0, column], back[0, column + 1], front[0, column + 1]]);
            faces.Add([front[rows, column], front[rows, column + 1], back[rows, column + 1], back[rows, column]]);
        }
        for (int row = 0; row < rows; row++)
        {
            faces.Add([front[row, 0], front[row + 1, 0], back[row + 1, 0], back[row, 0]]);
            faces.Add([
                front[row, columns], back[row, columns],
                back[row + 1, columns], front[row + 1, columns],
            ]);
        }
        return faces;
    }

    private static List<Vector3[]> PolyPrismFromOutline(IReadOnlyList<Vector2> outline, float depth)
    {
        Vector3[] front = outline.Select(point => new Vector3(point.x, point.y, depth * 0.5f)).ToArray();
        Vector3[] back = outline.Select(point => new Vector3(point.x, point.y, -depth * 0.5f)).Reverse().ToArray();
        List<Vector3[]> faces = [front, back];
        for (int i = 0; i < outline.Count; i++)
        {
            int next = (i + 1) % outline.Count;
            faces.Add([
                new Vector3(outline[i].x, outline[i].y, depth * 0.5f),
                new Vector3(outline[next].x, outline[next].y, depth * 0.5f),
                new Vector3(outline[next].x, outline[next].y, -depth * 0.5f),
                new Vector3(outline[i].x, outline[i].y, -depth * 0.5f),
            ]);
        }
        return faces;
    }
}

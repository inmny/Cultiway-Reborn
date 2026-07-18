using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Cultiway.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>运行时使用的只读 OBJ 模型；颜色由法器 Instance 提供。</summary>
internal sealed class ArtifactAppearanceModelData
{
    public readonly string Path;
    public readonly ArtifactAppearanceModelFace[] Faces;
    public readonly ArtifactAppearanceAnchorDef[] Anchors;

    public ArtifactAppearanceModelData(
        string path,
        ArtifactAppearanceModelFace[] faces,
        ArtifactAppearanceAnchorDef[] anchors)
    {
        Path = path;
        Faces = faces;
        Anchors = anchors;
    }
}

internal readonly struct ArtifactAppearanceModelFace
{
    public readonly Vector3[] Points;
    public readonly Vector3[] Normals;
    public readonly string Material;
    public readonly string Surface;
    public readonly string ObjectName;

    public ArtifactAppearanceModelFace(
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

/// <summary>读取 Blockbench 可导出的 OBJ，仅消费顶点、面和材质语义。</summary>
internal static class ArtifactAppearanceObjLoader
{
    private const string SurfaceSeparator = "__surface__";

    internal static ArtifactAppearanceModelData Load(string catalogRoot, string relativePath)
    {
        string path = ResolveModelPath(catalogRoot, relativePath);
        List<Vector3> vertices = new();
        List<RawFace> rawFaces = new();
        string material = "main";
        string surface = string.Empty;
        string objectName = Path.GetFileNameWithoutExtension(path);
        int lineNumber = 0;
        foreach (string rawLine in File.ReadLines(path))
        {
            lineNumber++;
            string line = rawLine;
            int comment = line.IndexOf('#');
            if (comment >= 0) line = line.Substring(0, comment);
            line = line.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int separator = line.IndexOfAny([' ', '\t']);
            string command = separator < 0 ? line : line.Substring(0, separator);
            string payload = separator < 0 ? string.Empty : line.Substring(separator + 1).Trim();
            switch (command)
            {
                case "v":
                    vertices.Add(ParseVertex(payload, path, lineNumber));
                    break;
                case "usemtl":
                    ParseMaterial(payload, out material, out surface);
                    if (string.IsNullOrWhiteSpace(material))
                        throw ParseError(path, lineNumber, "材质角色为空");
                    break;
                case "o":
                case "g":
                    if (!string.IsNullOrWhiteSpace(payload)) objectName = payload;
                    break;
                case "f":
                    rawFaces.Add(ParseFace(payload, vertices, material, surface, objectName, path, lineNumber));
                    break;
            }
        }

        if (vertices.Count == 0 || rawFaces.Count == 0)
            throw new InvalidDataException($"OBJ 没有有效几何体: {path}");
        return new ArtifactAppearanceModelData(path, BuildFaces(vertices, rawFaces), LoadAnchors(path));
    }

    private static string ResolveModelPath(string catalogRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidDataException("法器 variant 的 model 路径为空");
        string root = Path.GetFullPath(catalogRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                      Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"法器模型路径越过 AppearanceCatalog: {relativePath}");
        if (!string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"当前只支持 Blockbench OBJ: {relativePath}");
        if (!File.Exists(path)) throw new FileNotFoundException("找不到法器 OBJ", path);
        return path;
    }

    private static Vector3 ParseVertex(string payload, string path, int lineNumber)
    {
        string[] values = payload.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 3) throw ParseError(path, lineNumber, "顶点坐标不完整");
        return new Vector3(ParseFloat(values[0]), ParseFloat(values[1]), ParseFloat(values[2]));
    }

    private static RawFace ParseFace(
        string payload,
        IReadOnlyList<Vector3> vertices,
        string material,
        string surface,
        string objectName,
        string path,
        int lineNumber)
    {
        string[] values = payload.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length < 3) throw ParseError(path, lineNumber, "面至少需要三个顶点");
        int[] indices = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            string rawIndex = values[i].Split('/')[0];
            if (!int.TryParse(rawIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                throw ParseError(path, lineNumber, $"非法面索引 {values[i]}");
            int index = value > 0 ? value - 1 : vertices.Count + value;
            if (index < 0 || index >= vertices.Count)
                throw ParseError(path, lineNumber, $"面索引越界 {values[i]}");
            indices[i] = index;
        }
        return new RawFace(indices, material, surface, objectName);
    }

    private static ArtifactAppearanceModelFace[] BuildFaces(
        IReadOnlyList<Vector3> vertices,
        IReadOnlyList<RawFace> rawFaces)
    {
        Vector3[] faceNormals = new Vector3[rawFaces.Count];
        Dictionary<SmoothKey, List<int>> incidents = new();
        for (int faceIndex = 0; faceIndex < rawFaces.Count; faceIndex++)
        {
            RawFace face = rawFaces[faceIndex];
            Vector3[] points = ResolvePoints(vertices, face.Indices);
            faceNormals[faceIndex] = ArtifactAppearanceMath.FaceNormal(points);
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                SmoothKey key = new(face.ObjectName, face.Surface, points[pointIndex]);
                if (!incidents.TryGetValue(key, out List<int> values))
                {
                    values = new List<int>();
                    incidents.Add(key, values);
                }
                if (!values.Contains(faceIndex)) values.Add(faceIndex);
            }
        }

        ArtifactAppearanceModelFace[] faces = new ArtifactAppearanceModelFace[rawFaces.Count];
        for (int faceIndex = 0; faceIndex < rawFaces.Count; faceIndex++)
        {
            RawFace face = rawFaces[faceIndex];
            Vector3[] points = ResolvePoints(vertices, face.Indices);
            Vector3[] normals = new Vector3[points.Length];
            Vector3 faceNormal = faceNormals[faceIndex];
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                Vector3 normal = Vector3.zero;
                SmoothKey key = new(face.ObjectName, face.Surface, points[pointIndex]);
                List<int> values = incidents[key];
                for (int incidentIndex = 0; incidentIndex < values.Count; incidentIndex++)
                {
                    Vector3 candidate = faceNormals[values[incidentIndex]];
                    if (Vector3.Dot(faceNormal, candidate) >= 0.64f) normal += candidate;
                }
                normals[pointIndex] = ArtifactAppearanceMath.Normalize(normal, faceNormal);
            }
            faces[faceIndex] = new ArtifactAppearanceModelFace(
                points,
                normals,
                face.Material,
                face.Surface,
                face.ObjectName);
        }
        return faces;
    }

    private static Vector3[] ResolvePoints(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> indices)
    {
        Vector3[] points = new Vector3[indices.Count];
        for (int i = 0; i < points.Length; i++) points[i] = vertices[indices[i]];
        return points;
    }

    private static void ParseMaterial(string value, out string material, out string surface)
    {
        int separator = value.IndexOf(SurfaceSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            material = string.IsNullOrWhiteSpace(value) ? "main" : value;
            surface = string.Empty;
            return;
        }
        material = value.Substring(0, separator);
        surface = value.Substring(separator + SurfaceSeparator.Length);
    }

    private static ArtifactAppearanceAnchorDef[] LoadAnchors(string modelPath)
    {
        string path = Path.ChangeExtension(modelPath, ".anchors.json");
        if (!File.Exists(path)) throw new FileNotFoundException("找不到法器模型锚点", path);
        JObject root = JObject.Parse(File.ReadAllText(path));
        if (root["anchors"] is not JObject values || values.Count == 0)
            throw new InvalidDataException($"法器模型没有锚点: {path}");

        List<ArtifactAppearanceAnchorDef> anchors = new(values.Count);
        foreach (JProperty property in values.Properties())
        {
            if (property.Value is not JArray position || position.Count != 3)
                throw new InvalidDataException($"法器模型锚点必须是三维坐标: {path}#{property.Name}");
            anchors.Add(new ArtifactAppearanceAnchorDef
            {
                Key = property.Name,
                Position =
                [
                    position[0].Value<float>(),
                    position[1].Value<float>(),
                    position[2].Value<float>(),
                ],
            });
        }
        return anchors.ToArray();
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static InvalidDataException ParseError(string path, int lineNumber, string message)
    {
        return new InvalidDataException($"{path}:{lineNumber} {message}");
    }

    private readonly struct RawFace
    {
        internal readonly int[] Indices;
        internal readonly string Material;
        internal readonly string Surface;
        internal readonly string ObjectName;

        internal RawFace(int[] indices, string material, string surface, string objectName)
        {
            Indices = indices;
            Material = material;
            Surface = surface;
            ObjectName = objectName;
        }
    }

    /// <summary>以量化坐标连接 OBJ 中重复写出的同一点，同时保留对象与表面硬边。</summary>
    private readonly struct SmoothKey : IEquatable<SmoothKey>
    {
        private readonly string objectName;
        private readonly string surface;
        private readonly int x;
        private readonly int y;
        private readonly int z;

        internal SmoothKey(string objectName, string surface, Vector3 point)
        {
            this.objectName = objectName;
            this.surface = surface;
            x = Mathf.RoundToInt(point.x * 100000f);
            y = Mathf.RoundToInt(point.y * 100000f);
            z = Mathf.RoundToInt(point.z * 100000f);
        }

        public bool Equals(SmoothKey other)
        {
            return x == other.x && y == other.y && z == other.z &&
                   objectName == other.objectName && surface == other.surface;
        }

        public override bool Equals(object obj)
        {
            return obj is SmoothKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = objectName?.GetHashCode() ?? 0;
                hash = hash * 397 ^ (surface?.GetHashCode() ?? 0);
                hash = hash * 397 ^ x;
                hash = hash * 397 ^ y;
                return hash * 397 ^ z;
            }
        }
    }
}

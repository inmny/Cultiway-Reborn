using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>沿真实关节区域分开同一模型的表面，不增加切口封面或改变外观。</summary>
internal static class BodySurfacePartition
{
    /// <summary>每个原三角面分别裁到关节两侧，结果仍使用原部件变换。</summary>
    /// <param name="source">实际肢段。</param><param name="distal">掌或足的实际名称。</param>
    /// <param name="point">原关节处的模型坐标。</param><param name="normal">指向掌或足的模型方向。</param>
    /// <param name="proximal">关节内侧保留的肢段。</param><param name="end">实际末端表面。</param>
    internal static void Split(BodySurfacePart source, string distal, Vector3 point, Vector3 normal,
        out BodySurfacePart proximal, out BodySurfacePart end)
    {
        proximal = Clip(false, source.Id, source.Group);
        end = Clip(true, distal, source.Id);

        // 只生成原表面的子三角面；共面的原面归末端，避免两侧重复计算面积。
        /// <summary>先逐边裁出原面的多边形，再在原面上分成三角形。</summary>
        /// <param name="outside">是否保留末端一侧。</param><param name="id">实际部位。</param>
        /// <param name="group">包含本区域的原肢段。</param>
        BodySurfacePart Clip(bool outside, string id, string group)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = source.FaceColors == null ? null : new List<Color32>();
            var vertexColors = source.VertexColors == null ? null : new List<Color32>();
            var polygon = new List<Vector3>(4);
            for (int face = 0; face < source.Triangles.Length; face += 3)
            {
                polygon.Clear();
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 a = source.Vertices[source.Triangles[face + (corner + 2) % 3]],
                        b = source.Vertices[source.Triangles[face + corner]];
                    float da = Vector3.Dot(a - point, normal), db = Vector3.Dot(b - point, normal);
                    bool aIn = outside ? da >= 0f : da < 0f, bIn = outside ? db >= 0f : db < 0f;
                    if (aIn != bIn) polygon.Add(Vector3.LerpUnclamped(a, b, da / (da - db)));
                    if (bIn) polygon.Add(b);
                }

                int start = vertices.Count;
                vertices.AddRange(polygon);
                if (vertexColors != null)
                    foreach (Vector3 vertex in polygon)
                    {
                        int a = source.Triangles[face], b = source.Triangles[face + 1], c = source.Triangles[face + 2];
                        var anchor = new BodySurfaceAnchor(0, face, vertex, source.Vertices[a], source.Vertices[b], source.Vertices[c]);
                        vertexColors.Add((Color)source.VertexColors[a] * anchor.Weights.x +
                                         (Color)source.VertexColors[b] * anchor.Weights.y +
                                         (Color)source.VertexColors[c] * anchor.Weights.z);
                    }
                for (int corner = 1; corner < polygon.Count - 1; corner++)
                    if (Vector3.Cross(polygon[corner] - polygon[0], polygon[corner + 1] - polygon[0])
                            .sqrMagnitude > 0f)
                    {
                        triangles.Add(start);
                        triangles.Add(start + corner);
                        triangles.Add(start + corner + 1);
                        colors?.Add(source.FaceColors[face / 3]);
                    }
            }

            return new BodySurfacePart(id, vertices.ToArray(), triangles.ToArray(), source.Attachment,
                source.TransformIndex, group, faceColors: colors?.ToArray(), vertexColors: vertexColors?.ToArray()).WithSelections(source.Selections);
        }
    }
}
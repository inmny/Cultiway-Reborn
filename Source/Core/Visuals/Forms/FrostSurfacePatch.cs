using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>把一处薄霜裁在真正露出的身体或装备表面；两种视角读取同一个网格。</summary>
internal sealed class FrostSurfacePatch
{
    /// <summary>一块霜从出现到消退复用同一网格。</summary>
    internal readonly Mesh Mesh = new() { name = "Cultiway.FrostCoating", indexFormat = IndexFormat.UInt32 };

    /// <summary>主线程逐块发布，不为每个活动霜渍复制上传缓冲。</summary>
    private static readonly List<Vector3> points = new(), normals = new();
    /// <summary>薄冰表面取样的厚度和霜白密度。</summary>
    private static readonly List<Vector2> frost = new();
    /// <summary>实际露出三角面的索引。</summary>
    private static readonly List<int> triangles = new();
    /// <summary>三角面由六个平面裁切时复用的凸多边形顶点。</summary>
    private static readonly Vector3[] polygonA = new Vector3[12], polygonB = new Vector3[12];

    /// <summary>启用短时间更新的薄层网格。</summary>
    internal FrostSurfacePatch() => Mesh.MarkDynamic();

    /// <summary>仅发布接触点附近的实际外层；不会跨到另一部件或遮挡后的持物。</summary>
    /// <param name="pose">当时真正可见的姿态。</param><param name="part">接触到的部件。</param>
    /// <param name="center">同一模型面上的当前世界位置。</param><param name="radius">目前实际爬霜距离。</param>
    internal bool Update(BodySurfacePose pose, int part, Vector3 center, float radius)
    {
        points.Clear(); normals.Clear(); frost.Clear(); triangles.Clear();
        for (int face = 0; face < pose.Parts[part].Triangles.Length; face += 3)
            foreach (Vector3[] visible in pose.Fragments(part, face))
                for (int i = 1; i + 1 < visible.Length; i++)
                    ClipTriangle(visible[0], visible[i], visible[i + 1], center, radius);
        Mesh.Clear();
        if (triangles.Count == 0) return false;
        Mesh.SetVertices(points); Mesh.SetNormals(normals); Mesh.SetUVs(3, frost); Mesh.SetTriangles(triangles, 0);
        return true;
    }

    /// <summary>用相交的六个平面限制范围，保留每个原三角面的真实方向。</summary>
    /// <param name="a">首点。</param><param name="b">第二点。</param><param name="c">第三点。</param>
    /// <param name="center">实际霜心。</param><param name="radius">薄霜当前外延。</param>
    private static void ClipTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 center, float radius)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        if (normal == Vector3.zero || (AbilityTriangleSweep.Point(center, a, b, c) - center).sqrMagnitude > 3f * radius * radius)
            return;
        Vector3[] input = polygonA, output = polygonB;
        input[0] = a; input[1] = b; input[2] = c;
        int count = 3;
        for (int axis = 0; axis < 3; axis++)
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int used = 0;
            Vector3 prior = input[count - 1];
            float before = radius - sign * (prior[axis] - center[axis]);
            for (int i = 0; i < count; i++)
            {
                Vector3 next = input[i];
                float after = radius - sign * (next[axis] - center[axis]);
                if ((after >= 0f) != (before >= 0f))
                    output[used++] = Vector3.LerpUnclamped(prior, next, before / (before - after));
                if (after >= 0f) output[used++] = next;
                prior = next; before = after;
            }
            if (used < 3) return;
            (input, output) = (output, input);
            count = used;
        }
        int first = points.Count;
        for (int i = 0; i < count; i++)
        {
            Vector3 vertex = input[i];
            points.Add(vertex + normal * .012f);
            normals.Add(normal);
            float fade = 1f - Mathf.Clamp01((vertex - center).magnitude / (radius * 1.4f));
            frost.Add(new Vector2(.10f + .18f * fade, .58f + .42f * fade));
        }
        for (int i = 1; i + 1 < count; i++)
        {
            triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1);
        }
    }
}

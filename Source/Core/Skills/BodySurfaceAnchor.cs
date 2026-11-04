using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际模型三角面内的连接位置，避免大世界坐标取整后无法再认回原表面。</summary>
internal readonly struct BodySurfaceAnchor
{
    /// <summary>实际部件及三角面索引，不保存运行时人物引用。</summary>
    internal readonly int Part, Triangle;

    /// <summary>三个顶点的非负权重，合计为一。</summary>
    internal readonly Vector3 Weights;

    /// <summary>由本次确实接触的三角面取得位置，退化面沿最长有限边定位。</summary>
    /// <param name="part">实际部件。</param><param name="triangle">实际三角面。</param>
    /// <param name="point">共同接触位置。</param><param name="a">面第一点。</param><param name="b">第二点。</param><param name="c">第三点。</param>
    internal BodySurfaceAnchor(int part, int triangle, Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Part = part;
        Triangle = triangle;
        Vector3 ab = b - a, ac = c - a, p = point - a;
        float aa = Vector3.Dot(ab, ab),
            cc = Vector3.Dot(ac, ac),
            bc = Vector3.Dot(ab, ac),
            ap = Vector3.Dot(ab, p),
            cp = Vector3.Dot(ac, p),
            determinant = aa * cc - bc * bc;
        if (determinant > 1e-12f * aa * cc)
        {
            float v = (cc * ap - bc * cp) / determinant, w = (aa * cp - bc * ap) / determinant;
            Vector3 weights = Vector3.Max(Vector3.zero, new Vector3(1f - v - w, v, w));
            Weights = weights / (weights.x + weights.y + weights.z);
        }
        else
        {
            Vector3 edge = b - a, start = a, left = Vector3.right, right = Vector3.up;
            if (ac.sqrMagnitude > edge.sqrMagnitude)
            {
                edge = ac;
                right = Vector3.forward;
            }

            if ((c - b).sqrMagnitude > edge.sqrMagnitude)
            {
                edge = c - b;
                start = b;
                left = Vector3.up;
                right = Vector3.forward;
            }

            float along = edge.sqrMagnitude > 1e-20f
                ? Mathf.Clamp01(Vector3.Dot(point - start, edge) / edge.sqrMagnitude)
                : 0f;
            Weights = Vector3.Lerp(left, right, along);
        }
    }

    /// <summary>在实际局部三角面中计算同一位置，不先投影到世界再反求。</summary>
    /// <param name="pose">同一模型姿态。</param>
    internal Vector3 Local(BodySurfacePose pose)
    {
        BodySurfacePart part = pose.Parts[Part];
        int[] triangles = part.Triangles;
        return part.Vertices[triangles[Triangle]] * Weights.x + part.Vertices[triangles[Triangle + 1]] * Weights.y +
               part.Vertices[triangles[Triangle + 2]] * Weights.z;
    }

    /// <summary>由相同部件变换得到世界位置，旋转或移位也不换成其他表面。</summary>
    /// <param name="pose">共同模型姿态。</param>
    internal Vector3 Point(BodySurfacePose pose) => pose
        .Matrices[pose.Parts[Part].TransformIndex < 0 ? Part : pose.Parts[Part].TransformIndex]
        .MultiplyPoint3x4(Local(pose));
}
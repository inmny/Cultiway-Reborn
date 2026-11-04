using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>模型中一个具名部件的实际表面，资料创建后不修改。</summary>
internal sealed class BodySurfacePart
{
    /// <summary>构造中可以选择的身体部位。</summary>
    internal readonly string Id;

    /// <summary>模型自身的顶点与三角面，不放大到人物包络。</summary>
    internal readonly Vector3[] Vertices;

    /// <summary>每三个索引组成一个实际面。</summary>
    internal readonly int[] Triangles;

    /// <summary>从实际资源取得几何。</summary>
    /// <param name="id">部位。</param><param name="vertices">真实顶点。</param><param name="triangles">真实三角形。</param>
    internal BodySurfacePart(string id, Vector3[] vertices, int[] triangles)
    {
        Id = id;
        Vertices = vertices;
        Triangles = triangles;
    }
}

/// <summary>共同时间中的实际身体表面姿态，只含数值，显示与接触使用同一份。</summary>
internal sealed class BodySurfacePose
{
    /// <summary>同一身体模型的实际部件。</summary>
    internal readonly BodySurfacePart[] Parts;

    /// <summary>每个部件到共同世界的完整变换。</summary>
    internal readonly Matrix4x4[] Matrices;

    /// <summary>本姿态的实际脚下、身体高度与水平朝向。</summary>
    internal readonly Vector3 Foot;

    /// <summary>原身体的共同高度。</summary>
    internal readonly float Height, Heading;

    /// <summary>发布后不再修改所有权交入的数组。</summary>
    /// <param name="parts">实际模型。</param><param name="matrices">实际部件变换。</param>
    /// <param name="foot">共同脚下。</param><param name="height">共同高度。</param><param name="heading">朝向弧度。</param>
    internal BodySurfacePose(BodySurfacePart[] parts, Matrix4x4[] matrices, Vector3 foot, float height, float heading)
    {
        Parts = parts;
        Matrices = matrices;
        Foot = foot;
        Height = height;
        Heading = heading;
    }

    /// <summary>按共同扫掠所用的线性表面运动取得同一中间姿态，不重新求另一套骨架。</summary>
    /// <param name="from">前态。</param><param name="to">后态。</param><param name="at">共同进度。</param>
    internal static BodySurfacePose Lerp(BodySurfacePose from, BodySurfacePose to, float at)
    {
        if (at <= 0f) return from;
        if (at >= 1f) return to;
        var matrices = new Matrix4x4[to.Matrices.Length];
        for (int part = 0; part < matrices.Length; part++)
        for (int value = 0; value < 16; value++)
            matrices[part][value] = Mathf.Lerp(from.Matrices[part][value], to.Matrices[part][value], at);
        return new BodySurfacePose(to.Parts, matrices, Vector3.Lerp(from.Foot, to.Foot, at),
            Mathf.Lerp(from.Height, to.Height, at),
            Mathf.LerpAngle(from.Heading * Mathf.Rad2Deg, to.Heading * Mathf.Rad2Deg, at) * Mathf.Deg2Rad);
    }

    /// <summary>在所选真实部件中寻找最早接触，返回接触时完整表面供维持及显示。</summary>
    /// <param name="from">前态。</param><param name="to">后态。</param>
    /// <param name="sourceFrom">来源前态。</param><param name="sourceTo">来源后态。</param>
    /// <param name="selection">目标部位，surface代表所有已有真实部件。</param>
    /// <param name="at">最早进度。</param><param name="point">目标表面点。</param><param name="normal">接触方向。</param>
    internal static bool Find(BodySurfacePose from, BodySurfacePose to, in AbilityCapsule sourceFrom,
        in AbilityCapsule sourceTo, string selection, out float at, out Vector3 point, out Vector3 normal)
    {
        at = float.PositiveInfinity;
        point = normal = default;
        Bounds sweep = AbilityContactIndex.Swept(sourceFrom, sourceTo);
        for (int part = 0; part < to.Parts.Length; part++)
        {
            BodySurfacePart mesh = to.Parts[part];
            if (selection != "surface" && selection != mesh.Id) continue;
            for (int triangle = 0; triangle < mesh.Triangles.Length; triangle += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Triangles[triangle]],
                    b = mesh.Vertices[mesh.Triangles[triangle + 1]],
                    c = mesh.Vertices[mesh.Triangles[triangle + 2]];
                Vector3 a0 = from.Matrices[part].MultiplyPoint3x4(a),
                    b0 = from.Matrices[part].MultiplyPoint3x4(b),
                    c0 = from.Matrices[part].MultiplyPoint3x4(c),
                    a1 = to.Matrices[part].MultiplyPoint3x4(a),
                    b1 = to.Matrices[part].MultiplyPoint3x4(b),
                    c1 = to.Matrices[part].MultiplyPoint3x4(c);
                Bounds triangleBounds = new(a0, Vector3.zero);
                triangleBounds.Encapsulate(b0);
                triangleBounds.Encapsulate(c0);
                triangleBounds.Encapsulate(a1);
                triangleBounds.Encapsulate(b1);
                triangleBounds.Encapsulate(c1);
                if (!sweep.Intersects(triangleBounds)) continue;
                if (AbilityTriangleSweep.Find(sourceFrom, sourceTo,
                        a0, b0, c0, a1, b1, c1,
                        out float fraction, out Vector3 hit, out Vector3 direction) && fraction < at)
                {
                    at = fraction;
                    point = hit;
                    normal = direction;
                }
            }
        }

        return !float.IsPositiveInfinity(at);
    }
}
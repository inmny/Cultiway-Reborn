using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只把已经采样的作用形状转换为调试线段，不改变碰撞。</summary>
internal static class AbilityContactOutline
{
    /// <summary>圆弧线段的内缩与扇形共用同一个角度上限。</summary>
    private const int CircleSteps = 360 / FormContactRegions.ArcStepDegrees;

    /// <summary>添加这个时点的实际厚度、半径与轮廓。</summary>
    /// <param name="shape">共同判定使用的形状。</param>
    /// <param name="lines">每两个连续顶点构成一条线。</param>
    internal static void Append(in AbilityCapsule shape, List<Vector3> lines)
    {
        if (shape.Prism != null || shape.IsBox)
        {
            var frame = shape.Prism?.Pose ?? new AbilityPrism.Frame(AbilityPrism.Rectangle,
                Matrix4x4.TRS(shape.A, Quaternion.identity, shape.HalfExtents));
            int side = frame.VertexCount / 2;
            for (int i = 0; i < side; i++)
            {
                int next = (i + 1) % side;
                Edge(lines, frame.Vertex(i), frame.Vertex(next));
                Edge(lines, frame.Vertex(i + side), frame.Vertex(next + side));
                Edge(lines, frame.Vertex(i), frame.Vertex(i + side));
            }
            return;
        }

        Vector3 axis = (shape.B - shape.A).normalized;
        if (axis == Vector3.zero)
        {
            Arc(lines, shape.A, Vector3.right * shape.Radius, Vector3.up * shape.Radius, CircleSteps);
            Arc(lines, shape.A, Vector3.up * shape.Radius, Vector3.forward * shape.Radius, CircleSteps);
            Arc(lines, shape.A, Vector3.forward * shape.Radius, Vector3.right * shape.Radius, CircleSteps);
            return;
        }

        Vector3 radial = Vector3.Cross(axis, Mathf.Abs(axis.z) < .9f ? Vector3.forward : Vector3.right).normalized;
        Vector3 x = radial * shape.Radius, y = Vector3.Cross(axis, radial) * shape.Radius;
        Arc(lines, shape.A, x, y, CircleSteps);
        Arc(lines, shape.B, x, y, CircleSteps);
        Vector3 pole = axis * shape.Radius;
        for (int i = 0; i < 4; i++)
        {
            Vector3 side = i == 0 ? x : i == 1 ? y : i == 2 ? -x : -y;
            Edge(lines, shape.A + side, shape.B + side);
            Arc(lines, shape.A, side, -pole, CircleSteps / 4);
            Arc(lines, shape.B, side, pole, CircleSteps / 4);
        }
    }

    /// <summary>添加线段，不增加厚度造成额外范围。</summary>
    /// <param name="lines">显示线段。</param><param name="a">起点。</param><param name="b">终点。</param>
    private static void Edge(List<Vector3> lines, Vector3 a, Vector3 b)
    {
        lines.Add(a);
        lines.Add(b);
    }

    /// <summary>用相同角步画圆或球顶四分之一圆。</summary>
    /// <param name="lines">显示线段。</param><param name="centre">圆心。</param>
    /// <param name="x">起点方向和半径。</param><param name="y">四分之一圆的末端方向和半径。</param>
    /// <param name="steps">此次需要的线段数。</param>
    private static void Arc(List<Vector3> lines, Vector3 centre, Vector3 x, Vector3 y, int steps)
    {
        Vector3 from = centre + x;
        for (int i = 1; i <= steps; i++)
        {
            float angle = i * (360f / CircleSteps) * Mathf.Deg2Rad;
            Vector3 to = centre + x * Mathf.Cos(angle) + y * Mathf.Sin(angle);
            Edge(lines, from, to);
            from = to;
        }
    }
}

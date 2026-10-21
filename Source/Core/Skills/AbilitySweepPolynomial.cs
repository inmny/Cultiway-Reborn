using System;
using MathNet.Numerics;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>长轴增长或近切接触收敛过慢时，求出线性端点及半径的实际相交时点。</summary>
internal static class AbilitySweepPolynomial
{
    /// <summary>胶囊两端分别解球面，中段解距离方程；求根使用项目已有MathNet库。</summary>
    /// <param name="from">本步之前的形状。</param>
    /// <param name="to">本步结束的形状。</param>
    /// <param name="centre">目标中心。</param>
    /// <param name="radius">目标半径。</param>
    /// <param name="point">接触附近轴线点。</param>
    /// <param name="fraction">最早有效接触进度。</param>
    internal static bool Find(in AbilityCapsule from, in AbilityCapsule to, Vector3 centre, float radius,
        out Vector3 point, out float fraction)
    {
        // 距离平方先在双精度展开；中段乘以轴长平方，得到最多四次的多项式。
        Vector3 a = from.A, b = from.B, va = to.A - a, vb = to.B - b;
        Vector3 u = b - a, vu = vb - va, w = centre - a;
        double r = from.Radius + radius, dr = to.Radius - from.Radius;
        double d0 = Dot(u, u), d1 = 2 * Dot(u, vu), d2 = Dot(vu, vu);
        double q0 = Dot(w, u), q1 = Dot(w, vu) - Dot(va, u), q2 = -Dot(va, vu);
        double c0 = Dot(w, w) - r * r, c1 = -2 * Dot(w, va) - 2 * r * dr, c2 = Dot(va, va) - dr * dr;
        double best = double.PositiveInfinity;
        Vector3 closest = to.A;
        // 先取两端候选，再取中段；最后用真正的有限线段验证，排除无限直线上的伪根。
        Consider(new[] { c0, c1, c2 });
        Vector3 end = centre - b;
        Consider(new[] { Dot(end, end) - r * r, -2 * Dot(end, vb) - 2 * r * dr, Dot(vb, vb) - dr * dr });
        Consider(new[] {
            c0 * d0 - q0 * q0,
            c0 * d1 + c1 * d0 - 2 * q0 * q1,
            c0 * d2 + c1 * d1 + c2 * d0 - q1 * q1 - 2 * q0 * q2,
            c1 * d2 + c2 * d1 - 2 * q1 * q2,
            c2 * d2 - q2 * q2 });
        point = closest; fraction = double.IsPositiveInfinity(best) ? 1f : (float)best;
        return !double.IsPositiveInfinity(best);

        void Consider(double[] coefficients)
        {
            double magnitude = 0;
            foreach (double coefficient in coefficients) magnitude = Math.Max(magnitude, Math.Abs(coefficient));
            if (magnitude == 0d) return;
            for (int i = 0; i < coefficients.Length; i++) coefficients[i] /= magnitude;
            foreach (var root in FindRoots.Polynomial(coefficients))
            {
                double t = root.Real;
                if (Math.Abs(root.Imaginary) > .000001d || t < 0d || t > 1d || t >= best) continue;
                Vector3 start = a + va * (float)t, finish = b + vb * (float)t, axis = finish - start;
                double length = Dot(axis, axis);
                float along = length <= 1e-20d ? 0f : (float)Math.Max(0d, Math.Min(1d, Dot(centre - start, axis) / length));
                Vector3 candidate = start + axis * along;
                double limit = r + dr * t + .00001d;
                if (Dot(centre - candidate, centre - candidate) > limit * limit) continue;
                best = t; closest = candidate;
            }
        }
    }

    /// <summary>在乘法前转为双精度，减少细长形体距离方程的消去误差。</summary>
    /// <param name="a">左侧向量。</param>
    /// <param name="b">右侧向量。</param>
    private static double Dot(Vector3 a, Vector3 b) => (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;
}

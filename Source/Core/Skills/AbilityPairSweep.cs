using System;
using MathNet.Numerics;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>两个同时运动、变径的真实胶囊之间的连续接触。</summary>
internal static class AbilityPairSweep
{
    /// <summary>沿射线起端扫过真实厚度，求前方首先碰到的表面及包含端帽的整体长度。</summary>
    /// <param name="ray">同一时点的完整射线。</param><param name="target">实际阻挡形状。</param>
    /// <param name="length">从原起点量取的整体长度。</param><param name="point">射线表面接触点。</param>
    /// <param name="normal">射线指向阻挡的方向。</param>
    internal static bool RayEntry(in AbilityCapsule ray, in AbilityCapsule target, out float length,
        out Vector3 point, out Vector3 normal)
    {
        var start = new AbilityCapsule(0, ray.A, ray.A, ray.Radius, null);
        var end = new AbilityCapsule(0, ray.B, ray.B, ray.Radius, null);
        bool found = Find(start, end, target, target, out float at, out point, out normal);
        length = ray.Radius * 2f + Vector3.Distance(ray.A, ray.B) * at;
        return found;
    }

    /// <summary>从同一个交锋表面求另一射线的实际长度，不给双方各自制造不同的交锋点。</summary>
    /// <param name="ray">实际射线。</param><param name="point">共同接触点。</param>
    /// <param name="normal">本射线朝向另一侧的表面方向。</param>
    internal static float RayLengthAt(in AbilityCapsule ray, Vector3 point, Vector3 normal) =>
        Mathf.Clamp(Vector3.Dot(point - normal * ray.Radius - ray.A, (ray.B - ray.A).normalized) + ray.Radius * 2f,
            0f, Vector3.Distance(ray.A, ray.B) + ray.Radius * 2f);

    /// <summary>在共同时间段寻找首次接触，先按距离安全前进，难收敛时核对距离方程的根。</summary>
    /// <param name="a0">左侧前态。</param>
    /// <param name="a1">左侧后态。</param>
    /// <param name="b0">右侧前态。</param>
    /// <param name="b1">右侧后态。</param>
    /// <param name="fraction">首次接触进度。</param>
    /// <param name="point">左侧实际表面接触点。</param>
    /// <param name="normal">左侧指向右侧的接触方向。</param>
    internal static bool Find(in AbilityCapsule a0, in AbilityCapsule a1, in AbilityCapsule b0, in AbilityCapsule b1,
        out float fraction, out Vector3 point, out Vector3 normal)
    {
        float speed = Mathf.Max((a1.A - a0.A).magnitude, (a1.B - a0.B).magnitude) +
            Mathf.Max((b1.A - b0.A).magnitude, (b1.B - b0.B).magnitude) +
            Mathf.Abs(a1.Radius - a0.Radius) + Mathf.Abs(b1.Radius - b0.Radius);
        speed += (a1.HalfExtents - a0.HalfExtents).magnitude + (b1.HalfExtents - b0.HalfExtents).magnitude;
        float at = 0f; bool found = false;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            float distance = Separation(a0, a1, b0, b1, at, out _, out _);
            if (distance <= .00001f) { found = true; break; }
            if (speed <= 1e-10f) break;
            at += distance / speed;
            if (at > 1f) break;
        }
        if (!found && at <= 1f && speed > 1e-10f)
            found = a1.IsBox || b1.IsBox ? AbilityBoxSweep.Find(a0, a1, b0, b1, out at) : Roots(a0, a1, b0, b1, out at);
        fraction = at; point = normal = Vector3.zero;
        if (!found) return false;
        Separation(a0, a1, b0, b1, at, out Vector3 a, out Vector3 b);
        normal = (b - a).normalized;
        if (normal.sqrMagnitude < 1e-10f)
            normal = ((a1.A + a1.B - a0.A - a0.B) - (b1.A + b1.B - b0.A - b0.B)).normalized;
        point = a + normal * Mathf.Lerp(a0.Radius, a1.Radius, at);
        return true;
    }

    /// <summary>在同一时点求两个有限轴段之间的实际表面距离。</summary>
    /// <param name="a0">左前态。</param>
    /// <param name="a1">左后态。</param>
    /// <param name="b0">右前态。</param>
    /// <param name="b1">右后态。</param>
    /// <param name="at">共同进度。</param>
    /// <param name="a">左轴最近点。</param>
    /// <param name="b">右轴最近点。</param>
    private static float Separation(in AbilityCapsule a0, in AbilityCapsule a1, in AbilityCapsule b0,
        in AbilityCapsule b1, float at, out Vector3 a, out Vector3 b)
    {
        if (a1.IsBox || b1.IsBox) return AbilityBoxContact.Distance(a0, a1, b0, b1, at, out a, out b);
        Closest(Vector3.Lerp(a0.A, a1.A, at), Vector3.Lerp(a0.B, a1.B, at),
            Vector3.Lerp(b0.A, b1.A, at), Vector3.Lerp(b0.B, b1.B, at), out a, out b);
        return Vector3.Distance(a, b) - Mathf.Lerp(a0.Radius + b0.Radius, a1.Radius + b1.Radius, at);
    }

    /// <summary>求两个有限线段的最近点，零长轴作为球心处理。</summary>
    /// <param name="p">左起点。</param>
    /// <param name="q">左终点。</param>
    /// <param name="r">右起点。</param>
    /// <param name="s">右终点。</param>
    /// <param name="left">左最近点。</param>
    /// <param name="right">右最近点。</param>
    internal static void Closest(Vector3 p, Vector3 q, Vector3 r, Vector3 s, out Vector3 left, out Vector3 right)
    {
        Vector3 u = q - p, v = s - r, w = p - r;
        float a = Vector3.Dot(u, u), e = Vector3.Dot(v, v), f = Vector3.Dot(v, w), x, y;
        if (a <= 1e-12f && e <= 1e-12f) { left = p; right = r; return; }
        if (a <= 1e-12f) { x = 0f; y = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(u, w);
            if (e <= 1e-12f) { y = 0f; x = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(u, v), denominator = a * e - b * b;
                x = denominator > 1e-12f * a * e ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                y = (b * x + f) / e;
                if (y < 0f) { y = 0f; x = Mathf.Clamp01(-c / a); }
                else if (y > 1f) { y = 1f; x = Mathf.Clamp01((b - c) / a); }
            }
        }
        left = p + u * x; right = r + v * y;
    }

    /// <summary>端点到轴段的四组方程，以及两条轴线内部距离方程，覆盖有限胶囊的最近部位。</summary>
    /// <param name="a0">左前态。</param>
    /// <param name="a1">左后态。</param>
    /// <param name="b0">右前态。</param>
    /// <param name="b1">右后态。</param>
    /// <param name="at">最早的有效根。</param>
    private static bool Roots(AbilityCapsule a0, AbilityCapsule a1, AbilityCapsule b0, AbilityCapsule b1, out float at)
    {
        float best = float.PositiveInfinity;
        Endpoint(a0.A, a1.A, b0, b1); Endpoint(a0.B, a1.B, b0, b1);
        Endpoint(b0.A, b1.A, a0, a1); Endpoint(b0.B, b1.B, a0, a1);
        Vector3 u0 = a0.B - a0.A, u1 = a1.B - a1.A - u0;
        Vector3 v0 = b0.B - b0.A, v1 = b1.B - b1.A - v0;
        Vector3 w0 = b0.A - a0.A, w1 = b1.A - a1.A - w0;
        Vector3[] cross = { Vector3.Cross(u0, v0), Vector3.Cross(u0, v1) + Vector3.Cross(u1, v0), Vector3.Cross(u1, v1) };
        var triple = new double[4]; var crossSquared = new double[5];
        for (int i = 0; i < 3; i++)
        {
            triple[i] += Dot(w0, cross[i]); triple[i + 1] += Dot(w1, cross[i]);
            for (int j = 0; j < 3; j++) crossSquared[i + j] += Dot(cross[i], cross[j]);
        }
        double radius = a0.Radius + b0.Radius, delta = a1.Radius + b1.Radius - radius;
        double[] r2 = { radius * radius, 2d * radius * delta, delta * delta };
        var polynomial = new double[7];
        for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) polynomial[i + j] += triple[i] * triple[j];
        for (int i = 0; i < 5; i++) for (int j = 0; j < 3; j++) polynomial[i + j] -= crossSquared[i] * r2[j];
        double scale = 0d;
        foreach (double value in polynomial) scale = Math.Max(scale, Math.Abs(value));
        if (scale > 0d)
        {
            for (int i = 0; i < polynomial.Length; i++) polynomial[i] /= scale;
            foreach (var root in FindRoots.Polynomial(polynomial))
                if (Math.Abs(root.Imaginary) < 1e-6 && root.Real >= 0d && root.Real <= 1d && root.Real < best &&
                    Separation(a0, a1, b0, b1, (float)root.Real, out _, out _) <= .00002f)
                    best = (float)root.Real;
        }
        at = best; return !float.IsPositiveInfinity(best);

        void Endpoint(Vector3 p0, Vector3 p1, AbilityCapsule c0, AbilityCapsule c1)
        {
            var from = new AbilityCapsule(0, c0.A - p0, c0.B - p0, a0.Radius + b0.Radius, null);
            var to = new AbilityCapsule(0, c1.A - p1, c1.B - p1, a1.Radius + b1.Radius, null);
            if (AbilitySweepPolynomial.Find(from, to, Vector3.zero, 0f, out _, out float value)) best = Mathf.Min(best, value);
        }
    }

    /// <summary>保留多项式系数的双精度点积。</summary>
    /// <param name="a">左向量。</param>
    /// <param name="b">右向量。</param>
    private static double Dot(Vector3 a, Vector3 b) => (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;
}

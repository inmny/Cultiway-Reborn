using System.Collections.Generic;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>局部作用面，按显示模型的同一顺序拆成三角形。</summary>
internal sealed class AbilityMeshSurface
{
    internal readonly Vector3[] Triangles;
    internal readonly Bounds Bounds;

    internal AbilityMeshSurface(IEnumerable<Vector3[]> faces)
    {
        var triangles = new List<Vector3>();
        Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
        foreach (Vector3[] face in faces)
        {
            for (int i = 1; i < face.Length - 1; i++)
            {
                triangles.Add(face[0]);
                triangles.Add(face[i]);
                triangles.Add(face[i + 1]);
            }
            foreach (Vector3 point in face)
            {
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
        }

        Triangles = triangles.ToArray();
        if (Triangles.Length == 0) return;
        var bounds = new Bounds();
        bounds.SetMinMax(min, max);
        Bounds = bounds;
    }
}

/// <summary>以同一份模型姿态连续检查真实表面，包围范围只负责筛选。</summary>
internal readonly struct AbilityMeshSweep
{
    private readonly AbilityMeshSurface surface;
    private readonly Matrix4x4 before, after;
    private readonly AbilitySweepTurn turn;
    private readonly float speed;
    internal readonly Bounds Bounds;

    internal bool IsActive => surface != null;

    internal AbilityMeshSweep(AbilityMeshSurface surface, in SpatialVisualMeshInstance from,
        in SpatialVisualMeshInstance to, AbilityHeightField terrain)
    {
        this.surface = surface;
        Vector3 start = AtTerrain(from, terrain), end = AtTerrain(to, terrain);
        before = Matrix4x4.TRS(start, from.Rotation, from.Scale);
        after = Matrix4x4.TRS(end, to.Rotation, to.Scale);
        turn = new AbilitySweepTurn(new AbilitySweepPose(start, from.Rotation), new AbilitySweepPose(end, to.Rotation));
        speed = 0f;
        float padding = 0f;
        var bounds = new Bounds(before.MultiplyPoint3x4(surface.Bounds.min), Vector3.zero);
        Vector3 min = surface.Bounds.min, max = surface.Bounds.max;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new((i & 1) == 0 ? min.x : max.x,
                (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            Vector3 a = before.MultiplyPoint3x4(corner), b = after.MultiplyPoint3x4(corner);
            turn.Measure(a, b, out float travel, out float deviation);
            speed = Mathf.Max(speed, travel);
            padding = Mathf.Max(padding, deviation);
            bounds.Encapsulate(a);
            bounds.Encapsulate(b);
        }
        bounds.Expand(padding * 2f + .00004f);
        Bounds = bounds;
    }

    internal bool Touches(in AbilityCapsule target, bool plane)
    {
        Bounds limits = Bounds;
        if (plane)
        {
            Vector3 center = limits.center, size = limits.size;
            center.z = size.z = 0f;
            limits = new Bounds(center, size);
        }
        if (!limits.Intersects(AbilityContactIndex.Swept(target, target))) return false;

        float at = 0f;
        for (int step = 0; step < 128; step++)
        {
            Matrix4x4 frame = turn.Frame(before, after, at);
            // 建筑仍按平面占地判断，刃部先按真实的俯仰和转向移动，再投到平面。
            if (plane) frame.SetRow(2, Vector4.zero);
            float nearest = float.PositiveInfinity;
            Vector3[] points = surface.Triangles;
            for (int i = 0; i < points.Length; i += 3)
            {
                float distance = AbilityTriangleSweep.Segment(target.A, target.B, frame.MultiplyPoint3x4(points[i]),
                    frame.MultiplyPoint3x4(points[i + 1]), frame.MultiplyPoint3x4(points[i + 2]), out _, out _);
                if (distance <= target.Radius + .00001f) return true;
                nearest = Mathf.Min(nearest, distance);
            }

            // 最近面距离与任意顶点的最大移动速度给出下一步的安全前进量。
            if (speed <= 1e-10f) return false;
            at += (nearest - target.Radius) / speed;
            if (at > 1f) return false;
        }

        return false;
    }

    private static Vector3 AtTerrain(in SpatialVisualMeshInstance pose, AbilityHeightField terrain)
        => pose.Position + Vector3.forward * (terrain?.Sample(pose.GroundAnchor.x, pose.GroundAnchor.y) ?? 0f);
}

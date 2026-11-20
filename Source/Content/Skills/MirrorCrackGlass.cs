using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

/// <summary>同一组裂纹划出的实际玻璃片。镜碎后原裂片各自下坠。</summary>
internal sealed class MirrorCrackGlass : IDisposable
{
    /// <summary>两组不同明暗的磨损玻璃面。</summary>
    private readonly GlassLayer dark = new("mirror_cracked_dark");
    private readonly GlassLayer light = new("mirror_cracked_light");

    /// <summary>按已抵达的距离生长玻璃切片。碎裂后保留这批切片形状和原位。</summary>
    internal void Append(FormResource resource, Matrix4x4 mirror, Vector3 centre, Vector2 origin,
        Vector2[] ends, float length, float falling, AbilityHeightField terrain, List<FormVisualInstance> output)
    {
        dark.Clear();
        light.Clear();
        Vector3 front = ((Vector3)mirror.GetColumn(1)).normalized;
        for (int i = 0; i < ends.Length; i++)
        {
            Vector2 left = origin + Vector2.ClampMagnitude(ends[i] - origin, length);
            Vector2 right = origin + Vector2.ClampMagnitude(ends[(i + 1) % ends.Length] - origin, length);
            if (Vector2.Distance(left, origin) < .007f || Vector2.Distance(right, origin) < .007f) continue;

            // 裂片略微分开。片面有厚度，玻璃主体与缝都能读出。
            Vector2 middle = (origin + left + right) / 3f;
            const float bevel = .022f;
            Vector3 a = Point(Vector2.Lerp(origin, middle, bevel));
            Vector3 b = Point(Vector2.Lerp(left, middle, bevel));
            Vector3 c = Point(Vector2.Lerp(right, middle, bevel));
            Vector3 delta = ((a + b + c) / 3f - Point(origin)).normalized;
            (i % 3 == 1 ? light : dark).Add(a, b, c, front, delta, falling, i);
        }

        dark.Publish(resource, falling, terrain, output);
        light.Publish(resource, falling, terrain, output);

        Vector3 Point(Vector2 point) => mirror.MultiplyPoint3x4(centre + new Vector3(point.x, -.019f, point.y));
    }

    /// <summary>网格只属于本次形成的镜片。</summary>
    public void Dispose()
    {
        dark.Dispose();
        light.Dispose();
    }

    /// <summary>一组同色的实际玻璃三棱柱。</summary>
    private sealed class GlassLayer : IDisposable
    {
        private readonly string surface;
        private readonly List<Vector3> points = new();
        private readonly List<int> faces = new();
        private Mesh mesh;

        internal GlassLayer(string surface) => this.surface = surface;
        internal void Clear() { points.Clear(); faces.Clear(); }

        internal void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Vector3 outward, float falling, int index)
        {
            Vector3 centre = (a + b + c) / 3f;
            Quaternion spin = Quaternion.AngleAxis(falling * (index % 2 == 0 ? 210f : -165f), (b - a).normalized);
            Vector3 shift = outward * falling * 1.45f - Vector3.forward * (falling * falling * 3.1f);
            Vector3 start = centre + shift;
            Vector3 slab = spin * normal * .021f;
            int next = points.Count;
            Vector3 first = start + spin * (a - centre), second = start + spin * (b - centre), third = start + spin * (c - centre);
            points.Add(first); points.Add(second); points.Add(third);
            points.Add(first + slab); points.Add(second + slab); points.Add(third + slab);
            Face(0, 1, 2); Face(5, 4, 3);
            Face(0, 3, 1); Face(1, 3, 4);
            Face(1, 4, 2); Face(2, 4, 5);
            Face(2, 5, 0); Face(0, 5, 3);

            void Face(int from, int to, int end)
            {
                faces.Add(next + from); faces.Add(next + to); faces.Add(next + end);
            }
        }

        internal void Publish(FormResource resource, float falling, AbilityHeightField terrain, List<FormVisualInstance> output)
        {
            if (faces.Count == 0) return;
            if (mesh == null)
            {
                mesh = new Mesh { name = "Mirror Glass" };
                mesh.MarkDynamic();
            }
            mesh.SetVertices(points);
            mesh.SetTriangles(faces, 0);
            mesh.RecalculateNormals();
            Color? tint = null;
            if (falling > 0f)
            {
                Color colour = resource.Surface(surface, false).GetColor("_Color");
                colour.a *= 1f - Mathf.SmoothStep(0f, 1f, falling / .55f);
                tint = colour;
            }
            output.Add(new FormVisualInstance(resource, mesh, Matrix4x4.identity, tint, terrain, surface));
        }

        public void Dispose()
        {
            if (mesh != null) Object.Destroy(mesh);
        }
    }
}

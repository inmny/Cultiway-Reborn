using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>有限厚度的凸形作用面。轮廓留在制作坐标，姿态取自部位的实际变换。</summary>
internal sealed class AbilityPrism
{
    internal static readonly Vector2[] Rectangle = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };
    private readonly Vector2[] outline;
    private readonly Matrix4x4 transform;

    internal AbilityPrism(Vector2[] outline, Matrix4x4 transform)
    {
        this.outline = outline;
        this.transform = transform;
    }

    internal Frame Pose => new(outline, transform);
    internal AbilityPrism Move(Matrix4x4 world) => new(outline, world * transform);
    internal AbilityPrism At(AbilityPrism to, float at) => at <= 0f ? this : at >= 1f ? to : new(outline, Between(to, at));

    private Matrix4x4 Between(AbilityPrism to, float at)
    {
        var matrix = new Matrix4x4();
        for (int column = 0; column < 4; column++)
            matrix.SetColumn(column, Vector4.LerpUnclamped(transform.GetColumn(column), to.transform.GetColumn(column), at));
        return matrix;
    }

    internal float MotionTo(AbilityPrism to)
    {
        float maximum = 0f;
        Frame a = Pose, b = to.Pose;
        for (int i = 0; i < a.VertexCount; i++) maximum = Mathf.Max(maximum, (b.Vertex(i) - a.Vertex(i)).sqrMagnitude);
        return Mathf.Sqrt(maximum);
    }

    internal Vector3 VelocityTo(AbilityPrism to, Vector3 point, float at)
    {
        Vector3 local = Between(to, at).inverse.MultiplyPoint3x4(point);
        return to.transform.MultiplyPoint3x4(local) - transform.MultiplyPoint3x4(local);
    }

    internal static Frame Sample(in AbilityCapsule from, in AbilityCapsule to, float at)
    {
        if (to.Prism != null) return new Frame(to.Prism.outline, from.Prism.Between(to.Prism, at));
        return new Frame(Rectangle, Matrix4x4.TRS(Vector3.Lerp(from.A, to.A, at), Quaternion.identity,
            Vector3.Lerp(from.HalfExtents, to.HalfExtents, at)));
    }

    /// <summary>同一时点的形状，距离查询只读数值并复用同一局部轮廓。</summary>
    internal readonly struct Frame
    {
        private readonly Vector2[] outline;
        private readonly Matrix4x4 localToWorld;
        internal int VertexCount => outline.Length * 2;
        private int FaceCount => 4 * outline.Length - 4;
        private int EdgeCount => 3 * outline.Length;

        internal Frame(Vector2[] outline, Matrix4x4 localToWorld)
        {
            this.outline = outline;
            this.localToWorld = localToWorld;
        }

        internal Vector3 Vertex(int index)
        {
            Vector2 p = outline[index % outline.Length];
            return localToWorld.MultiplyPoint3x4(new Vector3(p.x, index < outline.Length ? -1f : 1f, p.y));
        }

        internal Bounds Bounds()
        {
            var bounds = new Bounds(Vertex(0), Vector3.zero);
            for (int i = 1; i < VertexCount; i++) bounds.Encapsulate(Vertex(i));
            return bounds;
        }

        private void Face(int face, out Vector3 a, out Vector3 b, out Vector3 c)
        {
            int count = outline.Length, cap = count - 2;
            if (face < 2 * cap)
            {
                int first = face < cap ? 0 : count, offset = face % cap;
                a = Vertex(first); b = Vertex(first + offset + 1); c = Vertex(first + offset + 2);
                return;
            }
            face -= 2 * cap;
            int left = face / 2, right = (left + 1) % count;
            a = Vertex(left); c = Vertex(right + count);
            b = Vertex((face & 1) == 0 ? right : left + count);
        }

        private void Edge(int index, out Vector3 p, out Vector3 q)
        {
            int count = outline.Length, i = index % count, ring = index / count;
            p = Vertex(i + (ring == 1 ? count : 0));
            q = Vertex(ring == 2 ? i + count : (i + 1) % count + ring * count);
        }

        private bool Contains(Vector3 point, Matrix4x4 inverse)
        {
            Vector3 local = inverse.MultiplyPoint3x4(point);
            if (Mathf.Abs(local.y) > 1f) return false;
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                if ((b.x - a.x) * (local.z - a.y) < (b.y - a.y) * (local.x - a.x)) return false;
            }
            return true;
        }

        internal float Line(Vector3 start, Vector3 end, out Vector3 solid, out Vector3 line)
        {
            Matrix4x4 inverse = localToWorld.inverse;
            if (inverse.m33 != 0f && Contains(start, inverse)) { solid = line = start; return 0f; }
            if (inverse.m33 != 0f && Contains(end, inverse)) { solid = line = end; return 0f; }
            float best = float.PositiveInfinity;
            solid = line = default;
            for (int i = 0; i < FaceCount; i++)
            {
                Face(i, out var a, out var b, out var c);
                float distance = AbilityTriangleSweep.Segment(start, end, a, b, c, out var p, out var q);
                if (distance >= best) continue;
                best = distance; solid = q; line = p;
                if (best == 0f) break;
            }
            return best;
        }

        internal float Distance(Frame other, out Vector3 left, out Vector3 right)
        {
            float best = float.PositiveInfinity;
            left = right = default;
            for (int i = 0; i < other.EdgeCount; i++)
            {
                other.Edge(i, out var a, out var b);
                float distance = Line(a, b, out var p, out var q);
                if (distance >= best) continue;
                best = distance; left = p; right = q;
                if (best == 0f) return best;
            }
            for (int i = 0; i < EdgeCount; i++)
            {
                Edge(i, out var a, out var b);
                float distance = other.Line(a, b, out var q, out var p);
                if (distance >= best) continue;
                best = distance; left = p; right = q;
                if (best == 0f) return best;
            }
            return best;
        }

        internal float Triangle(Vector3 a, Vector3 b, Vector3 c, out Vector3 solid, out Vector3 face)
        {
            float best = Line(a, b, out solid, out face);
            float next = Line(b, c, out var p, out var q);
            if (next < best) { best = next; solid = p; face = q; }
            next = Line(c, a, out p, out q);
            if (next < best) { best = next; solid = p; face = q; }
            for (int i = 0; i < EdgeCount && best > 0f; i++)
            {
                Edge(i, out var start, out var end);
                next = AbilityTriangleSweep.Segment(start, end, a, b, c, out p, out q);
                if (next < best) { best = next; solid = p; face = q; }
            }
            return best;
        }
    }
}

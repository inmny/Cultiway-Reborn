using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>同一组关节和粗细同时交给显示与实际抱持判定。</summary>
internal readonly struct ShadowArm
{
    internal readonly Vector3 Shoulder, Elbow, Hand;
    internal readonly float Radius;

    internal ShadowArm(Vector3 shoulder, Vector3 elbow, Vector3 hand, float radius)
    {
        Shoulder = shoulder;
        Elbow = elbow;
        Hand = hand;
        Radius = radius;
    }

    internal AbilityCapsule Palm(int node) => new(node, Vector3.Lerp(Elbow, Hand, .72f), Hand, Radius, "yin");
}

/// <summary>每侧两截有圆头的影臂。顶点缓冲沿用至影身退场。</summary>
internal sealed class ShadowArms
{
    private const int Sides = 10;
    private const int Rings = 8;
    private const int Points = Rings * Sides;
    private readonly Vector3[] vertices = new Vector3[Points * 4];
    private readonly Vector3[] normals = new Vector3[Points * 4];
    private static readonly int[] triangles = Triangles();

    internal void Draw(Mesh mesh, in ShadowArm left, in ShadowArm right)
    {
        Segment(0, left.Shoulder, left.Elbow, left.Radius * .83f);
        Segment(Points, left.Elbow, left.Hand, left.Radius);
        Segment(Points * 2, right.Shoulder, right.Elbow, right.Radius * .83f);
        Segment(Points * 3, right.Elbow, right.Hand, right.Radius);
        bool first = mesh.vertexCount == 0;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        if (first) mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private void Segment(int offset, Vector3 start, Vector3 end, float radius)
    {
        Vector3 axis = (end - start).normalized;
        Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.z) < .9f ? Vector3.forward : Vector3.right).normalized;
        Vector3 across = Vector3.Cross(axis, side);
        for (int ring = 0; ring < Rings; ring++)
        {
            bool far = ring >= Rings / 2;
            float angle = (ring - (far ? Rings / 2 : Rings / 2 - 1)) * Mathf.PI / (Rings - 2);
            Vector3 centre = far ? end : start;
            for (int i = 0; i < Sides; i++)
            {
                float around = i * Mathf.PI * 2f / Sides;
                Vector3 normal = axis * Mathf.Sin(angle) + (side * Mathf.Cos(around) + across * Mathf.Sin(around)) * Mathf.Cos(angle);
                int index = offset + ring * Sides + i;
                vertices[index] = centre + normal * radius;
                normals[index] = normal;
            }
        }
    }

    private static int[] Triangles()
    {
        var order = new int[4 * (Rings - 1) * Sides * 6];
        int index = 0;
        for (int segment = 0; segment < 4; segment++)
            for (int ring = 0; ring < Rings - 1; ring++)
                for (int side = 0; side < Sides; side++)
                {
                    int a = segment * Points + ring * Sides + side, b = segment * Points + ring * Sides + (side + 1) % Sides;
                    order[index++] = a;
                    order[index++] = b;
                    order[index++] = a + Sides;
                    order[index++] = a + Sides;
                    order[index++] = b;
                    order[index++] = b + Sides;
                }
        return order;
    }
}

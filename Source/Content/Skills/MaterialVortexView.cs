using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

/// <summary>有实体宽面和深度的裂口。网格创建一次，推进时只发布实际中心与开合姿态。</summary>
internal sealed class MaterialVortexView : IDisposable
{
    /// <summary>依次放置外唇、暗腔及进入深处的短厚层。</summary>
    internal readonly Mesh Rim, Chamber, Throat;

    /// <summary>短厚层使用实际能通过散件的同一喉口比例。</summary>
    /// <param name="rule">实际世界尺寸。</param>
    internal MaterialVortexView(CompiledMaterialVortex rule)
    {
        float aperture = rule.Throat / rule.Radius;
        Rim = Build("Cultiway Vortex Rim", new[] { new Vector2(1.08f, -.13f), new Vector2(1.04f, .06f),
            new Vector2(.94f, .19f), new Vector2(.86f, .075f), new Vector2(.80f, -.09f) }, .12f, false);
        Chamber = Build("Cultiway Vortex Chamber", new[] { new Vector2(.88f, .02f), new Vector2(.71f, -.26f),
            new Vector2(.51f, -.54f), new Vector2(aperture * 1.1f, -.92f), new Vector2(0f, -1f) }, .085f, true);
        Throat = Build("Cultiway Vortex Throat", new[] { new Vector2(aperture * 2.7f, .28f),
            new Vector2(aperture * 2.05f, .18f), new Vector2(aperture * 1.42f, .10f),
            new Vector2(aperture, .018f), new Vector2(aperture * .79f, -.05f) }, .11f, true);
    }

    /// <summary>同一截面的多层实体面连续连接。有宽度的褶面在俯视和立体视角均可读。</summary>
    /// <param name="name">运行网格名。</param><param name="sections">各段实际半径与高度。</param>
    /// <param name="uneven">裂口轮廓起伏。</param><param name="twist">向内旋转的截面。</param>
    private static Mesh Build(string name, Vector2[] sections, float uneven, bool twist)
    {
        const int sides = 60;
        var points = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var index = new List<int>();
        for (int ring = 0; ring < sections.Length - 1; ring++)
        for (int side = 0; side < sides; side++)
        {
            float left = (float)side / sides, right = (float)(side + 1) / sides;
            Vector3 a = Point(ring, left), b = Point(ring, right);
            Vector3 c = Point(ring + 1, right), d = Point(ring + 1, left);
            int first = points.Count;
            points.Add(a); points.Add(b); points.Add(c); points.Add(d);
            float from = (float)ring / (sections.Length - 1), to = (float)(ring + 1) / (sections.Length - 1);
            uv.Add(new Vector2(left, from)); uv.Add(new Vector2(right, from));
            uv.Add(new Vector2(right, to)); uv.Add(new Vector2(left, to));
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal == Vector3.zero) normal = Vector3.Cross(c - a, d - a).normalized;
            for (int i = 0; i < 4; i++) normals.Add(normal);
            index.Add(first); index.Add(first + 2); index.Add(first + 1);
            index.Add(first); index.Add(first + 3); index.Add(first + 2);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(points);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(index, 0);
        return mesh;

        Vector3 Point(int section, float at)
        {
            float angle = at * Mathf.PI * 2f + (twist ? section * .11f : section * .028f);
            float fold = Mathf.Sin(angle * 5f + section * .53f) * .54f +
                         Mathf.Sin(angle * 3f - section * .37f) * .31f +
                         Mathf.Sin(angle * 11f + .3f) * .15f;
            Vector2 shape = sections[section];
            float radius = shape.x * (1f + uneven * fold);
            return new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius,
                shape.y + .15f * shape.x * fold);
        }
    }

    /// <summary>跟随实际施放回收所有临时网格。</summary>
    public void Dispose()
    {
        Object.Destroy(Rim);
        Object.Destroy(Chamber);
        Object.Destroy(Throat);
    }
}

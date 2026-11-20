using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal sealed partial class FormResourceGeneration
{
    /// <summary>随资源版本共享的种核和裂口网格。</summary>
    private Mesh solarGrains, solarLips;

    /// <summary>聚拢的颗粒各有厚度，低视角仍能辨认轮廓。</summary>
    internal Mesh SolarGrains => solarGrains ??= BuildSolarGrains();

    /// <summary>反击前撑开的两侧断面，中央留出与实际喷口对齐的缝。</summary>
    internal Mesh SolarLips => solarLips ??= BuildSolarLips();

    /// <summary>把松散颗粒排成深浅不同的两层，近处看得到颗粒之间的空隙。</summary>
    private Mesh BuildSolarGrains()
    {
        const int count = 34;
        var points = new List<Vector3>(count * 6);
        var uv = new List<Vector2>(count * 6);
        var triangles = new List<int>(count * 24);
        var axes = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back, Vector3.up, Vector3.down };
        for (int grain = 0; grain < count; grain++)
        {
            float height = (grain + .5f) / count * 2f - 1f;
            float angle = grain * 2.399963f;
            float spread = Mathf.Sqrt(1f - height * height) * (.60f + .13f * Mathf.Sin(angle * 2.2f));
            var centre = new Vector3(Mathf.Cos(angle) * spread, Mathf.Sin(angle) * spread, height * .70f);
            float size = .10f + .06f * Mathf.Abs(Mathf.Sin(angle * .67f));
            var tilt = Quaternion.Euler(angle * 68f, angle * 14f, angle * 24f);
            int start = points.Count;
            for (int corner = 0; corner < axes.Length; corner++)
            {
                points.Add(centre + tilt * Vector3.Scale(axes[corner], new Vector3(size, size * .64f, size * 1.40f)));
                uv.Add(new Vector2(grain * .27f, corner / 5f));
            }
            for (int side = 0; side < 4; side++)
            {
                int a = start + side, b = start + (side + 1) % 4;
                triangles.Add(a); triangles.Add(b); triangles.Add(start + 4);
                triangles.Add(b); triangles.Add(a); triangles.Add(start + 5);
            }
        }
        return OwnSolarMesh("Cultiway Solar Grains", points, uv, triangles);
    }

    /// <summary>扭开两块有厚度的断面，边缘参差但不额外生成漫天线条。</summary>
    private Mesh BuildSolarLips()
    {
        const int rows = 13, faces = 4;
        var points = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        var profile = new[] { new Vector2(0f, .24f), new Vector2(.38f, .05f),
            new Vector2(.27f, -.42f), new Vector2(0f, -.25f) };
        for (int side = -1; side <= 1; side += 2)
        for (int row = 0; row < rows - 1; row++)
        for (int face = 0; face < faces; face++)
        {
            int next = (face + 1) % faces;
            int first = points.Count;
            points.Add(Vertex(row, face, side)); points.Add(Vertex(row + 1, face, side));
            points.Add(Vertex(row + 1, next, side)); points.Add(Vertex(row, next, side));
            float along = row / (rows - 1f), end = (row + 1) / (rows - 1f);
            uv.Add(new Vector2(along, 0f)); uv.Add(new Vector2(end, 0f));
            uv.Add(new Vector2(end, 1f)); uv.Add(new Vector2(along, 1f));
            triangles.Add(first); triangles.Add(first + (side > 0 ? 1 : 2)); triangles.Add(first + (side > 0 ? 2 : 1));
            triangles.Add(first); triangles.Add(first + (side > 0 ? 2 : 3)); triangles.Add(first + (side > 0 ? 3 : 2));
        }
        return OwnSolarMesh("Cultiway Solar Rupture", points, uv, triangles);

        Vector3 Vertex(int row, int face, int side)
        {
            float along = row / (rows - 1f);
            float arch = Mathf.Sin(Mathf.PI * along);
            float edge = .32f + arch * .23f + Mathf.Sin(row * 2.16f + side) * .075f;
            Vector2 corner = profile[face] * Mathf.Lerp(.26f, 1f, arch);
            return new Vector3(side * (edge + corner.x), (along - .5f) * 1.95f, corner.y - .18f * arch);
        }
    }

    /// <summary>把生成的共享网格交给所属资源版本回收。</summary>
    /// <param name="name">用于检查的实际网格名。</param>
    /// <param name="points">实际模型坐标。</param>
    /// <param name="uv">材质坐标。</param>
    /// <param name="triangles">按表面法线排列的三角面。</param>
    private Mesh OwnSolarMesh(string name, List<Vector3> points, List<Vector2> uv, List<int> triangles)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(points);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        Own(mesh);
        return mesh;
    }
}

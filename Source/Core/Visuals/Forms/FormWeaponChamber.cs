using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal sealed partial class FormResourceGeneration
{
    private Mesh weaponChamberShell, weaponChamberLip, weaponChamberJaw;

    internal Mesh WeaponChamberShell => weaponChamberShell ??= ChamberRings("器腹", true, new[]
    {
        new Vector2(.02f, -1f), new Vector2(.43f, -.98f), new Vector2(.72f, -.78f),
        new Vector2(.85f, -.39f), new Vector2(.84f, -.18f), new Vector2(1.07f, .01f),
        new Vector2(1f, .02f), new Vector2(.74f, -.22f), new Vector2(.53f, -.66f),
        new Vector2(.23f, -.87f), new Vector2(.02f, -.88f)
    });

    internal Mesh WeaponChamberLip => weaponChamberLip ??= ChamberRings("器口厚沿", false, new[]
    {
        new Vector2(1f, .01f), new Vector2(1.08f, -.015f), new Vector2(1.18f, .07f),
        new Vector2(1.17f, .17f), new Vector2(1.085f, WeaponChamberFrame.LipDepth),
        new Vector2(1f, WeaponChamberFrame.LipDepth)
    });

    internal Mesh WeaponChamberJaw => weaponChamberJaw ??= BuildWeaponChamberJaw();

    /// <summary>同一剖面形成厚壁与空腔。外壳八个折面，器口内沿为实际圆面。</summary>
    private Mesh ChamberRings(string label, bool faceted, IReadOnlyList<Vector2> profile)
    {
        const int segments = 48;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int layer = 0; layer < profile.Count; layer++)
        for (int side = 0; side <= segments; side++)
        {
            float turn = (float)side / segments, angle = turn * 2f * Mathf.PI;
            float bevel = faceted && layer is > 0 and < 5
                ? Mathf.Cos(Mathf.PI / 8f) / Mathf.Cos((Mathf.Repeat(turn * 8f, 1f) - .5f) * Mathf.PI / 4f)
                : 1f;
            float radius = profile[layer].x * bevel;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, profile[layer].y));
            uv.Add(new Vector2(turn * 4f, .82f + profile[layer].y));
            if (layer == 0 || side == 0) continue;
            int current = vertices.Count - 1, previous = current - segments - 1;
            triangles.Add(previous - 1); triangles.Add(previous); triangles.Add(current);
            triangles.Add(previous - 1); triangles.Add(current); triangles.Add(current - 1);
        }
        return OwnWeaponChamberMesh(label, vertices, uv, triangles);
    }

    /// <summary>上下夹页各是一块有厚度的半圆板，绕器口肩位打开。</summary>
    private Mesh BuildWeaponChamberJaw()
    {
        const int segments = 16;
        var vertices = new List<Vector3> { new(0f, .025f, .07f), new(0f, .025f, .14f) };
        var uv = new List<Vector2> { Vector2.zero, Vector2.zero };
        var triangles = new List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.PI * i / segments;
            var xy = new Vector2(Mathf.Cos(angle) * .95f, Mathf.Sin(angle) * .92f + .025f);
            vertices.Add(new Vector3(xy.x, xy.y, .07f)); vertices.Add(new Vector3(xy.x, xy.y, .14f));
            uv.Add(xy); uv.Add(xy);
            if (i == 0) continue;
            int a = 2 * i;
            triangles.Add(0); triangles.Add(a + 2); triangles.Add(a);
            triangles.Add(1); triangles.Add(a + 1); triangles.Add(a + 3);
            triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 3);
            triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 1);
        }
        int last = 2 * segments + 2;
        triangles.Add(last); triangles.Add(2); triangles.Add(3);
        triangles.Add(last); triangles.Add(3); triangles.Add(last + 1);
        return OwnWeaponChamberMesh("器口夹页", vertices, uv, triangles);
    }

    private Mesh OwnWeaponChamberMesh(string label, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        var mesh = new Mesh { name = label };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        Own(mesh);
        return mesh;
    }
}

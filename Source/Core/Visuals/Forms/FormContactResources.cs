using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal sealed partial class FormResourceGeneration
{
    /// <summary>真实范围反馈使用的单位球体，随资源版本释放，不列入攻击形体目录。</summary>
    internal FormResource ContactVolume { get; private set; }

    /// <summary>发布资源组前生成一次平滑单位球；显示只缩放至真实半径，不另算作用。</summary>
    private void CreateContactVolume()
    {
        const int rings = 16, sides = 24;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int y = 0; y <= rings; y++)
            for (int x = 0; x <= sides; x++)
            {
                float v = (float)y / rings, u = (float)x / sides, latitude = v * Mathf.PI, angle = u * Mathf.PI * 2;
                vertices.Add(new Vector3(Mathf.Cos(angle) * Mathf.Sin(latitude), Mathf.Sin(angle) * Mathf.Sin(latitude), -Mathf.Cos(latitude)));
                uv.Add(new Vector2(u, v));
                if (x == sides || y == rings) continue;
                int a = y * (sides + 1) + x, b = a + sides + 1;
                triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
                triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
            }
        var mesh = new Mesh { name = "Cultiway_contact_volume", hideFlags = HideFlags.DontSave };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); Own(mesh);
        var data = new FormDescription
        {
            Version = FormDescription.FormatVersion, Id = "contact_volume", Forward = new[] { 0f, 1f, 0f }, Up = new[] { 0f, 0f, 1f },
            Structure = new FormStructureDescription
            {
                Kind = "volume", Changes = new[] { "transform" },
                Generation = new FormGenerationDescription { Source = "Source/Core/Visuals/Forms/FormContactResources.cs", Entry = nameof(CreateContactVolume),
                    Parameters = new[] { new FormParameterDescription { Id = "sampling", Values = new float[] { rings, sides } } } }
            },
            Parts = new[] { new FormPartDescription { Id = "body", Roles = new[] { "contact.volume" }, Surface = "spirit_core", Pivot = new float[3] } },
            Anchors = Array.Empty<FormAnchorDescription>(), Controls = Array.Empty<FormControlDescription>(),
            Motions = Array.Empty<FormMotionDescription>(), Contacts = Array.Empty<FormContactDescription>()
        };
        ContactVolume = new FormResource(data, new[] { mesh }, this);
        references.Add(new WeakReference<FormResource>(ContactVolume));
        Surface("spirit_core", true); Surface("spirit_core", false);
    }
}

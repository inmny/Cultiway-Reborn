using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>有限火体和真实蒸散使用共用资源组，重载与释放沿用普通形体规则。</summary>
internal sealed partial class FormResourceGeneration
{
    /// <summary>本资源组的纯显示蒸散，不列入玩家可选择的攻击形体。</summary>
    internal FormResource Steam { get; private set; }

    /// <summary>在资源组发布前建立可复用的反应形体，不在碰撞或工作线程创建网格。</summary>
    internal void CreateReactionForms()
    {
        CreateReactionForm("fire_body", "flame", true);
        CreateReactionForm("reaction_steam", "foam", false);
    }

    /// <summary>生成具有厚度的低成本曲面，火体参与普通构造，蒸散只有显示。</summary>
    /// <param name="id">稳定形体编号。</param><param name="surface">本资源的默认表面。</param>
    /// <param name="fire">是否建立火体接触形状。</param>
    private void CreateReactionForm(string id, string surface, bool fire)
    {
        const int rings = 10, sides = 16;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int y = 0; y <= rings; y++)
            for (int x = 0; x <= sides; x++)
            {
                float v = (float)y / rings, u = (float)x / sides, angle = u * Mathf.PI * 2;
                float width = Mathf.Sin(v * Mathf.PI) * (fire ? Mathf.Lerp(.55f, .25f, v) : .5f);
                float ripple = 1 + .07f * Mathf.Sin(angle * 3 + v * 8);
                vertices.Add(new Vector3(Mathf.Cos(angle) * width * ripple, Mathf.Sin(angle) * width * ripple,
                    v * (fire ? 1.6f : 1f)));
                uv.Add(new Vector2(u, v));
                if (x == sides || y == rings) continue;
                int a = y * (sides + 1) + x, b = a + sides + 1;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(b + 1);
                triangles.Add(b);
            }

        var mesh = new Mesh { name = "Cultiway_" + id, hideFlags = HideFlags.DontSave };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Own(mesh);
        var data = new FormDescription
        {
            Version = FormDescription.FormatVersion,
            Id = id,
            Forward = new[] { 0f, 1f, 0f },
            Up = new[] { 0f, 0f, 1f },
            Structure = new FormStructureDescription
            {
                Kind = "volume",
                Generation = new FormGenerationDescription
                {
                    Source = "Source/Core/Visuals/Forms/FormReactionResources.cs",
                    Entry = nameof(CreateReactionForm),
                    Parameters = new[] { new FormParameterDescription { Id = "sampling", Values = new float[] { rings, sides } } }
                },
                Changes = fire ? new[] { "transform", "material" } : new[] { "transform" }
            },
            Parts = new[]
            {
                new FormPartDescription
                    { Id = "body", Roles = new[] { fire ? "flame.body" : "vapour.body" }, Surface = surface, Pivot = new float[3], UseMatterSurface = fire }
            },
            Anchors = Array.Empty<FormAnchorDescription>(),
            Controls = Array.Empty<FormControlDescription>(),
            Motions = Array.Empty<FormMotionDescription>(),
            Contacts = fire
                ? new[]
                {
                    new FormContactDescription
                        { Id = "body", Part = 0, A = new[] { 0f, 0f, .5f }, B = new[] { 0f, 0f, 1.1f }, Radius = .5f }
                }
                : Array.Empty<FormContactDescription>()
        };
        var resource = new FormResource(data, new[] { mesh }, this);
        if (fire) forms.Add(id, resource);
        else Steam = resource;
        references.Add(new WeakReference<FormResource>(resource));
        Surface(surface, true);
        Surface(surface, false);
    }
}

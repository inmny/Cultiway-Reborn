using System;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal sealed partial class FormResourceGeneration
{
    /// <summary>在阴兵真正出现的位置建立有深度的幡门和内层半透明幡布。</summary>
    internal void CreateSpiritHostGate()
    {
        const string id = "spirit_host_gate";
        const float dissolve = .25f;
        var arch = new SpatialVisualMeshBuilder();
        var edge = new SpatialVisualMeshBuilder();
        var veil = new SpatialVisualMeshBuilder();
        Vector2[] outside = [new(-1.24f, .01f), new(-1.15f, 1.07f), new(-.82f, 1.63f),
            new(0f, 2.03f), new(.82f, 1.63f), new(1.15f, 1.07f), new(1.24f, .01f)];
        Vector2[] inside = [new(-.86f, .01f), new(-.80f, .97f), new(-.59f, 1.36f),
            new(0f, 1.68f), new(.59f, 1.36f), new(.80f, .97f), new(.86f, .01f)];
        for (int i = 0; i < outside.Length - 1; i++)
        {
            AddGateBand(arch, outside[i], outside[i + 1], inside[i + 1], inside[i], -.22f, .22f);
            AddGateBand(edge, Vector2.Lerp(outside[i], inside[i], .18f), Vector2.Lerp(outside[i + 1], inside[i + 1], .18f),
                Vector2.Lerp(outside[i + 1], inside[i + 1], .36f), Vector2.Lerp(outside[i], inside[i], .36f), -.27f, -.22f);
        }

        // 幡布留出正中的通行空隙。折面替代烟雾或指向假目标的细线。
        for (int side = -1; side <= 1; side += 2)
        for (int panel = 0; panel < 4; panel++)
        {
            Vector3 a = Drapery(side, panel, false);
            Vector3 b = Drapery(side, panel + 1, false);
            Vector3 c = Drapery(side, panel + 1, true);
            Vector3 d = Drapery(side, panel, true);
            veil.AddFace([a, b, c, d], Color.white);
            veil.AddFace([d, c, b, a], Color.white);
        }

        FormPartDescription[] parts =
        [
            new() { Id = "lintel", Surface = "spirit_gate_lintel", Pivot = new float[3], Roles = ["gate.frame"] },
            new() { Id = "edging", Surface = "spirit_gate_edging", Pivot = new float[3], Roles = ["gate.edge"] },
            new() { Id = "veil", Surface = "spirit_gate_veil", Pivot = new float[3], Roles = ["gate.drapery"] }
        ];
        var desc = new FormDescription
        {
            Version = FormDescription.FormatVersion, Id = id, Forward = [0f, 1f, 0f], Up = [0f, 0f, 1f],
            Structure = new FormStructureDescription
            {
                Kind = "rigid", Changes = ["transform"],
                Generation = new FormGenerationDescription
                {
                    Source = "Source/Core/Visuals/Forms/FormSpiritHostResources.cs", Entry = nameof(CreateSpiritHostGate)
                }
            },
            Parts = parts,
            Anchors = [],
            Contacts = [],
            Controls = [new FormControlDescription { Id = "gate", Position = new float[3], Rotation = [0f, 0f, 0f, 1f] }],
            Motions =
            [
                new FormMotionDescription
                {
                    Id = "dissolve", Duration = dissolve,
                    Keys =
                    [
                        new FormMotionKeyDescription { Positions = new float[3], Rotations = [0f, 0f, 0f, 1f] },
                        new FormMotionKeyDescription { At = dissolve, Fade = 1f, Positions = new float[3], Rotations = [0f, 0f, 0f, 1f] }
                    ]
                }
            ]
        };
        Mesh[] meshes = [Bake(arch, "lintel"), Bake(edge, "edging"), Bake(veil, "veil")];
        var resource = new FormResource(desc, meshes, this);
        forms.Add(id, resource);
        references.Add(new WeakReference<FormResource>(resource));
        foreach (FormPartDescription part in parts)
        {
            Surface(part.Surface, true);
            Surface(part.Surface, false);
        }

        Mesh Bake(SpatialVisualMeshBuilder builder, string name)
        {
            SpatialVisualMesh data = builder.Build(name);
            var mesh = new Mesh
            {
                name = "Cultiway_" + id + "_" + name, hideFlags = HideFlags.DontSave,
                vertices = data.Vertices, normals = data.Normals, triangles = data.Triangles,
                uv = Array.ConvertAll(data.Vertices, point => new Vector2(point.x * .5f + .5f, point.z * .5f))
            };
            mesh.RecalculateBounds();
            Own(mesh);
            return mesh;
        }
    }

    /// <summary>每段金属门缘都有前后面及厚度。</summary>
    private static void AddGateBand(SpatialVisualMeshBuilder builder,
        Vector2 a, Vector2 b, Vector2 c, Vector2 d, float front, float back)
    {
        Vector3[] rim = [new(d.x, front, d.y), new(c.x, front, c.y), new(b.x, front, b.y), new(a.x, front, a.y)];
        Vector3 depth = Vector3.up * (back - front);
        builder.AddFace(rim, Color.white);
        builder.AddFace([rim[3] + depth, rim[2] + depth, rim[1] + depth, rim[0] + depth], Color.white);
        for (int i = 0; i < rim.Length; i++)
        {
            Vector3 one = rim[i], two = rim[(i + 1) % rim.Length];
            builder.AddFace([two, one, one + depth, two + depth], Color.white);
        }
    }

    /// <summary>内侧悬空的交替褶皱。</summary>
    private static Vector3 Drapery(int side, int panel, bool upper)
    {
        float t = panel * .25f;
        float outer = upper ? .79f - .28f * t : .94f - .62f * t;
        float depth = -.04f + (panel % 2 == 0 ? .05f : -.08f);
        float height = upper ? 1.07f + .43f * t : .10f + .39f * t;
        return new Vector3(side * outer, depth, height);
    }
}

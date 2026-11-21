using System;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

internal sealed partial class FormResourceGeneration
{
    /// <summary>建立两种圆面波体，厚度、轮廓与接触等比，共用同一个资源版本。</summary>
    internal void CreatePulseForms()
    {
        CreatePulseForm("pressure_wave", "pressure_wave", 0.11f, 8,
            [new(.62f, .015f), new(.69f, .078f), new(.76f, .032f), new(.84f, .10f), new(.92f, .177f),
                new(.975f, .099f), new(1f, .033f), new(.985f, .008f), new(.62f, .008f)]);
        CreatePulseForm("purifying_curtain", "purifying_curtain", 0.24f, 12,
            [new(.53f, .01f), new(.66f, .028f), new(.78f, .082f), new(.85f, .12f),
                new(.95f, .10f), new(1f, .027f), new(.98f, .008f), new(.53f, .006f)]);
    }

    /// <summary>转动完整截面，保留实体斜面和起伏，最外缘始终等于实际单位半径。</summary>
    /// <param name="id">共用形体编号。</param><param name="surface">实际表面。</param>
    /// <param name="fold">截面的起伏程度。</param><param name="lobes">沿周向的起伏数量。</param>
    /// <param name="profile">按半径和高度排列的一圈封闭截面。</param>
    private void CreatePulseForm(string id, string surface, float fold, int lobes, Vector2[] profile)
    {
        const int sides = 72;
        const float dissolve = .25f;
        int stride = sides + 1;
        var vertices = new Vector3[(profile.Length + 1) * stride];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[profile.Length * sides * 6];
        int index = 0;
        for (int row = 0; row <= profile.Length; row++)
        {
            Vector2 section = profile[row % profile.Length];
            for (int side = 0; side <= sides; side++)
            {
                float u = (float)side / sides, angle = 2f * Mathf.PI * u;
                float swell = Mathf.Cos(angle * lobes);
                float radius = section.x - fold * (1f - section.x) * (1f - swell) * .32f;
                float height = section.y * (1f + fold * swell);
                int a = row * stride + side;
                vertices[a] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), height);
                uv[a] = new Vector2(u, (float)row / profile.Length);
                if (side == sides || row == profile.Length) continue;
                int b = a + stride;
                triangles[index++] = a;
                triangles[index++] = b;
                triangles[index++] = a + 1;
                triangles[index++] = a + 1;
                triangles[index++] = b;
                triangles[index++] = b + 1;
            }
        }
        var mesh = new Mesh { name = "Cultiway_" + id, hideFlags = HideFlags.DontSave };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Own(mesh);
        var description = new FormDescription
        {
            Version = FormDescription.FormatVersion, Id = id, Forward = [0f, 1f, 0f], Up = [0f, 0f, 1f],
            Structure = new FormStructureDescription
            {
                Kind = "volume", Changes = ["transform"],
                Generation = new FormGenerationDescription
                {
                    Source = "Source/Core/Visuals/Forms/FormPulseResources.cs", Entry = nameof(CreatePulseForm),
                    Parameters = [new FormParameterDescription { Id = "folds", Values = [lobes, fold] }]
                }
            },
            Parts = [new FormPartDescription { Id = "front", Roles = ["wave.front"], Surface = surface, Pivot = new float[3] }],
            Anchors = Array.Empty<FormAnchorDescription>(),
            Controls = [new FormControlDescription { Id = "wave", Position = new float[3], Rotation = [0f, 0f, 0f, 1f] }],
            Contacts =
            [
                new FormContactDescription
                {
                    Id = "reach", Part = 0, A = new float[3],
                    Sector = new FormContactSector { Radius = 1f, Angle = 360f, HalfThickness = .04f }
                }
            ],
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
        var resource = new FormResource(description, new[] { mesh }, this);
        forms.Add(id, resource);
        references.Add(new WeakReference<FormResource>(resource));
        Surface(surface, true);
        Surface(surface, false);
    }
}

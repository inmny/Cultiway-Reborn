using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>云衣的三面弯折护体，各面有宽度并在肩腰两处收边。</summary>
internal static class ArtifactFoldedShield
{
    internal static readonly SpatialVisualMesh Surface = Build();

    private static SpatialVisualMesh Build()
    {
        const int divisions = 12;
        var mesh = new SpatialVisualMeshBuilder();
        for (int panel = 0; panel < 3; panel++)
        for (int row = 0; row < 4; row++)
        for (int strip = 0; strip < divisions; strip++)
        {
            float u = strip / (float)divisions, next = (strip + 1f) / divisions;
            float t = row / 4f, below = (row + 1f) / 4f;
            float light = .26f + .47f * Mathf.Sin((u + next) * Mathf.PI / 2f);
            Color tint = Color.Lerp(new Color(.24f, .56f, .64f, .65f), new Color(.88f, .98f, 1f, .86f), light);
            mesh.AddFace([Point(panel, u, t), Point(panel, next, t), Point(panel, next, below), Point(panel, u, below)], tint);
        }
        return mesh.Build("artifact_cloud_ward");
    }

    private static Vector3 Point(int panel, float u, float t)
    {
        float angle = (panel + .08f + .88f * u) * Mathf.PI * 2f / 3f;
        float ridge = 1f - Mathf.Abs(2f * t - .84f);
        float radius = .37f + .18f * ridge + .045f * Mathf.Sin(u * Mathf.PI * 4f);
        float height = Mathf.Lerp(.48f, -.39f, t) + .08f * Mathf.Cos(u * Mathf.PI * 2f);
        return new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), height);
    }
}

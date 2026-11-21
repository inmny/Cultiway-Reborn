using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>两个视角共用的衣面。胸前留开口，下摆的褶皱由实际曲面受光。</summary>
internal static class ArtifactVeilGeometry
{
    internal static readonly SpatialVisualMesh Drape = BuildDrape();

    private static SpatialVisualMesh BuildDrape()
    {
        const int folds = 24, rows = 8;
        var mesh = new SpatialVisualMeshBuilder();
        for (int row = 0; row < rows; row++)
        for (int fold = 0; fold < folds; fold++)
        {
            float t = row / (float)rows, next = (row + 1f) / rows;
            float u = fold / (float)folds, across = (fold + 1f) / folds;
            float pleat = .74f + .26f * Mathf.Cos((u + across) * Mathf.PI * 5f);
            bool hem = row == rows - 1 || row == 0;
            Color tint = Color.Lerp(new Color(.22f, .32f, .48f, .69f),
                new Color(.88f, .98f, 1f, .95f), hem ? .86f : pleat * .62f);
            mesh.AddFace([Point(t, u), Point(next, u), Point(next, across), Point(t, across)], tint);
        }
        return mesh.Build("heavenly_concealment_drape");
    }

    private static Vector3 Point(float t, float u)
    {
        float angle = Mathf.Lerp(.26f, 1.74f, u) * Mathf.PI;
        float fold = Mathf.Cos(u * Mathf.PI * 10f);
        float span = .20f + .58f * t * t + fold * t * .064f;
        float height = .42f - t * (.87f + .075f * Mathf.Sin(u * Mathf.PI * 3f)) + fold * t * .036f;
        return new Vector3(Mathf.Cos(angle) * span - t * .29f, Mathf.Sin(angle) * span * .76f, height);
    }
}

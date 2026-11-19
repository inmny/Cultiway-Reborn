using UnityEngine;

namespace Cultiway.Core.Visuals;

internal static partial class SpatialVisualGeometry
{
    /// <summary>贴在身体侧面的几道黏斑，上宽下窄，具有浅厚度。</summary>
    internal static readonly SpatialVisualMesh PoisonStain = BuildPoisonStain();

    /// <summary>预制厚斑和下垂的小滴，颜色由实际持有状态提供。</summary>
    private static SpatialVisualMesh BuildPoisonStain()
    {
        SpatialVisualMeshBuilder builder = new();
        AddDrop(-.30f, .17f, .38f, 1.08f);
        AddDrop(.25f, -.07f, .29f, .85f);
        AddDrop(.53f, -.64f, .13f, .28f);
        return builder.Build("poison_stain");

        void AddDrop(float x, float z, float width, float length)
        {
            const int rows = 6, sides = 10;
            for (int row = 0; row < rows; row++)
            for (int side = 0; side < sides; side++)
            {
                byte tone = (byte)(148 + 91 * Mathf.Max(0f, Mathf.Cos((side + .5f) * Mathf.PI * 2f / sides)));
                builder.AddFace(new[] { Point(row, side), Point(row, side + 1), Point(row + 1, side + 1), Point(row + 1, side) },
                    new Color32(tone, tone, tone, 255));
            }

            Vector3 Point(int row, int side)
            {
                float phase = row / (float)rows, angle = side * Mathf.PI * 2f / sides;
                float profile = Mathf.Max(.01f, Mathf.Sin(phase * Mathf.PI)) * (.48f + .66f * phase);
                return new Vector3(x + .18f * length * phase + Mathf.Cos(angle) * width * profile,
                    Mathf.Sin(angle) * width * .61f * profile, z + (phase - .5f) * length);
            }
        }
    }
}

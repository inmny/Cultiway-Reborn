using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>载具经过后向两侧张开再消退的成对风面。</summary>
internal static class ArtifactVehicleWakeGeometry
{
    /// <summary>两个视角共享的曲面。</summary>
    internal static readonly SpatialVisualMesh Surface = Build();

    /// <summary>按两侧的走向生成逐渐变薄的曲面。</summary>
    private static SpatialVisualMesh Build()
    {
        const int length = 14, width = 6;
        var mesh = new SpatialVisualMeshBuilder();
        for (int side = -1; side <= 1; side += 2)
        for (int row = 0; row < length; row++)
        for (int column = 0; column < width; column++)
        {
            float t = row / (float)length, next = (row + 1f) / length;
            float u = column / (float)width, edge = (column + 1f) / width;
            float across = (u + edge) * .5f, along = (t + next) * .5f;
            Color tint = new(.62f + across * .35f, .79f + across * .18f, 1f,
                Mathf.Sin(across * Mathf.PI) * Mathf.Sin(along * Mathf.PI) * (1f - .56f * along));
            Vector3 a = Point(side, t, u), b = Point(side, t, edge);
            Vector3 c = Point(side, next, edge), d = Point(side, next, u);
            mesh.AddFace(side > 0 ? [a, b, c, d] : [d, c, b, a], tint);
        }
        return mesh.Build("artifact_vehicle_wake", SpatialSurface.Mist);
    }

    /// <summary>沿行进方向的反向拉开曲面，外沿向上卷起。</summary>
    /// <param name="side">载具的左侧或右侧。</param>
    /// <param name="t">沿飞行反向的位置比例。</param>
    /// <param name="u">从内缘朝外沿的位置比例。</param>
    private static Vector3 Point(int side, float t, float u)
    {
        float flare = Mathf.Sin(t * Mathf.PI * .62f);
        float width = Mathf.Sin(t * Mathf.PI) * .31f;
        float curl = Mathf.Sin(u * Mathf.PI * .77f) * flare;
        return new Vector3(side * (.16f + flare * .48f + u * width), -t,
            curl * (.16f + u * .32f) - t * .18f);
    }
}

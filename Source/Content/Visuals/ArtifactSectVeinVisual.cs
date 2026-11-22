using System.Collections.Generic;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Visuals;

/// <summary>供奉处的浅层玉色地脉，只有实际护持成员的原件才显示。</summary>
internal static class ArtifactSectVeinVisual
{
    /// <summary>发散的断面由固定几何组成，两个视角直接复用。</summary>
    private static readonly SpatialVisualMesh surface = Build();

    /// <summary>将一处地脉放到供奉建筑前侧，同时保留与原件的横向位置。</summary>
    /// <param name="output">当前帧的供奉表现。</param>
    /// <param name="foundation">实际供奉建筑的位置。</param>
    /// <param name="artifactX">原件本轮的横向位置。</param>
    /// <param name="now">当前世界秒数。</param>
    /// <param name="phase">随各件原物错开的相位。</param>
    internal static void Append(List<SpatialVisualMeshInstance> output, Vector2 foundation, float artifactX,
        float now, float phase)
    {
        Vector3 origin = new(artifactX, foundation.y - .42f, .08f);
        float flow = .5f + .5f * Mathf.Sin(now * 2.7f + phase);
        Color jade = new(.55f, .88f, .70f, .64f + .18f * flow);
        SpatialVisualDraw.Mesh(output, surface, origin, Quaternion.identity,
            new Vector3(1.16f, .92f + .1f * flow, .82f), jade, false, origin, additive: false);
    }

    /// <summary>每支沿地势逐渐收窄，两侧弯出圆面，中心保有厚度和亮色。</summary>
    private static SpatialVisualMesh Build()
    {
        const int rows = 12, columns = 6;
        var mesh = new SpatialVisualMeshBuilder();
        for (int branch = -1; branch <= 1; branch++)
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            float t = row / (float)rows, next = (row + 1f) / rows;
            float u = column / (float)columns, edge = (column + 1f) / columns;
            float center = Mathf.Sin((u + edge) * Mathf.PI * .5f);
            Color shade = Color.Lerp(new(.14f, .43f, .39f, .48f), new(.87f, 1f, .72f, .91f), center);
            shade.a *= 1f - .64f * t;
            mesh.AddFace([Point(branch, t, u), Point(branch, next, u), Point(branch, next, edge), Point(branch, t, edge)], shade);
        }
        return mesh.Build("artifact_sect_vein", SpatialSurface.Energy);
    }

    /// <summary>用实际深度和三股质地画出贴地走势。</summary>
    /// <param name="branch">中脉或两侧支脉。</param>
    /// <param name="t">离开供奉处的距离比例。</param>
    /// <param name="u">横过一支地脉的比例。</param>
    private static Vector3 Point(int branch, float t, float u)
    {
        float width = .26f * (1f - .94f * t);
        float x = branch * (.13f + .68f * t * t) + Mathf.Sin(t * 5.1f + branch) * .07f;
        float y = -t * (branch == 0 ? 1.2f : .94f);
        float z = Mathf.Sin(u * Mathf.PI) * (.11f + Mathf.Sin(t * Mathf.PI) * .21f) * (1f - t);
        return new Vector3(x + (u - .5f) * width, y, z);
    }
}

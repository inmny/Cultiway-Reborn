using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>法术共用的小型实体；颜色与运动由实际招式提供。</summary>
internal static partial class SpatialVisualGeometry
{
    /// <summary>沿局部高度轴连接两点的单位细杆。</summary>
    internal static readonly SpatialVisualMesh Rod = BuildRod();
    /// <summary>有硬边的石块与团状核心。</summary>
    internal static readonly SpatialVisualMesh Stone = BuildOctahedron();
    /// <summary>带厚度和尖端的冰棱、针与火舌。</summary>
    internal static readonly SpatialVisualMesh Shard = BuildShard();
    /// <summary>保留刀背与刃缘的弯刃。</summary>
    internal static readonly SpatialVisualMesh Blade = BuildBlade();
    /// <summary>低面数树叶，用于治疗与木系。</summary>
    internal static readonly SpatialVisualMesh Leaf = BuildLeaf();
    /// <summary>有厚度的单位墙段。</summary>
    internal static readonly SpatialVisualMesh Block = BuildBlock();
    /// <summary>开口的一段空间圆弧，不会退化成始终朝向镜头的线。</summary>
    internal static readonly SpatialVisualMesh Arc = BuildArc();

    /// <summary>构造单位细杆。</summary>
    private static SpatialVisualMesh BuildRod()
    {
        SpatialVisualMeshBuilder b = new();
        AddRod(b, Vector3.zero, Vector3.forward, 1f, Color.white);
        return b.Build("spatial_rod");
    }

    /// <summary>构造八面硬质体，保留可辨认的明暗面。</summary>
    private static SpatialVisualMesh BuildOctahedron()
    {
        SpatialVisualMeshBuilder b = new();
        Vector3[] equator = { Vector3.right, Vector3.up, Vector3.left, Vector3.down };
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = equator[i], c = equator[(i + 1) % 4];
            b.AddFace(new[] { a, c, Vector3.forward }, Color.white);
            b.AddFace(new[] { c, a, Vector3.back }, Color.white);
        }
        return b.Build("spatial_stone");
    }

    /// <summary>构造尖棱的侧面与底面。</summary>
    private static SpatialVisualMesh BuildShard()
    {
        SpatialVisualMeshBuilder b = new();
        Vector3[] basePoints = { new(-.3f, -.2f, 0f), new(.3f, -.2f, 0f), new(.3f, .2f, 0f), new(-.3f, .2f, 0f) };
        for (int i = 0; i < 4; i++)
            b.AddFace(new[] { basePoints[i], basePoints[(i + 1) % 4], Vector3.forward }, Color.white);
        b.AddFace(basePoints, Color.white);
        return b.Build("spatial_shard");
    }

    /// <summary>构造有薄刃和厚刀背的弯曲主体。</summary>
    private static SpatialVisualMesh BuildBlade()
    {
        SpatialVisualMeshBuilder b = new();
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.Lerp(-1.1f, 1.1f, i / 8f), c = Mathf.Lerp(-1.1f, 1.1f, (i + 1) / 8f);
            Vector3 first = new(Mathf.Sin(a), Mathf.Cos(a) * .5f, 0f);
            Vector3 next = new(Mathf.Sin(c), Mathf.Cos(c) * .5f, 0f);
            Vector3 edge = Vector3.Lerp(first, next, .5f) + Vector3.up * .2f;
            b.AddFace(new[] { first + Vector3.forward * .035f, next + Vector3.forward * .035f, edge }, Color.white);
            b.AddFace(new[] { next - Vector3.forward * .035f, first - Vector3.forward * .035f, edge }, Color.white);
        }
        return b.Build("spatial_blade");
    }

    /// <summary>构造有叶脊的四面叶片。</summary>
    private static SpatialVisualMesh BuildLeaf()
    {
        SpatialVisualMeshBuilder b = new();
        Vector3[] edge = { Vector3.zero, new(-.35f, 0f, .4f), Vector3.forward, new(.35f, 0f, .4f) };
        for (int i = 0; i < 4; i++)
        {
            b.AddFace(new[] { edge[i], edge[(i + 1) % 4], new Vector3(0f, .1f, .45f) }, Color.white);
            b.AddFace(new[] { edge[(i + 1) % 4], edge[i], new Vector3(0f, -.1f, .45f) }, Color.white);
        }
        return b.Build("spatial_leaf",SpatialSurface.Foliage);
    }

    /// <summary>构造可平铺的墙段。</summary>
    private static SpatialVisualMesh BuildBlock()
    {
        SpatialVisualMeshBuilder b = new();
        Vector3[] p = { new(-.5f,-.5f,0), new(.5f,-.5f,0), new(.5f,.5f,0), new(-.5f,.5f,0),
            new(-.5f,-.5f,1), new(.5f,-.5f,1), new(.5f,.5f,1), new(-.5f,.5f,1) };
        int[][] faces = { new[]{0,1,5,4}, new[]{1,2,6,5}, new[]{2,3,7,6}, new[]{3,0,4,7}, new[]{4,5,6,7}, new[]{3,2,1,0} };
        foreach (int[] f in faces) b.AddFace(new[] { p[f[0]], p[f[1]], p[f[2]], p[f[3]] }, Color.white);
        return b.Build("spatial_block");
    }

    /// <summary>构造留有开口的宽雾带，两端渐收，保留侧面厚度。</summary>
    private static SpatialVisualMesh BuildArc()
    {
        SpatialVisualMeshBuilder b = new();
        AddMistBand(b, 0f, Mathf.PI * 2f / 3f);
        return b.Build("spatial_mist_arc", SpatialSurface.Mist);
    }
}

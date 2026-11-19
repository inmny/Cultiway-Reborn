using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Visuals;

/// <summary>不依赖镜头的局部模型；每个三角形保存自己的材质颜色。</summary>
internal sealed class SpatialVisualMesh
{
    /// <summary>模型名称，只用于识别与调试。</summary>
    internal readonly string Name;
    /// <summary>保留完整三维位置的局部顶点。</summary>
    internal readonly Vector3[] Vertices;
    /// <summary>各顶点的局部法向。</summary>
    internal readonly Vector3[] Normals;
    /// <summary>每三个索引构成一个三角形。</summary>
    internal readonly int[] Triangles;
    /// <summary>局部边界，供视距与模型落地位置共同使用。</summary>
    internal readonly Bounds Bounds;
    /// <summary>与三角形一一对应的颜色。</summary>
    internal readonly Color32[] Colors;
    /// <summary>按共享顶点预展开的面色，原版每帧不再重复展开三角形。</summary>
    internal readonly Color32[] VertexColors;
    /// <summary>主体使用的表面质感，两个视角均可读取。</summary>
    internal readonly SpatialSurface Surface;

    /// <summary>接收已经完成的几何资料，两个显示方式共享同一份内容。</summary>
    /// <param name="name">模型名称。</param>
    /// <param name="vertices">局部三维顶点。</param>
    /// <param name="normals">局部法向。</param>
    /// <param name="triangles">三角形索引。</param>
    /// <param name="colors">各三角形的材质色。</param>
    /// <param name="surface">主体表面类型。</param>
    internal SpatialVisualMesh(string name, Vector3[] vertices, Vector3[] normals, int[] triangles, Color32[] colors, SpatialSurface surface = SpatialSurface.None)
    {
        Name = name;
        Vertices = vertices;
        Normals = normals;
        Triangles = triangles;
        Colors = colors;
        Surface = surface;
        VertexColors = new Color32[vertices.Length];
        for(int i=0;i<triangles.Length;i++)VertexColors[triangles[i]]=colors[i/3];
        Bounds bounds = new(vertices[0], Vector3.zero);
        foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
        Bounds = bounds;
    }

}

/// <summary>模型的一次显示快照，不承担计时、命中或对象存活管理。</summary>
internal readonly struct SpatialVisualMeshInstance
{
    /// <summary>共享的局部模型。</summary>
    internal readonly SpatialVisualMesh Model;
    /// <summary>地图位置与独立离地高度。</summary>
    internal readonly Vector3 Position;
    /// <summary>局部空间到地图空间的旋转。</summary>
    internal readonly Quaternion Rotation;
    /// <summary>模型三个方向的显示尺寸，不回写碰撞。</summary>
    internal readonly Vector3 Scale;
    /// <summary>当前颜色与整体透明度。</summary>
    internal readonly Color Tint;
    /// <summary>是否由新的二维几何显示绘制；已有精灵显示的主体保持原入口。</summary>
    internal readonly bool DrawNative;
    /// <summary>能量表现保留自身颜色，不参与实体表面的明暗与投影。</summary>
    internal readonly bool Emissive;
    /// <summary>发光部件是否叠加亮度；外焰等有颜色的薄层使用普通透明混合。</summary>
    internal readonly bool Additive;
    /// <summary>本次显示的表面，可以把同一几何组合成不同材料。</summary>
    internal readonly SpatialSurface Surface;
    /// <summary>附着在同一身体上的部件共用的地面采样位置，防止坡面上关节错开。</summary>
    internal readonly Vector2 GroundAnchor;
    /// <summary>连接身体的部件所属人物，供空间显示对齐游泳、跳起和栈桥高度；不入档。</summary>
    internal readonly Actor GroundActor;

    /// <summary>从原有执行状态生成一个只读显示快照。</summary>
    /// <param name="model">共用模型。</param>
    /// <param name="position">地图位置与离地高度。</param>
    /// <param name="rotation">局部到地图的旋转。</param>
    /// <param name="scale">显示缩放，不改变碰撞。</param>
    /// <param name="tint">整体颜色与透明度。</param>
    /// <param name="drawNative">是否同时由原版几何视图绘制。</param>
    /// <param name="emissive">是否作为保持自身颜色的能量表现。</param>
    /// <param name="groundAnchor">共同承托位置；未指定时使用自身平面位置。</param>
    /// <param name="groundActor">需要与实际身体共用承托高度的人物。</param>
    /// <param name="additive">发光层是否叠加亮度；实体不受此项影响。</param>
    /// <param name="surface">本次显示明确指定的表面；省略时采用几何的默认表面。</param>
    internal SpatialVisualMeshInstance(SpatialVisualMesh model, Vector3 position, Quaternion rotation,
        Vector3 scale, Color tint, bool drawNative = false, bool emissive = false, Vector2? groundAnchor = null, Actor groundActor = null,bool additive=true,SpatialSurface? surface=null)
    {
        Model = model;
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Tint = tint;
        DrawNative = drawNative;
        Emissive = emissive;
        Additive = additive;
        Surface = surface ?? model.Surface;
        GroundAnchor = groundAnchor ?? (Vector2)position;
        GroundActor = groundActor;
    }

    /// <summary>让身体相连的部件使用同一个地面承托点，原版投影不变。</summary>
    /// <param name="anchor">人物或法宝脚下的地图位置。</param>
    internal SpatialVisualMeshInstance WithGroundAnchor(Vector2 anchor) =>
        new(Model, Position, Rotation, Scale, Tint, DrawNative, Emissive, anchor, GroundActor,Additive,Surface);

    /// <summary>身体相连的部件使用同一个承托点；二维显示保持原有位置。</summary>
    /// <param name="actor">实际身体，不能传资源所属的另一位人物。</param>
    internal SpatialVisualMeshInstance WithBodyAnchor(Actor actor) =>
        new(Model, Position, Rotation, Scale, Tint, DrawNative, Emissive, actor.current_position, actor,Additive,Surface);

    /// <summary>取得没有经过二维投影的世界顶点。</summary>
    /// <param name="point">尚未投影的局部位置。</param>
    internal Vector3 TransformPoint(Vector3 point) => Position + Rotation * Vector3.Scale(point, Scale);
}

/// <summary>将小型凸面拆为共用三角形，保留每面的硬边。</summary>
internal sealed class SpatialVisualMeshBuilder
{
    /// <summary>构造期间的顶点。</summary>
    private readonly List<Vector3> vertices = new();
    /// <summary>构造期间的法向。</summary>
    private readonly List<Vector3> normals = new();
    /// <summary>构造期间的三角形。</summary>
    private readonly List<int> triangles = new();
    /// <summary>构造期间的面色。</summary>
    private readonly List<Color32> colors = new();

    /// <summary>添加一个按边界顺序排列的凸面；与现有组合模型的面划分一致。</summary>
    /// <param name="points">按边界顺序排列的凸面顶点。</param>
    /// <param name="color">面色。</param>
    /// <param name="sourceNormals">原模型法向；未提供时使用硬边面法向。</param>
    internal void AddFace(Vector3[] points, Color32 color, Vector3[] sourceNormals = null)
    {
        Vector3 normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
        int first = vertices.Count;
        for (int i = 0; i < points.Length; i++)
        {
            vertices.Add(points[i]);
            normals.Add(sourceNormals == null ? normal : sourceNormals[i]);
        }
        for (int i = 1; i < points.Length - 1; i++)
        {
            triangles.Add(first);
            triangles.Add(first + i);
            triangles.Add(first + i + 1);
            colors.Add(color);
        }
    }

    /// <summary>完成后冻结资料，不再逐帧重建模型。</summary>
    /// <param name="name">完成后模型的名称。</param>
    /// <param name="surface">主体表面质感。</param>
    internal SpatialVisualMesh Build(string name, SpatialSurface surface = SpatialSurface.None) =>
        new(name, vertices.ToArray(), normals.ToArray(), triangles.ToArray(), colors.ToArray(), surface);
}

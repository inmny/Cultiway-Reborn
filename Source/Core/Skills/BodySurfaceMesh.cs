using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>已经冻结的真实可见三角面，携带期间共享几何，只更新刚性运动。</summary>
internal sealed class BodySurfaceMesh
{
    /// <summary>原世界或已经摆放好的环境顶点。</summary>
    internal readonly Vector3[] Points;

    /// <summary>实际三角连接；每两个边索引组成一条不重复的有限边。</summary>
    internal readonly int[] Triangles, Edges;

    /// <summary>真实表面的外包，只用于候选筛选。</summary>
    internal readonly Bounds Bounds;

    /// <summary>环境三角面索引与复用查询空间。</summary>
    private AbilityContactIndex index;

    /// <summary>每个面自己实际覆盖的范围。</summary>
    internal readonly Bounds[] Faces;

    /// <summary>从已经确认的真实可见片段冻结一次，不重新扩大到身体包络。</summary>
    /// <param name="pose">建立关系时的完整表面。</param>
    internal BodySurfaceMesh(BodySurfacePose pose)
    {
        var points = new List<Vector3>();
        var triangles = new List<int>();
        var known = new Dictionary<Vector3, int>();
        for (int part = 0; part < pose.Parts.Length; part++)
        for (int face = 0; face < pose.Parts[part].Triangles.Length; face += 3)
        foreach (Vector3[] polygon in pose.Fragments(part, face))
        {
            var vertices = new int[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                if (!known.TryGetValue(polygon[i], out int vertex))
                {
                    vertex = points.Count;
                    known.Add(polygon[i], vertex);
                    points.Add(polygon[i]);
                }
                vertices[i] = vertex;
            }
            for (int i = 1; i + 1 < vertices.Length; i++)
            { triangles.Add(vertices[0]); triangles.Add(vertices[i]); triangles.Add(vertices[i + 1]); }
        }
        Points = points.ToArray();
        Triangles = triangles.ToArray();
        Prepare(out Edges, out Bounds, out Faces);
    }

    /// <summary>接收采集边界已经冻结的环境资料，不持有游戏或外部模型对象。</summary>
    /// <param name="points">实际摆放后的顶点。</param><param name="triangles">原三角连接。</param>
    internal BodySurfaceMesh(Vector3[] points, int[] triangles)
    {
        Points = points;
        Triangles = triangles;
        Prepare(out Edges, out Bounds, out Faces);
    }

    /// <summary>一次建立整体、逐面范围和不重复有限边，后续扫掠复用。</summary>
    /// <param name="edges">不重复边。</param><param name="bounds">整体范围。</param><param name="faces">逐面范围。</param>
    private void Prepare(out int[] edges, out Bounds bounds, out Bounds[] faces)
    {
        var unique = new HashSet<(int, int)>();
        var lines = new List<int>();
        bounds = Points.Length == 0 ? default : new Bounds(Points[0], Vector3.zero);
        foreach (Vector3 point in Points) bounds.Encapsulate(point);
        faces = new Bounds[Triangles.Length / 3];
        for (int face = 0; face < Triangles.Length; face += 3)
        {
            var area = new Bounds(Points[Triangles[face]], Vector3.zero);
            for (int i = 0; i < 3; i++)
            {
                int a = Triangles[face + i], b = Triangles[face + (i + 1) % 3];
                area.Encapsulate(Points[a]);
                if (a > b) (a, b) = (b, a);
                if (unique.Add((a, b))) { lines.Add(a); lines.Add(b); }
            }
            area.Expand(.00004f);
            faces[face / 3] = area;
        }
        edges = lines.ToArray();
    }

    /// <summary>按真实扫过范围取得附近三角面，不逐项比较整栋房屋。</summary>
    /// <param name="bounds">扫掠范围。</param><param name="faces">复用候选列表。</param>
    internal void Query(Bounds bounds, List<int> faces)
    {
        if (index == null)
        {
            index = new AbilityContactIndex(.5f);
            foreach (Bounds face in Faces) index.Add(face);
        }
        index.Query(bounds, faces);
    }

    /// <summary>原版墙格和明确基础阻挡沿其真实有限轮廓生成竖直表面。</summary>
    /// <param name="outline">按边界排列的原阻挡轮廓。</param><param name="bottom">实际底面。</param><param name="top">实际顶面。</param>
    internal static BodySurfaceMesh Prism(Vector2[] outline, float bottom, float top)
    {
        int count = outline.Length;
        var points = new Vector3[count * 2];
        var triangles = new List<int>();
        for (int i = 0; i < count; i++)
        {
            points[i] = new Vector3(outline[i].x, outline[i].y, bottom);
            points[count + i] = new Vector3(outline[i].x, outline[i].y, top);
            int next = (i + 1) % count;
            triangles.AddRange(new[] { i, next, count + next, i, count + next, count + i });
        }
        for (int i = 1; i + 1 < count; i++) triangles.AddRange(new[] { 0, i + 1, i, count, count + i, count + i + 1 });
        return new BodySurfaceMesh(points, triangles.ToArray());
    }

    /// <summary>只供原有矩形阻挡，不用它替换已有精确模型。</summary>
    /// <param name="area">原矩形轮廓。</param><param name="bottom">底面。</param><param name="top">顶面。</param>
    internal static BodySurfaceMesh Box(Rect area, float bottom, float top) => Prism(new[]
    { area.min, new Vector2(area.xMax, area.yMin), area.max, new Vector2(area.xMin, area.yMax) }, bottom, top);

    /// <summary>原世界几何随实际摆放变换，三角连接继续共享。</summary>
    /// <param name="matrix">本次真实摆放。</param>
    internal BodySurfaceMesh Transformed(Matrix4x4 matrix) =>
        new(Array.ConvertAll(Points, matrix.MultiplyPoint3x4), Triangles);
}

/// <summary>一个已采集环境表面及其稳定身份，不访问建筑对象。</summary>
internal readonly struct BodySurfaceObstacle
{
    /// <summary>稳定环境身份和实际三角面。</summary>
    internal readonly long Owner;
    /// <summary>已采集的实际环境表面。</summary>
    internal readonly BodySurfaceMesh Mesh;

    /// <summary>区分模型、原版墙格和地图边界。</summary>
    internal readonly string Reason;

    /// <summary>保存本轮真实环境。</summary>
    /// <param name="owner">环境身份。</param><param name="mesh">实际世界表面。</param>
    /// <param name="reason">实际环境来源。</param>
    internal BodySurfaceObstacle(long owner, BodySurfaceMesh mesh, string reason = "ModelContact") { Owner = owner; Mesh = mesh; Reason = reason; }
}

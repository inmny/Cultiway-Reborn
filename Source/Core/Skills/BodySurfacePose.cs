using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>模型中一个具名部件的实际表面，资料创建后不修改。</summary>
internal sealed class BodySurfacePart
{
    /// <summary>构造中可以选择的身体部位。</summary>
    internal readonly string Id;

    /// <summary>所属外层装备，身体本身为空。</summary>
    internal readonly string Attachment;

    /// <summary>所分出的掌足仍属于原肢段，动物原部件也可归入真实头身等部位。</summary>
    internal readonly string Group;

    /// <summary>共享姿态中的变换编号，负值沿用部件自己的顺序。</summary>
    internal readonly int TransformIndex;

    /// <summary>模型自身的顶点与三角面，不放大到人物包络。</summary>
    internal readonly Vector3[] Vertices;

    /// <summary>每三个索引组成一个实际面。</summary>
    internal readonly int[] Triangles;

    /// <summary>同一物理面可同时属于身体区域和仍可见的器官，避免复制重叠几何。</summary>
    internal string[] Selections { get; private set; } = Array.Empty<string>();

    /// <summary>为同一份几何增加明确选择名，原部件仍保持不变。</summary>
    /// <param name="selections">制作和实际显示共同给出的选择名。</param>
    internal BodySurfacePart WithSelections(string[] selections) =>
        selections.Length == 0 && Selections.Length == 0
            ? this
            : new BodySurfacePart(Id, Vertices, Triangles, Attachment, TransformIndex, Group)
                { Selections = selections };

    /// <summary>从实际资源取得几何。</summary>
    /// <param name="id">部位。</param><param name="vertices">真实顶点。</param><param name="triangles">真实三角形。</param>
    /// <param name="attachment">实际装备归属。</param><param name="transformIndex">共用变换，负值使用部件顺序。</param>
    /// <param name="group">包含本区域的完整身体部位。</param>
    internal BodySurfacePart(string id, Vector3[] vertices, int[] triangles, string attachment = null,
        int transformIndex = -1, string group = null)
    {
        Id = id;
        Attachment = attachment;
        Group = group;
        TransformIndex = transformIndex;
        Vertices = vertices;
        Triangles = triangles;
    }

    /// <summary>实际部位、完整肢段和装备归属共用同一选择规则。</summary>
    /// <param name="selection">目标部位。</param>
    internal bool Matches(string selection) => selection == "surface" || selection == Id ||
                                               selection == Attachment || selection == Group ||
                                               Array.IndexOf(Selections, selection) >= 0;
}

/// <summary>共同时间中的实际身体表面姿态，只含数值，显示与接触使用同一份。</summary>
internal sealed class BodySurfacePose
{
    /// <summary>同一身体模型的实际部件。</summary>
    internal readonly BodySurfacePart[] Parts;

    /// <summary>每个部件到共同世界的完整变换。</summary>
    internal readonly Matrix4x4[] Matrices;

    /// <summary>保持原三角连接的变形顶点；空值直接复用资源网格。</summary>
    private Vector3[][] deformedVertices;

    /// <summary>原版同轮真正不透明的身体轮廓，只作为独立背景部件的遮挡来源。</summary>
    internal BodySurfacePose Opaque { get; private set; }

    /// <summary>各部件需要扣除的前景投影；普通三维身体为空。</summary>
    private BodySurfaceOcclusion[] occlusions;

    /// <summary>保留原几何，附上同轮实际不透明轮廓。</summary>
    /// <param name="opaque">已采集的真实不透明表面。</param>
    internal BodySurfacePose WithOpaque(BodySurfacePose opaque) => ReferenceEquals(opaque, Opaque)
        ? this
        : new(Parts, Matrices, Foot, Height, Heading)
            { deformedVertices = deformedVertices, occlusions = occlusions, Opaque = opaque };

    /// <summary>给本次独立背景部件指定实际前景；不修改原资源或其他姿态。</summary>
    /// <param name="occlusion">本轮实际前景。</param>
    internal BodySurfacePose WithOcclusion(BodySurfaceOcclusion occlusion)
        => WithOcclusions(Array.ConvertAll(Parts, _ => occlusion));

    /// <summary>合并布局后保留每个原部件自己的遮挡。</summary>
    /// <param name="values">按实际部件顺序排列的前景。</param>
    internal BodySurfacePose WithOcclusions(BodySurfaceOcclusion[] values) => values == null
        ? this
        : new(Parts, Matrices, Foot, Height, Heading)
            { deformedVertices = deformedVertices, occlusions = values, Opaque = Opaque };

    /// <summary>查询本轮部件的实际遮挡，普通部件保持原来路径。</summary>
    /// <param name="part">部件顺序。</param>
    internal BodySurfaceOcclusion Occlusion(int part) => occlusions?[part];

    /// <summary>本轮确实含有非刚性顶点变化。</summary>
    internal bool HasDeformation => deformedVertices != null;

    /// <summary>取得本轮实际顶点，后续查询不返回原来未变形的资源表面。</summary>
    /// <param name="part">原部件顺序。</param>
    internal Vector3[] Vertices(int part) => deformedVertices?[part] ?? Parts[part].Vertices;

    /// <summary>发布一份独立数值变形；资源顶点、三角形和已有姿态均不修改。</summary>
    /// <param name="vertices">按部件顺序排列的实际局部顶点。</param>
    internal BodySurfacePose Deformed(Vector3[][] vertices) =>
        new(Parts, Matrices, Foot, Height, Heading)
            { deformedVertices = vertices, occlusions = occlusions, Opaque = Opaque };

    /// <summary>本姿态的实际脚下、身体高度与水平朝向。</summary>
    internal readonly Vector3 Foot;

    /// <summary>原身体的共同高度。</summary>
    internal readonly float Height, Heading;

    /// <summary>发布后不再修改所有权交入的数组。</summary>
    /// <param name="parts">实际模型。</param><param name="matrices">实际部件变换。</param>
    /// <param name="foot">共同脚下。</param><param name="height">共同高度。</param><param name="heading">朝向弧度。</param>
    internal BodySurfacePose(BodySurfacePart[] parts, Matrix4x4[] matrices, Vector3 foot, float height, float heading)
    {
        Parts = parts;
        Matrices = matrices;
        Foot = foot;
        Height = height;
        Heading = heading;
    }

    /// <summary>正式施放前确认所选部位确实存在，不把缺少部位当成随后落空。</summary>
    /// <param name="selection">构造需要的实际目标部位。</param>
    internal bool Supports(string selection)
    {
        for (int part = 0; part < Parts.Length; part++)
        {
            if (!Parts[part].Matches(selection) || Parts[part].Triangles.Length == 0) continue;
            if (Occlusion(part) == null) return true;
            for (int face = 0; face < Parts[part].Triangles.Length; face += 3)
                if (Fragments(part, face).Count != 0)
                    return true;
        }

        return false;
    }

    /// <summary>仅用实际存在的所选三角面估算离身体根点的水平外伸，供目标选择使用。</summary>
    /// <param name="selection">本次确实要接触的部位。</param>
    internal float Radius(string selection)
    {
        float radius = 0f;
        for (int i = 0; i < Parts.Length; i++)
        {
            BodySurfacePart part = Parts[i];
            if (!part.Matches(selection)) continue;
            Matrix4x4 matrix = Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
            Vector3[] points = Vertices(i);
            if (Occlusion(i) != null)
            {
                for (int face = 0; face < part.Triangles.Length; face += 3)
                    foreach (Vector3[] polygon in Fragments(i, face))
                    foreach (Vector3 point in polygon)
                    {
                        Vector3 delta = point - Foot;
                        radius = Mathf.Max(radius, delta.x * delta.x + delta.y * delta.y);
                    }

                continue;
            }

            foreach (int vertex in part.Triangles)
            {
                Vector3 delta = matrix.MultiplyPoint3x4(points[vertex]) - Foot;
                radius = Mathf.Max(radius, delta.x * delta.x + delta.y * delta.y);
            }
        }

        return Mathf.Sqrt(radius);
    }

    /// <summary>已确认存在的部位用于出手瞄准，实际命中仍取三角面接触。</summary>
    /// <param name="selection">所选实际部位。</param>
    internal Vector3 Aim(string selection)
    {
        Bounds bounds = default;
        bool started = false;
        for (int i = 0; i < Parts.Length; i++)
        {
            BodySurfacePart part = Parts[i];
            if (!part.Matches(selection)) continue;
            Matrix4x4 matrix = Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
            if (Occlusion(i) != null)
            {
                for (int face = 0; face < part.Triangles.Length; face += 3)
                    foreach (Vector3[] polygon in Fragments(i, face))
                    foreach (Vector3 point in polygon)
                        if (!started)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            started = true;
                        }
                        else bounds.Encapsulate(point);

                continue;
            }

            foreach (int vertex in part.Triangles)
            {
                Vector3 point = matrix.MultiplyPoint3x4(Vertices(i)[vertex]);
                if (!started)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    started = true;
                }
                else bounds.Encapsulate(point);
            }
        }

        return bounds.center;
    }

    /// <summary>适配实际接触面上的位置，不能把模型内部的包围盒中心当作皮肤。</summary>
    /// <param name="selection">已提供的真实部位。</param><param name="from">原动作中段的作用面中心。</param>
    internal Vector3 Nearest(string selection, Vector3 from)
    {
        float nearest = float.PositiveInfinity;
        Vector3 result = default;
        for (int i = 0; i < Parts.Length; i++)
        {
            BodySurfacePart part = Parts[i];
            if (!part.Matches(selection)) continue;
            Matrix4x4 matrix = Matrices[part.TransformIndex < 0 ? i : part.TransformIndex];
            for (int face = 0; face < part.Triangles.Length; face += 3)
            {
                if (Occlusion(i) != null)
                {
                    foreach (Vector3[] polygon in Fragments(i, face))
                        for (int corner = 1; corner + 1 < polygon.Length; corner++)
                        {
                            Vector3 visible = AbilityTriangleSweep.Point(from, polygon[0], polygon[corner],
                                polygon[corner + 1]);
                            float d = (visible - from).sqrMagnitude;
                            if (d < nearest)
                            {
                                nearest = d;
                                result = visible;
                            }
                        }

                    continue;
                }

                Vector3 point = AbilityTriangleSweep.Point(from,
                    matrix.MultiplyPoint3x4(Vertices(i)[part.Triangles[face]]),
                    matrix.MultiplyPoint3x4(Vertices(i)[part.Triangles[face + 1]]),
                    matrix.MultiplyPoint3x4(Vertices(i)[part.Triangles[face + 2]]));
                float distance = (point - from).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                result = point;
            }
        }

        return result;
    }

    /// <summary>当前原三角面经过真实前景后露出的片段。</summary>
    /// <param name="part">部件顺序。</param><param name="face">原三角面的起始索引。</param>
    private System.Collections.Generic.List<Vector3[]> Fragments(int part, int face)
    {
        BodySurfacePart mesh = Parts[part];
        Matrix4x4 matrix = Matrices[mesh.TransformIndex < 0 ? part : mesh.TransformIndex];
        Vector3[] vertices = Vertices(part);
        return Occlusion(part).Clip(matrix.MultiplyPoint3x4(vertices[mesh.Triangles[face]]),
            matrix.MultiplyPoint3x4(vertices[mesh.Triangles[face + 1]]),
            matrix.MultiplyPoint3x4(vertices[mesh.Triangles[face + 2]]));
    }

    /// <summary>按共同扫掠所用的线性表面运动取得同一中间姿态，不重新求另一套骨架。</summary>
    /// <param name="from">前态。</param><param name="to">后态。</param><param name="at">共同进度。</param>
    internal static BodySurfacePose Lerp(BodySurfacePose from, BodySurfacePose to, float at)
    {
        if (at <= 0f) return from;
        if (at >= 1f) return to;
        var matrices = new Matrix4x4[to.Matrices.Length];
        for (int part = 0; part < matrices.Length; part++)
        for (int value = 0; value < 16; value++)
            matrices[part][value] = Mathf.Lerp(from.Matrices[part][value], to.Matrices[part][value], at);
        var result = new BodySurfacePose(to.Parts, matrices, Vector3.Lerp(from.Foot, to.Foot, at),
            Mathf.Lerp(from.Height, to.Height, at),
            Mathf.LerpAngle(from.Heading * Mathf.Rad2Deg, to.Heading * Mathf.Rad2Deg, at) * Mathf.Deg2Rad);
        result.Opaque = from.Opaque == null || to.Opaque == null
            ? to.Opaque ?? from.Opaque
            : Lerp(from.Opaque, to.Opaque, at);
        if (from.occlusions != null || to.occlusions != null)
        {
            result.occlusions = new BodySurfaceOcclusion[to.Parts.Length];
            for (int part = 0; part < result.occlusions.Length; part++)
            {
                BodySurfaceOcclusion a = from.Occlusion(part), b = to.Occlusion(part);
                result.occlusions[part] = a == null || b == null ? b ?? a : BodySurfaceOcclusion.Lerp(a, b, at);
            }
        }

        if (from.deformedVertices == null && to.deformedVertices == null) return result;
        var vertices = new Vector3[to.Parts.Length][];
        for (int part = 0; part < vertices.Length; part++)
        {
            Vector3[] a = from.Vertices(part), b = to.Vertices(part);
            vertices[part] = new Vector3[b.Length];
            for (int i = 0; i < b.Length; i++) vertices[part][i] = Vector3.Lerp(a[i], b[i], at);
        }

        return result.Deformed(vertices);
    }

    /// <summary>在所选真实部件中寻找最早接触，返回接触时完整表面供维持及显示。</summary>
    /// <param name="from">前态。</param><param name="to">后态。</param>
    /// <param name="sourceFrom">来源前态。</param><param name="sourceTo">来源后态。</param>
    /// <param name="selection">目标部位，surface代表所有已有真实部件。</param>
    /// <param name="at">最早进度。</param><param name="anchor">实际面内位置。</param><param name="normal">接触方向。</param>
    internal static bool Find(BodySurfacePose from, BodySurfacePose to, in AbilityCapsule sourceFrom,
        in AbilityCapsule sourceTo, string selection, out float at, out BodySurfaceAnchor anchor, out Vector3 normal)
    {
        at = float.PositiveInfinity;
        anchor = default;
        normal = default;
        Bounds sweep = AbilityContactIndex.Swept(sourceFrom, sourceTo);
        for (int part = 0; part < to.Parts.Length; part++)
        {
            BodySurfacePart mesh = to.Parts[part];
            if (!mesh.Matches(selection)) continue;
            int transform = mesh.TransformIndex < 0 ? part : mesh.TransformIndex;
            for (int triangle = 0; triangle < mesh.Triangles.Length; triangle += 3)
            {
                Vector3[] before = from.Vertices(part), after = to.Vertices(part);
                int a = mesh.Triangles[triangle], b = mesh.Triangles[triangle + 1], c = mesh.Triangles[triangle + 2];
                Vector3 a0 = from.Matrices[transform].MultiplyPoint3x4(before[a]),
                    b0 = from.Matrices[transform].MultiplyPoint3x4(before[b]),
                    c0 = from.Matrices[transform].MultiplyPoint3x4(before[c]),
                    a1 = to.Matrices[transform].MultiplyPoint3x4(after[a]),
                    b1 = to.Matrices[transform].MultiplyPoint3x4(after[b]),
                    c1 = to.Matrices[transform].MultiplyPoint3x4(after[c]);
                Bounds triangleBounds = new(a0, Vector3.zero);
                triangleBounds.Encapsulate(b0);
                triangleBounds.Encapsulate(c0);
                triangleBounds.Encapsulate(a1);
                triangleBounds.Encapsulate(b1);
                triangleBounds.Encapsulate(c1);
                if (!sweep.Intersects(triangleBounds)) continue;
                BodySurfaceOcclusion cover = to.Occlusion(part) ?? from.Occlusion(part);
                float fraction;
                Vector3 hit, direction;
                bool touched = cover == null
                    ? AbilityTriangleSweep.Find(sourceFrom, sourceTo, a0, b0, c0, a1, b1, c1,
                        out fraction, out hit, out direction)
                    : BodySurfaceOcclusion.Find(from.Occlusion(part) ?? cover, to.Occlusion(part) ?? cover,
                        sourceFrom, sourceTo, a0, b0, c0, a1, b1, c1, out fraction, out hit, out direction);
                if (touched && fraction < at)
                {
                    at = fraction;
                    anchor = new BodySurfaceAnchor(part, triangle, hit, Vector3.Lerp(a0, a1, fraction),
                        Vector3.Lerp(b0, b1, fraction), Vector3.Lerp(c0, c1, fraction));
                    normal = direction;
                }
            }
        }

        return !float.IsPositiveInfinity(at);
    }
}
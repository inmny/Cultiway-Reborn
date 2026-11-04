using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>原身体和当前动作的真实表面布局；资源没有变化时只合并本轮数值。</summary>
internal sealed class BodySurfaceComposition
{
    /// <summary>各来源的固定资源顶点与三角面。</summary>
    private readonly BodySurfacePart[][] sources;

    /// <summary>各来源在完整变换数组中的起点。</summary>
    private readonly int[] offsets;

    /// <summary>合并后表面的资源与原来源部件。</summary>
    private readonly BodySurfacePart[] parts;

    /// <summary>合并后每个部件对应的来源及原部件顺序。</summary>
    private readonly (int Source, int Part)[] origins;

    /// <summary>本布局真正替换的身体部位。</summary>
    private readonly BodyOccupancy arms;

    /// <summary>普通静态持物是否由当前动作的真实器物替换。</summary>
    private readonly bool held;

    /// <summary>先保留未被替换的身体及衣甲，再加入动作网格；原骨架变换编号保持不变。</summary>
    /// <param name="poses">原身体与各动作的数值姿态。</param><param name="arms">真正接管的两侧手臂。</param>
    /// <param name="held">是否接管持物。</param>
    internal BodySurfaceComposition(IReadOnlyList<BodySurfacePose> poses, BodyOccupancy arms, bool held)
    {
        this.arms = arms;
        this.held = held;
        sources = new BodySurfacePart[poses.Count][];
        offsets = new int[poses.Count];
        var selected = new List<BodySurfacePart>();
        var map = new List<(int, int)>();
        var organs = new HashSet<string>(System.StringComparer.Ordinal);
        for (int source = 1; source < poses.Count; source++)
            foreach (BodySurfacePart part in poses[source].Parts)
                if (part.Id.StartsWith("organ:", System.StringComparison.Ordinal))
                    organs.Add(part.Id);
        int offset = 0;
        for (int source = 0; source < poses.Count; source++)
        {
            BodySurfacePose pose = poses[source];
            sources[source] = pose.Parts;
            offsets[source] = offset;
            for (int i = 0; i < pose.Parts.Length; i++)
            {
                BodySurfacePart part = pose.Parts[i];
                if (source == 0 && (organs.Contains(part.Id) || held && part.Attachment == "item" ||
                                    part.Attachment == null &&
                                    ((arms & BodyOccupancy.RightArm) != 0 && Right(part.Id) ||
                                     (arms & BodyOccupancy.LeftArm) != 0 && Left(part.Id)))) continue;
                selected.Add(new BodySurfacePart(part.Id, part.Vertices, part.Triangles, part.Attachment,
                    offset + (part.TransformIndex < 0 ? i : part.TransformIndex), part.Group));
                map.Add((source, i));
            }

            offset += pose.Matrices.Length;
        }

        parts = selected.ToArray();
        origins = map.ToArray();
    }

    /// <summary>资源、实际动作或控制部位变化时重建一次布局。</summary>
    /// <param name="poses">当前来源。</param><param name="arms">当前手臂控制。</param><param name="held">持物控制。</param>
    internal bool Matches(IReadOnlyList<BodySurfacePose> poses, BodyOccupancy arms, bool held)
    {
        if (poses.Count != sources.Length || arms != this.arms || held != this.held) return false;
        for (int i = 0; i < sources.Length; i++)
            if (!ReferenceEquals(poses[i].Parts, sources[i]))
                return false;
        return true;
    }

    /// <summary>只复制本轮变换和变形顶点引用，不复制共享资源或修改任一来源。</summary>
    /// <param name="poses">同一时点的实际来源。</param>
    internal BodySurfacePose Sample(IReadOnlyList<BodySurfacePose> poses)
    {
        int last = poses.Count - 1;
        var matrices = new Matrix4x4[offsets[last] + poses[last].Matrices.Length];
        for (int source = 0; source < poses.Count; source++) poses[source].Matrices.CopyTo(matrices, offsets[source]);
        BodySurfacePose body = poses[0];
        var result = new BodySurfacePose(parts, matrices, body.Foot, body.Height, body.Heading);
        bool deform = false;
        foreach (BodySurfacePose source in poses) deform |= source.HasDeformation;
        if (!deform) return result;
        var vertices = new Vector3[parts.Length][];
        for (int i = 0; i < origins.Length; i++) vertices[i] = poses[origins[i].Source].Vertices(origins[i].Part);
        return result.Deformed(vertices);
    }

    /// <summary>原身体的右臂及末端，由实际动作整体替换。</summary>
    /// <param name="part">原部位。</param>
    private static bool Right(string part) => part is "arm.right.upper" or "arm.right.lower" or "hand.right";

    /// <summary>原身体的左臂及末端，由实际动作整体替换。</summary>
    /// <param name="part">原部位。</param>
    private static bool Left(string part) => part is "arm.left.upper" or "arm.left.lower" or "hand.left";
}
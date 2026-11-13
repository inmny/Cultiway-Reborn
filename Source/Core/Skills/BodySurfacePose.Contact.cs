using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

internal sealed partial class BodySurfacePose
{
    /// <summary>真实足面与所选目标面按共同前后态接触，包围球只参与外层候选筛选。</summary>
    /// <param name="from">目标前态。</param><param name="to">目标后态。</param>
    /// <param name="sourceFrom">实际作用面前态。</param><param name="sourceTo">实际作用面后态。</param>
    /// <param name="selection">目标明确选择。</param><param name="at">最早接触进度。</param>
    /// <param name="anchor">目标原三角面内的位置。</param><param name="normal">由来源指向目标的方向。</param>
    internal static bool FindSurfaces(BodySurfacePose from, BodySurfacePose to, BodySurfacePose sourceFrom,
        BodySurfacePose sourceTo, string selection, out float at, out BodySurfaceAnchor anchor, out Vector3 normal)
    {
        // 正反有限边都要查询；小目标碰到大面内部时，只有反向边能找到真实接触。
        float first = float.PositiveInfinity;
        BodySurfaceAnchor selected = default;
        Vector3 direction = default;
        foreach (var edge in ContactEdges(sourceFrom, sourceTo, "surface"))
        {
            var begin = new AbilityCapsule(-1, edge.A0, edge.B0, 0f, null);
            var end = new AbilityCapsule(-1, edge.A1, edge.B1, 0f, null);
            if (!Find(from, to, begin, end, selection, out float fraction, out var found, out var outward) || fraction >= first) continue;
            first = fraction; selected = found; direction = outward;
        }
        foreach (var edge in ContactEdges(from, to, selection))
        {
            var begin = new AbilityCapsule(-1, edge.A0, edge.B0, 0f, null);
            var end = new AbilityCapsule(-1, edge.A1, edge.B1, 0f, null);
            if (!Find(sourceFrom, sourceTo, begin, end, "surface", out float fraction, out var found, out var outward) || fraction >= first) continue;
            BodySurfacePose target = Lerp(from, to, fraction), source = Lerp(sourceFrom, sourceTo, fraction);
            BodySurfacePart part = target.Parts[edge.Part];
            Matrix4x4 matrix = target.Matrices[part.TransformIndex < 0 ? edge.Part : part.TransformIndex];
            Vector3[] vertices = target.Vertices(edge.Part);
            selected = new BodySurfaceAnchor(edge.Part, edge.Face, found.Point(source),
                matrix.MultiplyPoint3x4(vertices[part.Triangles[edge.Face]]),
                matrix.MultiplyPoint3x4(vertices[part.Triangles[edge.Face + 1]]),
                matrix.MultiplyPoint3x4(vertices[part.Triangles[edge.Face + 2]]));
            first = fraction; direction = -outward;
        }
        at = first; anchor = selected; normal = direction;
        return !float.IsPositiveInfinity(first);
    }

    /// <summary>沿当前真实露出片段取得有限边，透明区域和被遮住的面不生成接触。</summary>
    /// <param name="from">同轮前态。</param><param name="to">同轮后态。</param><param name="selection">实际选择。</param>
    private static IEnumerable<(int Part, int Face, Vector3 A0, Vector3 B0, Vector3 A1, Vector3 B1)> ContactEdges(
        BodySurfacePose from, BodySurfacePose to, string selection)
    {
        for (int part = 0; part < to.Parts.Length; part++)
        {
            BodySurfacePart mesh = to.Parts[part];
            if (!mesh.Matches(selection)) continue;
            for (int face = 0; face < mesh.Triangles.Length; face += 3)
            {
                var before = from.Fragments(part, face);
                var after = to.Fragments(part, face);
                for (int polygon = 0; polygon < after.Count; polygon++)
                for (int corner = 0; corner < after[polygon].Length; corner++)
                {
                    int next = (corner + 1) % after[polygon].Length;
                    yield return (part, face, before[polygon][corner], before[polygon][next], after[polygon][corner], after[polygon][next]);
                }
            }
        }
    }
}

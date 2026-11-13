using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实身体的一个已有骨段；关节和坐标系随同一表面取样，不预设人形四肢。</summary>
internal readonly struct BodyStructureBone
{
    /// <summary>实际可选择的部位及其真实父部位，根部父名为空。</summary>
    internal readonly string Part, Parent;
    /// <summary>当前共同空间中的真实关节及其局部活动坐标系。</summary>
    internal readonly Vector3 Pivot;
    /// <summary>原制作坐标系，不根据部位名称推算活动轴。</summary>
    internal readonly Quaternion Axes;

    /// <summary>保存已经由真实资源采集的关节。</summary>
    /// <param name="part">实际部位。</param><param name="parent">真实父部位。</param>
    /// <param name="pivot">同轮关节。</param><param name="axes">原活动坐标系。</param>
    internal BodyStructureBone(string part, string parent, Vector3 pivot, Quaternion axes)
    { Part = part; Parent = parent; Pivot = pivot; Axes = axes; }
}

/// <summary>实际承托部位的最低接触边界，可为面、线或点，不补人形足距。</summary>
internal sealed class BodySupportPatch
{
    /// <summary>实际承托部位。</summary>
    internal readonly string Part;
    /// <summary>真实最低边界的世界顶点，供支撑过程建立边界。</summary>
    internal readonly Vector3[] Points;

    /// <summary>从已经确定的部位采集其最低真实顶点。</summary>
    /// <param name="part">真实部位。</param><param name="points">同轮完整部位表面。</param>
    internal BodySupportPatch(string part, Vector3[] points)
    {
        Part = part;
        float bottom = points.Min(point => point.z);
        Points = points.Where(point => point.z <= bottom + .00001f).Distinct().ToArray();
    }
}

/// <summary>同轮身体、骨段和承托的只读数值；采集后可交给共同动作和工作线程。</summary>
internal sealed class BodyStructure
{
    /// <summary>关节对应的同轮实际几何。</summary>
    internal readonly BodySurfacePose Surface;
    /// <summary>原身体实际提供的全部关节，缺失名称不能借用其他肢体。</summary>
    internal readonly IReadOnlyDictionary<string, BodyStructureBone> Bones;
    /// <summary>本身体自己的候选承托，不固定为左右双足；实际接地由支撑过程判断。</summary>
    internal readonly BodySupportPatch[] Supports;

    /// <summary>读取实际别名与父子结构，不根据类似名称猜测同一肢体。</summary>
    /// <param name="part">实际部位。</param><param name="ancestor">实际骨段。</param>
    internal bool IsWithin(string part, string ancestor)
    {
        if (part == ancestor || Surface.Parts.Any(piece => piece.Matches(part) && piece.Matches(ancestor))) return true;
        for (int i = 0; i < Bones.Count && Bones.TryGetValue(part, out var bone) && bone.Parent != null; i++)
        { part = bone.Parent; if (part == ancestor) return true; }
        return false;
    }

    /// <summary>建立一次骨段查询和实际支撑面，不持有任何游戏或三维模组对象。</summary>
    /// <param name="surface">同轮真实表面。</param><param name="bones">原资源已经提供的骨段。</param>
    /// <param name="supports">明确登记的真实承托部位。</param>
    internal BodyStructure(BodySurfacePose surface, IEnumerable<BodyStructureBone> bones, IEnumerable<string> supports)
    {
        Surface = surface;
        Bones = bones.Where(bone => surface.Supports(bone.Part)).ToDictionary(bone => bone.Part, StringComparer.Ordinal);
        var patches = new List<BodySupportPatch>();
        foreach (string part in supports.Distinct(StringComparer.Ordinal))
        {
            Vector3[] points = Points(surface, part);
            if (points.Length > 0) patches.Add(new BodySupportPatch(part, points));
        }
        Supports = patches.ToArray();
    }

    /// <summary>只采集所选实际三角面引用的顶点，空部位不给人物包络替代。</summary>
    /// <param name="surface">同轮身体。</param><param name="selection">实际选择名。</param>
    internal static Vector3[] Points(BodySurfacePose surface, string selection)
    {
        var result = new HashSet<Vector3>();
        for (int part = 0; part < surface.Parts.Length; part++)
        {
            if (!surface.Parts[part].Matches(selection)) continue;
            int index = surface.Parts[part].TransformIndex < 0 ? part : surface.Parts[part].TransformIndex;
            Vector3[] vertices = surface.Vertices(part);
            foreach (int vertex in surface.Parts[part].Triangles) result.Add(surface.Matrices[index].MultiplyPoint3x4(vertices[vertex]));
        }
        return result.ToArray();
    }
}

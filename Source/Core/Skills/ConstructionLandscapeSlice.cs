using System;
using System.IO;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>把实际景物带入有厚度的断面；闭合边沿用普通形体的运动和命中时段。</summary>
public sealed class ConstructionLandscapeSlice
{
    /// <summary>两条地图方向上的取景格数，也是局部断面的实际尺寸。</summary>
    public int Width = 8;
    public int Length = 3;

    /// <summary>地表脚下的断面厚度。</summary>
    public float Thickness = .55f;

    /// <summary>回合先触到目标的侧边。负一为左侧，正一为右侧。</summary>
    public int Edge = -1;

    /// <summary>同一真实刃口的宽度及其超出地面的高度。</summary>
    public float EdgeWidth = .18f;
    public float EdgeHeight = .86f;
}

/// <summary>供共同接触及显示读取的局部取景尺寸和实体刃口。</summary>
internal sealed class CompiledLandscapeSlice
{
    /// <summary>未缩放的长宽和实际岩层厚度。</summary>
    internal readonly int Width, Length;
    internal readonly float Thickness;

    /// <summary>刃口在部位内的完整空间范围。</summary>
    internal readonly Bounds Edge;

    /// <summary>检查真实形体类型和单条景物带的尺寸。</summary>
    /// <param name="source">作者给出的尺寸。</param><param name="resource">允许添加动态形体的空支点。</param>
    /// <param name="path">资料路径。</param>
    internal CompiledLandscapeSlice(ConstructionLandscapeSlice source, FormResource resource, string path)
    {
        if (source.Width is < 2 or > 24 || source.Length is < 1 or > 12 ||
            source.Width * source.Length > 192 || source.Edge is not (-1 or 1) ||
            !(source.Thickness >= .15f && source.Thickness <= 3f) ||
            !(source.EdgeWidth >= .06f && source.EdgeWidth <= .6f) ||
            !(source.EdgeHeight >= .15f && source.EdgeHeight <= 4f) ||
            resource.Meshes.Length != 0 || resource.Contacts.Length != 0)
            throw new InvalidDataException(path + "：分景断面需要空支点、有限取景格数和实际刃口尺寸");
        Width = source.Width;
        Length = source.Length;
        Thickness = source.Thickness;
        Edge = new Bounds(new Vector3(source.Edge * (Width - source.EdgeWidth) * .5f, 0f,
            (source.EdgeHeight - Thickness) * .5f), new Vector3(source.EdgeWidth, Length, source.EdgeHeight + Thickness));
    }

    /// <summary>与显示中的同一个实体刃口采用一样的实际变换。</summary>
    /// <param name="node">实际部位。</param><param name="pose">当前实际部位姿态。</param>
    /// <param name="revision">当前接触版本。</param><param name="matter">当前实际材料。</param>
    internal AbilityCapsule Contact(int node, in Matrix4x4 pose, int revision, AbilityMatter matter)
    {
        Vector3 centre = pose.MultiplyPoint3x4(Edge.center);
        var prism = new AbilityPrism(AbilityPrism.Rectangle, pose * Matrix4x4.TRS(Edge.center, Quaternion.identity, Edge.extents));
        return new AbilityCapsule(node, centre, centre, 0f, matter.Id, revision: revision, material: matter,
            prism: prism, sweepPose: new AbilitySweepPose(pose.GetColumn(3), pose.rotation));
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>按需在资源边界取得实际表面，施放与工作线程不读取Unity网格。</summary>
public sealed partial class FormResource
{
    /// <summary>本资源版本共享的各部件完整制作表面。</summary>
    private FormSurfaceMesh[] surfaces;

    /// <summary>同一部件只读取一次原网格及数值通道。</summary>
    /// <param name="part">已核对的部件索引。</param>
    internal FormSurfaceMesh SurfaceMesh(int part)
    {
        surfaces ??= new FormSurfaceMesh[Meshes.Length];
        return surfaces[part] ??= new FormSurfaceMesh(this, part);
    }
}

/// <summary>一个部件的只读数值制作表面，不保留Unity对象。</summary>
internal sealed class FormSurfaceMesh
{
    /// <summary>原资源中的部件。</summary>
    private readonly int part;
    /// <summary>三个索引对应一张实际三角面。</summary>
    private readonly int[] triangles;
    /// <summary>制作坐标。</summary>
    private readonly Vector3[] positions;
    /// <summary>原制作的控制、权重及成形通道。</summary>
    private readonly Vector4[] controls, weights, builds;
    /// <summary>刚性部件控制及是否采用蒙皮。</summary>
    private readonly int rigid;
    /// <summary>蒙皮标记。</summary>
    private readonly bool skin;
    /// <summary>各面面积，退化面为零，不作为候选。</summary>
    internal readonly double[] Areas;
    /// <summary>该部件的实际总面积。</summary>
    internal readonly double Area;

    /// <summary>在主线程读取及检查本次真正使用的资源。</summary>
    /// <param name="resource">所属完整版本。</param><param name="part">真实部件。</param>
    internal FormSurfaceMesh(FormResource resource, int part)
    {
        this.part = part; Mesh mesh = resource.Meshes[part];
        FormPartDescription description = resource.Description.Parts[part];
        string path = resource.Description.Id + "/" + description.Id + "/Surface";
        positions = mesh.vertices; triangles = mesh.triangles;
        rigid = resource.PartControls[part]; skin = description.Skin;
        /// <summary>只在当前资源边界复制一个制作通道。</summary><param name="channel">原网格通道。</param>
        Vector4[] Read(int channel) { var result = new List<Vector4>(); mesh.GetUVs(channel, result); return result.ToArray(); }
        controls = skin ? Read(1) : null; weights = skin ? Read(2) : null;
        builds = description.HasBuildData ? Read(4) : null;
        if (triangles.Length % 3 != 0 || triangles.Any(i => i < 0 || i >= positions.Length) ||
            skin && (controls.Length != positions.Length || weights.Length != positions.Length) ||
            builds != null && builds.Length != positions.Length)
            throw new InvalidDataException(path + "：表面索引或顶点资料不完整");
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 p = positions[i];
            if (!FormRig.Finite(p.x) || !FormRig.Finite(p.y) || !FormRig.Finite(p.z))
                throw new InvalidDataException(path + "：表面坐标不是有限值");
            if (skin)
            {
                float sum = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float index = controls[i][k], weight = weights[i][k];
                    if (!FormRig.Finite(index) || index != (int)index || index < 0 || index >= resource.Rig.Count ||
                        !FormRig.Finite(weight) || weight < 0f) throw new InvalidDataException(path + "：顶点控制或权重无效");
                    sum += weight;
                }
                if (Mathf.Abs(sum - 1f) > .00001f) throw new InvalidDataException(path + "：顶点权重没有归一");
            }
            if (builds != null && (Enumerable.Range(0, 4).Any(k => !FormRig.Finite(builds[i][k])) || builds[i].w < 0 || builds[i].w > 1))
                throw new InvalidDataException(path + "：截面中心或成形进度无效");
        }
        Areas = new double[triangles.Length / 3];
        for (int face = 0; face < Areas.Length; face++)
        {
            Vector3 cross = Vector3.Cross(positions[triangles[face * 3 + 1]] - positions[triangles[face * 3]],
                positions[triangles[face * 3 + 2]] - positions[triangles[face * 3]]);
            double area = Math.Sqrt(cross.sqrMagnitude) * .5d;
            if (double.IsNaN(area) || double.IsInfinity(area)) throw new InvalidDataException(path + "：三角面面积无效");
            Areas[face] = area; Area += area;
        }
    }

    /// <summary>只复制选中面的三个顶点；计划不会为每个点复制整件几何。</summary>
    /// <param name="face">已选择的非退化面。</param><param name="barycentric">已经确定的面内位置。</param>
    internal FormSurfaceBinding Bind(int face, Vector3 barycentric)
    {
        /// <summary>只取实际选中的制作顶点。</summary><param name="i">真实顶点索引。</param>
        FormSurfaceVertex Vertex(int i) => new(positions[i], builds?[i] ?? new Vector4(0, 0, 0, -1),
            controls?[i] ?? Vector4.zero, weights?[i] ?? Vector4.zero, rigid, skin);
        return new FormSurfaceBinding(part, face, barycentric, Vertex(triangles[face * 3]),
            Vertex(triangles[face * 3 + 1]), Vertex(triangles[face * 3 + 2]));
    }
}

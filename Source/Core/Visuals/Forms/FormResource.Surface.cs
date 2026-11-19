using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>按需在资源边界取得实际表面，施放与工作线程不读取Unity网格。</summary>
public sealed partial class FormResource
{
    /// <summary>主体是否提供材料槽；拆开后仍保留其余部件自己的表面。</summary>
    private readonly bool HasMatterParts;

    /// <summary>同材保留制作层次。没有材料槽时仅替换第一种主体表面，保留其余层。</summary>
    /// <param name="part">实际显示部件。</param><param name="surface">原资料的固定表面。</param>
    /// <param name="matterSurface">本次实际材料选出的表面。</param><param name="followMatter">是否主动跟随换材。</param>
    internal string SelectPartSurface(int part, string surface, string matterSurface, bool followMatter)
    {
        FormPartDescription piece = Description.Parts[part];
        if (surface != null) return surface;
        if (matterSurface == null || MaterialFamily(piece.Surface) == matterSurface) return piece.Surface;
        bool replace = HasMatterParts ? piece.UseMatterSurface : followMatter &&
            piece.Surface == (whole ?? this).Description.Parts[0].Surface;
        return replace ? matterSurface : piece.Surface;
    }

    /// <summary>制作表面的同材细分。真实材料或物态仍由构造单独决定。</summary>
    /// <param name="surface">制作时选择的表面。</param>
    private static string MaterialFamily(string surface) => surface switch
    {
        "stone_strata" => "stone",
        "wood_leaf" => "wood",
        "wind_open" or "wind_pressure" => "wind",
        "meteor_heat" or "meteor_crown" => "flame",
        "aegis_jade" or "spirit_core" or "spirit_beam_edge" or "spirit_beam_core" => "spirit_pearl",
        "aegis_solar" or "solar_gold" or "solar_beam_edge" or "solar_beam_flank" or "solar_beam_core" => "solar_light",
        "aegis_lightning" or "thunder_core" or "thunder_body" or "thunder_corona" or "thunder_arc" or "thunder_arc_fork" => "lightning_dense",
        "yin_basin" or "yin_moon" => "yin_depth",
        "poison_core" => "poison_vapor",
        "entropy_depth" => "entropy_veil",
        _ => surface
    };

    /// <summary>本资源版本共享的各部件完整制作表面。</summary>
    private FormSurfaceMesh[] surfaces;

    /// <summary>真正需要着地的构造在编译时取得完整主体，重复顶点只保存一次。</summary>
    private FormSurfaceVertex[] groundPoints;

    /// <summary>主线程一次采集实际制作、成形及骨架资料；运行只读纯数值。</summary>
    internal FormSurfaceVertex[] GroundPoints() => groundPoints ??= Enumerable.Range(0, Meshes.Length)
        .SelectMany(part => SurfaceMesh(part).Vertices()).Distinct().ToArray();

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
        return new FormSurfaceBinding(part, face, barycentric, Vertex(triangles[face * 3]),
            Vertex(triangles[face * 3 + 1]), Vertex(triangles[face * 3 + 2]));
    }

    /// <summary>只取边界已检查的同一个制作顶点。</summary><param name="i">真实顶点索引。</param>
    private FormSurfaceVertex Vertex(int i) => new(positions[i], builds?[i] ?? new Vector4(0, 0, 0, -1),
        controls?[i] ?? Vector4.zero, weights?[i] ?? Vector4.zero, rigid, skin);

    /// <summary>保留所有真实部件的物理底部，不把攻击面当作整个主体。</summary>
    internal IEnumerable<FormSurfaceVertex> Vertices() => Enumerable.Range(0, positions.Length).Select(Vertex);
}

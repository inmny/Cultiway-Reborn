using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际部位及表面与明确点共用同一有限分布，不按资源种类分派。</summary>
public sealed partial class ConstructionDistribution
{
    /// <summary>按真实部位选择和实际面积建立有限候选；只有编译边界访问资源。</summary>
    /// <param name="parent">父形体完整版本。</param><param name="path">外部资料位置。</param>
    private FormAnchorBinding[] SurfaceCandidates(FormResource parent, string path)
    {
        if (Selectors.Length == 0 || Points.Length != 0)
            throw new InvalidDataException(path + "：部位及表面来源需要部件名称或role:用途，不能混入明确点");
        int[] all = Enumerable.Range(0, parent.Description.Parts.Length).ToArray();
        /// <summary>名称及用途只匹配制作时明确声明的部件。</summary>
        /// <param name="part">真实部件索引。</param><param name="selector">当前已检查的选择条件。</param>
        bool MatchesPart(int part, string selector) => selector.StartsWith("role:", StringComparison.Ordinal)
            ? parent.Structure.Parts(selector.Substring(5)).Contains(part)
            : Matches(parent.Description.Parts[part].Id, selector);
        foreach (string selector in Selectors)
            if (string.IsNullOrWhiteSpace(selector) || selector.IndexOf('*') >= 0 &&
                (selector.StartsWith("role:", StringComparison.Ordinal) || selector.IndexOf('*') != selector.Length - 1) ||
                !all.Any(part => MatchesPart(part, selector)))
                throw new InvalidDataException(path + "/Selectors：部件名称、末尾前缀或role:用途没有匹配实际部位：" + selector);
        int[] parts = all.Where(part => Selectors.Any(selector => MatchesPart(part, selector))).ToArray();
        if (parts.Length > AbilityCompiler.MaxForms) throw new InvalidDataException(path + "：候选部件超过128份预算");
        FormSurfaceMesh[] meshes = parts.Select(part => parent.SurfaceMesh(part)).ToArray();
        if (meshes.Any(mesh => mesh.Area <= 0d)) throw new InvalidDataException(path + "：所选部件没有非退化的实际三角面");
        if (Source == ConstructionDistributionSource.Parts)
            return meshes.Select((mesh, i) => new FormAnchorBinding("part." + parent.Description.Parts[parts[i]].Id,
                mesh.Bind(Array.IndexOf(mesh.Areas, mesh.Areas.Max()), Vector3.one / 3f))).ToArray();

        // 等面积区间决定真实三角面，面内采用固定的分散序列；不保存随机运行状态。
        double total = meshes.Sum(mesh => mesh.Area), end = meshes[0].Areas[0];
        int selected = 0, face = 0;
        var result = new FormAnchorBinding[SurfaceSamples];
        for (int i = 0; i < result.Length; i++)
        {
            double target = (i + .5d) * total / result.Length;
            while (target >= end)
            {
                face++;
                if (face == meshes[selected].Areas.Length) { selected++; face = 0; }
                end += meshes[selected].Areas[face];
            }
            double u = (i + 1d) * .7548776662466927d % 1d, v = (i + 1d) * .5698402909980532d % 1d;
            float root = (float)Math.Sqrt(u);
            result[i] = new FormAnchorBinding("surface." + i, meshes[selected].Bind(face,
                new Vector3(1f - root, root * (1f - (float)v), root * (float)v)));
        }
        return result;
    }
}

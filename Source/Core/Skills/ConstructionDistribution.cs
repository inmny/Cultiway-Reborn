using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>为同一子结构提供位置的实际来源，不按技能名称分派。</summary>
public enum ConstructionDistributionSource
{
    /// <summary>父形体实际提供的命名挂点。</summary>
    Anchors = 0,

    /// <summary>父模型空间中的明确命名位置。</summary>
    Points = 1,

    /// <summary>每个所选真实部件的最大三角面中心。</summary>
    Parts = 2,

    /// <summary>所选真实部件表面上的有限面积采样。</summary>
    Surface = 3
}

/// <summary>可以保存及改造的命名位置；不是世界对象或一次施放的状态。</summary>
public sealed class ConstructionDistributionPoint
{
    /// <summary>本集合中稳定且不重复的名称，用于展开后的实例身份。</summary>
    public string Id;

    /// <summary>父模型空间中的实际位置。</summary>
    public float[] Position = new float[3];

    /// <summary>该位置朝外的方向，子结构仍可叠加自身旋转。</summary>
    public float[] Direction = { 0f, 0f, 1f };
}

/// <summary>先选择有限位置，再以同一规则放置完整子结构；运行只读取展开后的计划。</summary>
public sealed partial class ConstructionDistribution
{
    /// <summary>位置来自挂点、明确点、实际部件或实际表面。</summary>
    public ConstructionDistributionSource Source;

    /// <summary>挂点或部件名称、末尾星号前缀；部件另支持role:用途；按父资源顺序取并集。</summary>
    public string[] Selectors = Array.Empty<string>();

    /// <summary>作者明确给出的有序位置，使用Points来源时读取。</summary>
    public ConstructionDistributionPoint[] Points = Array.Empty<ConstructionDistributionPoint>();

    /// <summary>表面来源产生的候选数量，再共同应用起始、间隔及最多份数。</summary>
    public int SurfaceSamples = 1;

    /// <summary>先跳过的候选数量。</summary>
    public int First;

    /// <summary>每隔多少个候选取一份，一表示全部。</summary>
    public int Every = 1;

    /// <summary>本声明最多放置的份数，仍受整招形体与嵌套预算约束。</summary>
    public int Maximum = AbilityCompiler.MaxForms;

    /// <summary>在构造边界一次解析与检查，不在每帧查询名称或读取原始资料。</summary>
    /// <param name="parent">完整计划引用的父资源。</param><param name="path">分布资料的位置。</param>
    internal FormAnchorBinding[] Resolve(FormResource parent, string path)
    {
        // 先核对共同选取规则，再解析实际来源，最后按相同顺序取有限实例。
        if (!Enum.IsDefined(typeof(ConstructionDistributionSource), Source) || First < 0 || Every < 1 ||
            Maximum < 1 || Maximum > AbilityCompiler.MaxForms)
            throw new InvalidDataException(path + "：来源无效，起始须非负、间隔至少1、最多份数须在1至128之间");
        if (Selectors == null || Points == null || Selectors.Length > AbilityCompiler.MaxForms ||
            Points.Length > AbilityCompiler.MaxForms)
            throw new InvalidDataException(path + "：选择名称与明确点须为有限列表，各最多128项");
        if (SurfaceSamples < 1 || SurfaceSamples > AbilityCompiler.MaxForms ||
            Source != ConstructionDistributionSource.Surface && SurfaceSamples != 1)
            throw new InvalidDataException(path + "/SurfaceSamples：表面候选须在1至128之间，其他来源不读取此参数");
        FormAnchorBinding[] candidates;
        if (Source == ConstructionDistributionSource.Anchors)
        {
            if (Selectors.Length == 0 || Points.Length != 0)
                throw new InvalidDataException(path + "：挂点来源需要名称列表，不能混入明确点");
            foreach (string selector in Selectors)
            {
                if (string.IsNullOrWhiteSpace(selector) || selector.IndexOf('*') >= 0 &&
                    selector.IndexOf('*') != selector.Length - 1)
                    throw new InvalidDataException(path + "/Selectors：名称不能为空，星号只能出现在末尾");
                if (!parent.Anchors.Any(point => Matches(point.Id, selector)))
                    throw new InvalidDataException(path + "/Selectors：父形体" + parent.Description.Id + "没有挂点：" + selector);
            }

            candidates = parent.Anchors.Where(point => Selectors.Any(selector => Matches(point.Id, selector))).ToArray();
        }
        else if (Source == ConstructionDistributionSource.Points)
        {
            if (Points.Length == 0 || Selectors.Length != 0)
                throw new InvalidDataException(path + "：明确点来源需要位置列表，不能混入挂点名称");
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            candidates = new FormAnchorBinding[Points.Length];
            for (int i = 0; i < Points.Length; i++)
            {
                ConstructionDistributionPoint point = Points[i];
                string at = path + "/Points[" + i + "]";
                if (point == null || string.IsNullOrWhiteSpace(point.Id) || point.Id.IndexOfAny(new[] { '/', '@', '*' }) >= 0 ||
                    !ids.Add(point.Id))
                    throw new InvalidDataException(at + "：点名须唯一且不能包含/、@或星号");
                if (point.Position?.Length != 3 || point.Direction?.Length != 3)
                    throw new InvalidDataException(at + "：位置与方向都需要三个有限坐标");
                Vector3 position = FormRig.Vector(point.Position, 0, at + "/Position");
                Vector3 direction = FormRig.Vector(point.Direction, 0, at + "/Direction");
                if (!FormRig.Finite(direction.sqrMagnitude) || direction.sqrMagnitude < .00000001f)
                    throw new InvalidDataException(at + "/Direction：需要有限的非零方向");
                candidates[i] = new FormAnchorBinding(new FormAnchorDescription
                {
                    Id = "point." + point.Id,
                    Position = new[] { position.x, position.y, position.z },
                    Direction = new[] { direction.x, direction.y, direction.z }
                }, parent.Rig, null);
            }
        }
        else candidates = ConstructionReferences.Resolve(path, () => SurfaceCandidates(parent, path));

        FormAnchorBinding[] result = candidates.Where((_, index) => index >= First && (index - First) % Every == 0)
            .Take(Maximum).ToArray();
        if (result.Length == 0) throw new InvalidDataException(path + "：起始与间隔之后没有实际位置，不能退回原点");
        return result;
    }

    /// <summary>同一明确名称与末尾前缀规则，不把通配符解释为任意查询。</summary>
    /// <param name="id">父资源的实际名称。</param><param name="selector">已经核对的选择条件。</param>
    private static bool Matches(string id, string selector) => selector.EndsWith("*", StringComparison.Ordinal)
        ? id.StartsWith(selector.Substring(0, selector.Length - 1), StringComparison.Ordinal)
        : id == selector;
}

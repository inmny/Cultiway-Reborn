using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>形体的制作与组合合同，不包含世界中某次施放的状态。</summary>
public sealed class FormStructureDescription
{
    /// <summary>明确声明的结构种类：刚性、关节、蒙皮、分枝、曲面、体积或支点。</summary>
    public string Kind;
    /// <summary>模型一个单位对应的地图长度；实例仍可按共同变换缩放。</summary>
    public float UnitInTiles = 1f;
    /// <summary>能重新找到制作入口及生成输入的资料。</summary>
    public FormGenerationDescription Generation;
    /// <summary>绑定空间中明确声明的部位连接，不代表运行中已被拆开。</summary>
    public FormConnectionDescription[] Connections = Array.Empty<FormConnectionDescription>();
    /// <summary>资源实际支持的变化，不声明尚未制作的生长、裂纹等能力。</summary>
    public string[] Changes = Array.Empty<string>();
}

/// <summary>可追溯的制作入口与实际生成参数。</summary>
public sealed class FormGenerationDescription
{
    /// <summary>相对仓库的制作源路径，不在游戏中执行。</summary>
    public string Source;
    /// <summary>源内的函数或明确制作段名称。</summary>
    public string Entry;
    /// <summary>关键尺寸、采样或路径等数值输入；不保存Unity对象。</summary>
    public FormParameterDescription[] Parameters = Array.Empty<FormParameterDescription>();
}

/// <summary>制作参数；路径等多值输入按制作顺序存放。</summary>
public sealed class FormParameterDescription
{
    /// <summary>本入口内稳定的参数名。</summary>
    public string Id;
    /// <summary>一个或多个有限数值。</summary>
    public float[] Values;
}

/// <summary>两个真实部位在同一模型空间中的连接。</summary>
public sealed class FormConnectionDescription
{
    /// <summary>同资源内稳定的连接编号。</summary>
    public string Id;
    /// <summary>父部位稳定编号。</summary>
    public string Parent;
    /// <summary>子部位稳定编号。</summary>
    public string Child;
    /// <summary>绑定模型空间中的连接位置，不使用二维图片比例。</summary>
    public float[] Position;
    /// <summary>是否提供作为真实分件解除连接的资料。</summary>
    public bool Detachable;
}

/// <summary>名称已经解析的连接；不引用模型、材质或世界对象。</summary>
public readonly struct FormConnection
{
    /// <summary>稳定连接编号。</summary>
    public readonly string Id;
    /// <summary>父、子真实部件索引。</summary>
    public readonly int Parent, Child;
    /// <summary>绑定模型空间中的连接位置。</summary>
    public readonly Vector3 Position;
    /// <summary>制作资料是否允许此连接解除。</summary>
    public readonly bool Detachable;

    /// <summary>加载时一次解析，后续只按索引读取。</summary>
    /// <param name="source">已检查的连接说明。</param>
    /// <param name="parent">已解析父部件。</param>
    /// <param name="child">已解析子部件。</param>
    internal FormConnection(FormConnectionDescription source, int parent, int child)
    {
        Id = source.Id; Parent = parent; Child = child;
        Position = FormRig.Vector(source.Position, 0, source.Id); Detachable = source.Detachable;
    }
}

/// <summary>共用结构索引；资源发布后不再读取可编辑的制作说明。</summary>
public sealed class FormStructure
{
    /// <summary>制作时声明的结构种类。</summary>
    public readonly string Kind;
    /// <summary>制作单位与地图长度的对应关系。</summary>
    public readonly float UnitInTiles;
    /// <summary>制作源相对路径及源内入口，只用于资料追溯。</summary>
    public readonly string Source, Entry;
    /// <summary>已复制的只读数值生成输入，不引用可编辑数组。</summary>
    public readonly ReadOnlyDictionary<string, ReadOnlyCollection<float>> Parameters;
    /// <summary>每个真实部位的用途集合，查询只在编译边界进行。</summary>
    private readonly Dictionary<string, ReadOnlyCollection<int>> roles = new(StringComparer.Ordinal);
    /// <summary>已声明且经过实际资料核对的变化能力。</summary>
    private readonly HashSet<string> changes;
    /// <summary>不含Unity对象的只读连接列表。</summary>
    public readonly ReadOnlyCollection<FormConnection> Connections;

    /// <summary>取得拥有指定用途的实际部位，未知用途为空，不退回整物。</summary>
    /// <param name="role">制作时声明的用途。</param>
    public IReadOnlyList<int> Parts(string role) => roles.TryGetValue(role, out var found) ? found : Array.Empty<int>();

    /// <summary>查询制作资料明确支持的变化，不根据文件名推测。</summary>
    /// <param name="change">变化名称。</param>
    public bool Supports(string change) => changes.Contains(change);

    /// <summary>只在外部资料进入资源组时验证并复制结构。</summary>
    /// <param name="description">同一资源版本的完整形体说明。</param>
    internal FormStructure(FormDescription description)
    {
        string path = description.Id;
        FormStructureDescription source = description.Structure;
        if (source == null || !new[] { "rigid", "articulated", "skinned", "branch", "surface", "volume", "anchor" }.Contains(source.Kind) ||
            !FormRig.Finite(source.UnitInTiles) || source.UnitInTiles <= 0f || source.Generation == null ||
            string.IsNullOrWhiteSpace(source.Generation.Source) || string.IsNullOrWhiteSpace(source.Generation.Entry) ||
            source.Generation.Parameters == null || source.Connections == null || source.Changes == null)
            throw new InvalidDataException(path + "：缺少结构种类、制作单位、来源、连接或变化声明");
        Kind = source.Kind; UnitInTiles = source.UnitInTiles;
        Source = source.Generation.Source; Entry = source.Generation.Entry;
        var parameters = new HashSet<string>(StringComparer.Ordinal);
        var values = new Dictionary<string, ReadOnlyCollection<float>>(StringComparer.Ordinal);
        foreach (FormParameterDescription parameter in source.Generation.Parameters)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Id) || !parameters.Add(parameter.Id) ||
                parameter.Values == null || parameter.Values.Length == 0 || parameter.Values.Any(v => !FormRig.Finite(v)))
                throw new InvalidDataException(path + "：制作参数缺少名称、数值或存在重复");
            values.Add(parameter.Id, Array.AsReadOnly((float[])parameter.Values.Clone()));
        }
        Parameters = new ReadOnlyDictionary<string, ReadOnlyCollection<float>>(values);
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (int i = 0; i < description.Parts.Length; i++)
        {
            FormPartDescription part = description.Parts[i];
            if (part.Roles == null || part.Roles.Length == 0 || part.Roles.Any(string.IsNullOrWhiteSpace) || part.Roles.Distinct().Count() != part.Roles.Length)
                throw new InvalidDataException(path + "/" + part.Id + "：部位需要不重复的用途声明");
            names.Add(part.Id, i);
            foreach (string role in part.Roles)
            {
                if (!groups.TryGetValue(role, out var group)) groups.Add(role, group = new List<int>());
                group.Add(i);
            }
        }
        foreach (var group in groups) roles.Add(group.Key, Array.AsReadOnly(group.Value.ToArray()));
        var connectionIds = new HashSet<string>(StringComparer.Ordinal);
        var connections = new List<FormConnection>();
        foreach (FormConnectionDescription connection in source.Connections)
        {
            if (connection == null || string.IsNullOrWhiteSpace(connection.Id) || !connectionIds.Add(connection.Id) ||
                !names.TryGetValue(connection.Parent ?? "", out int parent) || !names.TryGetValue(connection.Child ?? "", out int child) || parent == child)
                throw new InvalidDataException(path + "：连接名称重复或引用了不存在的两个部位");
            connections.Add(new FormConnection(connection, parent, child));
        }
        Connections = Array.AsReadOnly(connections.ToArray());
        changes = new HashSet<string>(source.Changes, StringComparer.Ordinal);
        if (changes.Count != source.Changes.Length || changes.Any(c => !new[] { "transform", "material", "separate", "motion", "ground" }.Contains(c)) ||
            changes.Contains("separate") && (description.Parts.Length < 2 || connections.Count == 0 || connections.Any(c => !c.Detachable)) ||
            changes.Contains("motion") && (description.Controls.Length == 0 || description.Motions.Length == 0) ||
            changes.Contains("ground") && !description.ConformToGround ||
            changes.Contains("material") && !description.Parts.Any(p => p.UseMatterSurface))
            throw new InvalidDataException(path + "：变化声明重复、未知或缺少实际部件、动作、地表或材料资料");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Core.Skills;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>请求受理时的模型数值输入，不持有网格或材质；升级资源后不重算等待中的结构。</summary>
public sealed class SynthesisResources
{
    /// <summary>第二版生成规则所需的基础形体，不是完整法术清单。</summary>
    internal static readonly string[] RequiredForms =
        { "sword", "hand", "branch", "water_wave", "water_flow", "water_whale", "fire_body" };

    /// <summary>此份资源输入的完整数值。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly SynthesisForm[] Forms;

    /// <summary>受理时完整身体动作，旧请求为两份掌法，新请求保存本身体已选动作或明确无身体来源。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly AbilityDefinition[] Bodies;

    /// <summary>受理时确定的法力通道；真实身体动作另用体力，旧请求继续灵力。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly string Resource;

    /// <summary>受理时可选的世界操作；旧请求缺省为空，不从当前目录补写。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public readonly SynthesisWorldAction[] WorldActions;

    /// <summary>受理时登记的其他可变模型，保留原有基础模型的选择顺序。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public readonly SynthesisForm[] DeformationForms;

    /// <summary>只为当前输入建立的查找，不保存运行时对象。</summary>
    private readonly Dictionary<string, SynthesisForm> forms;

    /// <summary>在正式读取边界检查有限数量、重复和缺失，不用当前资源补写历史输入。</summary>
    /// <param name="forms">受理时完整记录的基础形体。</param>
    /// <param name="bodies">当时确定的有限身体动作完整资料。</param>
    /// <param name="resource">受理时实际采用的法力通道。</param>
    /// <param name="worldActions">受理时的世界操作编号和默认持续时间。</param>
    /// <param name="deformationForms">基础模型以外的实际可变模型。</param>
    [JsonConstructor]
    public SynthesisResources(SynthesisForm[] forms, AbilityDefinition[] bodies, string resource = "wakan",
        SynthesisWorldAction[] worldActions = null, SynthesisForm[] deformationForms = null)
    {
        if (forms == null || forms.Length > 16 || forms.Any(form => form == null) ||
            forms.Select(form => form.Id).Distinct(StringComparer.Ordinal).Count() != forms.Length ||
            RequiredForms.Any(id => forms.All(form => form.Id != id)))
            throw new ArgumentException("创作形体输入缺失、重复或超过预算");
        if (bodies == null || bodies.Length > 32 || bodies.Any(body => body?.Body == null || body.Root != null))
            throw new ArgumentException("创作身体动作输入缺失或格式无效");
        if (worldActions != null && (worldActions.Length > 128 || worldActions.Any(action => action == null) ||
            worldActions.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count() != worldActions.Length))
            throw new ArgumentException("创作世界操作输入重复或超过预算");
        WorldActions = worldActions;
        if (deformationForms != null && (deformationForms.Length > 128 ||
            deformationForms.Any(form => form == null || !form.Deformable) ||
            deformationForms.Select(form => form.Id).Distinct(StringComparer.Ordinal).Count() != deformationForms.Length ||
            deformationForms.Any(form => forms.Any(basis => basis.Id == form.Id))))
            throw new ArgumentException("其他形变模型输入重复、无真实控制或超过预算");
        DeformationForms = deformationForms;
        Forms = forms;
        Bodies = bodies;
        if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("创法资源通道缺失");
        Resource = resource;
        this.forms = forms.ToDictionary(form => form.Id, StringComparer.Ordinal);
    }

    /// <summary>只读已经检查的固定形体资料。</summary>
    /// <param name="id">生成规则已声明的形体。</param>
    internal SynthesisForm Get(string id) => forms[id];

    /// <summary>每个已受理请求独占可保存数组，编辑一个请求不能改写其他等待输入。</summary>
    internal SynthesisResources Copy() => new(Forms.Select(CopyForm).ToArray(), Bodies.Select(ConstructionEditing.Copy).ToArray(), Resource,
        WorldActions?.Select(action => new SynthesisWorldAction(action.Id, action.DefaultDuration)).ToArray(),
        DeformationForms?.Select(CopyForm).ToArray());

    /// <summary>两类模型输入共用逐数组复制，避免请求之间共享可编辑控制名称。</summary>
    /// <param name="form">已检查的冻结资料。</param>
    private static SynthesisForm CopyForm(SynthesisForm form) => new(form.Id, form.Size,
        (float[])form.Forward.Clone(), (float[])form.Up.Clone(), (string[])form.Anchors.Clone(),
        (string[])form.Motions.Clone(), (string[])form.Parts.Clone(), form.SpanRadius,
        form.Formations == null ? null : (string[])form.Formations.Clone(), form.Kind,
        form.Controls == null ? null : (string[])form.Controls.Clone());
}

/// <summary>一个模型参与创作所需的尺寸、坐标、挂点和动作编号。</summary>
public sealed class SynthesisForm
{
    /// <summary>实际资源编号。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly string Id;

    /// <summary>受理时绑定姿态的实际包围长度。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly float Size;

    /// <summary>受理时制作前向。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly float[] Forward;

    /// <summary>受理时制作上向。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly float[] Up;

    /// <summary>当时可供选择的明确挂点。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly string[] Anchors;

    /// <summary>当时可供选择的实际制作动作。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly string[] Motions;

    /// <summary>受理时可供部位和表面分布选择的实际部件；旧请求经转换保留空集合。</summary>
    [JsonProperty(Required = Required.Always)]
    public readonly string[] Parts;

    /// <summary>受理时的真实伸展半径；旧输入和不支持伸展的模型为空。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public readonly float? SpanRadius;

    /// <summary>受理时确实逐段建立几何的动作，旧请求保持为空。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public readonly string[] Formations;

    /// <summary>受理时的真实结构种类，旧输入保持为空。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public readonly string Kind;

    /// <summary>受理时已有的控制名称，不能在等待后重新选择。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public readonly string[] Controls;

    /// <summary>冻结资料中可接受自由控制的实际模型。</summary>
    [JsonIgnore] public bool Deformable => Controls?.Length > 0 && Kind is not ("rigid" or "anchor");

    /// <summary>从固定数值计算的制作朝向，不作为另一份长期数据保存。</summary>
    [JsonIgnore] internal readonly Quaternion Facing;

    /// <summary>拒绝损坏的尺寸、坐标和有限编号集合。</summary>
    /// <param name="id">基础资源编号。</param>
    /// <param name="size">正的有限绑定长度。</param>
    /// <param name="forward">三个制作前向数值。</param>
    /// <param name="up">三个制作上向数值。</param>
    /// <param name="anchors">明确挂点编号。</param>
    /// <param name="motions">实际动作编号。</param>
    /// <param name="parts">第九版固定的真实部件名称，旧规则为空集合。</param>
    /// <param name="spanRadius">第十版固定的实际伸展能力。</param>
    /// <param name="formations">第二十三版固定的逐段成形动作。</param>
    /// <param name="kind">第二十四版固定的模型结构种类。</param>
    /// <param name="controls">同版本已有的真实控制名称。</param>
    [JsonConstructor]
    public SynthesisForm(string id, float size, float[] forward, float[] up, string[] anchors, string[] motions, string[] parts = null, float? spanRadius = null, string[] formations = null, string kind = null, string[] controls = null)
    {
        bool Names(string[] values, int maximum) => values != null && values.Length <= maximum &&
                                                    values.All(value => !string.IsNullOrWhiteSpace(value)) &&
                                                    values.Distinct(StringComparer.Ordinal).Count() == values.Length;

        if (string.IsNullOrWhiteSpace(id) || float.IsNaN(size) || float.IsInfinity(size) || size <= 0f ||
            !Names(anchors, 256) || !Names(motions, 64)) throw new ArgumentException("创作形体尺寸或编号集合无效");
        parts ??= Array.Empty<string>();
        if (!Names(parts, 256)) throw new ArgumentException("创作部件名称缺失、重复或超过预算");
        if (spanRadius.HasValue && (float.IsNaN(spanRadius.Value) || float.IsInfinity(spanRadius.Value) || spanRadius <= 0f))
            throw new ArgumentException("创作伸展半径须为正的有限数值");
        SpanRadius = spanRadius;
        if (formations != null && (!Names(formations, 64) || formations.Any(clip => !motions.Contains(clip))))
            throw new ArgumentException("逐段成形动作需要来自同一份实际动作输入");
        Formations = formations;
        if ((kind == null) != (controls == null) || kind != null &&
            (!new[] { "rigid", "articulated", "skinned", "branch", "surface", "volume", "anchor" }.Contains(kind) ||
             !Names(controls, FormRig.MaximumControls)))
            throw new ArgumentException("自由形变需要同一资源中的真实结构种类和控制名称");
        Kind = kind;
        Controls = controls;
        Parts = parts;
        Facing = FormResource.ReadFacing(forward, up, id);
        Id = id;
        Size = size;
        Forward = forward;
        Up = up;
        Anchors = anchors;
        Motions = motions;
    }
}

/// <summary>每个资源版本只采集一次纯数值，多个等待请求可共用，完成后释放请求引用。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>仅供本进程识别资源重载，不把运行时版本号写入长期资料。</summary>
    private static int synthesisResourceVersion = -1;

    /// <summary>当前资源的共享纯数值输入，不持有图形资源。</summary>
    private static SynthesisResources synthesisResources;

    /// <summary>不编译整招，只读取已发布资源的尺寸、坐标和稳定名称。</summary>
    /// <param name="independent">受理请求时独占数组，临时生成只读当前缓存。</param>
    private static SynthesisResources CaptureSynthesisResources(bool independent = false)
    {
        if (synthesisResourceVersion == FormResources.Version)
            return independent ? synthesisResources.Copy() : synthesisResources;
        string[] baseIds = SynthesisResources.RequiredForms.Concat(new[] { "energy_ray", "root_network", "spirit_vine" }).ToArray();
        synthesisResources = new SynthesisResources(baseIds.Select(CaptureSynthesisForm).ToArray(),
            BodyActionCatalog.All.Take(2).Select(ConstructionEditing.Copy).ToArray(), deformationForms:
            FormResources.Ids.Where(id => !baseIds.Contains(id) && FormResources.Load(id).Deformable).OrderBy(id => id, StringComparer.Ordinal)
                .Select(CaptureSynthesisForm).ToArray());
        synthesisResourceVersion = FormResources.Version;
        return independent ? synthesisResources.Copy() : synthesisResources;
    }

    /// <summary>两类选择共用实际资源采集，只有纯数值和稳定名称进入等待请求。</summary>
    /// <param name="id">当前已登记的模型。</param>
    private static SynthesisForm CaptureSynthesisForm(string id)
    {
        FormResource resource = FormResources.Load(id);
        return new SynthesisForm(id, resource.RestSize, (float[])resource.Description.Forward.Clone(),
                (float[])resource.Description.Up.Clone(),
                resource.Description.Anchors.Select(anchor => anchor.Id).ToArray(),
                resource.Description.Motions.Select(motion => motion.Id).ToArray(),
                resource.Description.Parts.Select(part => part.Id).ToArray(), resource.Span?.Radius,
                resource.Builds ? resource.Description.Motions.Where(motion => motion.Keys[0].Build < motion.Keys[motion.Keys.Length - 1].Build).Select(motion => motion.Id).ToArray() : Array.Empty<string>(),
            resource.Structure.Kind, resource.Description.Controls.Select(control => control.Id).ToArray());
    }
}

using System;
using System.ComponentModel;
using System.IO;
using Newtonsoft.Json;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>通过一个活动介质部位传递结果，人物必须实际处在触发点所在的连通部分。</summary>
public sealed class ConstructionSpreadMedium
{
    /// <summary>部位明确声明的介质通道，不按形体名字猜测。</summary>
    public string Channel;
    /// <summary>要求的实际材料；空值不限制材料。</summary>
    public string Matter;
    /// <summary>要求的实际物态；材料不为空时可省略为默认物态。</summary>
    public string State;
    /// <summary>按距离依次选取的额外人物和建筑总上限。</summary>
    public int MaximumTargets = 3;
    /// <summary>同次施放的同通道只使用一次，不因后续部位或反射重置。</summary>
    [DefaultValue(true), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool OncePerCast = true;
    /// <summary>额外人物还接收原接触附加作用，介质分送本身不会递归。</summary>
    public bool InheritContactEffects;
    /// <summary>可选的支持伸展的反馈模型，空值只结算。</summary>
    public string LinkForm;
    /// <summary>反馈模型的世界横截面半径，不随连接长度扩大。</summary>
    public float LinkRadius = .09f;
}

/// <summary>资料边界检查的介质选择和反馈，运行只读取独立参数。</summary>
public sealed class CompiledSpreadMedium
{
    /// <summary>已经检查的实际通道、材料和物态。</summary>
    public string Channel { get; }
    /// <summary>要求的实际材料，空值不限制。</summary>
    public AbilityMatter Matter { get; }
    /// <summary>额外人物的有限上限。</summary>
    public int MaximumTargets { get; }
    /// <summary>同施放通道是否只使用一次。</summary>
    public bool OncePerCast { get; }
    /// <summary>是否还应用原接触附加作用。</summary>
    public bool InheritContactEffects { get; }
    /// <summary>本次编译所属版本的反馈资源。</summary>
    internal FormResource Link { get; }
    /// <summary>明确世界厚度。</summary>
    internal float LinkRadius { get; }

    /// <summary>只在创建、保存与编辑边界解析，不在运行时查可修改资料。</summary>
    /// <param name="source">外部介质规则。</param><param name="path">实际资料位置。</param>
    internal CompiledSpreadMedium(ConstructionSpreadMedium source, string path)
    {
        ValidateChannel(source.Channel, path + "/Channel", false);
        if (source.MaximumTargets < 1 || source.MaximumTargets > 64 || source.Matter == null && source.State != null ||
            float.IsNaN(source.LinkRadius) || float.IsInfinity(source.LinkRadius) || source.LinkRadius <= 0f || source.LinkRadius > 100f)
            throw new InvalidDataException(path + "：介质接收者数量须为1至64，物态须有材料，反馈厚度须为正数且不超过100");
        Channel = source.Channel; MaximumTargets = source.MaximumTargets; OncePerCast = source.OncePerCast;
        InheritContactEffects = source.InheritContactEffects; LinkRadius = source.LinkRadius;
        if (source.Matter != null) Matter = AbilityMatters.Resolve(source.Matter, source.State, path + "/Matter");
        if (source.LinkForm != null)
        {
            Link = ConstructionReferences.Resolve(path + "/LinkForm", () => FormResources.Load(source.LinkForm), "Form", source.LinkForm);
            if (Link.Span == null) throw new InvalidDataException(path + "：介质反馈模型须明确支持纵向伸展");
        }
    }

    /// <summary>部位声明和来源选择共用相同的通道边界。</summary>
    /// <param name="channel">准确通道名。</param><param name="path">资料位置。</param><param name="optional">是否允许省略声明。</param>
    internal static void ValidateChannel(string channel, string path, bool optional = true)
    {
        if (channel == null && optional) return;
        if (string.IsNullOrWhiteSpace(channel) || channel.Length > 128)
            throw new InvalidDataException(path + "：介质通道需要1至128字符的非空名称");
    }
}

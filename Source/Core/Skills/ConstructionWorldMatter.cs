using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>材料转换同时作用于真实范围内的世界生成材料。</summary>
public sealed class ConstructionWorldMatter
{
    /// <summary>明确的来源材料，空值允许任意已经登记的材料。</summary>
    public string FromMatter;

    /// <summary>来源物态，空值允许该材料的所有已登记物态。</summary>
    public string FromState;

    /// <summary>只转换原支付者为当前控制者的材料，不转移支付归属。</summary>
    public bool OwnedOnly;

    /// <summary>一次操作最多实际转换的地图立方格。</summary>
    public float MaximumVolume = 16;
}

/// <summary>入口已经检查的世界材料选择与转换容量。</summary>
internal sealed class CompiledWorldMatter
{
    /// <summary>同一部位资料内的先后顺序。</summary>
    internal readonly int Order;

    /// <summary>来源筛选仍使用稳定材料与物态名。</summary>
    internal readonly string FromMatter, FromState;

    /// <summary>是否限制为当前控制者原支付的材料。</summary>
    internal readonly bool OwnedOnly;

    /// <summary>一次操作的实际体积容量。</summary>
    internal readonly double MaximumVolume;

    /// <summary>只在构造资料入口核对，数值和确认阶段只读。</summary>
    /// <param name="source">明确选择参数。</param><param name="path">错误位置。</param><param name="order">同部位资料顺序。</param>
    internal CompiledWorldMatter(ConstructionWorldMatter source, string path, int order = 0)
    {
        if (float.IsNaN(source.MaximumVolume) || float.IsInfinity(source.MaximumVolume) || source.MaximumVolume <= 0 ||
            source.MaximumVolume > 1e9f || source.FromState != null && source.FromMatter == null)
            throw new InvalidDataException(path + "：世界材料转换需要正数容量，物态筛选须指定来源材料");
        if (source.FromMatter != null) AbilityMatters.Resolve(source.FromMatter, source.FromState, path + "/From");
        Order = order;
        FromMatter = source.FromMatter;
        FromState = source.FromState;
        OwnedOnly = source.OwnedOnly;
        MaximumVolume = source.MaximumVolume;
    }

    /// <summary>不凭颜色猜测匹配，已经相同的输出也不重复转换。</summary>
    /// <param name="source">真实来源材料。</param><param name="target">明确输出。</param>
    internal bool Matches(AbilityMatter source, AbilityMatter target) => !ReferenceEquals(source, target) &&
                                                                         (FromMatter == null ||
                                                                          source.Id == FromMatter &&
                                                                          (FromState == null ||
                                                                           source.State == FromState));
}

/// <summary>一次已确认前的纯数值请求，只在主线程接受后改变世界。</summary>
internal readonly struct AbilityWorldMatterRequest
{
    /// <summary>实际选择部位。</summary>
    internal readonly int Node;

    /// <summary>明确输出及筛选规则。</summary>
    internal readonly AbilityMatter Matter;

    /// <summary>已经检查的容量及来源筛选。</summary>
    internal readonly CompiledWorldMatter Selection;

    /// <summary>从真实执行状态取得，不包含人物或Unity对象。</summary>
    /// <param name="node">实际部位。</param><param name="matter">明确输出。</param><param name="selection">筛选和容量。</param>
    internal AbilityWorldMatterRequest(int node, AbilityMatter matter, CompiledWorldMatter selection)
    {
        Node = node;
        Matter = matter;
        Selection = selection;
    }
}
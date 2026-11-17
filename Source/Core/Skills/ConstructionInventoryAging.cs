using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>真实人物接触推进其当前库存物件已有的有限寿命。</summary>
public sealed class ConstructionInventoryAging
{
    /// <summary>每件原物件额外推进的世界秒数。</summary>
    public float Seconds = 60;
    /// <summary>每次接触最多接受的原物件数。</summary>
    public int MaximumItems = 1;
    /// <summary>可选的物件形状筛选，空值或空表接受全部形状。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string[] Shapes;
}

/// <summary>编译后固定的寿命推进量、件数及形状，不持有原物件。</summary>
public sealed class CompiledInventoryAging
{
    /// <summary>每件物件额外推进的世界秒数。</summary>
    public float Seconds { get; }
    /// <summary>每次真正接受的件数上限。</summary>
    public int MaximumItems { get; }
    /// <summary>已复制的形状筛选，空表接受全部形状。</summary>
    public IReadOnlyList<string> Shapes { get; }

    /// <summary>在资料边界核对有限进度和范围，实际形状由内容入口解析。</summary>
    /// <param name="source">原声明。</param><param name="path">错误对应位置。</param>
    internal CompiledInventoryAging(ConstructionInventoryAging source, string path)
    {
        if (float.IsNaN(source.Seconds) || float.IsInfinity(source.Seconds) || source.Seconds <= 0 || source.Seconds > 1e9f ||
            source.MaximumItems < 1 || source.MaximumItems > 1024 || source.Shapes?.Length > 32 ||
            source.Shapes != null && (source.Shapes.Any(string.IsNullOrWhiteSpace) ||
                                      source.Shapes.Distinct(StringComparer.Ordinal).Count() != source.Shapes.Length))
            throw new InvalidDataException(path + "：库存老化需要大于零至十亿秒、一至1024件上限，以及最多32个互异有效形状");
        Seconds = source.Seconds; MaximumItems = source.MaximumItems;
        Shapes = source.Shapes == null ? Array.Empty<string>() : Array.AsReadOnly((string[])source.Shapes.Clone());
    }
}

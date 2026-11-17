using System;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>把生成材料交给世界维护，不延长原来的施放容器。</summary>
public sealed class ConstructionResidue
{
    /// <summary>接受后还能存在的世界秒数；零表示持续保留。</summary>
    public float Lifetime;

    /// <summary>可选的独立侵蚀，真实交接后才开始。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionErosion Erosion;

    /// <summary>独立侵蚀使用的世界秒数，没有侵蚀时为零。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float ErosionSeconds;
}

/// <summary>已经检查的独立存在规则。</summary>
internal sealed class CompiledResidue
{
    /// <summary>零为持续保留，其他值按世界时间消退。</summary>
    internal readonly float Lifetime;

    /// <summary>世界材料接受后自行推进的有限过程。</summary>
    internal readonly CompiledErosion Erosion;

    /// <summary>只在构造资料入口核对。</summary>
    /// <param name="source">外部存在规则。</param>
    /// <param name="path">错误位置。</param>
    internal CompiledResidue(ConstructionResidue source, string path)
    {
        Lifetime = source?.Lifetime ?? 0;
        if (float.IsNaN(Lifetime) || float.IsInfinity(Lifetime) || Lifetime < 0 || Lifetime > 1000000000)
            throw new InvalidDataException(path + "：世界材料存在时间须为零或有限正数");
        if (source?.Erosion != null)
            Erosion = new CompiledErosion(source.ErosionSeconds, source.Erosion, 1000000000, path + "/Erosion");
        else if (source != null && source.ErosionSeconds != 0)
            throw new InvalidDataException(path + "：侵蚀历时需要同时声明侵蚀份额");
    }
}

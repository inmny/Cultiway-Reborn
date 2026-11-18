using System;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>按明确材料耐受推进的侵蚀份额。</summary>
public sealed class ConstructionErosion
{
    /// <summary>整段投入份额，乘当前材料系数后得到实际损耗比例。</summary>
    public float Fraction = .75f;

    /// <summary>本次侵蚀自己的变速时段，旧资料默认常速。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionProcessWindow[] TimeWindows;
}

/// <summary>入口核对后的有限侵蚀过程。</summary>
internal sealed class CompiledErosion
{
    /// <summary>按常速完成需要的有效历时。</summary>
    internal readonly float Seconds;
    /// <summary>未乘材料耐受的投入份额。</summary>
    internal readonly float Fraction;
    /// <summary>本过程变速规则，空值保留原有推进。</summary>
    internal readonly CompiledProcessTime Pace;
    /// <summary>当前安排走完本次侵蚀所需的世界秒数。</summary>
    internal readonly float Completion;
    /// <summary>可选的世界材料筛选，空值仍侵蚀操作自身。</summary>
    internal readonly CompiledWorldMatter World;
    /// <summary>解析实际参数，非法声明停在资料边界。</summary>
    /// <param name="source">原始操作。</param><param name="lifetime">整次上限。</param><param name="path">资料位置。</param>
    internal CompiledErosion(ConstructionOperation source, float lifetime, string path, int order = 0)
        : this(source.Seconds, source.Erosion, source.WorldMatter == null ? lifetime : 1000000000, path)
    { if (source.WorldMatter != null) World = new CompiledWorldMatter(source.WorldMatter, path + "/WorldMatter", order); }

    /// <summary>主体与独立材料共用一次参数核对。</summary>
    /// <param name="seconds">实际历时。</param><param name="source">明确份额。</param>
    /// <param name="lifetime">入口允许的最长时间。</param><param name="path">资料位置。</param>
    internal CompiledErosion(float seconds, ConstructionErosion source, float lifetime, string path)
    {
        Seconds = seconds; Fraction = source?.Fraction ?? .75f;
        if (float.IsNaN(Seconds) || float.IsInfinity(Seconds) || Seconds <= 0 || Seconds > lifetime ||
            float.IsNaN(Fraction) || float.IsInfinity(Fraction) || Fraction <= 0 || Fraction > 1)
            throw new InvalidDataException(path + "：侵蚀需要有效历时及大于零且不超过一的份额");
        if (source?.TimeWindows is { Length: > 0 })
            Pace = new CompiledProcessTime(source.TimeWindows, lifetime, path + "/Erosion/TimeWindows");
        Completion = Pace == null ? Seconds : (float)Pace.CompletionFor(Seconds);
    }

    /// <summary>真实材料能表示本段损耗时才接受交接。</summary>
    /// <param name="matter">交接当时的材料。</param><param name="volume">实际余量。</param>
    internal bool CanAffect(AbilityMatter matter, double volume) => volume > 0 && matter.ErosionFactor > 0 &&
        volume * (float)(1 - Fraction * (double)matter.ErosionFactor) < volume;

    /// <summary>按当前余量推进，其他过程造成的损失不会被重建。</summary>
    /// <param name="loss">当前材料的整段损耗比例。</param><param name="before">上次实际扣到的进度。</param>
    /// <param name="after">本次目标进度。</param>
    internal static double Retained(double loss, double before, double after) => (1 - loss * after) / (1 - loss * before);
}

/// <summary>由实际累计推进得到的材料阶段。</summary>
public enum MaterialErosionStage
{
    /// <summary>没有接受侵蚀。</summary>
    Intact,
    /// <summary>已发生表面及实体损耗。</summary>
    Weathered,
    /// <summary>累计侵蚀达到脆弱阶段。</summary>
    Weakened,
    /// <summary>累计侵蚀已耗尽实体。</summary>
    Spent
}

/// <summary>已发生的侵蚀事实，体积和支付去向分别维护。</summary>
public struct AbilityErosion
{
    /// <summary>累计侵蚀占比，按实际保留比例叠加，最大为一。</summary>
    public float Exposure;
    /// <summary>已经耗蚀退出实体的真实体积。</summary>
    public double LostVolume;
    /// <summary>随实际损耗退出的原支付份额，不记为回能。</summary>
    public double SpentResource;
    /// <summary>从真实累计值读取当前阶段。</summary>
    public MaterialErosionStage Stage => Exposure >= .99999f ? MaterialErosionStage.Spent :
        Exposure >= .5f ? MaterialErosionStage.Weakened : Exposure > 0 ? MaterialErosionStage.Weathered : MaterialErosionStage.Intact;

    /// <summary>主体与世界材料共同登记已经发生的真实损失。</summary>
    /// <param name="before">本次损耗前的体积。</param><param name="volume">实际移除体积。</param>
    /// <param name="resource">随材料退出的本金。</param>
    internal void Add(double before, double volume, double resource)
    {
        LostVolume += volume;
        SpentResource += resource;
        Exposure = (float)Math.Min(1, Exposure + (1 - Exposure) * volume / before);
    }

    /// <summary>阶段随实际余量继承，累计损失按交出份额分配。</summary>
    /// <param name="fraction">真正交出的体积比例。</param>
    internal AbilityErosion Share(double fraction) => new()
    { Exposure = Exposure, LostVolume = LostVolume * fraction, SpentResource = SpentResource * fraction };
}

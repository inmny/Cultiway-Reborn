using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>真实接触后的向外击退距离和腾起高度，在命中时结合目标当前质量换算世界速度。</summary>
public sealed class ConstructionKnockback
{
    /// <summary>无抗性及阻挡时的平地击退距离；零表示只腾起。</summary>
    public float Distance;

    /// <summary>无抗性及阻挡时的最高腾起高度；零表示在0.25世界秒内沿地击退，避免腾起公式除零。</summary>
    public float Height;

    /// <summary>只有本次实际接触的生成部位正在向上移动才击退，缺省仍按原规则。</summary>
    [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public bool OnlyWhileRising;
}

/// <summary>检查并复制后的接触击退资料，不保存人物或旧执行回调。</summary>
public sealed class CompiledConstructionKnockback
{
    /// <summary>已经固定的击退幅度。</summary>
    public float Distance { get; }

    /// <summary>已经固定的腾起幅度。</summary>
    public float Height { get; }

    /// <summary>击退需要生成部位的实际向上速度。</summary>
    public bool OnlyWhileRising { get; }

    /// <summary>本次声明是否有实际位移作用。</summary>
    public bool HasEffect => Distance > 0f || Height > 0f;

    /// <summary>在构造资料的共同编译边界检查有限参数并独立复制。</summary>
    /// <param name="source">可编辑的接触资料。</param><param name="path">错误对应的位置。</param>
    internal CompiledConstructionKnockback(ConstructionKnockback source, string path)
    {
        Distance = source.Distance;
        Height = source.Height;
        OnlyWhileRising = source.OnlyWhileRising;
        if (float.IsNaN(Distance) || float.IsInfinity(Distance) || Distance < 0f || Distance > 1000f ||
            float.IsNaN(Height) || float.IsInfinity(Height) || Height < 0f || Height > 100f)
            throw new InvalidDataException(path + "：击退幅度须为0至1000，腾起幅度须为0至100的有限值");
        // 共同运动的质量重力上限为20，最高加速度为120；先保证所有目标的换算速度有限且在共同上限内。
        double horizontal = Height > 0f ? Distance * Math.Sqrt(15d / Height) : Distance / .25d;
        if (horizontal > 1000d)
            throw new InvalidDataException(path + "：距离和高度组合超过每世界秒1000格的共同速度上限，请增加高度或降低距离");
    }
}

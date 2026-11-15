using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>真实接触后选择最近且同组尚未命中的人物，不预先选择整条后续链。</summary>
public sealed class ConstructionContactTarget
{
    /// <summary>以刚命中人物脚下为中心的地图平面搜索距离。</summary>
    public float Radius = 4f;
    /// <summary>下一部位相对来源接触时实际余力的倍率，不重新取得初始威力。</summary>
    public float ForceMultiplier = .8f;
}

/// <summary>创建边界复制的有限搜索和力量衰减。</summary>
internal sealed class CompiledContactTarget
{
    /// <summary>地图距离与实际余力倍率。</summary>
    internal readonly float Radius, ForceMultiplier;

    /// <summary>只在外部资料入口检查数值。</summary>
    /// <param name="source">可保存资料。</param><param name="path">错误位置。</param>
    internal CompiledContactTarget(ConstructionContactTarget source, string path)
    {
        Radius = source.Radius; ForceMultiplier = source.ForceMultiplier;
        if (float.IsNaN(Radius + ForceMultiplier) || float.IsInfinity(Radius + ForceMultiplier) || Radius <= 0f || Radius > 10000f || ForceMultiplier <= 0f || ForceMultiplier > 1f)
            throw new InvalidDataException(path + "：附近距离须为正数且不超过一万，余力倍率须大于零且不超过一");
    }
}

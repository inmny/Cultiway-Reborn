namespace Cultiway.Core.Visuals;

/// <summary>主体表面的质感；细线和范围标记继续使用原来的简单材质。</summary>
internal enum SpatialSurface
{
    /// <summary>普通实体、细线或边界。</summary>
    None,
    /// <summary>有暗焰、亮芯和翻卷的火。</summary>
    Flame,
    /// <summary>有水体、波纹和泡沫的水。</summary>
    Water,
    /// <summary>有岩层、裂隙和受光面的石。</summary>
    Stone,
    /// <summary>有冷色体积和霜纹的冰。</summary>
    Ice,
    /// <summary>有叶脉和明暗面的枝叶。</summary>
    Foliage,
    /// <summary>有疏密和软边的雾气。</summary>
    Mist,
    /// <summary>有内部流动的灵力或法相。</summary>
    Energy,
    /// <summary>有纵向木纹与深色沟槽的木材。</summary>
    Wood,
    /// <summary>有剑脊受光和细密磨痕的金属。</summary>
    Metal
}

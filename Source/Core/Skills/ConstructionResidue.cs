using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>把生成材料交给世界维护，不延长原来的施放容器。</summary>
public sealed class ConstructionResidue
{
    /// <summary>接受后还能存在的世界秒数；零表示持续保留。</summary>
    public float Lifetime;
}

/// <summary>已经检查的独立存在规则。</summary>
internal sealed class CompiledResidue
{
    /// <summary>零为持续保留，其他值按世界时间消退。</summary>
    internal readonly float Lifetime;

    /// <summary>只在构造资料入口核对。</summary>
    /// <param name="source">外部存在规则。</param>
    /// <param name="path">错误位置。</param>
    internal CompiledResidue(ConstructionResidue source, string path)
    {
        Lifetime = source?.Lifetime ?? 0;
        if (float.IsNaN(Lifetime) || float.IsInfinity(Lifetime) || Lifetime < 0 || Lifetime > 1000000000)
            throw new InvalidDataException(path + "：世界材料存在时间须为零或有限正数");
    }
}
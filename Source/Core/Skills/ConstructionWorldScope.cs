using System;

namespace Cultiway.Core.Skills;

/// <summary>本次世界行动的实际任务边界，只在主线程检查，不改写构造或保存回调。</summary>
public sealed class ConstructionWorldScope
{
    /// <summary>需求和来源是否仍有效；失效时取消整次施放。</summary>
    internal readonly Func<bool> IsValid;

    /// <summary>某项原理此刻能否作用于这块地，不额外产生作用范围。</summary>
    internal readonly Func<string, WorldTile, bool> Allows;

    /// <summary>由发起任务提供动态边界，正常手动施法可以不提供。</summary>
    /// <param name="isValid">任务和来源的当前事实。</param>
    /// <param name="allows">当前操作、地块及保护条件。</param>
    public ConstructionWorldScope(Func<bool> isValid, Func<string, WorldTile, bool> allows)
    {
        IsValid = isValid ?? throw new ArgumentNullException(nameof(isValid));
        Allows = allows ?? throw new ArgumentNullException(nameof(allows));
    }
}
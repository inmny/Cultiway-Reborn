using System;

namespace Cultiway.Core.Skills;

/// <summary>本次世界行动的实际任务边界，只在主线程检查，不改写构造或保存回调。</summary>
public sealed class ConstructionWorldScope
{
    /// <summary>需求和来源是否仍有效；失效时取消整次施放。</summary>
    internal readonly Func<bool> IsValid;

    /// <summary>某项原理此刻能否作用于这块地，不额外产生作用范围。</summary>
    internal readonly Func<string, WorldTile, bool> Allows;

    /// <summary>本人为完成出行任务跨门时，核对实际原人物与落点；保留原任务继续步行。</summary>
    internal Func<Actor, UnityEngine.Vector3, bool> AllowsTravelArrival { get; }

    /// <summary>自然回收选定的真实材料，零表示普通选择。</summary>
    internal long RecoveryMaterial { get; }

    /// <summary>选定的材料已经交给独立的世界收束，原施放可以正常收尾。</summary>
    internal bool RecoveryStarted { get; set; }

    /// <summary>由发起任务提供动态边界，正常手动施法可以不提供。</summary>
    /// <param name="isValid">任务和来源的当前事实。</param>
    /// <param name="allows">当前操作、地块及保护条件。</param>
    public ConstructionWorldScope(Func<bool> isValid, Func<string, WorldTile, bool> allows)
        : this(isValid, allows, null) { }

    /// <summary>发起出行的任务可以进一步约束同一次搬运的实际落点。</summary>
    internal ConstructionWorldScope(Func<bool> isValid, Func<string, WorldTile, bool> allows,
        Func<Actor, UnityEngine.Vector3, bool> allowsTravelArrival)
    {
        IsValid = isValid ?? throw new ArgumentNullException(nameof(isValid));
        Allows = allows ?? throw new ArgumentNullException(nameof(allows));
        AllowsTravelArrival = allowsTravelArrival;
    }

    /// <summary>自主回收从实际需求开始，交出材料后继续核对施术者。</summary>
    internal ConstructionWorldScope(Func<bool> sourceValid, long material, Func<bool> demandValid)
        : this(sourceValid, (_, _) => false)
    {
        RecoveryMaterial = material;
        IsValid = () => sourceValid() && (RecoveryStarted || demandValid());
    }
}

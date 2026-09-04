using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Core;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>人物当前可供法器与元神共同使用的神识预算。</summary>
public readonly struct DivineSenseBudget
{
    /// <summary>人物当前总神识。</summary>
    public readonly float TotalLoadCapacity;

    /// <summary>非自主法器与独立行动对象合计可用的分念总数。</summary>
    public readonly int TotalThreadCapacity;

    /// <summary>元神系统已经优先占用的连续神识负荷。</summary>
    public readonly float ReservedLoad;

    /// <summary>元神系统已经优先占用的分念数。</summary>
    public readonly int ReservedThreads;

    /// <summary>法器自动准备时可使用的神识上限。</summary>
    public readonly float AutomaticPreparedLimit;

    /// <summary>法器准备状态能够使用的神识硬上限。</summary>
    public readonly float PreparedHardLimit;

    /// <summary>法器自动运转时可使用的神识上限。</summary>
    public readonly float AutomaticOperatingLimit;

    /// <summary>法器强制运转时可使用的神识上限。</summary>
    public readonly float ForcedOperatingLimit;

    /// <summary>法器可以使用的剩余分念数。</summary>
    public readonly int AvailableArtifactThreads;

    /// <summary>按总量和外部预留生成一份不可变预算。</summary>
    /// <param name="totalLoadCapacity">人物当前总神识。</param>
    /// <param name="totalThreadCapacity">人物当前总分念容量。</param>
    /// <param name="reservedLoad">元神系统占用的连续负荷。</param>
    /// <param name="reservedThreads">元神系统占用的分念数。</param>
    public DivineSenseBudget(
        float totalLoadCapacity,
        int totalThreadCapacity,
        float reservedLoad,
        int reservedThreads)
    {
        TotalLoadCapacity = Mathf.Max(0f, totalLoadCapacity);
        TotalThreadCapacity = Mathf.Max(0, totalThreadCapacity);
        ReservedLoad = Mathf.Clamp(reservedLoad, 0f, TotalLoadCapacity * 1.3f);
        ReservedThreads = Mathf.Clamp(reservedThreads, 0, TotalThreadCapacity);
        AutomaticPreparedLimit = Mathf.Max(0f, TotalLoadCapacity * 0.8f - ReservedLoad);
        PreparedHardLimit = Mathf.Max(0f, TotalLoadCapacity - ReservedLoad);
        AutomaticOperatingLimit = Mathf.Max(0f, TotalLoadCapacity * 0.8f - ReservedLoad);
        ForcedOperatingLimit = Mathf.Max(0f, TotalLoadCapacity * 1.3f - ReservedLoad);
        AvailableArtifactThreads = Mathf.Max(0, TotalThreadCapacity - ReservedThreads);
    }
}

/// <summary>法器和元神读取同一神识总量与分念占用的唯一入口。</summary>
public static class DivineSenseBudgetService
{
    /// <summary>独立活动肉身需要占用的分念数量。</summary>
    private const int IndependentBodyThreadCost = 1;

    /// <summary>计算人物当前完整神识预算。</summary>
    /// <param name="actor">需要计算预算的人物。</param>
    /// <returns>包含元神预留与法器剩余额度的预算。</returns>
    public static DivineSenseBudget Resolve(ActorExtend actor)
    {
        if (actor == null || actor.Base == null || actor.Base.isRekt()) return default;
        float totalLoad = Mathf.Max(0f, actor.Base.stats[nameof(WorldboxGame.BaseStats.DivineSense)]);
        int totalThreads = ArtifactControlRules.GetThreadCapacity(totalLoad);
        ResolveActiveNodeUsage(actor, out int nodeThreads, out float nodeLoadRatio);
        int reservedThreads = (IsIndependentBodyActive(actor) ? IndependentBodyThreadCost : 0) + nodeThreads;
        float reservedLoad = 0f;
        if (actor.HasComponent<YuanshenArtifactAnchorState>())
            reservedLoad += totalLoad * 0.2f;
        reservedLoad += totalLoad * YuanshenAnchorNetworkService.ResolveOwnedNetworkLoadRatio(actor);
        reservedLoad += totalLoad * nodeLoadRatio;
        return new DivineSenseBudget(totalLoad, totalThreads, reservedLoad, reservedThreads);
    }

    /// <summary>判断命魂离体后的肉身是否仍以独立心念行动。</summary>
    /// <param name="actor">需要检查的人物。</param>
    /// <returns>命魂在外时始终返回真。</returns>
    public static bool IsIndependentBodyActive(ActorExtend actor)
    {
        return actor != null &&
               actor.TryGetComponent(out YuanshenRuntimeState runtime) &&
               runtime.IsOutside;
    }

    /// <summary>一次遍历统计活动节点占用的分念数和连续神识负荷比例。</summary>
    /// <param name="actor">节点所属人物。</param>
    /// <param name="threads">返回活动节点占用的分念数。</param>
    /// <param name="loadRatio">返回相对人物总神识的负荷比例。</param>
    private static void ResolveActiveNodeUsage(ActorExtend actor, out int threads, out float loadRatio)
    {
        threads = 0;
        loadRatio = actor.HasComponent<YuanshenBodilessTransitState>() ? 0.05f : 0f;
        bool preparingAvatar = actor.HasComponent<YuanshenAvatarPreparationState>();
        if (preparingAvatar) loadRatio += 0.1f;
        if (!actor.TryGetComponent(out YuanshenRuntimeState runtime)) return;
        if (preparingAvatar) threads++;

        if (runtime.thought_nodes != null)
        {
            for (var i = 0; i < runtime.thought_nodes.Count; i++)
            {
                if (!YuanshenNodeLockService.TryResolve(runtime.thought_nodes[i], out var node)) continue;
                threads++;
                loadRatio += node.TryGetComponent(out YuanshenNodeTask task) &&
                             task.kind is YuanshenNodeTaskKind.TrackLockedNode or
                                 YuanshenNodeTaskKind.ControlArtifact or 
                                 YuanshenNodeTaskKind.EngageActor or YuanshenNodeTaskKind.AnchorTransit
                    ? 0.15f
                    : 0.1f;
            }
        }

        if (runtime.advanced_nodes == null) return;
        for (var i = 0; i < runtime.advanced_nodes.Count; i++)
        {
            if (!YuanshenNodeLockService.TryResolve(runtime.advanced_nodes[i], out var node)) continue;
            threads++;
            if (!node.TryGetComponent(out YuanshenNodeIdentity identity)) continue;
            float nodeRatio = identity.role switch
            {
                YuanshenNodeRole.DharmaForm => 0.35f,
                YuanshenNodeRole.Avatar => 0.25f,
                YuanshenNodeRole.Manifestation => 0.2f,
                _ => 0.1f
            };
            if (node.TryGetComponent(out YuanshenNodeTask task) &&
                task.kind is YuanshenNodeTaskKind.EngageActor or YuanshenNodeTaskKind.AnchorTransit)
                nodeRatio += 0.05f;
            loadRatio += nodeRatio;
        }
    }

    /// <summary>读取法器与元神合计占用的分念数量。</summary>
    /// <param name="actor">需要检查的人物。</param>
    /// <param name="budget">同一刷新过程已经计算出的神识预算。</param>
    /// <returns>当前实际占用的分念数。</returns>
    public static int ResolveUsedThreads(ActorExtend actor, in DivineSenseBudget budget)
    {
        int artifactThreads = actor != null && actor.TryGetComponent(out ArtifactLoadoutState state)
            ? Mathf.Max(0, state.used_threads)
            : 0;
        return budget.ReservedThreads + artifactThreads;
    }
}

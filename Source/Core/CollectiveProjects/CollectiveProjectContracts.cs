using System;
using System.Collections.Generic;

namespace Cultiway.Core.CollectiveProjects;

/// <summary>集体工程的处理紧急度。</summary>
public enum CollectiveProjectUrgency
{
    /// <summary>只在角色自然选择新工作时执行。</summary>
    Routine,

    /// <summary>优先于普通工程，但不会强制中断当前任务。</summary>
    Urgent,

    /// <summary>允许由高优先级原版决策抢占普通工作。</summary>
    Emergency
}

/// <summary>集体工程从规划到结束的统一生命周期状态。</summary>
public enum CollectiveProjectState
{
    /// <summary>已发布，尚未被成员认领。</summary>
    Planned,

    /// <summary>已有成员认领执行槽位。</summary>
    Claimed,

    /// <summary>执行器已经提交实际行动，等待行动完成信号。</summary>
    Executing,

    /// <summary>行动已经完成，等待目标世界状态稳定后验收。</summary>
    Verifying,

    /// <summary>工程目标已经通过验收。</summary>
    Completed,

    /// <summary>工程执行失败且不再自动重试。</summary>
    Failed,

    /// <summary>规划器撤销了已经不再需要的工程。</summary>
    Cancelled,

    /// <summary>发起者、目标或工程定义已经失效。</summary>
    Expired
}

/// <summary>发起者适配器与其世界内对象 ID 共同组成的稳定工程所有者标识。</summary>
public readonly struct CollectiveProjectOwnerKey : IEquatable<CollectiveProjectOwnerKey>
{
    /// <summary>创建一个不依赖具体 MetaType 的所有者标识。</summary>
    public CollectiveProjectOwnerKey(string providerId, long ownerId)
    {
        ProviderId = providerId ?? string.Empty;
        OwnerId = ownerId;
    }

    /// <summary>负责解析该所有者的适配器 ID。</summary>
    public string ProviderId { get; }

    /// <summary>所有者在对应适配器中的世界内 ID。</summary>
    public long OwnerId { get; }

    /// <summary>判断两个所有者标识是否完全一致。</summary>
    public bool Equals(CollectiveProjectOwnerKey other)
    {
        return OwnerId == other.OwnerId &&
               string.Equals(ProviderId, other.ProviderId, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is CollectiveProjectOwnerKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((ProviderId != null ? ProviderId.GetHashCode() : 0) * 397) ^ OwnerId.GetHashCode();
        }
    }

    public override string ToString()
    {
        return $"{ProviderId}:{OwnerId}";
    }

    public static bool operator ==(CollectiveProjectOwnerKey left, CollectiveProjectOwnerKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CollectiveProjectOwnerKey left, CollectiveProjectOwnerKey right)
    {
        return !left.Equals(right);
    }
}

/// <summary>向发起者适配器请求一种具有组织语义的空间范围。</summary>
public readonly struct CollectiveProjectSpatialRequest
{
    public const string Primary = "primary";
    public const string PrimaryAdjacent = "primary_adjacent";
    public const string MemberVicinity = "member_vicinity";
    public const string AssociatedSettlements = "associated_settlements";

    /// <summary>创建一个可由不同发起者自行解释的空间请求。</summary>
    public CollectiveProjectSpatialRequest(string scopeId)
    {
        ScopeId = scopeId ?? string.Empty;
    }

    /// <summary>空间语义 ID；适配器可以拒绝自身不支持的语义。</summary>
    public string ScopeId { get; }
}

/// <summary>规划器访问一个已解析工程发起者时使用的只读上下文。</summary>
public readonly struct CollectiveProjectOwnerContext
{
    /// <summary>创建发起者上下文。</summary>
    public CollectiveProjectOwnerContext(
        CollectiveProjectOwnerKey key,
        NanoObject owner,
        ICollectiveProjectOwnerAdapter adapter)
    {
        Key = key;
        Owner = owner;
        Adapter = adapter;
    }

    public CollectiveProjectOwnerKey Key { get; }
    public NanoObject Owner { get; }
    public ICollectiveProjectOwnerAdapter Adapter { get; }
}

/// <summary>
/// 把城市、宗门、世家等具体 MetaObject 转换为工程框架所需的成员与空间能力。
/// </summary>
public interface ICollectiveProjectOwnerAdapter
{
    string Id { get; }

    IEnumerable<NanoObject> EnumerateOwners();

    bool TryResolve(long ownerId, out NanoObject owner);

    IEnumerable<Actor> EnumerateMembers(NanoObject owner);

    bool IsMember(NanoObject owner, Actor actor);

    float ResolveMemberAffinity(NanoObject owner, Actor actor);

    bool CollectTiles(
        NanoObject owner,
        in CollectiveProjectSpatialRequest request,
        ICollection<WorldTile> output);
}

/// <summary>配置一个工程定义共享的完成额度。</summary>
public sealed class CollectiveProjectRatePolicy
{
    /// <summary>同一发起者共享额度的分组；空值表示不限制。</summary>
    public string BudgetGroup;

    /// <summary>统计窗口内允许成功完成的最大次数。</summary>
    public int MaxCompletions;

    /// <summary>完成额度的世界时间窗口，单位为秒。</summary>
    public double WindowSeconds;
}

/// <summary>定义一类工程的执行器、默认优先级、限流与生命周期校验。</summary>
public sealed class CollectiveProjectDefinitionAsset : Asset
{
    /// <summary>负责把工程转换为角色行为的执行器 ID。</summary>
    public string ExecutorId;

    /// <summary>同一工程允许同时认领并准备的候选槽位；首个成功提交者取得本轮执行权。</summary>
    public int WorkerSlots = 1;

    /// <summary>提案未指定时采用的基础优先级。</summary>
    public float DefaultPriority;

    /// <summary>工程成功后采用的完成额度策略。</summary>
    public CollectiveProjectRatePolicy RatePolicy;

    /// <summary>重新调度前检查工程是否仍有必要且目标仍然有效。</summary>
    public Func<CollectiveProjectView, bool> Validate;

    /// <summary>执行完成后根据真实世界状态验收工程结果。</summary>
    public Func<CollectiveProjectView, bool> Verify;
}

/// <summary>规划器提交给生命周期服务的不可变工程候选。</summary>
public sealed class CollectiveProjectProposal
{
    public string DefinitionId;
    public string DeduplicationKey;
    public CollectiveProjectOwnerKey Owner;
    public int TargetTileId = -1;
    public object Payload;
    public CollectiveProjectUrgency Urgency;
    public float Priority;
    public string HistoryTag;
    public string[] ConflictingHistoryTags = Array.Empty<string>();
    public double ConflictWindowSeconds;
    public float ConflictRadius;
}

/// <summary>向规划器声明其发起者类型、调度周期和单帧处理预算。</summary>
public interface ICollectiveProjectPlanner
{
    string Id { get; }
    string OwnerProviderId { get; }
    double IntervalSeconds { get; }
    int OwnersPerUpdate { get; }

    void CollectProposals(
        in CollectiveProjectOwnerContext owner,
        ICollection<CollectiveProjectProposal> output);
}

/// <summary>项目服务向规划、执行和验收代码公开的稳定项目快照。</summary>
public readonly struct CollectiveProjectView
{
    /// <summary>由运行时项目记录创建一个只读快照。</summary>
    internal CollectiveProjectView(
        long projectId,
        string definitionId,
        string plannerId,
        CollectiveProjectOwnerKey owner,
        int targetTileId,
        object payload,
        CollectiveProjectUrgency urgency,
        float priority,
        CollectiveProjectState state,
        long claimedActorId,
        double createdAt,
        string historyTag)
    {
        ProjectId = projectId;
        DefinitionId = definitionId;
        PlannerId = plannerId;
        Owner = owner;
        TargetTileId = targetTileId;
        Payload = payload;
        Urgency = urgency;
        Priority = priority;
        State = state;
        ClaimedActorId = claimedActorId;
        CreatedAt = createdAt;
        HistoryTag = historyTag;
    }

    public long ProjectId { get; }
    public string DefinitionId { get; }
    public string PlannerId { get; }
    public CollectiveProjectOwnerKey Owner { get; }
    public int TargetTileId { get; }
    public object Payload { get; }
    public CollectiveProjectUrgency Urgency { get; }
    public float Priority { get; }
    public CollectiveProjectState State { get; }
    public long ClaimedActorId { get; }
    public double CreatedAt { get; }
    public string HistoryTag { get; }
}

/// <summary>
/// 把项目槽位适配为具体角色工作。执行器可以来自魔法、建造、仪式或其他体系。
/// </summary>
public interface ICollectiveProjectExecutor
{
    string Id { get; }

    string ResolveRoutineJobId(in CollectiveProjectView project);

    bool CanExecute(ActorExtend actor, in CollectiveProjectView project);

    float ScoreExecutor(ActorExtend actor, in CollectiveProjectView project);

    bool TryPrepare(ActorExtend actor, in CollectiveProjectView project);

    bool TryExecute(ActorExtend actor, in CollectiveProjectView project);

    bool IsAssignmentActive(ActorExtend actor, in CollectiveProjectView project);

    /// <summary>角色任务被切换或项目撤销时清理由认领槽位持有的准备状态。</summary>
    void OnAssignmentReleased(
        long actorId,
        ActorExtend actor,
        in CollectiveProjectView project);

    /// <summary>项目离开本次执行/验收周期时清理由执行令牌持有的外部状态。</summary>
    void OnExecutionReleased(
        long actorId,
        long executionToken,
        in CollectiveProjectView project);

    void ClearWorldState();
}

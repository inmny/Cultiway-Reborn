namespace Cultiway.Core.Coordination;

/// <summary>一次协调行动的最终结果。</summary>
public readonly struct CoordinatedActivityResult
{
    /// <summary>创建最终结果。</summary>
    internal CoordinatedActivityResult(
        long activityId,
        string definitionId,
        CoordinationGroupKey group,
        CoordinatedActivityEndReason reason,
        double endedAt)
    {
        ActivityId = activityId;
        DefinitionId = definitionId;
        Group = group;
        Reason = reason;
        EndedAt = endedAt;
    }

    /// <summary>行动运行期 ID。</summary>
    public long ActivityId { get; }

    /// <summary>行动定义 ID。</summary>
    public string DefinitionId { get; }

    /// <summary>行动所属群组。</summary>
    public CoordinationGroupKey Group { get; }

    /// <summary>行动结束原因。</summary>
    public CoordinatedActivityEndReason Reason { get; }

    /// <summary>行动结束时的世界时间。</summary>
    public double EndedAt { get; }
}

/// <summary>提供给调试工具的不可变行动快照。</summary>
public readonly struct CoordinatedActivityDebugSnapshot
{
    /// <summary>创建诊断快照。</summary>
    internal CoordinatedActivityDebugSnapshot(
        long id,
        string definitionId,
        CoordinationGroupKey group,
        CoordinatedActivityStage stage,
        int participantCount,
        int readyCount,
        int invitationCount,
        int blockedParticipantCount,
        int maximumPathFailures,
        double stageAge)
    {
        Id = id;
        DefinitionId = definitionId;
        Group = group;
        Stage = stage;
        ParticipantCount = participantCount;
        ReadyCount = readyCount;
        InvitationCount = invitationCount;
        BlockedParticipantCount = blockedParticipantCount;
        MaximumPathFailures = maximumPathFailures;
        StageAge = stageAge;
    }

    /// <summary>行动运行期 ID。</summary>
    public long Id { get; }

    /// <summary>行动定义 ID。</summary>
    public string DefinitionId { get; }

    /// <summary>行动群组。</summary>
    public CoordinationGroupKey Group { get; }

    /// <summary>行动阶段。</summary>
    public CoordinatedActivityStage Stage { get; }

    /// <summary>已分配参与者数量。</summary>
    public int ParticipantCount { get; }

    /// <summary>已到场参与者数量。</summary>
    public int ReadyCount { get; }

    /// <summary>尚未接受的受邀角色数量。</summary>
    public int InvitationCount { get; }

    /// <summary>已经达到路径失败上限的参与者数量。</summary>
    public int BlockedParticipantCount { get; }

    /// <summary>单个参与者当前最大的连续路径失败次数。</summary>
    public int MaximumPathFailures { get; }

    /// <summary>当前阶段已持续的模拟秒数。</summary>
    public double StageAge { get; }
}

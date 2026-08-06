using System;
using System.Collections.Generic;

namespace Cultiway.Core.Coordination;

/// <summary>领域会话提交给招募器的候选成员。</summary>
public readonly struct CoordinationCandidate
{
    /// <summary>创建一个带稳定评分的席位候选。</summary>
    public CoordinationCandidate(Actor actor, float score)
    {
        Actor = actor;
        Score = score;
    }

    /// <summary>候选角色。</summary>
    public Actor Actor { get; }

    /// <summary>同席位内的降序选择分数。</summary>
    public float Score { get; }
}

/// <summary>启动行动时已经确定的成员及其席位。</summary>
public readonly struct CoordinationInitialParticipant
{
    /// <summary>创建一条初始成员记录。</summary>
    public CoordinationInitialParticipant(Actor actor, string roleId)
    {
        Actor = actor;
        RoleId = roleId ?? string.Empty;
    }

    /// <summary>初始成员。</summary>
    public Actor Actor { get; }

    /// <summary>初始成员占用的席位。</summary>
    public string RoleId { get; }

}

/// <summary>参与者的只读运行时视图。</summary>
public readonly struct CoordinationParticipantView
{
    /// <summary>由服务构造参与者视图。</summary>
    internal CoordinationParticipantView(
        long actorId,
        string roleId,
        bool ready,
        CoordinationParticipantLifetime participantLifetime,
        int orderRevision,
        int pathFailures)
    {
        ActorId = actorId;
        RoleId = roleId;
        Ready = ready;
        ParticipantLifetime = participantLifetime;
        OrderRevision = orderRevision;
        PathFailures = pathFailures;
    }

    /// <summary>角色世界内稳定 ID。</summary>
    public long ActorId { get; }

    /// <summary>当前占用的席位。</summary>
    public string RoleId { get; }

    /// <summary>是否满足当前的位置与领域到场条件。</summary>
    public bool Ready { get; }

    /// <summary>参与关系与角色执行任务之间的生命周期约束。</summary>
    public CoordinationParticipantLifetime ParticipantLifetime { get; }

    /// <summary>位置订单版本。</summary>
    public int OrderRevision { get; }

    /// <summary>执行当前位置订单以来的路径失败次数。</summary>
    public int PathFailures { get; }
}

/// <summary>协调行动的只读运行时视图。</summary>
public readonly struct CoordinatedActivityView
{
    /// <summary>由服务构造行动视图。</summary>
    internal CoordinatedActivityView(
        long id,
        CoordinatedActivityDefinitionAsset definition,
        CoordinationGroupKey group,
        CoordinatedActivityStage stage,
        double createdAt,
        double stageStartedAt,
        CoordinationParticipantView[] participants)
    {
        Id = id;
        Definition = definition;
        Group = group;
        Stage = stage;
        CreatedAt = createdAt;
        StageStartedAt = stageStartedAt;
        Participants = participants ?? Array.Empty<CoordinationParticipantView>();
    }

    /// <summary>行动运行期 ID。</summary>
    public long Id { get; }

    /// <summary>行动静态定义。</summary>
    public CoordinatedActivityDefinitionAsset Definition { get; }

    /// <summary>行动所属长期群组。</summary>
    public CoordinationGroupKey Group { get; }

    /// <summary>当前生命周期阶段。</summary>
    public CoordinatedActivityStage Stage { get; }

    /// <summary>行动创建时的世界时间。</summary>
    public double CreatedAt { get; }

    /// <summary>当前阶段开始时的世界时间。</summary>
    public double StageStartedAt { get; }

    /// <summary>当前全部参与者的快照。</summary>
    public IReadOnlyList<CoordinationParticipantView> Participants { get; }
}

namespace Cultiway.Core.Coordination;

/// <summary>一个协调行动当前所处的生命周期阶段。</summary>
public enum CoordinatedActivityStage
{
    /// <summary>正在填充席位并发送自愿邀请。</summary>
    Recruiting,

    /// <summary>席位已满足，参与者正在前往各自位置。</summary>
    Assembling,

    /// <summary>到场条件已满足，领域行动正在执行。</summary>
    Running,

    /// <summary>执行结束，正在统一释放席位与订单。</summary>
    Releasing,

    /// <summary>行动正常完成。</summary>
    Completed,

    /// <summary>行动因条件失效或执行失败而结束。</summary>
    Failed,

    /// <summary>行动被领域系统或更高优先级行动取消。</summary>
    Cancelled
}

/// <summary>角色席位对成员的约束方式。</summary>
public enum CoordinationParticipationMode
{
    /// <summary>仅发出邀请，角色自然选择工作时才会参加。</summary>
    Voluntary,

    /// <summary>空闲成员必须参加，但不会抢占更高优先级工作。</summary>
    Duty,

    /// <summary>允许抢占可中断的低优先级协调行动。</summary>
    Forced
}

/// <summary>参与关系与角色 AI 执行任务之间的生命周期约束。</summary>
public enum CoordinationParticipantLifetime
{
    /// <summary>角色离开该席位声明的执行任务后立即释放参与关系。</summary>
    ExecutionBound,

    /// <summary>参与关系持续到活动结束；离开执行任务时只撤销到场状态和位置订单。</summary>
    ActivityBound
}

/// <summary>执行阶段失去集合条件时采用的生命周期策略。</summary>
public enum CoordinationRunningReadinessPolicy
{
    /// <summary>执行阶段不持续要求集合条件，具体行为由领域会话决定。</summary>
    Ignore,

    /// <summary>退回集合阶段，重新满足全部到场条件后再继续执行。</summary>
    Reassemble,

    /// <summary>立即以必要到场条件丢失结束活动。</summary>
    Fail
}

/// <summary>协调行动的结束原因。</summary>
public enum CoordinatedActivityEndReason
{
    /// <summary>领域会话主动报告完成。</summary>
    Completed,

    /// <summary>群组或行动定义已经失效。</summary>
    SourceInvalid,

    /// <summary>招募截止时仍缺少必要席位。</summary>
    RecruitmentTimedOut,

    /// <summary>集合截止时实际到场人数不足。</summary>
    AssemblyTimedOut,

    /// <summary>会话执行失败。</summary>
    SessionFailed,

    /// <summary>行动超过允许的运行时长。</summary>
    RunningTimedOut,

    /// <summary>执行阶段失去了无法补充的必要席位。</summary>
    RequiredParticipantLost,

    /// <summary>执行阶段失去了活动要求持续保持的到场条件。</summary>
    RequiredReadinessLost,

    /// <summary>被领域系统显式取消。</summary>
    Cancelled,

    /// <summary>被更高优先级协调行动抢占。</summary>
    Preempted
}

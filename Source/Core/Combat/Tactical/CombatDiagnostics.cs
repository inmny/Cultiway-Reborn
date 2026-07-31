using System.Collections.Generic;
using System.Threading;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 战斗系统的只读计数快照，供调试工具判断规划、提交与执行是否发生异常堆积。
/// </summary>
public readonly struct CombatDiagnosticsSnapshot
{
    public readonly long HighFidelityPlans;
    public readonly long LowFidelityPlans;
    public readonly long Commits;
    public readonly long DiscardedCommits;
    public readonly long StartedActions;
    public readonly long BlockedActions;
    public readonly long InvalidActions;
    public readonly long Replans;
    public readonly long PathFailures;
    public readonly long ArmyRouts;
    public readonly long ThreatSignals;
    public readonly long AssistPlans;
    public readonly long MovementOrdersIssued;
    public readonly long MovementOrdersRetained;
    public readonly long ForcedMovementOrders;
    public readonly long MovementStops;

    internal CombatDiagnosticsSnapshot(
        long highFidelityPlans,
        long lowFidelityPlans,
        long commits,
        long discardedCommits,
        long startedActions,
        long blockedActions,
        long invalidActions,
        long replans,
        long pathFailures,
        long armyRouts,
        long threatSignals,
        long assistPlans,
        long movementOrdersIssued,
        long movementOrdersRetained,
        long forcedMovementOrders,
        long movementStops)
    {
        HighFidelityPlans = highFidelityPlans;
        LowFidelityPlans = lowFidelityPlans;
        Commits = commits;
        DiscardedCommits = discardedCommits;
        StartedActions = startedActions;
        BlockedActions = blockedActions;
        InvalidActions = invalidActions;
        Replans = replans;
        PathFailures = pathFailures;
        ArmyRouts = armyRouts;
        ThreatSignals = threatSignals;
        AssistPlans = assistPlans;
        MovementOrdersIssued = movementOrdersIssued;
        MovementOrdersRetained = movementOrdersRetained;
        ForcedMovementOrders = forcedMovementOrders;
        MovementStops = movementStops;
    }
}

/// <summary>移动控制器一次保留、签发或结束订单的纯数据摘要。</summary>
public readonly struct CombatMovementTrace
{
    public readonly long Sequence;
    public readonly long ActorId;
    public readonly CombatMovementDecision Decision;
    public readonly int DesiredX;
    public readonly int DesiredY;
    public readonly int CommittedX;
    public readonly int CommittedY;

    internal CombatMovementTrace(
        long sequence,
        long actorId,
        CombatMovementDecision decision,
        WorldTile desired,
        WorldTile committed)
    {
        Sequence = sequence;
        ActorId = actorId;
        Decision = decision;
        DesiredX = desired?.x ?? int.MinValue;
        DesiredY = desired?.y ?? int.MinValue;
        CommittedX = committed?.x ?? int.MinValue;
        CommittedY = committed?.y ?? int.MinValue;
    }
}

/// <summary>移动订单提交层对本轮期望终点作出的处理。</summary>
public enum CombatMovementDecision
{
    Issued,
    Forced,
    Retained,
    Stopped,
}

/// <summary>最近一次规划留下的有界摘要，不持有 Actor、Tile 或技能实体引用。</summary>
public readonly struct CombatDecisionTrace
{
    public readonly long Sequence;
    public readonly long ActorId;
    public readonly int Revision;
    public readonly bool HighFidelity;
    public readonly CombatIntent Intent;
    public readonly float StrengthRatio;
    public readonly long TargetId;
    public readonly string Action;
    public readonly CombatActionUse ActionUse;
    public readonly long AssistedAllyId;
    public readonly CombatThreatSource ThreatSource;
    public readonly CombatPositionRole PositionRole;
    public readonly bool HasPosition;

    internal CombatDecisionTrace(
        long sequence,
        CombatPlanningSnapshot snapshot,
        CombatPlan plan)
    {
        Sequence = sequence;
        ActorId = snapshot.ActorId;
        Revision = snapshot.Revision;
        HighFidelity = snapshot.HighFidelity;
        Intent = plan?.Intent ?? CombatIntent.None;
        StrengthRatio = plan?.Outcome.StrengthRatio ?? 0f;
        TargetId = plan?.HasEnemy == true ? plan.PrimaryEnemy.Id : 0;
        Action = plan?.Action?.Key.ToString() ?? string.Empty;
        ActionUse = plan?.ActionUse ?? CombatActionUse.None;
        AssistedAllyId = plan?.AssistedAllyId ?? 0;
        ThreatSource = plan?.HasEnemy == true
            ? plan.PrimaryEnemy.ThreatSource
            : CombatThreatSource.None;
        PositionRole = plan?.HasPosition == true
            ? plan.Position.Role
            : CombatPositionRole.Tactical;
        HasPosition = plan?.HasPosition == true;
    }
}

/// <summary>一次整军溃退发生时的共识输入，不持有军队或角色对象。</summary>
public readonly struct CombatArmyRoutTrace
{
    public readonly long Sequence;
    public readonly long ArmyId;
    public readonly float Morale;
    public readonly float CasualtyRatio;
    public readonly int UnfavorableReports;
    public readonly int ReportCount;
    public readonly int RequiredReports;

    internal CombatArmyRoutTrace(
        long sequence,
        long armyId,
        float morale,
        float casualtyRatio,
        int unfavorableReports,
        int reportCount,
        int requiredReports)
    {
        Sequence = sequence;
        ArmyId = armyId;
        Morale = morale;
        CasualtyRatio = casualtyRatio;
        UnfavorableReports = unfavorableReports;
        ReportCount = reportCount;
        RequiredReports = requiredReports;
    }
}

/// <summary>
/// 线程安全的战斗诊断入口。只保存计数和最近 128 条纯数据摘要，不扩大世界运行时引用集。
/// </summary>
public static class CombatDiagnostics
{
    private const int TraceLimit = 128;
    private static readonly object TraceLock = new();
    private static readonly Queue<CombatDecisionTrace> Traces = new(TraceLimit);
    private static readonly Queue<CombatMovementTrace> MovementTraces = new(TraceLimit);
    private static readonly Queue<CombatArmyRoutTrace> ArmyRoutTraces = new(TraceLimit);
    private static long sequence;
    private static long highFidelityPlans;
    private static long lowFidelityPlans;
    private static long commits;
    private static long discardedCommits;
    private static long startedActions;
    private static long blockedActions;
    private static long invalidActions;
    private static long replans;
    private static long pathFailures;
    private static long armyRouts;
    private static long threatSignals;
    private static long assistPlans;
    private static long movementOrdersIssued;
    private static long movementOrdersRetained;
    private static long forcedMovementOrders;
    private static long movementStops;

    /// <summary>复制当前累计计数。</summary>
    public static CombatDiagnosticsSnapshot GetSnapshot()
    {
        return new CombatDiagnosticsSnapshot(
            Interlocked.Read(ref highFidelityPlans),
            Interlocked.Read(ref lowFidelityPlans),
            Interlocked.Read(ref commits),
            Interlocked.Read(ref discardedCommits),
            Interlocked.Read(ref startedActions),
            Interlocked.Read(ref blockedActions),
            Interlocked.Read(ref invalidActions),
            Interlocked.Read(ref replans),
            Interlocked.Read(ref pathFailures),
            Interlocked.Read(ref armyRouts),
            Interlocked.Read(ref threatSignals),
            Interlocked.Read(ref assistPlans),
            Interlocked.Read(ref movementOrdersIssued),
            Interlocked.Read(ref movementOrdersRetained),
            Interlocked.Read(ref forcedMovementOrders),
            Interlocked.Read(ref movementStops));
    }

    /// <summary>按发生顺序复制仍在环形窗口内的规划摘要。</summary>
    public static void CopyRecentDecisions(ICollection<CombatDecisionTrace> output)
    {
        lock (TraceLock)
        {
            foreach (CombatDecisionTrace trace in Traces) output.Add(trace);
        }
    }

    /// <summary>按发生顺序复制仍在环形窗口内的移动订单摘要。</summary>
    public static void CopyRecentMovements(ICollection<CombatMovementTrace> output)
    {
        lock (TraceLock)
        {
            foreach (CombatMovementTrace trace in MovementTraces) output.Add(trace);
        }
    }

    /// <summary>按发生顺序复制仍在环形窗口内的整军溃退摘要。</summary>
    public static void CopyRecentArmyRouts(ICollection<CombatArmyRoutTrace> output)
    {
        lock (TraceLock)
        {
            foreach (CombatArmyRoutTrace trace in ArmyRoutTraces) output.Add(trace);
        }
    }

    internal static void RecordPlan(CombatPlanningSnapshot snapshot, CombatPlan plan)
    {
        if (snapshot.HighFidelity) Interlocked.Increment(ref highFidelityPlans);
        else Interlocked.Increment(ref lowFidelityPlans);
        if (plan?.HasEnemy != true) return;
        if (plan.Intent is CombatIntent.Assist or CombatIntent.Protect)
            Interlocked.Increment(ref assistPlans);

        var trace = new CombatDecisionTrace(
            Interlocked.Increment(ref sequence),
            snapshot,
            plan);
        lock (TraceLock)
        {
            while (Traces.Count >= TraceLimit) Traces.Dequeue();
            Traces.Enqueue(trace);
        }
    }

    internal static void RecordCommit(bool accepted)
    {
        if (accepted) Interlocked.Increment(ref commits);
        else Interlocked.Increment(ref discardedCommits);
    }

    internal static void RecordExecution(CombatExecutionStatus status)
    {
        switch (status)
        {
            case CombatExecutionStatus.Started:
                Interlocked.Increment(ref startedActions);
                break;
            case CombatExecutionStatus.TemporarilyBlocked:
                Interlocked.Increment(ref blockedActions);
                break;
            case CombatExecutionStatus.Invalid:
                Interlocked.Increment(ref invalidActions);
                break;
        }
    }

    internal static void RecordReplan()
    {
        Interlocked.Increment(ref replans);
    }

    internal static void RecordPathFailure()
    {
        Interlocked.Increment(ref pathFailures);
    }

    internal static void RecordThreatSignal()
    {
        Interlocked.Increment(ref threatSignals);
    }

    internal static void RecordArmyRout(
        long armyId,
        float morale,
        float casualtyRatio,
        int unfavorableReports,
        int reportCount,
        int requiredReports)
    {
        Interlocked.Increment(ref armyRouts);
        var trace = new CombatArmyRoutTrace(
            Interlocked.Increment(ref sequence),
            armyId,
            morale,
            casualtyRatio,
            unfavorableReports,
            reportCount,
            requiredReports);
        lock (TraceLock)
        {
            while (ArmyRoutTraces.Count >= TraceLimit) ArmyRoutTraces.Dequeue();
            ArmyRoutTraces.Enqueue(trace);
        }
    }

    internal static void RecordMovementIssued(long actorId, WorldTile desired, bool forced)
    {
        Interlocked.Increment(ref movementOrdersIssued);
        CombatMovementDecision decision = forced
            ? CombatMovementDecision.Forced
            : CombatMovementDecision.Issued;
        if (forced) Interlocked.Increment(ref forcedMovementOrders);
        RecordMovementTrace(actorId, decision, desired, desired);
    }

    internal static void RecordMovementRetained(
        long actorId,
        WorldTile desired,
        WorldTile committed)
    {
        Interlocked.Increment(ref movementOrdersRetained);
        RecordMovementTrace(actorId, CombatMovementDecision.Retained, desired, committed);
    }

    internal static void RecordMovementStopped(long actorId, WorldTile committed)
    {
        Interlocked.Increment(ref movementStops);
        RecordMovementTrace(actorId, CombatMovementDecision.Stopped, null, committed);
    }

    private static void RecordMovementTrace(
        long actorId,
        CombatMovementDecision decision,
        WorldTile desired,
        WorldTile committed)
    {
        var trace = new CombatMovementTrace(
            Interlocked.Increment(ref sequence),
            actorId,
            decision,
            desired,
            committed);
        lock (TraceLock)
        {
            while (MovementTraces.Count >= TraceLimit) MovementTraces.Dequeue();
            MovementTraces.Enqueue(trace);
        }
    }

    internal static void Reset()
    {
        Interlocked.Exchange(ref sequence, 0);
        Interlocked.Exchange(ref highFidelityPlans, 0);
        Interlocked.Exchange(ref lowFidelityPlans, 0);
        Interlocked.Exchange(ref commits, 0);
        Interlocked.Exchange(ref discardedCommits, 0);
        Interlocked.Exchange(ref startedActions, 0);
        Interlocked.Exchange(ref blockedActions, 0);
        Interlocked.Exchange(ref invalidActions, 0);
        Interlocked.Exchange(ref replans, 0);
        Interlocked.Exchange(ref pathFailures, 0);
        Interlocked.Exchange(ref armyRouts, 0);
        Interlocked.Exchange(ref threatSignals, 0);
        Interlocked.Exchange(ref assistPlans, 0);
        Interlocked.Exchange(ref movementOrdersIssued, 0);
        Interlocked.Exchange(ref movementOrdersRetained, 0);
        Interlocked.Exchange(ref forcedMovementOrders, 0);
        Interlocked.Exchange(ref movementStops, 0);
        lock (TraceLock)
        {
            Traces.Clear();
            MovementTraces.Clear();
            ArmyRoutTraces.Clear();
        }
    }
}

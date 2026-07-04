using System;

namespace Cultiway.Core.Pathfinding;

public enum PathRequestState
{
    Pending,
    Streaming,
    Succeeded,
    Failed,
    Cancelled
}

public enum PathPollKind
{
    StepReady,
    Waiting,
    Completed,
    Failed,
    Cancelled,
    NoRequest
}

public enum PathFailureReason
{
    None,
    InvalidActor,
    InvalidStart,
    InvalidTarget,
    Unreachable,
    SearchLimitExceeded,
    UnsafeStep,
    StepBlocked,
    PortalUnavailable,
    TransportFailed,
    Timeout,
    GeneratorException,
    CancelledByNewRequest,
    ActorDead,
    ClearWorld
}

public readonly struct PathPollResult
{
    private PathPollResult(PathPollKind kind, PathStep step, PathFailureReason failureReason, Exception error)
    {
        Kind = kind;
        Step = step;
        FailureReason = failureReason;
        Error = error;
    }

    public PathPollKind Kind { get; }
    public PathStep Step { get; }
    public PathFailureReason FailureReason { get; }
    public Exception Error { get; }

    public static PathPollResult StepReady(PathStep step)
    {
        return new PathPollResult(PathPollKind.StepReady, step, PathFailureReason.None, null);
    }

    public static PathPollResult Waiting()
    {
        return new PathPollResult(PathPollKind.Waiting, default, PathFailureReason.None, null);
    }

    public static PathPollResult Completed()
    {
        return new PathPollResult(PathPollKind.Completed, default, PathFailureReason.None, null);
    }

    public static PathPollResult Failed(PathFailureReason reason, Exception error = null)
    {
        return new PathPollResult(PathPollKind.Failed, default, reason, error);
    }

    public static PathPollResult Cancelled(PathFailureReason reason)
    {
        return new PathPollResult(PathPollKind.Cancelled, default, reason, null);
    }

    public static PathPollResult NoRequest()
    {
        return new PathPollResult(PathPollKind.NoRequest, default, PathFailureReason.None, null);
    }
}

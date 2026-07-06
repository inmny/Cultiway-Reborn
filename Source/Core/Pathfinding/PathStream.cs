using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Cultiway.Core.Pathfinding;

public sealed class PathStream : IPathStreamWriter
{
    private readonly ConcurrentQueue<PathStep> _steps = new();
    private int _status;
    private PathFailureReason _failureReason;
    private Exception _error;

    public void AddStep(PathStep step)
    {
        if (!step.HasTile || IsFinalized) return;
        _steps.Enqueue(step);
    }

    public bool TryDequeue(out PathStep step)
    {
        return _steps.TryDequeue(out step);
    }

    public int DrainMovementSteps(List<WorldTile> path, int maxCount)
    {
        if (path == null || maxCount <= 0)
        {
            return 0;
        }

        int count = 0;
        while (count < maxCount && _steps.TryPeek(out var step))
        {
            if (step.Method != MovementMethod.Walk && step.Method != MovementMethod.Swim)
            {
                break;
            }

            var tile = step.Tile;
            if (tile == null)
            {
                break;
            }

            if (!_steps.TryDequeue(out step))
            {
                break;
            }

            path.Add(tile);
            count++;
        }

        return count;
    }

    public List<PathStep> TryViewAll()
    {
        return _steps.ToList();
    }
    public bool TryPeek(out PathStep step)
    {
        return _steps.TryPeek(out step);
    }

    public void Complete()
    {
        Interlocked.CompareExchange(ref _status, 1, 0);
    }

    public void Cancel(PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        if (IsFinalized)
        {
            return;
        }

        _failureReason = reason;
        Interlocked.CompareExchange(ref _status, 2, 0);
    }

    public void Fail(PathFailureReason reason, Exception error = null)
    {
        if (IsFinalized)
        {
            return;
        }

        _failureReason = reason == PathFailureReason.None ? PathFailureReason.GeneratorException : reason;
        _error = error;
        Interlocked.CompareExchange(ref _status, 3, 0);
    }

    public void Fail(Exception error)
    {
        Fail(PathFailureReason.GeneratorException, error);
    }

    public void EnsureCompleted()
    {
        if (!IsFinalized)
        {
            Complete();
        }
    }

    public bool HasPendingSteps => !_steps.IsEmpty;
    public int PendingCount => _steps.Count;
    public PathRequestState State
    {
        get
        {
            var status = Volatile.Read(ref _status);
            return status switch
            {
                0 => HasPendingSteps ? PathRequestState.Streaming : PathRequestState.Pending,
                1 => PathRequestState.Succeeded,
                2 => PathRequestState.Cancelled,
                3 => PathRequestState.Failed,
                _ => PathRequestState.Failed
            };
        }
    }
    public bool IsFinalized => _status != 0;
    public bool IsFinished => IsFinalized && _steps.IsEmpty;
    public bool IsFaulted => _status == 3;
    public bool IsCancelled => _status == 2;
    public PathFailureReason FailureReason => _failureReason;
    public Exception Error => _error;
}

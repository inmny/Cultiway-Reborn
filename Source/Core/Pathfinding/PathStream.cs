using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Cultiway.Core.Pathfinding;

public sealed class PathStream : IPathStreamWriter
{
    private readonly ConcurrentQueue<PathStep> _steps = new();
    private int _status;
    private Exception _error;

    public void AddStep(WorldTile tile, MovementMethod method)
    {
        if (tile == null || IsFinalized)
        {
            return;
        }

        _steps.Enqueue(new PathStep(tile, method));
    }

    public bool TryDequeue(out PathStep step)
    {
        return _steps.TryDequeue(out step);
    }

    public bool TryPeek(out PathStep step)
    {
        return _steps.TryPeek(out step);
    }

    public void Complete()
    {
        Interlocked.CompareExchange(ref _status, 1, 0);
    }

    public void Cancel()
    {
        Interlocked.Exchange(ref _status, 2);
    }

    public void Fail(Exception error)
    {
        _error = error;
        Interlocked.Exchange(ref _status, 3);
    }

    public void EnsureCompleted()
    {
        if (!IsFinalized)
        {
            Complete();
        }
    }

    public bool HasPendingSteps => !_steps.IsEmpty;
    public bool IsFinalized => _status != 0;
    public bool IsFinished => IsFinalized && _steps.IsEmpty;
    public bool IsFaulted => _status == 3;
    public bool IsCancelled => _status == 2;
    public Exception Error => _error;
}

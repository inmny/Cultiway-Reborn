using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Utils;
using Cultiway;
using Cultiway.Const;
using Cultiway.Debug;
using System.Collections.Generic;

namespace Cultiway.Core.Pathfinding;

public class PathFinder
{
    public static PathFinder Instance { get; } = new();
    internal static readonly object ActorSyncLock = new object();

    private readonly ConcurrentDictionary<long, PathfindingTask> _tasks = new();
    private readonly ConcurrentDictionary<long, PathRequestOptions> _lastRequests = new();
    private readonly ConcurrentQueue<PathfindingTask> _pendingTasks = new();
    private readonly AutoResetEvent _pendingSignal = new(false);
    private readonly object _workerLock = new();
    private IPathGenerator _generator;
    private bool _workersStarted;

    public void UseGenerator(IPathGenerator generator)
    {
        _generator = generator ?? new PortalAwarePathGenerator(PortalRegistry.Instance, PathfindingConfig.Default);
    }

    public bool RequestPath(Actor actor, WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int limitRegions)
    {
        if (!CanAcceptRequest(actor, target, out _))
        {
            return false;
        }
        if (TryReuseActiveRequest(actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions))
        {
            return true;
        }

        var request = new PathRequest(actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions);

        return RequestPathCore(request, true, true);
    }

    public bool RequestPath(PathRequest request)
    {
        return RequestPathCore(request, false, false);
    }

    private bool RequestPathCore(PathRequest request, bool alreadyValidated,
        bool alreadyCheckedReuse)
    {
        if (!alreadyValidated)
        {
            if (!CanAcceptRequest(request.Actor, request.Target, out _))
            {
                return false;
            }
        }

        if (!alreadyCheckedReuse)
        {
            if (TryReuseActiveRequest(request.Actor, request.Target, request.PathOnWater, request.WalkOnBlocks,
                    request.WalkOnLava, request.RegionLimit))
            {
                return true;
            }
        }

        _lastRequests[request.Actor.data.id] = new PathRequestOptions(request.Target, request.PathOnWater,
            request.WalkOnBlocks, request.WalkOnLava, request.RegionLimit);
        Cancel(request.Actor);

        var task = new PathfindingTask(request);
        _tasks[request.Actor.data.id] = task;

        EnqueueTask(task);
        return true;
    }

    private void EnqueueTask(PathfindingTask task)
    {
        EnsureWorkersStarted();
        _pendingTasks.Enqueue(task);
        _pendingSignal.Set();
    }

    private void EnsureWorkersStarted()
    {
        if (_workersStarted)
        {
            return;
        }

        lock (_workerLock)
        {
            if (_workersStarted)
            {
                return;
            }

            int workerCount = Math.Min(4, PerformanceSettings.WorkerCount);
            for (int i = 0; i < workerCount; i++)
            {
                var worker = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"CultiwayPathFinder-{i}",
                    Priority = ThreadPriority.BelowNormal
                };
                worker.Start();
            }

            _workersStarted = true;
        }
    }

    private void WorkerLoop()
    {
        while (true)
        {
            if (!_pendingTasks.TryDequeue(out var task))
            {
                _pendingSignal.WaitOne(50);
                continue;
            }

            RunGenerator(task);
            task.MarkWorkerFinished();
        }
    }

    private bool TryReuseActiveRequest(Actor actor, WorldTile target, bool pathOnWater, bool walkOnBlocks,
        bool walkOnLava, int limitRegions)
    {
        if (actor?.data == null || target == null)
        {
            return false;
        }

        long actorId = actor.data.id;
        if (!_tasks.TryGetValue(actorId, out var task))
        {
            return false;
        }

        if (!task.Request.HasSameTargetAndOptions(target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions))
        {
            return false;
        }

        PathRequestState state = task.Stream.State;
        if (state == PathRequestState.Pending ||
            state == PathRequestState.Streaming ||
            task.Stream.HasPendingSteps)
        {
            return true;
        }

        Cleanup(actorId, task);
        return false;
    }

    public bool CanAcceptRequest(Actor actor, WorldTile target, out PathFailureReason failureReason)
    {
        if (actor?.data == null)
        {
            failureReason = PathFailureReason.InvalidActor;
            return false;
        }

        if (actor.current_tile == null)
        {
            failureReason = PathFailureReason.InvalidStart;
            return false;
        }

        if (target == null)
        {
            failureReason = PathFailureReason.InvalidTarget;
            return false;
        }

        if (actor.asset == null)
        {
            failureReason = PathFailureReason.InvalidActor;
            return false;
        }

        if (actor.asset.is_boat && !target.isGoodForBoat())
        {
            failureReason = PathFailureReason.InvalidTarget;
            return false;
        }

        failureReason = PathFailureReason.None;
        return true;
    }

    public void RequestDirectPath(Actor actor, WorldTile target)
    {
        if (actor?.data == null || target == null)
        {
            return;
        }

        _lastRequests[actor.data.id] = new PathRequestOptions(target, true, true, true, 0);
        Cancel(actor);

        var task = new PathfindingTask(new PathRequest(actor, target, true, true, true, 0));
        task.Stream.AddStep(new PathStep(target, MovementMethod.Walk, TraversalEstimate.Direct));
        task.Stream.Complete();
        task.MarkWorkerFinished();
        _tasks[actor.data.id] = task;
    }

    public bool IsActorPathing(Actor actor)
    {
        if (actor?.data == null)
        {
            return false;
        }
        if (!_tasks.TryGetValue(actor.data.id, out var task))
        {
            return false;
        }

        if (task.Stream.HasPendingSteps ||
            task.Stream.State == PathRequestState.Pending ||
            task.Stream.State == PathRequestState.Streaming)
        {
            return true;
        }

        Cleanup(actor.data.id, task);
        return false;
    }
    public List<PathStep> TryViewAll(Actor actor)
    {
        if (actor?.data == null) return null;
        if (!_tasks.TryGetValue(actor.data.id, out var task)) return null;
        return task.Stream.TryViewAll();
    }

    public PathPollResult PollStep(Actor actor)
    {
        if (actor?.data == null)
        {
            return PathPollResult.Failed(PathFailureReason.InvalidActor);
        }

        if (!_tasks.TryGetValue(actor.data.id, out var task))
        {
            return PathPollResult.NoRequest();
        }

        return GetPollResult(actor.data.id, task);
    }

    public PathPollResult PeekReadyStep(Actor actor, out ReadyPathStep readyStep)
    {
        readyStep = default;
        if (actor?.data == null)
        {
            return PathPollResult.Failed(PathFailureReason.InvalidActor);
        }

        if (!_tasks.TryGetValue(actor.data.id, out var task))
        {
            return PathPollResult.NoRequest();
        }

        var result = GetPollResult(actor.data.id, task);
        if (result.Kind == PathPollKind.StepReady)
        {
            readyStep = new ReadyPathStep(this, actor.data.id, task, result.Step);
        }

        return result;
    }

    public PathPollResult OpenReadyCursor(Actor actor, out ReadyPathCursor cursor)
    {
        cursor = default;
        if (actor?.data == null)
        {
            return PathPollResult.Failed(PathFailureReason.InvalidActor);
        }

        var actorId = actor.data.id;
        if (!_tasks.TryGetValue(actorId, out var task))
        {
            return PathPollResult.NoRequest();
        }

        var result = GetPollResult(actorId, task);
        if (result.Kind == PathPollKind.StepReady)
        {
            cursor = new ReadyPathCursor(this, actorId, task);
        }

        return result;
    }

    public bool TryPeekStep(Actor actor, out PathStep step, out bool finished)
    {
        finished = false;
        step = default;
        var result = PollStep(actor);
        if (result.Kind == PathPollKind.StepReady)
        {
            step = result.Step;
            return true;
        }

        finished = result.Kind != PathPollKind.Waiting;
        return false;
    }

    private PathPollResult GetPollResult(long actorId, PathfindingTask task)
    {
        if (task.Stream.TryPeek(out var step))
        {
            return PathPollResult.StepReady(step);
        }

        switch (task.Stream.State)
        {
            case PathRequestState.Pending:
            case PathRequestState.Streaming:
                return PathPollResult.Waiting();
            case PathRequestState.Succeeded:
                Cleanup(actorId, task);
                return PathPollResult.Completed();
            case PathRequestState.Failed:
                var failure = task.Stream.FailureReason == PathFailureReason.None
                    ? PathFailureReason.GeneratorException
                    : task.Stream.FailureReason;
                var error = task.Stream.Error;
                Cleanup(actorId, task);
                return PathPollResult.Failed(failure, error);
            case PathRequestState.Cancelled:
                var cancelReason = task.Stream.FailureReason == PathFailureReason.None
                    ? PathFailureReason.CancelledByNewRequest
                    : task.Stream.FailureReason;
                Cleanup(actorId, task);
                return PathPollResult.Cancelled(cancelReason);
            default:
                Cleanup(actorId, task);
                return PathPollResult.Failed(PathFailureReason.GeneratorException);
        }
    }

    public readonly struct ReadyPathStep
    {
        private readonly PathFinder _owner;
        private readonly long _actorId;
        private readonly PathfindingTask _task;

        internal ReadyPathStep(PathFinder owner, long actorId, PathfindingTask task, PathStep step)
        {
            _owner = owner;
            _actorId = actorId;
            _task = task;
            Step = step;
        }

        public PathStep Step { get; }
        public bool IsValid => _owner != null && _task != null;

        public void Consume()
        {
            if (!IsValid || !_task.Stream.TryDequeue(out _))
            {
                return;
            }

            if (_task.Stream.IsFinished && !_task.Stream.HasPendingSteps)
            {
                _owner.Cleanup(_actorId, _task);
            }
        }
    }

    public readonly struct ReadyPathCursor
    {
        private readonly PathFinder _owner;
        private readonly long _actorId;
        private readonly PathfindingTask _task;

        internal ReadyPathCursor(PathFinder owner, long actorId, PathfindingTask task)
        {
            _owner = owner;
            _actorId = actorId;
            _task = task;
        }

        public bool IsValid => _owner != null && _task != null;

        public PathPollResult Poll()
        {
            return IsValid ? _owner.GetPollResult(_actorId, _task) : PathPollResult.NoRequest();
        }

        public void Consume()
        {
            if (!IsValid || !_task.Stream.TryDequeue(out _))
            {
                return;
            }

            if (_task.Stream.IsFinished && !_task.Stream.HasPendingSteps)
            {
                _owner.Cleanup(_actorId, _task);
            }
        }
    }

    public void ConsumeStep(Actor actor)
    {
        if (actor?.data == null)
        {
            return;
        }

        if (!_tasks.TryGetValue(actor.data.id, out var task))
        {
            return;
        }

        if (task.Stream.TryDequeue(out _))
        {
            if (task.Stream.IsFinished && !task.Stream.HasPendingSteps)
            {
                Cleanup(actor.data.id, task);
            }
        }
    }

    public void Cancel(Actor actor, PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        if (actor?.data == null)
        {
            return;
        }

        if (_tasks.TryRemove(actor.data.id, out var task))
        {
            task.Stream.Cancel(reason);
            task.Cancellation.Cancel();
            task.DisposeWhenWorkerFinished();
        }
    }

    private void RunGenerator(PathfindingTask task)
    {
        try
        {
            _generator.GenerateAsync(task.Request, task.Stream, task.Cancellation.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            task.Stream.Cancel();
        }
        catch (Exception e)
        {
            task.Stream.Fail(PathFailureReason.GeneratorException, e);
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
        }
        finally
        {
            task.Stream.EnsureCompleted();
        }
    }

    private void Cleanup(long actorId, PathfindingTask task)
    {
        var entry = new KeyValuePair<long, PathfindingTask>(actorId, task);
        if (((ICollection<KeyValuePair<long, PathfindingTask>>)_tasks).Remove(entry))
        {
            task.DisposeWhenWorkerFinished();
        }
    }
    public void Cleanup(long actorId)
    {
        if (_tasks.TryRemove(actorId, out var task))
        {
            task.DisposeWhenWorkerFinished();
        }
        _lastRequests.TryRemove(actorId, out _);
    }

    public void Clear()
    {
        foreach (var id_task_pair in _tasks)
        {
            var task = id_task_pair.Value;
            task.Stream.Cancel(PathFailureReason.ClearWorld);
            task.Cancellation.Cancel();
            task.DisposeWhenWorkerFinished();
        }
        _tasks.Clear();
        _lastRequests.Clear();
    }

    internal bool TryGetLastRequestOptions(Actor actor, out PathRequestOptions options)
    {
        options = default;
        if (actor?.data == null)
        {
            return false;
        }

        return _lastRequests.TryGetValue(actor.data.id, out options);
    }

    internal bool TryRequestRecover(Actor actor, WorldTile overrideTarget = null)
    {
        if (actor == null || actor.data == null)
        {
            return false;
        }
        if (!TryGetLastRequestOptions(actor, out var opt))
        {
            return false;
        }
        var target = overrideTarget ?? actor.tile_target ?? opt.Target;
        if (target == null)
        {
            return false;
        }

        if (!CanAcceptRequest(actor, target, out _))
        {
            return false;
        }

        return RequestPath(new PathRequest(actor, target, opt.PathOnWater, opt.WalkOnBlocks, opt.WalkOnLava,
            opt.RegionLimit));
    }
}

internal sealed class PathfindingTask : IDisposable
{
    private int _disposeRequested;
    private int _disposed;
    private int _workerFinished;

    public PathfindingTask(PathRequest request)
    {
        Request = request;
        Stream = new PathStream();
        Cancellation = new CancellationTokenSource();
    }

    public PathRequest Request { get; }
    public PathStream Stream { get; }
    public CancellationTokenSource Cancellation { get; }

    public void MarkWorkerFinished()
    {
        Volatile.Write(ref _workerFinished, 1);
        if (Volatile.Read(ref _disposeRequested) != 0)
        {
            Dispose();
        }
    }

    public void DisposeWhenWorkerFinished()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
        {
            return;
        }

        if (Volatile.Read(ref _workerFinished) != 0)
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Cancellation.Dispose();
    }
}

internal readonly struct PathRequestOptions
{
    public PathRequestOptions(WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava, int regionLimit)
    {
        Target = target;
        PathOnWater = pathOnWater;
        WalkOnBlocks = walkOnBlocks;
        WalkOnLava = walkOnLava;
        RegionLimit = regionLimit;
    }

    public WorldTile Target { get; }
    public bool PathOnWater { get; }
    public bool WalkOnBlocks { get; }
    public bool WalkOnLava { get; }
    public int RegionLimit { get; }
}

internal sealed class PassthroughPathGenerator : IPathGenerator
{
    public Task GenerateAsync(PathRequest request, IPathStreamWriter stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.TargetTileId >= 0)
        {
            stream.AddStep(new PathStep(request.TargetTileId, MovementMethod.Walk, TraversalEstimate.Direct));
        }
        else
        {
            stream.Fail(PathFailureReason.InvalidTarget);
            return Task.CompletedTask;
        }

        stream.Complete();
        return Task.CompletedTask;
    }
}

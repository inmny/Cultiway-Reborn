using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Core.Performance;
using Cultiway.Utils;
using Cultiway;
using Cultiway.Const;
using Cultiway.Debug;
using System.Collections.Generic;
using System.Globalization;

namespace Cultiway.Core.Pathfinding;

public class PathFinder
{
    public static PathFinder Instance { get; } = new();
    internal static readonly object ActorSyncLock = new object();

    // 路径 worker 只持有 PathfindingTask/PathStream，不访问任务表。
    // 任务表由有序模拟提交线程独占写入；批量准备阶段只做并发只读。
    private readonly Dictionary<long, PathfindingTask> _tasks = new();
    private readonly Dictionary<long, PathRequestOptions> _lastRequests = new();
    private readonly ConcurrentQueue<PathfindingTask> _pendingTasks = new();
    private readonly AutoResetEvent _pendingSignal = new(false);
    private readonly object _workerLock = new();
    private IPathGenerator _generator;
    private bool _workersStarted;
    private int _workerCount;

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

        return RequestPathValidated(
            actor,
            target,
            pathOnWater,
            walkOnBlocks,
            walkOnLava,
            limitRegions);
    }

    internal bool RequestPathValidated(
        Actor actor,
        WorldTile target,
        bool pathOnWater,
        bool walkOnBlocks,
        bool walkOnLava,
        int limitRegions)
    {
        PathfindingProfiler.Measurement reuseMeasurement = PathfindingProfiler.Start();
        bool reused = TryReuseActiveRequest(actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions);
        reuseMeasurement.Complete(
            reused
                ? PathfindingBenchmarkMetric.Reuse
                : PathfindingBenchmarkMetric.ReuseMiss);
        if (reused)
        {
            return true;
        }

        PathRequest request = PrepareValidatedRequest(
            actor,
            target,
            pathOnWater,
            walkOnBlocks,
            walkOnLava,
            limitRegions);

        return RequestPathCore(request, true, true);
    }

    internal PathRequest PrepareValidatedRequest(
        Actor actor,
        WorldTile target,
        bool pathOnWater,
        bool walkOnBlocks,
        bool walkOnLava,
        int limitRegions)
    {
        PathfindingProfiler.Measurement createMeasurement =
            PathfindingProfiler.Start();
        var request = new PathRequest(
            actor,
            target,
            pathOnWater,
            walkOnBlocks,
            walkOnLava,
            limitRegions);
        createMeasurement.Complete(PathfindingBenchmarkMetric.Create);
        return request;
    }

    internal PathRequest TryPrepareValidatedRequest(
        Actor actor,
        WorldTile target,
        bool pathOnWater,
        bool walkOnBlocks,
        bool walkOnLava,
        int limitRegions)
    {
        PathfindingProfiler.Measurement reuseMeasurement =
            PathfindingProfiler.Start();
        bool reused = TryReuseActiveRequestReadOnly(
            actor,
            target,
            pathOnWater,
            walkOnBlocks,
            walkOnLava,
            limitRegions);
        reuseMeasurement.Complete(
            reused
                ? PathfindingBenchmarkMetric.Reuse
                : PathfindingBenchmarkMetric.ReuseMiss);
        return reused
            ? null
            : PrepareValidatedRequest(
                actor,
                target,
                pathOnWater,
                walkOnBlocks,
                walkOnLava,
                limitRegions);
    }

    /// <summary>
    /// 路径快照可以并行构建，但活动任务表必须按原调用顺序提交。
    /// 这样同一角色在一个 AI 周期内多次 goTo 时，最后一次请求仍然获胜，
    /// 同时避免多个 worker 争用任务字典和逐请求唤醒寻路线程。
    /// </summary>
    internal void RequestPreparedBatch(
        IReadOnlyList<PathRequest> requests,
        int count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureWorkersStarted();
        int enqueuedCount = 0;
        for (int i = 0; i < count; i++)
        {
            PathRequest request = requests[i];
            if (request == null)
            {
                continue;
            }

            PathfindingProfiler.Measurement reuseMeasurement =
                PathfindingProfiler.Start();
            bool reused = TryReuseActiveRequest(
                request.Actor,
                request.Target,
                request.PathOnWater,
                request.WalkOnBlocks,
                request.WalkOnLava,
                request.RegionLimit);
            reuseMeasurement.Complete(
                reused
                    ? PathfindingBenchmarkMetric.Reuse
                    : PathfindingBenchmarkMetric.ReuseMiss);
            if (reused)
            {
                continue;
            }

            if (RequestPathCore(
                    request,
                    alreadyValidated: true,
                    alreadyCheckedReuse: true,
                    signalWorker: false))
            {
                enqueuedCount++;
            }
        }

        SignalPendingWorkers(enqueuedCount);
    }

    public bool RequestPath(PathRequest request)
    {
        return RequestPathCore(request, false, false);
    }

    private bool RequestPathCore(PathRequest request, bool alreadyValidated,
        bool alreadyCheckedReuse, bool signalWorker = true)
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
            PathfindingProfiler.Measurement reuseMeasurement = PathfindingProfiler.Start();
            bool reused = TryReuseActiveRequest(
                request.Actor,
                request.Target,
                request.PathOnWater,
                request.WalkOnBlocks,
                request.WalkOnLava,
                request.RegionLimit);
            reuseMeasurement.Complete(
                reused
                    ? PathfindingBenchmarkMetric.Reuse
                    : PathfindingBenchmarkMetric.ReuseMiss);
            if (reused)
            {
                return true;
            }
        }

        _lastRequests[request.Actor.data.id] = new PathRequestOptions(request.Target, request.PathOnWater,
            request.WalkOnBlocks, request.WalkOnLava, request.RegionLimit);
        Cancel(request.Actor);

        PathfindingProfiler.Measurement taskCreateMeasurement = PathfindingProfiler.Start();
        var task = new PathfindingTask(request, taskCreateMeasurement.Session);
        _tasks[request.Actor.data.id] = task;
        taskCreateMeasurement.Complete(PathfindingBenchmarkMetric.TaskCreate);

        EnqueueTask(task, signalWorker);
        return true;
    }

    private void EnqueueTask(
        PathfindingTask task,
        bool signalWorker)
    {
        PathfindingProfiler.Measurement enqueueMeasurement =
            PathfindingProfiler.Start(task.BenchmarkSession);
        EnsureWorkersStarted();
        task.MarkEnqueued();
        _pendingTasks.Enqueue(task);
        if (signalWorker)
        {
            _pendingSignal.Set();
        }

        enqueueMeasurement.Complete(PathfindingBenchmarkMetric.Enqueue);
    }

    private void SignalPendingWorkers(int enqueuedCount)
    {
        int signals = Math.Min(
            Math.Max(0, enqueuedCount),
            Math.Max(1, Volatile.Read(ref _workerCount)));
        for (int i = 0; i < signals; i++)
        {
            _pendingSignal.Set();
        }
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

            int workerCount = PerformanceSettings.PathfindingWorkerCount;
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

            _workerCount = workerCount;
            _workersStarted = true;
        }
    }

    internal void EnsureWorkersReady()
    {
        EnsureWorkersStarted();
    }

    internal string GetDiagnostics()
    {
        string generatorDiagnostics =
            _generator is PortalAwarePathGenerator portalAware
                ? portalAware.GetDiagnostics()
                : _generator?.GetType().Name ?? "none";
        return string.Format(
            CultureInfo.InvariantCulture,
            "active={0} pending={1} workers={2} generator=[{3}]",
            _tasks.Count,
            _pendingTasks.Count,
            Volatile.Read(ref _workerCount),
            generatorDiagnostics);
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

            task.MarkDequeued();
            PathfindingProfiler.Measurement backgroundMeasurement =
                PathfindingProfiler.Start(task.BenchmarkSession);
            RunGenerator(task);
            backgroundMeasurement.Complete(PathfindingBenchmarkMetric.BackgroundPath);
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

    private bool TryReuseActiveRequestReadOnly(
        Actor actor,
        WorldTile target,
        bool pathOnWater,
        bool walkOnBlocks,
        bool walkOnLava,
        int limitRegions)
    {
        if (actor?.data == null || target == null)
        {
            return false;
        }

        if (!_tasks.TryGetValue(
                actor.data.id,
                out PathfindingTask task) ||
            !task.Request.HasSameTargetAndOptions(
                target,
                pathOnWater,
                walkOnBlocks,
                walkOnLava,
                limitRegions))
        {
            return false;
        }

        PathRequestState state = task.Stream.State;
        return state == PathRequestState.Pending ||
               state == PathRequestState.Streaming ||
               task.Stream.HasPendingSteps;
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

        PathfindingProfiler.Measurement createMeasurement = PathfindingProfiler.Start();
        var request = new PathRequest(actor, target, true, true, true, 0);
        createMeasurement.Complete(PathfindingBenchmarkMetric.Create);
        PathfindingProfiler.Measurement taskCreateMeasurement = PathfindingProfiler.Start();
        var task = new PathfindingTask(request, taskCreateMeasurement.Session);
        task.Stream.AddStep(new PathStep(target, MovementMethod.Walk, TraversalEstimate.Direct));
        task.Stream.Complete();
        task.MarkWorkerFinished();
        _tasks[actor.data.id] = task;
        taskCreateMeasurement.Complete(PathfindingBenchmarkMetric.TaskCreate);
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

        if (task.WaitingInitialized &&
            !task.HasWorkerUpdate)
        {
            cursor = new ReadyPathCursor(
                this,
                actorId,
                task,
                initializeWaiting: false);
            return PathPollResult.Waiting();
        }

        task.ClearWorkerUpdate();
        var result = GetPollResult(actorId, task);
        if (result.Kind == PathPollKind.StepReady)
        {
            task.ResetWaitingInitialized();
            cursor = new ReadyPathCursor(
                this,
                actorId,
                task,
                initializeWaiting: false);
        }
        else if (result.Kind == PathPollKind.Waiting)
        {
            cursor = new ReadyPathCursor(
                this,
                actorId,
                task,
                task.MarkWaitingInitialized());
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
        private readonly bool _initializeWaiting;

        internal ReadyPathCursor(
            PathFinder owner,
            long actorId,
            PathfindingTask task,
            bool initializeWaiting)
        {
            _owner = owner;
            _actorId = actorId;
            _task = task;
            _initializeWaiting = initializeWaiting;
        }

        public bool IsValid => _owner != null && _task != null;
        internal bool InitializeWaiting =>
            _initializeWaiting;

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

        PathfindingProfiler.Measurement cancelMeasurement = PathfindingProfiler.Start();
        long actorId = actor.data.id;
        bool cancelled =
            _tasks.TryGetValue(actorId, out PathfindingTask task) &&
            _tasks.Remove(actorId);
        if (cancelled)
        {
            task.Stream.Cancel(reason);
            task.Cancellation.Cancel();
            task.DisposeWhenWorkerFinished();
        }

        cancelMeasurement.Complete(
            cancelled
                ? PathfindingBenchmarkMetric.Cancel
                : PathfindingBenchmarkMetric.CancelEmpty);
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
        if (_tasks.TryGetValue(
                actorId,
                out PathfindingTask task) &&
            _tasks.Remove(actorId))
        {
            task.DisposeWhenWorkerFinished();
        }
        _lastRequests.Remove(actorId);
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

        PathfindingProfiler.Measurement createMeasurement = PathfindingProfiler.Start();
        var request = new PathRequest(
            actor,
            target,
            opt.PathOnWater,
            opt.WalkOnBlocks,
            opt.WalkOnLava,
            opt.RegionLimit);
        createMeasurement.Complete(PathfindingBenchmarkMetric.Create);
        return RequestPath(request);
    }
}

internal sealed class PathfindingTask : IDisposable
{
    private int _disposeRequested;
    private int _disposed;
    private int _workerFinished;
    private int _workerUpdate;
    private int _waitingInitialized;

    private long enqueuedAt;

    public PathfindingTask(
        PathRequest request,
        PathfindingProfiler.Session benchmarkSession = null)
    {
        Request = request;
        Stream = new PathStream(SignalWorkerUpdate);
        Cancellation = new CancellationTokenSource();
        BenchmarkSession = benchmarkSession;
    }

    public PathRequest Request { get; }
    public PathStream Stream { get; }
    public CancellationTokenSource Cancellation { get; }
    internal PathfindingProfiler.Session BenchmarkSession { get; }
    internal bool HasWorkerUpdate =>
        Volatile.Read(ref _workerUpdate) != 0;
    internal bool WaitingInitialized =>
        Volatile.Read(ref _waitingInitialized) != 0;

    internal void ClearWorkerUpdate()
    {
        Interlocked.Exchange(ref _workerUpdate, 0);
    }

    internal bool MarkWaitingInitialized()
    {
        return Interlocked.Exchange(
                   ref _waitingInitialized,
                   1) == 0;
    }

    internal void ResetWaitingInitialized()
    {
        Volatile.Write(ref _waitingInitialized, 0);
    }

    private void SignalWorkerUpdate()
    {
        Volatile.Write(ref _workerUpdate, 1);
    }

    internal void MarkEnqueued()
    {
        enqueuedAt = PathfindingProfiler.MarkEnqueued(BenchmarkSession);
    }

    internal void MarkDequeued()
    {
        PathfindingProfiler.RecordQueueWait(BenchmarkSession, enqueuedAt);
    }

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

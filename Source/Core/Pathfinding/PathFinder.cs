using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cultiway.Const;
using Cultiway.Core.Performance;
using Cultiway.Utils;

namespace Cultiway.Core.Pathfinding;

/// <summary>
/// 以角色为键维护长期寻路会话，并由常驻工作线程按优先级生成局部分段。
/// </summary>
public class PathFinder
{
    public static PathFinder Instance { get; } = new();
    internal static readonly object ActorSyncLock = new();

    private readonly ConcurrentDictionary<long, PathSession> sessions = new();
    private readonly ConcurrentDictionary<long, PathRequestOptions> lastRequests = new();
    private readonly ConcurrentQueue<ScheduledPathWork> starvedQueue = new();
    private readonly ConcurrentQueue<ScheduledPathWork> initialQueue = new();
    private readonly ConcurrentQueue<ScheduledPathWork> continuationQueue = new();
    private readonly ConcurrentQueue<RetryTicket> pendingRetries = new();
    private readonly List<ScheduledRetry> scheduledRetries = new();
    private readonly SemaphoreSlim pendingSignal = new(0);
    private readonly object workerLock = new();
    private readonly List<Thread> workers = new();
    private IPathGenerator generator;
    private bool workersStarted;
    private volatile bool shuttingDown;
    private int queueDepth;
    private int queueHighWatermark;
    private int activeWorkers;
    private long replacedRequests;
    private long staleWorkItems;
    private long generatedSegments;
    private long straightSegments;
    private long corridorSegments;
    private long portalSegments;
    private long expandedNodes;
    private long firstStepTicks;
    private long firstStepCount;

    public PathFinder()
    {
        generator = new PortalAwarePathGenerator(PortalRegistry.Instance, PathfindingConfig.Default);
    }

    public void UseGenerator(IPathGenerator pathGenerator)
    {
        generator = pathGenerator ?? new PortalAwarePathGenerator(PortalRegistry.Instance, PathfindingConfig.Default);
    }

    /// <summary>在 Mod 初始化时创建固定数量的常驻寻路线程。</summary>
    public void Initialize()
    {
        EnsureWorkersStarted();
    }

    /// <summary>仅在应用退出时停止工作线程。</summary>
    public void Shutdown()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        Clear();
        int count;
        lock (workerLock)
        {
            count = workers.Count;
        }

        if (count > 0) pendingSignal.Release(count);
        lock (workerLock)
        {
            for (int i = 0; i < workers.Count; i++)
            {
                Thread worker = workers[i];
                if (worker?.IsAlive == true) worker.Join(500);
            }
        }
    }

    public bool RequestPath(Actor actor, WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int limitRegions)
    {
        return RequestPathDetailed(actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions).Accepted;
    }

    public PathSubmissionResult RequestPathDetailed(Actor actor, WorldTile target, bool pathOnWater,
        bool walkOnBlocks, bool walkOnLava, int limitRegions)
    {
        if (!CanAcceptRequest(actor, target, out PathFailureReason failureReason))
        {
            return new PathSubmissionResult(PathSubmissionKind.Rejected, failureReason);
        }

        PathfindingProfiler.Measurement reuseMeasurement = PathfindingProfiler.Start();
        if (TryReuseActiveRequest(actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions))
        {
            reuseMeasurement.Complete(PathfindingBenchmarkMetric.Reuse);
            return new PathSubmissionResult(PathSubmissionKind.Reused);
        }

        reuseMeasurement.Complete(PathfindingBenchmarkMetric.ReuseMiss);
        PathfindingProfiler.Measurement createMeasurement = PathfindingProfiler.Start();
        var request = new PathRequest(actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions);
        createMeasurement.Complete(PathfindingBenchmarkMetric.Create);
        return Submit(request);
    }

    public bool RequestPath(PathRequest request)
    {
        if (request?.Actor == null || request.Target == null)
        {
            return false;
        }

        return RequestPathDetailed(request.Actor, request.Target, request.PathOnWater, request.WalkOnBlocks,
            request.WalkOnLava, request.RegionLimit).Accepted;
    }

    private PathSubmissionResult Submit(PathRequest request)
    {
        long actorId = request.ActorId;
        if (actorId == 0)
        {
            return new PathSubmissionResult(PathSubmissionKind.Rejected, PathFailureReason.InvalidActor);
        }

        lastRequests[actorId] = new PathRequestOptions(request.Target, request.PathOnWater, request.WalkOnBlocks,
            request.WalkOnLava, request.RegionLimit);
        while (sessions.TryGetValue(actorId, out PathSession existing))
        {
            if (!existing.TryReplace(request))
            {
                ((ICollection<KeyValuePair<long, PathSession>>)sessions)
                    .Remove(new KeyValuePair<long, PathSession>(actorId, existing));
                continue;
            }

            Interlocked.Increment(ref replacedRequests);
            Schedule(existing, PathWorkPriority.Initial);
            return new PathSubmissionResult(PathSubmissionKind.Replaced);
        }

        PathfindingProfiler.Measurement taskCreateMeasurement = PathfindingProfiler.Start();
        var session = new PathSession(request, taskCreateMeasurement.Session);
        if (!sessions.TryAdd(actorId, session))
        {
            session.Cancel(PathFailureReason.CancelledByNewRequest);
            return Submit(request);
        }

        taskCreateMeasurement.Complete(PathfindingBenchmarkMetric.TaskCreate);
        Schedule(session, PathWorkPriority.Initial);
        return new PathSubmissionResult(PathSubmissionKind.Created);
    }

    private bool TryReuseActiveRequest(Actor actor, WorldTile target, bool pathOnWater, bool walkOnBlocks,
        bool walkOnLava, int limitRegions)
    {
        if (actor?.data == null || target == null ||
            !sessions.TryGetValue(actor.data.id, out PathSession session))
        {
            return false;
        }

        return session.CanReuse(target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions);
    }

    private void Schedule(PathSession session, PathWorkPriority priority)
    {
        if (session == null || shuttingDown) return;
        EnsureWorkersStarted();
        if (!session.TrySchedule(priority, out ScheduledPathWork work)) return;
        PathfindingProfiler.Measurement enqueueMeasurement = PathfindingProfiler.Start(session.BenchmarkSession);
        work = work.WithEnqueuedAt(PathfindingProfiler.MarkEnqueued(session.BenchmarkSession));
        QueueFor(priority).Enqueue(work);
        int depth = Interlocked.Increment(ref queueDepth);
        UpdateHighWatermark(depth);
        pendingSignal.Release();
        enqueueMeasurement.Complete(PathfindingBenchmarkMetric.Enqueue);
    }

    private ConcurrentQueue<ScheduledPathWork> QueueFor(PathWorkPriority priority)
    {
        return priority switch
        {
            PathWorkPriority.Starved => starvedQueue,
            PathWorkPriority.Initial => initialQueue,
            _ => continuationQueue
        };
    }

    private void EnsureWorkersStarted()
    {
        if (workersStarted || shuttingDown) return;
        lock (workerLock)
        {
            if (workersStarted || shuttingDown) return;
            int workerCount = PerformanceSettings.PathfindingWorkerCount;
            for (int i = 0; i < workerCount; i++)
            {
                var worker = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = $"CultiwayPathFinder-{i}",
                    Priority = ThreadPriority.Normal
                };
                workers.Add(worker);
                worker.Start();
            }

            workersStarted = true;
        }
    }

    private void WorkerLoop()
    {
        while (true)
        {
            pendingSignal.Wait();
            if (shuttingDown) return;
            if (!TryDequeue(out ScheduledPathWork work)) continue;
            RecordDequeued();
            if (!work.Session.TryBeginWork(work.QueueVersion, out PathWorkContext context))
            {
                Interlocked.Increment(ref staleWorkItems);
                continue;
            }

            PathfindingProfiler.RecordQueueWait(work.Session.BenchmarkSession, work.EnqueuedAt);
            Interlocked.Increment(ref activeWorkers);
            PathGenerationResult result;
            PathfindingProfiler.Measurement backgroundMeasurement =
                PathfindingProfiler.Start(work.Session.BenchmarkSession);
            try
            {
                result = generator.GenerateSegment(context.Request, context.Cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                result = PathGenerationResult.Fail(PathFailureReason.CancelledByNewRequest);
            }
            catch (Exception e)
            {
                result = PathGenerationResult.Fail(PathFailureReason.GeneratorException, e);
                ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
            }
            finally
            {
                backgroundMeasurement.Complete(PathfindingBenchmarkMetric.BackgroundPath);
                Interlocked.Decrement(ref activeWorkers);
            }

            CompleteWork(work.Session, context, result);
        }
    }

    private bool TryDequeue(out ScheduledPathWork work)
    {
        return starvedQueue.TryDequeue(out work) || initialQueue.TryDequeue(out work) ||
               continuationQueue.TryDequeue(out work);
    }

    private void CompleteWork(PathSession session, PathWorkContext context, PathGenerationResult result)
    {
        PathSessionCompletion completion = session.CompleteWork(context, result,
            PathfindingConfig.Default.SegmentLowWatermark);
        if (completion.Stale)
        {
            Interlocked.Increment(ref staleWorkItems);
        }

        if (completion.Retry.HasValue)
        {
            pendingRetries.Enqueue(completion.Retry.Value);
        }

        if (completion.Schedule)
        {
            Schedule(session, completion.Priority);
        }

        if (!completion.Stale)
        {
            Interlocked.Add(ref expandedNodes, result.ExpandedNodes);
        }

        if (!completion.AcceptedResult) return;
        Interlocked.Increment(ref generatedSegments);
        switch (result.Kind)
        {
            case PathGenerationKind.StraightLine:
                Interlocked.Increment(ref straightSegments);
                break;
            case PathGenerationKind.RegionCorridor:
                Interlocked.Increment(ref corridorSegments);
                break;
            case PathGenerationKind.Portal:
                Interlocked.Increment(ref portalSegments);
                break;
        }

        if (completion.FirstStepElapsedTicks > 0)
        {
            Interlocked.Add(ref firstStepTicks, completion.FirstStepElapsedTicks);
            Interlocked.Increment(ref firstStepCount);
        }
    }

    /// <summary>
    /// 在主线程发布地形更新并激活到期的重试。由 MapBox 模拟循环每 tick 调用一次。
    /// </summary>
    public void Tick()
    {
        if (shuttingDown || World.world?.tiles_list == null) return;
        PathNavigationGridService.FlushDirty();
        if (!SimulationTime.IsBound) return;
        double now = SimulationTime.Now;
        while (pendingRetries.TryDequeue(out RetryTicket ticket))
        {
            if (!ticket.Session.TryArmRetry(ticket.Version)) continue;
            scheduledRetries.Add(new ScheduledRetry(ticket.Session, ticket.Version, now + ticket.DelaySeconds));
        }

        for (int i = scheduledRetries.Count - 1; i >= 0; i--)
        {
            ScheduledRetry retry = scheduledRetries[i];
            if (now < retry.DueTime) continue;
            scheduledRetries.RemoveAt(i);
            ActivateRetry(retry);
        }
    }

    private void ActivateRetry(ScheduledRetry retry)
    {
        if (!sessions.TryGetValue(retry.Session.ActorId, out PathSession current) ||
            !ReferenceEquals(current, retry.Session))
        {
            return;
        }

        if (!retry.Session.TryGetRetryRequest(retry.Version, out Actor actor, out PathRequestOptions options))
        {
            return;
        }

        WorldTile target = actor?.tile_target ?? options.Target;
        if (!CanAcceptRequest(actor, target, out PathFailureReason failureReason))
        {
            retry.Session.FailRetry(retry.Version, failureReason);
            return;
        }

        var request = new PathRequest(actor, target, options.PathOnWater, options.WalkOnBlocks,
            options.WalkOnLava, options.RegionLimit);
        if (retry.Session.ActivateRetry(retry.Version, request))
        {
            Schedule(retry.Session, PathWorkPriority.Starved);
        }
    }

    /// <summary>
    /// 将路径执行阶段发现的阻塞并入当前会话重试，不让角色掉出 isUsingPath 轮询。
    /// </summary>
    public bool ScheduleRecovery(Actor actor, PathFailureReason reason)
    {
        if (actor?.data == null || !sessions.TryGetValue(actor.data.id, out PathSession session))
        {
            return false;
        }

        return ScheduleRecovery(actor.data.id, session, session.CurrentStream, actor, reason);
    }

    private bool ScheduleRecovery(long actorId, PathSession session, PathStream stream, Actor actor,
        PathFailureReason reason)
    {
        if (!IsCurrent(actorId, session)) return false;
        int startTileId = TileTraversalInfo.TileIdOf(actor?.current_tile);
        if (!session.PrepareExternalRetry(stream, reason, startTileId, out RetryTicket retry)) return false;
        pendingRetries.Enqueue(retry);
        return true;
    }

    public bool CanAcceptRequest(Actor actor, WorldTile target, out PathFailureReason failureReason)
    {
        if (actor?.data == null || actor.asset == null)
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
        if (!CanAcceptRequest(actor, target, out _)) return;
        long actorId = actor.data.id;
        lastRequests[actorId] = new PathRequestOptions(target, true, true, true, 0);
        var request = new PathRequest(actor, target, true, true, true, 0);
        var direct = PathSession.CreateDirect(request,
            new PathStep(target, MovementMethod.Walk, TraversalEstimate.Direct));
        if (sessions.TryGetValue(actorId, out PathSession old)) old.Cancel(PathFailureReason.CancelledByNewRequest);
        sessions[actorId] = direct;
    }

    public bool IsActorPathing(Actor actor)
    {
        return actor?.data != null && sessions.TryGetValue(actor.data.id, out PathSession session) &&
               session.IsVisibleToPoller;
    }

    public List<PathStep> TryViewAll(Actor actor)
    {
        if (actor?.data == null || !sessions.TryGetValue(actor.data.id, out PathSession session)) return null;
        return session.CurrentStream.TryViewAll();
    }

    public PathPollResult PollStep(Actor actor)
    {
        if (actor?.data == null) return PathPollResult.Failed(PathFailureReason.InvalidActor);
        if (!sessions.TryGetValue(actor.data.id, out PathSession session)) return PathPollResult.NoRequest();
        PathStream stream = session.CurrentStream;
        PathPollResult result = GetPollResult(actor.data.id, session, stream);
        if (IsTerminal(result.Kind)) CleanupSession(actor.data.id, session, stream);
        return result;
    }

    public PathPollResult PeekReadyStep(Actor actor, out ReadyPathStep readyStep)
    {
        readyStep = default;
        if (actor?.data == null) return PathPollResult.Failed(PathFailureReason.InvalidActor);
        if (!sessions.TryGetValue(actor.data.id, out PathSession session)) return PathPollResult.NoRequest();
        PathStream stream = session.CurrentStream;
        PathPollResult result = GetPollResult(actor.data.id, session, stream);
        if (result.Kind == PathPollKind.StepReady)
        {
            readyStep = new ReadyPathStep(this, actor.data.id, session, stream, result.Step);
        }
        else if (IsTerminal(result.Kind))
        {
            CleanupSession(actor.data.id, session, stream);
        }

        return result;
    }

    public PathPollResult OpenReadyCursor(Actor actor, out ReadyPathCursor cursor)
    {
        cursor = default;
        if (actor?.data == null) return PathPollResult.Failed(PathFailureReason.InvalidActor);
        long actorId = actor.data.id;
        if (!sessions.TryGetValue(actorId, out PathSession session)) return PathPollResult.NoRequest();
        PathStream stream = session.CurrentStream;
        PathPollResult result = GetPollResult(actorId, session, stream);
        cursor = new ReadyPathCursor(this, actorId, session, stream);

        return result;
    }

    public bool TryPeekStep(Actor actor, out PathStep step, out bool finished)
    {
        finished = false;
        step = default;
        PathPollResult result = PollStep(actor);
        if (result.Kind == PathPollKind.StepReady)
        {
            step = result.Step;
            return true;
        }

        finished = result.Kind != PathPollKind.Waiting;
        return false;
    }

    private PathPollResult GetPollResult(long actorId, PathSession session, PathStream stream)
    {
        if (!IsCurrent(actorId, session)) return PathPollResult.NoRequest();
        if (!session.IsCurrentStream(stream)) return PathPollResult.Waiting();
        if (stream.TryPeek(out PathStep step)) return PathPollResult.StepReady(step);
        PathSessionState state = session.State;
        if (state is PathSessionState.Queued or PathSessionState.Searching or PathSessionState.Streaming or
            PathSessionState.RetryDelay)
        {
            return PathPollResult.Waiting();
        }

        if (state == PathSessionState.Completed || stream.State == PathRequestState.Succeeded)
        {
            return PathPollResult.Completed();
        }

        if (state == PathSessionState.Failed || stream.State == PathRequestState.Failed)
        {
            PathFailureReason reason = stream.FailureReason == PathFailureReason.None
                ? PathFailureReason.GeneratorException
                : stream.FailureReason;
            return PathPollResult.Failed(reason, stream.Error);
        }

        PathFailureReason cancelReason = stream.FailureReason == PathFailureReason.None
            ? PathFailureReason.CancelledByNewRequest
            : stream.FailureReason;
        return PathPollResult.Cancelled(cancelReason);
    }

    public bool Acknowledge(Actor actor)
    {
        if (actor?.data == null || !sessions.TryGetValue(actor.data.id, out PathSession session)) return false;
        return CleanupSession(actor.data.id, session, session.CurrentStream);
    }

    public void ConsumeStep(Actor actor)
    {
        if (actor?.data == null || !sessions.TryGetValue(actor.data.id, out PathSession session)) return;
        Consume(actor.data.id, session, session.CurrentStream);
    }

    private void Consume(long actorId, PathSession session, PathStream stream)
    {
        if (!IsCurrent(actorId, session)) return;
        PathConsumeResult result = session.TryConsume(stream, PathfindingConfig.Default.SegmentLowWatermark);
        if (result.ScheduleContinuation)
        {
            Schedule(session, result.Starved ? PathWorkPriority.Starved : PathWorkPriority.Continuation);
        }

        if (result.Finished) CleanupSession(actorId, session, stream);
    }

    public void Cancel(Actor actor, PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        if (actor?.data == null) return;
        PathfindingProfiler.Measurement measurement = PathfindingProfiler.Start();
        bool removed = sessions.TryRemove(actor.data.id, out PathSession session);
        if (removed) session.Cancel(reason);
        measurement.Complete(removed ? PathfindingBenchmarkMetric.Cancel : PathfindingBenchmarkMetric.CancelEmpty);
    }

    private bool CleanupSession(long actorId, PathSession session, PathStream stream)
    {
        if (!IsCurrent(actorId, session) || !session.TryDetach(stream))
        {
            return false;
        }

        bool removed = ((ICollection<KeyValuePair<long, PathSession>>)sessions)
            .Remove(new KeyValuePair<long, PathSession>(actorId, session));
        session.DisposeCompleted();
        return removed;
    }

    public void Cleanup(long actorId)
    {
        if (sessions.TryRemove(actorId, out PathSession session))
        {
            session.Cancel(PathFailureReason.ActorDead);
        }

        lastRequests.TryRemove(actorId, out _);
    }

    public void Clear()
    {
        foreach (KeyValuePair<long, PathSession> pair in sessions)
        {
            pair.Value.Cancel(PathFailureReason.ClearWorld);
        }

        sessions.Clear();
        lastRequests.Clear();
        while (pendingRetries.TryDequeue(out _)) { }
        scheduledRetries.Clear();
        Drain(starvedQueue);
        Drain(initialQueue);
        Drain(continuationQueue);
        while (pendingSignal.Wait(0)) { }
        PathNavigationGridService.Clear();
    }

    internal bool TryGetLastRequestOptions(Actor actor, out PathRequestOptions options)
    {
        options = default;
        return actor?.data != null && lastRequests.TryGetValue(actor.data.id, out options);
    }

    internal bool TryRequestRecover(Actor actor, WorldTile overrideTarget = null)
    {
        if (actor?.data == null || !TryGetLastRequestOptions(actor, out PathRequestOptions options)) return false;
        WorldTile target = overrideTarget ?? actor.tile_target ?? options.Target;
        if (!CanAcceptRequest(actor, target, out _)) return false;
        var request = new PathRequest(actor, target, options.PathOnWater, options.WalkOnBlocks,
            options.WalkOnLava, options.RegionLimit);
        return Submit(request).Accepted;
    }

    public string GetDiagnostics()
    {
        long firstCount = Interlocked.Read(ref firstStepCount);
        double firstMilliseconds = firstCount == 0
            ? 0d
            : Interlocked.Read(ref firstStepTicks) * 1000d / Stopwatch.Frequency / firstCount;
        var builder = new StringBuilder(256);
        builder.Append("sessions=").Append(sessions.Count)
            .Append(" queue=").Append(Math.Max(0, Volatile.Read(ref queueDepth)))
            .Append(" high=").Append(Volatile.Read(ref queueHighWatermark))
            .Append(" active=").Append(Volatile.Read(ref activeWorkers))
            .Append(" replaced=").Append(Interlocked.Read(ref replacedRequests))
            .Append(" stale=").Append(Interlocked.Read(ref staleWorkItems))
            .Append(" segments=").Append(Interlocked.Read(ref generatedSegments))
            .Append(" straight=").Append(Interlocked.Read(ref straightSegments))
            .Append(" corridor=").Append(Interlocked.Read(ref corridorSegments))
            .Append(" portal=").Append(Interlocked.Read(ref portalSegments))
            .Append(" expanded=").Append(Interlocked.Read(ref expandedNodes))
            .Append(" first_ms=").Append(firstMilliseconds.ToString("0.00"));
        return builder.ToString();
    }

    private bool IsCurrent(long actorId, PathSession session)
    {
        return sessions.TryGetValue(actorId, out PathSession current) && ReferenceEquals(current, session);
    }

    private static bool IsTerminal(PathPollKind kind)
    {
        return kind is PathPollKind.Completed or PathPollKind.Failed or PathPollKind.Cancelled;
    }

    private void Drain(ConcurrentQueue<ScheduledPathWork> queue)
    {
        while (queue.TryDequeue(out _)) RecordDequeued();
    }

    private void RecordDequeued()
    {
        int value = Interlocked.Decrement(ref queueDepth);
        if (value < 0) Interlocked.Exchange(ref queueDepth, 0);
    }

    private void UpdateHighWatermark(int value)
    {
        int observed = Volatile.Read(ref queueHighWatermark);
        while (value > observed)
        {
            int previous = Interlocked.CompareExchange(ref queueHighWatermark, value, observed);
            if (previous == observed) return;
            observed = previous;
        }
    }

    public readonly struct ReadyPathStep
    {
        private readonly PathFinder owner;
        private readonly long actorId;
        private readonly PathSession session;
        private readonly PathStream stream;

        internal ReadyPathStep(PathFinder owner, long actorId, PathSession session, PathStream stream, PathStep step)
        {
            this.owner = owner;
            this.actorId = actorId;
            this.session = session;
            this.stream = stream;
            Step = step;
        }

        public PathStep Step { get; }
        public bool IsValid => owner != null && session != null && stream != null;
        public void Consume()
        {
            if (IsValid) owner.Consume(actorId, session, stream);
        }
    }

    public readonly struct ReadyPathCursor
    {
        private readonly PathFinder owner;
        private readonly long actorId;
        private readonly PathSession session;
        private readonly PathStream stream;

        internal ReadyPathCursor(PathFinder owner, long actorId, PathSession session, PathStream stream)
        {
            this.owner = owner;
            this.actorId = actorId;
            this.session = session;
            this.stream = stream;
        }

        public bool IsValid => owner != null && session != null && stream != null;
        public PathPollResult Poll()
        {
            return IsValid ? owner.GetPollResult(actorId, session, stream) : PathPollResult.NoRequest();
        }

        public void Consume()
        {
            if (IsValid) owner.Consume(actorId, session, stream);
        }

        public bool Acknowledge()
        {
            return IsValid && owner.CleanupSession(actorId, session, stream);
        }

        public bool ScheduleRecovery(Actor actor, PathFailureReason reason)
        {
            return IsValid && owner.ScheduleRecovery(actorId, session, stream, actor, reason);
        }
    }
}

internal enum PathWorkPriority
{
    Starved,
    Initial,
    Continuation
}

internal readonly struct ScheduledPathWork
{
    internal ScheduledPathWork(PathSession session, int queueVersion, long enqueuedAt = 0)
    {
        Session = session;
        QueueVersion = queueVersion;
        EnqueuedAt = enqueuedAt;
    }

    internal PathSession Session { get; }
    internal int QueueVersion { get; }
    internal long EnqueuedAt { get; }
    internal ScheduledPathWork WithEnqueuedAt(long value)
    {
        return new ScheduledPathWork(Session, QueueVersion, value);
    }
}

internal readonly struct PathWorkContext
{
    internal PathWorkContext(int requestGeneration, PathRequest request, CancellationTokenSource cancellation)
    {
        RequestGeneration = requestGeneration;
        Request = request;
        Cancellation = cancellation;
    }

    internal int RequestGeneration { get; }
    internal PathRequest Request { get; }
    internal CancellationTokenSource Cancellation { get; }
}

internal readonly struct PathSessionCompletion
{
    internal PathSessionCompletion(bool acceptedResult, bool stale, bool schedule, PathWorkPriority priority,
        RetryTicket? retry, long firstStepElapsedTicks)
    {
        AcceptedResult = acceptedResult;
        Stale = stale;
        Schedule = schedule;
        Priority = priority;
        Retry = retry;
        FirstStepElapsedTicks = firstStepElapsedTicks;
    }

    internal bool AcceptedResult { get; }
    internal bool Stale { get; }
    internal bool Schedule { get; }
    internal PathWorkPriority Priority { get; }
    internal RetryTicket? Retry { get; }
    internal long FirstStepElapsedTicks { get; }
}

internal readonly struct PathConsumeResult
{
    internal PathConsumeResult(bool scheduleContinuation, bool starved, bool finished)
    {
        ScheduleContinuation = scheduleContinuation;
        Starved = starved;
        Finished = finished;
    }

    internal bool ScheduleContinuation { get; }
    internal bool Starved { get; }
    internal bool Finished { get; }
}

internal readonly struct RetryTicket
{
    internal RetryTicket(PathSession session, int version, double delaySeconds)
    {
        Session = session;
        Version = version;
        DelaySeconds = delaySeconds;
    }

    internal PathSession Session { get; }
    internal int Version { get; }
    internal double DelaySeconds { get; }
}

internal readonly struct ScheduledRetry
{
    internal ScheduledRetry(PathSession session, int version, double dueTime)
    {
        Session = session;
        Version = version;
        DueTime = dueTime;
    }

    internal PathSession Session { get; }
    internal int Version { get; }
    internal double DueTime { get; }
}

internal sealed class PathSession
{
    private readonly object syncRoot = new();
    private long requestStartedAt = Stopwatch.GetTimestamp();
    private PathRequest request;
    private PathStream stream;
    private PathSessionState state;
    private int requestGeneration = 1;
    private int continuationStartTileId;
    private float continuationStamina;
    private float continuationHealth;
    private bool hasMoreSegments = true;
    private bool queued;
    private bool running;
    private bool cancelled;
    private int queueVersion;
    private PathWorkPriority queuedPriority;
    private bool rescheduleRequested;
    private PathWorkPriority requestedPriority = PathWorkPriority.Continuation;
    private CancellationTokenSource requestCancellation;
    private CancellationTokenSource activeCancellation;
    private int retryCount;
    private int retryVersion;
    private PathFailureReason lastFailure;
    private bool firstStepPublished;

    internal PathSession(PathRequest request, PathfindingProfiler.Session benchmarkSession = null)
    {
        this.request = request;
        ActorId = request.ActorId;
        stream = new PathStream();
        state = PathSessionState.Queued;
        continuationStartTileId = request.StartTileId;
        continuationStamina = request.ActorCurrentStamina;
        continuationHealth = request.ActorCurrentHealth;
        requestCancellation = new CancellationTokenSource();
        BenchmarkSession = benchmarkSession;
    }

    internal long ActorId { get; }
    internal PathfindingProfiler.Session BenchmarkSession { get; }
    internal PathStream CurrentStream
    {
        get
        {
            lock (syncRoot) return stream;
        }
    }

    internal PathSessionState State
    {
        get
        {
            lock (syncRoot) return state;
        }
    }

    internal bool IsVisibleToPoller
    {
        get
        {
            lock (syncRoot) return !cancelled;
        }
    }

    internal static PathSession CreateDirect(PathRequest request, PathStep step)
    {
        var session = new PathSession(request);
        lock (session.syncRoot)
        {
            session.stream.AddStep(step);
            session.stream.Complete();
            session.state = PathSessionState.Completed;
            session.hasMoreSegments = false;
            session.firstStepPublished = true;
            session.DisposeCurrentCancellationLocked();
        }

        return session;
    }

    internal bool CanReuse(WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava, int regionLimit)
    {
        lock (syncRoot)
        {
            if (cancelled || state is PathSessionState.Failed or PathSessionState.Cancelled) return false;
            return request.HasSameTargetAndOptions(target, pathOnWater, walkOnBlocks, walkOnLava, regionLimit) &&
                   (state != PathSessionState.Completed || stream.HasPendingSteps);
        }
    }

    internal bool TryReplace(PathRequest replacement)
    {
        lock (syncRoot)
        {
            if (cancelled) return false;
            requestGeneration++;
            RenewCancellationLocked();
            stream.Cancel(PathFailureReason.CancelledByNewRequest);
            stream = new PathStream();
            request = replacement;
            requestStartedAt = Stopwatch.GetTimestamp();
            firstStepPublished = false;
            continuationStartTileId = replacement.StartTileId;
            continuationStamina = replacement.ActorCurrentStamina;
            continuationHealth = replacement.ActorCurrentHealth;
            hasMoreSegments = true;
            state = PathSessionState.Queued;
            retryCount = 0;
            retryVersion++;
            lastFailure = PathFailureReason.None;
            if (running)
            {
                rescheduleRequested = true;
                requestedPriority = PathWorkPriority.Initial;
            }

            return true;
        }
    }

    internal bool TrySchedule(PathWorkPriority priority, out ScheduledPathWork work)
    {
        lock (syncRoot)
        {
            work = default;
            if (cancelled || state is PathSessionState.Completed or PathSessionState.Failed or
                PathSessionState.Cancelled or PathSessionState.RetryDelay)
            {
                return false;
            }

            if (running)
            {
                rescheduleRequested = true;
                if (priority < requestedPriority) requestedPriority = priority;
                return false;
            }

            if (queued)
            {
                if (priority >= queuedPriority) return false;
                queuedPriority = priority;
                queueVersion++;
                work = new ScheduledPathWork(this, queueVersion);
                return true;
            }

            queued = true;
            queuedPriority = priority;
            queueVersion++;
            state = PathSessionState.Queued;
            work = new ScheduledPathWork(this, queueVersion);
            return true;
        }
    }

    internal bool TryBeginWork(int expectedQueueVersion, out PathWorkContext context)
    {
        lock (syncRoot)
        {
            context = default;
            if (cancelled || running || !queued || queueVersion != expectedQueueVersion) return false;
            queued = false;
            running = true;
            rescheduleRequested = false;
            requestedPriority = PathWorkPriority.Continuation;
            state = PathSessionState.Searching;
            requestCancellation ??= new CancellationTokenSource();
            activeCancellation = requestCancellation;
            PathNavigationGrid grid = PathNavigationGridService.Current;
            PathRequest segmentRequest = request.WithStart(continuationStartTileId, grid,
                continuationStamina, continuationHealth);
            context = new PathWorkContext(requestGeneration, segmentRequest, activeCancellation);
            return true;
        }
    }

    internal PathSessionCompletion CompleteWork(PathWorkContext context, PathGenerationResult result,
        int lowWatermark)
    {
        lock (syncRoot)
        {
            if (ReferenceEquals(activeCancellation, context.Cancellation))
            {
                activeCancellation = null;
            }

            running = false;
            if (!ReferenceEquals(requestCancellation, context.Cancellation) || cancelled)
            {
                context.Cancellation.Dispose();
                if (ReferenceEquals(requestCancellation, context.Cancellation)) requestCancellation = null;
            }

            if (cancelled)
            {
                return new PathSessionCompletion(false, true, false, default, null, 0);
            }

            if (context.RequestGeneration != requestGeneration)
            {
                bool scheduleReplacement = rescheduleRequested || state == PathSessionState.Queued;
                PathWorkPriority priority = requestedPriority;
                rescheduleRequested = false;
                return new PathSessionCompletion(false, true, scheduleReplacement, priority, null, 0);
            }

            rescheduleRequested = false;
            if (!result.IsSuccess)
            {
                if (TryPrepareRetry(result.FailureReason, out RetryTicket retry))
                {
                    return new PathSessionCompletion(false, false, false, default, retry, 0);
                }

                stream.Fail(result.FailureReason, result.Error);
                state = PathSessionState.Failed;
                return new PathSessionCompletion(false, false, false, default, null, 0);
            }

            long firstStepElapsed = 0;
            for (int i = 0; i < result.Steps.Count; i++)
            {
                stream.AddStep(result.Steps[i]);
            }

            if (!firstStepPublished && result.Steps.Count > 0)
            {
                firstStepPublished = true;
                firstStepElapsed = Math.Max(0L, Stopwatch.GetTimestamp() - requestStartedAt);
            }

            retryCount = 0;
            lastFailure = PathFailureReason.None;
            continuationStamina = float.IsNaN(result.EndStamina)
                ? context.Request.ActorCurrentStamina
                : result.EndStamina;
            continuationHealth = float.IsNaN(result.EndHealth)
                ? context.Request.ActorCurrentHealth
                : result.EndHealth;
            if (result.ReachedTarget)
            {
                hasMoreSegments = false;
                continuationStartTileId = request.TargetTileId;
                stream.Complete();
                state = PathSessionState.Completed;
                return new PathSessionCompletion(true, false, false, default, null, firstStepElapsed);
            }

            if (result.EndTileId < 0 || result.Steps.Count == 0)
            {
                stream.Fail(PathFailureReason.Unreachable);
                state = PathSessionState.Failed;
                return new PathSessionCompletion(false, false, false, default, null, firstStepElapsed);
            }

            continuationStartTileId = result.EndTileId;
            hasMoreSegments = true;
            state = PathSessionState.Streaming;
            bool schedule = stream.PendingCount <= Math.Max(1, lowWatermark);
            return new PathSessionCompletion(true, false, schedule,
                schedule && stream.PendingCount == 0 ? PathWorkPriority.Starved : PathWorkPriority.Continuation,
                null, firstStepElapsed);
        }
    }

    internal PathConsumeResult TryConsume(PathStream expectedStream, int lowWatermark)
    {
        lock (syncRoot)
        {
            if (cancelled || !ReferenceEquals(stream, expectedStream) || !stream.TryDequeue(out _))
            {
                return default;
            }

            bool finished = !hasMoreSegments && stream.IsFinished;
            int pendingSteps = stream.PendingCount;
            bool canSchedule = hasMoreSegments && !running &&
                               state is PathSessionState.Streaming or PathSessionState.Queued;
            bool schedule = canSchedule &&
                            (!queued && pendingSteps <= Math.Max(1, lowWatermark) ||
                             queued && queuedPriority != PathWorkPriority.Starved && pendingSteps == 0);
            bool starved = schedule && pendingSteps == 0;
            return new PathConsumeResult(schedule, starved, finished);
        }
    }

    internal bool PrepareExternalRetry(PathStream expectedStream, PathFailureReason reason, int startTileId,
        out RetryTicket retry)
    {
        lock (syncRoot)
        {
            retry = default;
            if (cancelled || !ReferenceEquals(stream, expectedStream)) return false;
            requestGeneration++;
            RenewCancellationLocked();
            stream.Cancel(reason);
            stream = new PathStream();
            continuationStartTileId = startTileId;
            hasMoreSegments = true;
            queued = false;
            rescheduleRequested = false;
            if (TryPrepareRetry(reason, out retry)) return true;
            stream.Fail(reason);
            state = PathSessionState.Failed;
            return false;
        }
    }

    internal bool TryArmRetry(int expectedVersion)
    {
        lock (syncRoot)
        {
            if (cancelled || state != PathSessionState.RetryDelay || retryVersion != expectedVersion) return false;
            return true;
        }
    }

    internal bool TryGetRetryRequest(int expectedVersion, out Actor actor, out PathRequestOptions options)
    {
        lock (syncRoot)
        {
            actor = null;
            options = default;
            if (cancelled || state != PathSessionState.RetryDelay || retryVersion != expectedVersion) return false;
            actor = request.Actor;
            options = new PathRequestOptions(request.Target, request.PathOnWater, request.WalkOnBlocks,
                request.WalkOnLava, request.RegionLimit);
            return true;
        }
    }

    internal bool ActivateRetry(int expectedVersion, PathRequest refreshedRequest)
    {
        lock (syncRoot)
        {
            if (cancelled || state != PathSessionState.RetryDelay || retryVersion != expectedVersion) return false;
            request = refreshedRequest;
            requestGeneration++;
            if (stream.PendingCount == 0)
            {
                continuationStartTileId = refreshedRequest.StartTileId;
                continuationStamina = refreshedRequest.ActorCurrentStamina;
                continuationHealth = refreshedRequest.ActorCurrentHealth;
            }

            state = PathSessionState.Streaming;
            return true;
        }
    }

    internal void FailRetry(int expectedVersion, PathFailureReason reason)
    {
        lock (syncRoot)
        {
            if (cancelled || state != PathSessionState.RetryDelay || retryVersion != expectedVersion) return;
            stream.Fail(reason);
            state = PathSessionState.Failed;
        }
    }

    internal bool IsCurrentStream(PathStream expected)
    {
        lock (syncRoot) return ReferenceEquals(stream, expected);
    }

    /// <summary>仅当调用方确认的流仍为当前版本时封闭会话，防止终态轮询误删新请求。</summary>
    internal bool TryDetach(PathStream expectedStream)
    {
        lock (syncRoot)
        {
            if (cancelled || !ReferenceEquals(stream, expectedStream)) return false;
            cancelled = true;
            requestGeneration++;
            CancelCurrentCancellationLocked();
            stream.Cancel(PathFailureReason.CancelledByNewRequest);
            state = PathSessionState.Cancelled;
            queued = false;
            rescheduleRequested = false;
            return true;
        }
    }

    internal void Cancel(PathFailureReason reason)
    {
        lock (syncRoot)
        {
            if (cancelled) return;
            cancelled = true;
            requestGeneration++;
            CancelCurrentCancellationLocked();
            stream.Cancel(reason);
            state = PathSessionState.Cancelled;
            queued = false;
            rescheduleRequested = false;
        }
    }

    internal void DisposeCompleted()
    {
        lock (syncRoot)
        {
            CancelCurrentCancellationLocked();
        }
    }

    /// <summary>为新目标换代取消源；仍由活跃 worker 使用的旧源交给完成回调释放。</summary>
    private void RenewCancellationLocked()
    {
        CancellationTokenSource previous = requestCancellation;
        previous?.Cancel();
        requestCancellation = new CancellationTokenSource();
        if (previous != null && !ReferenceEquals(previous, activeCancellation)) previous.Dispose();
    }

    /// <summary>取消当前请求，并立即释放未被活跃 worker 持有的取消源。</summary>
    private void CancelCurrentCancellationLocked()
    {
        CancellationTokenSource current = requestCancellation;
        current?.Cancel();
        if (current == null || ReferenceEquals(current, activeCancellation)) return;
        current.Dispose();
        requestCancellation = null;
    }

    /// <summary>释放从未提交给 worker 的取消源。</summary>
    private void DisposeCurrentCancellationLocked()
    {
        CancellationTokenSource current = requestCancellation;
        if (current == null || ReferenceEquals(current, activeCancellation)) return;
        current.Dispose();
        requestCancellation = null;
    }

    private bool TryPrepareRetry(PathFailureReason reason, out RetryTicket retry)
    {
        retry = default;
        if (!CanRecover(reason)) return false;
        if (lastFailure != reason)
        {
            retryCount = 0;
            lastFailure = reason;
        }

        retryCount++;
        if (retryCount > MaxRetriesFor(reason)) return false;
        double delay = Math.Min(2d, 0.3d * Math.Pow(2d, retryCount - 1));
        retryVersion++;
        state = PathSessionState.RetryDelay;
        retry = new RetryTicket(this, retryVersion, delay);
        return true;
    }

    private static bool CanRecover(PathFailureReason reason)
    {
        return reason switch
        {
            PathFailureReason.Unreachable => true,
            PathFailureReason.SearchLimitExceeded => true,
            PathFailureReason.StepBlocked => true,
            PathFailureReason.UnsafeStep => true,
            PathFailureReason.PortalUnavailable => true,
            PathFailureReason.TransportFailed => true,
            PathFailureReason.Timeout => true,
            PathFailureReason.GeneratorException => true,
            PathFailureReason.NavigationGridUnavailable => true,
            _ => false
        };
    }

    private static int MaxRetriesFor(PathFailureReason reason)
    {
        return reason switch
        {
            PathFailureReason.GeneratorException => 1,
            PathFailureReason.PortalUnavailable => 2,
            PathFailureReason.TransportFailed => 2,
            PathFailureReason.Timeout => 2,
            PathFailureReason.NavigationGridUnavailable => 2,
            PathFailureReason.Unreachable => 2,
            PathFailureReason.SearchLimitExceeded => 2,
            _ => 4
        };
    }
}

internal readonly struct PathRequestOptions
{
    internal PathRequestOptions(WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int regionLimit)
    {
        Target = target;
        PathOnWater = pathOnWater;
        WalkOnBlocks = walkOnBlocks;
        WalkOnLava = walkOnLava;
        RegionLimit = regionLimit;
    }

    internal WorldTile Target { get; }
    internal bool PathOnWater { get; }
    internal bool WalkOnBlocks { get; }
    internal bool WalkOnLava { get; }
    internal int RegionLimit { get; }
}

internal sealed class PassthroughPathGenerator : IPathGenerator
{
    public PathGenerationResult GenerateSegment(PathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return request.TargetTileId < 0
            ? PathGenerationResult.Fail(PathFailureReason.InvalidTarget)
            : PathGenerationResult.Success(
                new[] { new PathStep(request.TargetTileId, MovementMethod.Walk, TraversalEstimate.Direct) },
                true, request.TargetTileId, kind: PathGenerationKind.StraightLine);
    }
}

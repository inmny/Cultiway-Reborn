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

    private readonly ConcurrentDictionary<PathAgentKey, PathSession> sessions = new();
    private readonly ConcurrentDictionary<PathAgentKey, PathRequestOptions> lastRequests = new();
    private readonly ConcurrentDictionary<PathAgentKey, long> submissionTokens = new();
    private readonly object sessionOwnershipLock = new();
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
    private bool clearing;
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
    private long nextSubmissionToken;

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

        PathRequest request = PathRequest.CreateMainWorld(
            actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions);
        PathAgentKey agentKey = request.AgentKey;
        lock (sessionOwnershipLock)
        {
            PathfindingProfiler.Measurement reuseMeasurement = PathfindingProfiler.Start();
            if (TryReuseActiveRequest(agentKey, request.TargetTileId, pathOnWater, walkOnBlocks, walkOnLava,
                    limitRegions))
            {
                reuseMeasurement.Complete(PathfindingBenchmarkMetric.Reuse);
                return new PathSubmissionResult(
                    PathSubmissionKind.Reused,
                    submissionToken: RecordSubmission(agentKey));
            }

            reuseMeasurement.Complete(PathfindingBenchmarkMetric.ReuseMiss);
            PathfindingProfiler.Measurement createMeasurement = PathfindingProfiler.Start();
            lastRequests[agentKey] = new PathRequestOptions(target, pathOnWater, walkOnBlocks, walkOnLava,
                limitRegions);
            createMeasurement.Complete(PathfindingBenchmarkMetric.Create);
            PathSubmissionResult result = Submit(request);
            return result.Accepted
                ? new PathSubmissionResult(result.Kind, submissionToken: RecordSubmission(agentKey))
                : result;
        }
    }

    /// <summary>提交已经在所属模拟线程完成快照化的通用寻路请求。</summary>
    public PathSubmissionResult RequestPathDetailed(PathRequest request)
    {
        if (request == null || !request.AgentKey.IsValid)
        {
            return new PathSubmissionResult(PathSubmissionKind.Rejected, PathFailureReason.InvalidActor);
        }
        if (request.NavigationGrid == null || request.NavigationGrid.WorldKey != request.AgentKey.World)
        {
            return new PathSubmissionResult(PathSubmissionKind.Rejected,
                PathFailureReason.NavigationGridUnavailable);
        }
        if (request.StartTileId < 0 || request.StartTileId >= request.NavigationGrid.TileCount)
        {
            return new PathSubmissionResult(PathSubmissionKind.Rejected, PathFailureReason.InvalidStart);
        }
        if (request.TargetTileId < 0 || request.TargetTileId >= request.NavigationGrid.TileCount)
        {
            return new PathSubmissionResult(PathSubmissionKind.Rejected, PathFailureReason.InvalidTarget);
        }

        lock (sessionOwnershipLock)
        {
            PathSubmissionResult result = Submit(request);
            return result.Accepted
                ? new PathSubmissionResult(result.Kind, submissionToken: RecordSubmission(request.AgentKey))
                : result;
        }
    }

    public bool RequestPath(PathRequest request)
    {
        return RequestPathDetailed(request).Accepted;
    }

    private PathSubmissionResult Submit(PathRequest request)
    {
        PathAgentKey agentKey = request.AgentKey;
        if (!agentKey.IsValid)
        {
            return new PathSubmissionResult(PathSubmissionKind.Rejected, PathFailureReason.InvalidActor);
        }

        while (sessions.TryGetValue(agentKey, out PathSession existing))
        {
            if (!existing.TryReplace(request))
            {
                ((ICollection<KeyValuePair<PathAgentKey, PathSession>>)sessions)
                    .Remove(new KeyValuePair<PathAgentKey, PathSession>(agentKey, existing));
                continue;
            }

            Interlocked.Increment(ref replacedRequests);
            Schedule(existing, PathWorkPriority.Initial);
            return new PathSubmissionResult(PathSubmissionKind.Replaced);
        }

        PathfindingProfiler.Measurement taskCreateMeasurement = PathfindingProfiler.Start();
        var session = new PathSession(request, taskCreateMeasurement.Session);
        if (!sessions.TryAdd(agentKey, session))
        {
            session.Cancel(PathFailureReason.CancelledByNewRequest);
            return Submit(request);
        }

        taskCreateMeasurement.Complete(PathfindingBenchmarkMetric.TaskCreate);
        Schedule(session, PathWorkPriority.Initial);
        return new PathSubmissionResult(PathSubmissionKind.Created);
    }

    private bool TryReuseActiveRequest(PathAgentKey agentKey, int targetTileId, bool pathOnWater,
        bool walkOnBlocks, bool walkOnLava, int limitRegions)
    {
        if (!agentKey.IsValid || !sessions.TryGetValue(agentKey, out PathSession session))
        {
            return false;
        }

        return session.CanReuse(targetTileId, pathOnWater, walkOnBlocks, walkOnLava, limitRegions);
    }

    private void Schedule(PathSession session, PathWorkPriority priority)
    {
        lock (sessionOwnershipLock)
        {
            if (session == null || shuttingDown || clearing) return;
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
        PathAgentKey agentKey = retry.Session.AgentKey;
        if (agentKey.World.Kind != PathWorldKind.MainWorld ||
            !sessions.TryGetValue(agentKey, out PathSession current) ||
            !ReferenceEquals(current, retry.Session) ||
            !retry.Session.TryGetRetryRequest(retry.Version, out _))
        {
            return;
        }

        if (!lastRequests.TryGetValue(agentKey, out PathRequestOptions options))
        {
            retry.Session.FailRetry(retry.Version, PathFailureReason.InvalidActor);
            return;
        }

        Actor actor = World.world?.units?.get(agentKey.AgentId);
        WorldTile target = actor?.tile_target ?? options.Target;
        if (!CanAcceptRequest(actor, target, out PathFailureReason failureReason))
        {
            retry.Session.FailRetry(retry.Version, failureReason);
            return;
        }

        PathRequest request = PathRequest.CreateMainWorld(
            actor, target, options.PathOnWater, options.WalkOnBlocks, options.WalkOnLava,
            options.RegionLimit);
        if (request.AgentKey.World != agentKey.World)
        {
            retry.Session.FailRetry(retry.Version, PathFailureReason.ClearWorld);
            return;
        }

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
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        if (!agentKey.IsValid || !sessions.TryGetValue(agentKey, out PathSession session))
        {
            return false;
        }

        return ScheduleRecovery(agentKey, session, session.CurrentStream, actor, reason);
    }

    private bool ScheduleRecovery(PathAgentKey agentKey, PathSession session, PathStream stream, Actor actor,
        PathFailureReason reason)
    {
        if (!IsCurrent(agentKey, session)) return false;
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
        PathRequest request = PathRequest.CreateMainWorld(actor, target, true, true, true, 0);
        PathAgentKey agentKey = request.AgentKey;
        lock (sessionOwnershipLock)
        {
            lastRequests[agentKey] = new PathRequestOptions(target, true, true, true, 0);
            var direct = PathSession.CreateDirect(request,
                new PathStep(target, MovementMethod.Walk, TraversalEstimate.Direct));
            if (sessions.TryGetValue(agentKey, out PathSession old))
            {
                old.Cancel(PathFailureReason.CancelledByNewRequest);
            }
            sessions[agentKey] = direct;
            RecordSubmission(agentKey);
        }
    }

    public bool IsActorPathing(Actor actor)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        return agentKey.IsValid && sessions.TryGetValue(agentKey, out PathSession session) &&
               session.IsVisibleToPoller;
    }

    public List<PathStep> TryViewAll(Actor actor)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        return agentKey.IsValid && sessions.TryGetValue(agentKey, out PathSession session)
            ? session.CurrentStream.TryViewAll()
            : null;
    }

    public PathPollResult PollStep(Actor actor)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        if (!agentKey.IsValid) return PathPollResult.Failed(PathFailureReason.InvalidActor);
        if (!sessions.TryGetValue(agentKey, out PathSession session)) return PathPollResult.NoRequest();
        PathStream stream = session.CurrentStream;
        PathPollResult result = GetPollResult(agentKey, session, stream);
        if (IsTerminal(result.Kind)) CleanupSession(agentKey, session, stream);
        return result;
    }

    public PathPollResult PeekReadyStep(Actor actor, out ReadyPathStep readyStep)
    {
        readyStep = default;
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        if (!agentKey.IsValid) return PathPollResult.Failed(PathFailureReason.InvalidActor);
        if (!sessions.TryGetValue(agentKey, out PathSession session)) return PathPollResult.NoRequest();
        PathStream stream = session.CurrentStream;
        PathPollResult result = GetPollResult(agentKey, session, stream);
        if (result.Kind == PathPollKind.StepReady)
        {
            readyStep = new ReadyPathStep(this, agentKey, session, stream, result.Step);
        }
        else if (IsTerminal(result.Kind))
        {
            CleanupSession(agentKey, session, stream);
        }

        return result;
    }

    public PathPollResult OpenReadyCursor(Actor actor, out ReadyPathCursor cursor)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        lock (sessionOwnershipLock)
        {
            return OpenReadyCursor(agentKey, false, 0, out cursor);
        }
    }

    /// <summary>按提交 handle 打开游标；token 不匹配时旧调用方只能看到 NoRequest。</summary>
    public PathPollResult OpenReadyCursor(PathHandle handle, out ReadyPathCursor cursor)
    {
        lock (sessionOwnershipLock)
        {
            cursor = default;
            if (!handle.IsValid || !submissionTokens.TryGetValue(handle.Agent, out long currentToken) ||
                currentToken != handle.SubmissionToken)
            {
                return PathPollResult.NoRequest();
            }

            return OpenReadyCursor(handle.Agent, true, handle.SubmissionToken, out cursor);
        }
    }

    private PathPollResult OpenReadyCursor(PathAgentKey agentKey, bool tokenBound, long token,
        out ReadyPathCursor cursor)
    {
        cursor = default;
        if (!agentKey.IsValid) return PathPollResult.Failed(PathFailureReason.InvalidActor);
        if (!sessions.TryGetValue(agentKey, out PathSession session)) return PathPollResult.NoRequest();
        PathStream stream = session.CurrentStream;
        PathPollResult result = GetPollResult(agentKey, session, stream);
        cursor = new ReadyPathCursor(this, agentKey, session, stream, tokenBound, token);
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

    private PathPollResult GetPollResult(PathAgentKey agentKey, PathSession session, PathStream stream)
    {
        if (!IsCurrent(agentKey, session)) return PathPollResult.NoRequest();
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
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        if (!agentKey.IsValid || !sessions.TryGetValue(agentKey, out PathSession session)) return false;
        return CleanupSession(agentKey, session, session.CurrentStream);
    }

    public void ConsumeStep(Actor actor)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        if (!agentKey.IsValid || !sessions.TryGetValue(agentKey, out PathSession session)) return;
        Consume(agentKey, session, session.CurrentStream);
    }

    private void Consume(PathAgentKey agentKey, PathSession session, PathStream stream)
    {
        if (!IsCurrent(agentKey, session)) return;
        PathConsumeResult result = session.TryConsume(stream, PathfindingConfig.Default.SegmentLowWatermark);
        if (result.ScheduleContinuation)
        {
            Schedule(session, result.Starved ? PathWorkPriority.Starved : PathWorkPriority.Continuation);
        }

        if (result.Finished) CleanupSession(agentKey, session, stream);
    }

    public void Cancel(Actor actor, PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        if (agentKey.IsValid) Cancel(agentKey, reason);
    }

    public bool Cancel(PathHandle handle, PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        if (!handle.IsValid) return false;
        lock (sessionOwnershipLock)
        {
            bool owned = ((ICollection<KeyValuePair<PathAgentKey, long>>)submissionTokens)
                .Remove(new KeyValuePair<PathAgentKey, long>(handle.Agent, handle.SubmissionToken));
            if (!owned) return false;
            if (sessions.TryRemove(handle.Agent, out PathSession session)) session.Cancel(reason);
            return true;
        }
    }

    public void Cancel(PathAgentKey agentKey,
        PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        if (!agentKey.IsValid) return;
        lock (sessionOwnershipLock)
        {
            submissionTokens.TryRemove(agentKey, out _);
            PathfindingProfiler.Measurement measurement = PathfindingProfiler.Start();
            bool removed = sessions.TryRemove(agentKey, out PathSession session);
            if (removed) session.Cancel(reason);
            measurement.Complete(removed ? PathfindingBenchmarkMetric.Cancel : PathfindingBenchmarkMetric.CancelEmpty);
        }
    }

    /// <summary>只取消仍对应指定提交令牌的寻路，避免撤销后来接管的同目标请求。</summary>
    public bool CancelOwned(Actor actor, long submissionToken,
        PathFailureReason reason = PathFailureReason.CancelledByNewRequest)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        return agentKey.IsValid && Cancel(new PathHandle(agentKey, submissionToken), reason);
    }

    /// <summary>读取角色最近一次被接受的外部寻路提交令牌。</summary>
    public bool TryGetCurrentSubmissionToken(Actor actor, out long submissionToken)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        submissionToken = 0;
        lock (sessionOwnershipLock)
        {
            return agentKey.IsValid && submissionTokens.TryGetValue(agentKey, out submissionToken) &&
                   sessions.ContainsKey(agentKey);
        }
    }

    private bool CleanupSession(PathAgentKey agentKey, PathSession session, PathStream stream)
    {
        lock (sessionOwnershipLock)
        {
            if (!IsCurrent(agentKey, session) || !session.TryDetach(stream))
            {
                return false;
            }

            bool removed = ((ICollection<KeyValuePair<PathAgentKey, PathSession>>)sessions)
                .Remove(new KeyValuePair<PathAgentKey, PathSession>(agentKey, session));
            if (removed) submissionTokens.TryRemove(agentKey, out _);
            session.DisposeCompleted();
            return removed;
        }
    }

    public void Cleanup(long actorId)
    {
        PathWorldKey world = PathNavigationGridService.Current?.WorldKey ??
                             PathWorldKey.MainWorld(SimulationTime.Generation);
        PathAgentKey agentKey = new(world, actorId);
        lock (sessionOwnershipLock)
        {
            if (sessions.TryRemove(agentKey, out PathSession session))
            {
                session.Cancel(PathFailureReason.ActorDead);
            }

            lastRequests.TryRemove(agentKey, out _);
            submissionTokens.TryRemove(agentKey, out _);
        }
    }

    public void CancelWorld(PathWorldKey world,
        PathFailureReason reason = PathFailureReason.ClearWorld)
    {
        lock (sessionOwnershipLock)
        {
            foreach (KeyValuePair<PathAgentKey, PathSession> pair in sessions)
            {
                if (pair.Key.World != world) continue;
                if (((ICollection<KeyValuePair<PathAgentKey, PathSession>>)sessions).Remove(pair))
                {
                    pair.Value.Cancel(reason);
                }
                lastRequests.TryRemove(pair.Key, out _);
                submissionTokens.TryRemove(pair.Key, out _);
            }
        }
    }

    public void Clear()
    {
        lock (sessionOwnershipLock)
        {
            clearing = true;
            try
            {
                foreach (KeyValuePair<PathAgentKey, PathSession> pair in sessions)
                {
                    pair.Value.Cancel(PathFailureReason.ClearWorld);
                }

                sessions.Clear();
                lastRequests.Clear();
                submissionTokens.Clear();
                while (pendingRetries.TryDequeue(out _)) { }
                scheduledRetries.Clear();
                Drain(starvedQueue);
                Drain(initialQueue);
                Drain(continuationQueue);
                while (pendingSignal.Wait(0)) { }
                PathNavigationGridService.Clear();
            }
            finally
            {
                clearing = false;
            }
        }
    }

    internal bool TryGetLastRequestOptions(Actor actor, out PathRequestOptions options)
    {
        PathAgentKey agentKey = ResolveMainWorldAgentKey(actor);
        options = default;
        return agentKey.IsValid && lastRequests.TryGetValue(agentKey, out options);
    }

    internal bool TryRequestRecover(Actor actor, WorldTile overrideTarget = null)
    {
        if (actor?.data == null || !TryGetLastRequestOptions(actor, out PathRequestOptions options)) return false;
        WorldTile target = overrideTarget ?? actor.tile_target ?? options.Target;
        if (!CanAcceptRequest(actor, target, out _)) return false;
        PathRequest request = PathRequest.CreateMainWorld(
            actor, target, options.PathOnWater, options.WalkOnBlocks, options.WalkOnLava,
            options.RegionLimit);
        lock (sessionOwnershipLock)
        {
            return Submit(request).Accepted;
        }
    }

    /// <summary>为一次外部寻路命令生成单调递增的所有权令牌。</summary>
    private long RecordSubmission(PathAgentKey agentKey)
    {
        long token = Interlocked.Increment(ref nextSubmissionToken);
        submissionTokens[agentKey] = token;
        return token;
    }

    private static PathAgentKey ResolveMainWorldAgentKey(Actor actor)
    {
        PathWorldKey world = PathNavigationGridService.Current?.WorldKey ??
                             PathWorldKey.MainWorld(SimulationTime.Generation);
        return new PathAgentKey(world, actor?.data?.id ?? 0);
    }

    public string GetDiagnostics()
    {
        long firstCount = Interlocked.Read(ref firstStepCount);
        double firstMilliseconds = firstCount == 0
            ? 0d
            : Interlocked.Read(ref firstStepTicks) * 1000d / Stopwatch.Frequency / firstCount;
        int mainSessions = 0;
        int subWorldSessions = 0;
        foreach (PathAgentKey key in sessions.Keys)
        {
            if (key.World.Kind == PathWorldKind.MainWorld) mainSessions++;
            else subWorldSessions++;
        }

        var builder = new StringBuilder(288);
        builder.Append("sessions=").Append(sessions.Count)
            .Append(" main=").Append(mainSessions)
            .Append(" sub=").Append(subWorldSessions)
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

    private bool IsCurrent(PathAgentKey agentKey, PathSession session)
    {
        return sessions.TryGetValue(agentKey, out PathSession current) && ReferenceEquals(current, session);
    }

    private bool IsCursorCurrent(PathAgentKey agentKey, PathSession session, PathStream stream,
        bool tokenBound, long submissionToken)
    {
        lock (sessionOwnershipLock)
        {
            return IsCursorCurrentLocked(agentKey, session, stream, tokenBound, submissionToken);
        }
    }

    private bool IsCursorCurrentLocked(PathAgentKey agentKey, PathSession session, PathStream stream,
        bool tokenBound, long submissionToken)
    {
        if (!IsCurrent(agentKey, session) || !session.IsCurrentStream(stream)) return false;
        return !tokenBound || submissionToken > 0 &&
               submissionTokens.TryGetValue(agentKey, out long current) && current == submissionToken;
    }

    private PathPollResult PollCursor(PathAgentKey agentKey, PathSession session, PathStream stream,
        bool tokenBound, long submissionToken)
    {
        lock (sessionOwnershipLock)
        {
            return IsCursorCurrentLocked(agentKey, session, stream, tokenBound, submissionToken)
                ? GetPollResult(agentKey, session, stream)
                : PathPollResult.NoRequest();
        }
    }

    private void ConsumeCursor(PathAgentKey agentKey, PathSession session, PathStream stream,
        bool tokenBound, long submissionToken)
    {
        lock (sessionOwnershipLock)
        {
            if (IsCursorCurrentLocked(agentKey, session, stream, tokenBound, submissionToken))
            {
                Consume(agentKey, session, stream);
            }
        }
    }

    private bool AcknowledgeCursor(PathAgentKey agentKey, PathSession session, PathStream stream,
        bool tokenBound, long submissionToken)
    {
        lock (sessionOwnershipLock)
        {
            return IsCursorCurrentLocked(agentKey, session, stream, tokenBound, submissionToken) &&
                   CleanupSession(agentKey, session, stream);
        }
    }

    private bool ScheduleRecoveryCursor(PathAgentKey agentKey, PathSession session, PathStream stream,
        bool tokenBound, long submissionToken, Actor actor, PathFailureReason reason)
    {
        lock (sessionOwnershipLock)
        {
            return IsCursorCurrentLocked(agentKey, session, stream, tokenBound, submissionToken) &&
                   ScheduleRecovery(agentKey, session, stream, actor, reason);
        }
    }

    private bool TryExecuteCursorStep<T>(PathAgentKey agentKey, PathSession session, PathStream stream,
        bool tokenBound, long submissionToken, Func<PathStep, T> action, out T result)
    {
        lock (sessionOwnershipLock)
        {
            if (action == null ||
                !IsCursorCurrentLocked(agentKey, session, stream, tokenBound, submissionToken) ||
                !stream.TryPeek(out PathStep step))
            {
                result = default;
                return false;
            }

            result = action(step);
            return true;
        }
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
        private readonly PathAgentKey agentKey;
        private readonly PathSession session;
        private readonly PathStream stream;

        internal ReadyPathStep(PathFinder owner, PathAgentKey agentKey, PathSession session, PathStream stream,
            PathStep step)
        {
            this.owner = owner;
            this.agentKey = agentKey;
            this.session = session;
            this.stream = stream;
            Step = step;
        }

        public PathStep Step { get; }
        public bool IsValid => owner != null && session != null && stream != null;
        public void Consume()
        {
            if (IsValid) owner.Consume(agentKey, session, stream);
        }
    }

    public readonly struct ReadyPathCursor
    {
        private readonly PathFinder owner;
        private readonly PathAgentKey agentKey;
        private readonly PathSession session;
        private readonly PathStream stream;
        private readonly bool tokenBound;
        private readonly long submissionToken;

        internal ReadyPathCursor(PathFinder owner, PathAgentKey agentKey, PathSession session, PathStream stream,
            bool tokenBound, long submissionToken)
        {
            this.owner = owner;
            this.agentKey = agentKey;
            this.session = session;
            this.stream = stream;
            this.tokenBound = tokenBound;
            this.submissionToken = submissionToken;
        }

        public bool IsValid => owner != null && session != null && stream != null &&
                               owner.IsCursorCurrent(
                                   agentKey, session, stream, tokenBound, submissionToken);
        public PathPollResult Poll()
        {
            return owner == null
                ? PathPollResult.NoRequest()
                : owner.PollCursor(agentKey, session, stream, tokenBound, submissionToken);
        }

        public void Consume()
        {
            owner?.ConsumeCursor(agentKey, session, stream, tokenBound, submissionToken);
        }

        public bool Acknowledge()
        {
            return owner != null &&
                   owner.AcknowledgeCursor(agentKey, session, stream, tokenBound, submissionToken);
        }

        public bool ScheduleRecovery(Actor actor, PathFailureReason reason)
        {
            return owner != null && owner.ScheduleRecoveryCursor(
                agentKey, session, stream, tokenBound, submissionToken, actor, reason);
        }

        public bool TryExecuteCurrentStep<T>(Func<PathStep, T> action, out T result)
        {
            if (owner != null)
            {
                return owner.TryExecuteCursorStep(
                    agentKey, session, stream, tokenBound, submissionToken, action, out result);
            }

            result = default;
            return false;
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
        AgentKey = request.AgentKey;
        stream = new PathStream();
        state = PathSessionState.Queued;
        continuationStartTileId = request.StartTileId;
        continuationStamina = request.ActorCurrentStamina;
        continuationHealth = request.ActorCurrentHealth;
        requestCancellation = new CancellationTokenSource();
        BenchmarkSession = benchmarkSession;
    }

    internal PathAgentKey AgentKey { get; }
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

    internal bool CanReuse(int targetTileId, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int regionLimit)
    {
        lock (syncRoot)
        {
            if (cancelled || state is PathSessionState.Failed or PathSessionState.Cancelled) return false;
            return request.HasSameTargetAndOptions(targetTileId, pathOnWater, walkOnBlocks, walkOnLava,
                       regionLimit) &&
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
            PathRequest segmentRequest = request.WithStart(continuationStartTileId,
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

    internal bool TryGetRetryRequest(int expectedVersion, out PathRequest retryRequest)
    {
        lock (syncRoot)
        {
            retryRequest = null;
            if (cancelled || state != PathSessionState.RetryDelay || retryVersion != expectedVersion)
            {
                return false;
            }

            retryRequest = request;
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
        if (request.SearchRules.RetryMode != PathRetryMode.TimedMainWorld || !CanRecover(reason)) return false;
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

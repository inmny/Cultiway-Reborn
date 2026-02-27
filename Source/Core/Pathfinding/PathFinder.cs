using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Utils;
using Cultiway;
using System.Collections.Generic;

namespace Cultiway.Core.Pathfinding;

public class PathFinder
{
    public static PathFinder Instance { get; } = new();
    internal static readonly object ActorSyncLock = new object();

    private readonly ConcurrentDictionary<long, PathfindingTask> _tasks = new();
    private readonly ConcurrentDictionary<long, PathRequestOptions> _lastRequests = new();
    private IPathGenerator _generator;

    public void UseGenerator(IPathGenerator generator)
    {
        _generator = generator ?? new PortalAwarePathGenerator(PortalRegistry.Instance, PathfindingConfig.Default);
    }

    public void RequestPath(Actor actor, WorldTile target, bool pathOnWater, bool walkOnBlocks, bool walkOnLava,
        int limitRegions)
    {
        RequestPath(new PathRequest(actor, target, pathOnWater, walkOnBlocks, walkOnLava, limitRegions));
    }

    public void RequestPath(PathRequest request)
    {
        if (request.Actor?.data == null || request.Target == null)
        {
            return;
        }

        _lastRequests[request.Actor.data.id] = new PathRequestOptions(request.Target, request.PathOnWater,
            request.WalkOnBlocks, request.WalkOnLava, request.RegionLimit);
        Cancel(request.Actor);

        var task = new PathfindingTask(request);
        _tasks[request.Actor.data.id] = task;

        task.Worker = Task.Run(() => RunGeneratorAsync(task), task.Cancellation.Token);
    }

    public bool IsActorPathing(Actor actor)
    {
        if (actor?.data == null) return false;
        if (!_tasks.TryGetValue(actor.data.id, out var task))
        {
            return false;
        }

        if (task.Stream.HasPendingSteps || !task.Stream.IsFinished)
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
    public bool TryPeekStep(Actor actor, out PathStep step, out bool finished)
    {
        finished = false;
        step = default;
        if (actor?.data == null)
        {
            finished = true;
            return false;
        }

        if (!_tasks.TryGetValue(actor.data.id, out var task))
        {
            finished = true;
            return false;
        }

        if (task.Stream.TryPeek(out step))
        {
            return true;
        }

        if (task.Stream.IsFinished)
        {
            finished = true;
            Cleanup(actor.data.id, task);
        }

        return false;
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

    public void Cancel(Actor actor)
    {
        if (actor?.data == null)
        {
            return;
        }

        if (_tasks.TryRemove(actor.data.id, out var task))
        {
            task.Stream.Cancel();
            task.Cancellation.Cancel();
            if (task.Worker != null)
            {
                task.Worker.ContinueWith(_ => task.Dispose());
            }
            else
            {
                task.Dispose();
            }
        }
    }

    private async Task RunGeneratorAsync(PathfindingTask task)
    {
        try
        {
            await _generator.GenerateAsync(task.Request, task.Stream, task.Cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            task.Stream.Cancel();
        }
        catch (Exception e)
        {
            task.Stream.Fail(e);
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
        }
        finally
        {
            task.Stream.EnsureCompleted();
        }
    }

    private void Cleanup(long actorId, PathfindingTask task)
    {
        if (_tasks.TryRemove(actorId, out _))
        {
            task.Dispose();
        }
    }
    public void Cleanup(long actorId)
    {
        if (_tasks.TryRemove(actor.data.id, out var task))
        {
            task.Dispose();
        }
        _lastRequests.TryRemove(actorId, out _);
    }

    public void Clear()
    {
        foreach (var id_task_pair in _tasks)
        {
            var task = id_task_pair.Value;
            task.Stream.Cancel();
            task.Cancellation.Cancel();
            if (task.Worker != null)
            {
                task.Worker.ContinueWith(_ => task.Dispose());
            }
            else
            {
                task.Dispose();
            }
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

        RequestPath(new PathRequest(actor, target, opt.PathOnWater, opt.WalkOnBlocks, opt.WalkOnLava,
            opt.RegionLimit));
        return true;
    }
}

internal sealed class PathfindingTask : IDisposable
{
    public PathfindingTask(PathRequest request)
    {
        Request = request;
        Stream = new PathStream();
        Cancellation = new CancellationTokenSource();
    }

    public PathRequest Request { get; }
    public PathStream Stream { get; }
    public CancellationTokenSource Cancellation { get; }
    public Task Worker { get; set; }

    public void Dispose()
    {
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
        if (request.Target != null)
        {
            stream.AddStep(new PathStep(request.Target, MovementMethod.Walk, StepPenalty.Block | StepPenalty.Lava | StepPenalty.Ocean));
        }

        stream.Complete();
        return Task.CompletedTask;
    }
}

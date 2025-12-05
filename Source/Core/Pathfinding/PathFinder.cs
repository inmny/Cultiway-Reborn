using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Utils;
using Cultiway;

namespace Cultiway.Core.Pathfinding;

public class PathFinder
{
    public static PathFinder Instance { get; } = new();
    internal static readonly object ActorSyncLock = new object();

    private readonly ConcurrentDictionary<long, PathfindingTask> _tasks = new();
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

internal sealed class PassthroughPathGenerator : IPathGenerator
{
    public Task GenerateAsync(PathRequest request, IPathStreamWriter stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Target != null)
        {
            stream.AddStep(request.Target, GetMethod(request.Actor, request.Target, request.PathOnWater));
        }

        stream.Complete();
        return Task.CompletedTask;
    }

    private static MovementMethod GetMethod(Actor actor, WorldTile tile, bool pathOnWater)
    {
        var tileType = tile.Type;
        if (actor.asset.is_boat)
        {
            return MovementMethod.Sail;
        }

        if (tileType.ocean || pathOnWater)
        {
            return MovementMethod.Swim;
        }

        return MovementMethod.Walk;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Core.Pathfinding;

public class PortalAwarePathGenerator : IPathGenerator
{
    private readonly PortalRegistry _registry;
    private readonly PathfindingConfig _config;

    public PortalAwarePathGenerator(PortalRegistry registry, PathfindingConfig config)
    {
        _registry = registry ?? PortalRegistry.Instance;
        _config = config ?? PathfindingConfig.Default;
    }

    public Task GenerateAsync(PathRequest request, IPathStreamWriter stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Start == null || request.Target == null)
        {
            stream.Complete();
            return Task.CompletedTask;
        }

        try
        {
            GenerateInternal(request, stream, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            stream.Cancel();
        }
        catch (Exception e)
        {
            // 多线程求路径失败不应炸游戏，记录错误即可
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
            stream.Fail(e);
        }

        stream.EnsureCompleted();
        return Task.CompletedTask;
    }

    private void GenerateInternal(PathRequest request, IPathStreamWriter stream, CancellationToken token)
    {
        var profile = MovementProfile.Build(request, _config);

        var direct = TryBuildLocalPath(request.Start, request.Target, profile, useLongRange: true, token);
        var candidates = new List<RouteCandidate>();
        if (direct.IsSuccess)
        {
            candidates.Add(RouteCandidate.FromSegments(direct.Steps, direct.Cost));
        }

        foreach (var portalCandidate in BuildPortalCandidates(request, profile, token))
        {
            candidates.Add(portalCandidate);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var best = candidates.OrderBy(c => c.Cost).First();
        EmitCandidate(best, stream, token);
    }

    private IEnumerable<RouteCandidate> BuildPortalCandidates(PathRequest request, MovementProfile profile,
        CancellationToken token)
    {
        var portals = _registry.Snapshot();
        if (portals.Count == 0)
        {
            yield break;
        }

        var portalDict = portals.ToDictionary(p => p.Id, p => p);
        var startTile = request.Start;
        var targetTile = request.Target;
        // 缓存入口/出口的局部路径，避免重复长程 A*
        var toEntryCache = new Dictionary<long, LocalPathResult>();
        var exitToTargetCache = new Dictionary<long, LocalPathResult>();

        var nearStart = portals
            .OrderBy(p => DistTile(startTile, p.Tile))
            .Take(_config.PortalCandidates)
            .ToList();

        var nearEndIds = new HashSet<long>(portals
            .OrderBy(p => DistTile(targetTile, p.Tile))
            .Take(_config.PortalCandidates)
            .Select(p => p.Id));

        // 用直接路径的成本作为剪枝上界
        var bestCost = float.MaxValue;
        {
            var direct = TryBuildLocalPath(startTile, targetTile, profile, useLongRange: true, token);
            if (direct.IsSuccess)
            {
                bestCost = direct.Cost;
                yield return RouteCandidate.FromSegments(direct.Steps, direct.Cost);
            }
        }

        foreach (var entry in nearStart)
        {
            if (DistTile(startTile, entry.Tile) > _config.PortalSearchRadius)
            {
                continue;
            }

            // 入口局部路径（缓存）
            if (!toEntryCache.TryGetValue(entry.Id, out var toEntry))
            {
                toEntry = TryBuildLocalPath(startTile, entry.Tile, profile, useLongRange: true, token);
                toEntryCache[entry.Id] = toEntry;
            }
            if (!toEntry.IsSuccess)
            {
                continue;
            }

            foreach (var link in entry.Connections.OrderBy(c => c.TravelTime))
            {
                token.ThrowIfCancellationRequested();
                if (!portalDict.TryGetValue(link.TargetId, out var exit))
                {
                    continue;
                }

                if (!nearEndIds.Contains(exit.Id) && DistTile(targetTile, exit.Tile) > _config.PortalSearchRadius * 2)
                {
                    continue;
                }

                // 估算下界：已知 toEntry + portal 固定代价 + 出口到终点的启发式
                var heuristicExit = Heuristic(exit.Tile, targetTile) / Mathf.Max(profile.WalkSpeed, 0.01f);
                var lowerBound = toEntry.Cost + entry.WaitTime + link.TravelTime + exit.TransferTime + heuristicExit;
                if (lowerBound >= bestCost)
                {
                    continue;
                }

                // 出口局部路径（缓存）
                if (!exitToTargetCache.TryGetValue(exit.Id, out var exitToTarget))
                {
                    exitToTarget = TryBuildLocalPath(exit.Tile, targetTile, profile, useLongRange: true, token);
                    exitToTargetCache[exit.Id] = exitToTarget;
                }
                if (!exitToTarget.IsSuccess)
                {
                    continue;
                }

                var cost = toEntry.Cost + entry.WaitTime + link.TravelTime + exit.TransferTime + exitToTarget.Cost;
                if (cost >= bestCost)
                {
                    continue;
                }

                var legs = new List<RouteLeg>
                {
                    new MovementLeg(toEntry.Steps, toEntry.Cost),
                    new PortalLeg(entry, exit, entry.WaitTime + link.TravelTime + exit.TransferTime),
                    new MovementLeg(exitToTarget.Steps, exitToTarget.Cost)
                };
                bestCost = cost;
                yield return RouteCandidate.FromLegs(legs, cost);
            }
        }
    }

    private void EmitCandidate(RouteCandidate candidate, IPathStreamWriter stream, CancellationToken token)
    {
        foreach (var leg in candidate.Legs)
        {
            token.ThrowIfCancellationRequested();
            switch (leg)
            {
                case MovementLeg movement:
                    foreach (var step in movement.Steps)
                    {
                        token.ThrowIfCancellationRequested();
                        stream.AddStep(step.Tile, step.Method);
                    }

                    break;
                case PortalLeg portal:
                    // 传送/乘船在客户端处理，默认用 Sail 占位
                    stream.AddStep(portal.Exit.Tile, MovementMethod.Sail);
                    break;
            }
        }
    }

    private LocalPathResult TryBuildLocalPath(WorldTile start, WorldTile target, MovementProfile profile,
        bool useLongRange, CancellationToken token)
    {
        if (start == null || target == null)
        {
            return LocalPathResult.Fail();
        }

        if (start == target)
        {
            return LocalPathResult.Success(Array.Empty<PathStep>(), 0);
        }

        var maxNodes = useLongRange ? profile.MaxNodesLong : profile.MaxNodesShort;
        var open = new PriorityQueuePreview<PathNode>(128, PathNodeComparer.Instance);
        var visited = new Dictionary<int, PathNode>(256);
        var startNode = new PathNode(start, null, MovementMethod.Walk, 0, Heuristic(start, target));

        open.Enqueue(startNode);
        visited[start.data.tile_id] = startNode;

        while (open.Count > 0 && visited.Count < maxNodes)
        {
            token.ThrowIfCancellationRequested();
            var current = open.Dequeue();
            if (current.Tile == target)
            {
                return BuildResult(current, profile);
            }

            var neighbours = current.Tile.neighbours;
            if (neighbours == null)
            {
                continue;
            }

            foreach (var neighbour in neighbours)
            {
                if (neighbour == null)
                {
                    continue;
                }

                if (!IsTileAllowed(neighbour, profile))
                {
                    continue;
                }

                var method = DecideMethod(neighbour, profile);
                var stepCost = StepCost(current.Tile, neighbour, method, profile);
                var tentativeG = current.G + stepCost;
                var tileId = neighbour.data.tile_id;

                if (visited.TryGetValue(tileId, out var existing) && existing.G <= tentativeG)
                {
                    continue;
                }

                var node = new PathNode(neighbour, current, method, tentativeG, Heuristic(neighbour, target));
                visited[tileId] = node;
                open.Enqueue(node);
            }
        }

        return LocalPathResult.Fail();
    }

    private static bool IsTileAllowed(WorldTile tile, MovementProfile profile)
    {
        var type = tile.Type;
        if (type == null)
        {
            return false;
        }

        if (profile.IsBoat && !type.ocean)
        {
            return false;
        }

        if (type.block && !profile.IgnoreBlocks && !profile.AllowBlocks)
        {
            return false;
        }

        if (type.lava && !profile.AllowLava)
        {
            return false;
        }

        if (type.ocean && !profile.AllowOcean)
        {
            return false;
        }

        return true;
    }

    private LocalPathResult BuildResult(PathNode node, MovementProfile profile)
    {
        var reversed = new List<PathStep>();
        int maxSwimRun = 0;
        int swimRun = 0;
        var current = node;
        while (current.Parent != null)
        {
            if (current.Method == MovementMethod.Swim)
            {
                swimRun++;
                if (swimRun > maxSwimRun)
                {
                    maxSwimRun = swimRun;
                }
            }
            else
            {
                swimRun = 0;
            }

            reversed.Add(new PathStep(current.Tile, current.Method));
            current = current.Parent;
        }

        reversed.Reverse();

        var cost = node.G;
        if (maxSwimRun > profile.MaxSwimWidth)
        {
            cost += (maxSwimRun - profile.MaxSwimWidth) * profile.LongSwimPenalty;
        }

        return LocalPathResult.Success(reversed, cost);
    }

    private static float StepCost(WorldTile from, WorldTile to, MovementMethod method, MovementProfile profile)
    {
        var speed = method switch
        {
            MovementMethod.Swim => profile.SwimSpeed,
            MovementMethod.Sail => profile.SailSpeed,
            _ => profile.WalkSpeed
        };

        var dist = (from.x != to.x && from.y != to.y) ? 1.4142f : 1f;
        var time = dist / Mathf.Max(speed, 0.01f);

        return time;
    }

    private static MovementMethod DecideMethod(WorldTile tile, MovementProfile profile)
    {
        if (profile.IsBoat)
        {
            return MovementMethod.Swim;
        }

        return tile.Type.ocean ? MovementMethod.Swim : MovementMethod.Walk;
    }

    private static float Heuristic(WorldTile a, WorldTile b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static int DistTile(WorldTile a, WorldTile b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private sealed class MovementLeg : RouteLeg
    {
        public MovementLeg(IReadOnlyList<PathStep> steps, float cost)
        {
            Steps = steps;
            Cost = cost;
        }

        public IReadOnlyList<PathStep> Steps { get; }
        public float Cost { get; }
    }

    private sealed class PortalLeg : RouteLeg
    {
        public PortalLeg(PortalSnapshot entry, PortalSnapshot exit, float transferCost)
        {
            Entry = entry;
            Exit = exit;
            TransferCost = transferCost;
        }

        public PortalSnapshot Entry { get; }
        public PortalSnapshot Exit { get; }
        public float TransferCost { get; }
    }

    private sealed class RouteCandidate
    {
        private RouteCandidate(IReadOnlyList<RouteLeg> legs, float cost)
        {
            Legs = legs;
            Cost = cost;
        }

        public IReadOnlyList<RouteLeg> Legs { get; }
        public float Cost { get; }

        public static RouteCandidate FromSegments(IReadOnlyList<PathStep> steps, float cost)
        {
            return new RouteCandidate(new RouteLeg[] { new MovementLeg(steps, cost) }, cost);
        }

        public static RouteCandidate FromLegs(IReadOnlyList<RouteLeg> legs, float cost)
        {
            return new RouteCandidate(legs, cost);
        }
    }

    private abstract class RouteLeg;

    private sealed class PathNode
    {
        public PathNode(WorldTile tile, PathNode parent, MovementMethod method, float g, float h)
        {
            Tile = tile;
            Parent = parent;
            Method = method;
            G = g;
            H = h;
        }

        public WorldTile Tile { get; }
        public PathNode Parent { get; }
        public MovementMethod Method { get; }
        public float G { get; }
        public float H { get; }
        public float F => G + H;
    }

    private sealed class PathNodeComparer : IComparer<PathNode>
    {
        public static readonly PathNodeComparer Instance = new();
        public int Compare(PathNode x, PathNode y)
        {
            return x.F.CompareTo(y.F);
        }
    }

    private sealed class LocalPathResult
    {
        private LocalPathResult(bool success, IReadOnlyList<PathStep> steps, float cost)
        {
            IsSuccess = success;
            Steps = steps;
            Cost = cost;
        }

        public bool IsSuccess { get; }
        public IReadOnlyList<PathStep> Steps { get; }
        public float Cost { get; }

        public static LocalPathResult Fail()
        {
            return new LocalPathResult(false, Array.Empty<PathStep>(), float.MaxValue);
        }

        public static LocalPathResult Success(IReadOnlyList<PathStep> steps, float cost)
        {
            return new LocalPathResult(true, steps, cost);
        }
    }

    private sealed class MovementProfile
    {
        private MovementProfile()
        {
        }

        public bool AllowBlocks { get; private set; }
        public bool IgnoreBlocks { get; private set; }
        public bool AllowLava { get; private set; }
        public bool AllowOcean { get; private set; }
        public bool IsBoat { get; private set; }
        public int MaxSwimWidth { get; private set; }
        public int MaxNodesShort { get; private set; }
        public int MaxNodesLong { get; private set; }
        public float WalkSpeed { get; private set; }
        public float SwimSpeed { get; private set; }
        public float SailSpeed { get; private set; }
        public float LongSwimPenalty { get; private set; }

        public static MovementProfile Build(PathRequest request, PathfindingConfig config)
        {
            lock (PathFinder.ActorSyncLock)
            {
                var actor = request.Actor;
                var isWaterCreature = actor != null && actor.isWaterCreature();
                var inLiquid = actor?.current_tile?.is_liquid ?? false;
                var profile = new MovementProfile
                {
                    AllowBlocks = request.WalkOnBlocks,
                    IgnoreBlocks = actor != null && actor.ignoresBlocks(),
                    AllowLava = request.WalkOnLava || (actor != null && (actor.asset.die_in_lava == false || actor.isImmuneToFire())),
                    AllowOcean = request.PathOnWater || isWaterCreature || inLiquid,
                    IsBoat = actor != null && actor.asset.is_boat,
                    MaxSwimWidth = config.MaxSwimWidth,
                    MaxNodesShort = config.MaxNodesShort,
                    MaxNodesLong = config.MaxNodesLong,
                    LongSwimPenalty = config.LongSwimPenalty
                };

                if (actor != null && actor.isDamagedByOcean() && !request.PathOnWater)
                {
                    profile.AllowOcean = false;
                }

                var baseSpeed = actor?.stats?["speed"] ?? 5f;
                profile.WalkSpeed = Mathf.Max(0.1f, baseSpeed * config.WalkSpeedScale);
                profile.SwimSpeed = Mathf.Max(0.05f, profile.WalkSpeed * config.SwimSpeedScale);
                profile.SailSpeed = Mathf.Max(0.05f, profile.WalkSpeed * config.SailSpeedScale);

                if (profile.IsBoat)
                {
                profile.AllowOcean = true;
            }

            return profile;
        }
    }
    }
}

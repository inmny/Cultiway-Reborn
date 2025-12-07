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
        RouteCandidate bestCandidate = null;
        var bestCost = float.MaxValue;
        if (direct.IsSuccess)
        {
            bestCandidate = RouteCandidate.FromSegments(direct.Steps, direct.Cost);
            bestCost = direct.Cost;
        }
        if (!request.Actor.asset.is_boat)
        {
            var estimates = BuildPortalEstimates(request, profile);
            var bestEstimate = estimates.Count > 0 ? estimates.OrderBy(e => e.EstCost).First() : null;
            if (bestEstimate != null)
            {
                if (bestCandidate != null && bestEstimate.EstCost >= bestCost)
                {
                    EmitCandidate(bestCandidate, stream, token);
                    return;
                }

                token.ThrowIfCancellationRequested();

                var toEntry = TryBuildLocalPath(request.Start, bestEstimate.Entry.Tile, profile, useLongRange: true, token);
                if (!toEntry.IsSuccess)
                {
                    goto OUTSIDE;
                }
                var exitToTarget = TryBuildLocalPath(bestEstimate.Exit.Tile, request.Target, profile, useLongRange: true, token);
                if (!exitToTarget.IsSuccess)
                {
                    goto OUTSIDE;
                }
                var realCost = toEntry.Cost + bestEstimate.Entry.WaitTime + bestEstimate.Link.TravelTime +
                            bestEstimate.Exit.TransferTime + exitToTarget.Cost;
                if (realCost < bestCost)
                {
                    bestCost = realCost;
                    var legs = new List<RouteLeg>
                    {
                        new MovementLeg(toEntry.Steps, toEntry.Cost),
                        new PortalLeg(bestEstimate.Entry, bestEstimate.Exit,
                            bestEstimate.Entry.WaitTime + bestEstimate.Link.TravelTime + bestEstimate.Exit.TransferTime),
                        new MovementLeg(exitToTarget.Steps, exitToTarget.Cost)
                    };
                    bestCandidate = RouteCandidate.FromLegs(legs, realCost);
                }
            }
        }
        OUTSIDE:
        if (bestCandidate == null)
        {
            return;
        }

        EmitCandidate(bestCandidate, stream, token);
    }

    private List<PortalEstimate> BuildPortalEstimates(PathRequest request, MovementProfile profile)
    {
        var portals = _registry.Snapshot();
        var estimates = new List<PortalEstimate>();
        if (portals.Count == 0)
        {
            return estimates;
        }

        var portalDict = portals.ToDictionary(p => p.Id, p => p);
        var startTile = request.Start;
        var targetTile = request.Target;

        var nearStart = portals
            .OrderBy(p => DistTile(startTile, p.Tile))
            .Take(_config.PortalCandidates)
            .ToList();

        var nearEndIds = new HashSet<long>(portals.Select(p => p.Id));
        foreach (var entry in nearStart)
        {
            if (DistTile(startTile, entry.Tile) > _config.PortalSearchRadius)
            {
                continue;
            }

            foreach (var link in entry.Connections.OrderBy(c => c.TravelTime))
            {
                if (!portalDict.TryGetValue(link.TargetId, out var exit))
                {
                    continue;
                }

                if (!nearEndIds.Contains(exit.Id))
                {
                    continue;
                }

                var entryDist = DistTile(startTile, entry.Tile);
                var exitDist = DistTile(exit.Tile, targetTile);
                var walkSpeed = Mathf.Max(profile.WalkSpeed, 0.01f);
                var estEntryCost = entryDist / walkSpeed;
                var estExitCost = exitDist / walkSpeed;
                var estCost = estEntryCost + entry.WaitTime + link.TravelTime + exit.TransferTime + estExitCost;

                estimates.Add(new PortalEstimate(entry, exit, link, estCost));
            }
        }

        return estimates;
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
                        stream.AddStep(step.Tile, step.Method, step.Penalty);
                    }

                    break;
                case PortalLeg portal:
                    // 传送/乘船在客户端处理，默认用 Sail 占位
                    stream.AddStep(portal.Exit.Tile, MovementMethod.Portal, StepPenalty.Block | StepPenalty.Ocean | StepPenalty.Block);
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
        var startNode = new PathNode(start, null, MovementMethod.Walk, 0, Heuristic(start, target), StepPenalty.None);

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
                var stepCost = StepCost(current.Tile, neighbour, method, profile, out var penalty);
                var tentativeG = current.G + stepCost;
                var tileId = neighbour.data.tile_id;

                if (visited.TryGetValue(tileId, out var existing) && existing.G <= tentativeG)
                {
                    continue;
                }

                var node = new PathNode(neighbour, current, method, tentativeG, Heuristic(neighbour, target), penalty);
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

            reversed.Add(new PathStep(current.Tile, current.Method, current.Penalty));
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

    private static float StepCost(WorldTile from, WorldTile to, MovementMethod method, MovementProfile profile,
        out StepPenalty penalty)
    {
        penalty = StepPenalty.None;
        var speed = method switch
        {
            MovementMethod.Swim => profile.SwimSpeed,
            MovementMethod.Portal => profile.SailSpeed,
            _ => profile.WalkSpeed
        };

        var dist = (from.x != to.x && from.y != to.y) ? 1.4142f : 1f;
        var time = dist / Mathf.Max(speed, 0.01f);
        var multiplier = 1f;
        var type = to.Type;
        if (type != null)
        {
            if (!profile.IgnoreBlocks && type.block)
            {
                multiplier *= profile.BlockCostMultiplier;
                penalty |= StepPenalty.Block;
            }

            if (type.lava)
            {
                multiplier *= profile.LavaCostMultiplier;
                penalty |= StepPenalty.Lava;
            }

            if (type.ocean)
            {
                multiplier *= profile.OceanCostMultiplier;
                penalty |= StepPenalty.Ocean;
            }
        }

        return time * multiplier;
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

    private sealed class PortalEstimate
    {
        public PortalEstimate(PortalSnapshot entry, PortalSnapshot exit, PortalConnection link, float estCost)
        {
            Entry = entry;
            Exit = exit;
            Link = link;
            EstCost = estCost;
        }

        public PortalSnapshot Entry { get; }
        public PortalSnapshot Exit { get; }
        public PortalConnection Link { get; }
        public float EstCost { get; }
    }

    private sealed class PathNode
    {
        public PathNode(WorldTile tile, PathNode parent, MovementMethod method, float g, float h, StepPenalty penalty)
        {
            Tile = tile;
            Parent = parent;
            Method = method;
            G = g;
            H = h;
            Penalty = penalty;
        }

        public WorldTile Tile { get; }
        public PathNode Parent { get; }
        public MovementMethod Method { get; }
        public StepPenalty Penalty { get; }
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

        public bool IgnoreBlocks { get; private set; }
        public bool IsBoat { get; private set; }
        public int MaxSwimWidth { get; private set; }
        public int MaxNodesShort { get; private set; }
        public int MaxNodesLong { get; private set; }
        public float WalkSpeed { get; private set; }
        public float SwimSpeed { get; private set; }
        public float SailSpeed { get; private set; }
        public float LongSwimPenalty { get; private set; }
        public float BlockCostMultiplier { get; private set; }
        public float LavaCostMultiplier { get; private set; }
        public float OceanCostMultiplier { get; private set; }

        public static MovementProfile Build(PathRequest request, PathfindingConfig config)
        {
            lock (PathFinder.ActorSyncLock)
            {
                var actor = request.Actor;
                var isWaterCreature = actor != null && actor.isWaterCreature();
                var inLiquid = actor?.current_tile?.is_liquid ?? false;
                var ignoreBlocks = actor != null && actor.ignoresBlocks();
                var allowBlocks = request.WalkOnBlocks;
                var allowLava = request.WalkOnLava || (actor != null && (actor.asset.die_in_lava == false || actor.isImmuneToFire()));
                var allowOcean = request.PathOnWater || isWaterCreature || inLiquid;
                var isBoat = actor != null && actor.asset.is_boat;
                if (isBoat)
                {
                    allowOcean = true;
                }

                var profile = new MovementProfile
                {
                    IgnoreBlocks = ignoreBlocks,
                    IsBoat = isBoat,
                    MaxSwimWidth = config.MaxSwimWidth,
                    MaxNodesShort = config.MaxNodesShort,
                    MaxNodesLong = config.MaxNodesLong,
                    LongSwimPenalty = config.LongSwimPenalty
                };

                if (actor != null && actor.isDamagedByOcean() && !request.PathOnWater)
                {
                    allowOcean = false;
                }

                var baseSpeed = actor?.stats?["speed"] ?? 5f;
                profile.WalkSpeed = Mathf.Max(0.1f, baseSpeed * config.WalkSpeedScale);
                profile.SwimSpeed = Mathf.Max(0.05f, profile.WalkSpeed * config.SwimSpeedScale);
                profile.SailSpeed = Mathf.Max(0.05f, profile.WalkSpeed * config.SailSpeedScale);

                profile.BlockCostMultiplier = (profile.IgnoreBlocks || allowBlocks) ? 1f : 10f;
                profile.LavaCostMultiplier = allowLava ? 1f : 10f;
                profile.OceanCostMultiplier = allowOcean ? 1f : 10f;

                return profile;
            }
        }
    }
}

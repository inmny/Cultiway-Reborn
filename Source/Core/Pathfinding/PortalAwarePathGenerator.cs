using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Const;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Core.Pathfinding;

public class PortalAwarePathGenerator : IPathGenerator
{
    [ThreadStatic]
    private static List<PathStep> directPathBuffer;
    [ThreadStatic]
    private static FastPathWorkspace fastPathWorkspace;
    [ThreadStatic]
    private static FullPathWorkspace fullPathWorkspace;

    private readonly PortalRegistry _registry;
    private readonly PathfindingConfig _config;
    private long directAttempts;
    private long directHits;
    private long directSteps;
    private long fullSearches;
    private long directHitTicks;
    private long fullSearchTicks;
    private long maximumDirectHitTicks;
    private long maximumFullSearchTicks;
    private long fastPathAttempts;
    private long fastPathHits;
    private long fastPathAttemptTicks;
    private long maximumFastPathAttemptTicks;

    public PortalAwarePathGenerator(PortalRegistry registry, PathfindingConfig config)
    {
        _registry = registry ?? PortalRegistry.Instance;
        _config = config ?? PathfindingConfig.Default;
    }

    public Task GenerateAsync(PathRequest request, IPathStreamWriter stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.StartTileId < 0)
        {
            stream.Fail(PathFailureReason.InvalidStart);
            return Task.CompletedTask;
        }

        if (request.TargetTileId < 0)
        {
            stream.Fail(PathFailureReason.InvalidTarget);
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
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
            stream.Fail(PathFailureReason.GeneratorException, e);
        }

        stream.EnsureCompleted();
        return Task.CompletedTask;
    }

    private void GenerateInternal(PathRequest request, IPathStreamWriter stream, CancellationToken token)
    {
        long startedAt = Stopwatch.GetTimestamp();
        var profile = MovementProfile.Build(request, _config);
        Interlocked.Increment(ref directAttempts);
        if (TryBuildDirectPath(
                request,
                profile,
                token,
                out List<PathStep> directPath))
        {
            for (int i = 0; i < directPath.Count; i++)
            {
                stream.AddStep(directPath[i]);
            }

            Interlocked.Increment(ref directHits);
            Interlocked.Add(ref directSteps, directPath.Count);
            long elapsedTicks =
                Stopwatch.GetTimestamp() - startedAt;
            Interlocked.Add(ref directHitTicks, elapsedTicks);
            UpdateMaximum(
                ref maximumDirectHitTicks,
                elapsedTicks);
            return;
        }

        Interlocked.Increment(ref fastPathAttempts);
        long fastPathStartedAt = Stopwatch.GetTimestamp();
        bool foundFastPath = TryBuildFastPath(
            request,
            profile,
            token,
            out List<PathStep> fastPath);
        long fastPathElapsedTicks =
            Stopwatch.GetTimestamp() - fastPathStartedAt;
        Interlocked.Add(
            ref fastPathAttemptTicks,
            fastPathElapsedTicks);
        UpdateMaximum(
            ref maximumFastPathAttemptTicks,
            fastPathElapsedTicks);
        if (foundFastPath)
        {
            Interlocked.Increment(ref fastPathHits);
            for (int i = 0; i < fastPath.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                stream.AddStep(fastPath[i]);
            }

            return;
        }

        if (_registry.IsEmpty)
        {
            // 无传送门时，单标签搜索已经完整遍历所有可通行地块；
            // 多标签只会重复同一拓扑搜索，无法创造新的连通路线。
            stream.Fail(PathFailureReason.Unreachable);
            return;
        }

        Interlocked.Increment(ref fullSearches);
        try
        {
            var direct = TryBuildLocalPath(
                request,
                request.StartTileId,
                request.TargetTileId,
                profile,
                useLongRange: true,
                token);
            RouteCandidate bestCandidate = null;
            var failureReason = FailureReasonFrom(direct);
            var bestCost = float.MaxValue;
            if (direct.IsSuccess)
            {
                bestCandidate = RouteCandidate.FromSegments(direct.Steps, direct.Cost);
                bestCost = direct.Cost;
                failureReason = PathFailureReason.None;
            }

            if (!profile.IsBoat)
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

                    var toEntry = TryBuildLocalPath(
                        request,
                        request.StartTileId,
                        TileTraversalInfo.TileIdOf(
                            bestEstimate.Entry.Tile),
                        profile,
                        useLongRange: true,
                        token);
                    if (!toEntry.IsSuccess)
                    {
                        failureReason = MoreSpecificFailure(failureReason, FailureReasonFrom(toEntry));
                        goto OUTSIDE;
                    }

                    var exitToTarget = TryBuildLocalPath(
                        request,
                        TileTraversalInfo.TileIdOf(
                            bestEstimate.Exit.Tile),
                        request.TargetTileId,
                        profile,
                        useLongRange: true,
                        token);
                    if (!exitToTarget.IsSuccess)
                    {
                        failureReason = MoreSpecificFailure(failureReason, FailureReasonFrom(exitToTarget));
                        goto OUTSIDE;
                    }

                    var portalCost = bestEstimate.Entry.WaitTime + bestEstimate.Link.TravelTime + bestEstimate.Exit.TransferTime;
                    var realCost = toEntry.Cost + portalCost + exitToTarget.Cost;
                    if (realCost < bestCost)
                    {
                        bestCost = realCost;
                        var legs = new List<RouteLeg>
                        {
                            new MovementLeg(toEntry.Steps, toEntry.Cost),
                            new PortalLeg(bestEstimate.Entry, bestEstimate.Exit, portalCost),
                            new MovementLeg(exitToTarget.Steps, exitToTarget.Cost)
                        };
                        bestCandidate = RouteCandidate.FromLegs(legs, realCost);
                    }
                }
            }

            OUTSIDE:
            if (bestCandidate == null)
            {
                stream.Fail(failureReason == PathFailureReason.None ? PathFailureReason.Unreachable : failureReason);
                return;
            }

            EmitCandidate(bestCandidate, stream, token);
        }
        finally
        {
            long elapsedTicks =
                Stopwatch.GetTimestamp() - startedAt;
            Interlocked.Add(ref fullSearchTicks, elapsedTicks);
            UpdateMaximum(
                ref maximumFullSearchTicks,
                elapsedTicks);
        }
    }

    internal string GetDiagnostics()
    {
        long attempts = Interlocked.Read(ref directAttempts);
        long hits = Interlocked.Read(ref directHits);
        return string.Format(
            CultureInfo.InvariantCulture,
            "direct={0}/{1}({2:0.0}%) steps={3}" +
            " safe={4}/{5} time={6:0.000}/{7:0.00}ms(avg/max)" +
            " full={8} time={9:0.000}ms(avg) max={10:0.00}ms" +
            " direct_time={11:0.000}/{12:0.00}ms(avg/max)",
            hits,
            attempts,
            attempts == 0L
                ? 0.0
                : hits * 100.0 / attempts,
            Interlocked.Read(ref directSteps),
            Interlocked.Read(ref fastPathHits),
            Interlocked.Read(ref fastPathAttempts),
            AverageMilliseconds(
                Interlocked.Read(ref fastPathAttemptTicks),
                Interlocked.Read(ref fastPathAttempts)),
            TicksToMilliseconds(
                Interlocked.Read(ref maximumFastPathAttemptTicks)),
            Interlocked.Read(ref fullSearches),
            AverageMilliseconds(
                Interlocked.Read(ref fullSearchTicks),
                Interlocked.Read(ref fullSearches)),
            TicksToMilliseconds(
                Interlocked.Read(ref maximumFullSearchTicks)),
            AverageMilliseconds(
                Interlocked.Read(ref directHitTicks),
                hits),
            TicksToMilliseconds(
                Interlocked.Read(ref maximumDirectHitTicks)));
    }

    private static double AverageMilliseconds(
        long ticks,
        long count)
    {
        return count <= 0L
            ? 0.0
            : TicksToMilliseconds(ticks) / count;
    }

    private static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }

    private static void UpdateMaximum(
        ref long target,
        long value)
    {
        long current = Volatile.Read(ref target);
        while (value > current)
        {
            long observed = Interlocked.CompareExchange(
                ref target,
                value,
                current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    /// <summary>
    /// 原版会先尝试无遮挡射线路径。这里只接受无环境伤害的同岛短路径；
    /// 任一格不满足条件就交回完整多标签 A*，不会截断或丢弃寻路请求。
    /// </summary>
    private bool TryBuildDirectPath(
        PathRequest request,
        MovementProfile profile,
        CancellationToken token,
        out List<PathStep> result)
    {
        result = directPathBuffer ??= new List<PathStep>(64);
        result.Clear();
        if (!TileTraversalInfo.TryGet(
                request.StartTileId,
                out TileTraversalInfo current) ||
            !TileTraversalInfo.TryGet(
                request.TargetTileId,
                out TileTraversalInfo target))
        {
            return false;
        }

        if (request.StartTileId == request.TargetTileId)
        {
            return true;
        }

        if (profile.IsFlying)
        {
            TraversalState flyingState =
                TraversalState.Start(profile);
            MovementMethod method =
                DecideMethod(target, profile);
            TraversalEstimate estimate =
                EstimateTraversal(
                    current,
                    target,
                    method,
                    flyingState,
                    profile);
            result.Add(new PathStep(
                target.TileId,
                method,
                estimate));
            return true;
        }

        WorldTile startTile =
            TileTraversalInfo.ResolveTile(request.StartTileId);
        WorldTile targetTile =
            TileTraversalInfo.ResolveTile(request.TargetTileId);
        if (startTile?.region?.island == null ||
            !ReferenceEquals(
                startTile.region.island,
                targetTile?.region?.island) ||
            DistTile(current, target) > _config.LongRangeTiles)
        {
            return false;
        }

        int x = current.X;
        int y = current.Y;
        int targetX = target.X;
        int targetY = target.Y;
        int deltaX = Math.Abs(targetX - x);
        int stepX = x < targetX ? 1 : -1;
        int deltaY = -Math.Abs(targetY - y);
        int stepY = y < targetY ? 1 : -1;
        int error = deltaX + deltaY;
        TraversalState state = TraversalState.Start(profile);
        while (x != targetX || y != targetY)
        {
            token.ThrowIfCancellationRequested();
            int doubledError = error * 2;
            if (doubledError >= deltaY)
            {
                error += deltaY;
                x += stepX;
            }

            if (doubledError <= deltaX)
            {
                error += deltaX;
                y += stepY;
            }

            if (!TileTraversalInfo.TryGetAt(
                    x,
                    y,
                    out TileTraversalInfo next) ||
                !IsSafeDirectTile(next, profile))
            {
                result.Clear();
                return false;
            }

            MovementMethod method =
                DecideMethod(next, profile);
            TraversalEstimate estimate =
                EstimateTraversal(
                    current,
                    next,
                    method,
                    state,
                    profile);
            state = state.Advance(estimate, profile);
            if (state.Health <= 0f)
            {
                result.Clear();
                return false;
            }

            result.Add(new PathStep(
                next.TileId,
                method,
                estimate));
            current = next;
        }

        return true;
    }

    private static bool IsSafeDirectTile(
        TileTraversalInfo tile,
        MovementProfile profile)
    {
        if (!tile.HasType ||
            tile.Block ||
            tile.Lava ||
            tile.DamageUnits ||
            (tile.IsOnFire && !profile.IsFireImmune))
        {
            return false;
        }

        if (profile.IsBoat)
        {
            return tile.Ocean ||
                   (tile.Liquid && !tile.Lava);
        }

        if (!tile.Liquid && !tile.Ocean)
        {
            return true;
        }

        return profile.IsWaterCreature &&
               !profile.IsDamagedByOcean;
    }

    /// <summary>
    /// 原版允许寻路不保证最优。直线失败后先用线程本地数组执行权重 A*，
    /// 并严格应用原版的地面、海洋、障碍、熔岩和火焰通行约束。
    /// 找不到路线时仍交给多标签搜索处理复杂环境状态与传送门语义。
    /// </summary>
    private static bool TryBuildFastPath(
        PathRequest request,
        MovementProfile profile,
        CancellationToken token,
        out List<PathStep> result)
    {
        FastPathWorkspace workspace =
            fastPathWorkspace ??= new FastPathWorkspace();
        result = workspace.Result;
        result.Clear();

        WorldTile[] tiles = World.world?.tiles_list;
        if (tiles == null ||
            request.StartTileId < 0 ||
            request.TargetTileId < 0 ||
            request.StartTileId >= tiles.Length ||
            request.TargetTileId >= tiles.Length)
        {
            return false;
        }

        workspace.BeginSearch(tiles.Length);
        if (!workspace.TryGetTileInfo(
                request.StartTileId,
                out TileTraversalInfo startInfo) ||
            !workspace.TryGetTileInfo(
                request.TargetTileId,
                out TileTraversalInfo targetInfo))
        {
            return false;
        }

        TraversalRules rules =
            TraversalRules.Build(
                request,
                startInfo,
                targetInfo);
        if (!rules.CanReachTarget ||
            !rules.CanTraverse(targetInfo))
        {
            return false;
        }

        TraversalState startState =
            TraversalState.Start(profile);
        workspace.SetBest(
            request.StartTileId,
            0f,
            -1,
            MovementMethod.Walk,
            default,
            startState);
        workspace.Enqueue(
            new SafeOpenNode(
                request.StartTileId,
                0f,
                Heuristic(
                    startInfo,
                    targetInfo,
                    profile) * 2f));

        int expanded = 0;
        int maximumExpanded = tiles.Length;
        while (workspace.OpenCount > 0 &&
               expanded < maximumExpanded)
        {
            token.ThrowIfCancellationRequested();
            SafeOpenNode openNode = workspace.Dequeue();
            if (!workspace.IsCurrentBest(
                    openNode.TileId,
                    openNode.G) ||
                !workspace.TryClose(
                    openNode.TileId))
            {
                continue;
            }

            expanded++;
            if (openNode.TileId == request.TargetTileId)
            {
                return workspace.BuildResult(
                    request.StartTileId,
                    request.TargetTileId);
            }

            if (!workspace.TryGetTileInfo(
                    openNode.TileId,
                    out TileTraversalInfo currentInfo))
            {
                continue;
            }

            WorldTile currentTile =
                TileTraversalInfo.ResolveTile(openNode.TileId);
            WorldTile[] neighbours =
                currentTile?.neighboursAll ??
                currentTile?.neighbours;
            if (neighbours == null)
            {
                continue;
            }

            TraversalState currentState =
                workspace.GetState(openNode.TileId);
            for (int i = 0; i < neighbours.Length; i++)
            {
                int neighbourId =
                    TileTraversalInfo.TileIdOf(neighbours[i]);
                if (workspace.IsClosed(neighbourId) ||
                    !workspace.TryGetTileInfo(
                        neighbourId,
                        out TileTraversalInfo neighbour) ||
                    !rules.CanTraverse(neighbour) ||
                    IsDiagonalOutsideMap(
                        currentInfo,
                        neighbour))
                {
                    continue;
                }

                MovementMethod method =
                    DecideMethod(neighbour, profile);
                TraversalEstimate estimate =
                    EstimateTraversal(
                        currentInfo,
                        neighbour,
                        method,
                        currentState,
                        profile);
                TraversalState nextState =
                    currentState.Advance(
                        estimate,
                        profile);
                float nextG =
                    openNode.G +
                    profile.CostOf(
                        estimate,
                        nextState);
                if (!workspace.TryImprove(
                        neighbourId,
                        nextG,
                        openNode.TileId,
                        method,
                        estimate,
                        nextState))
                {
                    continue;
                }

                workspace.Enqueue(
                    new SafeOpenNode(
                        neighbourId,
                        nextG,
                        nextG +
                        Heuristic(
                            neighbour,
                            targetInfo,
                            profile) * 2f));
            }
        }

        result.Clear();
        return false;
    }

    private static PathFailureReason FailureReasonFrom(LocalPathResult result)
    {
        return result.HitNodeLimit ? PathFailureReason.SearchLimitExceeded : PathFailureReason.Unreachable;
    }

    private static PathFailureReason MoreSpecificFailure(PathFailureReason current, PathFailureReason next)
    {
        if (current == PathFailureReason.None)
        {
            return next;
        }

        if (next == PathFailureReason.SearchLimitExceeded)
        {
            return next;
        }

        return current;
    }

    private List<PortalEstimate> BuildPortalEstimates(PathRequest request, MovementProfile profile)
    {
        if (!TileTraversalInfo.TryGet(request.StartTileId, out var startInfo) ||
            !TileTraversalInfo.TryGet(request.TargetTileId, out var targetInfo))
        {
            return new List<PortalEstimate>();
        }

        var estimates = new List<PortalEstimate>();

        var nearStart = _registry.Enumerate()
            .Where(p => TileTraversalInfo.TileIdOf(p.Tile) >= 0)
            .OrderBy(p => DistTile(startInfo, p))
            .Take(_config.PortalCandidates)
            .ToArray();
        foreach (var entry in nearStart)
        {
            if (DistTile(startInfo, entry) > _config.PortalSearchRadius)
            {
                continue;
            }

            foreach (var link in entry.Connections.OrderBy(c => c.TravelTime))
            {
                if (!_registry.TryGet(link.TargetId, out var exit))
                {
                    continue;
                }

                var entryDist = DistTile(startInfo, entry);
                var exitDist = DistTile(targetInfo, exit);
                var estEntryCost = profile.EstimateOpenTerrainCost(entryDist);
                var estExitCost = profile.EstimateOpenTerrainCost(exitDist);
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
                        stream.AddStep(step);
                    }

                    break;
                case PortalLeg portal:
                    stream.AddStep(new PathStep(TileTraversalInfo.TileIdOf(portal.Exit.Tile), MovementMethod.Portal,
                        TraversalEstimate.Portal(portal.TransferCost), portal.Entry, portal.Exit));
                    break;
            }
        }
    }

    private LocalPathResult TryBuildLocalPath(
        PathRequest request,
        int startId,
        int targetId,
        MovementProfile profile,
        bool useLongRange,
        CancellationToken token)
    {
        if (startId == targetId)
        {
            return LocalPathResult.Success(Array.Empty<PathStep>(), 0);
        }

        WorldTile[] tiles = World.world?.tiles_list;
        if (tiles == null)
        {
            return LocalPathResult.Fail();
        }

        FullPathWorkspace workspace =
            fullPathWorkspace ??= new FullPathWorkspace();
        workspace.BeginLocalPath(
            tiles.Length,
            profile.MaxLabelsPerTile);
        if (!workspace.TryGetTileInfo(startId, out var startInfo) ||
            !workspace.TryGetTileInfo(targetId, out var targetInfo))
        {
            return LocalPathResult.Fail();
        }

        TraversalRules rules =
            TraversalRules.Build(
                request,
                startInfo,
                targetInfo);
        if (!rules.CanReachTarget ||
            !rules.CanTraverse(targetInfo))
        {
            return LocalPathResult.Fail();
        }

        var maxNodes = useLongRange ? profile.MaxNodesLong : profile.MaxNodesShort;
        var result = TryBuildLocalPathCore(startId, targetId, startInfo, targetInfo, workspace, profile, rules, maxNodes,
            corridorLimit: 0, token);
        if (result.IsSuccess || !useLongRange || !result.HitNodeLimit)
        {
            return result;
        }

        var directDistance = DistTile(startInfo, targetInfo);
        var detour = Mathf.Max(profile.FallbackCorridorMinDetour,
            Mathf.RoundToInt(directDistance * profile.FallbackCorridorDetourScale));
        var fallbackNodes = Mathf.Max(profile.MaxNodesLongFallback, profile.MaxNodesLong);
        return TryBuildLocalPathCore(startId, targetId, startInfo, targetInfo, workspace, profile, rules, fallbackNodes,
            directDistance + detour, token);
    }

    private LocalPathResult TryBuildLocalPathCore(int startId, int targetId, TileTraversalInfo startInfo,
        TileTraversalInfo targetInfo, FullPathWorkspace workspace, MovementProfile profile, TraversalRules rules,
        int maxNodes, int corridorLimit, CancellationToken token)
    {
        maxNodes = Mathf.Max(1, maxNodes);
        workspace.BeginGraphSearch();
        int startNode = workspace.AddStart(
            startId,
            TraversalState.Start(profile),
            Heuristic(startInfo, targetInfo, profile));
        workspace.Enqueue(startNode);

        var expanded = 0;
        while (workspace.OpenCount > 0 && expanded < maxNodes)
        {
            token.ThrowIfCancellationRequested();
            int currentIndex = workspace.Dequeue();
            if (!workspace.IsActive(currentIndex))
            {
                continue;
            }

            FullPathNode current =
                workspace.GetNode(currentIndex);
            expanded++;
            if (current.TileId == targetId)
            {
                return LocalPathResult.Success(
                    workspace.BuildResult(currentIndex),
                    current.G);
            }

            if (!workspace.TryGetTileInfo(
                    current.TileId,
                    out var currentInfo))
            {
                continue;
            }

            var currentTile = TileTraversalInfo.ResolveTile(current.TileId);
            var neighbours = currentTile?.neighboursAll ?? currentTile?.neighbours;
            if (neighbours == null || neighbours.Length == 0)
            {
                continue;
            }

            for (int i = 0; i < neighbours.Length; i++)
            {
                var neighbourId = TileTraversalInfo.TileIdOf(neighbours[i]);
                if (!workspace.TryGetTileInfo(
                        neighbourId,
                        out var neighbour) ||
                    !rules.CanTraverse(neighbour))
                {
                    continue;
                }

                if (IsDiagonalOutsideMap(currentInfo, neighbour))
                {
                    continue;
                }

                if (corridorLimit > 0 && DistTile(startInfo, neighbour) + DistTile(neighbour, targetInfo) > corridorLimit)
                {
                    continue;
                }

                var method = DecideMethod(neighbour, profile);
                var estimate = EstimateTraversal(currentInfo, neighbour, method, current.State, profile);
                var nextState = current.State.Advance(estimate, profile);
                var stepCost = profile.CostOf(estimate, nextState);
                int nodeIndex = workspace.TryAddLabel(
                    neighbourId,
                    currentIndex,
                    method,
                    estimate,
                    nextState,
                    current.G + stepCost,
                    Heuristic(
                        neighbour,
                        targetInfo,
                        profile),
                    profile.MaxLabelsPerTile);
                if (nodeIndex < 0)
                {
                    continue;
                }

                workspace.Enqueue(nodeIndex);
            }
        }

        return LocalPathResult.Fail(
            workspace.OpenCount > 0 &&
            expanded >= maxNodes);
    }

    private static bool IsDiagonalOutsideMap(TileTraversalInfo from, TileTraversalInfo to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) != 1 || Math.Abs(dy) != 1)
        {
            return false;
        }

        return !TileTraversalInfo.TryGetAt(from.X + dx, from.Y, out _) ||
               !TileTraversalInfo.TryGetAt(from.X, from.Y + dy, out _);
    }

    private static TraversalEstimate EstimateTraversal(TileTraversalInfo from, TileTraversalInfo to,
        MovementMethod method, TraversalState state, MovementProfile profile)
    {
        var hazards = HazardFlags.None;
        var dist = (from.X != to.X && from.Y != to.Y) ? 1.4142f : 1f;
        var speed = profile.GetSpeed(to, method, state);
        var time = dist / Mathf.Max(speed, 0.01f);
        var staminaCost = 0f;
        var healthCost = 0f;
        var riskCost = 0f;

        if (to.Block)
        {
            hazards |= HazardFlags.Block;
            if (!profile.IgnoreBlocks)
            {
                riskCost += profile.BlockRiskCost;
                if (profile.DieOnBlocks)
                {
                    healthCost += profile.EstimateEnvironmentalDamage(time * profile.BlockDamagePerSecond);
                }
            }
        }

        if (to.Ocean || (to.Liquid && !to.Lava))
        {
            hazards |= HazardFlags.Ocean;
            if (!profile.IsWaterCreature && !profile.IsFlying)
            {
                hazards |= HazardFlags.StaminaDrain;
                staminaCost += time * profile.WaterStaminaDrainPerSecond;
                riskCost += profile.OceanRiskCost;
                var exhausted = Mathf.Max(0f, staminaCost - state.Stamina);
                if (exhausted > 0f && profile.WaterStaminaDrainPerSecond > 0f)
                {
                    hazards |= HazardFlags.Drowning;
                    healthCost += profile.EstimateEnvironmentalDamage(
                        exhausted / profile.WaterStaminaDrainPerSecond * profile.DrowningDamagePerSecond);
                }
            }

            if (profile.IsDamagedByOcean && to.Ocean)
            {
                healthCost += profile.EstimateEnvironmentalDamage(time * profile.WaterDamagePerSecond);
                riskCost += profile.OceanRiskCost;
            }
        }

        if (to.Lava)
        {
            hazards |= HazardFlags.Lava;
            riskCost += profile.LavaRiskCost;
        }

        if (to.IsOnFire && !profile.IsFireImmune)
        {
            hazards |= HazardFlags.Fire;
            riskCost += profile.FireRiskCost;
        }

        if (to.DamageUnits && (!to.Lava || profile.IsLavaDamaging))
        {
            hazards |= HazardFlags.TerrainDamage;
            var damage = time * to.Damage * profile.TerrainDamageTicksPerSecond;
            healthCost += profile.EstimateEnvironmentalDamage(damage);
            riskCost += profile.TerrainDamageRiskCost;
        }

        var healthAfter = state.Health - healthCost;
        if (healthAfter <= profile.LowHealthThreshold)
        {
            hazards |= HazardFlags.LowHealth;
        }

        return new TraversalEstimate(time, staminaCost, healthCost, riskCost, hazards);
    }

    private static MovementMethod DecideMethod(TileTraversalInfo tile, MovementProfile profile)
    {
        if (profile.IsBoat)
        {
            return MovementMethod.Swim;
        }

        return tile.Liquid ? MovementMethod.Swim : MovementMethod.Walk;
    }

    private static float Heuristic(TileTraversalInfo a, TileTraversalInfo b, MovementProfile profile)
    {
        return DistTile(a, b) / Mathf.Max(profile.BestCaseSpeed, 0.01f);
    }

    private static int DistTile(TileTraversalInfo a, TileTraversalInfo b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);
    }

    private static int DistTile(TileTraversalInfo a, PortalDefinition b)
    {
        if (!TileTraversalInfo.TryCreate(b.Tile, out var info))
        {
            return int.MaxValue;
        }

        return Mathf.Abs(a.X - info.X) + Mathf.Abs(a.Y - info.Y);
    }

    private readonly struct TraversalRules
    {
        private TraversalRules(
            bool isBoat,
            bool allowGround,
            bool allowOcean,
            bool allowBlocks,
            bool allowLava,
            bool allowFire,
            bool canReachTarget)
        {
            IsBoat = isBoat;
            AllowGround = allowGround;
            AllowOcean = allowOcean;
            AllowBlocks = allowBlocks;
            AllowLava = allowLava;
            AllowFire = allowFire;
            CanReachTarget = canReachTarget;
        }

        private bool IsBoat { get; }
        private bool AllowGround { get; }
        private bool AllowOcean { get; }
        private bool AllowBlocks { get; }
        private bool AllowLava { get; }
        private bool AllowFire { get; }
        internal bool CanReachTarget { get; }

        internal static TraversalRules Build(
            PathRequest request,
            TileTraversalInfo start,
            TileTraversalInfo target)
        {
            bool startInWater =
                start.Ocean ||
                (start.Liquid && !start.Lava);
            bool allowOcean =
                request.ActorIsWaterCreature ||
                startInWater ||
                request.PathOnWater &&
                !request.ActorIsDamagedByOcean;
            WorldTile startTile =
                TileTraversalInfo.ResolveTile(
                    start.TileId);
            WorldTile targetTile =
                TileTraversalInfo.ResolveTile(
                    target.TileId);
            TileIsland startIsland =
                startTile?.region?.island;
            TileIsland targetIsland =
                targetTile?.region?.island;
            bool sameIsland =
                startIsland != null &&
                ReferenceEquals(
                    startIsland,
                    targetIsland);
            if (sameIsland &&
                !startInWater &&
                !request.ActorIsWaterCreature)
            {
                // 与原版 ActorMove.goTo 一致：陆地单位在同岛移动时
                // 不会仅因调用方传入 pathOnWater 就横穿海洋。
                allowOcean = false;
            }

            bool islandsConnected =
                sameIsland ||
                startIsland != null &&
                targetIsland != null &&
                startIsland.isConnectedWith(
                    targetIsland);
            bool canReachTarget =
                islandsConnected ||
                allowOcean ||
                request.ActorIsBoat;
            return new TraversalRules(
                request.ActorIsBoat,
                !request.ActorIsWaterCreature ||
                request.ActorForceLandCreature ||
                !startInWater,
                allowOcean,
                request.WalkOnBlocks ||
                request.ActorIgnoresBlocks,
                request.WalkOnLava ||
                request.ActorIsFireImmune ||
                start.Lava,
                request.ActorIsFireImmune ||
                start.IsOnFire,
                canReachTarget);
        }

        internal bool CanTraverse(
            TileTraversalInfo tile)
        {
            if (!tile.HasType ||
                tile.IsOnFire &&
                !AllowFire)
            {
                return false;
            }

            if (IsBoat)
            {
                return !tile.Lava &&
                       (tile.Ocean ||
                        tile.Liquid);
            }

            if (tile.Block)
            {
                return AllowBlocks;
            }

            if (tile.Lava)
            {
                return AllowLava;
            }

            if (tile.Ocean ||
                tile.Liquid)
            {
                return AllowOcean;
            }

            return tile.Ground &&
                   AllowGround;
        }
    }

    private readonly struct SafeOpenNode
    {
        public SafeOpenNode(
            int tileId,
            float g,
            float f)
        {
            TileId = tileId;
            G = g;
            F = f;
        }

        public int TileId { get; }
        public float G { get; }
        public float F { get; }
    }

    private sealed class FastPathWorkspace
    {
        private int generation;
        private int[] tileInfoGenerations = Array.Empty<int>();
        private TileTraversalInfo[] tileInfos =
            Array.Empty<TileTraversalInfo>();
        private int[] bestGenerations = Array.Empty<int>();
        private int[] closedGenerations =
            Array.Empty<int>();
        private float[] bestCosts = Array.Empty<float>();
        private int[] parents = Array.Empty<int>();
        private MovementMethod[] methods =
            Array.Empty<MovementMethod>();
        private TraversalEstimate[] estimates =
            Array.Empty<TraversalEstimate>();
        private TraversalState[] states =
            Array.Empty<TraversalState>();
        private SafeOpenNode[] open =
            new SafeOpenNode[256];

        internal List<PathStep> Result { get; } = new(64);
        internal int OpenCount { get; private set; }

        internal void BeginSearch(int tileCount)
        {
            EnsureCapacity(tileCount);
            generation++;
            if (generation == int.MaxValue)
            {
                Array.Clear(
                    tileInfoGenerations,
                    0,
                    tileInfoGenerations.Length);
                Array.Clear(
                    bestGenerations,
                    0,
                    bestGenerations.Length);
                Array.Clear(
                    closedGenerations,
                    0,
                    closedGenerations.Length);
                generation = 1;
            }

            OpenCount = 0;
            Result.Clear();
        }

        internal bool TryGetTileInfo(
            int tileId,
            out TileTraversalInfo info)
        {
            if (tileId < 0 ||
                tileId >= tileInfos.Length)
            {
                info = default;
                return false;
            }

            if (tileInfoGenerations[tileId] == generation)
            {
                info = tileInfos[tileId];
                return info.Exists;
            }

            bool exists =
                TileTraversalInfo.TryGet(
                    tileId,
                    out info);
            tileInfoGenerations[tileId] = generation;
            tileInfos[tileId] = info;
            return exists;
        }

        internal void SetBest(
            int tileId,
            float cost,
            int parent,
            MovementMethod method,
            TraversalEstimate estimate,
            TraversalState state)
        {
            bestGenerations[tileId] = generation;
            bestCosts[tileId] = cost;
            parents[tileId] = parent;
            methods[tileId] = method;
            estimates[tileId] = estimate;
            states[tileId] = state;
        }

        internal bool TryImprove(
            int tileId,
            float cost,
            int parent,
            MovementMethod method,
            TraversalEstimate estimate,
            TraversalState state)
        {
            if (bestGenerations[tileId] == generation &&
                bestCosts[tileId] <= cost + 0.001f)
            {
                return false;
            }

            SetBest(
                tileId,
                cost,
                parent,
                method,
                estimate,
                state);
            return true;
        }

        internal bool IsCurrentBest(
            int tileId,
            float cost)
        {
            return tileId >= 0 &&
                   tileId < bestGenerations.Length &&
                   bestGenerations[tileId] == generation &&
                   cost <= bestCosts[tileId] + 0.001f;
        }

        internal TraversalState GetState(int tileId)
        {
            return states[tileId];
        }

        internal bool TryClose(int tileId)
        {
            if (closedGenerations[tileId] == generation)
            {
                return false;
            }

            closedGenerations[tileId] = generation;
            return true;
        }

        internal bool IsClosed(int tileId)
        {
            return tileId >= 0 &&
                   tileId < closedGenerations.Length &&
                   closedGenerations[tileId] == generation;
        }

        internal void Enqueue(SafeOpenNode node)
        {
            if (OpenCount == open.Length)
            {
                Array.Resize(
                    ref open,
                    open.Length * 2);
            }

            int index = OpenCount++;
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (Compare(
                        node,
                        open[parent]) >= 0)
                {
                    break;
                }

                open[index] = open[parent];
                index = parent;
            }

            open[index] = node;
        }

        internal SafeOpenNode Dequeue()
        {
            SafeOpenNode first = open[0];
            SafeOpenNode last =
                open[--OpenCount];
            if (OpenCount == 0)
            {
                return first;
            }

            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= OpenCount)
                {
                    break;
                }

                int right = left + 1;
                int child =
                    right < OpenCount &&
                    Compare(
                        open[right],
                        open[left]) < 0
                        ? right
                        : left;
                if (Compare(
                        last,
                        open[child]) <= 0)
                {
                    break;
                }

                open[index] = open[child];
                index = child;
            }

            open[index] = last;
            return first;
        }

        internal bool BuildResult(
            int startTileId,
            int targetTileId)
        {
            Result.Clear();
            int current = targetTileId;
            int remaining = parents.Length;
            while (current != startTileId &&
                   remaining-- > 0)
            {
                if (current < 0 ||
                    current >= bestGenerations.Length ||
                    bestGenerations[current] != generation)
                {
                    Result.Clear();
                    return false;
                }

                Result.Add(
                    new PathStep(
                        current,
                        methods[current],
                        estimates[current]));
                current = parents[current];
            }

            if (current != startTileId)
            {
                Result.Clear();
                return false;
            }

            Result.Reverse();
            return true;
        }

        private static int Compare(
            SafeOpenNode left,
            SafeOpenNode right)
        {
            int result = left.F.CompareTo(right.F);
            return result != 0
                ? result
                : left.TileId.CompareTo(right.TileId);
        }

        private void EnsureCapacity(int capacity)
        {
            if (tileInfos.Length >= capacity)
            {
                return;
            }

            int nextCapacity =
                Math.Max(
                    capacity,
                    Math.Max(256, tileInfos.Length * 2));
            Array.Resize(
                ref tileInfoGenerations,
                nextCapacity);
            Array.Resize(
                ref tileInfos,
                nextCapacity);
            Array.Resize(
                ref bestGenerations,
                nextCapacity);
            Array.Resize(
                ref closedGenerations,
                nextCapacity);
            Array.Resize(
                ref bestCosts,
                nextCapacity);
            Array.Resize(
                ref parents,
                nextCapacity);
            Array.Resize(
                ref methods,
                nextCapacity);
            Array.Resize(
                ref estimates,
                nextCapacity);
            Array.Resize(
                ref states,
                nextCapacity);
        }
    }

    private struct FullPathNode
    {
        internal int TileId;
        internal int ParentIndex;
        internal MovementMethod Method;
        internal TraversalEstimate Estimate;
        internal TraversalState State;
        internal float G;
        internal float H;
        internal bool Active;

        internal float F => G + H;
    }

    /// <summary>
    /// 完整多标签 A* 的线程本地工作区。标签支配和裁剪规则与原实现一致，
    /// 但节点、地块缓存、标签槽和开放堆都跨请求复用，不再为每个扩展节点
    /// 创建对象和字典项。
    /// </summary>
    private sealed class FullPathWorkspace
    {
        private int tileGeneration;
        private int graphGeneration;
        private int labelSlotCapacity;
        private int nodeCount;
        private int[] tileInfoGenerations =
            Array.Empty<int>();
        private TileTraversalInfo[] tileInfos =
            Array.Empty<TileTraversalInfo>();
        private int[] labelGenerations =
            Array.Empty<int>();
        private int[] labelCounts =
            Array.Empty<int>();
        private int[] labelIndices =
            Array.Empty<int>();
        private FullPathNode[] nodes =
            new FullPathNode[1024];
        private int[] open = new int[1024];

        internal int OpenCount { get; private set; }

        internal void BeginLocalPath(
            int tileCount,
            int maximumLabels)
        {
            EnsureTileCapacity(
                tileCount,
                Math.Max(1, maximumLabels) + 1);
            tileGeneration++;
            if (tileGeneration == int.MaxValue)
            {
                Array.Clear(
                    tileInfoGenerations,
                    0,
                    tileInfoGenerations.Length);
                tileGeneration = 1;
            }
        }

        internal void BeginGraphSearch()
        {
            graphGeneration++;
            if (graphGeneration == int.MaxValue)
            {
                Array.Clear(
                    labelGenerations,
                    0,
                    labelGenerations.Length);
                graphGeneration = 1;
            }

            nodeCount = 0;
            OpenCount = 0;
        }

        internal bool TryGetTileInfo(
            int tileId,
            out TileTraversalInfo info)
        {
            if (tileId < 0 ||
                tileId >= tileInfos.Length)
            {
                info = default;
                return false;
            }

            if (tileInfoGenerations[tileId] ==
                tileGeneration)
            {
                info = tileInfos[tileId];
                return info.Exists;
            }

            bool exists =
                TileTraversalInfo.TryGet(
                    tileId,
                    out info);
            tileInfoGenerations[tileId] =
                tileGeneration;
            tileInfos[tileId] = info;
            return exists;
        }

        internal int AddStart(
            int tileId,
            TraversalState state,
            float heuristic)
        {
            int nodeIndex = AddNode(
                new FullPathNode
                {
                    TileId = tileId,
                    ParentIndex = -1,
                    Method = MovementMethod.Walk,
                    Estimate = default,
                    State = state,
                    G = 0f,
                    H = heuristic,
                    Active = true
                });
            InitializeLabels(tileId);
            labelIndices[
                LabelOffset(tileId)] = nodeIndex;
            labelCounts[tileId] = 1;
            return nodeIndex;
        }

        internal int TryAddLabel(
            int tileId,
            int parentIndex,
            MovementMethod method,
            TraversalEstimate estimate,
            TraversalState state,
            float g,
            float h,
            int maximumLabels)
        {
            InitializeLabels(tileId);
            var candidate =
                new FullPathNode
                {
                    TileId = tileId,
                    ParentIndex = parentIndex,
                    Method = method,
                    Estimate = estimate,
                    State = state,
                    G = g,
                    H = h,
                    Active = true
                };
            int count = labelCounts[tileId];
            int offset = LabelOffset(tileId);
            for (int i = 0; i < count; i++)
            {
                if (Dominates(
                        nodes[labelIndices[offset + i]],
                        candidate))
                {
                    return -1;
                }
            }

            for (int i = count - 1; i >= 0; i--)
            {
                int existingIndex =
                    labelIndices[offset + i];
                if (!Dominates(
                        candidate,
                        nodes[existingIndex]))
                {
                    continue;
                }

                SetInactive(existingIndex);
                RemoveLabelAt(
                    offset,
                    ref count,
                    i);
            }

            int nodeIndex = AddNode(candidate);
            labelIndices[offset + count++] = nodeIndex;
            if (count > maximumLabels)
            {
                int worstPosition = 0;
                float worstScore =
                    nodes[labelIndices[offset]].F;
                for (int i = 1; i < count; i++)
                {
                    float score =
                        nodes[labelIndices[offset + i]].F;
                    if (score > worstScore)
                    {
                        worstScore = score;
                        worstPosition = i;
                    }
                }

                int worstNode =
                    labelIndices[offset + worstPosition];
                SetInactive(worstNode);
                RemoveLabelAt(
                    offset,
                    ref count,
                    worstPosition);
                labelCounts[tileId] = count;
                return worstNode == nodeIndex
                    ? -1
                    : nodeIndex;
            }

            labelCounts[tileId] = count;
            return nodeIndex;
        }

        internal FullPathNode GetNode(int nodeIndex)
        {
            return nodes[nodeIndex];
        }

        internal bool IsActive(int nodeIndex)
        {
            return nodeIndex >= 0 &&
                   nodeIndex < nodeCount &&
                   nodes[nodeIndex].Active;
        }

        internal void Enqueue(int nodeIndex)
        {
            EnsureOpenCapacity(OpenCount + 1);
            int index = OpenCount++;
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (CompareNodes(
                        nodeIndex,
                        open[parent]) >= 0)
                {
                    break;
                }

                open[index] = open[parent];
                index = parent;
            }

            open[index] = nodeIndex;
        }

        internal int Dequeue()
        {
            int first = open[0];
            int last = open[--OpenCount];
            if (OpenCount == 0)
            {
                return first;
            }

            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= OpenCount)
                {
                    break;
                }

                int right = left + 1;
                int child =
                    right < OpenCount &&
                    CompareNodes(
                        open[right],
                        open[left]) < 0
                        ? right
                        : left;
                if (CompareNodes(
                        last,
                        open[child]) <= 0)
                {
                    break;
                }

                open[index] = open[child];
                index = child;
            }

            open[index] = last;
            return first;
        }

        internal List<PathStep> BuildResult(
            int nodeIndex)
        {
            var result = new List<PathStep>(64);
            int remaining = nodeCount;
            while (nodeIndex >= 0 &&
                   remaining-- > 0)
            {
                FullPathNode node =
                    nodes[nodeIndex];
                if (node.ParentIndex < 0)
                {
                    break;
                }

                result.Add(
                    new PathStep(
                        node.TileId,
                        node.Method,
                        node.Estimate));
                nodeIndex = node.ParentIndex;
            }

            result.Reverse();
            return result;
        }

        private static bool Dominates(
            FullPathNode left,
            FullPathNode right)
        {
            return left.G <= right.G + 0.001f &&
                   left.State.Stamina >=
                   right.State.Stamina - 0.001f &&
                   left.State.Health >=
                   right.State.Health - 0.001f &&
                   left.State.Risk <=
                   right.State.Risk + 0.001f;
        }

        private int AddNode(FullPathNode node)
        {
            EnsureNodeCapacity(nodeCount + 1);
            int index = nodeCount++;
            nodes[index] = node;
            return index;
        }

        private void SetInactive(int nodeIndex)
        {
            FullPathNode node = nodes[nodeIndex];
            node.Active = false;
            nodes[nodeIndex] = node;
        }

        private void InitializeLabels(int tileId)
        {
            if (labelGenerations[tileId] ==
                graphGeneration)
            {
                return;
            }

            labelGenerations[tileId] =
                graphGeneration;
            labelCounts[tileId] = 0;
        }

        private int LabelOffset(int tileId)
        {
            return tileId * labelSlotCapacity;
        }

        private void RemoveLabelAt(
            int offset,
            ref int count,
            int position)
        {
            for (int i = position; i < count - 1; i++)
            {
                labelIndices[offset + i] =
                    labelIndices[offset + i + 1];
            }

            count--;
        }

        private int CompareNodes(
            int leftIndex,
            int rightIndex)
        {
            return nodes[leftIndex].F.CompareTo(
                nodes[rightIndex].F);
        }

        private void EnsureTileCapacity(
            int tileCapacity,
            int requiredLabelSlots)
        {
            bool growTiles =
                tileInfos.Length < tileCapacity;
            bool growLabels =
                labelSlotCapacity <
                requiredLabelSlots;
            if (!growTiles && !growLabels)
            {
                return;
            }

            int nextTileCapacity =
                growTiles
                    ? Math.Max(
                        tileCapacity,
                        Math.Max(
                            256,
                            tileInfos.Length * 2))
                    : tileInfos.Length;
            int nextLabelSlots =
                Math.Max(
                    labelSlotCapacity,
                    requiredLabelSlots);
            Array.Resize(
                ref tileInfoGenerations,
                nextTileCapacity);
            Array.Resize(
                ref tileInfos,
                nextTileCapacity);
            Array.Resize(
                ref labelGenerations,
                nextTileCapacity);
            Array.Resize(
                ref labelCounts,
                nextTileCapacity);
            labelIndices =
                new int[
                    nextTileCapacity *
                    nextLabelSlots];
            labelSlotCapacity =
                nextLabelSlots;
            Array.Clear(
                labelGenerations,
                0,
                labelGenerations.Length);
            graphGeneration = 0;
        }

        private void EnsureNodeCapacity(int capacity)
        {
            if (nodes.Length >= capacity)
            {
                return;
            }

            Array.Resize(
                ref nodes,
                Math.Max(
                    capacity,
                    nodes.Length * 2));
        }

        private void EnsureOpenCapacity(int capacity)
        {
            if (open.Length >= capacity)
            {
                return;
            }

            Array.Resize(
                ref open,
                Math.Max(
                    capacity,
                    open.Length * 2));
        }
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
        public PortalLeg(PortalDefinition entry, PortalDefinition exit, float transferCost)
        {
            Entry = entry;
            Exit = exit;
            TransferCost = transferCost;
        }

        public PortalDefinition Entry { get; }
        public PortalDefinition Exit { get; }
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
        public int StepCount => Legs.Sum(leg => leg is MovementLeg movement ? movement.Steps.Count : 1);

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
        public PortalEstimate(PortalDefinition entry, PortalDefinition exit, PortalConnection link, float estCost)
        {
            Entry = entry;
            Exit = exit;
            Link = link;
            EstCost = estCost;
        }

        public PortalDefinition Entry { get; }
        public PortalDefinition Exit { get; }
        public PortalConnection Link { get; }
        public float EstCost { get; }
    }

    private sealed class LocalPathResult
    {
        private LocalPathResult(bool success, IReadOnlyList<PathStep> steps, float cost, bool hitNodeLimit)
        {
            IsSuccess = success;
            Steps = steps;
            Cost = cost;
            HitNodeLimit = hitNodeLimit;
        }

        public bool IsSuccess { get; }
        public IReadOnlyList<PathStep> Steps { get; }
        public float Cost { get; }
        public bool HitNodeLimit { get; }

        public static LocalPathResult Fail(bool hitNodeLimit = false)
        {
            return new LocalPathResult(false, Array.Empty<PathStep>(), float.MaxValue, hitNodeLimit);
        }

        public static LocalPathResult Success(IReadOnlyList<PathStep> steps, float cost)
        {
            return new LocalPathResult(true, steps, cost, false);
        }
    }

    private readonly struct TraversalState
    {
        private TraversalState(float stamina, float health, float risk)
        {
            Stamina = stamina;
            Health = health;
            Risk = risk;
        }

        public float Stamina { get; }
        public float Health { get; }
        public float Risk { get; }

        public static TraversalState Start(MovementProfile profile)
        {
            return new TraversalState(profile.CurrentStamina, profile.CurrentHealth, 0f);
        }

        public TraversalState Advance(TraversalEstimate estimate, MovementProfile profile)
        {
            var stamina = Mathf.Clamp(Stamina - estimate.StaminaCost + estimate.TimeSeconds * profile.StaminaRegenPerSecond,
                0f, profile.MaxStamina);
            var health = Health - estimate.HealthCost;
            var risk = Risk + estimate.RiskCost;
            return new TraversalState(stamina, health, risk);
        }
    }

    private sealed class MovementProfile
    {
        private MovementProfile(PathfindingConfig config)
        {
            Config = config;
        }

        private PathfindingConfig Config { get; }
        public bool IgnoreBlocks { get; private set; }
        public bool DieOnBlocks { get; private set; }
        public bool IsBoat { get; private set; }
        public bool IsWaterCreature { get; private set; }
        public bool IsFlying { get; private set; }
        public bool IsFireImmune { get; private set; }
        public bool IsDamagedByOcean { get; private set; }
        public bool IsLavaDamaging { get; private set; }
        public bool HasFastSwimming { get; private set; }
        public int MaxLabelsPerTile { get; private set; }
        public int MaxNodesShort { get; private set; }
        public int MaxNodesLong { get; private set; }
        public int MaxNodesLongFallback { get; private set; }
        public int FallbackCorridorMinDetour { get; private set; }
        public float FallbackCorridorDetourScale { get; private set; }
        public float CurrentStamina { get; private set; }
        public float MaxStamina { get; private set; }
        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public float LowHealthThreshold { get; private set; }
        public float WalkSpeed { get; private set; }
        public float SwimSpeed { get; private set; }
        public float SailSpeed { get; private set; }
        public float BestCaseSpeed { get; private set; }
        public float PowerLevel { get; private set; }
        public float StaminaRegenPerSecond { get; private set; }
        public float WaterStaminaDrainPerSecond { get; private set; }
        public float DrowningDamagePerSecond { get; private set; }
        public float WaterDamagePerSecond { get; private set; }
        public float TerrainDamageTicksPerSecond { get; private set; }
        public float BlockDamagePerSecond { get; private set; }
        public float BlockRiskCost { get; private set; }
        public float FireRiskCost { get; private set; }
        public float OceanRiskCost { get; private set; }
        public float LavaRiskCost { get; private set; }
        public float TerrainDamageRiskCost { get; private set; }

        public static MovementProfile Build(PathRequest request, PathfindingConfig config)
        {
            config ??= PathfindingConfig.Default;
            var profile = new MovementProfile(config)
            {
                IgnoreBlocks = request.ActorIgnoresBlocks,
                DieOnBlocks = request.ActorDiesOnBlocks,
                IsBoat = request.ActorIsBoat,
                IsWaterCreature = request.ActorIsWaterCreature,
                IsFlying = request.ActorIsFlying,
                IsFireImmune = request.ActorIsFireImmune,
                IsDamagedByOcean = request.ActorIsDamagedByOcean,
                HasFastSwimming = request.ActorHasFastSwimming,
                IsLavaDamaging = request.ActorIsLavaDamaging,
                MaxLabelsPerTile = Mathf.Max(1, config.MaxLabelsPerTile),
                MaxNodesShort = config.MaxNodesShort,
                MaxNodesLong = config.MaxNodesLong,
                MaxNodesLongFallback = config.MaxNodesLongFallback,
                FallbackCorridorMinDetour = config.FallbackCorridorMinDetour,
                FallbackCorridorDetourScale = config.FallbackCorridorDetourScale,
                CurrentStamina = request.ActorCurrentStamina,
                MaxStamina = Mathf.Max(1f, request.ActorMaxStamina),
                CurrentHealth = request.ActorCurrentHealth,
                MaxHealth = Mathf.Max(1f, request.ActorMaxHealth),
                StaminaRegenPerSecond = request.StaminaRegenPerSecond,
                WaterStaminaDrainPerSecond = config.WaterStaminaDrainPerSecond,
                DrowningDamagePerSecond = config.DrowningDamagePerSecond,
                TerrainDamageTicksPerSecond = config.DamageUnitsTicksPerSecond,
                BlockDamagePerSecond = 3.333f,
                BlockRiskCost = request.WalkOnBlocks ? config.BlockRiskCost * 0.2f : config.BlockRiskCost,
                FireRiskCost = config.FireRiskCost,
                OceanRiskCost = request.PathOnWater ? config.OceanRiskCost * 0.2f : config.OceanRiskCost,
                LavaRiskCost = request.WalkOnLava ? config.LavaRiskCost * 0.2f : config.LavaRiskCost,
                TerrainDamageRiskCost = config.TerrainDamageRiskCost
            };

            profile.LowHealthThreshold = Mathf.Max(1f, profile.MaxHealth * 0.15f);
            profile.WaterDamagePerSecond = request.ActorWaterDamagePerSecond > 0f
                ? request.ActorWaterDamagePerSecond
                : profile.MaxHealth * 0.1f * 3.333f;

            var baseSpeed = request.ActorBaseSpeed;
            profile.WalkSpeed = Mathf.Max(0.1f, baseSpeed * config.WalkSpeedScale);
            profile.SwimSpeed = Mathf.Max(0.05f, profile.WalkSpeed * config.SwimSpeedScale);
            profile.SailSpeed = Mathf.Max(0.05f, profile.WalkSpeed * config.SailSpeedScale);
            if (profile.HasFastSwimming)
            {
                profile.SwimSpeed *= 5f;
                profile.WaterStaminaDrainPerSecond *= 0.2f;
            }

            profile.PowerLevel = request.ActorPowerLevel;
            if (request.ActorHasXianCultisys)
            {
                profile.OceanRiskCost *= 0.25f;
                profile.LavaRiskCost *= 0.15f;
                profile.FireRiskCost *= 0.25f;
                profile.TerrainDamageRiskCost *= 0.25f;
            }

            profile.BestCaseSpeed = Mathf.Max(profile.WalkSpeed, Mathf.Max(profile.SwimSpeed, profile.SailSpeed));
            return profile;
        }

        public float GetSpeed(TileTraversalInfo type, MovementMethod method, TraversalState state)
        {
            var speed = method switch
            {
                MovementMethod.Swim => IsBoat ? SailSpeed : SwimSpeed,
                MovementMethod.Portal => SailSpeed,
                _ => WalkSpeed
            };

            if (type.HasType && !IsFlying && !IsWaterCreature && method == MovementMethod.Walk)
            {
                speed *= Mathf.Max(type.WalkMultiplier, 0.05f);
            }

            if (type.Lava && !IsFlying && !IsWaterCreature)
            {
                speed *= Mathf.Max(type.WalkMultiplier, 0.05f);
            }

            if (method == MovementMethod.Swim && !IsWaterCreature && !IsBoat && state.Stamina <= 0f && !HasFastSwimming)
            {
                speed *= Config.ExhaustedSwimSpeedScale;
            }

            if (IsBoat && type.HasType && !type.Ocean)
            {
                speed *= 0.05f;
            }

            return Mathf.Max(speed, 0.01f);
        }

        public float EstimateOpenTerrainCost(int distance)
        {
            return distance / Mathf.Max(WalkSpeed, 0.01f);
        }

        public float EstimateEnvironmentalDamage(float rawDamage)
        {
            if (rawDamage <= 0f)
            {
                return 0f;
            }

            if (PowerLevel <= 0f)
            {
                return rawDamage;
            }

            var divisor = Mathf.Pow(DamageCalcHyperParameters.PowerBase, PowerLevel);
            var adjusted = Mathf.Log(Mathf.Max(rawDamage, 1f), divisor);
            if (adjusted < 1f)
            {
                return 0f;
            }

            var floor = rawDamage * Config.XianEnvironmentalDamageFloor;
            return Mathf.Max(adjusted, floor);
        }

        public float CostOf(TraversalEstimate estimate, TraversalState nextState)
        {
            var cost = estimate.TimeSeconds
                       + estimate.StaminaCost * Config.StaminaCostWeight
                       + estimate.HealthCost * Config.HealthCostWeight
                       + estimate.RiskCost;

            if (nextState.Health <= 0f)
            {
                cost += Config.DeathRiskCost;
            }
            else if (nextState.Health <= LowHealthThreshold)
            {
                var missing = Mathf.Clamp01((LowHealthThreshold - nextState.Health) / LowHealthThreshold);
                cost += Config.LowHealthRiskCost * (0.25f + missing);
            }

            return cost;
        }
    }
}

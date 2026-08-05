using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Const;
using Cultiway.Utils;
using UnityEngine;

namespace Cultiway.Core.Pathfinding;

/// <summary>
/// 生成可增量消费的局部路径。工作线程只读取 PathRequest 中的标量和导航缓存。
/// </summary>
public sealed class PortalAwarePathGenerator : IPathGenerator
{
    private readonly PortalRegistry registry;
    private readonly PathfindingConfig config;
    private readonly RegionRouteCache regionRouteCache;
    private readonly ThreadLocal<SearchWorkspace> workspaces = new(() => new SearchWorkspace());

    public PortalAwarePathGenerator(PortalRegistry registry, PathfindingConfig config)
    {
        this.registry = registry ?? PortalRegistry.Instance;
        this.config = config ?? PathfindingConfig.Default;
        regionRouteCache = new RegionRouteCache(this.config.RegionRouteCacheSize);
    }

    public PathGenerationResult GenerateSegment(PathRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request == null || request.StartTileId < 0)
        {
            return PathGenerationResult.Fail(PathFailureReason.InvalidStart);
        }

        if (request.TargetTileId < 0)
        {
            return PathGenerationResult.Fail(PathFailureReason.InvalidTarget);
        }

        PathNavigationGrid grid = request.NavigationGrid;
        if (grid == null || grid.Generation != request.WorldGeneration)
        {
            return PathGenerationResult.Fail(PathFailureReason.NavigationGridUnavailable);
        }

        if (!grid.TryGetTile(request.StartTileId, out _))
        {
            return PathGenerationResult.Fail(PathFailureReason.InvalidStart);
        }

        if (!grid.TryGetTile(request.TargetTileId, out _))
        {
            return PathGenerationResult.Fail(PathFailureReason.InvalidTarget);
        }

        if (request.StartTileId == request.TargetTileId)
        {
            return PathGenerationResult.Success(Array.Empty<PathStep>(), true, request.TargetTileId,
                endStamina: request.ActorCurrentStamina, endHealth: request.ActorCurrentHealth);
        }

        try
        {
            return GenerateInternal(request, grid, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
            return PathGenerationResult.Fail(PathFailureReason.GeneratorException, e);
        }
    }

    private PathGenerationResult GenerateInternal(PathRequest request, PathNavigationGrid grid,
        CancellationToken token)
    {
        MovementProfile profile = MovementProfile.Build(request, config);
        bool usePortal = TryChoosePortal(request, grid, profile, out PortalChoice portal);
        int objectiveTileId = usePortal ? portal.Entry.TileId : request.TargetTileId;

        if (usePortal && request.StartTileId == objectiveTileId)
        {
            PathStep portalStep = CreatePortalStep(portal);
            return BuildSuccess(new[] { portalStep }, false, portal.Exit.TileId, profile,
                kind: PathGenerationKind.Portal);
        }

        if (TryBuildStraightSegment(request.StartTileId, objectiveTileId, grid, profile,
                config.SegmentTargetSteps, out PathStep[] straightSteps, out int straightEnd))
        {
            bool reachedObjective = straightEnd == objectiveTileId;
            if (usePortal && reachedObjective && straightSteps.Length < config.SegmentTargetSteps)
            {
                straightSteps = AppendPortal(straightSteps, portal);
                straightEnd = portal.Exit.TileId;
                return BuildSuccess(straightSteps, false, straightEnd, profile,
                    kind: PathGenerationKind.Portal);
            }

            bool reachedTarget = !usePortal && straightEnd == request.TargetTileId;
            return BuildSuccess(straightSteps, reachedTarget, straightEnd, profile,
                kind: PathGenerationKind.StraightLine);
        }

        int objectiveDistance = grid.ManhattanDistance(request.StartTileId, objectiveTileId);
        int searchTarget = objectiveTileId;
        int maxNodes;
        float heuristicWeight;
        RegionCorridor corridor = null;
        PathGenerationKind generationKind = PathGenerationKind.Search;

        if (objectiveDistance <= config.ShortRangeTiles)
        {
            maxNodes = config.MaxNodesShort;
            heuristicWeight = 1f;
        }
        else if (objectiveDistance <= config.LongRangeTiles)
        {
            maxNodes = config.MaxNodesLong;
            heuristicWeight = config.LongRangeHeuristicWeight;
        }
        else
        {
            maxNodes = config.MaxNodesLong;
            heuristicWeight = config.LongRangeHeuristicWeight;
            ResolveLongRangeSearch(request, grid, objectiveTileId, out searchTarget, out corridor);
            generationKind = corridor == null ? PathGenerationKind.Search : PathGenerationKind.RegionCorridor;
        }

        SearchWorkspace workspace = workspaces.Value;
        LocalPathResult local = TryBuildLocalPath(request.StartTileId, searchTarget, grid, profile,
            maxNodes, heuristicWeight, corridor, workspace, token);
        int totalExpandedNodes = local.ExpandedNodes;
        if (!local.IsSuccess && corridor != null)
        {
            RegionCorridor widened = corridor.Expand();
            local = TryBuildLocalPath(request.StartTileId, searchTarget, grid, profile,
                maxNodes, heuristicWeight, widened, workspace, token);
            totalExpandedNodes += local.ExpandedNodes;
        }

        if (!local.IsSuccess)
        {
            return PathGenerationResult.Fail(local.HitNodeLimit
                ? PathFailureReason.SearchLimitExceeded
                : PathFailureReason.Unreachable, expandedNodes: totalExpandedNodes);
        }

        PathStep[] segment = TrimSegment(local.Steps, config.SegmentTargetSteps);
        if (segment.Length == 0)
        {
            return PathGenerationResult.Fail(PathFailureReason.Unreachable, expandedNodes: totalExpandedNodes);
        }

        int endTileId = segment[segment.Length - 1].TileId;
        bool reachedObjectiveBySearch = local.Steps.Length <= config.SegmentTargetSteps &&
                                        endTileId == objectiveTileId;
        if (usePortal && reachedObjectiveBySearch && segment.Length < config.SegmentTargetSteps)
        {
            segment = AppendPortal(segment, portal);
            endTileId = portal.Exit.TileId;
            generationKind = PathGenerationKind.Portal;
        }

        bool reachedFinalTarget = !usePortal && endTileId == request.TargetTileId;
        return BuildSuccess(segment, reachedFinalTarget, endTileId, profile, totalExpandedNodes,
            generationKind);
    }

    private static PathGenerationResult BuildSuccess(PathStep[] steps, bool reachedTarget, int endTileId,
        MovementProfile profile, int expandedNodes = 0,
        PathGenerationKind kind = PathGenerationKind.Search)
    {
        TraversalState state = TraversalState.Start(profile);
        for (int i = 0; i < steps.Length; i++)
        {
            state = state.Advance(steps[i].Estimate, profile);
        }

        return PathGenerationResult.Success(steps, reachedTarget, endTileId, expandedNodes, kind,
            state.Stamina, state.Health);
    }

    private void ResolveLongRangeSearch(PathRequest request, PathNavigationGrid grid, int objectiveTileId,
        out int searchTarget, out RegionCorridor corridor)
    {
        searchTarget = ResolveGeometricWaypoint(grid, request.StartTileId, objectiveTileId,
            config.RegionCorridorLookaheadTiles);
        corridor = null;
        PathRegionTopology topology = grid.Topology;
        if (topology == null || topology.Generation != grid.Generation ||
            !grid.TryGetTile(request.StartTileId, out PathTileSnapshot start) ||
            !grid.TryGetTile(objectiveTileId, out PathTileSnapshot target) ||
            start.RegionId < 0 || target.RegionId < 0)
        {
            return;
        }

        int traversalClass = ResolveTraversalClass(request);
        int[] route = regionRouteCache.GetOrBuild(grid.Identity, topology, start.RegionId, target.RegionId,
            traversalClass);
        if (route == null || route.Length == 0)
        {
            return;
        }

        int lookaheadTarget = ResolveRegionLookahead(grid, topology, request.StartTileId, route,
            config.RegionCorridorLookaheadTiles);
        if (lookaheadTarget >= 0 && lookaheadTarget != request.StartTileId)
        {
            searchTarget = lookaheadTarget;
        }

        corridor = RegionCorridor.Create(topology, route);
    }

    private static int ResolveRegionLookahead(PathNavigationGrid grid, PathRegionTopology topology,
        int startTileId, int[] route, int lookaheadTiles)
    {
        int selected = -1;
        for (int i = 1; i < route.Length; i++)
        {
            if (!topology.TryGetRegion(route[i], out PathRegionSnapshot region) || region.CenterTileId < 0)
            {
                continue;
            }

            selected = region.CenterTileId;
            if (grid.ManhattanDistance(startTileId, selected) >= lookaheadTiles)
            {
                break;
            }
        }

        return selected;
    }

    private static int ResolveGeometricWaypoint(PathNavigationGrid grid, int startTileId, int targetTileId,
        int lookaheadTiles)
    {
        int distance = grid.ManhattanDistance(startTileId, targetTileId);
        if (distance <= Math.Max(1, lookaheadTiles)) return targetTileId;
        int startX = grid.XOf(startTileId);
        int startY = grid.YOf(startTileId);
        int targetX = grid.XOf(targetTileId);
        int targetY = grid.YOf(targetTileId);
        float ratio = Math.Max(1, lookaheadTiles) / (float)distance;
        int x = Mathf.RoundToInt(startX + (targetX - startX) * ratio);
        int y = Mathf.RoundToInt(startY + (targetY - startY) * ratio);
        if (grid.TryGetTileAt(x, y, out int tileId, out _)) return tileId;

        for (int radius = 1; radius <= 3; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                    if (grid.TryGetTileAt(x + dx, y + dy, out tileId, out _)) return tileId;
                }
            }
        }

        return targetTileId;
    }

    private bool TryChoosePortal(PathRequest request, PathNavigationGrid grid, MovementProfile profile,
        out PortalChoice choice)
    {
        choice = default;
        if (profile.IsBoat) return false;
        PathPortalSnapshot[] portals = registry.CapturePathSnapshot();
        if (portals.Length < 2) return false;

        int directDistance = grid.ManhattanDistance(request.StartTileId, request.TargetTileId);
        if (directDistance <= config.ShortRangeTiles) return false;
        float directCost = profile.EstimateOpenTerrainCost(directDistance);
        float bestCost = directCost * 0.95f;
        bool found = false;
        int candidateCount = Math.Min(Math.Max(1, config.PortalCandidates), portals.Length);
        var nearestIndices = new int[candidateCount];
        var nearestDistances = new int[candidateCount];
        for (int i = 0; i < candidateCount; i++)
        {
            nearestIndices[i] = -1;
            nearestDistances[i] = int.MaxValue;
        }

        int startX = grid.XOf(request.StartTileId);
        int startY = grid.YOf(request.StartTileId);
        for (int i = 0; i < portals.Length; i++)
        {
            if (portals[i].TileId < 0) continue;
            int distance = Math.Abs(startX - portals[i].X) + Math.Abs(startY - portals[i].Y);
            for (int slot = 0; slot < candidateCount; slot++)
            {
                if (distance >= nearestDistances[slot]) continue;
                for (int move = candidateCount - 1; move > slot; move--)
                {
                    nearestDistances[move] = nearestDistances[move - 1];
                    nearestIndices[move] = nearestIndices[move - 1];
                }

                nearestDistances[slot] = distance;
                nearestIndices[slot] = i;
                break;
            }
        }

        int targetX = grid.XOf(request.TargetTileId);
        int targetY = grid.YOf(request.TargetTileId);
        for (int candidate = 0; candidate < candidateCount; candidate++)
        {
            int entryIndex = nearestIndices[candidate];
            if (entryIndex < 0) continue;
            PathPortalSnapshot entry = portals[entryIndex];
            int entryDistance = nearestDistances[candidate];
            if (directDistance <= config.LongRangeTiles && entryDistance > config.PortalSearchRadius) continue;

            for (int connectionIndex = 0; connectionIndex < entry.Connections.Length; connectionIndex++)
            {
                PortalConnection connection = entry.Connections[connectionIndex];
                if (!TryFindPortal(portals, connection.TargetId, out PathPortalSnapshot exit)) continue;
                int exitDistance = Math.Abs(targetX - exit.X) + Math.Abs(targetY - exit.Y);
                if (exitDistance + 4 >= directDistance) continue;
                float estimate = profile.EstimateOpenTerrainCost(entryDistance) + entry.WaitTime +
                                 connection.TravelTime + exit.TransferTime +
                                 profile.EstimateOpenTerrainCost(exitDistance);
                if (estimate >= bestCost) continue;
                bestCost = estimate;
                choice = new PortalChoice(entry, exit,
                    entry.WaitTime + connection.TravelTime + exit.TransferTime);
                found = true;
            }
        }

        return found;
    }

    private static bool TryFindPortal(PathPortalSnapshot[] portals, long id, out PathPortalSnapshot portal)
    {
        for (int i = 0; i < portals.Length; i++)
        {
            if (portals[i].Id != id) continue;
            portal = portals[i];
            return true;
        }

        portal = default;
        return false;
    }

    private static PathStep CreatePortalStep(PortalChoice portal)
    {
        return new PathStep(portal.Exit.TileId, MovementMethod.Portal,
            TraversalEstimate.Portal(portal.TransferCost), portal.Entry.Definition, portal.Exit.Definition);
    }

    private static PathStep[] AppendPortal(PathStep[] steps, PortalChoice portal)
    {
        var result = new PathStep[steps.Length + 1];
        Array.Copy(steps, result, steps.Length);
        result[steps.Length] = CreatePortalStep(portal);
        return result;
    }

    private static PathStep[] TrimSegment(PathStep[] steps, int maximum)
    {
        maximum = Math.Max(1, maximum);
        if (steps.Length <= maximum) return steps;
        var result = new PathStep[maximum];
        Array.Copy(steps, result, maximum);
        return result;
    }

    private static bool TryBuildStraightSegment(int startTileId, int targetTileId, PathNavigationGrid grid,
        MovementProfile profile, int maximumSteps, out PathStep[] steps, out int endTileId)
    {
        maximumSteps = Math.Max(1, maximumSteps);
        var buffer = new PathStep[maximumSteps];
        int count = 0;
        int x = grid.XOf(startTileId);
        int y = grid.YOf(startTileId);
        int targetX = grid.XOf(targetTileId);
        int targetY = grid.YOf(targetTileId);
        int dx = Math.Abs(targetX - x);
        int dy = Math.Abs(targetY - y);
        int sx = x < targetX ? 1 : -1;
        int sy = y < targetY ? 1 : -1;
        int error = dx - dy;
        TraversalState state = TraversalState.Start(profile);
        int currentTileId = startTileId;

        while ((x != targetX || y != targetY) && count < maximumSteps)
        {
            int previousX = x;
            int previousY = y;
            int doubled = error * 2;
            if (doubled > -dy)
            {
                error -= dy;
                x += sx;
            }

            if (doubled < dx)
            {
                error += dx;
                y += sy;
            }

            if (!grid.TryGetTileAt(x, y, out int nextTileId, out PathTileSnapshot next) ||
                !IsFastTileSafe(next, profile))
            {
                steps = null;
                endTileId = startTileId;
                return false;
            }

            bool diagonal = previousX != x && previousY != y;
            if (diagonal && (!grid.TryGetTileAt(x, previousY, out _, out PathTileSnapshot sideX) ||
                             !grid.TryGetTileAt(previousX, y, out _, out PathTileSnapshot sideY) ||
                             !IsFastTileSafe(sideX, profile) || !IsFastTileSafe(sideY, profile)))
            {
                steps = null;
                endTileId = startTileId;
                return false;
            }

            grid.TryGetTile(currentTileId, out PathTileSnapshot current);
            MovementMethod method = DecideMethod(next, profile);
            TraversalEstimate estimate = EstimateTraversal(current, next, diagonal, method, state, profile);
            state = state.Advance(estimate, profile);
            buffer[count++] = new PathStep(nextTileId, method, estimate, plannedTileFlags: next.Flags);
            currentTileId = nextTileId;
        }

        if (count == 0)
        {
            steps = Array.Empty<PathStep>();
            endTileId = startTileId;
            return startTileId == targetTileId;
        }

        if (count == buffer.Length)
        {
            steps = buffer;
        }
        else
        {
            steps = new PathStep[count];
            Array.Copy(buffer, steps, count);
        }

        endTileId = currentTileId;
        return true;
    }

    private static bool IsFastTileSafe(PathTileSnapshot tile, MovementProfile profile)
    {
        if (!tile.Exists || !tile.HasType) return false;
        if (profile.IsBoat) return tile.Ocean && !tile.Lava && !tile.Block;
        if (tile.Block && !profile.IgnoreBlocks && !profile.IsFlying) return false;
        if (tile.Lava && !profile.AllowLava) return false;
        if (tile.IsOnFire && !profile.IsFireImmune) return false;
        if (tile.DamageUnits) return false;
        if (tile.Liquid && !profile.IsFlying && !profile.IsWaterCreature && !profile.PreferWater) return false;
        return true;
    }

    private static LocalPathResult TryBuildLocalPath(int startTileId, int targetTileId,
        PathNavigationGrid grid, MovementProfile profile, int maxNodes, float heuristicWeight,
        RegionCorridor corridor, SearchWorkspace workspace, CancellationToken token)
    {
        if (startTileId == targetTileId)
        {
            return LocalPathResult.Success(Array.Empty<PathStep>(), 0f, 0);
        }

        if (!grid.TryGetTile(startTileId, out _) || !grid.TryGetTile(targetTileId, out _))
        {
            return LocalPathResult.Fail(false, 0);
        }

        maxNodes = Math.Max(1, maxNodes);
        workspace.Begin(maxNodes, profile.MaxLabelsPerTile);
        TraversalState initialState = TraversalState.Start(profile);
        var startNode = new SearchNode(startTileId, -1, MovementMethod.Walk, default, default, initialState,
            0f, Heuristic(grid, startTileId, targetTileId, profile) * heuristicWeight);
        workspace.AddStart(startNode);

        int expanded = 0;
        while (workspace.TryDequeue(out int currentIndex) && expanded < maxNodes)
        {
            if ((expanded & 31) == 0) token.ThrowIfCancellationRequested();
            SearchNode current = workspace.GetNode(currentIndex);
            if (!current.Active) continue;
            expanded++;
            if (current.TileId == targetTileId)
            {
                return LocalPathResult.Success(workspace.BuildPath(currentIndex), current.G, expanded);
            }

            int currentX = grid.XOf(current.TileId);
            int currentY = grid.YOf(current.TileId);
            if (!grid.TryGetTile(current.TileId, out PathTileSnapshot currentTile)) continue;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    if (!grid.TryGetTileAt(currentX + offsetX, currentY + offsetY,
                            out int neighbourId, out PathTileSnapshot neighbour) || !neighbour.HasType)
                    {
                        continue;
                    }

                    if (corridor != null && !corridor.Contains(neighbour.RegionId) && neighbourId != targetTileId)
                    {
                        continue;
                    }

                    bool diagonal = offsetX != 0 && offsetY != 0;
                    MovementMethod method = DecideMethod(neighbour, profile);
                    TraversalEstimate estimate = EstimateTraversal(currentTile, neighbour, diagonal, method,
                        current.State, profile);
                    TraversalState nextState = current.State.Advance(estimate, profile);
                    float g = current.G + profile.CostOf(estimate, nextState);
                    float h = Heuristic(grid, neighbourId, targetTileId, profile) * heuristicWeight;
                    var candidate = new SearchNode(neighbourId, currentIndex, method, estimate, neighbour.Flags,
                        nextState, g, h);
                    workspace.TryAdd(candidate);
                }
            }
        }

        bool hitLimit = expanded >= maxNodes || workspace.CapacityHit;
        return LocalPathResult.Fail(hitLimit, expanded);
    }

    private static TraversalEstimate EstimateTraversal(PathTileSnapshot from, PathTileSnapshot to, bool diagonal,
        MovementMethod method, TraversalState state, MovementProfile profile)
    {
        HazardFlags hazards = HazardFlags.None;
        float distance = diagonal ? 1.4142f : 1f;
        float speed = profile.GetSpeed(to, method, state);
        float time = distance / Mathf.Max(speed, 0.01f);
        float staminaCost = 0f;
        float healthCost = 0f;
        float riskCost = 0f;

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

        if (to.Ocean || to.Liquid && !to.Lava)
        {
            hazards |= HazardFlags.Ocean;
            if (!profile.IsWaterCreature && !profile.IsFlying)
            {
                hazards |= HazardFlags.StaminaDrain;
                staminaCost += time * profile.WaterStaminaDrainPerSecond;
                riskCost += profile.OceanRiskCost;
                float exhausted = Mathf.Max(0f, staminaCost - state.Stamina);
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
            float damage = time * to.Damage * profile.TerrainDamageTicksPerSecond;
            healthCost += profile.EstimateEnvironmentalDamage(damage);
            riskCost += profile.TerrainDamageRiskCost;
        }

        if (state.Health - healthCost <= profile.LowHealthThreshold)
        {
            hazards |= HazardFlags.LowHealth;
        }

        return new TraversalEstimate(time, staminaCost, healthCost, riskCost, hazards);
    }

    private static MovementMethod DecideMethod(PathTileSnapshot tile, MovementProfile profile)
    {
        if (profile.IsBoat) return MovementMethod.Swim;
        return tile.Liquid ? MovementMethod.Swim : MovementMethod.Walk;
    }

    private static float Heuristic(PathNavigationGrid grid, int firstTileId, int secondTileId,
        MovementProfile profile)
    {
        return grid.ManhattanDistance(firstTileId, secondTileId) /
               Mathf.Max(profile.BestCaseSpeed, 0.01f);
    }

    private static int ResolveTraversalClass(PathRequest request)
    {
        int result = 0;
        if (request.ActorIsBoat) result |= 1;
        if (request.ActorIsWaterCreature) result |= 1 << 1;
        if (request.ActorIsFlying) result |= 1 << 2;
        if (request.ActorIgnoresBlocks) result |= 1 << 3;
        if (request.PathOnWater) result |= 1 << 4;
        if (request.WalkOnLava) result |= 1 << 5;
        return result;
    }

    private readonly struct PortalChoice
    {
        internal PortalChoice(PathPortalSnapshot entry, PathPortalSnapshot exit, float transferCost)
        {
            Entry = entry;
            Exit = exit;
            TransferCost = transferCost;
        }

        internal PathPortalSnapshot Entry { get; }
        internal PathPortalSnapshot Exit { get; }
        internal float TransferCost { get; }
    }

    private readonly struct LocalPathResult
    {
        private LocalPathResult(bool success, PathStep[] steps, float cost, bool hitNodeLimit, int expandedNodes)
        {
            IsSuccess = success;
            Steps = steps;
            Cost = cost;
            HitNodeLimit = hitNodeLimit;
            ExpandedNodes = expandedNodes;
        }

        internal bool IsSuccess { get; }
        internal PathStep[] Steps { get; }
        internal float Cost { get; }
        internal bool HitNodeLimit { get; }
        internal int ExpandedNodes { get; }

        internal static LocalPathResult Success(PathStep[] steps, float cost, int expandedNodes)
        {
            return new LocalPathResult(true, steps, cost, false, expandedNodes);
        }

        internal static LocalPathResult Fail(bool hitNodeLimit, int expandedNodes)
        {
            return new LocalPathResult(false, Array.Empty<PathStep>(), float.MaxValue, hitNodeLimit, expandedNodes);
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

        internal float Stamina { get; }
        internal float Health { get; }
        internal float Risk { get; }

        internal static TraversalState Start(MovementProfile profile)
        {
            return new TraversalState(profile.CurrentStamina, profile.CurrentHealth, 0f);
        }

        internal TraversalState Advance(TraversalEstimate estimate, MovementProfile profile)
        {
            float stamina = Mathf.Clamp(Stamina - estimate.StaminaCost +
                                         estimate.TimeSeconds * profile.StaminaRegenPerSecond,
                0f, profile.MaxStamina);
            return new TraversalState(stamina, Health - estimate.HealthCost, Risk + estimate.RiskCost);
        }
    }

    private struct SearchNode
    {
        internal SearchNode(int tileId, int parentIndex, MovementMethod method, TraversalEstimate estimate,
            PathTileFlags plannedTileFlags, TraversalState state, float g, float h)
        {
            TileId = tileId;
            ParentIndex = parentIndex;
            Method = method;
            Estimate = estimate;
            PlannedTileFlags = plannedTileFlags;
            State = state;
            G = g;
            H = h;
            Active = true;
        }

        internal int TileId;
        internal int ParentIndex;
        internal MovementMethod Method;
        internal TraversalEstimate Estimate;
        internal PathTileFlags PlannedTileFlags;
        internal TraversalState State;
        internal float G;
        internal float H;
        internal bool Active;
        internal float F => G + H;
    }

    /// <summary>
    /// 每个常驻工作线程复用一份搜索工作区，避免为每个节点创建对象、字典和标签列表。
    /// </summary>
    private sealed class SearchWorkspace
    {
        private SearchNode[] nodes = Array.Empty<SearchNode>();
        private int[] heap = Array.Empty<int>();
        private int[] slotStamps = Array.Empty<int>();
        private int[] slotKeys = Array.Empty<int>();
        private byte[] slotCounts = Array.Empty<byte>();
        private int[] slotLabels = Array.Empty<int>();
        private int nodeCount;
        private int heapCount;
        private int stamp;
        private int slotMask;
        private int maxLabels;

        internal bool CapacityHit { get; private set; }

        internal void Begin(int maxNodes, int requestedMaxLabels)
        {
            maxLabels = Mathf.Clamp(requestedMaxLabels, 1, 4);
            int nodeCapacity = Math.Max(64, maxNodes * maxLabels + 8);
            if (nodes.Length < nodeCapacity) nodes = new SearchNode[nodeCapacity];
            if (heap.Length < nodeCapacity) heap = new int[nodeCapacity];

            int slotCapacity = NextPowerOfTwo(Math.Max(128, maxNodes * 8));
            if (slotStamps.Length < slotCapacity)
            {
                slotStamps = new int[slotCapacity];
                slotKeys = new int[slotCapacity];
                slotCounts = new byte[slotCapacity];
            }

            if (slotLabels.Length < slotStamps.Length * maxLabels)
            {
                slotLabels = new int[slotStamps.Length * maxLabels];
            }

            slotMask = slotStamps.Length - 1;
            stamp++;
            if (stamp == 0)
            {
                Array.Clear(slotStamps, 0, slotStamps.Length);
                stamp = 1;
            }

            nodeCount = 0;
            heapCount = 0;
            CapacityHit = false;
        }

        internal SearchNode GetNode(int index)
        {
            return nodes[index];
        }

        internal void AddStart(SearchNode node)
        {
            int index = Allocate(node);
            int slot = FindSlot(node.TileId);
            slotCounts[slot] = 1;
            slotLabels[slot * maxLabels] = index;
            Enqueue(index);
        }

        internal bool TryAdd(SearchNode candidate)
        {
            int slot = FindSlot(candidate.TileId);
            int offset = slot * maxLabels;
            int count = slotCounts[slot];
            for (int i = 0; i < count; i++)
            {
                SearchNode existing = nodes[slotLabels[offset + i]];
                if (existing.Active && Dominates(existing, candidate)) return false;
            }

            int write = 0;
            for (int i = 0; i < count; i++)
            {
                int existingIndex = slotLabels[offset + i];
                SearchNode existing = nodes[existingIndex];
                if (existing.Active && Dominates(candidate, existing))
                {
                    nodes[existingIndex].Active = false;
                    continue;
                }

                slotLabels[offset + write++] = existingIndex;
            }

            count = write;
            if (count >= maxLabels)
            {
                int worstPosition = -1;
                float worstScore = candidate.F;
                for (int i = 0; i < count; i++)
                {
                    float score = nodes[slotLabels[offset + i]].F;
                    if (score <= worstScore) continue;
                    worstScore = score;
                    worstPosition = i;
                }

                if (worstPosition < 0) return false;
                nodes[slotLabels[offset + worstPosition]].Active = false;
                for (int i = worstPosition; i < count - 1; i++)
                {
                    slotLabels[offset + i] = slotLabels[offset + i + 1];
                }

                count--;
            }

            if (nodeCount >= nodes.Length)
            {
                CapacityHit = true;
                return false;
            }

            int index = Allocate(candidate);
            slotLabels[offset + count] = index;
            slotCounts[slot] = (byte)(count + 1);
            Enqueue(index);
            return true;
        }

        internal bool TryDequeue(out int nodeIndex)
        {
            while (heapCount > 0)
            {
                nodeIndex = heap[0];
                heapCount--;
                if (heapCount > 0)
                {
                    int replacement = heap[heapCount];
                    int index = 0;
                    while (true)
                    {
                        int left = index * 2 + 1;
                        if (left >= heapCount) break;
                        int right = left + 1;
                        int child = right < heapCount && Compare(heap[right], heap[left]) < 0 ? right : left;
                        if (Compare(replacement, heap[child]) <= 0) break;
                        heap[index] = heap[child];
                        index = child;
                    }

                    heap[index] = replacement;
                }

                if (nodes[nodeIndex].Active) return true;
            }

            nodeIndex = -1;
            return false;
        }

        internal PathStep[] BuildPath(int targetIndex)
        {
            int count = 0;
            for (int index = targetIndex; index >= 0 && nodes[index].ParentIndex >= 0;
                 index = nodes[index].ParentIndex)
            {
                count++;
            }

            var result = new PathStep[count];
            int write = count - 1;
            for (int index = targetIndex; index >= 0 && nodes[index].ParentIndex >= 0;
                 index = nodes[index].ParentIndex)
            {
                SearchNode node = nodes[index];
                result[write--] = new PathStep(node.TileId, node.Method, node.Estimate,
                    plannedTileFlags: node.PlannedTileFlags);
            }

            return result;
        }

        private int Allocate(SearchNode node)
        {
            int index = nodeCount++;
            nodes[index] = node;
            return index;
        }

        private int FindSlot(int tileId)
        {
            int slot = unchecked(tileId * -1640531527) & slotMask;
            while (slotStamps[slot] == stamp && slotKeys[slot] != tileId)
            {
                slot = (slot + 1) & slotMask;
            }

            if (slotStamps[slot] != stamp)
            {
                slotStamps[slot] = stamp;
                slotKeys[slot] = tileId;
                slotCounts[slot] = 0;
            }

            return slot;
        }

        private void Enqueue(int nodeIndex)
        {
            int index = heapCount++;
            while (index > 0)
            {
                int parent = (index - 1) >> 1;
                if (Compare(nodeIndex, heap[parent]) >= 0) break;
                heap[index] = heap[parent];
                index = parent;
            }

            heap[index] = nodeIndex;
        }

        private int Compare(int first, int second)
        {
            float firstF = nodes[first].F;
            float secondF = nodes[second].F;
            int result = firstF.CompareTo(secondF);
            return result != 0 ? result : nodes[first].H.CompareTo(nodes[second].H);
        }

        private static bool Dominates(SearchNode first, SearchNode second)
        {
            return first.G <= second.G + 0.001f &&
                   first.State.Stamina >= second.State.Stamina - 0.001f &&
                   first.State.Health >= second.State.Health - 0.001f &&
                   first.State.Risk <= second.State.Risk + 0.001f;
        }

        private static int NextPowerOfTwo(int value)
        {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }

    private sealed class RegionCorridor
    {
        private readonly HashSet<int> regionIds;
        private readonly int expansionDepth;
        private readonly PathRegionTopology topology;

        private RegionCorridor(HashSet<int> regionIds, int expansionDepth, PathRegionTopology topology)
        {
            this.regionIds = regionIds;
            this.expansionDepth = expansionDepth;
            this.topology = topology;
        }

        internal bool Contains(int regionId)
        {
            return regionId < 0 || regionIds.Contains(regionId);
        }

        internal static RegionCorridor Create(PathRegionTopology topology, int[] route)
        {
            var ids = new HashSet<int>(route);
            AddNeighbourRing(topology, ids);
            return new RegionCorridor(ids, 1, topology);
        }

        internal RegionCorridor Expand()
        {
            if (expansionDepth >= 2) return this;
            var ids = new HashSet<int>(regionIds);
            AddNeighbourRing(topology, ids);
            return new RegionCorridor(ids, expansionDepth + 1, topology);
        }

        private static void AddNeighbourRing(PathRegionTopology topology, HashSet<int> ids)
        {
            var source = new int[ids.Count];
            ids.CopyTo(source);
            for (int i = 0; i < source.Length; i++)
            {
                if (!topology.TryGetRegion(source[i], out PathRegionSnapshot region)) continue;
                for (int n = 0; n < region.Neighbours.Length; n++) ids.Add(region.Neighbours[n]);
            }
        }
    }

    private sealed class RegionRouteCache
    {
        private readonly int capacity;
        private readonly object syncRoot = new();
        private readonly Dictionary<RegionRouteKey, LinkedListNode<RegionRouteEntry>> entries = new();
        private readonly LinkedList<RegionRouteEntry> lru = new();

        internal RegionRouteCache(int capacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        internal int[] GetOrBuild(int gridIdentity, PathRegionTopology topology, int startRegion,
            int targetRegion, int traversalClass)
        {
            var key = new RegionRouteKey(gridIdentity, topology.Revision, startRegion, targetRegion,
                traversalClass);
            lock (syncRoot)
            {
                if (entries.TryGetValue(key, out LinkedListNode<RegionRouteEntry> cached))
                {
                    lru.Remove(cached);
                    lru.AddFirst(cached);
                    return cached.Value.Route;
                }
            }

            int[] route = BuildRoute(topology, startRegion, targetRegion);
            lock (syncRoot)
            {
                if (entries.TryGetValue(key, out LinkedListNode<RegionRouteEntry> existing))
                {
                    lru.Remove(existing);
                    lru.AddFirst(existing);
                    return existing.Value.Route;
                }

                var node = new LinkedListNode<RegionRouteEntry>(new RegionRouteEntry(key, route));
                lru.AddFirst(node);
                entries.Add(key, node);
                while (entries.Count > capacity)
                {
                    LinkedListNode<RegionRouteEntry> last = lru.Last;
                    if (last == null) break;
                    lru.RemoveLast();
                    entries.Remove(last.Value.Key);
                }
            }

            return route;
        }

        private static int[] BuildRoute(PathRegionTopology topology, int startRegion, int targetRegion)
        {
            if (startRegion == targetRegion) return new[] { startRegion };
            if (!topology.TryGetRegion(startRegion, out _) || !topology.TryGetRegion(targetRegion, out _))
            {
                return null;
            }

            var parents = new Dictionary<int, int>();
            var queue = new Queue<int>();
            parents[startRegion] = int.MinValue;
            queue.Enqueue(startRegion);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!topology.TryGetRegion(current, out PathRegionSnapshot region)) continue;
                for (int i = 0; i < region.Neighbours.Length; i++)
                {
                    int neighbour = region.Neighbours[i];
                    if (parents.ContainsKey(neighbour)) continue;
                    parents[neighbour] = current;
                    if (neighbour == targetRegion)
                    {
                        return Reconstruct(parents, startRegion, targetRegion);
                    }

                    queue.Enqueue(neighbour);
                }
            }

            return null;
        }

        private static int[] Reconstruct(Dictionary<int, int> parents, int startRegion, int targetRegion)
        {
            var reversed = new List<int>();
            int current = targetRegion;
            while (current != int.MinValue)
            {
                reversed.Add(current);
                if (current == startRegion) break;
                if (!parents.TryGetValue(current, out current)) return null;
            }

            reversed.Reverse();
            return reversed.ToArray();
        }

        private readonly struct RegionRouteKey : IEquatable<RegionRouteKey>
        {
            internal RegionRouteKey(int gridIdentity, int revision, int start, int target, int traversalClass)
            {
                GridIdentity = gridIdentity;
                Revision = revision;
                Start = start;
                Target = target;
                TraversalClass = traversalClass;
            }

            private int GridIdentity { get; }
            private int Revision { get; }
            private int Start { get; }
            private int Target { get; }
            private int TraversalClass { get; }

            public bool Equals(RegionRouteKey other)
            {
                return GridIdentity == other.GridIdentity && Revision == other.Revision && Start == other.Start &&
                       Target == other.Target && TraversalClass == other.TraversalClass;
            }

            public override bool Equals(object obj)
            {
                return obj is RegionRouteKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = GridIdentity;
                    hash = hash * 397 ^ Revision;
                    hash = hash * 397 ^ Start;
                    hash = hash * 397 ^ Target;
                    hash = hash * 397 ^ TraversalClass;
                    return hash;
                }
            }
        }

        private readonly struct RegionRouteEntry
        {
            internal RegionRouteEntry(RegionRouteKey key, int[] route)
            {
                Key = key;
                Route = route;
            }

            internal RegionRouteKey Key { get; }
            internal int[] Route { get; }
        }
    }

    private sealed class MovementProfile
    {
        private MovementProfile(PathfindingConfig config)
        {
            Config = config;
        }

        private PathfindingConfig Config { get; }
        internal bool IgnoreBlocks { get; private set; }
        internal bool DieOnBlocks { get; private set; }
        internal bool IsBoat { get; private set; }
        internal bool IsWaterCreature { get; private set; }
        internal bool IsFlying { get; private set; }
        internal bool IsFireImmune { get; private set; }
        internal bool IsDamagedByOcean { get; private set; }
        internal bool IsLavaDamaging { get; private set; }
        internal bool HasFastSwimming { get; private set; }
        internal bool PreferWater { get; private set; }
        internal bool AllowLava { get; private set; }
        internal int MaxLabelsPerTile { get; private set; }
        internal float CurrentStamina { get; private set; }
        internal float MaxStamina { get; private set; }
        internal float CurrentHealth { get; private set; }
        internal float MaxHealth { get; private set; }
        internal float LowHealthThreshold { get; private set; }
        internal float WalkSpeed { get; private set; }
        internal float SwimSpeed { get; private set; }
        internal float SailSpeed { get; private set; }
        internal float BestCaseSpeed { get; private set; }
        internal float PowerLevel { get; private set; }
        internal float StaminaRegenPerSecond { get; private set; }
        internal float WaterStaminaDrainPerSecond { get; private set; }
        internal float DrowningDamagePerSecond { get; private set; }
        internal float WaterDamagePerSecond { get; private set; }
        internal float TerrainDamageTicksPerSecond { get; private set; }
        internal float BlockDamagePerSecond { get; private set; }
        internal float BlockRiskCost { get; private set; }
        internal float FireRiskCost { get; private set; }
        internal float OceanRiskCost { get; private set; }
        internal float LavaRiskCost { get; private set; }
        internal float TerrainDamageRiskCost { get; private set; }

        internal static MovementProfile Build(PathRequest request, PathfindingConfig config)
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
                PreferWater = request.PathOnWater,
                AllowLava = request.WalkOnLava || request.ActorIsFireImmune,
                MaxLabelsPerTile = Mathf.Clamp(config.MaxLabelsPerTile, 1, 4),
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
            float baseSpeed = request.ActorBaseSpeed;
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

            profile.BestCaseSpeed = Mathf.Max(profile.WalkSpeed,
                Mathf.Max(profile.SwimSpeed, profile.SailSpeed));
            return profile;
        }

        internal float GetSpeed(PathTileSnapshot tile, MovementMethod method, TraversalState state)
        {
            float speed = method switch
            {
                MovementMethod.Swim => IsBoat ? SailSpeed : SwimSpeed,
                MovementMethod.Portal => SailSpeed,
                _ => WalkSpeed
            };

            if (tile.HasType && !IsFlying && !IsWaterCreature && method == MovementMethod.Walk)
            {
                speed *= Mathf.Max(tile.WalkMultiplier, 0.05f);
            }

            if (tile.Lava && !IsFlying && !IsWaterCreature)
            {
                speed *= Mathf.Max(tile.WalkMultiplier, 0.05f);
            }

            if (method == MovementMethod.Swim && !IsWaterCreature && !IsBoat && state.Stamina <= 0f &&
                !HasFastSwimming)
            {
                speed *= Config.ExhaustedSwimSpeedScale;
            }

            if (IsBoat && tile.HasType && !tile.Ocean) speed *= 0.05f;
            return Mathf.Max(speed, 0.01f);
        }

        internal float EstimateOpenTerrainCost(int distance)
        {
            return distance / Mathf.Max(WalkSpeed, 0.01f);
        }

        internal float EstimateEnvironmentalDamage(float rawDamage)
        {
            if (rawDamage <= 0f) return 0f;
            if (PowerLevel <= 0f) return rawDamage;
            float divisor = Mathf.Pow(DamageCalcHyperParameters.PowerBase, PowerLevel);
            float adjusted = Mathf.Log(Mathf.Max(rawDamage, 1f), divisor);
            if (adjusted < 1f) return 0f;
            return Mathf.Max(adjusted, rawDamage * Config.XianEnvironmentalDamageFloor);
        }

        internal float CostOf(TraversalEstimate estimate, TraversalState nextState)
        {
            float cost = estimate.TimeSeconds + estimate.StaminaCost * Config.StaminaCostWeight +
                         estimate.HealthCost * Config.HealthCostWeight + estimate.RiskCost;
            if (nextState.Health <= 0f)
            {
                cost += Config.DeathRiskCost;
            }
            else if (nextState.Health <= LowHealthThreshold)
            {
                float missing = Mathf.Clamp01((LowHealthThreshold - nextState.Health) / LowHealthThreshold);
                cost += Config.LowHealthRiskCost * (0.25f + missing);
            }

            return cost;
        }
    }
}

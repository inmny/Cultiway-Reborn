using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cultiway.Const;
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
        if (request.StartTileId < 0 || request.TargetTileId < 0)
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
            ModClass.LogErrorConcurrent(SystemUtils.GetFullExceptionMessage(e));
            stream.Fail(e);
        }

        stream.EnsureCompleted();
        return Task.CompletedTask;
    }

    private void GenerateInternal(PathRequest request, IPathStreamWriter stream, CancellationToken token)
    {
        var profile = MovementProfile.Build(request, _config);

        var direct = TryBuildLocalPath(request.StartTileId, request.TargetTileId, profile, useLongRange: true, token);
        RouteCandidate bestCandidate = null;
        var bestCost = float.MaxValue;
        if (direct.IsSuccess)
        {
            bestCandidate = RouteCandidate.FromSegments(direct.Steps, direct.Cost);
            bestCost = direct.Cost;
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

                var toEntry = TryBuildLocalPath(request.StartTileId, TileTraversalInfo.TileIdOf(bestEstimate.Entry.Tile), profile,
                    useLongRange: true, token);
                if (!toEntry.IsSuccess)
                {
                    goto OUTSIDE;
                }

                var exitToTarget = TryBuildLocalPath(TileTraversalInfo.TileIdOf(bestEstimate.Exit.Tile), request.TargetTileId, profile,
                    useLongRange: true, token);
                if (!exitToTarget.IsSuccess)
                {
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
            return;
        }

        EmitCandidate(bestCandidate, stream, token);
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

    private LocalPathResult TryBuildLocalPath(int startId, int targetId, MovementProfile profile, bool useLongRange,
        CancellationToken token)
    {
        if (startId == targetId)
        {
            return LocalPathResult.Success(Array.Empty<PathStep>(), 0);
        }

        var tileInfoCache = new Dictionary<int, TileTraversalInfo>(512);
        if (!TryGetTileInfo(tileInfoCache, startId, out var startInfo) ||
            !TryGetTileInfo(tileInfoCache, targetId, out var targetInfo))
        {
            return LocalPathResult.Fail();
        }

        var maxNodes = useLongRange ? profile.MaxNodesLong : profile.MaxNodesShort;
        var result = TryBuildLocalPathCore(startId, targetId, startInfo, targetInfo, tileInfoCache, profile, maxNodes,
            corridorLimit: 0, token);
        if (result.IsSuccess || !useLongRange || !result.HitNodeLimit)
        {
            return result;
        }

        var directDistance = DistTile(startInfo, targetInfo);
        var detour = Mathf.Max(profile.FallbackCorridorMinDetour,
            Mathf.RoundToInt(directDistance * profile.FallbackCorridorDetourScale));
        var fallbackNodes = Mathf.Max(profile.MaxNodesLongFallback, profile.MaxNodesLong);
        return TryBuildLocalPathCore(startId, targetId, startInfo, targetInfo, tileInfoCache, profile, fallbackNodes,
            directDistance + detour, token);
    }

    private LocalPathResult TryBuildLocalPathCore(int startId, int targetId, TileTraversalInfo startInfo,
        TileTraversalInfo targetInfo, Dictionary<int, TileTraversalInfo> tileInfoCache, MovementProfile profile,
        int maxNodes, int corridorLimit, CancellationToken token)
    {
        maxNodes = Mathf.Max(1, maxNodes);
        var open = new PriorityQueuePreview<PathNode>(128, PathNodeComparer.Instance);
        var labelsByTile = new Dictionary<int, List<PathNode>>(256);
        var startNode = new PathNode(startId, null, MovementMethod.Walk, default,
            TraversalState.Start(profile), 0, Heuristic(startInfo, targetInfo, profile));

        AddLabel(labelsByTile, startNode, profile);
        open.Enqueue(startNode);

        var expanded = 0;
        while (open.Count > 0 && expanded < maxNodes)
        {
            token.ThrowIfCancellationRequested();
            var current = open.Dequeue();
            if (!IsActiveLabel(labelsByTile, current))
            {
                continue;
            }

            expanded++;
            if (current.TileId == targetId)
            {
                return BuildResult(current);
            }

            if (!TryGetTileInfo(tileInfoCache, current.TileId, out var currentInfo))
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
                if (!TryGetTileInfo(tileInfoCache, neighbourId, out var neighbour) || !neighbour.HasType)
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
                var node = new PathNode(neighbourId, current, method, estimate, nextState,
                    current.G + stepCost, Heuristic(neighbour, targetInfo, profile));

                if (!TryAddLabel(labelsByTile, node, profile))
                {
                    continue;
                }

                open.Enqueue(node);
            }
        }

        return LocalPathResult.Fail(open.Count > 0 && expanded >= maxNodes);
    }

    private static bool IsActiveLabel(Dictionary<int, List<PathNode>> labelsByTile, PathNode node)
    {
        return labelsByTile.TryGetValue(node.TileId, out var labels) && labels.Contains(node);
    }

    private static void AddLabel(Dictionary<int, List<PathNode>> labelsByTile, PathNode node, MovementProfile profile)
    {
        if (!labelsByTile.TryGetValue(node.TileId, out var labels))
        {
            labels = new List<PathNode>(profile.MaxLabelsPerTile);
            labelsByTile.Add(node.TileId, labels);
        }

        labels.Add(node);
    }

    private static bool TryAddLabel(Dictionary<int, List<PathNode>> labelsByTile, PathNode node,
        MovementProfile profile)
    {
        if (!labelsByTile.TryGetValue(node.TileId, out var labels))
        {
            labels = new List<PathNode>(profile.MaxLabelsPerTile);
            labelsByTile.Add(node.TileId, labels);
        }

        for (int i = 0; i < labels.Count; i++)
        {
            if (Dominates(labels[i], node))
            {
                return false;
            }
        }

        for (int i = labels.Count - 1; i >= 0; i--)
        {
            if (Dominates(node, labels[i]))
            {
                labels.RemoveAt(i);
            }
        }

        labels.Add(node);
        if (labels.Count > profile.MaxLabelsPerTile)
        {
            var worstIndex = 0;
            var worstScore = labels[0].F;
            for (int i = 1; i < labels.Count; i++)
            {
                if (labels[i].F > worstScore)
                {
                    worstScore = labels[i].F;
                    worstIndex = i;
                }
            }

            if (labels[worstIndex] == node)
            {
                labels.RemoveAt(worstIndex);
                return false;
            }

            labels.RemoveAt(worstIndex);
        }

        return true;
    }

    private static bool Dominates(PathNode a, PathNode b)
    {
        return a.G <= b.G + 0.001f
               && a.State.Stamina >= b.State.Stamina - 0.001f
               && a.State.Health >= b.State.Health - 0.001f
               && a.State.Risk <= b.State.Risk + 0.001f;
    }

    private LocalPathResult BuildResult(PathNode node)
    {
        var reversed = new List<PathStep>();
        var current = node;
        while (current.Parent != null)
        {
            reversed.Add(new PathStep(current.TileId, current.Method, current.Estimate));
            current = current.Parent;
        }

        reversed.Reverse();
        return LocalPathResult.Success(reversed, node.G);
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

    private static bool TryGetTileInfo(Dictionary<int, TileTraversalInfo> cache, int tileId,
        out TileTraversalInfo info)
    {
        if (tileId < 0)
        {
            info = default;
            return false;
        }

        if (cache.TryGetValue(tileId, out info))
        {
            return info.Exists;
        }

        if (!TileTraversalInfo.TryGet(tileId, out info))
        {
            return false;
        }

        cache[tileId] = info;
        return true;
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

    private sealed class PathNode
    {
        public PathNode(int tileId, PathNode parent, MovementMethod method, TraversalEstimate estimate,
            TraversalState state, float g, float h)
        {
            TileId = tileId;
            Parent = parent;
            Method = method;
            Estimate = estimate;
            State = state;
            G = g;
            H = h;
        }

        public int TileId { get; }
        public PathNode Parent { get; }
        public MovementMethod Method { get; }
        public TraversalEstimate Estimate { get; }
        public TraversalState State { get; }
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

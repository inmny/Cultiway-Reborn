using System;
using System.Collections.Generic;
using System.Threading;
using Cultiway.Const;
using Cultiway.Core;
using UnityEngine;

namespace Cultiway.Content.SpiritVeins;

/// <summary>一次后台风水龙脉生成的完整结果。</summary>
internal sealed class SpiritVeinGenerationResult
{
    internal SpiritVeinGenerationResult(
        int worldSeedId,
        int width,
        int height,
        List<SpiritVeinDraft> veins,
        List<SpiritVeinBranch> branches,
        List<SpiritVeinSection> sections,
        List<GatheringGround> grounds,
        List<SpiritVeinEye> eyes,
        SpiritVeinFieldSnapshot field)
    {
        WorldSeedId = worldSeedId;
        Width = width;
        Height = height;
        Veins = veins;
        Branches = branches;
        Sections = sections;
        Grounds = grounds;
        Eyes = eyes;
        Field = field;
    }

    internal int WorldSeedId { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal List<SpiritVeinDraft> Veins { get; }
    internal List<SpiritVeinBranch> Branches { get; }
    internal List<SpiritVeinSection> Sections { get; }
    internal List<GatheringGround> Grounds { get; }
    internal List<SpiritVeinEye> Eyes { get; }
    internal SpiritVeinFieldSnapshot Field { get; }
}

/// <summary>从山川地势生成祖山、宽广脉域、脉节、结穴地和自然灵眼。</summary>
internal static class SpiritVeinGenerator
{
    private static readonly (int x, int y)[] Directions =
    {
        (1, 0), (0, 1), (-1, 0), (0, -1),
        (1, 1), (-1, 1), (-1, -1), (1, -1)
    };

    internal static SpiritVeinGenerationResult Generate(
        SpiritVeinTerrainSnapshot terrain,
        CancellationToken cancellationToken = default)
    {
        if (terrain == null) throw new ArgumentNullException(nameof(terrain));
        cancellationToken.ThrowIfCancellationRequested();

        List<int> sourceCandidates = CollectSourceCandidates(terrain);
        List<int> outletCandidates = CollectOutletCandidates(terrain);
        int targetCount = ResolveTargetCount(terrain, sourceCandidates.Count, outletCandidates.Count);
        List<int> selectedSources = SelectSources(terrain, sourceCandidates, targetCount * 3);

        var veins = new List<SpiritVeinDraft>(targetCount);
        var branches = new List<SpiritVeinBranch>(targetCount * 2);
        var routes = new List<GuideRoute>(targetCount * 3);
        var occupiedGuides = new HashSet<int>();
        int nextBranchId = 1;

        for (int sourceIndex = 0; sourceIndex < selectedSources.Count && veins.Count < targetCount; sourceIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int source = selectedSources[sourceIndex];
            List<int> mainRoute = FindMainRoute(
                terrain,
                source,
                outletCandidates,
                sourceIndex,
                occupiedGuides,
                cancellationToken);
            if (mainRoute.Count < SpiritVeinSettings.MainMinimumLength) continue;

            int veinId = veins.Count + 1;
            int outlet = mainRoute[mainRoute.Count - 1];
            int[] sourceDomain = CollectSourceDomain(terrain, source);
            var vein = new SpiritVeinDraft(
                veinId,
                source,
                outlet,
                sourceDomain,
                DragonVeinScale.Micro,
                CalculateComposition(terrain, mainRoute));
            veins.Add(vein);

            var mainGuide = new GuideRoute(veinId, -1, true, source, outlet, mainRoute);
            routes.Add(mainGuide);
            AddOccupied(occupiedGuides, mainRoute);

            int desiredBranches = Mathf.Clamp(mainRoute.Count / 70, 1, 3);
            for (int branchIndex = 0; branchIndex < desiredBranches; branchIndex++)
            {
                int joinIndex = Mathf.Clamp(
                    Mathf.RoundToInt(mainRoute.Count * (0.32f + branchIndex * 0.2f)),
                    SpiritVeinSettings.BranchMinimumLength,
                    mainRoute.Count - 4);
                int joinTile = mainRoute[joinIndex];
                int branchSource = FindBranchSource(terrain, joinTile, occupiedGuides, veinId, branchIndex);
                if (branchSource < 0) continue;

                List<int> branchRoute = FindRoute(
                    terrain,
                    branchSource,
                    joinTile,
                    occupiedGuides,
                    false,
                    cancellationToken);
                TrimAtFirstIntersection(branchRoute, occupiedGuides, joinTile);
                if (branchRoute.Count < SpiritVeinSettings.BranchMinimumLength) continue;

                int branchId = nextBranchId++;
                var branch = new SpiritVeinBranch(
                    branchId,
                    veinId,
                    branchSource,
                    branchRoute[branchRoute.Count - 1],
                    SpiritBranchScale.Micro,
                    CalculateComposition(terrain, branchRoute));
                branches.Add(branch);
                vein.BranchIds.Add(branchId);
                routes.Add(new GuideRoute(
                    veinId,
                    branchId,
                    false,
                    branchSource,
                    branchRoute[branchRoute.Count - 1],
                    branchRoute));
                AddOccupied(occupiedGuides, branchRoute);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sectionPlans = BuildSectionPlans(terrain, routes);
        ConnectSectionPlans(sectionPlans, routes);
        SpiritVeinFieldSnapshot field = BuildField(terrain, veins, routes, sectionPlans, cancellationToken);
        ComputeFengShui(terrain, field, cancellationToken);

        var grounds = BuildGrounds(terrain, veins, sectionPlans, field, cancellationToken);
        var sections = BuildSections(terrain, veins, branches, sectionPlans, field);
        ResolveVeinScalesAndElements(veins, branches, sections);
        var eyes = BuildEyes(terrain, veins, sections, grounds, field);
        ApplyEyeCharacteristics(sections, grounds, eyes);
        ValidateGeneratedState(terrain, veins, branches, sections, grounds, eyes, field);

        return new SpiritVeinGenerationResult(
            terrain.WorldSeedId,
            terrain.Width,
            terrain.Height,
            veins,
            branches,
            sections,
            grounds,
            eyes,
            field);
    }

    internal static ElementComposition CalculateComposition(
        SpiritVeinTerrainSnapshot terrain,
        IReadOnlyList<int> tileIds)
    {
        var values = new float[ElementIndex.Count];
        int count = 0;
        for (int i = 0; i < tileIds.Count; i++)
        {
            int tileId = tileIds[i];
            if ((uint)tileId >= (uint)terrain.CellCount) continue;
            ElementComposition composition = terrain[tileId].Composition;
            for (int element = 0; element < ElementIndex.Count; element++) values[element] += composition[element];
            count++;
        }

        if (count == 0) return new ElementComposition(earth: 1f, normalize: true);
        for (int element = 0; element < ElementIndex.Count; element++) values[element] /= count;
        return new ElementComposition(values, true);
    }

    private static List<int> CollectSourceCandidates(SpiritVeinTerrainSnapshot terrain)
    {
        var candidates = new List<ScoredTile>();
        for (int tileId = 0; tileId < terrain.CellCount; tileId++)
        {
            SpiritVeinTerrainCell cell = terrain[tileId];
            if (!cell.IsUsableLand || !cell.IsHighland) continue;
            float score = ResolveSourceScore(terrain, tileId);
            candidates.Add(new ScoredTile(tileId, score));
        }
        candidates.Sort(CompareScoredTiles);
        var result = new List<int>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++) result.Add(candidates[i].TileId);
        return result;
    }

    private static List<int> CollectOutletCandidates(SpiritVeinTerrainSnapshot terrain)
    {
        var candidates = new List<int>();
        for (int tileId = 0; tileId < terrain.CellCount; tileId++)
        {
            SpiritVeinTerrainCell cell = terrain[tileId];
            if (cell.IsWater && HasUsableLandNeighbour(terrain, tileId))
            {
                candidates.Add(tileId);
                continue;
            }

            if (cell.IsUsableLand && !cell.IsHighland && cell.IsBeach) candidates.Add(tileId);
        }
        return candidates;
    }

    private static int ResolveTargetCount(
        SpiritVeinTerrainSnapshot terrain,
        int sourceCount,
        int outletCount)
    {
        if (sourceCount == 0 || outletCount == 0) return 0;
        int landCount = 0;
        for (int i = 0; i < terrain.CellCount; i++)
        {
            if (terrain[i].IsUsableLand) landCount++;
        }

        int span = SpiritVeinSettings.DragonVeinMaximum - SpiritVeinSettings.DragonVeinMinimum + 1;
        int ordinary = SpiritVeinSettings.DragonVeinMinimum +
                       StableValue(terrain.WorldSeedId, landCount) % Mathf.Max(1, span);
        float scale = Mathf.Sqrt(Mathf.Max(0.15f, landCount / 8000f));
        int target = Mathf.RoundToInt(ordinary * scale);
        return Mathf.Clamp(target, 1, SpiritVeinSettings.DragonVeinAbsoluteMaximum);
    }

    private static List<int> SelectSources(
        SpiritVeinTerrainSnapshot terrain,
        List<int> candidates,
        int desiredCount)
    {
        var selected = new List<int>();
        int minimumDistance = Mathf.Max(12, Mathf.RoundToInt(Mathf.Sqrt(terrain.CellCount) * 0.065f));
        for (int i = 0; i < candidates.Count && selected.Count < desiredCount; i++)
        {
            int tileId = candidates[i];
            bool tooClose = false;
            for (int j = 0; j < selected.Count; j++)
            {
                if (TileDistance(terrain.Width, tileId, selected[j]) >= minimumDistance) continue;
                tooClose = true;
                break;
            }
            if (!tooClose) selected.Add(tileId);
        }
        return selected;
    }

    private static List<int> FindMainRoute(
        SpiritVeinTerrainSnapshot terrain,
        int source,
        List<int> outletCandidates,
        int sourceIndex,
        HashSet<int> occupied,
        CancellationToken cancellationToken)
    {
        var rejected = new HashSet<int>();
        for (int attempt = 0; attempt < SpiritVeinSettings.TargetSearchAttempts; attempt++)
        {
            int outlet = SelectOutlet(terrain, source, outletCandidates, sourceIndex, rejected);
            if (outlet < 0) break;
            List<int> route = FindRoute(terrain, source, outlet, occupied, true, cancellationToken);
            if (route.Count >= SpiritVeinSettings.MainMinimumLength) return route;
            rejected.Add(outlet);
        }
        return new List<int>();
    }

    private static int SelectOutlet(
        SpiritVeinTerrainSnapshot terrain,
        int source,
        List<int> candidates,
        int sourceIndex,
        HashSet<int> rejected)
    {
        int desiredDistance = Mathf.Max(20, Mathf.RoundToInt(Mathf.Sqrt(terrain.CellCount) * 0.28f));
        int minimumDistance = Mathf.Max(12, SpiritVeinSettings.MainMinimumLength);
        int best = -1;
        float bestScore = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidate = candidates[i];
            if (rejected.Contains(candidate)) continue;
            int distance = TileDistance(terrain.Width, source, candidate);
            if (distance < minimumDistance) continue;
            SpiritVeinTerrainCell cell = terrain[candidate];
            float score = Mathf.Abs(distance - desiredDistance) * 1.6f +
                          (cell.IsWater ? 0f : 24f) +
                          Stable01(terrain.WorldSeedId + sourceIndex * 41, candidate) * 3f;
            if (score >= bestScore) continue;
            bestScore = score;
            best = candidate;
        }
        return best;
    }

    private static int FindBranchSource(
        SpiritVeinTerrainSnapshot terrain,
        int joinTile,
        HashSet<int> occupied,
        int veinId,
        int branchIndex)
    {
        int centerX = joinTile % terrain.Width;
        int centerY = joinTile / terrain.Width;
        int radius = Mathf.Clamp(Mathf.Min(terrain.Width, terrain.Height) / 5, 14, 52);
        int best = -1;
        float bestScore = float.MinValue;
        for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(terrain.Height - 1, centerY + radius); y++)
        {
            for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(terrain.Width - 1, centerX + radius); x++)
            {
                int tileId = y * terrain.Width + x;
                SpiritVeinTerrainCell cell = terrain[tileId];
                if (!cell.IsUsableLand || !cell.IsHighland || occupied.Contains(tileId)) continue;
                int distance = TileDistance(terrain.Width, joinTile, tileId);
                if (distance < SpiritVeinSettings.BranchMinimumLength || distance > radius * 2) continue;
                float score = ResolveSourceScore(terrain, tileId) - distance * 0.7f +
                              Stable01(terrain.WorldSeedId + veinId * 101 + branchIndex * 17, tileId) * 5f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = tileId;
            }
        }
        return best;
    }

    private static int[] CollectSourceDomain(SpiritVeinTerrainSnapshot terrain, int source)
    {
        int radius = SpiritVeinSettings.SourceDomainRadius;
        int centerX = source % terrain.Width;
        int centerY = source / terrain.Width;
        var tiles = new List<int>();
        for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(terrain.Height - 1, centerY + radius); y++)
        {
            for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(terrain.Width - 1, centerX + radius); x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                if (dx * dx + dy * dy > radius * radius) continue;
                int tileId = y * terrain.Width + x;
                if (!terrain[tileId].IsUsableLand || !terrain[tileId].IsHighland) continue;
                tiles.Add(tileId);
            }
        }
        if (tiles.Count == 0) tiles.Add(source);
        return tiles.ToArray();
    }

    private static List<SectionPlan> BuildSectionPlans(
        SpiritVeinTerrainSnapshot terrain,
        List<GuideRoute> routes)
    {
        var plans = new List<SectionPlan>();
        int nextSectionId = 1;
        for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            GuideRoute route = routes[routeIndex];
            int chunkCount = Mathf.Max(1, Mathf.CeilToInt(route.Tiles.Count / (float)SpiritVeinSettings.SectionTargetLength));
            route.SectionIdByPoint = new int[route.Tiles.Count];
            for (int chunk = 0; chunk < chunkCount; chunk++)
            {
                int start = Mathf.FloorToInt(route.Tiles.Count * chunk / (float)chunkCount);
                int end = Mathf.Max(start, Mathf.FloorToInt(route.Tiles.Count * (chunk + 1) / (float)chunkCount) - 1);
                VeinSectionKind kind = ResolveSectionKind(terrain, route, chunk, chunkCount, start, end);
                var plan = new SectionPlan(nextSectionId++, route.VeinId, route.BranchId, kind, route, start, end);
                plans.Add(plan);
                route.SectionPlans.Add(plan);
                for (int i = start; i <= end; i++) route.SectionIdByPoint[i] = plan.Id;
            }
        }
        return plans;
    }

    private static VeinSectionKind ResolveSectionKind(
        SpiritVeinTerrainSnapshot terrain,
        GuideRoute route,
        int chunk,
        int chunkCount,
        int start,
        int end)
    {
        if (chunk == 0) return route.Main ? VeinSectionKind.SourceDomain : VeinSectionKind.UpperCourse;
        if (chunk == chunkCount - 1) return route.Main ? VeinSectionKind.Outlet : VeinSectionKind.Confluence;
        if (route.Main && chunk == chunkCount - 2) return VeinSectionKind.Approaching;

        int highland = 0;
        int land = 0;
        for (int i = start; i <= end; i++)
        {
            SpiritVeinTerrainCell cell = terrain[route.Tiles[i]];
            if (cell.IsHighland) highland++;
            if (cell.IsUsableLand) land++;
        }
        return land > 0 && highland / (float)land >= 0.55f
            ? VeinSectionKind.Passage
            : VeinSectionKind.UpperCourse;
    }

    private static void ConnectSectionPlans(List<SectionPlan> plans, List<GuideRoute> routes)
    {
        var mainRoutesByVein = new Dictionary<int, GuideRoute>();
        for (int i = 0; i < routes.Count; i++)
        {
            GuideRoute route = routes[i];
            if (route.Main) mainRoutesByVein[route.VeinId] = route;
            for (int sectionIndex = 0; sectionIndex < route.SectionPlans.Count - 1; sectionIndex++)
            {
                Connect(route.SectionPlans[sectionIndex], route.SectionPlans[sectionIndex + 1]);
            }
        }

        for (int i = 0; i < routes.Count; i++)
        {
            GuideRoute branch = routes[i];
            if (branch.Main || branch.SectionPlans.Count == 0 || !mainRoutesByVein.TryGetValue(branch.VeinId, out GuideRoute main))
                continue;
            int joinTile = branch.Tiles[branch.Tiles.Count - 1];
            int joinIndex = main.Tiles.IndexOf(joinTile);
            if (joinIndex < 0) continue;
            int sectionId = main.SectionIdByPoint[joinIndex];
            SectionPlan target = FindPlan(plans, sectionId);
            if (target == null) continue;
            target.Kind = VeinSectionKind.Confluence;
            Connect(branch.SectionPlans[branch.SectionPlans.Count - 1], target);
        }
    }

    private static void Connect(SectionPlan upstream, SectionPlan downstream)
    {
        if (!upstream.DownstreamIds.Contains(downstream.Id)) upstream.DownstreamIds.Add(downstream.Id);
        if (!downstream.UpstreamIds.Contains(upstream.Id)) downstream.UpstreamIds.Add(upstream.Id);
    }

    private static SpiritVeinFieldSnapshot BuildField(
        SpiritVeinTerrainSnapshot terrain,
        List<SpiritVeinDraft> veins,
        List<GuideRoute> routes,
        List<SectionPlan> plans,
        CancellationToken cancellationToken)
    {
        int count = terrain.CellCount;
        int[] primary = CreateFilledArray(count, -1);
        int[] secondary = CreateFilledArray(count, -1);
        int[] section = CreateFilledArray(count, -1);
        int[] secondarySection = CreateFilledArray(count, -1);
        int[] ground = CreateFilledArray(count, -1);
        float[] strength = new float[count];
        float[] secondaryStrength = new float[count];
        float[] flowX = new float[count];
        float[] flowY = new float[count];
        float[] convergence = new float[count];
        float[] shelter = new float[count];
        float[] leakage = new float[count];
        float[] ownerSectionStrength = new float[count];
        float[] progress = new float[count];

        for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GuideRoute route = routes[routeIndex];
            float routeWeight = route.Main ? 1f : 0.72f;
            for (int point = 0; point < route.Tiles.Count; point++)
            {
                int tileId = route.Tiles[point];
                int previous = route.Tiles[Mathf.Max(0, point - 1)];
                int next = route.Tiles[Mathf.Min(route.Tiles.Count - 1, point + 1)];
                float directionX = next % terrain.Width - previous % terrain.Width;
                float directionY = next / terrain.Width - previous / terrain.Width;
                Normalize(ref directionX, ref directionY);
                int radius = ResolveFieldRadius(terrain, tileId, route.Main, point, route.Tiles.Count);
                float routeProgress = route.Main ? point / (float)Mathf.Max(1, route.Tiles.Count - 1) : 0.35f;
                RasterizeFieldPoint(
                    terrain,
                    route.VeinId,
                    route.SectionIdByPoint[point],
                    tileId,
                    radius,
                    routeWeight,
                    directionX,
                    directionY,
                    routeProgress,
                    primary,
                    secondary,
                    section,
                    secondarySection,
                    strength,
                    secondaryStrength,
                    flowX,
                    flowY,
                    ownerSectionStrength,
                    progress);
            }
        }

        for (int veinIndex = 0; veinIndex < veins.Count; veinIndex++)
        {
            SpiritVeinDraft vein = veins[veinIndex];
            GuideRoute main = null;
            for (int i = 0; i < routes.Count; i++)
            {
                if (routes[i].Main && routes[i].VeinId == vein.Id)
                {
                    main = routes[i];
                    break;
                }
            }
            if (main == null || main.SectionPlans.Count == 0) continue;
            int firstSection = main.SectionPlans[0].Id;
            int nextTile = main.Tiles[Mathf.Min(main.Tiles.Count - 1, 4)];
            float targetX = nextTile % terrain.Width;
            float targetY = nextTile / terrain.Width;
            for (int sourceIndex = 0; sourceIndex < vein.SourceTileIds.Length; sourceIndex++)
            {
                int sourceTile = vein.SourceTileIds[sourceIndex];
                float sx = sourceTile % terrain.Width;
                float sy = sourceTile / terrain.Width;
                float directionX = targetX - sx;
                float directionY = targetY - sy;
                Normalize(ref directionX, ref directionY);
                RasterizeFieldPoint(
                    terrain,
                    vein.Id,
                    firstSection,
                    sourceTile,
                    Mathf.Max(2, SpiritVeinSettings.SourceDomainRadius / 2),
                    0.82f,
                    directionX,
                    directionY,
                    0f,
                    primary,
                    secondary,
                    section,
                    secondarySection,
                    strength,
                    secondaryStrength,
                    flowX,
                    flowY,
                    ownerSectionStrength,
                    progress);
            }
        }

        for (int tileId = 0; tileId < count; tileId++)
        {
            if (strength[tileId] < SpiritVeinSettings.FieldMinimumStrength)
            {
                primary[tileId] = -1;
                section[tileId] = -1;
                strength[tileId] = 0f;
                flowX[tileId] = 0f;
                flowY[tileId] = 0f;
            }
            if (secondaryStrength[tileId] < SpiritVeinSettings.FieldMinimumStrength)
            {
                secondary[tileId] = -1;
                secondarySection[tileId] = -1;
                secondaryStrength[tileId] = 0f;
            }
        }

        FieldBuildProgressByTile = progress;
        return new SpiritVeinFieldSnapshot(
            terrain.Width,
            terrain.Height,
            primary,
            secondary,
            section,
            secondarySection,
            ground,
            strength,
            secondaryStrength,
            flowX,
            flowY,
            convergence,
            shelter,
            leakage);
    }

    [ThreadStatic]
    private static float[] FieldBuildProgressByTile;

    private static int ResolveFieldRadius(
        SpiritVeinTerrainSnapshot terrain,
        int tileId,
        bool main,
        int point,
        int length)
    {
        SpiritVeinTerrainCell cell = terrain[tileId];
        float progress = point / (float)Mathf.Max(1, length - 1);
        int radius = SpiritVeinSettings.FieldBaseRadius + (main ? 1 : -1);
        if (cell.IsMountain || cell.IsHighland) radius++;
        if (progress > 0.58f && progress < 0.86f) radius += 2;
        if (cell.IsBeach || cell.IsWater) radius--;
        return Mathf.Clamp(radius, 2, SpiritVeinSettings.FieldMaximumRadius);
    }

    private static void RasterizeFieldPoint(
        SpiritVeinTerrainSnapshot terrain,
        int veinId,
        int sectionId,
        int centerTile,
        int radius,
        float routeWeight,
        float directionX,
        float directionY,
        float progress,
        int[] primary,
        int[] secondary,
        int[] section,
        int[] secondarySection,
        float[] strength,
        float[] secondaryStrength,
        float[] flowX,
        float[] flowY,
        float[] ownerSectionStrength,
        float[] progressByTile)
    {
        int centerX = centerTile % terrain.Width;
        int centerY = centerTile / terrain.Width;
        float radiusSquared = radius * radius;
        for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(terrain.Height - 1, centerY + radius); y++)
        {
            for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(terrain.Width - 1, centerX + radius); x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > radiusSquared) continue;
                int tileId = y * terrain.Width + x;
                SpiritVeinTerrainCell cell = terrain[tileId];
                float distance = Mathf.Sqrt(distanceSquared);
                float falloff = 1f - distance / (radius + 0.5f);
                falloff *= falloff;
                float irregularity = Mathf.Lerp(
                    0.72f,
                    1.16f,
                    Stable01(terrain.WorldSeedId + veinId * 733 + centerTile, tileId));
                float terrainFactor = cell.IsLava || cell.IsGoo
                    ? 0.22f
                    : cell.IsWater
                        ? 0.55f
                        : cell.IsHighland
                            ? 1.08f
                            : 1f;
                float contribution = Mathf.Clamp01(falloff * routeWeight * irregularity * terrainFactor);
                if (contribution < SpiritVeinSettings.FieldMinimumStrength * 0.45f) continue;
                ApplyFieldContribution(
                    tileId,
                    veinId,
                    sectionId,
                    contribution,
                    directionX,
                    directionY,
                    progress,
                    primary,
                    secondary,
                    section,
                    secondarySection,
                    strength,
                    secondaryStrength,
                    flowX,
                    flowY,
                    ownerSectionStrength,
                    progressByTile);
            }
        }
    }

    private static void ApplyFieldContribution(
        int tileId,
        int veinId,
        int sectionId,
        float contribution,
        float directionX,
        float directionY,
        float progress,
        int[] primary,
        int[] secondary,
        int[] section,
        int[] secondarySection,
        float[] strength,
        float[] secondaryStrength,
        float[] flowX,
        float[] flowY,
        float[] ownerSectionStrength,
        float[] progressByTile)
    {
        if (primary[tileId] == veinId)
        {
            float old = strength[tileId];
            float combined = old + contribution * (1f - old);
            float total = old + contribution;
            flowX[tileId] = (flowX[tileId] * old + directionX * contribution) / Mathf.Max(0.001f, total);
            flowY[tileId] = (flowY[tileId] * old + directionY * contribution) / Mathf.Max(0.001f, total);
            progressByTile[tileId] = (progressByTile[tileId] * old + progress * contribution) / Mathf.Max(0.001f, total);
            strength[tileId] = Mathf.Clamp01(combined);
            if (contribution > ownerSectionStrength[tileId])
            {
                ownerSectionStrength[tileId] = contribution;
                section[tileId] = sectionId;
            }
            return;
        }

        if (contribution > strength[tileId])
        {
            if (primary[tileId] >= 0)
            {
                secondary[tileId] = primary[tileId];
                secondarySection[tileId] = section[tileId];
                secondaryStrength[tileId] = strength[tileId];
            }
            primary[tileId] = veinId;
            strength[tileId] = contribution;
            section[tileId] = sectionId;
            ownerSectionStrength[tileId] = contribution;
            flowX[tileId] = directionX;
            flowY[tileId] = directionY;
            progressByTile[tileId] = progress;
            return;
        }

        if (secondary[tileId] == veinId)
        {
            if (contribution > secondaryStrength[tileId]) secondarySection[tileId] = sectionId;
            secondaryStrength[tileId] = Mathf.Clamp01(
                secondaryStrength[tileId] + contribution * (1f - secondaryStrength[tileId]));
        }
        else if (contribution > secondaryStrength[tileId])
        {
            secondary[tileId] = veinId;
            secondarySection[tileId] = sectionId;
            secondaryStrength[tileId] = contribution;
        }
    }

    private static void ComputeFengShui(
        SpiritVeinTerrainSnapshot terrain,
        SpiritVeinFieldSnapshot field,
        CancellationToken cancellationToken)
    {
        for (int tileId = 0; tileId < terrain.CellCount; tileId++)
        {
            if ((tileId & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (field.PrimaryVeinByTile[tileId] < 0) continue;
            int x = tileId % terrain.Width;
            int y = tileId / terrain.Width;
            int highland = 0;
            int mountain = 0;
            int water = 0;
            int usable = 0;
            int sampleCount = 0;
            float averageHeight = 0f;
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= terrain.Width || ny < 0 || ny >= terrain.Height) continue;
                    SpiritVeinTerrainCell neighbour = terrain[ny * terrain.Width + nx];
                    if (neighbour.IsHighland) highland++;
                    if (neighbour.IsMountain) mountain++;
                    if (neighbour.IsWater) water++;
                    if (neighbour.IsUsableLand) usable++;
                    averageHeight += neighbour.Height;
                    sampleCount++;
                }
            }

            float localHeight = terrain[tileId].Height;
            averageHeight /= Mathf.Max(1, sampleCount);
            float shelter = Mathf.Clamp01((highland + mountain * 0.5f) / Mathf.Max(1f, sampleCount * 0.52f));
            float waterEmbrace = Mathf.Clamp01(water / Mathf.Max(1f, sampleCount * 0.28f));
            float flatness = Mathf.Clamp01(1f - Mathf.Abs(localHeight - averageHeight) * 0.45f);
            float flowMagnitude = Mathf.Sqrt(
                field.FlowX[tileId] * field.FlowX[tileId] + field.FlowY[tileId] * field.FlowY[tileId]);
            float opposingFlows = Mathf.Clamp01(1f - flowMagnitude);
            float overlap = Mathf.Clamp01(field.SecondaryStrength[tileId] * 1.5f);
            float convergence = Mathf.Clamp01(
                opposingFlows * 0.42f +
                field.FieldStrength[tileId] * 0.24f +
                shelter * 0.18f +
                waterEmbrace * 0.12f +
                overlap * 0.18f +
                flatness * 0.08f);
            float openLand = usable / Mathf.Max(1f, sampleCount);
            float leakage = Mathf.Clamp01(
                (1f - shelter) * 0.48f +
                openLand * 0.2f +
                (terrain[tileId].IsBeach ? 0.22f : 0f) -
                convergence * 0.35f -
                waterEmbrace * 0.12f);

            field.Shelter[tileId] = shelter;
            field.Convergence[tileId] = convergence;
            field.Leakage[tileId] = leakage;
            Normalize(field.FlowX, field.FlowY, tileId);
        }
    }

    private static List<GatheringGround> BuildGrounds(
        SpiritVeinTerrainSnapshot terrain,
        List<SpiritVeinDraft> veins,
        List<SectionPlan> plans,
        SpiritVeinFieldSnapshot field,
        CancellationToken cancellationToken)
    {
        var grounds = new List<GatheringGround>();
        float[] groundInfluence = new float[terrain.CellCount];
        for (int veinIndex = 0; veinIndex < veins.Count; veinIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SpiritVeinDraft vein = veins[veinIndex];
            List<GroundCandidate> candidates = CollectGroundCandidates(terrain, field, vein.Id);
            if (candidates.Count == 0) continue;

            GroundCandidate main = candidates[0];
            GatheringGround mainGround = CreateGround(
                terrain,
                field,
                plans,
                groundInfluence,
                grounds.Count + 1,
                vein.Id,
                -1,
                GatheringGroundKind.Main,
                main,
                SpiritVeinSettings.MainGroundRadius);
            if (mainGround != null)
            {
                grounds.Add(mainGround);
                vein.MainGroundId = mainGround.Id;
                vein.GroundIds.Add(mainGround.Id);
            }

            int secondaryCount = 0;
            for (int candidateIndex = 1;
                 candidateIndex < candidates.Count && secondaryCount < SpiritVeinSettings.SecondaryGroundMaximum;
                 candidateIndex++)
            {
                GroundCandidate candidate = candidates[candidateIndex];
                bool nearExisting = false;
                for (int groundIndex = 0; groundIndex < grounds.Count; groundIndex++)
                {
                    GatheringGround existing = grounds[groundIndex];
                    if (existing.PrimaryVeinId != vein.Id) continue;
                    if (TileDistance(terrain.Width, existing.CenterTileId, candidate.TileId) >=
                        SpiritVeinSettings.GroundMinimumDistance) continue;
                    nearExisting = true;
                    break;
                }
                if (nearExisting || candidate.Score < 1.7f) continue;

                GatheringGround ground = CreateGround(
                    terrain,
                    field,
                    plans,
                    groundInfluence,
                    grounds.Count + 1,
                    vein.Id,
                    -1,
                    GatheringGroundKind.Secondary,
                    candidate,
                    SpiritVeinSettings.SecondaryGroundRadius);
                if (ground == null) continue;
                grounds.Add(ground);
                vein.GroundIds.Add(ground.Id);
                secondaryCount++;
            }
        }

        AddCrossingGrounds(terrain, veins, plans, field, groundInfluence, grounds);
        return grounds;
    }

    private static List<GroundCandidate> CollectGroundCandidates(
        SpiritVeinTerrainSnapshot terrain,
        SpiritVeinFieldSnapshot field,
        int veinId)
    {
        var candidates = new List<GroundCandidate>();
        float[] progress = FieldBuildProgressByTile;
        for (int tileId = 0; tileId < terrain.CellCount; tileId++)
        {
            if (field.PrimaryVeinByTile[tileId] != veinId || field.FieldStrength[tileId] < 0.24f) continue;
            SpiritVeinTerrainCell cell = terrain[tileId];
            if (!cell.IsUsableLand || cell.IsMountain) continue;
            float routeProgress = progress != null && tileId < progress.Length ? progress[tileId] : 0.5f;
            if (routeProgress < 0.2f || routeProgress > 0.94f) continue;
            float flatness = cell.IsHighland ? 0.58f : 1f;
            float downstreamPreference = 1f - Mathf.Abs(routeProgress - 0.7f);
            float score = field.FieldStrength[tileId] * 0.85f +
                          field.Convergence[tileId] * 1.35f +
                          field.Shelter[tileId] * 0.78f +
                          (1f - field.Leakage[tileId]) * 0.62f +
                          flatness * 0.34f +
                          downstreamPreference * 0.42f;
            score += Stable01(terrain.WorldSeedId + veinId * 997, tileId) * 0.08f;
            if (!IsLocalGroundMaximum(terrain, field, veinId, tileId, score)) continue;
            candidates.Add(new GroundCandidate(tileId, score));
        }
        if (candidates.Count == 0)
        {
            GroundCandidate fallback = FindFallbackGroundCandidate(terrain, field, veinId, progress);
            if (fallback.TileId >= 0) candidates.Add(fallback);
        }
        candidates.Sort(CompareGroundCandidates);
        return candidates;
    }

    private static GroundCandidate FindFallbackGroundCandidate(
        SpiritVeinTerrainSnapshot terrain,
        SpiritVeinFieldSnapshot field,
        int veinId,
        float[] progress)
    {
        int bestTile = -1;
        float bestScore = float.MinValue;
        for (int tileId = 0; tileId < terrain.CellCount; tileId++)
        {
            if (field.PrimaryVeinByTile[tileId] != veinId || !terrain[tileId].IsUsableLand ||
                terrain[tileId].IsMountain || field.FieldStrength[tileId] < 0.18f) continue;
            float routeProgress = progress != null && tileId < progress.Length ? progress[tileId] : 0.5f;
            if (routeProgress < 0.28f || routeProgress > 0.94f) continue;
            float score = field.FieldStrength[tileId] +
                          field.Convergence[tileId] * 1.2f +
                          field.Shelter[tileId] * 0.65f -
                          field.Leakage[tileId] * 0.5f +
                          (1f - Mathf.Abs(routeProgress - 0.72f)) * 0.4f;
            if (score <= bestScore) continue;
            bestScore = score;
            bestTile = tileId;
        }
        return new GroundCandidate(bestTile, Mathf.Max(1.2f, bestScore));
    }

    private static bool IsLocalGroundMaximum(
        SpiritVeinTerrainSnapshot terrain,
        SpiritVeinFieldSnapshot field,
        int veinId,
        int tileId,
        float score)
    {
        int x = tileId % terrain.Width;
        int y = tileId / terrain.Width;
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= terrain.Width || ny < 0 || ny >= terrain.Height) continue;
                int neighbour = ny * terrain.Width + nx;
                if (field.PrimaryVeinByTile[neighbour] != veinId) continue;
                float neighbourScore = field.FieldStrength[neighbour] * 0.85f +
                                       field.Convergence[neighbour] * 1.35f +
                                       field.Shelter[neighbour] * 0.78f +
                                       (1f - field.Leakage[neighbour]) * 0.62f +
                                       (terrain[neighbour].IsHighland ? 0.58f : 1f) * 0.34f;
                if (neighbourScore > score + 0.03f) return false;
            }
        }
        return true;
    }

    private static GatheringGround CreateGround(
        SpiritVeinTerrainSnapshot terrain,
        SpiritVeinFieldSnapshot field,
        List<SectionPlan> plans,
        float[] groundInfluence,
        int id,
        int primaryVeinId,
        int guestVeinId,
        GatheringGroundKind kind,
        GroundCandidate candidate,
        int baseRadius)
    {
        int sectionId = field.SectionByTile[candidate.TileId];
        SectionPlan plan = FindPlan(plans, sectionId);
        if (sectionId < 0 || plan == null) return null;
        plan.Kind = VeinSectionKind.GatheringGround;

        GatheringGroundQuality quality = SpiritVeinSettings.ResolveGroundQuality(candidate.Score);
        int radius = baseRadius + Mathf.Max(0, (int)quality - 2);
        int centerX = candidate.TileId % terrain.Width;
        int centerY = candidate.TileId / terrain.Width;
        var area = new List<int>();
        var hall = new List<int>();
        float radiusSquared = radius * radius;
        for (int y = Mathf.Max(0, centerY - radius); y <= Mathf.Min(terrain.Height - 1, centerY + radius); y++)
        {
            for (int x = Mathf.Max(0, centerX - radius); x <= Mathf.Min(terrain.Width - 1, centerX + radius); x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > radiusSquared) continue;
                int tileId = y * terrain.Width + x;
                float localStrength = field.PrimaryVeinByTile[tileId] == primaryVeinId
                    ? field.FieldStrength[tileId]
                    : field.SecondaryVeinByTile[tileId] == primaryVeinId
                        ? field.SecondaryStrength[tileId]
                        : 0f;
                if (localStrength < SpiritVeinSettings.FieldMinimumStrength) continue;
                float influence = (1f - Mathf.Sqrt(distanceSquared) / (radius + 0.5f)) *
                                  Mathf.Lerp(0.55f, 1f, field.Convergence[tileId]);
                if (influence <= groundInfluence[tileId]) continue;
                groundInfluence[tileId] = influence;
                field.GroundByTile[tileId] = id;
                area.Add(tileId);
                SpiritVeinTerrainCell cell = terrain[tileId];
                if (cell.IsUsableLand && !cell.IsMountain && field.Leakage[tileId] < 0.78f) hall.Add(tileId);
            }
        }
        if (area.Count == 0) return null;

        return new GatheringGround(
            id,
            primaryVeinId,
            guestVeinId,
            sectionId,
            guestVeinId >= 0 ? field.SecondarySectionByTile[candidate.TileId] : -1,
            kind,
            quality,
            candidate.TileId,
            area.ToArray(),
            hall.ToArray(),
            field.Convergence[candidate.TileId],
            field.Shelter[candidate.TileId],
            field.Leakage[candidate.TileId]);
    }

    private static void AddCrossingGrounds(
        SpiritVeinTerrainSnapshot terrain,
        List<SpiritVeinDraft> veins,
        List<SectionPlan> plans,
        SpiritVeinFieldSnapshot field,
        float[] groundInfluence,
        List<GatheringGround> grounds)
    {
        var bestByPair = new Dictionary<long, GroundCandidate>();
        for (int tileId = 0; tileId < terrain.CellCount; tileId++)
        {
            int primary = field.PrimaryVeinByTile[tileId];
            int secondary = field.SecondaryVeinByTile[tileId];
            if (primary < 0 || secondary < 0 || field.FieldStrength[tileId] < 0.38f ||
                field.SecondaryStrength[tileId] < 0.3f || !terrain[tileId].IsUsableLand || terrain[tileId].IsMountain)
            {
                continue;
            }
            int low = Mathf.Min(primary, secondary);
            int high = Mathf.Max(primary, secondary);
            long key = ((long)low << 32) | (uint)high;
            float score = field.FieldStrength[tileId] + field.SecondaryStrength[tileId] +
                          field.Convergence[tileId] * 1.5f + field.Shelter[tileId] -
                          field.Leakage[tileId] * 0.7f;
            if (!bestByPair.TryGetValue(key, out GroundCandidate current) || score > current.Score)
                bestByPair[key] = new GroundCandidate(tileId, score);
        }

        foreach (KeyValuePair<long, GroundCandidate> pair in bestByPair)
        {
            GroundCandidate candidate = pair.Value;
            if (candidate.Score < 2.85f) continue;
            bool nearGround = false;
            for (int i = 0; i < grounds.Count; i++)
            {
                if (TileDistance(terrain.Width, grounds[i].CenterTileId, candidate.TileId) >=
                    SpiritVeinSettings.GroundMinimumDistance) continue;
                nearGround = true;
                break;
            }
            if (nearGround) continue;

            int primary = field.PrimaryVeinByTile[candidate.TileId];
            int guest = field.SecondaryVeinByTile[candidate.TileId];
            GatheringGround ground = CreateGround(
                terrain,
                field,
                plans,
                groundInfluence,
                grounds.Count + 1,
                primary,
                guest,
                GatheringGroundKind.Crossing,
                candidate,
                SpiritVeinSettings.CrossingGroundRadius);
            if (ground == null) continue;
            grounds.Add(ground);
            FindVein(veins, primary)?.GroundIds.Add(ground.Id);
            FindVein(veins, guest)?.GroundIds.Add(ground.Id);
        }
    }

    private static List<SpiritVeinSection> BuildSections(
        SpiritVeinTerrainSnapshot terrain,
        List<SpiritVeinDraft> veins,
        List<SpiritVeinBranch> branches,
        List<SectionPlan> plans,
        SpiritVeinFieldSnapshot field)
    {
        var tilesBySection = new Dictionary<int, List<int>>();
        for (int tileId = 0; tileId < terrain.CellCount; tileId++)
        {
            int sectionId = field.SectionByTile[tileId];
            if (sectionId < 0) continue;
            if (!tilesBySection.TryGetValue(sectionId, out List<int> tiles))
            {
                tiles = new List<int>();
                tilesBySection[sectionId] = tiles;
            }
            tiles.Add(tileId);
        }

        var sections = new List<SpiritVeinSection>(plans.Count);
        var sectionById = new Dictionary<int, SpiritVeinSection>();
        for (int i = 0; i < plans.Count; i++)
        {
            SectionPlan plan = plans[i];
            if (!tilesBySection.TryGetValue(plan.Id, out List<int> tiles) || tiles.Count == 0)
                tiles = new List<int>(plan.GuideTiles);
            float strengthTotal = 0f;
            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
                strengthTotal += field.FieldStrength[tiles[tileIndex]];
            bool branch = plan.BranchId >= 0;
            float kindMultiplier = plan.Kind switch
            {
                VeinSectionKind.SourceDomain => 1.45f,
                VeinSectionKind.GatheringGround => 1.35f,
                VeinSectionKind.Passage => 0.78f,
                VeinSectionKind.Outlet => 0.72f,
                VeinSectionKind.Remnant => 0.55f,
                _ => 1f
            };
            float capacity = (tiles.Count * 70f + strengthTotal * 220f) * kindMultiplier * (branch ? 0.78f : 1f);
            float patency = plan.Kind == VeinSectionKind.Passage ? 0.82f : 1f;
            int centerTile = plan.GuideTiles[plan.GuideTiles.Count / 2];
            var section = new SpiritVeinSection(
                plan.Id,
                plan.VeinId,
                plan.BranchId,
                plan.Kind,
                centerTile,
                tiles.ToArray(),
                capacity,
                SpiritVeinSettings.ResolveMonthlyRecovery(branch, capacity),
                SpiritVeinSettings.ResolveMonthlySupply(branch, capacity),
                SpiritVeinSettings.ResolveMonthlyTransfer(capacity),
                patency,
                CalculateComposition(terrain, tiles));
            SpiritVeinTerrainCell centerCell = terrain[centerTile];
            section.RegionName = !string.IsNullOrWhiteSpace(centerCell.LandformRegionName)
                ? centerCell.LandformRegionName
                : centerCell.PrimaryRegionName;
            sections.Add(section);
            sectionById[section.Id] = section;
            FindVein(veins, plan.VeinId)?.SectionIds.Add(section.Id);
            if (branch) FindBranch(branches, plan.BranchId)?.SectionIds.Add(section.Id);
        }

        for (int i = 0; i < plans.Count; i++)
        {
            SectionPlan plan = plans[i];
            if (!sectionById.TryGetValue(plan.Id, out SpiritVeinSection section)) continue;
            section.UpstreamSectionIds = plan.UpstreamIds.ToArray();
            section.DownstreamSectionIds = plan.DownstreamIds.ToArray();
        }
        return sections;
    }

    private static void ResolveVeinScalesAndElements(
        List<SpiritVeinDraft> veins,
        List<SpiritVeinBranch> branches,
        List<SpiritVeinSection> sections)
    {
        for (int veinIndex = 0; veinIndex < veins.Count; veinIndex++)
        {
            SpiritVeinDraft vein = veins[veinIndex];
            float capacity = 0f;
            var values = new float[ElementIndex.Count];
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                SpiritVeinSection section = sections[sectionIndex];
                if (section.VeinId != vein.Id) continue;
                capacity += section.Capacity;
                for (int element = 0; element < ElementIndex.Count; element++)
                    values[element] += section.Composition[element] * section.Capacity;
            }
            vein.Scale = SpiritVeinSettings.ResolveDragonScale(capacity);
            vein.Composition = capacity > 0f
                ? NormalizeComposition(values)
                : vein.Composition;
        }

        for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
        {
            SpiritVeinBranch branch = branches[branchIndex];
            float capacity = 0f;
            var values = new float[ElementIndex.Count];
            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                SpiritVeinSection section = sections[sectionIndex];
                if (section.BranchId != branch.Id) continue;
                capacity += section.Capacity;
                for (int element = 0; element < ElementIndex.Count; element++)
                    values[element] += section.Composition[element] * section.Capacity;
            }
            branch.Scale = SpiritVeinSettings.ResolveBranchScale(capacity);
            if (capacity > 0f) branch.Composition = NormalizeComposition(values);
        }
    }

    private static List<SpiritVeinEye> BuildEyes(
        SpiritVeinTerrainSnapshot terrain,
        List<SpiritVeinDraft> veins,
        List<SpiritVeinSection> sections,
        List<GatheringGround> grounds,
        SpiritVeinFieldSnapshot field)
    {
        var eyes = new List<SpiritVeinEye>(grounds.Count);
        for (int groundIndex = 0; groundIndex < grounds.Count; groundIndex++)
        {
            GatheringGround ground = grounds[groundIndex];
            SpiritVeinSection section = FindSection(sections, ground.SectionId);
            SpiritVeinDraft vein = FindVein(veins, ground.PrimaryVeinId);
            if (section == null || vein == null) continue;
            int eyeTile = SelectEyeTile(terrain, field, ground);
            ElementComposition composition = CalculateComposition(terrain, ground.TileIds);
            SpiritEyeManifestation manifestation = ResolveManifestation(terrain, eyeTile, composition);
            float qualityMultiplier = 1f + (int)ground.Quality * 0.22f;
            float baseConcentration = SpiritVeinSettings.ResolveBaseWakan(vein.Scale) * qualityMultiplier;
            var eye = new SpiritVeinEye(
                eyes.Count + 1,
                vein.Id,
                ground.Id,
                section.Id,
                eyeTile,
                manifestation,
                baseConcentration,
                composition);
            eyes.Add(eye);
            ground.EyeId = eye.Id;
            vein.EyeIds.Add(eye.Id);
        }
        return eyes;
    }

    private static int SelectEyeTile(
        SpiritVeinTerrainSnapshot terrain,
        SpiritVeinFieldSnapshot field,
        GatheringGround ground)
    {
        int best = ground.CenterTileId;
        float bestScore = float.MinValue;
        for (int i = 0; i < ground.TileIds.Length; i++)
        {
            int tileId = ground.TileIds[i];
            SpiritVeinTerrainCell cell = terrain[tileId];
            float score = field.FieldStrength[tileId] * 1.1f +
                          field.Convergence[tileId] * 1.4f +
                          field.Shelter[tileId] * 0.35f -
                          field.Leakage[tileId] * 0.4f +
                          (cell.IsWater ? 0.08f : 0f);
            if (score <= bestScore) continue;
            bestScore = score;
            best = tileId;
        }
        return best;
    }

    private static SpiritEyeManifestation ResolveManifestation(
        SpiritVeinTerrainSnapshot terrain,
        int tileId,
        ElementComposition composition)
    {
        SpiritVeinTerrainCell cell = terrain[tileId];
        int dominant = ResolveDominantElement(composition);
        if (cell.IsLava || dominant == ElementIndex.Fire) return SpiritEyeManifestation.FireCave;
        if (cell.IsGoo || dominant == ElementIndex.Entropy) return SpiritEyeManifestation.ChaosBreath;
        if (cell.IsWater || HasWaterNearby(terrain, tileId, 2)) return SpiritEyeManifestation.SpiritSpring;
        if (cell.IsMountain && dominant == ElementIndex.Iron) return SpiritEyeManifestation.StoneMarrow;
        if (cell.IsMountain) return SpiritEyeManifestation.EarthBreath;
        if (dominant == ElementIndex.Wood && IsWooded(cell.BiomeId)) return SpiritEyeManifestation.WoodBloom;
        if (dominant == ElementIndex.Neg) return SpiritEyeManifestation.YinPool;
        if (dominant == ElementIndex.Pos) return SpiritEyeManifestation.YangPool;
        if (cell.IsHighland) return SpiritEyeManifestation.WindEye;
        return SpiritEyeManifestation.EarthBreath;
    }

    private static void ApplyEyeCharacteristics(
        List<SpiritVeinSection> sections,
        List<GatheringGround> grounds,
        List<SpiritVeinEye> eyes)
    {
        for (int i = 0; i < eyes.Count; i++)
        {
            SpiritVeinEye eye = eyes[i];
            SpiritVeinSection section = FindSection(sections, eye.SectionId);
            GatheringGround ground = FindGround(grounds, eye.GroundId);
            if (section == null || ground == null) continue;
            float capacityMultiplier = SpiritVeinSettings.ResolveManifestationCapacityMultiplier(eye.Manifestation);
            section.Capacity *= capacityMultiplier;
            section.CurrentAmount = section.Capacity * 0.82f;
            section.MonthlyRecovery *= SpiritVeinSettings.ResolveManifestationRecoveryMultiplier(eye.Manifestation);
            section.MonthlySupply *= SpiritVeinSettings.ResolveManifestationSupplyMultiplier(eye.Manifestation);
            section.RefreshStatus();
            ground.FillRatio = section.FillRatio;
            ground.Purity = section.Purity;
            eye.FillRatio = section.FillRatio;
            eye.Purity = section.Purity;
        }
    }

    internal static void ValidateGeneratedState(
        SpiritVeinTerrainSnapshot terrain,
        List<SpiritVeinDraft> veins,
        List<SpiritVeinBranch> branches,
        List<SpiritVeinSection> sections,
        List<GatheringGround> grounds,
        List<SpiritVeinEye> eyes,
        SpiritVeinFieldSnapshot field)
    {
        int count = terrain.CellCount;
        if (field == null || field.PrimaryVeinByTile.Length != count ||
            field.SecondaryVeinByTile.Length != count || field.SectionByTile.Length != count ||
            field.SecondarySectionByTile.Length != count || field.GroundByTile.Length != count ||
            field.FieldStrength.Length != count || field.SecondaryStrength.Length != count ||
            field.FlowX.Length != count || field.FlowY.Length != count ||
            field.Convergence.Length != count || field.Shelter.Length != count || field.Leakage.Length != count)
        {
            throw new InvalidOperationException("风水龙脉生成结果的逐格脉域尺寸不一致");
        }

        var veinIds = new HashSet<int>();
        var branchIds = new HashSet<int>();
        var sectionIds = new HashSet<int>();
        var groundIds = new HashSet<int>();
        var eyeIds = new HashSet<int>();
        for (int i = 0; i < veins.Count; i++)
        {
            if (!veinIds.Add(veins[i].Id)) throw new InvalidOperationException("风水龙脉编号重复");
        }
        for (int i = 0; i < branches.Count; i++)
        {
            SpiritVeinBranch branch = branches[i];
            if (!branchIds.Add(branch.Id) || !veinIds.Contains(branch.VeinId))
                throw new InvalidOperationException("支龙编号或所属龙脉无效");
        }
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection section = sections[i];
            if (!sectionIds.Add(section.Id) || !veinIds.Contains(section.VeinId) ||
                section.Capacity <= 0f || section.CurrentAmount < 0f || section.CurrentAmount > section.Capacity)
            {
                throw new InvalidOperationException("龙脉脉节编号、归属或储量无效");
            }
            for (int tileIndex = 0; tileIndex < section.TileIds.Length; tileIndex++)
            {
                if ((uint)section.TileIds[tileIndex] >= (uint)count)
                    throw new InvalidOperationException("龙脉脉节包含越界地块");
            }
        }
        for (int i = 0; i < sections.Count; i++)
        {
            SpiritVeinSection section = sections[i];
            for (int link = 0; link < section.UpstreamSectionIds.Length; link++)
            {
                if (!sectionIds.Contains(section.UpstreamSectionIds[link]))
                    throw new InvalidOperationException("龙脉脉节的上游关系无效");
            }
            for (int link = 0; link < section.DownstreamSectionIds.Length; link++)
            {
                if (!sectionIds.Contains(section.DownstreamSectionIds[link]))
                    throw new InvalidOperationException("龙脉脉节的下游关系无效");
            }
        }
        for (int i = 0; i < grounds.Count; i++)
        {
            GatheringGround ground = grounds[i];
            if (!groundIds.Add(ground.Id) || !veinIds.Contains(ground.PrimaryVeinId) ||
                !sectionIds.Contains(ground.SectionId) || ground.TileIds.Length == 0)
            {
                throw new InvalidOperationException("结穴地编号、归属或范围无效");
            }
            if (ground.GuestVeinId >= 0 &&
                (!veinIds.Contains(ground.GuestVeinId) || !sectionIds.Contains(ground.GuestSectionId)))
            {
                throw new InvalidOperationException("交龙地的客供龙脉无效");
            }
        }
        for (int i = 0; i < eyes.Count; i++)
        {
            SpiritVeinEye eye = eyes[i];
            if (!eyeIds.Add(eye.Id) || !veinIds.Contains(eye.VeinId) ||
                !groundIds.Contains(eye.GroundId) || !sectionIds.Contains(eye.SectionId) ||
                (uint)eye.TileId >= (uint)count)
            {
                throw new InvalidOperationException("灵眼编号、归属或位置无效");
            }
        }
        for (int i = 0; i < grounds.Count; i++)
        {
            if (!eyeIds.Contains(grounds[i].EyeId)) throw new InvalidOperationException("结穴地缺少自然灵眼");
        }
        for (int tileId = 0; tileId < count; tileId++)
        {
            int veinId = field.PrimaryVeinByTile[tileId];
            int sectionId = field.SectionByTile[tileId];
            if (veinId >= 0 && (!veinIds.Contains(veinId) || !sectionIds.Contains(sectionId)))
                throw new InvalidOperationException("脉域地块的主要归属无效");
            int secondaryVeinId = field.SecondaryVeinByTile[tileId];
            int secondarySectionId = field.SecondarySectionByTile[tileId];
            if (secondaryVeinId >= 0 &&
                (!veinIds.Contains(secondaryVeinId) || !sectionIds.Contains(secondarySectionId)))
            {
                throw new InvalidOperationException("脉域地块的次要来气归属无效");
            }
            int groundId = field.GroundByTile[tileId];
            if (groundId >= 0 && !groundIds.Contains(groundId))
                throw new InvalidOperationException("脉域地块的结穴地归属无效");
        }
    }

    private static List<int> FindRoute(
        SpiritVeinTerrainSnapshot terrain,
        int source,
        int target,
        HashSet<int> occupied,
        bool allowWaterEnd,
        CancellationToken cancellationToken)
    {
        int count = terrain.CellCount;
        var costs = new float[count];
        var parents = new int[count];
        var closed = new bool[count];
        for (int i = 0; i < count; i++)
        {
            costs[i] = float.MaxValue;
            parents[i] = -1;
        }

        var heap = new MinHeap();
        costs[source] = 0f;
        heap.Push(source, Heuristic(terrain.Width, source, target));
        int iterations = 0;
        while (heap.Count > 0)
        {
            if ((iterations++ & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
            HeapNode current = heap.Pop();
            if (closed[current.TileId]) continue;
            if (current.TileId == target) return ReconstructPath(parents, source, target);
            closed[current.TileId] = true;
            int x = current.TileId % terrain.Width;
            int y = current.TileId / terrain.Width;
            for (int direction = 0; direction < Directions.Length; direction++)
            {
                int nx = x + Directions[direction].x;
                int ny = y + Directions[direction].y;
                if (nx < 0 || nx >= terrain.Width || ny < 0 || ny >= terrain.Height) continue;
                int next = ny * terrain.Width + nx;
                if (!CanTraverse(terrain, next, target, allowWaterEnd)) continue;
                float occupiedPenalty = occupied.Contains(next) && next != target ? 24f : 0f;
                float nextCost = costs[current.TileId] + StepCost(terrain, current.TileId, next) + occupiedPenalty;
                if (nextCost >= costs[next]) continue;
                costs[next] = nextCost;
                parents[next] = current.TileId;
                heap.Push(next, nextCost + Heuristic(terrain.Width, next, target));
            }
        }
        return new List<int>();
    }

    private static bool CanTraverse(
        SpiritVeinTerrainSnapshot terrain,
        int tileId,
        int target,
        bool allowWaterEnd)
    {
        SpiritVeinTerrainCell cell = terrain[tileId];
        if (cell.IsLava || cell.IsGoo) return false;
        if (cell.IsWater) return allowWaterEnd && tileId == target;
        return true;
    }

    private static float StepCost(SpiritVeinTerrainSnapshot terrain, int from, int to)
    {
        SpiritVeinTerrainCell left = terrain[from];
        SpiritVeinTerrainCell right = terrain[to];
        float cost = 1f;
        if (right.IsMountain) cost += 1.4f;
        else if (right.IsHighland) cost += 0.55f;
        if (right.IsBeach) cost -= 0.12f;
        float heightDifference = right.Height - left.Height;
        if (heightDifference > 0f) cost += heightDifference * 0.82f;
        else cost += heightDifference * 0.09f;
        return Mathf.Max(0.2f, cost);
    }

    private static float Heuristic(int width, int from, int to)
    {
        int fromX = from % width;
        int fromY = from / width;
        int toX = to % width;
        int toY = to / width;
        return (Mathf.Abs(fromX - toX) + Mathf.Abs(fromY - toY)) * 0.64f;
    }

    private static List<int> ReconstructPath(int[] parents, int source, int target)
    {
        var reversed = new List<int>();
        int current = target;
        while (current >= 0)
        {
            reversed.Add(current);
            if (current == source) break;
            current = parents[current];
        }
        if (reversed.Count == 0 || reversed[reversed.Count - 1] != source) return new List<int>();
        reversed.Reverse();
        return reversed;
    }

    private static void TrimAtFirstIntersection(List<int> route, HashSet<int> occupied, int requiredEnd)
    {
        for (int i = 1; i < route.Count; i++)
        {
            int tileId = route[i];
            if (tileId == requiredEnd) return;
            if (!occupied.Contains(tileId)) continue;
            route.RemoveRange(i + 1, route.Count - i - 1);
            return;
        }
    }

    private static float ResolveSourceScore(SpiritVeinTerrainSnapshot terrain, int tileId)
    {
        int x = tileId % terrain.Width;
        int y = tileId / terrain.Width;
        int highland = 0;
        int mountain = 0;
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= terrain.Width || ny < 0 || ny >= terrain.Height) continue;
                SpiritVeinTerrainCell cell = terrain[ny * terrain.Width + nx];
                if (cell.IsHighland) highland++;
                if (cell.IsMountain) mountain++;
            }
        }
        int edge = Mathf.Min(Mathf.Min(x, terrain.Width - x - 1), Mathf.Min(y, terrain.Height - y - 1));
        return highland * 1.8f + mountain * 2.4f + Mathf.Min(edge, 24) * 0.5f +
               Stable01(terrain.WorldSeedId, tileId) * 2f;
    }

    private static bool HasUsableLandNeighbour(SpiritVeinTerrainSnapshot terrain, int tileId)
    {
        int x = tileId % terrain.Width;
        int y = tileId / terrain.Width;
        for (int i = 0; i < Directions.Length; i++)
        {
            int nx = x + Directions[i].x;
            int ny = y + Directions[i].y;
            if (nx < 0 || nx >= terrain.Width || ny < 0 || ny >= terrain.Height) continue;
            if (terrain[ny * terrain.Width + nx].IsUsableLand) return true;
        }
        return false;
    }

    private static bool HasWaterNearby(SpiritVeinTerrainSnapshot terrain, int tileId, int radius)
    {
        int x = tileId % terrain.Width;
        int y = tileId / terrain.Width;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= terrain.Width || ny < 0 || ny >= terrain.Height) continue;
                if (terrain[ny * terrain.Width + nx].IsWater) return true;
            }
        }
        return false;
    }

    private static bool IsWooded(string biomeId)
    {
        return !string.IsNullOrEmpty(biomeId) &&
               (biomeId.IndexOf("forest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                biomeId.IndexOf("jungle", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static int ResolveDominantElement(ElementComposition composition)
    {
        int best = 0;
        float value = composition[0];
        for (int i = 1; i < ElementIndex.Count; i++)
        {
            if (composition[i] <= value) continue;
            best = i;
            value = composition[i];
        }
        return best;
    }

    private static ElementComposition NormalizeComposition(float[] values)
    {
        return new ElementComposition(values, true);
    }

    private static void AddOccupied(HashSet<int> occupied, List<int> route)
    {
        for (int i = 0; i < route.Count; i++) occupied.Add(route[i]);
    }

    private static int TileDistance(int width, int left, int right)
    {
        return Mathf.Abs(left % width - right % width) + Mathf.Abs(left / width - right / width);
    }

    private static int[] CreateFilledArray(int count, int value)
    {
        var result = new int[count];
        if (value == 0) return result;
        for (int i = 0; i < result.Length; i++) result[i] = value;
        return result;
    }

    private static int StableValue(int seed, int value)
    {
        unchecked
        {
            uint x = (uint)seed * 1664525u + (uint)value * 1013904223u;
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            return (int)(x & 0x7fffffff);
        }
    }

    private static float Stable01(int seed, int value)
    {
        return StableValue(seed, value) / (float)int.MaxValue;
    }

    private static void Normalize(ref float x, ref float y)
    {
        float magnitude = Mathf.Sqrt(x * x + y * y);
        if (magnitude <= 0.0001f)
        {
            x = 0f;
            y = 0f;
            return;
        }
        x /= magnitude;
        y /= magnitude;
    }

    private static void Normalize(float[] valuesX, float[] valuesY, int index)
    {
        float x = valuesX[index];
        float y = valuesY[index];
        Normalize(ref x, ref y);
        valuesX[index] = x;
        valuesY[index] = y;
    }

    private static int CompareScoredTiles(ScoredTile left, ScoredTile right)
    {
        int score = right.Score.CompareTo(left.Score);
        return score != 0 ? score : left.TileId.CompareTo(right.TileId);
    }

    private static int CompareGroundCandidates(GroundCandidate left, GroundCandidate right)
    {
        int score = right.Score.CompareTo(left.Score);
        return score != 0 ? score : left.TileId.CompareTo(right.TileId);
    }

    private static SectionPlan FindPlan(List<SectionPlan> plans, int id)
    {
        for (int i = 0; i < plans.Count; i++)
        {
            if (plans[i].Id == id) return plans[i];
        }
        return null;
    }

    private static SpiritVeinDraft FindVein(List<SpiritVeinDraft> veins, int id)
    {
        for (int i = 0; i < veins.Count; i++)
        {
            if (veins[i].Id == id) return veins[i];
        }
        return null;
    }

    private static SpiritVeinBranch FindBranch(List<SpiritVeinBranch> branches, int id)
    {
        for (int i = 0; i < branches.Count; i++)
        {
            if (branches[i].Id == id) return branches[i];
        }
        return null;
    }

    private static SpiritVeinSection FindSection(List<SpiritVeinSection> sections, int id)
    {
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i].Id == id) return sections[i];
        }
        return null;
    }

    private static GatheringGround FindGround(List<GatheringGround> grounds, int id)
    {
        for (int i = 0; i < grounds.Count; i++)
        {
            if (grounds[i].Id == id) return grounds[i];
        }
        return null;
    }

    private readonly struct ScoredTile
    {
        internal ScoredTile(int tileId, float score)
        {
            TileId = tileId;
            Score = score;
        }
        internal int TileId { get; }
        internal float Score { get; }
    }

    private readonly struct GroundCandidate
    {
        internal GroundCandidate(int tileId, float score)
        {
            TileId = tileId;
            Score = score;
        }
        internal int TileId { get; }
        internal float Score { get; }
    }

    private sealed class GuideRoute
    {
        internal GuideRoute(int veinId, int branchId, bool main, int source, int sink, List<int> tiles)
        {
            VeinId = veinId;
            BranchId = branchId;
            Main = main;
            Source = source;
            Sink = sink;
            Tiles = tiles;
        }
        internal int VeinId { get; }
        internal int BranchId { get; }
        internal bool Main { get; }
        internal int Source { get; }
        internal int Sink { get; }
        internal List<int> Tiles { get; }
        internal int[] SectionIdByPoint { get; set; }
        internal List<SectionPlan> SectionPlans { get; } = new();
    }

    private sealed class SectionPlan
    {
        internal SectionPlan(
            int id,
            int veinId,
            int branchId,
            VeinSectionKind kind,
            GuideRoute route,
            int start,
            int end)
        {
            Id = id;
            VeinId = veinId;
            BranchId = branchId;
            Kind = kind;
            Route = route;
            Start = start;
            End = end;
            GuideTiles = route.Tiles.GetRange(start, end - start + 1);
        }
        internal int Id { get; }
        internal int VeinId { get; }
        internal int BranchId { get; }
        internal VeinSectionKind Kind { get; set; }
        internal GuideRoute Route { get; }
        internal int Start { get; }
        internal int End { get; }
        internal List<int> GuideTiles { get; }
        internal List<int> UpstreamIds { get; } = new();
        internal List<int> DownstreamIds { get; } = new();
    }

    private sealed class MinHeap
    {
        private readonly List<HeapNode> nodes = new();
        internal int Count => nodes.Count;

        internal void Push(int tileId, float priority)
        {
            nodes.Add(new HeapNode(tileId, priority));
            int index = nodes.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (nodes[parent].Priority <= nodes[index].Priority) break;
                (nodes[parent], nodes[index]) = (nodes[index], nodes[parent]);
                index = parent;
            }
        }

        internal HeapNode Pop()
        {
            HeapNode result = nodes[0];
            int last = nodes.Count - 1;
            nodes[0] = nodes[last];
            nodes.RemoveAt(last);
            int index = 0;
            while (true)
            {
                int left = index * 2 + 1;
                int right = left + 1;
                if (left >= nodes.Count) break;
                int smallest = right < nodes.Count && nodes[right].Priority < nodes[left].Priority ? right : left;
                if (nodes[index].Priority <= nodes[smallest].Priority) break;
                (nodes[index], nodes[smallest]) = (nodes[smallest], nodes[index]);
                index = smallest;
            }
            return result;
        }
    }

    private readonly struct HeapNode
    {
        internal HeapNode(int tileId, float priority)
        {
            TileId = tileId;
            Priority = priority;
        }
        internal int TileId { get; }
        internal float Priority { get; }
    }
}

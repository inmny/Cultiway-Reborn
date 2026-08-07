using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Content.MapModeVisuals;

internal sealed class KingdomMapContour
{
    internal readonly Vector2[] Points;
    internal readonly long Owner;

    internal KingdomMapContour(Vector2[] points, long owner)
    {
        Points = points;
        Owner = owner;
    }
}

internal sealed class KingdomBoundaryPath
{
    internal readonly Vector2[] Points;
    internal readonly long LeftOwner;
    internal readonly long RightOwner;
    internal readonly bool Cycle;

    internal KingdomBoundaryPath(Vector2[] points, long leftOwner, long rightOwner, bool cycle)
    {
        Points = points;
        LeftOwner = leftOwner;
        RightOwner = rightOwner;
        Cycle = cycle;
    }
}

internal sealed class KingdomBoundaryMap
{
    internal readonly KingdomMapContour[] Contours;
    internal readonly KingdomBoundaryPath[] Paths;

    internal KingdomBoundaryMap(KingdomMapContour[] contours, KingdomBoundaryPath[] paths)
    {
        Contours = contours;
        Paths = paths;
    }
}

internal static class KingdomMapBoundaryBuilder
{
    private const int ZoneSize = 8;
    private const int MaximumShortcutEdges = 8;
    private const float CorridorRadius = ZoneSize * 0.5f;
    private const float DetailSpacing = ZoneSize * 0.5f;
    private const float DetailOffset = ZoneSize * 0.2f;
    private const float BoundaryVertexOffset = ZoneSize * 0.14f;
    private const float JunctionOffset = ZoneSize * 0.18f;
    private const float ThreeWayJunctionOffset = ZoneSize * 0.45f;
    private const float MinimumShortDetourDepth = ZoneSize * 0.45f;
    private const float MaximumShortDetourDepth = ZoneSize * 1.2f;
    private const float MinimumShortDetourWidth = ZoneSize * 0.65f;
    private const float MaximumShortDetourWidth = ZoneSize * 3.5f;
    private const float MaximumCornerTrim = ZoneSize * 0.72f;
    private const float MinimumRoundedCross = 0.3f;
    private const float SampleStep = 1f;
    private const float ShortDetourSampleStep = 0.5f;
    private const float Epsilon = 0.0001f;

    internal static KingdomBoundaryMap Build(KingdomMapSnapshot snapshot)
    {
        List<RawEdge> edges = BuildRawEdges(snapshot);
        if (edges.Count == 0)
        {
            return new KingdomBoundaryMap(Array.Empty<KingdomMapContour>(), Array.Empty<KingdomBoundaryPath>());
        }

        Dictionary<GridPoint, List<int>> incident = BuildIncidentEdges(edges);
        HashSet<GridPoint> hardVertices = BuildHardVertices(snapshot, edges, incident);
        List<RawChain> chains = TraceChains(snapshot, edges, incident, hardVertices);
        KingdomBoundaryPath[] paths = FitPaths(chains, snapshot.WorldWidth, snapshot.WorldHeight);
        return new KingdomBoundaryMap(BuildContours(paths), paths);
    }

    private static List<RawEdge> BuildRawEdges(KingdomMapSnapshot snapshot)
    {
        var result = new List<RawEdge>(snapshot.Owners.Length * 2);
        for (int y = 0; y < snapshot.ZonesHeight; y++)
        {
            int bottom = y * ZoneSize;
            int top = Math.Min(bottom + ZoneSize, snapshot.WorldHeight);
            AddEdge(result, new GridPoint(0, bottom), new GridPoint(0, top), 0, snapshot.GetOwner(0, y));
            for (int x = 1; x < snapshot.ZonesWidth; x++)
            {
                int position = Math.Min(x * ZoneSize, snapshot.WorldWidth);
                AddEdge(
                    result,
                    new GridPoint(position, bottom),
                    new GridPoint(position, top),
                    snapshot.GetOwner(x - 1, y),
                    snapshot.GetOwner(x, y));
            }
            AddEdge(
                result,
                new GridPoint(snapshot.WorldWidth, bottom),
                new GridPoint(snapshot.WorldWidth, top),
                snapshot.GetOwner(snapshot.ZonesWidth - 1, y),
                0);
        }

        for (int x = 0; x < snapshot.ZonesWidth; x++)
        {
            int left = x * ZoneSize;
            int right = Math.Min(left + ZoneSize, snapshot.WorldWidth);
            AddEdge(result, new GridPoint(left, 0), new GridPoint(right, 0), snapshot.GetOwner(x, 0), 0);
            for (int y = 1; y < snapshot.ZonesHeight; y++)
            {
                int position = Math.Min(y * ZoneSize, snapshot.WorldHeight);
                AddEdge(
                    result,
                    new GridPoint(left, position),
                    new GridPoint(right, position),
                    snapshot.GetOwner(x, y),
                    snapshot.GetOwner(x, y - 1));
            }
            AddEdge(
                result,
                new GridPoint(left, snapshot.WorldHeight),
                new GridPoint(right, snapshot.WorldHeight),
                0,
                snapshot.GetOwner(x, snapshot.ZonesHeight - 1));
        }
        return result;
    }

    private static void AddEdge(
        List<RawEdge> edges,
        GridPoint from,
        GridPoint to,
        long leftOwner,
        long rightOwner)
    {
        if (leftOwner == rightOwner || from == to) return;
        edges.Add(new RawEdge(from, to, leftOwner, rightOwner));
    }

    private static Dictionary<GridPoint, List<int>> BuildIncidentEdges(IReadOnlyList<RawEdge> edges)
    {
        var result = new Dictionary<GridPoint, List<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            AddIncident(result, edges[i].From, i);
            AddIncident(result, edges[i].To, i);
        }
        return result;
    }

    private static void AddIncident(Dictionary<GridPoint, List<int>> incident, GridPoint point, int edgeIndex)
    {
        if (!incident.TryGetValue(point, out List<int> edges))
        {
            edges = new List<int>(4);
            incident.Add(point, edges);
        }
        edges.Add(edgeIndex);
    }

    private static HashSet<GridPoint> BuildHardVertices(
        KingdomMapSnapshot snapshot,
        IReadOnlyList<RawEdge> edges,
        Dictionary<GridPoint, List<int>> incident)
    {
        var result = new HashSet<GridPoint>();
        foreach (KeyValuePair<GridPoint, List<int>> pair in incident)
        {
            List<int> edgeIndices = pair.Value;
            if (edgeIndices.Count != 2 || edges[edgeIndices[0]].Pair != edges[edgeIndices[1]].Pair)
            {
                result.Add(pair.Key);
                continue;
            }

            GridPoint point = pair.Key;
            bool corner = (point.X == 0 || point.X == snapshot.WorldWidth) &&
                          (point.Y == 0 || point.Y == snapshot.WorldHeight);
            if (corner || IsFrameTurn(point, edges[edgeIndices[0]], edges[edgeIndices[1]], snapshot))
                result.Add(point);
        }

        return result;
    }

    private static bool IsFrameTurn(
        GridPoint point,
        RawEdge first,
        RawEdge second,
        KingdomMapSnapshot snapshot)
    {
        bool onFrame = point.X == 0 || point.X == snapshot.WorldWidth ||
                       point.Y == 0 || point.Y == snapshot.WorldHeight;
        if (!onFrame) return false;
        if (!IsFrameEdge(first, snapshot) || !IsFrameEdge(second, snapshot)) return true;
        return first.From.X == first.To.X != (second.From.X == second.To.X);
    }

    private static bool IsFrameEdge(RawEdge edge, KingdomMapSnapshot snapshot)
    {
        return edge.From.X == edge.To.X && (edge.From.X == 0 || edge.From.X == snapshot.WorldWidth) ||
               edge.From.Y == edge.To.Y && (edge.From.Y == 0 || edge.From.Y == snapshot.WorldHeight);
    }

    private static List<RawChain> TraceChains(
        KingdomMapSnapshot snapshot,
        IReadOnlyList<RawEdge> edges,
        Dictionary<GridPoint, List<int>> incident,
        HashSet<GridPoint> hardVertices)
    {
        var result = new List<RawChain>();
        var visited = new bool[edges.Count];
        var starts = new List<GridPoint>(hardVertices);
        starts.Sort();
        for (int i = 0; i < starts.Count; i++)
        {
            if (!incident.TryGetValue(starts[i], out List<int> edgeIndices)) continue;
            edgeIndices.Sort();
            for (int j = 0; j < edgeIndices.Count; j++)
            {
                if (!visited[edgeIndices[j]])
                    result.Add(TraceChain(
                        snapshot,
                        edges,
                        incident,
                        hardVertices,
                        visited,
                        edgeIndices[j],
                        starts[i]));
            }
        }

        for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            if (!visited[edgeIndex])
                result.Add(TraceChain(
                    snapshot,
                    edges,
                    incident,
                    hardVertices,
                    visited,
                    edgeIndex,
                    edges[edgeIndex].From));
        }
        return result;
    }

    private static RawChain TraceChain(
        KingdomMapSnapshot snapshot,
        IReadOnlyList<RawEdge> edges,
        Dictionary<GridPoint, List<int>> incident,
        HashSet<GridPoint> hardVertices,
        bool[] visited,
        int firstEdgeIndex,
        GridPoint start)
    {
        var points = new List<Vector2> { ResolveVertexPosition(snapshot, hardVertices, start) };
        int edgeIndex = firstEdgeIndex;
        GridPoint current = start;
        long leftOwner = 0;
        long rightOwner = 0;
        bool first = true;
        bool cycle = false;
        while (true)
        {
            RawEdge edge = edges[edgeIndex];
            bool forward = edge.From == current;
            GridPoint next = forward ? edge.To : edge.From;
            long edgeLeft = forward ? edge.LeftOwner : edge.RightOwner;
            long edgeRight = forward ? edge.RightOwner : edge.LeftOwner;
            if (first)
            {
                leftOwner = edgeLeft;
                rightOwner = edgeRight;
                first = false;
            }
            visited[edgeIndex] = true;
            points.Add(ResolveVertexPosition(snapshot, hardVertices, next));

            if (next == start)
            {
                cycle = !hardVertices.Contains(start);
                break;
            }
            if (hardVertices.Contains(next)) break;

            List<int> nextEdges = incident[next];
            int nextEdgeIndex = nextEdges[0] == edgeIndex ? nextEdges[1] : nextEdges[0];
            if (visited[nextEdgeIndex]) break;
            RawEdge nextEdge = edges[nextEdgeIndex];
            bool nextForward = nextEdge.From == next;
            long nextLeft = nextForward ? nextEdge.LeftOwner : nextEdge.RightOwner;
            long nextRight = nextForward ? nextEdge.RightOwner : nextEdge.LeftOwner;
            if (nextLeft != leftOwner || nextRight != rightOwner) break;
            current = next;
            edgeIndex = nextEdgeIndex;
        }
        return new RawChain(points.ToArray(), leftOwner, rightOwner, cycle);
    }

    private static Vector2 ResolveVertexPosition(
        KingdomMapSnapshot snapshot,
        HashSet<GridPoint> hardVertices,
        GridPoint point)
    {
        Vector2 position = point.ToVector2();
        if (point.X == 0 || point.X == snapshot.WorldWidth ||
            point.Y == 0 || point.Y == snapshot.WorldHeight)
            return position;

        int zoneX = point.X / ZoneSize;
        int zoneY = point.Y / ZoneSize;
        uint hash = 2166136261;
        MixHash(ref hash, point.X);
        MixHash(ref hash, point.Y);
        MixHash(ref hash, snapshot.GetOwner(zoneX - 1, zoneY - 1).GetHashCode());
        MixHash(ref hash, snapshot.GetOwner(zoneX, zoneY - 1).GetHashCode());
        MixHash(ref hash, snapshot.GetOwner(zoneX - 1, zoneY).GetHashCode());
        MixHash(ref hash, snapshot.GetOwner(zoneX, zoneY).GetHashCode());
        float offsetX = SignedHashValue(hash);
        MixHash(ref hash, 0x53a9);
        float offsetY = SignedHashValue(hash);
        if (hardVertices.Contains(point) && TryGetThreeWayJunctionDirection(snapshot, point, out Vector2 direction))
        {
            Vector2 tangent = new(-direction.y, direction.x);
            return position + direction * ThreeWayJunctionOffset +
                   tangent * (offsetX * BoundaryVertexOffset * 0.25f);
        }
        float offset = hardVertices.Contains(point) ? JunctionOffset : BoundaryVertexOffset;
        return position + new Vector2(offsetX, offsetY) * offset;
    }

    private static bool TryGetThreeWayJunctionDirection(
        KingdomMapSnapshot snapshot,
        GridPoint point,
        out Vector2 direction)
    {
        int zoneX = point.X / ZoneSize;
        int zoneY = point.Y / ZoneSize;
        bool up = snapshot.GetOwner(zoneX - 1, zoneY) != snapshot.GetOwner(zoneX, zoneY);
        bool right = snapshot.GetOwner(zoneX, zoneY) != snapshot.GetOwner(zoneX, zoneY - 1);
        bool down = snapshot.GetOwner(zoneX, zoneY - 1) != snapshot.GetOwner(zoneX - 1, zoneY - 1);
        bool left = snapshot.GetOwner(zoneX - 1, zoneY - 1) != snapshot.GetOwner(zoneX - 1, zoneY);
        int count = (up ? 1 : 0) + (right ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0);
        if (count != 3)
        {
            direction = Vector2.zero;
            return false;
        }
        direction = !up ? Vector2.down : !right ? Vector2.left : !down ? Vector2.up : Vector2.right;
        return true;
    }

    private static float SignedHashValue(uint hash)
    {
        float value = (hash & 0xffff) / 65535f * 2f - 1f;
        float sign = value < 0f ? -1f : 1f;
        return sign * (0.45f + Math.Abs(value) * 0.55f);
    }

    private static KingdomBoundaryPath[] FitPaths(
        IReadOnlyList<RawChain> chains,
        int worldWidth,
        int worldHeight)
    {
        var variants = new PathVariants[chains.Count];
        for (int i = 0; i < chains.Count; i++)
            variants[i] = BuildVariants(chains[i], worldWidth, worldHeight);

        var levels = new int[chains.Count];
        Vector2[][] resolved = ResolveVariants(variants, levels);
        for (int iteration = 0; iteration < chains.Count * 2 + 3; iteration++)
        {
            HashSet<int> conflicts = FindConflictingPaths(resolved);
            if (conflicts.Count == 0) break;
            bool changed = false;
            foreach (int pathIndex in conflicts)
            {
                if (levels[pathIndex] >= 2) continue;
                levels[pathIndex]++;
                changed = true;
            }
            if (!changed) break;
            resolved = ResolveVariants(variants, levels);
        }

        var result = new KingdomBoundaryPath[chains.Count];
        for (int i = 0; i < chains.Count; i++)
        {
            result[i] = new KingdomBoundaryPath(
                resolved[i],
                chains[i].LeftOwner,
                chains[i].RightOwner,
                chains[i].Cycle);
        }
        return result;
    }

    private static PathVariants BuildVariants(RawChain chain, int worldWidth, int worldHeight)
    {
        Vector2[] raw = RemoveDuplicatePoints(chain.Points);
        Vector2[] turns = ExtractGridTurns(raw, chain.Cycle);
        Vector2[] collapsed = CollapseShortDetours(turns, chain.Cycle);
        bool folded = collapsed.Length < turns.Length;
        Vector2[] gridRaw = folded ? BuildGridPath(raw, worldWidth, worldHeight) : raw;
        float corridorRadius = folded ? MaximumShortDetourDepth : CorridorRadius;
        if (folded && !PathInsideCorridor(collapsed, gridRaw, ShortDetourSampleStep, corridorRadius))
        {
            folded = false;
            corridorRadius = CorridorRadius;
        }
        Vector2[] basePath = folded ? collapsed : raw;
        Vector2[] corridor = folded ? gridRaw : raw;
        float sampleStep = folded ? ShortDetourSampleStep : SampleStep;
        Vector2[] simplified = chain.Cycle ? SimplifyClosed(basePath) : SimplifyOpen(basePath);
        if (!PathInsideCorridor(simplified, corridor, sampleStep, corridorRadius)) simplified = basePath;
        Vector2[] detailed = AddBoundaryDetail(simplified, chain, worldWidth, worldHeight);
        if (!PathInsideCorridor(detailed, corridor, sampleStep, corridorRadius)) detailed = simplified;
        Vector2[] rounded = chain.Cycle
            ? RoundClosed(detailed, chain.LeftOwner, chain.RightOwner)
            : RoundOpen(detailed, chain.LeftOwner, chain.RightOwner);
        if (!PathInsideCorridor(rounded, corridor, sampleStep, corridorRadius)) rounded = simplified;
        return new PathVariants(rounded, simplified, raw);
    }

    private static Vector2[] BuildGridPath(Vector2[] path, int worldWidth, int worldHeight)
    {
        var result = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++)
        {
            result[i] = new Vector2(
                GridCoordinate(path[i].x, worldWidth),
                GridCoordinate(path[i].y, worldHeight));
        }
        return result;
    }

    private static float GridCoordinate(float value, int worldExtent)
    {
        if (Math.Abs(value) <= JunctionOffset) return 0f;
        if (Math.Abs(value - worldExtent) <= JunctionOffset) return worldExtent;
        return (float)Math.Round(value / ZoneSize) * ZoneSize;
    }

    private static Vector2[] ExtractGridTurns(Vector2[] path, bool cycle)
    {
        if (path.Length < 3) return path;
        if (!cycle)
        {
            var result = new List<Vector2>(path.Length) { path[0] };
            for (int i = 1; i < path.Length - 1; i++)
            {
                if (GridDirection(path[i - 1], path[i]) != GridDirection(path[i], path[i + 1]))
                    AppendDistinct(result, path[i]);
            }
            AppendDistinct(result, path[^1]);
            return result.ToArray();
        }

        int count = path.Length - 1;
        var closed = new List<Vector2>(count + 1);
        for (int i = 0; i < count; i++)
        {
            Vector2 previous = path[(i + count - 1) % count];
            Vector2 current = path[i];
            Vector2 next = path[(i + 1) % count];
            if (GridDirection(previous, current) != GridDirection(current, next))
                AppendDistinct(closed, current);
        }
        if (closed.Count < 3) return path;
        closed.Add(closed[0]);
        return closed.ToArray();
    }

    private static int GridDirection(Vector2 from, Vector2 to)
    {
        Vector2 direction = to - from;
        if (Math.Abs(direction.x) >= Math.Abs(direction.y)) return direction.x < 0f ? 0 : 1;
        return direction.y < 0f ? 2 : 3;
    }

    private static Vector2[] CollapseShortDetours(Vector2[] path, bool cycle)
    {
        if (!cycle) return CollapseOpenShortDetours(path);

        int count = path.Length - 1;
        if (count < 5) return path;
        Vector2[] best = path;
        for (int offset = 0; offset < count; offset++)
        {
            var rotated = new Vector2[count + 1];
            for (int i = 0; i < count; i++) rotated[i] = path[(offset + i) % count];
            rotated[count] = rotated[0];
            Vector2[] candidate = CollapseOpenShortDetours(rotated);
            if (candidate.Length >= 5 && candidate.Length < best.Length) best = candidate;
        }
        return best;
    }

    private static Vector2[] CollapseOpenShortDetours(Vector2[] path)
    {
        if (path.Length < 4) return path;
        var result = new List<Vector2>(path.Length) { path[0] };
        int index = 0;
        while (index < path.Length - 1)
        {
            if (index + 3 < path.Length && IsShortDetour(
                    path[index],
                    path[index + 1],
                    path[index + 2],
                    path[index + 3]))
            {
                AppendDistinct(result, path[index + 3]);
                index += 3;
                continue;
            }
            AppendDistinct(result, path[index + 1]);
            index++;
        }
        return result.ToArray();
    }

    private static bool IsShortDetour(
        Vector2 mouthFrom,
        Vector2 backFrom,
        Vector2 backTo,
        Vector2 mouthTo)
    {
        Vector2 gridMouthFrom = SnapGridPoint(mouthFrom);
        Vector2 gridBackFrom = SnapGridPoint(backFrom);
        Vector2 gridBackTo = SnapGridPoint(backTo);
        Vector2 gridMouthTo = SnapGridPoint(mouthTo);
        Vector2 entering = gridBackFrom - gridMouthFrom;
        Vector2 back = gridBackTo - gridBackFrom;
        Vector2 leaving = gridMouthTo - gridBackTo;
        Vector2 mouth = gridMouthTo - gridMouthFrom;
        float enteringLength = entering.magnitude;
        float backLength = back.magnitude;
        float leavingLength = leaving.magnitude;
        float mouthLength = mouth.magnitude;
        if (enteringLength < Epsilon || backLength < Epsilon ||
            leavingLength < Epsilon || mouthLength < Epsilon)
            return false;

        Vector2 enteringDirection = entering / enteringLength;
        Vector2 backDirection = back / backLength;
        Vector2 leavingDirection = leaving / leavingLength;
        Vector2 mouthDirection = mouth / mouthLength;
        if (Vector2.Dot(enteringDirection, leavingDirection) > -0.72f ||
            Math.Abs(Vector2.Dot(enteringDirection, backDirection)) > 0.42f ||
            Vector2.Dot(backDirection, mouthDirection) < 0.68f)
            return false;

        Vector2 backCenter = (gridBackFrom + gridBackTo) * 0.5f;
        Vector2 mouthCenter = ClosestPointOnSegment(backCenter, gridMouthFrom, gridMouthTo);
        float depth = Distance(backCenter, mouthCenter);
        if (depth < MinimumShortDetourDepth || depth > MaximumShortDetourDepth ||
            backLength < MinimumShortDetourWidth || backLength > MaximumShortDetourWidth ||
            enteringLength > MaximumShortDetourDepth * 1.35f ||
            leavingLength > MaximumShortDetourDepth * 1.35f)
            return false;

        return true;
    }

    private static Vector2 SnapGridPoint(Vector2 point)
    {
        return new Vector2(
            (float)Math.Round(point.x / ZoneSize) * ZoneSize,
            (float)Math.Round(point.y / ZoneSize) * ZoneSize);
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float length = segment.sqrMagnitude;
        if (length < Epsilon) return from;
        float amount = Math.Max(0f, Math.Min(1f, Vector2.Dot(point - from, segment) / length));
        return from + segment * amount;
    }

    private static Vector2[] AddBoundaryDetail(
        Vector2[] path,
        RawChain chain,
        int worldWidth,
        int worldHeight)
    {
        var result = new List<Vector2>(path.Length * 2) { path[0] };
        for (int segmentIndex = 0; segmentIndex < path.Length - 1; segmentIndex++)
        {
            Vector2 from = path[segmentIndex];
            Vector2 to = path[segmentIndex + 1];
            float length = Distance(from, to);
            bool frame = IsFrameSegment(from, to, worldWidth, worldHeight);
            int sectionCount = frame ? 1 : Math.Max(1, (int)Math.Ceiling(length / DetailSpacing));
            Vector2 direction = length < Epsilon ? Vector2.right : (to - from) / length;
            Vector2 normal = new(-direction.y, direction.x);
            float axisFactor = Math.Abs(direction.x) < Epsilon || Math.Abs(direction.y) < Epsilon ? 1f : 0.65f;
            for (int section = 1; section < sectionCount; section++)
            {
                float amount = section / (float)sectionCount;
                float variation = SignedVariation(
                    from,
                    to,
                    segmentIndex * 17 + section,
                    chain.LeftOwner,
                    chain.RightOwner);
                float envelope = (float)Math.Sin(amount * Math.PI);
                Vector2 point = Vector2.Lerp(from, to, amount) +
                                normal * (variation * DetailOffset * axisFactor * envelope);
                AppendDistinct(result, point);
            }
            AppendDistinct(result, to);
        }
        return result.ToArray();
    }

    private static bool IsFrameSegment(Vector2 from, Vector2 to, int worldWidth, int worldHeight)
    {
        return Math.Abs(from.x) < Epsilon && Math.Abs(to.x) < Epsilon ||
               Math.Abs(from.x - worldWidth) < Epsilon && Math.Abs(to.x - worldWidth) < Epsilon ||
               Math.Abs(from.y) < Epsilon && Math.Abs(to.y) < Epsilon ||
               Math.Abs(from.y - worldHeight) < Epsilon && Math.Abs(to.y - worldHeight) < Epsilon;
    }

    private static float SignedVariation(
        Vector2 from,
        Vector2 to,
        int salt,
        long leftOwner,
        long rightOwner)
    {
        uint hash = 2166136261;
        MixHash(ref hash, (int)Math.Round(from.x * 16f));
        MixHash(ref hash, (int)Math.Round(from.y * 16f));
        MixHash(ref hash, (int)Math.Round(to.x * 16f));
        MixHash(ref hash, (int)Math.Round(to.y * 16f));
        MixHash(ref hash, salt);
        MixHash(ref hash, leftOwner.GetHashCode());
        MixHash(ref hash, rightOwner.GetHashCode());
        float value = (hash & 0xffff) / 65535f * 2f - 1f;
        return Math.Sign(value) * (0.45f + Math.Abs(value) * 0.55f);
    }

    private static float UnitVariation(Vector2 point, int salt, long leftOwner, long rightOwner)
    {
        uint hash = 2166136261;
        MixHash(ref hash, (int)Math.Round(point.x * 16f));
        MixHash(ref hash, (int)Math.Round(point.y * 16f));
        MixHash(ref hash, salt);
        MixHash(ref hash, leftOwner.GetHashCode());
        MixHash(ref hash, rightOwner.GetHashCode());
        return (hash & 0xffff) / 65535f;
    }

    private static void MixHash(ref uint hash, int value)
    {
        unchecked
        {
            hash = (hash ^ (uint)value) * 16777619;
        }
    }

    private static Vector2[] SimplifyOpen(Vector2[] path)
    {
        if (path.Length <= 4) return path;
        var result = new List<Vector2>(path.Length) { path[0] };
        Vector2[] middle = SimplifyRange(path, 1, path.Length - 2);
        for (int i = 0; i < middle.Length; i++) AppendDistinct(result, middle[i]);
        AppendDistinct(result, path[^1]);
        return result.ToArray();
    }

    private static Vector2[] SimplifyClosed(Vector2[] path)
    {
        int count = path.Length - 1;
        if (count < 4) return path;
        int first = 0;
        for (int i = 1; i < count; i++)
        {
            if (Compare(path[i], path[first]) < 0) first = i;
        }

        int opposite = first;
        float farthest = -1f;
        for (int i = 0; i < count; i++)
        {
            float distance = (path[i] - path[first]).sqrMagnitude;
            if (distance <= farthest) continue;
            opposite = i;
            farthest = distance;
        }

        Vector2[] firstArc = ExtractClosedArc(path, first, opposite, count);
        Vector2[] secondArc = ExtractClosedArc(path, opposite, first, count);
        Vector2[] firstSimplified = SimplifyRange(firstArc, 0, firstArc.Length - 1);
        Vector2[] secondSimplified = SimplifyRange(secondArc, 0, secondArc.Length - 1);
        var result = new List<Vector2>(firstSimplified.Length + secondSimplified.Length);
        for (int i = 0; i < firstSimplified.Length; i++) AppendDistinct(result, firstSimplified[i]);
        for (int i = 1; i < secondSimplified.Length; i++) AppendDistinct(result, secondSimplified[i]);
        if (!SamePoint(result[0], result[^1])) result.Add(result[0]);

        float rawArea = SignedArea(path);
        float resultArea = SignedArea(result);
        if (result.Count < 4 || rawArea * resultArea <= 0f ||
            Math.Abs(resultArea) < Math.Abs(rawArea) * 0.35f)
            return path;
        return result.ToArray();
    }

    private static Vector2[] ExtractClosedArc(Vector2[] path, int from, int to, int count)
    {
        var result = new List<Vector2>();
        int index = from;
        result.Add(path[index]);
        while (index != to)
        {
            index = (index + 1) % count;
            result.Add(path[index]);
        }
        return result.ToArray();
    }

    private static Vector2[] SimplifyRange(Vector2[] path, int from, int to)
    {
        var result = new List<Vector2> { path[from] };
        int current = from;
        while (current < to)
        {
            int maximum = Math.Min(to, current + MaximumShortcutEdges);
            int next = maximum;
            while (next > current + 1 && !SegmentInsideCorridor(path, current, next)) next--;
            result.Add(path[next]);
            current = next;
        }
        return result.ToArray();
    }

    private static bool SegmentInsideCorridor(Vector2[] path, int from, int to)
    {
        Vector2 start = path[from];
        Vector2 end = path[to];
        int samples = Math.Max(1, (int)Math.Ceiling(Distance(start, end) / SampleStep));
        float maximumDistance = CorridorRadius * CorridorRadius + Epsilon;
        for (int sample = 1; sample < samples; sample++)
        {
            Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
            float nearest = float.MaxValue;
            for (int i = from; i < to; i++)
            {
                nearest = Math.Min(nearest, DistanceToSegmentSquared(point, path[i], path[i + 1]));
            }
            if (nearest > maximumDistance) return false;
        }
        return true;
    }

    private static Vector2[] RoundOpen(Vector2[] path, long leftOwner, long rightOwner)
    {
        if (path.Length < 3) return path;
        var result = new List<Vector2>(path.Length * 3) { path[0] };
        for (int i = 1; i < path.Length - 1; i++)
            AppendRoundedCorner(result, path[i - 1], path[i], path[i + 1], leftOwner, rightOwner);
        AppendDistinct(result, path[^1]);
        return result.ToArray();
    }

    private static Vector2[] RoundClosed(Vector2[] path, long leftOwner, long rightOwner)
    {
        int count = path.Length - 1;
        if (count < 3) return path;
        Vector2 previous = path[count - 1];
        Vector2 current = path[0];
        Vector2 next = path[1];
        GetCornerPoints(
            previous,
            current,
            next,
            leftOwner,
            rightOwner,
            out Vector2 firstEntry,
            out Vector2 firstExit,
            out bool firstRound);
        var result = new List<Vector2>(count * 3) { firstRound ? firstExit : current };
        for (int i = 1; i < count; i++)
        {
            AppendRoundedCorner(
                result,
                path[i - 1],
                path[i],
                path[(i + 1) % count],
                leftOwner,
                rightOwner);
        }
        if (firstRound)
        {
            AppendDistinct(result, firstEntry);
            AppendQuadratic(result, firstEntry, current, firstExit);
        }
        else
        {
            AppendDistinct(result, current);
        }
        return result.ToArray();
    }

    private static void AppendRoundedCorner(
        List<Vector2> result,
        Vector2 previous,
        Vector2 current,
        Vector2 next,
        long leftOwner,
        long rightOwner)
    {
        GetCornerPoints(
            previous,
            current,
            next,
            leftOwner,
            rightOwner,
            out Vector2 entry,
            out Vector2 exit,
            out bool round);
        if (!round)
        {
            AppendDistinct(result, current);
            return;
        }
        AppendDistinct(result, entry);
        AppendQuadratic(result, entry, current, exit);
    }

    private static void GetCornerPoints(
        Vector2 previous,
        Vector2 current,
        Vector2 next,
        long leftOwner,
        long rightOwner,
        out Vector2 entry,
        out Vector2 exit,
        out bool round)
    {
        Vector2 incoming = current - previous;
        Vector2 outgoing = next - current;
        float incomingLength = incoming.magnitude;
        float outgoingLength = outgoing.magnitude;
        if (incomingLength < Epsilon || outgoingLength < Epsilon)
        {
            entry = current;
            exit = current;
            round = false;
            return;
        }

        incoming /= incomingLength;
        outgoing /= outgoingLength;
        float cross = incoming.x * outgoing.y - incoming.y * outgoing.x;
        round = Math.Abs(cross) >= MinimumRoundedCross;
        float baseFactor = 0.32f + UnitVariation(current, 41, leftOwner, rightOwner) * 0.12f;
        float entryFactor = baseFactor * (0.78f + UnitVariation(current, 73, leftOwner, rightOwner) * 0.32f);
        float exitFactor = baseFactor * (0.78f + UnitVariation(current, 109, leftOwner, rightOwner) * 0.32f);
        float entryTrim = Math.Min(MaximumCornerTrim, incomingLength * entryFactor);
        float exitTrim = Math.Min(MaximumCornerTrim, outgoingLength * exitFactor);
        entry = round ? current - incoming * entryTrim : current;
        exit = round ? current + outgoing * exitTrim : current;
    }

    private static void AppendQuadratic(List<Vector2> result, Vector2 from, Vector2 control, Vector2 to)
    {
        for (int sample = 1; sample <= 3; sample++)
        {
            float t = sample / 3f;
            float inverse = 1f - t;
            AppendDistinct(result, from * (inverse * inverse) + control * (2f * inverse * t) + to * (t * t));
        }
    }

    private static bool PathInsideCorridor(
        Vector2[] candidate,
        Vector2[] raw,
        float sampleStep,
        float corridorRadius)
    {
        float maximumDistance = corridorRadius * corridorRadius + Epsilon;
        for (int segment = 0; segment < candidate.Length - 1; segment++)
        {
            int samples = Math.Max(1, (int)Math.Ceiling(Distance(candidate[segment], candidate[segment + 1]) / sampleStep));
            for (int sample = 0; sample <= samples; sample++)
            {
                Vector2 point = Vector2.Lerp(candidate[segment], candidate[segment + 1], sample / (float)samples);
                float nearest = float.MaxValue;
                for (int rawSegment = 0; rawSegment < raw.Length - 1; rawSegment++)
                {
                    nearest = Math.Min(
                        nearest,
                        DistanceToSegmentSquared(point, raw[rawSegment], raw[rawSegment + 1]));
                }
                if (nearest > maximumDistance) return false;
            }
        }
        return true;
    }

    private static Vector2[][] ResolveVariants(PathVariants[] variants, int[] levels)
    {
        var result = new Vector2[variants.Length][];
        for (int i = 0; i < result.Length; i++) result[i] = variants[i].Get(levels[i]);
        return result;
    }

    private static HashSet<int> FindConflictingPaths(Vector2[][] paths)
    {
        var conflicts = new HashSet<int>();
        var buckets = new Dictionary<BucketKey, List<SegmentReference>>();
        for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
        {
            Vector2[] path = paths[pathIndex];
            for (int segmentIndex = 0; segmentIndex < path.Length - 1; segmentIndex++)
            {
                Vector2 from = path[segmentIndex];
                Vector2 to = path[segmentIndex + 1];
                int minX = (int)Math.Floor(Math.Min(from.x, to.x) / ZoneSize);
                int maxX = (int)Math.Floor(Math.Max(from.x, to.x) / ZoneSize);
                int minY = (int)Math.Floor(Math.Min(from.y, to.y) / ZoneSize);
                int maxY = (int)Math.Floor(Math.Max(from.y, to.y) / ZoneSize);
                var compared = new HashSet<SegmentReference>();
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var bucket = new BucketKey(x, y);
                        if (!buckets.TryGetValue(bucket, out List<SegmentReference> references)) continue;
                        for (int i = 0; i < references.Count; i++)
                        {
                            SegmentReference other = references[i];
                            if (!compared.Add(other)) continue;
                            if (!InvalidIntersection(paths, pathIndex, segmentIndex, other.PathIndex, other.SegmentIndex))
                                continue;
                            conflicts.Add(pathIndex);
                            conflicts.Add(other.PathIndex);
                        }
                    }
                }

                var reference = new SegmentReference(pathIndex, segmentIndex);
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        var bucket = new BucketKey(x, y);
                        if (!buckets.TryGetValue(bucket, out List<SegmentReference> references))
                        {
                            references = new List<SegmentReference>();
                            buckets.Add(bucket, references);
                        }
                        references.Add(reference);
                    }
                }
            }
        }
        return conflicts;
    }

    private static bool InvalidIntersection(
        Vector2[][] paths,
        int leftPathIndex,
        int leftSegmentIndex,
        int rightPathIndex,
        int rightSegmentIndex)
    {
        Vector2[] leftPath = paths[leftPathIndex];
        Vector2[] rightPath = paths[rightPathIndex];
        if (leftPathIndex == rightPathIndex)
        {
            if (Math.Abs(leftSegmentIndex - rightSegmentIndex) == 1) return false;
            bool closed = SamePoint(leftPath[0], leftPath[^1]);
            if (closed && (leftSegmentIndex == 0 && rightSegmentIndex == leftPath.Length - 2 ||
                           rightSegmentIndex == 0 && leftSegmentIndex == leftPath.Length - 2)) return false;
        }

        Vector2 leftFrom = leftPath[leftSegmentIndex];
        Vector2 leftTo = leftPath[leftSegmentIndex + 1];
        Vector2 rightFrom = rightPath[rightSegmentIndex];
        Vector2 rightTo = rightPath[rightSegmentIndex + 1];
        if (!SegmentsIntersect(leftFrom, leftTo, rightFrom, rightTo)) return false;
        bool sharedEndpoint = SamePoint(leftFrom, rightFrom) || SamePoint(leftFrom, rightTo) ||
                              SamePoint(leftTo, rightFrom) || SamePoint(leftTo, rightTo);
        return !sharedEndpoint || CollinearOverlap(leftFrom, leftTo, rightFrom, rightTo);
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float abC = Cross(a, b, c);
        float abD = Cross(a, b, d);
        float cdA = Cross(c, d, a);
        float cdB = Cross(c, d, b);
        if ((abC > Epsilon && abD < -Epsilon || abC < -Epsilon && abD > Epsilon) &&
            (cdA > Epsilon && cdB < -Epsilon || cdA < -Epsilon && cdB > Epsilon)) return true;
        return Math.Abs(abC) <= Epsilon && OnSegment(a, b, c) ||
               Math.Abs(abD) <= Epsilon && OnSegment(a, b, d) ||
               Math.Abs(cdA) <= Epsilon && OnSegment(c, d, a) ||
               Math.Abs(cdB) <= Epsilon && OnSegment(c, d, b);
    }

    private static bool CollinearOverlap(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (Math.Abs(Cross(a, b, c)) > Epsilon || Math.Abs(Cross(a, b, d)) > Epsilon) return false;
        bool useX = Math.Abs(b.x - a.x) >= Math.Abs(b.y - a.y);
        float a0 = useX ? a.x : a.y;
        float a1 = useX ? b.x : b.y;
        float c0 = useX ? c.x : c.y;
        float c1 = useX ? d.x : d.y;
        float overlap = Math.Min(Math.Max(a0, a1), Math.Max(c0, c1)) -
                        Math.Max(Math.Min(a0, a1), Math.Min(c0, c1));
        return overlap > Epsilon;
    }

    private static bool OnSegment(Vector2 from, Vector2 to, Vector2 point)
    {
        return point.x >= Math.Min(from.x, to.x) - Epsilon && point.x <= Math.Max(from.x, to.x) + Epsilon &&
               point.y >= Math.Min(from.y, to.y) - Epsilon && point.y <= Math.Max(from.y, to.y) + Epsilon;
    }

    private static KingdomMapContour[] BuildContours(KingdomBoundaryPath[] paths)
    {
        var result = new List<KingdomMapContour>();
        var arcsByOwner = new Dictionary<long, List<DirectedArc>>();
        for (int i = 0; i < paths.Length; i++)
        {
            KingdomBoundaryPath path = paths[i];
            if (path.Cycle)
            {
                if (path.LeftOwner != 0) AddContour(result, path.Points, path.LeftOwner, false);
                if (path.RightOwner != 0) AddContour(result, path.Points, path.RightOwner, true);
                continue;
            }
            if (path.LeftOwner != 0) AddArc(arcsByOwner, path.LeftOwner, path.Points, false);
            if (path.RightOwner != 0) AddArc(arcsByOwner, path.RightOwner, path.Points, true);
        }

        foreach (KeyValuePair<long, List<DirectedArc>> pair in arcsByOwner)
        {
            BuildOwnerContours(pair.Key, pair.Value, result);
        }
        return result.ToArray();
    }

    private static void AddArc(
        Dictionary<long, List<DirectedArc>> arcsByOwner,
        long owner,
        Vector2[] points,
        bool reverse)
    {
        if (!arcsByOwner.TryGetValue(owner, out List<DirectedArc> arcs))
        {
            arcs = new List<DirectedArc>();
            arcsByOwner.Add(owner, arcs);
        }
        arcs.Add(new DirectedArc(reverse ? Reverse(points) : points));
    }

    private static void BuildOwnerContours(long owner, List<DirectedArc> arcs, List<KingdomMapContour> contours)
    {
        var outgoing = new Dictionary<EndpointKey, List<int>>();
        for (int i = 0; i < arcs.Count; i++)
        {
            EndpointKey key = EndpointKey.From(arcs[i].Points[0]);
            if (!outgoing.TryGetValue(key, out List<int> indices))
            {
                indices = new List<int>();
                outgoing.Add(key, indices);
            }
            indices.Add(i);
        }

        var used = new bool[arcs.Count];
        for (int startArcIndex = 0; startArcIndex < arcs.Count; startArcIndex++)
        {
            if (used[startArcIndex]) continue;
            var points = new List<Vector2>();
            int arcIndex = startArcIndex;
            EndpointKey start = EndpointKey.From(arcs[arcIndex].Points[0]);
            bool closed = false;
            while (true)
            {
                DirectedArc arc = arcs[arcIndex];
                used[arcIndex] = true;
                for (int pointIndex = points.Count == 0 ? 0 : 1; pointIndex < arc.Points.Length; pointIndex++)
                    AppendDistinct(points, arc.Points[pointIndex]);
                EndpointKey end = EndpointKey.From(arc.Points[^1]);
                if (end == start)
                {
                    closed = true;
                    break;
                }
                if (!outgoing.TryGetValue(end, out List<int> candidates)) break;
                int nextArc = SelectLeftmostArc(points, arcs, candidates, used);
                if (nextArc < 0) break;
                arcIndex = nextArc;
            }
            if (closed) AddContour(result: contours, points.ToArray(), owner, false);
        }
    }

    private static int SelectLeftmostArc(
        List<Vector2> points,
        List<DirectedArc> arcs,
        List<int> candidates,
        bool[] used)
    {
        Vector2 incoming = Normalize(points[^1] - points[^2]);
        int selected = -1;
        double selectedTurn = double.NegativeInfinity;
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidateIndex = candidates[i];
            if (used[candidateIndex]) continue;
            Vector2[] candidate = arcs[candidateIndex].Points;
            Vector2 outgoing = Normalize(candidate[1] - candidate[0]);
            double turn = Math.Atan2(
                incoming.x * outgoing.y - incoming.y * outgoing.x,
                incoming.x * outgoing.x + incoming.y * outgoing.y);
            if (turn <= selectedTurn) continue;
            selected = candidateIndex;
            selectedTurn = turn;
        }
        return selected;
    }

    private static void AddContour(
        List<KingdomMapContour> result,
        Vector2[] points,
        long owner,
        bool reverse)
    {
        Vector2[] oriented = reverse ? Reverse(points) : points;
        oriented = RemoveDuplicatePoints(oriented);
        int count = oriented.Length > 1 && SamePoint(oriented[0], oriented[^1])
            ? oriented.Length - 1
            : oriented.Length;
        if (count < 3) return;
        var contour = new Vector2[count];
        Array.Copy(oriented, contour, count);
        if (Math.Abs(SignedArea(contour)) <= Epsilon) return;
        result.Add(new KingdomMapContour(contour, owner));
    }

    private static Vector2[] Reverse(Vector2[] points)
    {
        var result = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++) result[i] = points[points.Length - 1 - i];
        return result;
    }

    private static Vector2[] RemoveDuplicatePoints(Vector2[] points)
    {
        var result = new List<Vector2>(points.Length);
        for (int i = 0; i < points.Length; i++) AppendDistinct(result, points[i]);
        return result.ToArray();
    }

    private static void AppendDistinct(List<Vector2> points, Vector2 point)
    {
        if (points.Count == 0 || !SamePoint(points[^1], point)) points.Add(point);
    }

    private static float DistanceToSegmentSquared(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float length = segment.sqrMagnitude;
        if (length < Epsilon) return (point - from).sqrMagnitude;
        float amount = Math.Max(0f, Math.Min(1f, Vector2.Dot(point - from, segment) / length));
        return (point - (from + segment * amount)).sqrMagnitude;
    }

    private static float SignedArea(IReadOnlyList<Vector2> points)
    {
        int count = points.Count;
        if (count > 1 && SamePoint(points[0], points[count - 1])) count--;
        double area = 0;
        for (int i = 0; i < count; i++)
        {
            Vector2 current = points[i];
            Vector2 next = points[(i + 1) % count];
            area += current.x * next.y - next.x * current.y;
        }
        return (float)(area * 0.5);
    }

    private static float Cross(Vector2 from, Vector2 to, Vector2 point)
    {
        return (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);
    }

    private static float Distance(Vector2 left, Vector2 right)
    {
        return (right - left).magnitude;
    }

    private static Vector2 Normalize(Vector2 value)
    {
        float length = value.magnitude;
        return length < Epsilon ? Vector2.right : value / length;
    }

    private static bool SamePoint(Vector2 left, Vector2 right)
    {
        return Math.Abs(left.x - right.x) < Epsilon && Math.Abs(left.y - right.y) < Epsilon;
    }

    private static int Compare(Vector2 left, Vector2 right)
    {
        int x = left.x.CompareTo(right.x);
        return x != 0 ? x : left.y.CompareTo(right.y);
    }

    private readonly struct GridPoint : IEquatable<GridPoint>, IComparable<GridPoint>
    {
        internal readonly int X;
        internal readonly int Y;

        internal GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        internal Vector2 ToVector2() => new(X, Y);
        public int CompareTo(GridPoint other)
        {
            int x = X.CompareTo(other.X);
            return x != 0 ? x : Y.CompareTo(other.Y);
        }
        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public static bool operator ==(GridPoint left, GridPoint right) => left.Equals(right);
        public static bool operator !=(GridPoint left, GridPoint right) => !left.Equals(right);
    }

    private readonly struct OwnerPair : IEquatable<OwnerPair>
    {
        private readonly long first;
        private readonly long second;

        internal OwnerPair(long left, long right)
        {
            first = Math.Min(left, right);
            second = Math.Max(left, right);
        }

        public bool Equals(OwnerPair other) => first == other.first && second == other.second;
        public override bool Equals(object obj) => obj is OwnerPair other && Equals(other);
        public override int GetHashCode() => unchecked(first.GetHashCode() * 397 ^ second.GetHashCode());
        public static bool operator ==(OwnerPair left, OwnerPair right) => left.Equals(right);
        public static bool operator !=(OwnerPair left, OwnerPair right) => !left.Equals(right);
    }

    private readonly struct RawEdge
    {
        internal readonly GridPoint From;
        internal readonly GridPoint To;
        internal readonly long LeftOwner;
        internal readonly long RightOwner;
        internal readonly OwnerPair Pair;

        internal RawEdge(GridPoint from, GridPoint to, long leftOwner, long rightOwner)
        {
            From = from;
            To = to;
            LeftOwner = leftOwner;
            RightOwner = rightOwner;
            Pair = new OwnerPair(leftOwner, rightOwner);
        }
    }

    private readonly struct RawChain
    {
        internal readonly Vector2[] Points;
        internal readonly long LeftOwner;
        internal readonly long RightOwner;
        internal readonly bool Cycle;

        internal RawChain(Vector2[] points, long leftOwner, long rightOwner, bool cycle)
        {
            Points = points;
            LeftOwner = leftOwner;
            RightOwner = rightOwner;
            Cycle = cycle;
        }
    }

    private readonly struct PathVariants
    {
        private readonly Vector2[] rounded;
        private readonly Vector2[] simplified;
        private readonly Vector2[] raw;

        internal PathVariants(Vector2[] rounded, Vector2[] simplified, Vector2[] raw)
        {
            this.rounded = rounded;
            this.simplified = simplified;
            this.raw = raw;
        }

        internal Vector2[] Get(int level) => level <= 0 ? rounded : level == 1 ? simplified : raw;
    }

    private readonly struct BucketKey : IEquatable<BucketKey>
    {
        private readonly int x;
        private readonly int y;

        internal BucketKey(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(BucketKey other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is BucketKey other && Equals(other);
        public override int GetHashCode() => unchecked(x * 397 ^ y);
    }

    private readonly struct SegmentReference : IEquatable<SegmentReference>
    {
        internal readonly int PathIndex;
        internal readonly int SegmentIndex;

        internal SegmentReference(int pathIndex, int segmentIndex)
        {
            PathIndex = pathIndex;
            SegmentIndex = segmentIndex;
        }

        public bool Equals(SegmentReference other) => PathIndex == other.PathIndex && SegmentIndex == other.SegmentIndex;
        public override bool Equals(object obj) => obj is SegmentReference other && Equals(other);
        public override int GetHashCode() => unchecked(PathIndex * 397 ^ SegmentIndex);
    }

    private readonly struct EndpointKey : IEquatable<EndpointKey>
    {
        private const int Scale = 1024;
        private readonly int x;
        private readonly int y;

        private EndpointKey(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        internal static EndpointKey From(Vector2 point)
        {
            return new EndpointKey((int)Math.Round(point.x * Scale), (int)Math.Round(point.y * Scale));
        }

        public bool Equals(EndpointKey other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is EndpointKey other && Equals(other);
        public override int GetHashCode() => unchecked(x * 397 ^ y);
        public static bool operator ==(EndpointKey left, EndpointKey right) => left.Equals(right);
        public static bool operator !=(EndpointKey left, EndpointKey right) => !left.Equals(right);
    }

    private sealed class DirectedArc
    {
        internal readonly Vector2[] Points;

        internal DirectedArc(Vector2[] points)
        {
            Points = points;
        }
    }
}

using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 为持续空间攻击选择下一目标，并收集法器本体一帧位移扫过的敌人。
/// </summary>
internal sealed class ArtifactSpatialTargeting
{
    private const float ChunkSize = 16f;
    private const float ScanPadding = 3f;

    private readonly List<BaseSimObject> _candidates = new();
    private readonly HashSet<long> _candidateKeys = new();
    private readonly HashSet<long> _scanKeys = new();

    public bool TrySelect(
        Actor owner,
        Vector2 artifactPosition,
        ref ArtifactSpatialAttackMotion motion,
        float worldTime,
        out BaseSimObject target)
    {
        CollectCandidates(owner, motion.control_range);
        target = null;
        if (_candidates.Count == 0) return false;

        bool hasFreshTarget = false;
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (motion.hit_target_keys.Contains(GetTargetKey(_candidates[i]))) continue;
            hasFreshTarget = true;
            break;
        }

        bool avoidLastTarget = false;
        if (!hasFreshTarget)
        {
            if (worldTime < motion.repeat_ready_at) return false;
            motion.hit_target_keys.Clear();
            avoidLastTarget = motion.has_last_target && _candidates.Count > 1;
        }

        target = FindBestCandidate(artifactPosition, motion.direction, motion, avoidLastTarget);
        if (target != null) return true;

        target = FindBestCandidate(artifactPosition, motion.direction, motion, false);
        return target != null;
    }

    public void CollectSweptHits(
        Actor owner,
        Vector2 start,
        Vector2 end,
        ref ArtifactSpatialAttackMotion motion,
        List<BaseSimObject> hits)
    {
        hits.Clear();
        _scanKeys.Clear();

        float padding = motion.hit_radius + ScanPadding;
        Vector2 minimum = Vector2.Min(start, end) - Vector2.one * padding;
        Vector2 maximum = Vector2.Max(start, end) + Vector2.one * padding;
        Vector2Int lower = Vector2Int.FloorToInt(minimum);
        Vector2Int upper = Vector2Int.CeilToInt(maximum);
        lower.Clamp(Vector2Int.zero, new Vector2Int(MapBox.width - 1, MapBox.height - 1));
        upper.Clamp(Vector2Int.zero, new Vector2Int(MapBox.width - 1, MapBox.height - 1));

        for (int x = lower.x; x <= upper.x; x++)
        for (int y = lower.y; y <= upper.y; y++)
        {
            WorldTile tile = World.world.GetTileSimple(x, y);
            if (tile.building != null)
            {
                TryCollectHit(owner, tile.building, start, end, ref motion, hits);
            }
            for (int i = 0; i < tile._units.Count; i++)
            {
                TryCollectHit(owner, tile._units[i], start, end, ref motion, hits);
            }
        }
    }

    public static BaseSimObject ResolveTarget(ArtifactSpatialAttackMotion motion)
    {
        return motion.target_is_actor
            ? World.world.units.get(motion.target_id)
            : World.world.buildings.get(motion.target_id);
    }

    public static bool IsValidTarget(Actor owner, BaseSimObject target, float controlRange)
    {
        if (target.isRekt() || target == owner || !owner.canAttackTarget(target)) return false;

        float range = controlRange + target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(owner.current_position, target.current_position) <= range * range;
    }

    public static long GetTargetKey(BaseSimObject target)
    {
        return unchecked((target.getID() << 1) | (target.isActor() ? 0L : 1L));
    }

    private void CollectCandidates(Actor owner, float controlRange)
    {
        _candidates.Clear();
        _candidateKeys.Clear();

        int chunkRadius = Mathf.CeilToInt(controlRange / ChunkSize) + 1;
        foreach (Actor actor in Finder.getUnitsFromChunk(
                     owner.current_tile,
                     chunkRadius,
                     controlRange + 1f))
        {
            AddCandidate(owner, actor, controlRange);
        }
        foreach (Building building in Finder.getBuildingsFromChunk(
                     owner.current_tile,
                     chunkRadius,
                     Mathf.CeilToInt(controlRange + 1f)))
        {
            AddCandidate(owner, building, controlRange);
        }
    }

    private void AddCandidate(Actor owner, BaseSimObject candidate, float controlRange)
    {
        if (!IsValidTarget(owner, candidate, controlRange)) return;
        if (!_candidateKeys.Add(GetTargetKey(candidate))) return;
        _candidates.Add(candidate);
    }

    private BaseSimObject FindBestCandidate(
        Vector2 artifactPosition,
        Vector2 direction,
        ArtifactSpatialAttackMotion motion,
        bool avoidLastTarget)
    {
        BaseSimObject best = null;
        float bestScore = float.MaxValue;
        long bestKey = long.MaxValue;
        Vector2 currentDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;

        for (int i = 0; i < _candidates.Count; i++)
        {
            BaseSimObject candidate = _candidates[i];
            long key = GetTargetKey(candidate);
            if (motion.hit_target_keys.Contains(key)) continue;
            if (avoidLastTarget && key == motion.last_target_key) continue;

            Vector2 offset = candidate.current_position - artifactPosition;
            float distanceSquared = offset.sqrMagnitude;
            float alignment = distanceSquared > 0.0001f
                ? Vector2.Dot(currentDirection, offset.normalized)
                : 1f;
            float score = distanceSquared + (1f - alignment) * motion.control_range * 1.5f;
            if (score > bestScore || (Mathf.Approximately(score, bestScore) && key >= bestKey)) continue;

            best = candidate;
            bestScore = score;
            bestKey = key;
        }
        return best;
    }

    private void TryCollectHit(
        Actor owner,
        BaseSimObject candidate,
        Vector2 start,
        Vector2 end,
        ref ArtifactSpatialAttackMotion motion,
        List<BaseSimObject> hits)
    {
        if (!IsValidTarget(owner, candidate, motion.control_range)) return;

        long key = GetTargetKey(candidate);
        if (motion.hit_target_keys.Contains(key) || !_scanKeys.Add(key)) return;

        float radius = Mathf.Max(0.35f, motion.hit_radius + candidate.stats[S.size]);
        if (!ArtifactSpatialMotionTools.SegmentIntersectsCircle(
                start,
                end,
                candidate.current_position,
                radius)) return;

        motion.hit_target_keys.Add(key);
        hits.Add(candidate);
    }
}

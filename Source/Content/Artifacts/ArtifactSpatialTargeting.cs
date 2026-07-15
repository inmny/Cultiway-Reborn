using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 为持续空间攻击选择下一目标。实际扫掠命中由 SkillV3 的通用碰撞系统处理。
/// </summary>
internal sealed class ArtifactSpatialTargeting
{
    private const float ChunkSize = 16f;

    private readonly List<BaseSimObject> _candidates = new();
    private readonly HashSet<long> _candidateKeys = new();

    public bool TrySelect(
        Actor owner,
        Vector2 artifactPosition,
        ref ArtifactSpatialAttackMotion motion,
        float worldTime,
        Kingdom attackKingdom,
        out BaseSimObject target)
    {
        CollectCandidates(owner, motion.control_range, attackKingdom);
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

    public static bool IsValidTarget(
        Actor owner,
        BaseSimObject target,
        float controlRange,
        Kingdom attackKingdom = null)
    {
        if (target == null || target.isRekt() || target == owner) return false;
        if (attackKingdom != null)
        {
            if (!attackKingdom.isEnemy(target.kingdom)) return false;
        }
        else if (!owner.canAttackTarget(target))
        {
            return false;
        }

        float range = controlRange + target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(owner.current_position, target.current_position) <= range * range;
    }

    public static long GetTargetKey(BaseSimObject target)
    {
        return unchecked((target.getID() << 1) | (target.isActor() ? 0L : 1L));
    }

    private void CollectCandidates(Actor owner, float controlRange, Kingdom attackKingdom)
    {
        _candidates.Clear();
        _candidateKeys.Clear();

        int chunkRadius = Mathf.CeilToInt(controlRange / ChunkSize) + 1;
        foreach (Actor actor in Finder.getUnitsFromChunk(
                     owner.current_tile,
                     chunkRadius,
                     controlRange + 1f))
        {
            AddCandidate(owner, actor, controlRange, attackKingdom);
        }
        foreach (Building building in Finder.getBuildingsFromChunk(
                     owner.current_tile,
                     chunkRadius,
                     Mathf.CeilToInt(controlRange + 1f)))
        {
            AddCandidate(owner, building, controlRange, attackKingdom);
        }
    }

    private void AddCandidate(
        Actor owner,
        BaseSimObject candidate,
        float controlRange,
        Kingdom attackKingdom)
    {
        if (!IsValidTarget(owner, candidate, controlRange, attackKingdom)) return;
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
}

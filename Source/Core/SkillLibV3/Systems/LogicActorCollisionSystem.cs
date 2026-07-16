using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicActorCollisionSystem : QuerySystem<SkillContext, SkillEntity, ColliderSphere, ColliderConfig, Position>
{
    private readonly List<PendingHit> _pendingHits = new();
    private readonly HashSet<CollisionPair> _framePairs = new();
    private readonly HashSet<int> _stoppedSkillIds = new();

    public LogicActorCollisionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
    }

    protected override void OnUpdate()
    {
        _pendingHits.Clear();
        _framePairs.Clear();
        var world_min = new Vector2Int(0,            0);
        var world_max = new Vector2Int(MapBox.width-1, MapBox.height-1);
        Query.ForEachEntity(((ref SkillContext context, ref SkillEntity skill_entity, ref ColliderSphere collider,
            ref ColliderConfig config, ref Position pos, Entity entity) =>
        {
            if (entity.TryGetComponent(out SkillExecution execution) && execution.end_requested) return;
            if (!config.Enabled || (!config.Actor && !config.Building)) return;
            if (entity.TryGetComponent(out CollisionHeightGate heightGate) && pos.z > heightGate.MaxHeight) return;

            var radius = SkillEffectRadius.Resolve(entity, collider.Radius);
            var curr = pos.v2;

            // 扫掠碰撞：若实体带 PrevPosition，用上一帧→本帧的线段做扫描，防止高速漏检
            var hasPrev = entity.TryGetComponent(out PrevPosition prevPos);
            var prev = hasPrev ? prevPos.Value : curr;
            var delta = curr - prev;
            var sweepStart = prev;
            var sweepEnd = curr;
            if (entity.TryGetComponent(out ColliderLinearExtent linearExtent) && delta.sqrMagnitude > 0.0001f)
            {
                Vector2 direction = delta.normalized;
                sweepStart -= direction * Mathf.Max(0f, linearExtent.Backward);
                sweepEnd += direction * Mathf.Max(0f, linearExtent.Forward);
            }

            // 扫描 AABB：覆盖本帧完整扫掠胶囊体两侧的 radius 范围。
            const float targetScanPadding = 3f;
            var scan_min = Vector2.Min(sweepStart, sweepEnd) - (radius + targetScanPadding) * Vector2.one;
            var scan_max = Vector2.Max(sweepStart, sweepEnd) + (radius + targetScanPadding) * Vector2.one;

            Vector2Int lb_fixed = Vector2Int.FloorToInt(scan_min);
            Vector2Int rt_fixed = Vector2Int.CeilToInt(scan_max);

            lb_fixed.Clamp(world_min, world_max);
            rt_fixed.Clamp(world_min, world_max);

            var caster_kingdom = context.AttackKingdom ?? context.SourceObj?.kingdom;

            for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
            for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
            {
                WorldTile tile = World.world.GetTileSimple(x, y);
                if (config.Building && tile.building != null)
                {
                    TryQueueHit(entity, ref context, ref config, sweepStart, sweepEnd, radius, caster_kingdom,
                        tile.building);
                }

                if (!config.Actor) continue;
                for (var i = 0; i < tile._units.Count; i++)
                {
                    TryQueueHit(entity, ref context, ref config, sweepStart, sweepEnd, radius, caster_kingdom,
                        tile._units[i]);
                }
            }
        }));

        ResolvePendingHits();
    }

    private void TryQueueHit(
        Entity skillEntity,
        ref SkillContext context,
        ref ColliderConfig config,
        Vector2 start,
        Vector2 end,
        float colliderRadius,
        Kingdom casterKingdom,
        BaseSimObject target)
    {
        if (target == null || target.isRekt()) return;

        bool explicitTarget = context.TargetObj == target;
        if (!explicitTarget)
        {
            bool enemy = (casterKingdom?.isEnemy(target.kingdom) ?? true) && target != context.SourceObj;
            if ((!enemy || !config.Enemy) && (enemy || !config.Alias)) return;
        }

        var pair = new CollisionPair(skillEntity.Id, GetTargetKey(target));
        if (!_framePairs.Add(pair)) return;

        // 至少保留旧系统的一格目标容差，再为大型目标扩大真实命中半径。
        float targetRadius = colliderRadius + Mathf.Max(1f, target.stats[strings.S.size]);
        if (!SegmentIntersectsCircle(start, end, target.current_position, targetRadius)) return;
        _pendingHits.Add(new PendingHit(skillEntity, target));
    }

    /// <summary>
    /// 在 ECS 查询结束后执行命中回调，避免其触发的原版逻辑在查询锁内修改实体结构。
    /// </summary>
    private void ResolvePendingHits()
    {
        _stoppedSkillIds.Clear();
        for (int i = 0; i < _pendingHits.Count; i++)
        {
            PendingHit pending = _pendingHits[i];
            Entity entity = pending.SkillEntity;
            if (entity.IsNull || _stoppedSkillIds.Contains(entity.Id)) continue;
            if (entity.TryGetComponent(out SkillExecution execution) && execution.end_requested) continue;

            BaseSimObject target = pending.Target;
            if (target == null || target.isRekt()) continue;

            if (entity.HasComponent<SkillHitMemory>())
            {
                ref SkillHitMemory hitMemory = ref entity.GetComponent<SkillHitMemory>();
                if (!hitMemory.TargetIds.Add(GetTargetKey(target))) continue;
            }

            ref SkillContext context = ref entity.GetComponent<SkillContext>();
            ref SkillEntity skillEntity = ref entity.GetComponent<SkillEntity>();
            if (!skillEntity.Asset.OnObjCollision(ref context, skillEntity.SkillContainer, entity, target))
            {
                _stoppedSkillIds.Add(entity.Id);
            }
        }
    }

    private static long GetTargetKey(BaseSimObject target)
    {
        return unchecked((target.getID() << 1) | (target.isActor() ? 0L : 1L));
    }

    private static bool SegmentIntersectsCircle(Vector2 start, Vector2 end, Vector2 center, float radius)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        float t = lengthSquared > 0.0001f
            ? Mathf.Clamp01(Vector2.Dot(center - start, segment) / lengthSquared)
            : 0f;
        Vector2 closest = start + segment * t;
        return (center - closest).sqrMagnitude <= radius * radius;
    }

    private readonly struct PendingHit(Entity skillEntity, BaseSimObject target)
    {
        public Entity SkillEntity { get; } = skillEntity;
        public BaseSimObject Target { get; } = target;
    }

    private readonly struct CollisionPair : System.IEquatable<CollisionPair>
    {
        private readonly int _skillEntityId;
        private readonly long _targetKey;

        public CollisionPair(int skillEntityId, long targetKey)
        {
            _skillEntityId = skillEntityId;
            _targetKey = targetKey;
        }

        public bool Equals(CollisionPair other)
        {
            return _skillEntityId == other._skillEntityId && _targetKey == other._targetKey;
        }

        public override bool Equals(object obj)
        {
            return obj is CollisionPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_skillEntityId * 397) ^ _targetKey.GetHashCode();
            }
        }
    }
}

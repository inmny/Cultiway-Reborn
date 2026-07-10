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
    private readonly HashSet<int> _stoppedSkillIds = new();

    public LogicActorCollisionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive>());
    }

    protected override void OnUpdate()
    {
        _pendingHits.Clear();
        var world_min = new Vector2Int(0,            0);
        var world_max = new Vector2Int(MapBox.width-1, MapBox.height-1);
        Query.ForEachEntity(((ref SkillContext context, ref SkillEntity skill_entity, ref ColliderSphere collider,
            ref ColliderConfig config, ref Position pos, Entity entity) =>
        {
            if (!config.Enabled || !config.Actor) return;
            if (entity.TryGetComponent(out CollisionHeightGate heightGate) && pos.z > heightGate.MaxHeight) return;

            var radius = SkillEffectRadius.Resolve(entity, collider.Radius);
            var curr = pos.v2;

            // 扫掠碰撞：若实体带 PrevPosition，用上一帧→本帧的线段做扫描，防止高速漏检
            var hasPrev = entity.TryGetComponent(out PrevPosition prevPos);
            var prev = hasPrev ? prevPos.Value : curr;
            var delta = curr - prev;

            // 扫描 AABB：覆盖 prev→curr 线段两侧 radius 范围
            var scan_min = Vector2.Min(prev, curr) - radius * Vector2.one;
            var scan_max = Vector2.Max(prev, curr) + radius * Vector2.one;

            Vector2Int lb_fixed = Vector2Int.FloorToInt(scan_min);
            Vector2Int rt_fixed = Vector2Int.CeilToInt(scan_max);

            lb_fixed.Clamp(world_min, world_max);
            rt_fixed.Clamp(world_min, world_max);

            var caster_kingdom = context.AttackKingdom ?? context.SourceObj?.kingdom;

            var pos_x = pos.x;
            var pos_y = pos.y;

            // 位移极小时退化为终点圆检测，避免除零
            var useSegment = hasPrev && delta.sqrMagnitude > 0.0001f;
            var deltaSqrInv = useSegment ? 1f / delta.sqrMagnitude : 0f;
            var thresholdSqr = (radius + 1f) * (radius + 1f);

            for (var x = lb_fixed.x; x <= rt_fixed.x; x++)
            for (var y = lb_fixed.y; y <= rt_fixed.y; y++)
            {
                // 瓦片中心到线段（或终点）的距离过滤
                if (useSegment)
                {
                    // 点-线段最短距离的平方
                    var qx = (float)x - prev.x;
                    var qy = (float)y - prev.y;
                    var t = qx * delta.x + qy * delta.y;
                    t = deltaSqrInv * t;
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    var cx = prev.x + t * delta.x;
                    var cy = prev.y + t * delta.y;
                    var ddx = (float)x - cx;
                    var ddy = (float)y - cy;
                    if (ddx * ddx + ddy * ddy >= thresholdSqr) continue;
                }
                else
                {
                    if (((pos_x - x)*(pos_x-x) + (pos_y-y)*(pos_y-y)) >= thresholdSqr) continue;
                }

                WorldTile tile = World.world.GetTileSimple(x, y);
                for (var i = 0; i < tile._units.Count; i++)
                {
                    Actor obj = tile._units[i];

                    var explicitTarget = context.TargetObj == obj;
                    if (!explicitTarget)
                    {
                        var enemy = caster_kingdom?.isEnemy(obj.kingdom) ?? true;
                        if ((!enemy || !config.Enemy) && (enemy || !config.Alias)) continue;
                    }

                    _pendingHits.Add(new PendingHit(entity, obj));
                }
            }
        }));

        ResolvePendingHits();
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

            Actor target = pending.Target;
            if (target == null || target.isRekt()) continue;

            if (entity.HasComponent<SkillHitMemory>())
            {
                ref SkillHitMemory hitMemory = ref entity.GetComponent<SkillHitMemory>();
                if (!hitMemory.TargetIds.Add(target.getID())) continue;
            }

            ref SkillContext context = ref entity.GetComponent<SkillContext>();
            ref SkillEntity skillEntity = ref entity.GetComponent<SkillEntity>();
            if (!skillEntity.Asset.OnObjCollision(ref context, skillEntity.SkillContainer, entity, target))
            {
                _stoppedSkillIds.Add(entity.Id);
            }
        }
    }

    private readonly struct PendingHit(Entity skillEntity, Actor target)
    {
        public Entity SkillEntity { get; } = skillEntity;
        public Actor Target { get; } = target;
    }
}

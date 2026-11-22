using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Skills;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Skills;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Systems;

public class LogicActorCollisionSystem : QuerySystem<SkillContext, SkillEntity, ColliderSphere, ColliderConfig, Position>
{
    /// <summary>本轮待确认的命中，退出实体扫描后才调用原版。</summary>
    private readonly List<PendingHit> _pendingHits = new();
    /// <summary>同一建筑占多格时只确认一次。</summary>
    private readonly HashSet<CollisionPair> _framePairs = new();
    /// <summary>本轮已经被命中回调终止的技能。</summary>
    private readonly HashSet<int> _stoppedSkillIds = new();
    /// <summary>按人物实际横向覆盖登记，同轮多发技能共用。</summary>
    private readonly AbilityContactIndex _actorIndex = new();
    /// <summary>真实目标与姿态，和空间索引顺序一致。</summary>
    private readonly List<ActorTarget> _actorTargets = new();
    /// <summary>复用本次扫掠候选。</summary>
    private readonly List<int> _nearbyActors = new();
    /// <summary>只在本轮确有可命中人物的技能时采集。</summary>
    private bool _actorsCaptured;

    /// <summary>忽略没有运行或禁止碰撞的实例。</summary>
    public LogicActorCollisionSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle, TagSkillAnimationNoCollision>());
    }

    /// <summary>先筛选实际包围范围，再确认相交并执行原有回调。</summary>
    protected override void OnUpdate()
    {
        _pendingHits.Clear();
        _framePairs.Clear();
        _actorIndex.Clear();
        _actorTargets.Clear();
        _actorsCaptured = false;
        Query.ForEachEntity((ref SkillContext context, ref SkillEntity skill, ref ColliderSphere collider,
            ref ColliderConfig config, ref Position pos, Entity entity) =>
        {
            if (entity.TryGetComponent(out SkillExecution execution) && execution.end_requested) return;
            if (!config.Enabled || (!config.Actor && !config.Building)) return;
            if (entity.TryGetComponent(out MotionParams motion) && motion.HasHeightGate && pos.z > motion.HeightGateMax) return;

            bool movingBody = entity.TryGetComponent(out ColliderSweptBody body);
            float size = SkillEffectRadius.Resolve(entity, movingBody ? 1f : collider.Radius);
            AbilityCapsule before = movingBody ? body.Before(size) : FlightPath(entity, pos.value, size);
            AbilityCapsule after = movingBody ? body.After(size) : before;
            AbilityMeshSweep surface = body.Sweep();
            Bounds bounds = surface.IsActive ? surface.Bounds : AbilityContactIndex.Swept(before, after);

            int first = _pendingHits.Count;
            Kingdom kingdom = context.ResolveAttackKingdom();
            if (config.Actor) QueueActorHits(entity, ref context, ref config, before, after, movingBody, kingdom, body, surface, bounds);
            if (config.Building) QueueBuildingHits(entity, ref context, ref config, before, after, movingBody, kingdom, surface, bounds);
            // 原来按横格、纵格扫描，同格建筑在前；空间索引的内部顺序不应左右消耗结果。
            if (_pendingHits.Count - first > 1)
                _pendingHits.Sort(first, _pendingHits.Count - first, PendingHit.TileOrder);
        });

        ResolvePendingHits();
    }

    /// <summary>只有点位置的既有法术沿本轮中心路径确认接触。</summary>
    private static AbilityCapsule FlightPath(Entity entity, Vector3 position, float radius)
    {
        var start = entity.TryGetComponent(out PrevPosition previous)
            ? new Vector3(previous.Value.x, previous.Value.y, previous.Height) : position;
        var end = position;
        if (entity.TryGetComponent(out ColliderLinearExtent extent))
        {
            Vector3 direction = extent.UseEntityRotation || ((Vector2)(end - start)).sqrMagnitude <= .0001f
                ? entity.GetComponent<Rotation>().value : end - start;
            float horizontal = ((Vector2)direction).magnitude;
            direction = horizontal > .0001f ? direction / horizontal : Vector3.zero;
            if (extent.UseEntityRotation) start = end = position;
            start -= direction * Mathf.Max(0f, extent.Backward);
            end += direction * Mathf.Max(0f, extent.Forward);
        }
        return new AbilityCapsule(0, start, end, radius, null);
    }

    /// <summary>从真实世界人物计算平面包围范围，大目标按实际宽度跨格登记。</summary>
    private void CaptureActorTargets()
    {
        _actorsCaptured = true;
        foreach (Actor actor in World.world.units.dict.Values)
        {
            if (actor.isRekt() || !actor.isAlive() || !actor.hasHealth()) continue;
            var target = new ActorTarget(actor);
            _actorTargets.Add(target);
            var body = ActorHitBoundary.Create(target.Foot, target.Height, target.Torso);
            _actorIndex.Add(OnPlane(AbilityContactIndex.Swept(body, body)));
        }
    }

    /// <summary>仅筛选地图平面，准确高度用同一地表换算后再判定。</summary>
    /// <param name="bounds">本次有效形体范围。</param>
    private static Bounds OnPlane(Bounds bounds)
    {
        var center = bounds.center;
        var size = bounds.size;
        center.z = size.z = 0f;
        return new Bounds(center, size);
    }

    /// <summary>旧轨迹和实际身体先换算为共同世界高度，再比较有限轴段的真实距离。</summary>
    /// <param name="entity">本次技能实例。</param>
    /// <param name="context">来源和点名目标。</param>
    /// <param name="config">命中规则。</param>
    /// <param name="before">原相对高度的前一轮形状。</param>
    /// <param name="after">原相对高度的本轮形状。</param>
    /// <param name="movingBody">是否具有实际尺寸和运动的器物。</param>
    /// <param name="kingdom">本次攻击阵营。</param>
    /// <param name="moving">原始器物姿态，用于同一地表换算。</param>
    /// <param name="surface">器物真实表面。</param>
    /// <param name="bounds">本轮平面索引的筛选范围。</param>
    private void QueueActorHits(Entity entity, ref SkillContext context, ref ColliderConfig config,
        AbilityCapsule before, AbilityCapsule after, bool movingBody, Kingdom kingdom,
        in ColliderSweptBody moving, AbilityMeshSweep surface, Bounds bounds)
    {
        if (!_actorsCaptured) CaptureActorTargets();
        _actorIndex.Query(OnPlane(bounds), _nearbyActors);
        if (_nearbyActors.Count == 0) return;

        AbilityHeightField terrain = null;
        if (ConstructedAbilityService.GroundProvider != null)
        {
            Vector2 min = bounds.min, max = bounds.max;
            foreach (int index in _nearbyActors)
            {
                Vector2 foot = _actorTargets[index].Foot;
                min = Vector2.Min(min, foot); max = Vector2.Max(max, foot);
            }
            terrain = ConstructedAbilityService.GroundProvider(Rect.MinMaxRect(min.x - 1, min.y - 1, max.x + 1, max.y + 1));
            if (terrain != null)
            {
                if (surface.IsActive) surface = moving.Sweep(terrain);
                else
                {
                    before = AtTerrain(before, terrain);
                    after = movingBody ? AtTerrain(after, terrain) : before;
                }
            }
        }
        foreach (int index in _nearbyActors)
        {
            var target = _actorTargets[index];
            if (!CanHit(ref context, ref config, kingdom, target.Actor)) continue;
            var foot = target.Absolute ? target.Foot : ConstructedAbilityService.WorldFoot(target.Actor, target.Foot, terrain);
            var body = ActorHitBoundary.Create(foot, target.Height, target.Torso);
            bool touches = surface.IsActive ? surface.Touches(body, plane: false) : movingBody
                ? AbilityPairSweep.Find(before, after, body, body, out _, out _, out _) : Touches(before, body);
            if (touches) _pendingHits.Add(new PendingHit(entity, target.Actor));
        }
    }

    private static AbilityCapsule AtTerrain(in AbilityCapsule body, AbilityHeightField terrain)
    {
        Vector3 a = body.A, b = body.B;
        a.z += terrain.Sample(a.x, a.y);
        b.z += terrain.Sample(b.x, b.y);
        return new AbilityCapsule(0, a, b, body.Radius, null);
    }

    private static bool Touches(in AbilityCapsule path, in AbilityCapsule actor)
    {
        AbilityPairSweep.Closest(path.A, path.B, actor.A, actor.B, out var pathPoint, out var actorPoint);
        float limit = path.Radius + actor.Radius + .00001f;
        return (pathPoint - actorPoint).sqrMagnitude <= limit * limit;
    }

    /// <summary>建筑继续用自身的旧平面碰撞范围和格子登记。</summary>
    /// <param name="entity">本次技能实例。</param>
    /// <param name="context">来源和点名目标。</param>
    /// <param name="config">命中规则。</param>
    /// <param name="before">前一轮形状。</param>
    /// <param name="after">本轮形状。</param>
    /// <param name="movingBody">是否具有实际尺寸和运动的器物。</param>
    /// <param name="kingdom">本次攻击阵营。</param>
    /// <param name="surface">器物真实表面。</param>
    /// <param name="bounds">本轮平面索引的筛选范围。</param>
    private void QueueBuildingHits(Entity entity, ref SkillContext context, ref ColliderConfig config,
        AbilityCapsule before, AbilityCapsule after, bool movingBody, Kingdom kingdom,
        in AbilityMeshSweep surface, Bounds bounds)
    {
        const float buildingScanPadding = 3f;
        Vector2 padding = buildingScanPadding * Vector2.one;
        var lower = Vector2Int.FloorToInt((Vector2)bounds.min - padding);
        var upper = Vector2Int.CeilToInt((Vector2)bounds.max + padding);
        var limit = new Vector2Int(MapBox.width - 1, MapBox.height - 1);
        lower.Clamp(Vector2Int.zero, limit);
        upper.Clamp(Vector2Int.zero, limit);
        if (movingBody && !surface.IsActive)
        {
            before = new AbilityCapsule(0, (Vector2)before.A, (Vector2)before.B, before.Radius, null);
            after = new AbilityCapsule(0, (Vector2)after.A, (Vector2)after.B, after.Radius, null);
        }
        for (int x = lower.x; x <= upper.x; x++)
        for (int y = lower.y; y <= upper.y; y++)
        {
            Building building = World.world.GetTileSimple(x, y).building;
            if (!CanHit(ref context, ref config, kingdom, building) ||
                !_framePairs.Add(new CollisionPair(entity.Id, GetTargetKey(building)))) continue;
            float buildingSize = Mathf.Max(1f, building.stats[strings.S.size]);
            Vector3 floor = building.current_position;
            var buildingBody = new AbilityCapsule(0, floor, floor, buildingSize, null);
            bool touches = surface.IsActive ? surface.Touches(buildingBody, plane: true) : movingBody
                ? AbilityPairSweep.Find(before, after, buildingBody, buildingBody, out _, out _, out _)
                : SegmentIntersectsCircle(before.A, before.B, floor, before.Radius + buildingSize);
            if (touches) _pendingHits.Add(new PendingHit(entity, building));
        }
    }

    /// <summary>人物和建筑沿用原点名及敌友规则。</summary>
    /// <param name="context">已受理的施放资料。</param>
    /// <param name="config">该技能允许命中的种类及关系。</param>
    /// <param name="kingdom">实际攻击阵营。</param>
    /// <param name="target">候选目标。</param>
    private static bool CanHit(ref SkillContext context, ref ColliderConfig config, Kingdom kingdom, BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        if (context.TargetObj == target) return true;
        if (config.ExplicitTargetOnly) return false;
        bool enemy = (kingdom?.isEnemy(target.kingdom) ?? true) && target != context.SourceObj;
        return enemy ? config.Enemy : config.Alias;
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
                long targetKey = GetTargetKey(target);
                SkillImpactProfileAsset profile = entity.GetComponent<SkillEntity>().Asset.ImpactProfile;
                if (profile != null && profile.RepeatHitInterval > 0f)
                {
                    float elapsed = entity.GetComponent<AliveTimer>().value;
                    if (hitMemory.NextHitTimes.TryGetValue(targetKey, out float nextHit) && elapsed < nextHit) continue;
                    hitMemory.NextHitTimes[targetKey] = elapsed + profile.RepeatHitInterval;
                }
                else if (!hitMemory.TargetIds.Add(targetKey))
                {
                    continue;
                }
            }

            ref SkillContext context = ref entity.GetComponent<SkillContext>();
            ref SkillEntity skillEntity = ref entity.GetComponent<SkillEntity>();
            bool continueAfterHit =
                skillEntity.Asset.OnObjCollision(ref context, skillEntity.SkillContainer, entity, target);
            if (continueAfterHit)
            {
                MarkExplicitTargetPassed(entity, ref context, target);
            }
            else
            {
                _stoppedSkillIds.Add(entity.Id);
            }
        }
    }

    private static void MarkExplicitTargetPassed(Entity entity, ref SkillContext context, BaseSimObject target)
    {
        if (context.TargetObj != target || !entity.HasComponent<TrajectoryRuntimeState>()) return;

        ref TrajectoryRuntimeState state = ref entity.GetComponent<TrajectoryRuntimeState>();
        state.TargetPhase = TrajectoryTargetPhase.PassedTarget;
        state.TargetExitDirection = entity.GetComponent<Rotation>().value;
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

    /// <summary>本轮共用的实际人物位置和身体尺度。</summary>
    private readonly struct ActorTarget
    {
        /// <summary>关联实际游戏目标。</summary>
        internal readonly Actor Actor;
        /// <summary>脚下位置以及全身动作的整体移动量。</summary>
        internal readonly Vector3 Foot, Torso;
        /// <summary>真实体高。</summary>
        internal readonly float Height;
        /// <summary>全身动作已提供共同世界位置。</summary>
        internal readonly bool Absolute;

        /// <summary>采集原版人物或正式接管其站姿的动作。</summary>
        /// <param name="actor">实际人物。</param>
        internal ActorTarget(Actor actor)
        {
            Actor = actor;
            Height = SpatialActorPose.Height(actor);
            Absolute = ConstructedAbilityService.TryGetBodyPose(actor.id, BodyOccupancy.Stance, out var pose);
            Foot = Absolute ? pose.Foot : SpatialActorPose.Foot(actor);
            Torso = Absolute ? pose.Torso : Vector3.zero;
        }
    }

    /// <summary>在实体遍历完成后再提交的实际命中。</summary>
    /// <param name="skillEntity">已受理技能。</param>
    /// <param name="target">相交目标。</param>
    private readonly struct PendingHit(Entity skillEntity, BaseSimObject target)
    {
        /// <summary>命中来源技能。</summary>
        public Entity SkillEntity { get; } = skillEntity;
        /// <summary>相交的实际目标。</summary>
        public BaseSimObject Target { get; } = target;
        /// <summary>按原格子扫描次序归并人物和建筑；同格对象按稳定编号。</summary>
        internal static readonly IComparer<PendingHit> TileOrder = Comparer<PendingHit>.Create((a, b) =>
        {
            Vector2Int x = Vector2Int.FloorToInt(a.Target.current_position);
            Vector2Int y = Vector2Int.FloorToInt(b.Target.current_position);
            int order = x.x.CompareTo(y.x);
            if (order == 0) order = x.y.CompareTo(y.y);
            if (order == 0) order = a.Target.isActor().CompareTo(b.Target.isActor());
            return order == 0 ? a.Target.getID().CompareTo(b.Target.getID()) : order;
        });
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

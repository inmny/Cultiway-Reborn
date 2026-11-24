using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Skills;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>共用真实飞行物与人物、建筑的作用范围，收集阶段不修改世界。</summary>
internal sealed class WorldObjectContactSweep
{
    /// <summary>本轮实际人物和对应的空间索引。</summary>
    private readonly List<ActorTarget> actorTargets = new();
    private readonly AbilityContactIndex actorIndex = new();
    /// <summary>一段扫掠的实际候选，同一建筑只采集一次。</summary>
    private readonly List<int> nearbyActors = new();
    private readonly HashSet<long> buildings = new();
    private bool actorsCaptured;

    /// <summary>新的一轮在有实际扫掠时按需重新读取人物。</summary>
    internal void Begin()
    {
        actorIndex.Clear();
        actorTargets.Clear();
        actorsCaptured = false;
    }

    /// <summary>使用已经确认的飞行前后姿态，返回按原顺序排列的实际接触目标。</summary>
    /// <param name="entity">具有作用规则及实际姿态的飞行物。</param>
    /// <param name="targets">由调用者在收集后结算的目标列表。</param>
    internal void Collect(Entity entity, List<BaseSimObject> targets)
    {
        targets.Clear();
        ref ColliderConfig config = ref entity.GetComponent<ColliderConfig>();
        if (!config.Enabled || (!config.Actor && !config.Building)) return;
        ref SkillContext context = ref entity.GetComponent<SkillContext>();
        bool movingBody = entity.TryGetComponent(out ColliderSweptBody body);
        float radius = SkillEffectRadius.Resolve(entity, movingBody ? 1f : entity.GetComponent<ColliderSphere>().Radius);
        AbilityCapsule before = movingBody ? body.Before(radius) : FlightPath(entity, entity.GetComponent<Position>().value, radius);
        AbilityCapsule after = movingBody ? body.After(radius) : before;
        AbilityMeshSweep surface = body.Sweep();
        Bounds bounds = surface.IsActive ? surface.Bounds : AbilityContactIndex.Swept(before, after);
        Kingdom kingdom = context.ResolveAttackKingdom();
        if (config.Actor)
            QueueActorHits(ref context, ref config, before, after, movingBody, kingdom, body, surface, bounds, targets);
        if (config.Building)
        {
            buildings.Clear();
            QueueBuildingHits(ref context, ref config, before, after, movingBody, kingdom, surface, bounds, targets);
        }

        if (targets.Count > 1) targets.Sort(TileOrder);
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
        actorsCaptured = true;
        foreach (Actor actor in World.world.units.dict.Values)
        {
            if (actor.isRekt() || !actor.isAlive() || !actor.hasHealth()) continue;
            var target = new ActorTarget(actor);
            actorTargets.Add(target);
            var body = ActorHitBoundary.Create(target.Foot, target.Height, target.Torso);
            actorIndex.Add(OnPlane(AbilityContactIndex.Swept(body, body)));
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
    /// <param name="context">来源和点名目标。</param>
    /// <param name="config">命中规则。</param>
    /// <param name="before">原相对高度的前一轮形状。</param>
    /// <param name="after">原相对高度的本轮形状。</param>
    /// <param name="movingBody">是否具有实际尺寸和运动的器物。</param>
    /// <param name="kingdom">本次攻击阵营。</param>
    /// <param name="moving">原始器物姿态，用于同一地表换算。</param>
    /// <param name="surface">器物真实表面。</param>
    /// <param name="bounds">本轮平面索引的筛选范围。</param>
    /// <param name="targets">实际相交的对象。</param>
    private void QueueActorHits(ref SkillContext context, ref ColliderConfig config,
        AbilityCapsule before, AbilityCapsule after, bool movingBody, Kingdom kingdom,
        in ColliderSweptBody moving, AbilityMeshSweep surface, Bounds bounds, List<BaseSimObject> targets)
    {
        if (!actorsCaptured) CaptureActorTargets();
        actorIndex.Query(OnPlane(bounds), nearbyActors);
        if (nearbyActors.Count == 0) return;

        AbilityHeightField terrain = null;
        if (ConstructedAbilityService.GroundProvider != null)
        {
            Vector2 min = bounds.min, max = bounds.max;
            foreach (int index in nearbyActors)
            {
                Vector2 foot = actorTargets[index].Foot;
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
        foreach (int index in nearbyActors)
        {
            var target = actorTargets[index];
            if (!CanHit(ref context, ref config, kingdom, target.Actor)) continue;
            var foot = target.Absolute ? target.Foot : ConstructedAbilityService.WorldFoot(target.Actor, target.Foot, terrain);
            var body = ActorHitBoundary.Create(foot, target.Height, target.Torso);
            bool touches = surface.IsActive ? surface.Touches(body, plane: false) : movingBody
                ? AbilityPairSweep.Find(before, after, body, body, out _, out _, out _) : Touches(before, body);
            if (touches) targets.Add(target.Actor);
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
    /// <param name="context">来源和点名目标。</param>
    /// <param name="config">命中规则。</param>
    /// <param name="before">前一轮形状。</param>
    /// <param name="after">本轮形状。</param>
    /// <param name="movingBody">是否具有实际尺寸和运动的器物。</param>
    /// <param name="kingdom">本次攻击阵营。</param>
    /// <param name="surface">器物真实表面。</param>
    /// <param name="bounds">本轮平面索引的筛选范围。</param>
    /// <param name="targets">实际相交的对象。</param>
    private void QueueBuildingHits(ref SkillContext context, ref ColliderConfig config,
        AbilityCapsule before, AbilityCapsule after, bool movingBody, Kingdom kingdom,
        in AbilityMeshSweep surface, Bounds bounds, List<BaseSimObject> targets)
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
                !buildings.Add(building.getID())) continue;
            float buildingSize = Mathf.Max(1f, building.stats[strings.S.size]);
            Vector3 floor = building.current_position;
            var buildingBody = new AbilityCapsule(0, floor, floor, buildingSize, null);
            bool touches = surface.IsActive ? surface.Touches(buildingBody, plane: true) : movingBody
                ? AbilityPairSweep.Find(before, after, buildingBody, buildingBody, out _, out _, out _)
                : SegmentIntersectsCircle(before.A, before.B, floor, before.Radius + buildingSize);
            if (touches) targets.Add(building);
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

    /// <summary>按原格子次序归并对象，同格建筑在先。</summary>
    private static readonly IComparer<BaseSimObject> TileOrder = Comparer<BaseSimObject>.Create((a, b) =>
    {
        Vector2Int x = Vector2Int.FloorToInt(a.current_position);
        Vector2Int y = Vector2Int.FloorToInt(b.current_position);
        int order = x.x.CompareTo(y.x);
        if (order == 0) order = x.y.CompareTo(y.y);
        if (order == 0) order = a.isActor().CompareTo(b.isActor());
        return order == 0 ? a.getID().CompareTo(b.getID()) : order;
    });
}

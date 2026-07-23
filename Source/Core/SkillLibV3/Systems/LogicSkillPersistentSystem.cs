using System;
using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 维护场域/屏障数量约束，并处理墙与护盾对弹丸和地面单位的阻挡。
/// </summary>
public sealed class LogicSkillPersistentSystem : BaseSystem
{
    private readonly ArchetypeQuery<SkillPersistentState, SkillContext, Position, Rotation, SkillEntity>
        persistentQuery;
    private readonly ArchetypeQuery<SkillContext, Position, SkillEntity, Trajectory> skillQuery;
    private readonly List<PersistentSnapshot> persistent = new();
    private readonly Dictionary<PersistentKey, List<PersistentSnapshot>> groups = new();
    private readonly List<Entity> interceptedSkills = new();
    private readonly HashSet<Projectile> activeProjectiles = new();
    private readonly Dictionary<Projectile, Vector2> previousProjectilePositions = new();
    private readonly List<Projectile> staleProjectiles = new();
    private readonly Dictionary<long, Vector2> previousActorPositions = new();
    private readonly HashSet<long> activeActorIds = new();
    private readonly List<long> staleActorIds = new();

    public LogicSkillPersistentSystem(EntityStore world)
    {
        var filter = new QueryFilter();
        filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle, TagSkillAnimationNoCollision>());
        persistentQuery = world.Query<SkillPersistentState, SkillContext, Position, Rotation, SkillEntity>(filter);
        skillQuery = world.Query<SkillContext, Position, SkillEntity, Trajectory>(filter);
    }

    protected override void OnUpdateGroup()
    {
        CollectPersistentEntities();
        EnforceInstanceLimits();
        if (HasProjectileInterceptor())
        {
            InterceptSkillEntities();
            InterceptOriginalProjectiles();
        }
        else
        {
            previousProjectilePositions.Clear();
        }
        BlockGroundActors();
    }

    private bool HasProjectileInterceptor()
    {
        for (int i = 0; i < persistent.Count; i++)
        {
            PersistentSnapshot snapshot = persistent[i];
            if (snapshot.State.Kind != SkillPersistentKind.Field && IsBarrierOperational(snapshot.Entity))
            {
                return true;
            }
        }
        return false;
    }

    private void CollectPersistentEntities()
    {
        persistent.Clear();
        groups.Clear();
        persistentQuery.ForEachEntity((ref SkillPersistentState state, ref SkillContext context, ref Position position,
            ref Rotation rotation, ref SkillEntity skillEntity, Entity entity) =>
        {
            var snapshot = new PersistentSnapshot(
                entity,
                state,
                context.AttackKingdom ?? context.SourceObj.kingdom,
                position.v2,
                Normalize(rotation.value),
                entity.GetComponent<AliveTimer>().value);
            persistent.Add(snapshot);

            long sourceId = context.SourceObj.getID();
            var key = new PersistentKey(sourceId, skillEntity.SkillContainer.Id, state.Kind);
            if (!groups.TryGetValue(key, out List<PersistentSnapshot> entries))
            {
                entries = new List<PersistentSnapshot>();
                groups.Add(key, entries);
            }
            entries.Add(snapshot);
        });
    }

    private void EnforceInstanceLimits()
    {
        foreach (List<PersistentSnapshot> entries in groups.Values)
        {
            int maxInstances = entries[0].State.MaxInstances;
            if (maxInstances <= 0 || entries.Count <= maxInstances) continue;
            entries.Sort((left, right) => right.Elapsed.CompareTo(left.Elapsed));
            for (int i = 0; i < entries.Count - maxInstances; i++)
            {
                Entity entity = entries[i].Entity;
                if (!entity.Tags.Has<TagRecycle>()) entity.AddTag<TagRecycle>();
            }
        }
    }

    private void InterceptSkillEntities()
    {
        interceptedSkills.Clear();
        skillQuery.ForEachEntity((ref SkillContext incomingContext, ref Position incomingPosition,
            ref SkillEntity incomingSkill, ref Trajectory trajectory, Entity incomingEntity) =>
        {
            SkillTrajectoryDomain domains = trajectory.Asset.Domains;
            const SkillTrajectoryDomain interceptable = SkillTrajectoryDomain.FlyingBody |
                                                        SkillTrajectoryDomain.FlyingWave |
                                                        SkillTrajectoryDomain.Ballistic |
                                                        SkillTrajectoryDomain.Skyfall;
            if ((domains & interceptable) == SkillTrajectoryDomain.None) return;

            Kingdom incomingKingdom = incomingContext.AttackKingdom ?? incomingContext.SourceObj.kingdom;
            Vector2 end = incomingPosition.v2;
            Vector2 start = incomingEntity.TryGetComponent(out PrevPosition previous) ? previous.Value : end;
            for (int i = 0; i < persistent.Count; i++)
            {
                PersistentSnapshot barrier = persistent[i];
                if (barrier.State.Kind == SkillPersistentKind.Field ||
                    barrier.Entity == incomingEntity ||
                    !IsBarrierOperational(barrier.Entity) ||
                    !AreEnemies(barrier.Kingdom, incomingKingdom) ||
                    !Intersects(barrier, start, end)) continue;

                interceptedSkills.Add(incomingEntity);
                DamageBarrier(barrier.Entity, incomingContext.Strength);
                break;
            }
        });

        for (int i = 0; i < interceptedSkills.Count; i++)
        {
            Entity entity = interceptedSkills[i];
            if (!entity.IsNull && !entity.Tags.Has<TagRecycle>()) entity.AddTag<TagRecycle>();
        }
    }

    private void InterceptOriginalProjectiles()
    {
        activeProjectiles.Clear();
        List<Projectile> projectiles = World.world.projectiles.list;
        for (int projectileIndex = 0; projectileIndex < projectiles.Count; projectileIndex++)
        {
            Projectile projectile = projectiles[projectileIndex];
            if (!projectile.canBeCollided()) continue;
            activeProjectiles.Add(projectile);
            Vector2 current = projectile.getCurrentPosition();
            Vector2 previous = previousProjectilePositions.TryGetValue(projectile, out Vector2 recorded)
                ? recorded
                : current;
            bool intercepted = false;
            for (int barrierIndex = 0; barrierIndex < persistent.Count; barrierIndex++)
            {
                PersistentSnapshot barrier = persistent[barrierIndex];
                if (barrier.State.Kind == SkillPersistentKind.Field ||
                    !IsBarrierOperational(barrier.Entity) ||
                    !AreEnemies(barrier.Kingdom, projectile.kingdom) ||
                    !Intersects(barrier, previous, current)) continue;

                projectile.getCollided(barrier.Position);
                DamageBarrier(barrier.Entity, Mathf.Max(1f, projectile.asset.mass * 10f));
                intercepted = true;
                break;
            }
            previousProjectilePositions[projectile] = intercepted
                ? projectile.getCurrentPosition()
                : current;
        }

        staleProjectiles.Clear();
        foreach (Projectile projectile in previousProjectilePositions.Keys)
        {
            if (!activeProjectiles.Contains(projectile)) staleProjectiles.Add(projectile);
        }
        for (int i = 0; i < staleProjectiles.Count; i++)
        {
            previousProjectilePositions.Remove(staleProjectiles[i]);
        }
    }

    private void BlockGroundActors()
    {
        bool hasWall = false;
        for (int i = 0; i < persistent.Count; i++)
        {
            if (persistent[i].State.Kind == SkillPersistentKind.Barrier &&
                IsBarrierOperational(persistent[i].Entity))
            {
                hasWall = true;
                break;
            }
        }
        if (!hasWall)
        {
            previousActorPositions.Clear();
            return;
        }

        activeActorIds.Clear();
        List<Actor> actors = World.world.units.getSimpleList();
        for (int actorIndex = 0; actorIndex < actors.Count; actorIndex++)
        {
            Actor actor = actors[actorIndex];
            if (actor == null || actor.isRekt()) continue;
            long actorId = actor.getID();
            activeActorIds.Add(actorId);
            Vector2 current = actor.current_position;
            if (!previousActorPositions.TryGetValue(actorId, out Vector2 previous))
            {
                previousActorPositions[actorId] = current;
                continue;
            }

            if (!actor.isFlying())
            {
                for (int barrierIndex = 0; barrierIndex < persistent.Count; barrierIndex++)
                {
                    PersistentSnapshot barrier = persistent[barrierIndex];
                    if (barrier.State.Kind != SkillPersistentKind.Barrier ||
                        !IsBarrierOperational(barrier.Entity) ||
                        !AreEnemies(barrier.Kingdom, actor.kingdom) ||
                        !Intersects(barrier, previous, current)) continue;
                    actor.current_position = previous;
                    actor.dirty_current_tile = true;
                    current = previous;
                    break;
                }
            }
            previousActorPositions[actorId] = current;
        }

        staleActorIds.Clear();
        foreach (long actorId in previousActorPositions.Keys)
        {
            if (!activeActorIds.Contains(actorId)) staleActorIds.Add(actorId);
        }
        for (int i = 0; i < staleActorIds.Count; i++)
        {
            previousActorPositions.Remove(staleActorIds[i]);
        }
    }

    private static bool Intersects(PersistentSnapshot barrier, Vector2 start, Vector2 end)
    {
        if (barrier.State.Kind == SkillPersistentKind.Shield)
        {
            float radius = barrier.State.Length * 0.5f + barrier.State.Width;
            return SegmentIntersectsCircle(start, end, barrier.Position, radius);
        }

        Vector2 side = new(-barrier.Direction.y, barrier.Direction.x);
        Vector2 half = side * (barrier.State.Length * 0.5f);
        return SegmentDistanceSquared(start, end, barrier.Position - half, barrier.Position + half) <=
               barrier.State.Width * barrier.State.Width;
    }

    private static void DamageBarrier(Entity barrier, float damage)
    {
        if (barrier.IsNull || barrier.Tags.Has<TagRecycle>()) return;
        ref SkillPersistentState state = ref barrier.GetComponent<SkillPersistentState>();
        state.Durability -= Mathf.Max(0f, damage);
        if (state.Durability <= 0f)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(barrier.Id);
        }
    }

    private static bool IsBarrierOperational(Entity barrier)
    {
        return !barrier.IsNull &&
               !barrier.Tags.Has<TagRecycle>() &&
               barrier.GetComponent<SkillPersistentState>().Durability > 0f;
    }

    private static bool AreEnemies(Kingdom left, Kingdom right)
    {
        return left != null && right != null && left.isEnemy(right);
    }

    private static Vector2 Normalize(Vector3 direction)
    {
        var result = new Vector2(direction.x, direction.y);
        return result.sqrMagnitude > 0.0001f ? result.normalized : Vector2.right;
    }

    private static bool SegmentIntersectsCircle(Vector2 start, Vector2 end, Vector2 center, float radius)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        float t = lengthSquared > 0.0001f
            ? Mathf.Clamp01(Vector2.Dot(center - start, segment) / lengthSquared)
            : 0f;
        return (center - (start + segment * t)).sqrMagnitude <= radius * radius;
    }

    private static float SegmentDistanceSquared(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        if (SegmentsIntersect(a0, a1, b0, b1)) return 0f;
        return Mathf.Min(
            Mathf.Min(PointSegmentDistanceSquared(a0, b0, b1), PointSegmentDistanceSquared(a1, b0, b1)),
            Mathf.Min(PointSegmentDistanceSquared(b0, a0, a1), PointSegmentDistanceSquared(b1, a0, a1)));
    }

    private static bool SegmentsIntersect(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        const float epsilon = 0.0001f;
        float d1 = Cross(a1 - a0, b0 - a0);
        float d2 = Cross(a1 - a0, b1 - a0);
        float d3 = Cross(b1 - b0, a0 - b0);
        float d4 = Cross(b1 - b0, a1 - b0);
        if (Mathf.Abs(d1) <= epsilon && IsOnSegment(b0, a0, a1)) return true;
        if (Mathf.Abs(d2) <= epsilon && IsOnSegment(b1, a0, a1)) return true;
        if (Mathf.Abs(d3) <= epsilon && IsOnSegment(a0, b0, b1)) return true;
        if (Mathf.Abs(d4) <= epsilon && IsOnSegment(a1, b0, b1)) return true;
        return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
    }

    private static bool IsOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        const float epsilon = 0.0001f;
        return point.x >= Mathf.Min(start.x, end.x) - epsilon &&
               point.x <= Mathf.Max(start.x, end.x) + epsilon &&
               point.y >= Mathf.Min(start.y, end.y) - epsilon &&
               point.y <= Mathf.Max(start.y, end.y) + epsilon;
    }

    private static float PointSegmentDistanceSquared(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        float t = lengthSquared > 0.0001f
            ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared)
            : 0f;
        return (point - (start + segment * t)).sqrMagnitude;
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }

    private readonly struct PersistentKey : IEquatable<PersistentKey>
    {
        private readonly long sourceId;
        private readonly int containerId;
        private readonly SkillPersistentKind kind;

        public PersistentKey(long sourceId, int containerId, SkillPersistentKind kind)
        {
            this.sourceId = sourceId;
            this.containerId = containerId;
            this.kind = kind;
        }

        public bool Equals(PersistentKey other)
        {
            return sourceId == other.sourceId && containerId == other.containerId && kind == other.kind;
        }

        public override bool Equals(object obj)
        {
            return obj is PersistentKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((sourceId.GetHashCode() * 397) ^ containerId) * 397 ^ (int)kind;
            }
        }
    }

    private readonly struct PersistentSnapshot
    {
        public readonly Entity Entity;
        public readonly SkillPersistentState State;
        public readonly Kingdom Kingdom;
        public readonly Vector2 Position;
        public readonly Vector2 Direction;
        public readonly float Elapsed;

        public PersistentSnapshot(Entity entity, SkillPersistentState state, Kingdom kingdom,
            Vector2 position, Vector2 direction, float elapsed)
        {
            Entity = entity;
            State = state;
            Kingdom = kingdom;
            Position = position;
            Direction = direction;
            Elapsed = elapsed;
        }
    }
}

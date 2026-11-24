using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Content.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>将已确认的屏障形状接到旧技能、原版弹丸和人物的真实运动边界。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>只保存已确认姿态，预测及回退期间不发布临时形状。</summary>
    private static readonly List<BarrierContact> barriers = new();

    /// <summary>屏障复用共同空间索引，不为每个来袭对象遍历所有施放。</summary>
    private static readonly AbilityContactIndex barrierIndex = new();

    /// <summary>发布和查询分别复用缓冲，不逐人物分配形状列表。</summary>
    private static readonly List<AbilityCapsule> barrierShapes = new();

    /// <summary>当前路径可能接触的实际屏障。</summary>
    private static readonly List<int> barrierCandidates = new();

    /// <summary>原版弹丸在出生和反弹时保留真实来源编号，不让对象池换人改变个人敌意。</summary>
    private static readonly ConditionalWeakTable<Projectile, ProjectileSource> projectileSources = new();

    /// <summary>弹丸不长期持有人物对象。</summary>
    private sealed class ProjectileSource
    {
        /// <summary>实际出生或反弹时的来源人物，非人物为零。</summary>
        internal long ActorId;
    }

    /// <summary>在原版确定实际来源后记录，后续只读取该次来源身份。</summary>
    /// <param name="projectile">本次实际弹丸。</param><param name="source">本次原版来源。</param>
    internal static void RememberProjectile(Projectile projectile, BaseSimObject source) =>
        projectileSources.GetValue(projectile, _ => new ProjectileSource()).ActorId = source?.isActor() == true ? source.getID() : 0;

    /// <summary>真实屏障姿态及其稳定运行归属。</summary>
    private readonly struct BarrierContact
    {
        /// <summary>所属施放。</summary>
        internal readonly RunningCast Running;
        /// <summary>已确认的真实形状。</summary>
        internal readonly AbilityCapsule Shape;
        /// <summary>保留已确认姿态，不复制可回退的容量。</summary>
        /// <param name="running">所属施放。</param><param name="shape">实际形状。</param>
        internal BarrierContact(RunningCast running, AbilityCapsule shape) { Running = running; Shape = shape; }
    }

    /// <summary>在新施放受理和共同步开始、结束时发布；不在预测的临时姿态中调用。</summary>
    private static void PublishBarriers()
    {
        barriers.Clear(); barrierIndex.Clear();
        foreach (RunningCast running in casts)
        {
            if (!running.Cast.Plan.HasBarrier || running.Cast.Finished || running.Replaced) continue;
            barrierShapes.Clear(); running.Cast.AppendContacts(barrierShapes);
            foreach (AbilityCapsule shape in barrierShapes)
            {
                if (running.Cast.Role(shape.Node) != ConstructionContactRole.Barrier) continue;
                barriers.Add(new BarrierContact(running, shape));
                barrierIndex.Add(AbilityContactIndex.Swept(shape, shape));
            }
        }
    }

    /// <summary>筛去已结束、被替换或已耗尽的屏障，容量直接读取当前确认值。</summary>
    /// <param name="barrier">发布的稳定姿态。</param>
    private static bool BarrierLive(in BarrierContact barrier) => !barrier.Running.Cast.Finished &&
        !barrier.Running.Replaced && barrier.Running.Cast.RemainingForce(barrier.Shape.Node) > 0f;

    /// <summary>是否存在实际需要阻止敌人地面移动的屏障。</summary>
    bool ISkillBarrierBridge.HasGroundBarriers => barriers.Exists(b => BarrierLive(b) &&
        b.Running.Cast.Plan.Nodes[b.Shape.Node].BlocksGroundActors);

    /// <summary>确认一个实际飞行路径是否被共同屏障截住。</summary>
    /// <param name="entity">带实际前后位置及来向的飞行物。</param>
    internal static bool InterceptFlyingBody(Entity entity)
    {
        if (barriers.Count == 0 || !entity.TryGetComponent(out ColliderSphere collider)) return false;
        SkillContext context = entity.GetComponent<SkillContext>();
        Vector3 end = entity.GetComponent<Position>().value;
        Vector3 start = entity.TryGetComponent(out PrevPosition previous)
            ? new Vector3(previous.Value.x, previous.Value.y, previous.Height) : end;
        start = BarrierWorldPoint(start); end = BarrierWorldPoint(end);
        Vector3 direction = end - start;
        float forward = 0f, backward = 0f;
        if (entity.TryGetComponent(out ColliderLinearExtent extent))
        {
            if (extent.UseEntityRotation) direction = entity.GetComponent<Rotation>().value;
            forward = Mathf.Max(0f, extent.Forward); backward = Mathf.Max(0f, extent.Backward);
        }
        direction = direction.sqrMagnitude > .000001f ? direction.normalized : Vector3.right;
        float radius = SkillEffectRadius.Resolve(entity, collider.Radius);
        var a = new AbilityCapsule(0, start - direction * backward, start + direction * forward, radius, null);
        var b = new AbilityCapsule(0, end - direction * backward, end + direction * forward, radius, null);
        if (!FindBarrier(a, b, context.ResolveAttackKingdom(), context.SourceObj?.isActor() == true ? context.SourceId : 0, null, false, out BarrierContact wall,
            out _, out Vector3 point)) return false;
        entity.AddTag<TagRecycle>();
        ConsumeBarrier(wall, context.Strength, point);
        return true;
    }

    /// <summary>将相对地表的旧对象坐标换算到共同世界高度。</summary>
    /// <param name="point">原版位置及离地高度。</param>
    private static Vector3 BarrierWorldPoint(Vector3 point)
    {
        point.z += simulation?.terrain?.Sample(point.x, point.y) ?? 0f;
        return point;
    }

    /// <summary>原版弹丸在本步原伤害及落点回调之前确认真实撞点。</summary>
    /// <param name="projectile">实际弹丸。</param><param name="from">原版本步前态。</param>
    /// <param name="to">采用原积分规则算出的本步后态。</param><param name="hit">应写回原弹丸的相对地表撞点。</param>
    internal static bool InterceptProjectile(Projectile projectile, Vector3 from, Vector3 to, out Vector3 hit)
    {
        hit = default;
        if (barriers.Count == 0 || !projectile.canBeCollided()) return false;
        Vector3 start = BarrierWorldPoint(from), end = BarrierWorldPoint(to);
        float radius = Mathf.Max(0f, projectile.asset.size);
        var a = new AbilityCapsule(0, start, start, radius, null);
        var b = new AbilityCapsule(0, end, end, radius, null);
        if (!FindBarrier(a, b, projectile.kingdom, projectileSources.TryGetValue(projectile, out ProjectileSource source) ? source.ActorId : 0, null, false, out BarrierContact wall,
            out float fraction, out Vector3 point)) return false;
        hit = Vector3.Lerp(start, end, fraction);
        hit.z -= simulation?.terrain?.Sample(hit.x, hit.y) ?? 0f;
        ConsumeBarrier(wall, Mathf.Max(1f, projectile.asset.mass * 10f), point);
        return true;
    }

    /// <summary>人物普通移动按共同实际身体查询；飞行人物不占用地面通行。</summary>
    /// <param name="actor">真实人物。</param><param name="from">前次地图位置。</param><param name="to">本次地图位置。</param>
    Vector2 ISkillBarrierBridge.RestrictGroundActor(Actor actor, Vector2 from, Vector2 to)
    {
        Vector3 start = BarrierWorldPoint(new Vector3(from.x, from.y, actor.position_height)),
            end = BarrierWorldPoint(new Vector3(to.x, to.y, actor.position_height));
        return GroundMovementAllowed(actor, start, end, SpatialActorPose.Height(actor)) ? to : from;
    }

    /// <summary>步法及受力位移复用同一真实屏障，仅读取，不在预测中消耗容量。</summary>
    /// <param name="actor">被移动人物。</param><param name="from">绝对世界前态。</param>
    /// <param name="to">绝对世界后态。</param><param name="height">当前实际体高。</param>
    internal static bool GroundMovementAllowed(Actor actor, Vector3 from, Vector3 to, float height)
    {
        if (barriers.Count == 0 || actor.isFlying() || ((Vector2)(to - from)).sqrMagnitude < 1e-12f) return true;
        return !FindBarrier(ActorHitBoundary.Create(from, height, Vector3.zero), ActorHitBoundary.Create(to, height, Vector3.zero),
            null, actor.id, actor, true, out _, out _, out _);
    }

    /// <summary>空间筛选后以真实三维扫掠选择最近一面，不由登记先后决定拦截。</summary>
    /// <param name="from">来袭前态。</param><param name="to">来袭后态。</param><param name="kingdom">来袭阵营。</param>
    /// <param name="actorId">来袭人物稳定编号，非人物为零。</param>
    /// <param name="actor">地面人物，使用真实敌我关系。</param><param name="ground">只选阻挡地面人物的屏障。</param>
    /// <param name="wall">最近屏障。</param><param name="fraction">路径接触比例。</param><param name="point">真实表面撞点。</param>
    private static bool FindBarrier(in AbilityCapsule from, in AbilityCapsule to, Kingdom kingdom, long actorId, Actor actor,
        bool ground, out BarrierContact wall, out float fraction, out Vector3 point)
    {
        wall = default; fraction = float.PositiveInfinity; point = default;
        barrierIndex.Query(AbilityContactIndex.Swept(from, to), barrierCandidates);
        foreach (int candidate in barrierCandidates)
        {
            BarrierContact b = barriers[candidate];
            if (!BarrierLive(b) || ground && !b.Running.Cast.Plan.Nodes[b.Shape.Node].BlocksGroundActors) continue;
            CastAuthority owner = b.Running.Owner(b.Shape.Node);
            owner.RefreshRelations();
            bool hostile = actor != null && (kingdom == null || kingdom == actor.kingdom) ? owner.Matches(Core.SkillLibV3.Usage.SkillUseTargetRelation.Hostile, actor)
                : owner.IsHostileTo(kingdom, actorId);
            if (!hostile || !AbilityPairSweep.Find(from, to, b.Shape, b.Shape, out float at,
                out Vector3 contact, out Vector3 normal)) continue;
            // 墙出现在身体边缘时仍允许向外离开，不把人永远锁在起点。
            if (ground && at <= .00001f && Vector3.Dot(to.A - from.A, normal) < -.000001f) continue;
            if (at >= fraction) continue;
            wall = b; fraction = at; point = contact;
        }
        return !float.IsPositiveInfinity(fraction);
    }

    /// <summary>在真实拦截边界消费容量，下一次预测从已消费的状态起步。</summary>
    /// <param name="wall">已确认的屏障。</param><param name="force">来袭强度。</param><param name="point">实际表面接触。</param>
    private static void ConsumeBarrier(in BarrierContact wall, float force, Vector3 point)
    {
        int node = wall.Shape.Node;
        float absorbed = Mathf.Min(wall.Running.Cast.RemainingForce(node), Mathf.Max(0f, force));
        wall.Running.Cast.ConfirmClash(node, point, absorbed);
        wall.Running.Cast.ConsumeForce(node, absorbed);
        PublishBarriers();
    }
}

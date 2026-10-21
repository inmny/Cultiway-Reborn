using System;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际反射及部位归属的主线程交接，不创建额外施放或再次支付攻击资源。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>反射已经改变运动、归属和屏障余量后发布。</summary>
    public static event Action<ConstructedReflection> ReflectionConfirmed;

    /// <summary>当前控制者的稳定身份及已经冻结的伤害归属。</summary>
    private sealed class CastAuthority
    {
        /// <summary>实际控制者，只在主线程检查。</summary>
        internal readonly Actor Actor;
        /// <summary>对象池重用时仍可识别身份。</summary>
        internal readonly long Id;
        /// <summary>已经确定的归属及能级，交锋后不重复缩放。</summary>
        internal readonly GetHitEvent Damage;
        /// <summary>追踪的上一控制者及其稳定编号。</summary>
        internal readonly Actor Target;
        /// <summary>追踪目标的稳定编号。</summary>
        internal readonly long TargetId;
        /// <summary>当前维持来源是否仍有效。</summary>
        internal bool IsLive => Actor.data?.id == Id && Actor.isAlive() && Actor.hasHealth();

        /// <summary>保留当前实际控制者与已冻结模板。</summary>
        /// <param name="actor">当前控制者。</param>
        /// <param name="damage">当前伤害归属。</param>
        /// <param name="target">追踪目标。</param>
        internal CastAuthority(Actor actor, GetHitEvent damage, Actor target)
        { Actor = actor; Id = actor.data.id; Damage = damage; Target = target; TargetId = target?.data?.id ?? 0; }
    }

    /// <summary>容量足够且来袭部位真正独立运动时执行反射，否则保留正常吸收。</summary>
    /// <param name="hit">共同时间最早的接触。</param>
    private static bool TryReflect(in ContactChoice hit)
    {
        ShapeMotion left = hit.Left, right = hit.Right.Value;
        bool leftWard = left.Running.Cast.Role(left.To.Node) == ConstructionContactRole.Barrier;
        ShapeMotion ward = leftWard ? left : right, attack = leftWard ? right : left;
        int guardNode = ward.To.Node, node = attack.To.Node;
        if (ward.Running.Cast.Role(guardNode) != ConstructionContactRole.Barrier ||
            ward.Running.Cast.Plan.Nodes[guardNode].Interception != ConstructionInterception.Reflect ||
            !attack.Running.Cast.CanReflect(node)) return false;
        float force = attack.Running.Cast.RemainingForce(node);
        if (ward.Running.Cast.RemainingForce(guardNode) < force) return false;
        Vector3 normal = leftWard ? hit.Normal : -hit.Normal;
        if (normal.sqrMagnitude < .5f) return false;
        Vector3 surface = ward.SurfaceVelocity(hit.Point, normal, hit.Fraction, hit.Seconds);
        Vector3 incoming = attack.Running.Cast.Velocity(node);
        if (Vector3.Dot(incoming - surface, normal) >= -.00001f) return false;
        CastAuthority previous = attack.Running.Owner(node), defender = ward.Running.Owner(guardNode);
        var damage = previous.Damage;
        damage.AttackerID = defender.Damage.AttackerID; damage.AttackerIsActor = defender.Damage.AttackerIsActor;
        damage.HasAttacker = defender.Damage.HasAttacker; damage.DamageOrigin = DamageOrigin.Reaction;
        var owner = new CastAuthority(defender.Actor, damage, previous.Actor);
        Vector3 aim = previous.IsLive ? Combat.SpatialActorPose.Foot(previous.Actor) +
            Vector3.forward * Combat.SpatialActorPose.Height(previous.Actor) * .5f : attack.Running.LastTarget;
        attack.Running.Cast.Reflect(node, normal, surface, aim);
        attack.Running.Owners[node] = owner;
        for (int i = node + 1; i < attack.Running.Owners.Length; i++)
            if (attack.Running.Cast.IsAttachedBelow(i, node)) attack.Running.Owners[i] = owner;
        attack.Running.HasTransfers = true;
        ward.Running.Cast.ConsumeForce(guardNode, force);
        attack.Running.Cast.ApplyMatterChanges(node, ConstructionChangeTrigger.Clash);
        ward.Running.Cast.ApplyMatterChanges(guardNode, ConstructionChangeTrigger.Clash);
        ReflectionConfirmed?.Invoke(new ConstructedReflection(attack.Running.Id, attack.Running.Cast.Plan.Id,
            node, attack.Running.SourceId, previous.Id, owner.Id, attack.Running.Cast.Reflections(node),
            attack.Running.Cast.Age, hit.Point, normal, incoming, attack.Running.Cast.Velocity(node),
            force, ward.Running.Id, guardNode, ward.Running.Cast.RemainingForce(guardNode)));
        return true;
    }
}

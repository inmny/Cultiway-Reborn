using System;
using System.Collections.Generic;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private readonly List<BellWardMotion> bellWards = new();

    private void CaptureBellWards(double clock, float step)
    {
        bellWards.Clear();
        foreach (Entity item in ArtifactBellWard.Active)
        {
            if (!ArtifactBellWard.TryRead(item, out var context, out _, out var entry, out _) ||
                entry.GetNumber(ArtifactAbilities.ShieldCurrent) <= 0f || !ArtifactBellWard.TryShape(item, out var shape)) continue;
            Actor actor = context.ResolveCarrierActor();
            if (actor == null || !actorIndices.TryGetValue(actor.id, out int index) || !actor.isAlive() ||
                !actor.hasHealth() || actor.hasStatus("invincible")) continue;
            ActorMotion motion = actors[index];
            Sample(motion, clock, out Vector3 start, out float firstHeight);
            Sample(motion, clock + step, out Vector3 end, out float lastHeight);
            float first = shape.Scale(firstHeight, clock - entry.activity_started_at);
            float last = shape.Scale(lastHeight, clock + step - entry.activity_started_at);
            bellWards.Add(new BellWardMotion(item, motion, shape, start, end, first, last));
        }
    }

    // 原版受击及持续范围仍在人物处结算。只有实际在移动的伤害形体绕过原个人护盾入口。
    private static bool UsesBellSurface(in ShapeMotion attack, CompiledForm form, CompiledConstructionImpact impact)
    {
        int node = attack.To.Node;
        var cast = attack.Running.Cast;
        return attack.Continuous && form != null && !form.BorrowedWeaponDamage && !cast.IsRay(node) &&
               form.GroundCircleRadius <= 0f && cast.AffectsActors(node) && impact.DamageScale > 0f &&
               cast.ContactAttenuation(node, attack.To.Shape) > 0f &&
               cast.Role(node) == ConstructionContactRole.Attack &&
               cast.Plan.ContactPolicies[cast.ContactGroup(node)].Mode != ConstructionContactMode.Pulse &&
               ((attack.To.A - attack.From.A).sqrMagnitude > 1e-10f || (attack.To.B - attack.From.B).sqrMagnitude > 1e-10f);
    }

    private void FindBellWards(in ShapeMotion attack, CompiledForm form, CastAuthority owner, float touchFrom,
        float step, Action<ContactChoice> include)
    {
        if (bellWards.Count == 0 || form == null) return;
        var cast = attack.Running.Cast;
        int node = attack.To.Node;
        CompiledConstructionImpact impact = cast.Impact(node);
        if (!UsesBellSurface(attack, form, impact) ||
            !cast.ContactInterval(node, cast.BodyStage, out float from, out float until)) return;
        from = Mathf.Max(from, touchFrom);
        until = Mathf.Min(until, attack.To.Until);
        if (from > until) return;
        AbilityCapsule start = Slice(attack.From, attack.To, from);
        AbilityCapsule end = Slice(attack.From, attack.To, until);
        foreach (var ward in bellWards)
        {
            Actor actor = ward.Carrier.Actor;
            if (form.GroundActorsOnly && actor.isFlying() || impact.SelectedTargetOnly && owner.TargetId != ward.Carrier.Id ||
                !owner.Matches(impact.Targets, actor) || !attack.Bounds.Intersects(ward.Bounds)) continue;
            if (!ward.Find(start, end, from, until, out float at, out Vector3 point, out Vector3 normal)) continue;
            include(new ContactChoice(attack, ward.Carrier, null, Mathf.Lerp(from, until, at), point, normal,
                step, bellWard: ward.Item));
        }
    }

    private void ResolveBellWard(in ContactChoice hit)
    {
        if (!ArtifactBellWard.TryRead(hit.BellWard, out var context, out var ability, out _, out int index)) return;
        Actor actor = hit.Actor.Actor;
        if (actor.data?.id != hit.Actor.Id || !actor.isAlive() || actor.hasStatus("invincible")) return;
        var running = hit.Left.Running;
        var cast = running.Cast;
        int node = hit.Left.To.Node;
        CompiledConstructionImpact impact = cast.Impact(node);
        float damagePerForce = impact.DamageScale * cast.ContactAttenuation(node, hit.Left.To.Shape);
        if (impact.IsolatedDamageMultiplier > 0f && CombatTargeting.IsIsolated(actor, impact.IsolationRadius))
            damagePerForce *= impact.IsolatedDamageMultiplier;
        float original = cast.DamageForce(node) * damagePerForce;
        if (original <= 0f) return;

        Sample(hit.Actor, LogicTime, out Vector3 center, out _);
        // 两种显示都以护持者脚下为承托，受击点采用同一个离地基准。
        Vector3 point = hit.Point;
        float ground = terrain?.Sample(center.x, center.y) ?? 0f;
        point.z -= ground;
        center.z -= ground;
        var runtime = hit.BellWard.GetComponent<ArtifactAbilityRuntime>();
        float remaining = original;
        float spent = ArtifactAbilities.AbsorbWithGoldenBell(context, ability, ref runtime.abilities[index], ref remaining,
            actor.GetExtend(), running.Owner(node).Actor, -hit.Normal, point, center);
        if (spent <= 0f) return;
        var remainingForce = cast.RemainingForce(node);
        cast.ConfirmClash(node, hit.Point, spent);
        cast.ConsumeForceAtBodyGuard(node, Mathf.Max(0f, remainingForce - remaining / damagePerForce));
    }

    private readonly struct BellWardMotion
    {
        internal readonly Entity Item;
        internal readonly ActorMotion Carrier;
        internal readonly Bounds Bounds;
        private readonly ArtifactBellWard.Shape shape;
        private readonly Vector3 start, end;
        private readonly float firstScale, lastScale;

        internal BellWardMotion(Entity item, ActorMotion carrier, ArtifactBellWard.Shape shape,
            Vector3 start, Vector3 end, float firstScale, float lastScale)
        {
            Item = item;
            Carrier = carrier;
            this.shape = shape;
            this.start = start;
            this.end = end;
            this.firstScale = firstScale;
            this.lastScale = lastScale;
            var bounds = shape.At(start, firstScale);
            bounds.Encapsulate(shape.At(end, lastScale));
            bounds.Expand(.00004f);
            Bounds = bounds;
        }

        internal bool Find(in AbilityCapsule startAttack, in AbilityCapsule endAttack, float from, float until,
            out float first, out Vector3 point, out Vector3 normal)
        {
            var before = Pose(from);
            var after = Pose(until);
            Bounds sweep = AbilityContactIndex.Swept(startAttack, endAttack);
            SpatialVisualMesh mesh = shape.Model;
            first = float.PositiveInfinity;
            point = normal = default;
            for (int i = 0; i < mesh.Triangles.Length; i += 3)
            {
                Vector3 a = mesh.Vertices[mesh.Triangles[i]], b = mesh.Vertices[mesh.Triangles[i + 1]],
                    c = mesh.Vertices[mesh.Triangles[i + 2]];
                Vector3 a0 = before.TransformPoint(a), b0 = before.TransformPoint(b), c0 = before.TransformPoint(c);
                Vector3 a1 = after.TransformPoint(a), b1 = after.TransformPoint(b), c1 = after.TransformPoint(c);
                Vector3 min = Vector3.Min(Vector3.Min(a0, b0), Vector3.Min(c0, Vector3.Min(a1, Vector3.Min(b1, c1))));
                Vector3 max = Vector3.Max(Vector3.Max(a0, b0), Vector3.Max(c0, Vector3.Max(a1, Vector3.Max(b1, c1))));
                var bounds = new Bounds();
                bounds.SetMinMax(min, max);
                if (!bounds.Intersects(sweep) ||
                    !AbilityTriangleSweep.Find(startAttack, endAttack, a0, b0, c0, a1, b1, c1,
                        out float at, out Vector3 where, out Vector3 direction) || at >= first) continue;
                first = at;
                point = where;
                normal = direction;
            }
            return first <= 1f;
        }

        private SpatialVisualMeshInstance Pose(float at) =>
            shape.Pose(Vector3.Lerp(start, end, at), Mathf.Lerp(firstScale, lastScale, at), Color.white);
    }
}

using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>镜内撞点和实际传到原身处共用一次结果。</summary>
public readonly struct ConstructedMirrorCrack
{
    /// <summary>实际施放、原人物和接触部位。初始击镜时还未抵达接收部位。</summary>
    public readonly long CastId, TargetId;
    public readonly string Part;

    /// <summary>镜中位置和原身确实受影响的表面。</summary>
    public readonly Vector3 MirrorPoint;
    public readonly Vector3? OriginalPoint;

    /// <summary>共同结算时点及生命、护盾结果。</summary>
    public readonly double WorldTime;
    public readonly DamageSettlement Settlement;

    internal ConstructedMirrorCrack(long castId, long targetId, string part, Vector3 mirrorPoint,
        Vector3? originalPoint, double time, DamageSettlement settlement)
    {
        CastId = castId;
        TargetId = targetId;
        Part = part;
        MirrorPoint = mirrorPoint;
        OriginalPoint = originalPoint;
        WorldTime = time;
        Settlement = settlement;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>真实击镜和许可部位各自确认后发布，供两端共用。</summary>
    public static event System.Action<ConstructedMirrorCrack> MirrorCrackConfirmed;

    /// <summary>裂势只由资料指定的同一次施放的实际攻击发起。</summary>
    private static bool IsMirrorInitiator(in ShapeMotion attack, in ShapeMotion image)
    {
        var rule = image.Running.Cast.Plan.Nodes[image.To.Node].Correspondence?.MirrorImage?.Fracture;
        return rule == null || attack.Running == image.Running && attack.To.Node == rule.TriggerNode;
    }

    /// <summary>从碰撞已经消耗的真实作用量建立裂势，击镜这一刻还未抵达原身。</summary>
    private static ApplyAttackState BeginMirrorCrack(in ShapeMotion attack, in ShapeMotion image, Actor receiver,
        CompiledCorrespondence relation, float spent, Vector3 point)
    {
        RunningCast running = image.Running;
        int node = image.To.Node;
        if (!IsMirrorInitiator(attack, image) || running.MirrorCracks?.ContainsKey(node) == true ||
            !running.Cast.IsNodePresent(node)) return ApplyAttackState.Continue;

        double time = simulation.contactTime;
        MirrorReflection mirror = CurrentMirror(running, node, receiver, time);
        if (mirror == null) return ApplyAttackState.Continue;
        var authority = running.Owner(attack.To.Node);
        var impact = running.Cast.Impact(attack.To.Node);
        var damage = authority.Damage;
        damage.DamageOrigin = DamageOrigin.Reaction;
        damage.TargetID = receiver.id;
        damage.Element = running.Cast.FixedDamageElement(attack.To.Node, impact) ??
                         Composition(running.Cast.Matter(attack.To.Node).DamageKind);

        Matrix4x4 transform = running.Cast.NodeTransform(node);
        var rule = relation.MirrorImage;
        Vector3 localPoint = transform.inverse.MultiplyPoint3x4(point) - rule.Centre;
        Vector2 origin = Vector2.ClampMagnitude(new Vector2(localPoint.x, localPoint.z), rule.Radius * .99f);
        var crack = new MirrorCrack(origin, mirror, rule, time, authority, damage, spent, impact.DamageScale, impact.Targets)
        {
            Rule = rule, Resource = running.Cast.Plan.Nodes[node].Resource, LastPlane = transform,
            Travelled = rule.Radius * .08f
        };
        (running.MirrorCracks ??= new()).Add(node, crack);
        RememberContact(attack.Running, attack.To.Node, receiver.id);
        EndExhaustedContactGroup(attack.Running, attack.To.Node);
        MirrorCrackConfirmed?.Invoke(new ConstructedMirrorCrack(running.Id, receiver.id, null,
            transform.MultiplyPoint3x4(crack.OnPane(origin, rule)), null, time, default));
        return ApplyAttackState.Hit;
    }

    /// <summary>按裂势已走过的路径依次提交伤害，镜面、来源或原身失效后丢弃尚未抵达的结果。</summary>
    private static void AdvanceMirrorCracks(RunningCast running, double clock, long generation)
    {
        foreach (var entry in running.MirrorCracks)
        {
            int node = entry.Key;
            MirrorCrack crack = entry.Value;
            if (crack.BrokenAt.HasValue) continue;
            if (!running.Cast.IsNodePresent(node))
            {
                BreakMirror(crack, clock);
                continue;
            }
            crack.LastPlane = running.Cast.NodeTransform(node);
            if (crack.Stopped) continue;
            CompiledCorrespondence relation = running.Cast.Plan.Nodes[node].Correspondence;
            var rule = relation.MirrorImage;
            float progress = (float)(clock - crack.Started) / rule.Fracture.TravelSeconds;
            crack.Travelled = rule.Radius * Mathf.Max(.08f, 2f * Mathf.Clamp01((progress - .17f) / .83f));
            Actor receiver = CorrespondenceReceiver(running, node);
            if (receiver == null || !crack.Authority.IsLive || !crack.Authority.Matches(crack.Targets, receiver))
            {
                crack.Stopped = true;
                continue;
            }

            while (crack.Next < crack.Arrivals.Length && crack.Arrivals[crack.Next].At <= clock)
            {
                MirrorArrival arrival = crack.Arrivals[crack.Next++];
                MirrorReflection mirror = CurrentMirror(running, node, receiver, clock);
                if (mirror == null || !mirror.FindMirrorPoint(rule, arrival.Point, arrival.Part, out Vector3 original))
                    continue;

                var hit = crack.Damage;
                hit.Damage = crack.Spent * relation.DamageFraction * crack.DamageScale / rule.Fracture.Parts.Length;
                float before = receiver.data.health;
                DamageSettlement settlement = GetHitEventSystem.Resolve(hit);
                if (generation != worldGeneration) return;
                float after = receiver.data?.id == hit.TargetID ? receiver.data.health : 0f;
                Vector3 mirrorPoint = running.Cast.NodeTransform(node).MultiplyPoint3x4(crack.OnPane(arrival.Point, rule));
                CorrespondenceDamageConfirmed?.Invoke(new ConstructedCorrespondenceDamage(running.Id, running.Id,
                    crack.Authority.Id, hit.TargetID, rule.Fracture.TriggerNode, node, relation.Id, mirrorPoint, clock,
                    crack.Spent / rule.Fracture.Parts.Length, relation.DamageFraction, hit.Damage, before, after, settlement));
                if (generation != worldGeneration) return;
                MirrorCrackConfirmed?.Invoke(new ConstructedMirrorCrack(running.Id, hit.TargetID, arrival.Part,
                    mirrorPoint, original, clock, settlement));
                if (generation != worldGeneration) return;
                MirrorContactFeedback(mirrorPoint, original, settlement);
                if (!receiver.isAlive() || receiver.data?.id != hit.TargetID || !running.Cast.IsNodePresent(node))
                {
                    crack.Stopped = true;
                    break;
                }
            }
            if (progress < 1f || crack.Stopped) continue;
            BreakMirror(crack, clock);
            running.Cast.EndNode(node);
        }
    }

    /// <summary>原身与镜面用同一份实际伤害或护盾消耗发生局部碎光。</summary>
    private static void MirrorContactFeedback(Vector3 mirror, Vector3 original, DamageSettlement result)
    {
        if (result.HealthLost <= 0 && result.ShieldAbsorbed <= 0f) return;
        Color colour = result.ShieldAbsorbed > 0f ? new Color(.32f, .67f, .85f, .88f) : new Color(.92f, .94f, 1f, .94f);
        Vector3 upward = new(.08f, -.10f, .62f);
        SpatialVisualEffects.Particle(mirror, upward, colour, .13f, .19f, SpatialParticleMaterial.Fragment, -2f);
        SpatialVisualEffects.Particle(original, -upward * .22f, colour, .16f, .22f, SpatialParticleMaterial.Fragment, -2f);
    }
}

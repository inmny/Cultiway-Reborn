using System;
using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一次实际分担；原目标只免除承受者已经支付的份额。</summary>
public readonly struct SoulPactShare
{
    public readonly long CastId, ProtectedId, BearerId;
    public readonly float Covered, Remaining;
    public readonly DamageSettlement BearerSettlement;

    internal SoulPactShare(long castId, long protectedId, long bearerId, float covered, float remaining,
        DamageSettlement settlement)
    {
        CastId = castId;
        ProtectedId = protectedId;
        BearerId = bearerId;
        Covered = covered;
        Remaining = remaining;
        BearerSettlement = settlement;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>仅在实际扣血或消耗护盾后发布。</summary>
    public static event Action<SoulPactShare> SoulPactShared;

    private static readonly Dictionary<long, SoulPact> soulPacts = new();
    private static readonly List<SoulPactTransfer> soulPactTransfers = new();
    private const float SoulPactTransferSeconds = .46f;

    /// <summary>一个受保护者同时只接受一份契约，不为被拒绝的施放收费。</summary>
    private static bool CanBeginSoulPact(Actor bearer, Actor receiver) =>
        bearer != null && receiver != null && bearer != receiver && bearer.isAlive() && bearer.hasHealth() &&
        SkillTargetRelationResolver.IsFriendly(bearer, receiver) &&
        (!soulPacts.TryGetValue(receiver.id, out var existing) || !existing.IsLive(LogicTime));

    /// <summary>二次受击沿原规则结算；被抵消或超出承受者实际生命的部分仍由原目标负担。</summary>
    private static void ShareSoulPactDamage(ActorExtend receiver, BaseSimObject attacker, ElementComposition composition,
        AttackType kind, ref float damage)
    {
        if (DamageResolutionContext.IsReaction || !soulPacts.TryGetValue(receiver.Base.id, out var pact)) return;
        double now = LogicTime;
        if (!pact.IsLive(now) || pact.Cast.Target != receiver.Base)
        {
            soulPacts.Remove(receiver.Base.id);
            return;
        }
        // 原版扣血以生命点为单位；不请求小于一的份额，避免原版取整凭空增加伤害。
        float share = Mathf.Floor(Mathf.Min(damage * pact.Rule.Fraction, pact.Remaining));
        if (share < 1f || attacker == pact.Cast.Source || !pact.Cast.Source.TryGetExtend(out var bearer)) return;
        long generation = worldGeneration;
        Vector3 from = SoulPactPoint(receiver.Base), to = SoulPactPoint(pact.Cast.Source);
        DamageSettlement settled;
        using (DamageResolutionContext.Enter(DamageOrigin.Reaction, DamageResolutionContext.CurrentSourceScopeId))
            settled = bearer.ResolveHit(share, ref composition, attacker, kind, false, null);
        if (generation != worldGeneration) return;
        float covered = Mathf.Min(share, settled.HealthLost + settled.ShieldAbsorbed);
        if (covered <= 0f) return;
        damage -= covered;
        pact.Remaining -= covered;
        if (!pact.IsLive(now)) soulPacts.Remove(receiver.Base.id);
        if (soulPactTransfers.Count >= 64) soulPactTransfers.RemoveAt(0);
        soulPactTransfers.Add(new SoulPactTransfer(from, to, now, covered));
        SoulPactShared?.Invoke(new SoulPactShare(pact.Cast.Id, pact.Cast.TargetId, pact.Cast.SourceId,
            covered, pact.Remaining, settled));
    }

    private static Vector3 SoulPactPoint(Actor actor)
    {
        StartingPose(actor, out Vector3 foot, out float height);
        return foot + Vector3.forward * Mathf.Max(.45f, height * .65f);
    }

    /// <summary>两枚契印跟随真实人物；剩余份额越少，连接中的实体越少。</summary>
    private void AppendSoulPact(RunningCast running, double time)
    {
        if (running.Cast.Plan.SoulPact == null || !soulPacts.TryGetValue(running.TargetId, out var pact) || pact.Cast != running) return;
        if (!pact.IsLive(time))
        {
            soulPacts.Remove(running.TargetId);
            return;
        }
        Vector3 from = SoulPactPoint(running.Target), to = SoulPactPoint(running.Source);
        var model = FormResources.Load("soul_pact_seal");
        var facing = Quaternion.AngleAxis(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, Vector3.forward);
        visuals.Add(new FormVisualInstance(model, Matrix4x4.TRS(from, facing, Vector3.one * .92f),
            tint: new Color(.62f, .93f, 1f), terrain: terrain));
        visuals.Add(new FormVisualInstance(model, Matrix4x4.TRS(to, facing, Vector3.one * 1.13f),
            tint: new Color(1f, .69f, .48f), terrain: terrain));
        int count = Mathf.CeilToInt(5f * pact.Remaining / pact.Rule.Capacity);
        for (int i = 1; i <= count; i++)
        {
            Vector3 point = SoulPactArc(from, to, i / (float)(count + 1));
            visuals.Add(new FormVisualInstance(model, Matrix4x4.TRS(point, facing, Vector3.one * .28f),
                tint: new Color(.53f, .65f, .83f), terrain: terrain));
        }
    }

    /// <summary>已经转移的实体仍走完一次短程，即使这次分担耗尽契约。</summary>
    private void AppendSoulPactTransfers(double time)
    {
        for (int i = soulPactTransfers.Count - 1; i >= 0; i--)
        {
            var transfer = soulPactTransfers[i];
            float progress = Mathf.Max(0f, (float)(time - transfer.StartedAt) / SoulPactTransferSeconds);
            if (progress >= 1f)
            {
                soulPactTransfers.RemoveAt(i);
                continue;
            }
            var model = FormResources.Load("soul_pact_seal");
            Quaternion turn = Quaternion.AngleAxis(180f * progress, Vector3.forward);
            float size = Mathf.Clamp(Mathf.Sqrt(transfer.Covered) * .16f, .45f, 1.22f);
            visuals.Add(new FormVisualInstance(model,
                Matrix4x4.TRS(SoulPactArc(transfer.From, transfer.To, progress), turn, Vector3.one * size),
                tint: new Color(1f, .85f, .55f), terrain: terrain));
        }
    }

    private static Vector3 SoulPactArc(Vector3 from, Vector3 to, float progress) =>
        Vector3.LerpUnclamped(from, to, progress) + Vector3.forward * (Mathf.Sin(progress * Mathf.PI) * .76f);

    private sealed class SoulPact
    {
        internal readonly RunningCast Cast;
        internal readonly CompiledSoulPact Rule;
        internal float Remaining;

        internal SoulPact(RunningCast cast, CompiledSoulPact rule)
        {
            Cast = cast;
            Rule = rule;
            Remaining = rule.Capacity;
        }

        internal bool IsLive(double now) => Remaining >= 1f && !Cast.Replaced && !Cast.Cast.Finished &&
            now < Cast.StartedAt + Cast.Cast.Plan.Lifetime &&
            Cast.Source.data?.id == Cast.SourceId && Cast.Source.isAlive() && Cast.Source.hasHealth() &&
            Cast.Target.data?.id == Cast.TargetId && Cast.Target.isAlive() && Cast.Target.hasHealth() &&
            SkillTargetRelationResolver.IsFriendly(Cast.Source, Cast.Target);
    }

    private readonly struct SoulPactTransfer
    {
        internal readonly Vector3 From, To;
        internal readonly double StartedAt;
        internal readonly float Covered;

        internal SoulPactTransfer(Vector3 from, Vector3 to, double startedAt, float covered)
        {
            From = from;
            To = to;
            StartedAt = startedAt;
            Covered = covered;
        }
    }
}

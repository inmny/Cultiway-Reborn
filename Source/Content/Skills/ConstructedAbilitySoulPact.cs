using System;
using System.Collections.Generic;
using System.Linq;
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

    // 每次加入或退出换一份快照。受伤通知可以引发新的施放，不能改变这笔伤害遍历的顺序。
    private static readonly Dictionary<long, SoulPact[]> soulPacts = new();
    private static readonly List<SoulPactTransfer> soulPactTransfers = new();
    private const float SoulPactTransferSeconds = .46f;

    /// <summary>同一人不能重复承担同一受护者。已在场的承受者和新承受者都须允许共存。</summary>
    private static bool CanBeginSoulPact(Actor bearer, Actor receiver, CompiledSoulPact rule)
    {
        if (bearer == null || receiver == null || bearer == receiver || !bearer.isAlive() || !bearer.hasHealth() ||
            !SkillTargetRelationResolver.IsFriendly(bearer, receiver)) return false;
        if (!soulPacts.TryGetValue(receiver.id, out var entries)) return true;
        double now = LogicTime;
        foreach (var entry in entries)
            if (entry.IsLive(now) && (!rule.AllowOtherBearers || !entry.Rule.AllowOtherBearers ||
                                      entry.Cast.SourceId == bearer.id)) return false;
        return true;
    }

    /// <summary>只登记经正式受理的本次份额，按承受者加入的顺序兑现。</summary>
    private static void RegisterSoulPact(RunningCast running, CompiledSoulPact rule)
    {
        var pact = new SoulPact(running, rule);
        running.Pact = pact;
        if (!soulPacts.TryGetValue(running.TargetId, out var existing))
        {
            soulPacts.Add(running.TargetId, [pact]);
            return;
        }
        double now = LogicTime;
        var active = new List<SoulPact>(existing.Length + 1);
        foreach (var entry in existing)
        {
            if (entry.IsLive(now)) active.Add(entry);
            else entry.Cast.Pact = null;
        }
        active.Add(pact);
        soulPacts[running.TargetId] = active.ToArray();
    }

    /// <summary>只移除本次施放的份额，保留同一受护者的其他联系。</summary>
    private static void RemoveSoulPact(RunningCast running)
    {
        if (running.Pact is not { } pact) return;
        running.Pact = null;
        if (!soulPacts.TryGetValue(running.TargetId, out var entries) || Array.IndexOf(entries, pact) < 0) return;
        if (entries.Length == 1) soulPacts.Remove(running.TargetId);
        else soulPacts[running.TargetId] = entries.Where(entry => entry != pact).ToArray();
    }

    /// <summary>每份契约以同一笔原余伤计价，所有实际承担的总和最多等于这笔来伤。</summary>
    private static void ShareSoulPactDamage(ActorExtend receiver, BaseSimObject attacker, ElementComposition composition,
        AttackType kind, ref float damage)
    {
        if (DamageResolutionContext.IsReaction || !soulPacts.TryGetValue(receiver.Base.id, out var entries)) return;
        long generation = worldGeneration;
        double now = LogicTime;
        float original = damage;
        foreach (var pact in entries)
        {
            if (damage < 1f || generation != worldGeneration) return;
            if (!pact.IsLive(now) || pact.Cast.Target != receiver.Base)
            {
                RemoveSoulPact(pact.Cast);
                continue;
            }
            // 原版扣血以生命点为单位，不请求零头，以免取整增加伤害。
            float share = Mathf.Floor(Mathf.Min(damage, Mathf.Min(original * pact.Rule.Fraction, pact.Remaining)));
            if (share < 1f || attacker == pact.Cast.Source || !pact.Cast.Source.TryGetExtend(out var bearer)) continue;
            SettleSoulPactShare(pact, bearer, receiver.Base, attacker, composition, kind, share, now, ref damage);
        }
    }

    /// <summary>经原规则实际结算，兑现已扣的生命和护盾；未能支付的份额仍留在原来伤中。</summary>
    private static void SettleSoulPactShare(SoulPact pact, ActorExtend bearer, Actor receiver, BaseSimObject attacker,
        ElementComposition composition, AttackType kind, float share, double now, ref float damage)
    {
        long generation = worldGeneration;
        Vector3 from = SoulPactPoint(receiver), to = SoulPactPoint(pact.Cast.Source);
        DamageSettlement settled;
        using (DamageResolutionContext.Enter(DamageOrigin.Reaction, DamageResolutionContext.CurrentSourceScopeId))
            settled = bearer.ResolveHit(share, ref composition, attacker, kind, false, null);
        if (generation != worldGeneration) return;
        float covered = Mathf.Min(share, settled.HealthLost + settled.ShieldAbsorbed);
        if (covered <= 0f) return;
        damage -= covered;
        pact.Remaining -= covered;
        if (!pact.IsLive(now)) RemoveSoulPact(pact.Cast);
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
        if (running.Pact is not { } pact) return;
        if (!pact.IsLive(time))
        {
            RemoveSoulPact(running);
            return;
        }
        Vector3 from = SoulPactPoint(running.Target), to = SoulPactPoint(running.Source);
        var model = FormResources.Load("soul_pact_seal");
        var facing = Quaternion.AngleAxis(Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg, Vector3.forward);
        if (soulPacts.TryGetValue(running.TargetId, out var bearers) && bearers[0] == pact)
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

        internal bool IsLive(double now) => Cast.Pact == this && Remaining >= 1f && !Cast.Replaced && !Cast.Cast.Finished &&
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

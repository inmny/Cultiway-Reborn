using System;
using Cultiway.Content.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>已实际登记在指定人物身上的消息。</summary>
public readonly struct ConstructedSignalReceipt
{
    /// <summary>本次施放、实际发信人和实际收件人。</summary>
    public readonly long CastId, SenderId, RecipientId;
    /// <summary>真正收到的讯号内容。</summary>
    public readonly ConstructionSignalKind Kind;
    /// <summary>消息送达时的真实世界时点。</summary>
    public readonly double WorldTime;

    /// <summary>只能记录已经完成的实际送达。</summary>
    /// <param name="cast">本次施放。</param><param name="sender">有效发信人。</param>
    /// <param name="recipient">接触收件人。</param><param name="kind">实际内容。</param><param name="time">实际送达时点。</param>
    internal ConstructedSignalReceipt(long cast, long sender, long recipient, ConstructionSignalKind kind, double time)
    {
        CastId = cast; SenderId = sender; RecipientId = recipient; Kind = kind; WorldTime = time;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>首次有效登记或更换实际发信人之后发布；同来源刷新不会重复发出。</summary>
    public static event Action<ConstructedSignalReceipt> SignalReceived;

    /// <summary>在收件人身上实际建立共享状态，再发布收件事实。</summary>
    /// <param name="running">实际施放。</param><param name="node">作用部位。</param>
    /// <param name="target">确认存活的实际收件人。</param><param name="signal">明确讯号。</param><param name="strength">实际接触强度。</param>
    private static void DeliverSignal(RunningCast running, int node, Actor target, CompiledConstructionSignal signal, float strength)
    {
        CastAuthority sender = running.Owner(node);
        float duration = signal.Duration * Mathf.Clamp01(strength);
        if (!sender.IsLive || duration <= 0f) return;
        bool alreadyReceived = CombatStatusEffects.TryGetActiveStatus(target, signal.Asset, out var previous) &&
                               previous.GetComponent<StatusComponent>().SourceActorId == sender.Id;
        var received = CombatStatusEffects.ApplyContactStatus(target, signal.Asset, duration, sender.Actor,
            sender.Damage.AttackerPowerLevel ?? 0f, ConstructionStatusApplication.Refresh, null);
        if (!received.IsNull && !received.Tags.Has<TagRecycle>() && !alreadyReceived)
            SignalReceived?.Invoke(new ConstructedSignalReceipt(running.Id, sender.Id, target.id, signal.Kind, simulation.contactTime));
    }
}

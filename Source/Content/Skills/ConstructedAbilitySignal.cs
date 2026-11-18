using System;
using Cultiway.Content.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
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
    /// <param name="recipient">实际收件人。</param><param name="kind">实际内容。</param><param name="time">实际送达时点。</param>
    internal ConstructedSignalReceipt(long cast, long sender, long recipient, ConstructionSignalKind kind, double time)
    {
        CastId = cast; SenderId = sender; RecipientId = recipient; Kind = kind; WorldTime = time;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>首次有效登记或更换实际发信人之后发布；同来源刷新不会重复发出。</summary>
    public static event Action<ConstructedSignalReceipt> SignalReceived;

    /// <summary>通过真实接触登记并发布收件事实。</summary>
    /// <param name="running">实际施放。</param><param name="node">作用部位。</param>
    /// <param name="target">确认存活的实际收件人。</param><param name="signal">明确讯号。</param><param name="strength">实际接触强度。</param>
    private static void DeliverSignal(RunningCast running, int node, Actor target, CompiledConstructionSignal signal, float strength)
    {
        CastAuthority sender = running.Owner(node);
        if (!TryRecordSignal(sender, target, signal, signal.Duration * Mathf.Clamp01(strength))) return;
        StartingPose(target, out Vector3 point, out _);
        NotifySignal(running, node, sender.Id, target.id, signal.Kind, point, simulation.contactTime);
    }

    /// <summary>实际登记给指定人物，再确认步骤结果；本路径不产生任何接触。</summary>
    /// <param name="running">本次施放。</param><param name="clock">真实确认时点。</param>
    private static void ConfirmDirectSignals(RunningCast running, double clock)
    {
        AbilityCast cast = running.Cast;
        long generation = worldGeneration;
        foreach (int index in cast.Plan.DirectSignalSteps)
        {
            if (!cast.AwaitingStepWorld(index)) continue;
            var step = cast.Plan.Steps[index];
            Actor target = running.Target;
            CastAuthority sender = running.Owner(step.Node);
            if (!cast.IsNodePresent(step.Node) || target?.data?.id != running.TargetId || !target.isAlive() ||
                !target.hasHealth() || !sender.IsLive || !sender.Matches(cast.Plan.Impact.Targets, target))
            {
                cast.RejectStepWorld(index);
                continue;
            }

            StartingPose(target, out Vector3 point, out _);
            var signal = (CompiledConstructionSignal)step.Action.Parameters;
            if (StartingDistance(sender.Actor, point) > Range(sender.Actor.GetExtend(), cast.Plan, target) ||
                !TryRecordSignal(sender, target, signal, signal.Duration))
            {
                cast.RejectStepWorld(index);
                continue;
            }

            cast.ConfirmStepWorld(index, new ConstructionWorldReceipt(ConstructionWorldResultKind.SignalDelivered,
                step.Node, clock, targets: new[] { running.TargetId }, destination: point, signal: signal.Kind, senderId: sender.Id));
            NotifySignal(running, step.Node, sender.Id, running.TargetId, signal.Kind, point, clock);
            if (generation != worldGeneration) return;
        }
    }

    /// <summary>接触与指定共用同一登记。刷新同一发信人的有效讯号不算再次送达。</summary>
    /// <param name="sender">当前发信人。</param><param name="target">实际收件人。</param>
    /// <param name="signal">讯号内容。</param><param name="duration">实际有效时间。</param>
    private static bool TryRecordSignal(CastAuthority sender, Actor target, CompiledConstructionSignal signal, float duration)
    {
        if (!sender.IsLive || duration <= 0f) return false;
        bool alreadyReceived = CombatStatusEffects.TryGetActiveStatus(target, signal.Asset, out var previous) &&
                               previous.GetComponent<StatusComponent>().SourceActorId == sender.Id;
        var received = CombatStatusEffects.ApplyContactStatus(target, signal.Asset, duration, sender.Actor,
            sender.Damage.AttackerPowerLevel ?? 0f, ConstructionStatusApplication.Refresh, null);
        return !received.IsNull && !received.Tags.Has<TagRecycle>() && !alreadyReceived;
    }

    /// <summary>只有新收件事实才驱动到达监听与观察者。</summary>
    /// <param name="running">实际施放。</param><param name="node">发送部位。</param>
    /// <param name="sender">发信编号。</param><param name="target">收件编号。</param>
    /// <param name="kind">实际内容。</param><param name="point">收件位置。</param><param name="clock">确认时点。</param>
    private static void NotifySignal(RunningCast running, int node, long sender, long target, ConstructionSignalKind kind,
        Vector3 point, double clock)
    {
        running.Cast.ConfirmNetworkArrival(node, ConstructionChangeTrigger.SignalDelivery, point);
        SignalReceived?.Invoke(new ConstructedSignalReceipt(running.Id, sender, target, kind, clock));
    }
}

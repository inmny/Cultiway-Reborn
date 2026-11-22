using Cultiway.Core.SkillLibV3.Effects;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>送达的交战位置是当时看到的坐标，不跟踪该人物。</summary>
public readonly struct CombatReportedContact
{
    public readonly long SubjectId;
    public readonly Vector2 Position;
    public readonly double ObservedAt, ExpiresAt;

    internal CombatReportedContact(CombatObservation observation, double expiresAt)
    {
        SubjectId = observation.TargetId;
        Position = observation.LastPosition;
        ObservedAt = observation.LastLocationAt;
        ExpiresAt = expiresAt;
    }
}

public static partial class CombatWorldService
{
    /// <summary>把实际感知接触记入本人观察，记录接触时的方位。</summary>
    /// <param name="observer">接触归属的活人。</param>
    /// <param name="target">真正确认接触的敌人。</param>
    /// <param name="position">接触时的实际脚下位置。</param>
    /// <param name="now">实际确认的世界时点。</param>
    internal static void RecordPerceivedContact(Actor observer, Actor target, Vector2 position, double now)
    {
        var runtime = GetOrCreateActorState(observer);
        CombatObservation observation = CombatObservationService.ObserveVisible(runtime, observer, target, now);
        observation.LastPosition = position;
        runtime.LastConfirmedContact = CombatObservationService.CopyObservation(observation, runtime.LastConfirmedContact);
        runtime.NextPlanAt = 0;
        runtime.TouchRevision();
    }

    /// <summary>只取本人真实交战或感知时冻结的一条记录，收到的传讯不能冒充亲见。</summary>
    internal static CombatObservation ReportableContact(Actor sender, Actor recipient, double now, float duration)
    {
        if (sender == recipient || !SkillTargetRelationResolver.IsFriendly(sender, recipient) ||
            !ActorStates.TryGetValue(sender.id, out var runtime) || runtime.LastConfirmedContact is not { } contact ||
            contact.LastLocationAt + duration <= now || !IsCurrentContact(contact) ||
            !SkillTargetRelationResolver.HasHostileRelation(sender, contact.TargetObject)) return null;
        if (ActorStates.TryGetValue(recipient.id, out var receiver) && receiver.ReportedContact is { } previous &&
            receiver.ReportedUntil > now && previous.TargetId == contact.TargetId && previous.LastLocationAt >= contact.LastLocationAt)
            return null;
        return contact;
    }

    /// <summary>保存给收件人使用；亲见与原有观察独立保留。</summary>
    internal static CombatReportedContact ReportContact(Actor recipient, CombatObservation contact, float duration)
    {
        var runtime = GetOrCreateActorState(recipient);
        runtime.ReportedContact = CombatObservationService.CopyObservation(contact, runtime.ReportedContact);
        runtime.ReportedUntil = contact.LastLocationAt + duration;
        runtime.NextPlanAt = 0;
        runtime.TouchRevision();
        return new CombatReportedContact(contact, runtime.ReportedUntil);
    }

    /// <summary>同一份实际讯号仍在收件人的有效记录内时才保留外观。</summary>
    internal static bool HasReportedContact(Actor recipient, in CombatReportedContact report, double now) =>
        ActorStates.TryGetValue(recipient.id, out var receiver) && receiver.ReportedContact is { } contact &&
        contact.TargetId == report.SubjectId && contact.LastLocationAt == report.ObservedAt &&
        receiver.ReportedUntil == report.ExpiresAt && now < report.ExpiresAt && IsCurrentContact(contact);

    private static bool IsCurrentContact(CombatObservation observation) => observation.TargetObject != null &&
        !observation.TargetObject.isRekt() && observation.TargetObject.getID() == observation.TargetId &&
        observation.TargetObject.isAlive();

    /// <summary>仅当消息比已知方位新，且内容仍有效，才将它交给现有战术采样。</summary>
    private static CombatObservation ReportedFor(CombatActorRuntime runtime, long subjectId, double now)
    {
        if (runtime.ReportedContact is not { } report || report.TargetId != subjectId ||
            now >= runtime.ReportedUntil || !IsCurrentContact(report)) return null;
        return runtime.Observations.TryGetValue(subjectId, out var known) && known.LastLocationAt >= report.LastLocationAt
            ? null : report;
    }
}

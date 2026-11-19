using System.Collections.Generic;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    private readonly List<ObservedSignalVisual> observedSignals = new();
    private const float SignalFlightSeconds = .54f;

    /// <summary>只用一次确已送达的资料建立方位牌；同一收件人保留最后一条。</summary>
    private void RecordObservedSignal(Actor sender, Actor recipient, in CombatReportedContact report, double now)
    {
        for (int i = observedSignals.Count - 1; i >= 0; i--)
            if (observedSignals[i].RecipientId == recipient.id)
            {
                observedSignals.RemoveAt(i);
                break;
            }
        StartingPose(sender, out Vector3 emitter, out float height);
        observedSignals.Add(new ObservedSignalVisual(recipient, report, now,
            emitter + Vector3.forward * Mathf.Max(.75f, height * .78f)));
    }

    /// <summary>发信源、渡过的实体、收件人和旧方位来自同一份有效回执。</summary>
    private void AppendObservedSignals(double now)
    {
        for (int i = observedSignals.Count - 1; i >= 0; i--)
        {
            var signal = observedSignals[i];
            if (signal.Recipient.data?.id != signal.RecipientId || !signal.Recipient.isAlive() ||
                !CombatWorldService.HasReportedContact(signal.Recipient, signal.Report, now))
            {
                observedSignals.RemoveAt(i);
                continue;
            }
            StartingPose(signal.Recipient, out Vector3 receiver, out float height);
            receiver.z += Mathf.Max(.88f, height * .82f);
            Vector2 remembered = signal.Report.Position;
            var marker = new Vector3(remembered.x, remembered.y, (terrain?.Sample(remembered.x, remembered.y) ?? 0f) + .66f);
            float phase = Mathf.Clamp01((float)(now - signal.SentAt) / SignalFlightSeconds);
            float scale = .58f + .42f * Mathf.Clamp01((float)(signal.Report.ExpiresAt - now) / .4f);
            var model = FormResources.Load("signal_sight_tablet");
            Tablet(model, marker, receiver, 1.02f * scale, new Color(.75f, .91f, .98f));
            Tablet(model, receiver, marker, .68f + phase * .44f * scale, new Color(1f, .91f, .70f));
            if (phase >= 1f) continue;
            Tablet(model, signal.Emitter, receiver, .88f - phase * .40f, new Color(.61f, .82f, .95f));
            Vector3 carrier = Vector3.LerpUnclamped(signal.Emitter, receiver, phase);
            carrier.z += Mathf.Sin(phase * Mathf.PI) * 1.10f;
            Tablet(model, carrier, receiver, .57f + Mathf.Sin(phase * Mathf.PI) * .47f, new Color(1f, .75f, .38f));
        }
    }

    private void Tablet(FormResource resource, Vector3 point, Vector3 toward, float scale, Color color)
    {
        float yaw = Mathf.Atan2(toward.y - point.y, toward.x - point.x) * Mathf.Rad2Deg - 90f;
        var orientation = Quaternion.AngleAxis(yaw, Vector3.forward) * Quaternion.AngleAxis(18f, Vector3.right);
        visuals.Add(new FormVisualInstance(resource, Matrix4x4.TRS(point, orientation, Vector3.one * scale),
            tint: color, terrain: terrain));
    }

    private readonly struct ObservedSignalVisual
    {
        internal readonly Actor Recipient;
        internal readonly long RecipientId;
        internal readonly CombatReportedContact Report;
        internal readonly double SentAt;
        internal readonly Vector3 Emitter;

        internal ObservedSignalVisual(Actor recipient, in CombatReportedContact report, double sentAt, Vector3 emitter)
        {
            Recipient = recipient;
            RecipientId = recipient.id;
            Report = report;
            SentAt = sentAt;
            Emitter = emitter;
        }
    }
}

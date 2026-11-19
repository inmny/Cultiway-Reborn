using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    // 短暂余流只记录已经入账的转移，不保留人物引用，也不参与碰撞或存档。
    private static readonly List<YinTransfer> yinTransfers = new(24);

    /// <summary>按实际归属交付灵气，来源在运行中失效时不再收取。</summary>
    /// <param name="recipient">当前可接受灵气的人物归属。</param>
    /// <param name="donor">实际接触且仍有效的人物。</param>
    /// <param name="amount">已经过接触衰减的单次上限。</param>
    private static void TransferContactWakan(CastAuthority recipient, Actor donor, float amount)
    {
        if (!recipient.IsLive) return;
        long generation = worldGeneration;
        Vector3 start = YinEndpoint(donor, .38f), end = YinEndpoint(recipient.Actor, .48f);
        float moved = CombatResourceEffects.TransferWakan(donor, recipient.Actor, amount);
        if (moved <= 0f || generation != worldGeneration) return;
        if (yinTransfers.Count == 24) yinTransfers.RemoveAt(0);
        yinTransfers.Add(new YinTransfer(start, end, SpatialActorPose.Height(donor), moved, simulation.contactTime));
    }

    private static Vector3 YinEndpoint(Actor actor, float height)
    {
        Vector3 p = SpatialActorPose.Foot(actor);
        p.z += (simulation.terrain?.Sample(p.x, p.y) ?? 0f) + SpatialActorPose.Height(actor) * height;
        return p;
    }

    /// <summary>阴膜从实际受术位置收成厚流，向已收受的一端移动并消退。</summary>
    /// <param name="now">当前共同世界时间。</param>
    private void AppendYinTransfers(double now)
    {
        if (yinTransfers.Count == 0) return;
        const float lifetime = .77f;
        FormResource wrap = FormResources.Load("yin_transfer_wrap"), flow = FormResources.Load("yin_transfer_flow");
        for (int i = yinTransfers.Count - 1; i >= 0; i--)
        {
            YinTransfer transfer = yinTransfers[i];
            float phase = (float)((now - transfer.At) / lifetime);
            if (phase >= 1f)
            {
                yinTransfers.RemoveAt(i);
                continue;
            }

            Vector3 delta = transfer.End - transfer.Start;
            Vector3 horizontal = Vector3.Cross(Vector3.forward, delta).normalized;
            float distance = delta.magnitude;
            float turn = Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg;
            if (phase < .57f)
            {
                float growth = Mathf.SmoothStep(.35f, 1f, phase * 7f);
                float leave = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.23f, .57f, phase));
                float width = Mathf.Max(.48f, transfer.Height * .67f) * growth;
                Vector3 scale = new(width * (1f - .45f * leave), width, transfer.Height * (.5f + .2f * growth));
                Matrix4x4 matrix = Matrix4x4.TRS(transfer.Start, Quaternion.Euler(0, 0, turn + 32f * leave), scale);
                visuals.Add(new FormVisualInstance(wrap, matrix, dye: new Color(1f, 1f, 1f, 1f - leave), terrain: terrain));
            }

            if (distance < .03f || phase <= .12f) continue;
            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .87f, phase));
            float back = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.36f, 1f, phase));
            Vector3 Path(float t) => transfer.Start + delta * t + Mathf.Sin(t * Mathf.PI) *
                (horizontal * Mathf.Min(.78f, distance * .18f) + Vector3.forward * .48f);
            Vector3 from = Path(back), to = Path(front), heading = to - from;
            float widthFlow = (.28f + .060f * Mathf.Sqrt(transfer.Amount)) * Mathf.Sin(phase * Mathf.PI);
            Matrix4x4 trail = Matrix4x4.TRS(from, Quaternion.FromToRotation(Vector3.up, heading),
                new Vector3(widthFlow, heading.magnitude, widthFlow));
            visuals.Add(new FormVisualInstance(flow, trail, terrain: terrain));
        }
    }

    private readonly struct YinTransfer
    {
        internal readonly Vector3 Start, End;
        internal readonly float Height, Amount;
        internal readonly double At;

        internal YinTransfer(Vector3 start, Vector3 end, float height, float amount, double at)
        {
            Start = start;
            End = end;
            Height = height;
            Amount = amount;
            At = at;
        }
    }
}

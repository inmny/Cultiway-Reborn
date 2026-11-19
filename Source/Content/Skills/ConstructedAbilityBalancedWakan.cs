using System;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>每对人物只展示最近一段已交付的灵气；换世界清空。</summary>
    private static readonly List<BalancedWakan> balancedWakan = new(24);

    /// <summary>比较真元比例，然后通过既有的扣收路径准确结算，不越过两边相等的状态。</summary>
    /// <param name="owner">当前实际归属者。</param>
    /// <param name="contact">真实接触对象。</param>
    /// <param name="limit">衰减后的本段上限。</param>
    private static void BalanceWakan(Actor owner, Actor contact, float limit)
    {
        if (owner == contact) return;
        var a = owner.GetExtend();
        var b = contact.GetExtend();
        if (!a.HasCultisys<Xian>() || !b.HasCultisys<Xian>()) return;
        double aMax = WakanResourceService.ResolveMaximum(a), bMax = WakanResourceService.ResolveMaximum(b);
        if (aMax <= 0d || bMax <= 0d) return;
        double amount = (bMax * a.GetCultisys<Xian>().wakan - aMax * b.GetCultisys<Xian>().wakan) / (aMax + bMax);
        // 忽略低于灵气余额读数精度的余差，避免两端来回抖动。
        if (Math.Abs(amount) <= .001d) return;
        Actor sender = amount > 0d ? owner : contact;
        Actor receiver = amount > 0d ? contact : owner;
        long generation = worldGeneration, senderId = sender.id, receiverId = receiver.id;
        float moved = CombatResourceEffects.TransferWakan(sender, receiver, Mathf.Min(limit, (float)Math.Abs(amount)));
        if (moved <= 0f || generation != worldGeneration || sender.data?.id != senderId || receiver.data?.id != receiverId) return;

        for (int i = balancedWakan.Count - 1; i >= 0; i--)
        {
            BalancedWakan previous = balancedWakan[i];
            if ((previous.SenderId == senderId && previous.ReceiverId == receiverId) ||
                (previous.SenderId == receiverId && previous.ReceiverId == senderId))
                balancedWakan.RemoveAt(i);
        }
        if (balancedWakan.Count == 24) balancedWakan.RemoveAt(0);
        balancedWakan.Add(new BalancedWakan(sender, receiver, senderId, receiverId, moved, simulation.contactTime));
    }

    /// <summary>暗片向内交付、明片向外承接，实体包裹沿已确定的方向移动。</summary>
    /// <param name="now">当前世界时间。</param>
    private void AppendBalancedWakan(double now)
    {
        if (balancedWakan.Count == 0) return;
        FormResource draw = FormResources.Load("polarity_sender"), receive = FormResources.Load("polarity_receiver"),
            packet = FormResources.Load("polarity_packet");
        for (int i = balancedWakan.Count - 1; i >= 0; i--)
        {
            BalancedWakan flow = balancedWakan[i];
            float phase = (float)((now - flow.At) / .72d);
            if (phase >= 1f || flow.Sender.isRekt() || flow.Receiver.isRekt() ||
                flow.Sender.data?.id != flow.SenderId || flow.Receiver.data?.id != flow.ReceiverId ||
                !flow.Sender.isAlive() || !flow.Receiver.isAlive())
            {
                balancedWakan.RemoveAt(i);
                continue;
            }
            if (phase < 0f) continue;

            Vector3 start = YinEndpoint(flow.Sender, .38f), end = YinEndpoint(flow.Receiver, .4f);
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < .02f) continue;
            Vector3 horizontal = new(delta.x, delta.y, 0f);
            float heading = Mathf.Atan2(-horizontal.x, horizontal.y) * Mathf.Rad2Deg;
            float strength = .9f + .13f * Mathf.Sqrt(Mathf.Min(flow.Amount, 36f));
            float fold = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.04f, .64f, phase));
            float bloom = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.38f, .85f, phase));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.74f, 1f, phase));
            float darkAlpha = (1f - release) * Mathf.Clamp01(phase * 16f);
            float lightAlpha = (1f - release) * Mathf.Lerp(.28f, 1f, bloom) * Mathf.Clamp01(phase * 12f);
            Vector3 drawScale = new(1.2f - .57f * fold, .76f + .38f * fold, .85f - .21f * fold);
            Vector3 receiveScale = new(.58f + .68f * bloom, .64f + .36f * bloom, .53f + .5f * bloom);
            var sending = Matrix4x4.TRS(start, Quaternion.Euler(0f, 0f, heading + 18f * (1f - fold)),
                drawScale * (strength * Mathf.Max(1.15f, SpatialActorPose.Height(flow.Sender))));
            var receiving = Matrix4x4.TRS(end, Quaternion.Euler(0f, 0f, heading + 180f - 22f * (1f - bloom)),
                receiveScale * (strength * Mathf.Max(1.15f, SpatialActorPose.Height(flow.Receiver))));
            visuals.Add(new FormVisualInstance(draw, sending, dye: new Color(1f, 1f, 1f, darkAlpha), terrain: terrain));
            visuals.Add(new FormVisualInstance(receive, receiving, dye: new Color(1f, 1f, 1f, lightAlpha), terrain: terrain));

            float travel = Mathf.InverseLerp(.13f, .87f, phase);
            if (travel <= 0f || travel >= 1f) continue;
            // 弧度与人物距离联动。暗色包片护住金色内芯，前重后轻标明本段流向。
            Vector3 arc = Vector3.forward * Mathf.Min(1.55f, .33f + .16f * distance) +
                          Vector3.Cross(Vector3.forward, horizontal.normalized) * Mathf.Min(.68f, .095f * distance);
            Vector3 point = start + delta * travel + arc * Mathf.Sin(travel * Mathf.PI);
            Vector3 forward = delta + arc * (Mathf.PI * Mathf.Cos(travel * Mathf.PI));
            float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(travel, 1f - travel) * 5f);
            float length = Mathf.Min(2.8f, distance * .42f) * (.75f + .25f * envelope);
            var motion = Matrix4x4.TRS(point, Quaternion.FromToRotation(Vector3.up, forward),
                new Vector3(strength * .8f, length, strength * .8f));
            visuals.Add(new FormVisualInstance(packet, motion, dye: new Color(1f, 1f, 1f, envelope), terrain: terrain));
        }
    }

    /// <summary>本段实际送达记录，让形体跟随原人物且能识别对象复用。</summary>
    private readonly struct BalancedWakan
    {
        /// <summary>原送出人物和原收受人物。</summary>
        internal readonly Actor Sender, Receiver;
        /// <summary>扣收发生时两人的实际编号。</summary>
        internal readonly long SenderId, ReceiverId;
        /// <summary>实际移动的灵气。</summary>
        internal readonly float Amount;
        /// <summary>提交扣收的共同世界时间。</summary>
        internal readonly double At;

        /// <summary>保存一次已发生的交付，不另外扣费或补回。</summary>
        /// <param name="sender">原送出人物。</param><param name="receiver">原收受人物。</param>
        /// <param name="senderId">送出时的身份。</param><param name="receiverId">收受时的身份。</param>
        /// <param name="amount">实际交付量。</param><param name="at">实际时点。</param>
        internal BalancedWakan(Actor sender, Actor receiver, long senderId, long receiverId, float amount, double at)
        {
            Sender = sender;
            Receiver = receiver;
            SenderId = senderId;
            ReceiverId = receiverId;
            Amount = amount;
            At = at;
        }
    }
}

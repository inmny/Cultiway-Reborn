using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>共同时间上的重复接触；不按渲染帧或绘制次数造成伤害。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>已经发生的单个目标接触，反射后的新归属使用另一份记录。</summary>
    private sealed class ContactMemory
    {
        /// <summary>实际次数。</summary>
        internal int Count;
        /// <summary>最后一次确认的施放世界年龄。</summary>
        internal float Age;
        /// <summary>尚未离开同组全部作用面。</summary>
        internal bool Touching;
        /// <summary>本次候选查询是否已经求得重新进入时点。</summary>
        internal long Evaluation;
        /// <summary>本段共同时间内下一次进入的进度。</summary>
        internal float Fraction;
    }

    /// <summary>同组作用面在共同时间内的覆盖区间，求并集时复用。</summary>
    private readonly List<(float From, float To)> contactIntervals = new();
    /// <summary>候选查询编号，避免多部位对同一目标反复求覆盖并集。</summary>
    private long contactEvaluation;

    /// <summary>在已确认起点检查离开，仍接触任一同组部位时不能再次算作进入。</summary>
    /// <param name="running">本次施放及其真实前态。</param>
    /// <param name="clock">共同起点。</param>
    private void RefreshReentry(RunningCast running, double clock)
    {
        RefreshWorldReentry(running);
        if (!running.Cast.Plan.HasReentry) return;
        foreach (var pair in running.Hits)
        {
            ContactMemory memory = pair.Value;
            if (!memory.Touching || running.Cast.Plan.ContactPolicies[pair.Key.Group].Mode != ConstructionContactMode.Reentry) continue;
            memory.Touching = false;
            if (!actorIndices.TryGetValue(pair.Key.Target, out int index)) continue;
            ActorMotion actor = actors[index];
            Sample(actor, clock, out Vector3 foot, out float height);
            AbilityCapsule target = ActorContact(foot, height, actor.Body?.BodyControlled == true ? actor.Body.Cast.BodyPose.Value.Torso : Vector3.zero);
            foreach (AbilityCapsule shape in running.Before)
                if (running.Cast.Role(shape.Node) == ConstructionContactRole.Attack &&
                    running.HitKey(shape.Node, actor.Id) == pair.Key && AbilityPairSweep.Find(shape, shape, target, target, out _, out _, out _))
                {
                    if (shape.Node < 0 && running.BeforeJoint != null && !BodySurfacePose.Find(running.BeforeJoint.Contact,
                        running.BeforeJoint.Contact, target, target, "surface", out _, out _, out _)) continue;
                    memory.Touching = true; break;
                }
        }
    }

    /// <summary>覆盖脚底到头部的实际身高，低矮攻击不再从胸口球体下方漏过。</summary>
    /// <param name="foot">共同空间中的脚下位置。</param>
    /// <param name="height">当前身体高度。</param>
    /// <param name="torso">身体动作已有的整体偏移。</param>
    private static AbilityCapsule ActorContact(Vector3 foot, float height, Vector3 torso)
    {
        float radius = height * .24f;
        return new AbilityCapsule(0, foot + torso + Vector3.forward * radius,
            foot + torso + Vector3.forward * (height - radius), radius, null);
    }

    /// <summary>按已确认次数及年龄裁掉尚不能再次作用的路径。</summary>
    /// <param name="shape">本步作用部位。</param>
    /// <param name="actor">真实目标。</param>
    /// <param name="from">目标在共同起点的作用形状。</param>
    /// <param name="to">目标在共同末端的作用形状。</param>
    /// <param name="step">共同世界步长。</param>
    /// <param name="stage">本次需要确认的身体作用实例。</param>
    /// <param name="earliest">作用窗口内最早允许的路径进度。</param>
    private float ContactFraction(in ShapeMotion shape, in ActorMotion actor, in AbilityCapsule from, in AbilityCapsule to, float step, int stage, float earliest = 0f)
    {
        RunningCast running = shape.Running;
        var key = running.HitKeyAtStage(shape.To.Node, actor.Id, stage);
        CompiledContactPolicy policy = running.Cast.Plan.ContactPolicies[key.Item1];
        running.Hits.TryGetValue(key, out ContactMemory memory);
        if (policy.MaximumPerTarget > 0 && memory?.Count >= policy.MaximumPerTarget) return float.PositiveInfinity;
        if (policy.Mode == ConstructionContactMode.Pulse)
            return PulseFraction(policy.Interval, memory, running.Cast.Age - step, step, earliest);
        if (memory == null) return 0f;
        switch (policy.Mode)
        {
            case ConstructionContactMode.Once: return float.PositiveInfinity;
            case ConstructionContactMode.Interval:
                return Mathf.Max(0f, (memory.Age + policy.Interval - (running.Cast.Age - step)) / step);
            case ConstructionContactMode.Reentry:
                if (!memory.Touching) return 0f;
                if (memory.Evaluation != contactEvaluation)
                {
                    memory.Fraction = NextReentry(running, key, from, to, bodyStage: stage);
                    memory.Evaluation = contactEvaluation;
                }
                return memory.Fraction;
            default: throw new InvalidOperationException("编译后的接触方式无效");
        }
    }

    /// <summary>同一次施放的定时脉冲不随进入时间重排；已确认的整点不能再次消费。</summary>
    /// <param name="interval">共同间隔。</param><param name="memory">已确认接触。</param>
    /// <param name="start">本步施放年龄。</param><param name="step">共同步长。</param>
    /// <param name="earliest">作用窗口的最早进度。</param>
    private static float PulseFraction(float interval, ContactMemory memory, float start, float step, float earliest)
    {
        float tick = Mathf.Max(1f, Mathf.Ceil((start + earliest * step - .000001f) / interval));
        if (memory != null) tick = Mathf.Max(tick, Mathf.Floor((memory.Age + .000001f) / interval) + 1f);
        return Mathf.Max(0f, (tick * interval - start) / step);
    }

    /// <summary>合并同组覆盖区间，只允许真实离开后的下一段进入；分离部位之间的空隙也有效。</summary>
    /// <param name="running">同一次施放。</param>
    /// <param name="key">实际组、归属、反射数与目标。</param>
    /// <param name="from">目标前态。</param>
    /// <param name="to">目标后态。</param>
    /// <param name="onlyNode">世界作用按实际部位查询，缺省按人物接触组查询。</param>
    /// <param name="bodyStage">跨身体作用边界时明确使用原实例，负一沿用当前实例。</param>
    private float NextReentry(RunningCast running, (int, long, int, long) key, in AbilityCapsule from,
        in AbilityCapsule to, int onlyNode = int.MinValue, int bodyStage = -1)
    {
        contactIntervals.Clear();
        foreach (ShapeMotion shape in shapes)
        {
            if (shape.Running != running || (onlyNode == int.MinValue
                    ? running.Cast.Role(shape.To.Node) != ConstructionContactRole.Attack ||
                      (bodyStage < 0 ? running.HitKey(shape.To.Node, key.Item4) : running.HitKeyAtStage(shape.To.Node, key.Item4, bodyStage)) != key
                    : shape.To.Node != onlyNode)) continue;
            AbilityCapsule start = shape.Continuous ? shape.From : shape.To;
            if (shape.To.Node < 0 && running.BeforeJoint != null)
            {
                BodySurfacePose before = shape.Continuous ? running.BeforeJoint.Contact : running.Cast.BodyPose.Value.Joint.Contact;
                BodySurfacePose after = running.Cast.BodyPose.Value.Joint.Contact;
                AbilityCapsule targetBefore = shape.Continuous ? from : to;
                if (BodySurfacePose.Find(before, after, targetBefore, to, "surface", out float jointEnter, out _, out _) &&
                    BodySurfacePose.Find(after, before, to, targetBefore, "surface", out float jointReverse, out _, out _))
                    contactIntervals.Add((jointEnter, Mathf.Min(1f - jointReverse, shape.To.Until)));
                continue;
            }
            if (!shape.Continuous)
            {
                if (AbilityPairSweep.Find(start, start, to, to, out _, out _, out _)) contactIntervals.Add((1f, 1f));
                continue;
            }
            if (AbilityPairSweep.Find(start, shape.To, from, to, out float enter, out _, out _) &&
                AbilityPairSweep.Find(shape.To, start, to, from, out float reverse, out _, out _))
            {
                if (onlyNode == int.MinValue
                        ? start.Intersects(from.A, from.Radius + .00002f)
                        : AbilityPairSweep.Find(start, start, from, from, out _, out _, out _)) enter = 0f;
                contactIntervals.Add((enter, Mathf.Min(1f - reverse, shape.To.Until)));
            }
        }
        contactIntervals.Sort((left, right) => left.From.CompareTo(right.From));
        float covered = 0f;
        foreach (var interval in contactIntervals)
        {
            if (interval.From > covered + .00001f) return interval.From;
            covered = Mathf.Max(covered, interval.To);
        }
        return float.PositiveInfinity;
    }

    /// <summary>先登记实际接触，避免正常受击回调重入时再次消费同一时点。</summary>
    /// <param name="running">本次施放。</param>
    /// <param name="node">实际部位。</param>
    /// <param name="target">实际目标。</param>
    private static ContactMemory RememberContact(RunningCast running, int node, long target)
    {
        var key = running.HitKey(node, target);
        if (!running.Hits.TryGetValue(key, out ContactMemory memory)) running.Hits.Add(key, memory = new ContactMemory());
        memory.Count++; memory.Age = running.Cast.Age; memory.Touching = true;
        running.GroupHits.TryGetValue(key.Item1, out int count); running.GroupHits[key.Item1] = count + 1;
        return memory;
    }

    /// <summary>次数预算由全组共享，不因部位数或反射而复制。</summary>
    /// <param name="running">本次施放。</param>
    /// <param name="node">刚发生接触的部位。</param>
    private static void EndExhaustedContactGroup(RunningCast running, int node)
    {
        int group = running.Cast.ContactGroup(node);
        int maximum = running.Cast.Plan.ContactPolicies[group].MaximumTotal;
        if (maximum > 0 && running.GroupHits[group] >= maximum) running.Cast.EndContactGroup(group);
    }

    /// <summary>从同一线性扫掠截取实际可作用的后段，不另外扩大形状。</summary>
    /// <param name="from">本段起点。</param>
    /// <param name="to">本段末端。</param>
    /// <param name="fraction">截取进度。</param>
    private static AbilityCapsule Slice(in AbilityCapsule from, in AbilityCapsule to, float fraction) =>
        new(to.Node, Vector3.Lerp(from.A, to.A, fraction), Vector3.Lerp(from.B, to.B, fraction),
            Mathf.Lerp(from.Radius, to.Radius, fraction), to.Matter, to.Shape, to.Revision, to.Material, Vector3.Lerp(from.HalfExtents, to.HalfExtents, fraction), to.Until);
}

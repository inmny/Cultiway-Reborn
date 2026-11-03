using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>已真正分配的一段来力，不用命中粒子代替结果。</summary>
public readonly struct ConstructedForceRoute
{
    /// <summary>起点和终点人物；终点零表示实际支撑。</summary>
    public readonly long From, To;

    /// <summary>出口类型。</summary>
    public readonly ConstructionForceDestination Destination;

    /// <summary>世界作用点和实际传力方向。</summary>
    public readonly Vector3 Position, Direction;

    /// <summary>从本段输入中扣除的额度。</summary>
    public readonly float Amount;

    /// <summary>处理来力的身体施放，零表示攻击本身的传力。</summary>
    public readonly long ResponseCast;

    /// <summary>仅在实际出口成立后记录。</summary>
    /// <param name="from">承力人物。</param><param name="to">出口人物。</param><param name="kind">出口类型。</param>
    /// <param name="point">真实接触或支撑点。</param><param name="direction">世界方向。</param>
    /// <param name="amount">实际份额。</param><param name="response">承力施放。</param>
    internal ConstructedForceRoute(long from, long to, ConstructionForceDestination kind, Vector3 point,
        Vector3 direction, float amount, long response)
    {
        From = from;
        To = to;
        Destination = kind;
        Position = point;
        Direction = direction;
        Amount = amount;
        ResponseCast = response;
    }
}

/// <summary>一次真实承力后的余力及运动，不把抗性前请求当成已发生的速度变化。</summary>
public readonly struct ConstructedForceRecipient
{
    /// <summary>实际承力人物。</summary>
    public readonly long ActorId;

    /// <summary>入射与分配后仍由本体承担的世界向量。</summary>
    public readonly Vector3 Incoming, Remaining;

    /// <summary>由该人物承担的标量额度及反向分量相互抵消的部分。</summary>
    public readonly float RetainedAmount, CancelledAmount;

    /// <summary>经过此人物自身抗性后真正增加的世界速度。</summary>
    public readonly Vector3 AppliedImpulse;

    /// <summary>真正受理了非零运动。</summary>
    public readonly bool Applied;

    /// <summary>实际使用的承力动作及前后额度。</summary>
    public readonly long ResponseCast;

    /// <summary>整次动作共用额度。</summary>
    public readonly float CapacityBefore, CapacityAfter;

    /// <summary>运动入口完成后发布。</summary>
    /// <param name="id">人物。</param><param name="incoming">本段输入。</param><param name="remaining">剩余向量。</param>
    /// <param name="amount">剩余额度。</param><param name="applied">实际世界速度。</param><param name="accepted">真实受理。</param>
    /// <param name="response">承力动作。</param><param name="before">原额度。</param><param name="after">现额度。</param>
    internal ConstructedForceRecipient(long id, Vector3 incoming, Vector3 remaining, float amount,
        Vector3 applied, bool accepted, long response, float before, float after)
    {
        ActorId = id;
        Incoming = incoming;
        Remaining = remaining;
        RetainedAmount = amount;
        CancelledAmount = Mathf.Max(0f, amount - remaining.magnitude);
        AppliedImpulse = applied;
        Applied = accepted;
        ResponseCast = response;
        CapacityBefore = before;
        CapacityAfter = after;
    }
}

/// <summary>一次真实接触产生的有限传力链；伤害与状态不会沿链复制。</summary>
public sealed class ConstructedForceTransfer
{
    /// <summary>正式施放和原目标。</summary>
    public long CastId { get; }

    /// <summary>产生接触的真实部位。</summary>
    public int Node { get; }

    /// <summary>共同逻辑时点。</summary>
    public double WorldTime { get; }

    /// <summary>按实际处理顺序排列的出口。</summary>
    public IReadOnlyList<ConstructedForceRoute> Routes { get; }

    /// <summary>每个人物只出现一次。</summary>
    public IReadOnlyList<ConstructedForceRecipient> Recipients { get; }

    /// <summary>实际运动全部提交后发布不可修改的列表。</summary>
    /// <param name="cast">来源施放。</param><param name="node">来源部位。</param><param name="time">真实时点。</param>
    /// <param name="routes">已成立出口。</param><param name="recipients">已结算人物。</param>
    internal ConstructedForceTransfer(long cast, int node, double time, List<ConstructedForceRoute> routes,
        List<ConstructedForceRecipient> recipients)
    {
        CastId = cast;
        Node = node;
        WorldTime = time;
        Routes = routes.AsReadOnly();
        Recipients = recipients.AsReadOnly();
    }
}

/// <summary>只在真实接触确认时分配来力，复用已有空间筛选与人物受力。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>全部实际承力及运动已提交后发布。</summary>
    public static event Action<ConstructedForceTransfer> ForceTransferConfirmed;

    /// <summary>受击前捕获的真实站姿，避免同次伤害解除身体后丢失承力入口。</summary>
    private readonly struct ForceReceiver
    {
        /// <summary>真实人物与接触时的脚下、身体。</summary>
        internal readonly Actor Actor;

        /// <summary>对象池身份。</summary>
        internal readonly long Id;

        /// <summary>同刻脚下和体高。</summary>
        internal readonly Vector3 Foot;

        /// <summary>同刻体高。</summary>
        internal readonly float Height;

        /// <summary>同刻身体姿态，包含真实足底。</summary>
        internal readonly BodyPose? Pose;

        /// <summary>同刻有效承力动作，临时余额属于该次动作。</summary>
        internal readonly RunningCast Response;

        /// <summary>真实身体接触形状。</summary>
        internal AbilityCapsule Shape => ActorContact(Foot, Height, Pose?.Torso ?? Vector3.zero);

        /// <summary>冻结真实时点的身体与承力入口。</summary><param name="actor">当前人物。</param>
        internal ForceReceiver(Actor actor)
        {
            Actor = actor;
            Id = actor.id;
            StartingPose(actor, out Foot, out Height);
            RunningCast body = null;
            foreach (RunningCast cast in casts)
                if (cast.SourceId == Id && cast.BodyControlled && cast.Cast.Plan.Body.ControlsStance)
                {
                    body = cast;
                    break;
                }

            Pose = body?.Cast.BodyPose;
            Response = body?.Cast.Plan.Body.ForceResponse != null &&
                       body.Cast.Age >= body.Cast.Plan.Body.ActiveFrom &&
                       body.Cast.Age <= body.Cast.Plan.Body.ActiveUntil
                ? body
                : null;
        }
    }

    /// <summary>尚未提交的单个人物承力；链在提交前完成，途中不读取已经改变的姿态。</summary>
    private sealed class ForceReceipt
    {
        /// <summary>同刻人物及输入。</summary>
        internal ForceReceiver Receiver;

        /// <summary>本段来力与最终本体向量。</summary>
        internal Vector3 Incoming, Remaining;

        /// <summary>本体额度与承力前后预算。</summary>
        internal float Retained, CapacityBefore, CapacityAfter;

        /// <summary>已经过的连接数。</summary>
        internal int Depth;

        /// <summary>反送到来源后不再继续传播。</summary>
        internal bool Terminal;
    }

    /// <summary>先建立有限链，再逐人物提交实际运动，最后发布包含余力的完整记录。</summary>
    /// <param name="running">真实攻击。</param><param name="node">实际部位。</param><param name="root">原目标受击前身体。</param>
    /// <param name="impact">实际作用。</param><param name="incoming">抗性前世界来力。</param>
    /// <param name="priorVelocity">原目标在同次原版击退之前的运动。</param><param name="applied">原目标是否受理。</param>
    private static Vector3 ApplyRoutedForce(RunningCast running, int node, ForceReceiver root,
        CompiledConstructionImpact impact, Vector3 incoming, Vector3? priorVelocity, out bool applied)
    {
        CastAuthority owner = running.Owner(node);
        CompiledForceRouting initial = impact.ForceRouting ?? root.Response?.Cast.Plan.Body.ForceResponse.Routing;
        int maximumActors = initial?.MaximumActors ?? 1, maximumDepth = initial?.MaximumDepth ?? 0;
        var receipts = new List<ForceReceipt> { new() { Receiver = root, Incoming = incoming } };
        var routes = new List<ConstructedForceRoute>();
        var visited = new HashSet<long> { root.Id };
        var candidates = new List<int>();
        for (int index = 0; index < receipts.Count; index++)
        {
            ForceReceipt receipt = receipts[index];
            float magnitude = receipt.Incoming.magnitude;
            float sent = 0f;
            float redirectedBudget = 0f;
            Vector3 redirected = Vector3.zero;
            RunningCast response = receipt.Terminal ? null : receipt.Receiver.Response;
            receipt.CapacityBefore = response?.ForceResponseRemaining ?? 0f;
            if (response != null && receipt.CapacityBefore > 0f)
            {
                float used = Allocate(response.Cast.Plan.Body.ForceResponse.Routing,
                    Mathf.Min(magnitude, receipt.CapacityBefore), response.Id);
                response.ForceResponseRemaining = Mathf.Max(0f, response.ForceResponseRemaining - used);
            }

            receipt.CapacityAfter = response?.ForceResponseRemaining ?? 0f;
            if (!receipt.Terminal && impact.ForceRouting != null)
                Allocate(impact.ForceRouting, Mathf.Max(0f, magnitude - sent), 0);
            receipt.Retained = Mathf.Max(0f, magnitude - sent);
            receipt.Remaining = receipt.Incoming.normalized * receipt.Retained + redirected;
            // 改向仍是本体承担；只有人物或支撑出口才移走该份额。
            receipt.Retained += redirectedBudget;

            // 分支份额以本轮输入为准；找不到有效出口便不扣本体余力。
            float Allocate(CompiledForceRouting routing, float budget, long responseId)
            {
                float used = 0f;
                foreach (CompiledForceRoute route in routing.Routes)
                {
                    float amount = budget * route.Share;
                    if (amount <= 0f) continue;
                    Vector3 direction = route.WorldDirection(receipt.Incoming);
                    if (route.Destination == ConstructionForceDestination.Body)
                    {
                        redirected += direction * amount;
                        redirectedBudget += amount;
                        Add(receipt.Receiver.Id, receipt.Receiver.Foot, direction, amount);
                    }
                    else if (route.Destination == ConstructionForceDestination.Support)
                    {
                        if (!TryForceSupport(receipt.Receiver, out Vector3 support)) continue;
                        Vector3 center = receipt.Receiver.Pose?.CenterOfMass ??
                                         receipt.Receiver.Foot + Vector3.forward * receipt.Receiver.Height * .5f;
                        Add(0, support, (support - center).normalized, amount);
                    }
                    else
                    {
                        if (receipt.Depth >= Mathf.Min(maximumDepth, routing.MaximumDepth) ||
                            receipts.Count >= Mathf.Min(maximumActors, routing.MaximumActors)) continue;
                        if (route.Destination == ConstructionForceDestination.Source)
                        {
                            // 只有原始身体接触有到来源的真实连接，离体形体没有这条回路。
                            if (index != 0 || node >= 0 || !running.BodyControlled || !owner.IsLive ||
                                visited.Contains(owner.Id)) continue;
                            ForceReceiver source = new(owner.Actor);
                            Vector3 back = (source.Shape.A + source.Shape.B - receipt.Receiver.Shape.A -
                                            receipt.Receiver.Shape.B).normalized;
                            if (route.Direction.HasValue) back = direction;
                            Enqueue(source, back, amount, true);
                        }
                        else
                        {
                            AbilityCapsule shape = receipt.Receiver.Shape;
                            simulation.actorIndex.Query(AbilityContactIndex.Swept(shape, shape), candidates);
                            candidates.Sort((a, b) => simulation.actors[a].Id.CompareTo(simulation.actors[b].Id));
                            var touching = new List<(ForceReceiver Receiver, Vector3 Point)>();
                            foreach (int candidate in candidates)
                            {
                                ActorMotion actor = simulation.actors[candidate];
                                if (visited.Contains(actor.Id) || actor.Id == owner.Id ||
                                    actor.Actor.data?.id != actor.Id ||
                                    !actor.Actor.isAlive() || !actor.Actor.hasHealth()) continue;
                                ForceReceiver next = new(actor.Actor);
                                AbilityCapsule other = next.Shape;
                                Vector3 offset = (other.A + other.B - shape.A - shape.B).normalized;
                                if (Vector3.Dot(direction, offset) + .000001f < route.MinimumDot ||
                                    !AbilityPairSweep.Find(shape, shape, other, other, out _, out Vector3 point, out _))
                                    continue;
                                touching.Add((next, point));
                            }

                            int count = Mathf.Min(touching.Count,
                                Mathf.Min(maximumActors, routing.MaximumActors) - receipts.Count);
                            if (count == 0) continue;
                            for (int i = 0; i < count; i++)
                                Enqueue(touching[i].Receiver, direction, amount / touching.Count, false,
                                    touching[i].Point);
                            continue;
                        }
                    }

                    used += amount;
                    sent += amount;

                    void Add(long to, Vector3 point, Vector3 axis, float value) =>
                        routes.Add(new ConstructedForceRoute(receipt.Receiver.Id, to, route.Destination, point, axis,
                            value, responseId));

                    void Enqueue(ForceReceiver receiver, Vector3 axis, float value, bool terminal,
                        Vector3? point = null)
                    {
                        visited.Add(receiver.Id);
                        receipts.Add(new ForceReceipt
                        {
                            Receiver = receiver, Incoming = axis * value, Depth = receipt.Depth + 1, Terminal = terminal
                        });
                        Add(receiver.Id, point ?? receiver.Foot, axis, value);
                        if (route.Destination == ConstructionForceDestination.Contacts)
                        {
                            used += value;
                            sent += value;
                        }
                    }
                }

                return used;
            }
        }

        var results = new List<ConstructedForceRecipient>();
        Vector3 primaryChange = Vector3.zero;
        applied = false;
        foreach (ForceReceipt receipt in receipts)
        {
            Actor actor = receipt.Receiver.Actor;
            bool live = actor.data?.id == receipt.Receiver.Id && actor.isAlive() && actor.hasHealth();
            Vector3 change = Vector3.zero, actual = Vector3.zero;
            bool accepted = false;
            if (live)
                change = DeliverForce(owner, actor, receipt.Remaining, impact.SlideSeconds,
                    receipt == receipts[0] ? priorVelocity : null, out accepted, out actual);
            results.Add(new ConstructedForceRecipient(receipt.Receiver.Id, receipt.Incoming, receipt.Remaining,
                receipt.Retained, actual, accepted, receipt.Terminal ? 0 : receipt.Receiver.Response?.Id ?? 0,
                receipt.CapacityBefore, receipt.CapacityAfter));
            if (receipt == receipts[0])
            {
                primaryChange = change;
                applied = accepted;
            }
        }

        ForceTransferConfirmed?.Invoke(new ConstructedForceTransfer(running.Id, node, LogicTime, routes, results));
        return primaryChange;
    }

    /// <summary>核对同刻足底确实由共同地表或模型承托，空中不产生卸力出口。</summary>
    /// <param name="receiver">真实承力身体。</param><param name="point">实际落脚点。</param>
    private static bool TryForceSupport(in ForceReceiver receiver, out Vector3 point)
    {
        point = default;
        if (receiver.Actor.isFlying()) return false;
        Vector3[] feet = receiver.Pose.HasValue
            ? new[] { receiver.Pose.Value.LeftFoot, receiver.Pose.Value.RightFoot }
            : new[] { receiver.Foot };
        foreach (Vector3 foot in feet)
        {
            float floor = BodyLocomotion.Floor(simulation.terrain, foot, foot.z + .0001f, receiver.Height * .035f);
            if (Mathf.Abs(foot.z - floor) > .0001f) continue;
            point = new Vector3(foot.x, foot.y, floor);
            return true;
        }

        return false;
    }

    /// <summary>各人物分别经过自身抗性，复用真实受力与保存位置入口。</summary>
    /// <param name="owner">当前攻击来源。</param><param name="target">实际人物。</param>
    /// <param name="requested">抗性前余力。</param><param name="slide">贴地持续时间。</param>
    /// <param name="prior">同次原版击退之前的运动。</param><param name="applied">是否受理非零运动。</param>
    /// <param name="actual">真正增加的世界速度。</param>
    private static Vector3 DeliverForce(CastAuthority owner, Actor target, Vector3 requested, float slide,
        Vector3? prior, out bool applied, out Vector3 actual)
    {
        applied = false;
        actual = Vector3.zero;
        if (target.isImmovable() || !target.asset.can_be_moved_by_powers) return Vector3.zero;
        Vector3 before = target.velocity;
        Vector3 change = requested * target.GetExtend()
            .ForceResponseScale(owner.IsLive ? owner.Actor : null, owner.Damage.AttackerPowerLevel);
        Vector3 baseline = prior ?? ConstructedActorImpulse.Velocity(target);
        if (change != Vector3.zero) InterruptContactBody(target);
        if (change != Vector3.zero || prior.HasValue && target.under_forces)
            ConstructedActorImpulse.BeginDirected(target, baseline + change, change.z == 0f ? slide : 0f,
                simulation.terrain);
        if (change != Vector3.zero)
        {
            Cultiway.Content.Persistence.ActorPositionSaveService.Remember(target.GetExtend());
            applied = true;
            actual = ConstructedActorImpulse.Velocity(target) - baseline;
        }

        return target.velocity - before;
    }
}
using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>本步有可用格挡面时才为来招查询额外候选。</summary>
    private bool hasBodyGuardShapes;

    /// <summary>共用空间查询，但不覆盖正在迭代的其他候选。</summary>
    private readonly List<int> bodyGuardCandidates = new();

    /// <summary>从真实运动与动作窗口中，选择来向正确的敌对防御面。</summary>
    /// <param name="attack">已经具备攻击作用的真实部位。</param><param name="step">共同预测秒数。</param>
    /// <param name="include">把成立的候选交给共同时间排序。</param>
    private void FindBodyGuards(in ShapeMotion attack, float step, Action<ContactChoice> include)
    {
        if (singleActingNode) return;
        shapeIndex.Query(attack.Bounds, bodyGuardCandidates);
        CastAuthority owner = attack.Running.Owner(attack.To.Node);
        foreach (int index in bodyGuardCandidates)
        {
            ShapeMotion defense = shapes[index];
            RunningCast guard = defense.Running;
            if (defense.To.Node >= 0 || guard.BodyGuardRemaining <= 0f || guard == attack.Running ||
                !guard.BodyControlled || !owner.IsHostileTo(guard.Owner(-1)) && !guard.Owner(-1).IsHostileTo(owner)) continue;
            var cast = guard.Cast;
            bool changed = cast.ContactScopeChanged(-1);
            if (changed) AddBodyGuardCandidate(attack, defense, cast.PreviousBodyStage, step, true, false, include);
            AddBodyGuardCandidate(attack, defense, cast.BodyStage, step, false, changed, include);
        }
    }

    /// <summary>仅在限定的动作时段内扫掠真实形状，并用接触处的真实来向核对正面。</summary>
    /// <param name="attack">来袭形状。</param><param name="defense">格挡形状。</param><param name="stage">实际防御阶段。</param>
    /// <param name="step">本步长度。</param><param name="oldScope">边界前的旧窗口。</param>
    /// <param name="endpointOnly">新窗口只接受实际边界。</param><param name="include">共同候选入口。</param>
    private static void AddBodyGuardCandidate(in ShapeMotion attack, in ShapeMotion defense, int stage, float step,
        bool oldScope, bool endpointOnly, Action<ContactChoice> include)
    {
        RunningCast guard = defense.Running;
        if (!guard.Cast.ContactInterval(-1, stage, out float from, out float until)) return;
        if (endpointOnly || !defense.Continuous || !attack.Continuous) from = 1f;
        if (from > until) return;
        var start = Slice(attack.From, attack.To, from);
        var shield = Slice(defense.From, defense.To, from);
        if (!AbilityPairSweep.Find(start, attack.To, shield, defense.To, out float at, out Vector3 point, out Vector3 normal)) return;
        float fraction = Mathf.Lerp(from, 1f, at);
        if (fraction > until || oldScope && fraction >= 1f) return;
        Vector3 end = guard.Body.GuardNormal(guard.Cast.BodyPose.Value);
        Vector3 facing = Vector3.Slerp(guard.BeforeGuardNormal, end, fraction).normalized;
        Vector3 incoming = GuardIncoming(attack, fraction, point);
        if (!(Vector3.Dot(facing, -incoming.normalized) >= guard.Cast.Plan.Body.Guard.MinimumDot)) return;
        include(new ContactChoice(attack, default, defense, fraction, point, normal, step, bodyGuardFacing: facing));
    }

    /// <summary>射线读沿线朝向；移动或转动的形体读接触点的运动，不用施放者当前位置猜来向。</summary>
    /// <param name="attack">已经取得实际前后姿态的来招。</param><param name="at">接触进度。</param>
    /// <param name="point">实际接触位置。</param>
    private static Vector3 GuardIncoming(in ShapeMotion attack, float at, Vector3 point)
    {
        var from = attack.From;
        var to = attack.To;
        var shape = Slice(from, to, at);
        if (attack.Running.Cast.IsRay(to.Node)) return shape.B - shape.A;
        if (!attack.Continuous) return Vector3.zero;
        var turn = new AbilitySweepTurn(from.SweepPose, to.SweepPose);
        if (turn.Active)
        {
            var pose = shape.SweepPose.Value;
            var start = from.SweepPose.Value;
            var end = to.SweepPose.Value;
            Vector3 local = Quaternion.Inverse(pose.Rotation) * (point - pose.Pivot);
            return turn.Velocity(start.Pivot + start.Rotation * local, end.Pivot + end.Rotation * local, at);
        }
        Vector3 axis = shape.B - shape.A;
        float along = axis.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(point - shape.A, axis) / axis.sqrMagnitude) : 0f;
        return Vector3.Lerp(to.A - from.A, to.B - from.B, along);
    }

    /// <summary>只对真正拦住的来招扣除双方余量；不伪造人物命中。</summary>
    /// <param name="hit">共同时间选出的真实格挡。</param>
    private static void ResolveBodyGuard(in ContactChoice hit)
    {
        var attack = hit.Left.Running;
        int node = hit.Left.To.Node;
        var defense = hit.Right.Value;
        var guard = defense.Running;
        if (!guard.BodyControlled || !guard.Body.IsValid(guard.Source)) return;
        float before = attack.Cast.RemainingForce(node), guardBefore = guard.BodyGuardRemaining;
        float spent = Mathf.Min(before, guardBefore);
        if (spent <= 0f) return;
        guard.BodyGuardRemaining = guardBefore - spent;
        if (hit.LeftRayLength >= 0f && spent >= before) attack.Cast.ClipRay(node, hit.LeftRayLength);
        attack.Cast.ConfirmClash(node, hit.Point, spent);
        guard.Cast.ConfirmClash(-1, hit.Point, spent);
        attack.Cast.ConsumeForce(node, spent);
        ConfirmBodyFeedback(guard, hit.Point);
        if (guard.BodyGuardRemaining <= 0f) RespondToBodyResult(guard, BodyActionResult.Blocked);
        float after = attack.Cast.RemainingForce(node);
        ClashConfirmed?.Invoke(new ConstructedClash(attack.Id, guard.Id, attack.Owner(node).Id, guard.Owner(-1).Id,
            node, -1, hit.Point, hit.Normal, LogicTime, attack.Cast.Age, guard.Cast.Age, spent,
            after, guard.BodyGuardRemaining, attack.SourceId, guard.SourceId, before - after,
            guardBefore - guard.BodyGuardRemaining, ContactPartPresent(attack, node), guard.BodyControlled,
            hit.BodyGuardFacing, defense.To.Shape));
    }
}

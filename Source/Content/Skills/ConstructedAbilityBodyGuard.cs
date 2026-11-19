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
    /// <param name="attack">已经具备攻击作用的真实部位。</param><param name="touchFrom">来势可用的真实攻击路径起点。</param><param name="step">共同预测秒数。</param>
    /// <param name="include">把成立的候选交给共同时间排序。</param>
    private void FindBodyGuards(in ShapeMotion attack, float step, float touchFrom, Action<ContactChoice> include)
    {
        if (singleActingNode) return;
        RunningCast striker = attack.Running;
        bool body = attack.To.Node < 0;
        if (body && (!striker.Cast.Plan.Body.Construction.HasTouchRelations ||
                     striker.WeaponConstraint?.Active == true || striker.Connection?.Active == true && striker.Connection.WeaponOnly)) return;
        bool attackChanged = body && striker.Cast.ContactScopeChanged(-1);
        shapeIndex.Query(attack.Bounds, bodyGuardCandidates);
        CastAuthority owner = attack.Running.Owner(attack.To.Node);
        foreach (int index in bodyGuardCandidates)
        {
            ShapeMotion defense = shapes[index];
            RunningCast guard = defense.Running;
            if (defense.To.Node >= 0 || guard.BodyGuardRemaining <= 0f || guard == attack.Running ||
                !guard.BodyControlled || !owner.IsHostileTo(guard.Owner(-1)) && !guard.Owner(-1).IsHostileTo(owner) ||
                !TouchPath(defense, step, out float defenseFrom)) continue;
            var cast = guard.Cast;
            bool changed = cast.ContactScopeChanged(-1);
            for (int a = attackChanged ? 1 : 0; a >= 0; a--)
            {
                int attackStage = a == 1 ? striker.Cast.PreviousBodyStage : striker.Cast.BodyStage;
                float from = 0f, until = 1f;
                if (body && (BodyStrikeForce(striker, attackStage) <= 0f ||
                             !striker.Cast.ContactInterval(-1, attackStage, out from, out until))) continue;
                from = Mathf.Max(from, Mathf.Max(touchFrom, defenseFrom));
                if (attackChanged && a == 0) from = 1f;
                if (changed) AddBodyGuardCandidate(attack, defense, cast.PreviousBodyStage, step, from, until, true, a == 1, include);
                AddBodyGuardCandidate(attack, defense, cast.BodyStage, step, changed ? 1f : from, until, false, a == 1, include);
            }
        }
    }

    /// <summary>仅在限定的动作时段内扫掠真实形状，并用接触处的真实来向核对正面。</summary>
    /// <param name="attack">来袭形状。</param><param name="defense">格挡形状。</param><param name="stage">实际防御阶段。</param>
    /// <param name="step">本步长度。</param><param name="first">来势可接触的起点。</param>
    /// <param name="last">来势可接触的终点。</param><param name="oldGuard">动作边界前的旧格挡。</param>
    /// <param name="oldAttack">动作边界前的旧来招。</param><param name="include">共同候选入口。</param>
    private static void AddBodyGuardCandidate(in ShapeMotion attack, in ShapeMotion defense, int stage, float step,
        float first, float last, bool oldGuard, bool oldAttack, Action<ContactChoice> include)
    {
        RunningCast guard = defense.Running;
        if (!guard.Cast.ContactInterval(-1, stage, out float from, out float until)) return;
        from = Mathf.Max(first, from);
        until = Mathf.Min(last, until);
        if (!defense.Continuous || !attack.Continuous) from = 1f;
        if (from > until) return;
        var start = Slice(attack.From, attack.To, from);
        var shield = Slice(defense.From, defense.To, from);
        if (!AbilityPairSweep.Find(start, attack.To, shield, defense.To, out float at, out Vector3 point, out Vector3 normal)) return;
        float fraction = Mathf.Lerp(from, 1f, at);
        if (fraction > until || (oldGuard || oldAttack) && fraction >= 1f) return;
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

    /// <summary>近身来势使用当前动作实际伤害倍率，再扣除这段已经被拦掉的比例。</summary>
    /// <param name="running">具备可用接触面的真实近身来招。</param><param name="stage">实际攻击段。</param>
    private static float BodyStrikeForce(RunningCast running, int stage) => BodyCorrespondenceForce(running) *
        running.Cast.ImpactAtStage(-1, stage).DamageScale * running.BodyStrikeScale(stage);

    /// <summary>只对真正拦住的来招扣除双方余量；不伪造人物命中。</summary>
    /// <param name="hit">共同时间选出的真实格挡。</param>
    private static void ResolveBodyGuard(in ContactChoice hit)
    {
        var attack = hit.Left.Running;
        int node = hit.Left.To.Node;
        var defense = hit.Right.Value;
        var guard = defense.Running;
        if (!guard.BodyControlled || !guard.Body.IsValid(guard.Source) || node < 0 && !attack.BodyControlled) return;
        int stage = attack.Cast.BodyStage;
        float before = node < 0 ? BodyStrikeForce(attack, stage) : attack.Cast.RemainingForce(node);
        float guardBefore = guard.BodyGuardRemaining;
        float spent = Mathf.Min(before, guardBefore);
        if (spent <= 0f) return;
        guard.BodyGuardRemaining = guardBefore - spent;
        if (hit.LeftRayLength >= 0f && spent >= before) attack.Cast.ClipRay(node, hit.LeftRayLength);
        attack.Cast.ConfirmClash(node, hit.Point, spent);
        guard.Cast.ConfirmClash(-1, hit.Point, spent);
        float after;
        if (node < 0)
        {
            after = before - spent;
            attack.ReduceBodyStrike(stage, after / before);
            ConfirmBodyFeedback(attack, hit.Point);
            if (after == 0f && !RespondToBodyResult(attack, BodyActionResult.Blocked)) attack.Cast.ExhaustBodyStage();
        }
        else
        {
            attack.Cast.ConsumeForceAtBodyGuard(node, spent);
            after = attack.Cast.RemainingForce(node);
        }
        ConfirmBodyFeedback(guard, hit.Point);
        if (guard.BodyGuardRemaining <= 0f) RespondToBodyResult(guard, BodyActionResult.Blocked);
        ClashConfirmed?.Invoke(new ConstructedClash(attack.Id, guard.Id, attack.Owner(node).Id, guard.Owner(-1).Id,
            node, -1, hit.Point, hit.Normal, LogicTime, attack.Cast.Age, guard.Cast.Age, spent,
            after, guard.BodyGuardRemaining, attack.SourceId, guard.SourceId, before - after,
            guardBefore - guard.BodyGuardRemaining, ContactPartPresent(attack, node), guard.BodyControlled,
            hit.BodyGuardFacing, defense.To.Shape));
    }
}

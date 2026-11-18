using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>身体与实际持物沿共同接触面击中映像，再向原人物传递有限伤害。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>身体查询中的形体候选，与接触间隔查询分开复用。</summary>
    private readonly List<int> bodyCorrespondenceCandidates = new();

    /// <summary>这一身体按当前原攻击力发出的单次作用量，费用仍沿原战技入口。</summary>
    /// <param name="running">已取得真实身体的施放。</param>
    private static float BodyCorrespondenceForce(RunningCast running) =>
        Mathf.Max(0f, running.Source.stats["damage"] * running.Power);

    /// <summary>身体及借用兵器沿共同表面、作用窗口和接触限额寻找真实映像。</summary>
    /// <param name="shape">身体前后实际作用面。</param><param name="clock">共同起点。</param>
    /// <param name="step">本轮世界步长。</param><param name="include">共同最早接触候选。</param>
    private void FindBodyCorrespondences(ShapeMotion shape, double clock, float step, Action<ContactChoice> include)
    {
        RunningCast running = shape.Running;
        if (singleActingNode || !running.Cast.Plan.Body.Construction.HasTouchRelations || BodyCorrespondenceForce(running) <= 0f ||
            running.WeaponConstraint?.Active == true || running.Connection?.Active == true && running.Connection.WeaponOnly) return;
        shapeIndex.Query(shape.Bounds, bodyCorrespondenceCandidates);
        foreach (int candidate in bodyCorrespondenceCandidates)
        {
            ShapeMotion image = shapes[candidate];
            Actor receiver = CorrespondenceReceiver(image.Running, image.To.Node, current: false);
            if (receiver == null || !image.Running.Cast.CorrespondenceInterval(image.To.Node, out float imageFromTime, out float imageUntil) ||
                !actorIndices.TryGetValue(receiver.id, out int actorIndex)) continue;
            var relation = image.Running.Cast.Plan.Nodes[image.To.Node].Correspondence;
            ActorMotion actor = actors[actorIndex];
            Sample(actor, clock, out Vector3 fromFoot, out float fromHeight);
            Sample(actor, clock + step, out Vector3 toFoot, out float toHeight);
            AbilityCapsule originalFrom = ActorHitBoundary.Create(fromFoot, fromHeight,
                actor.Body?.BodyControlled == true ? actor.Body.BeforeTorso : Vector3.zero);
            AbilityCapsule originalTo = ActorHitBoundary.Create(toFoot, toHeight,
                actor.Body?.BodyControlled == true ? actor.Body.Cast.BodyPose.Value.Torso : Vector3.zero);
            bool changed = running.Cast.ContactScopeChanged(-1);
            if (changed) AtStage(running.Cast.PreviousBodyStage, true, false);
            AtStage(running.Cast.BodyStage, false, changed);

            /// <summary>跨身体阶段时只接受该阶段真实拥有的路径。</summary>
            /// <param name="stage">作用实例。</param><param name="oldScope">边界之前的旧作用。</param>
            /// <param name="endpointOnly">新作用只接受边界起点。</param>
            void AtStage(int stage, bool oldScope, bool endpointOnly)
            {
                var impact = running.Cast.ImpactAtStage(-1, stage);
                if (!CorrespondenceAllows(running, -1, impact, relation, receiver) ||
                    !running.Cast.ContactInterval(-1, stage, out float from, out float until)) return;
                float first = Mathf.Max(from, imageFromTime);
                float earliest = Mathf.Max(first, ContactFraction(shape, actor, originalFrom, originalTo, step, stage, first));
                if (earliest > 1f || earliest > until || earliest >= imageUntil) return;
                bool continuous = shape.Continuous && image.Continuous && !endpointOnly;
                bool pulse = running.Cast.Plan.ContactPolicies[running.Cast.ContactGroup(-1)].Mode == ConstructionContactMode.Pulse;
                if (pulse && !continuous && earliest < 1f - .000001f) return;
                if (!continuous) earliest = 1f;
                AbilityCapsule imageFrom = Slice(image.From, image.To, earliest);
                float at;
                Vector3 point, normal;
                if (running.Cast.Plan.Body.Part.IsJoint)
                {
                    BodySurfacePose end = running.Cast.BodyPose.Value.Joint.Contact;
                    BodySurfacePose start = continuous ? BodySurfacePose.Lerp(running.BeforeJoint.Contact, end, earliest) : end;
                    if (pulse) end = start;
                    if (!BodySurfacePose.Find(start, end, imageFrom, pulse ? imageFrom : image.To, "surface", out at, out var anchor, out normal)) return;
                    point = anchor.Point(BodySurfacePose.Lerp(start, end, at));
                    normal = -normal;
                }
                else
                {
                    AbilityCapsule start = Slice(shape.From, shape.To, earliest);
                    if (!AbilityPairSweep.Find(start, pulse ? start : shape.To, imageFrom, pulse ? imageFrom : image.To, out at, out point, out normal)) return;
                }
                float fraction = Mathf.Lerp(earliest, 1f, at);
                if (fraction > until || fraction >= imageUntil || oldScope && fraction >= 1f) return;
                include(new ContactChoice(shape, default, image, fraction, point, normal, step));
            }
        }
    }

    /// <summary>身体沿真实交锋扣除映像余量，原近战结算完成后进入对应的连续收势。</summary>
    /// <param name="hit">已经回到真实共同时间的表面接触。</param>
    private static void ResolveBodyCorrespondence(in ContactChoice hit)
    {
        ShapeMotion attack = hit.Left.To.Node < 0 ? hit.Left : hit.Right.Value;
        ShapeMotion image = hit.Left.To.Node < 0 ? hit.Right.Value : hit.Left;
        RunningCast running = attack.Running;
        Actor receiver = CorrespondenceReceiver(image.Running, image.To.Node);
        if (!running.BodyControlled || receiver == null || !CorrespondenceAllows(attack, image)) return;
        long leftOwner = hit.Left.Running.Owner(hit.Left.To.Node).Id, rightOwner = hit.Right.Value.Running.Owner(hit.Right.Value.To.Node).Id;
        float force = BodyCorrespondenceForce(running) * running.BodyStrikeScale(running.Cast.BodyStage), imageBefore = image.Running.Cast.RemainingForce(image.To.Node);
        float consumed = Mathf.Min(force, imageBefore);
        if (consumed <= 0f) return;
        running.Cast.ConfirmClash(-1, hit.Point, consumed);
        image.Running.Cast.ConfirmClash(image.To.Node, hit.Point, consumed);
        image.Running.Cast.ConsumeForce(image.To.Node, consumed);
        float imageAfter = image.Running.Cast.RemainingForce(image.To.Node);
        long generation = worldGeneration;
        ApplyAttackState result = ApplyCorrespondence(attack, image, receiver, consumed, hit.Point, contactConfirmed: true);
        if (generation != worldGeneration) return;
        ConfirmBodyFeedback(running, hit.Point);
        bool blocked = image.Running.Cast.RemainingForce(image.To.Node) > 0f || result is ApplyAttackState.Block or ApplyAttackState.Deflect;
        RespondToBodyResult(running, blocked ? BodyActionResult.Blocked : BodyActionResult.Contact);
        bool leftBody = hit.Left.To.Node < 0;
        ShapeMotion right = hit.Right.Value;
        ClashConfirmed?.Invoke(new ConstructedClash(hit.Left.Running.Id, right.Running.Id, leftOwner, rightOwner,
            hit.Left.To.Node, right.To.Node, hit.Point, hit.Normal, LogicTime, hit.Left.Running.Cast.Age,
            right.Running.Cast.Age, consumed, leftBody ? force - consumed : imageAfter,
            leftBody ? imageAfter : force - consumed, hit.Left.Running.SourceId, right.Running.SourceId,
            leftBody ? consumed : imageBefore - imageAfter, leftBody ? imageBefore - imageAfter : consumed,
            ContactPartPresent(hit.Left.Running, hit.Left.To.Node), ContactPartPresent(right.Running, right.To.Node)));
    }
}

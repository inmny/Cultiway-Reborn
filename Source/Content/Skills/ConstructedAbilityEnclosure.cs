using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>只有声明围合的施放才采集实体形状，复用当前确认线程的缓冲。</summary>
    private readonly List<AbilityCapsule> enclosureShapes = new();

    /// <summary>实际作用面在人物高度的闭合检查。</summary>
    private readonly AbilityEnclosure enclosure = new();

    /// <summary>先收集确认步末仍存在的实体；只对在真实闭环内的人物走普通状态与抗性结算。</summary>
    /// <param name="running">已完成本小步的施放。</param><param name="time">确认时点。</param>
    private void ConfirmEnclosures(RunningCast running, double time)
    {
        int[] nodes = running.Cast.Plan.EnclosureNodes;
        if (nodes.Length == 0 || running.Practice || running.Cast.Finished) return;
        enclosureShapes.Clear();
        running.Cast.AppendContacts(enclosureShapes);
        foreach (int node in nodes)
        {
            if (running.Cast.RemainingForce(node) <= 0f) continue;
            var owner = running.Owner(node);
            if (!owner.IsLive) continue;
            Bounds bounds = default;
            bool found = false;
            foreach (var shape in enclosureShapes)
            {
                if (shape.Node != node || shape.Material?.State != "solid") continue;
                Bounds part = AbilityContactIndex.Swept(shape, shape);
                if (!found) { bounds = part; found = true; }
                else bounds.Encapsulate(part);
            }
            if (!found) continue;
            actorIndex.Query(bounds, nearby);
            var effect = running.Cast.Impact(node);
            foreach (int index in nearby)
            {
                var actor = actors[index];
                if (!actor.Actor.isAlive() || running.EnclosedActors?.Contains((node, actor.Id)) == true ||
                    effect.SelectedTargetOnly && owner.TargetId != actor.Id || !owner.Matches(effect.Targets, actor.Actor)) continue;
                Sample(actor, time, out Vector3 foot, out float height);
                Vector3 point = foot + Vector3.forward * (height * .48f);
                if (!bounds.Contains(point) || !enclosure.Contains(enclosureShapes, node, point)) continue;
                (running.EnclosedActors ??= new()).Add((node, actor.Id));
                ApplyStatuses(running, node, actor.Actor, effect.Statuses);
            }
        }
    }
}

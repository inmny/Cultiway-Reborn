using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际人物接触后的有限选敌；不在工作线程读取人物，也不扫描整张世界。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>先读取本次已确认接触，再查询附近未命中人物，最后准备普通事件分支。</summary>
    /// <param name="running">实际施放。</param><param name="node">刚命中的普通部位，身体为负一。</param>
    /// <param name="contact">已经确认的真实人物。</param>
    private void SelectContactTargets(RunningCast running, int node, in ActorMotion contact)
    {
        var plan = running.Cast.Plan;
        if (plan.EventListeners.Length == 0 || node < 0) return;
        int bucket = node * CompiledConstructionCondition.EventCount + (int)ConstructionChangeTrigger.ActorContact - 1;
        Sample(contact, contactTime, out Vector3 centre, out _);
        CastAuthority owner = running.Owner(node);
        foreach (int branch in plan.EventListeners[bucket])
        {
            if (!running.Cast.AwaitsContactTarget(branch, node)) continue;
            var selection = plan.Nodes[branch].Activation.Target;
            float nearest = selection.Radius * selection.Radius;
            Actor next = null; Vector3 aim = default;
            // 索引先按地图范围筛选；高度不限制候选，与原人物间跳转的平面距离一致。
            actorIndex.QueryPlane(new Vector2(centre.x, centre.y), selection.Radius, nearby);
            foreach (int index in nearby)
            {
                ActorMotion candidate = actors[index];
                if (candidate.Actor.data?.id != candidate.Id || !candidate.Actor.isAlive() || !candidate.Actor.hasHealth() ||
                    !owner.Matches(running.Cast.Impact(branch).Targets, candidate.Actor) ||
                    running.Hits.ContainsKey((running.Cast.ContactGroup(branch), owner.Id, running.Cast.Reflections(node), candidate.Id))) continue;
                Sample(candidate, contactTime, out Vector3 foot, out float height);
                float distance = (new Vector2(foot.x - centre.x, foot.y - centre.y)).sqrMagnitude;
                if (distance > nearest || distance == nearest && next != null && candidate.Id >= next.id) continue;
                nearest = distance; next = candidate.Actor; aim = foot + Vector3.forward * height * .5f;
            }
            if (next == null) { running.Cast.EndNode(branch); continue; }
            running.Cast.SelectContactTarget(branch, node, aim);
            running.Owners[branch] = new CastAuthority(owner, next);
            running.HasTransfers = true;
        }
    }
}

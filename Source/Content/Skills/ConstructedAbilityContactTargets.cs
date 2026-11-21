using Cultiway.Core.Skills;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际人物接触后的有限选敌；不在工作线程读取人物，也不扫描整张世界。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>从已经确认的接触处选择尚未命中的最近对象，准备实际事件分支。</summary>
    /// <param name="running">实际施放。</param><param name="node">刚命中的普通部位，身体为负一。</param>
    /// <param name="centre">这次目标脚下的实际位置。</param>
    private void SelectContactTargets(RunningCast running, int node, Vector3 centre)
    {
        var plan = running.Cast.Plan;
        if (plan.EventListeners.Length == 0 || node < 0) return;
        int bucket = node * CompiledConstructionCondition.EventCount + (int)ConstructionChangeTrigger.ActorContact - 1;
        CastAuthority owner = running.Owner(node);
        foreach (int branch in plan.EventListeners[bucket])
        {
            if (!running.Cast.AwaitsContactTarget(branch, node)) continue;
            var selection = plan.Nodes[branch].Activation.Target;
            float nearest = selection.Radius * selection.Radius;
            Actor next = null;
            long buildingId = 0;
            Vector3 aim = default;
            int group = running.Cast.ContactGroup(branch), reflections = running.Cast.Reflections(node);
            // 索引先按地图范围筛选；高度不限制候选，与原人物间跳转的平面距离一致。
            actorIndex.QueryPlane(new Vector2(centre.x, centre.y), selection.Radius, nearby);
            foreach (int index in nearby)
            {
                ActorMotion candidate = actors[index];
                if (candidate.Actor.data?.id != candidate.Id || !candidate.Actor.isAlive() || !candidate.Actor.hasHealth() ||
                    !owner.Matches(running.Cast.Impact(branch).Targets, candidate.Actor) ||
                    running.Hits.ContainsKey((group, owner.Id, reflections, candidate.Id))) continue;
                Sample(candidate, contactTime, out Vector3 foot, out float height);
                float distance = (new Vector2(foot.x - centre.x, foot.y - centre.y)).sqrMagnitude;
                if (distance > nearest || distance == nearest && next != null && candidate.Id >= next.id) continue;
                nearest = distance; next = candidate.Actor; aim = foot + Vector3.forward * height * .5f;
            }
            if (running.BuildingContact is { ChainStartRadius: > 0f })
            {
                Actor caster = owner.IsLive ? owner.Actor : null;
                foreach (BaseSimObject candidate in SkillUtils.IterEnemyInSphere(centre, selection.Radius, caster, owner.AttackKingdom()))
                {
                    if (!candidate.isBuilding() || candidate.isRekt()) continue;
                    long id = candidate.getID();
                    if (running.Hits.ContainsKey((group, owner.Id, reflections, -id))) continue;
                    float distance = (candidate.current_position - (Vector2)centre).sqrMagnitude;
                    if (distance > nearest || distance == nearest && buildingId != 0 && id >= buildingId) continue;
                    nearest = distance;
                    next = null;
                    buildingId = id;
                    aim = candidate.GetSimPos();
                    aim.z += terrain?.Sample(aim.x, aim.y) ?? 0f;
                }
            }
            if (next == null && buildingId == 0) { running.Cast.EndNode(branch); continue; }
            if (buildingId != 0)
                (running.ChainBuildings ??= new()).Add(branch, buildingId);
            running.Cast.SelectContactTarget(branch, node, aim);
            running.Owners[branch] = new CastAuthority(owner, next);
            running.HasTransfers = true;
        }
    }
}

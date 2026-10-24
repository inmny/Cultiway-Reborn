using System;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>编译时计算候选选取的保守可达包络；实际命中始终读取共同作用形状。</summary>
public sealed class ConstructionReach
{
    /// <summary>实际形状、布局和源身体之外的最大投送路程。</summary>
    public float Extent { get; }
    /// <summary>根布局是否按施法者体高缩放。</summary>
    private readonly bool scaleByBody, fromHand, targetPlaced, targetPath;
    /// <summary>作者明确选择的投送距离，空值才采用结构推导。</summary>
    private readonly float? declared;
    /// <summary>根身体可达包络。</summary>
    private readonly float body, hand;
    /// <summary>布局随体高变化，独立飞行仍使用世界速度，两者不能一起乘体高。</summary>
    private readonly Vector2[] parts;

    /// <summary>只读取已展开数值，先算控制与布局，再算各部位的运动包络。</summary>
    /// <param name="plan">同一版本计划。</param>
    internal ConstructionReach(CompiledAbility plan)
    {
        declared = plan.TargetRange; body = plan.Body?.ContactReach ?? 0f; hand = plan.Body?.HandReach ?? 0f;
        scaleByBody = plan.Binding?.ScaleByBody == true; fromHand = plan.Binding?.Anchor == AbilityAnchor.Hand;
        targetPlaced = plan.FormCount > 0 && (plan.Binding == null || plan.Binding.Anchor == AbilityAnchor.Target);
        targetPath = plan.NeedsPathTarget;
        parts = new Vector2[plan.Nodes.Length];
        var scales = new float[plan.Nodes.Length]; var offsets = new float[plan.Nodes.Length]; var travel = new float[plan.Nodes.Length]; var speeds = new float[plan.Nodes.Length];
        var motions = plan.Nodes.Select(node => node.EventOperations.Select(operation => operation.Motion).Append(node.Motion).Where(motion => motion != null).Distinct().ToArray()).ToArray();
        for (int i = 0; i < plan.Nodes.Length; i++)
        {
            CompiledForm node = plan.Nodes[i]; int parent = node.Parent;
            float inherited = parent < 0 || node.Activation?.Placement == ConstructionEventPlacement.Event ? 1f : scales[parent];
            float growth = node.EventOperations.Where(operation => operation.Kind == ConstructionOperationKind.Grow).Select(operation => operation.TargetScale).DefaultIfEmpty(1f).Max();
            scales[i] = inherited * node.Scale * Mathf.Max(1f, growth);
            float anchor = parent < 0 ? 0f : node.AnchorBlend.RadiusBound(node.Anchor.GetColumn(3), plan.Nodes[parent].Resource.Rig, motions[parent]);
            float layout = node.EventOperations.Where(operation => operation.Gather != null).Select(operation => operation.Gather.Target.magnitude).Append(node.Gather?.Target.magnitude ?? 0f).Append(node.Offset.magnitude).Max();
            offsets[i] = (parent < 0 ? 0f : offsets[parent]) + (anchor + layout) * inherited;
            travel[i] = parent < 0 ? 0f : travel[parent];
            speeds[i] = parent < 0 ? 0f : speeds[parent];
            if (node.FragmentSource >= 0)
            {
                CompiledFracture fracture = plan.Nodes[node.FragmentSource].Fracture;
                travel[i] += (speeds[node.FragmentSource] + fracture.Speed) * node.Presence.y;
                speeds[i] += fracture.Speed;
            }
            AddFlight(node.Flight, node.FlyAt);
            foreach (CompiledEventOperation operation in node.EventOperations) AddFlight(operation.Flight, operation.At);
            float radius = 0f;
            foreach (var contact in node.Resource.Contacts)
                radius = Mathf.Max(radius, Mathf.Max(contact.ABlend.RadiusBound(contact.A, node.Resource.Rig, motions[i]), contact.BBlend.RadiusBound(contact.B, node.Resource.Rig, motions[i])) + contact.Radius);
            if (node.ContactRole != ConstructionContactRole.Decoration && radius > 0f)
            {
                parts[i] = new Vector2(offsets[i] + radius * scales[i], travel[i]);
                Extent = Mathf.Max(Extent, offsets[i] + radius * scales[i] + travel[i]);
            }

            void AddFlight(CompiledFlight flight, float at)
            {
                if (flight == null) return;
                // 显现前也会推进运动，不能把不可见的准备时间从已走路程中扣掉。
                float seconds = Mathf.Max(0f, Mathf.Min(node.Presence.y, node.Active.y) - Mathf.Max(0f, at));
                travel[i] += flight.Velocity.magnitude * seconds + flight.Acceleration.magnitude * seconds * seconds * .5f;
                if (flight.InheritVelocity && parent >= 0 && node.FragmentSource < 0) travel[i] += speeds[parent] * seconds;
                speeds[i] += flight.Velocity.magnitude + flight.Acceleration.magnitude * seconds;
                if (flight.Path != null) travel[i] += (float)flight.Path.TravelBudget(seconds, declared ?? 12f);
            }
        }
    }

    /// <summary>采用真实体高和目标身体半径；明确距离及目标处生成保留自己的选择约定。</summary>
    /// <param name="height">来源实际体高，定义统计时为一。</param><param name="targetRadius">实际目标用于候选筛选的半径。</param>
    public float Selection(float height = 1f, float targetRadius = 0f)
    {
        if (declared.HasValue) return declared.Value;
        if (targetPlaced || targetPath) return Mathf.Max(body * height + targetRadius, 12f);
        float generated = 0f;
        foreach (Vector2 part in parts) generated = Mathf.Max(generated, part.x * (scaleByBody ? height : 1f) + part.y);
        if (generated > 0f && fromHand) generated += hand * height;
        return Mathf.Max(body * height, generated) + targetRadius;
    }
}

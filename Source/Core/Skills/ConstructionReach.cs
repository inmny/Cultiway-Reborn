using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>从实际展开资料计算候选范围；命中仍读取同一施放的真实作用形状。</summary>
public sealed class ConstructionReach
{
    /// <summary>单位体高下得到的结构与投送包络。</summary>
    public float Extent { get; }
    /// <summary>目标处生成和相对目标路径保留自己的控制距离约定。</summary>
    private readonly bool targetPlaced, targetPath;
    /// <summary>作者明确选择的距离。</summary>
    private readonly float? declared;
    /// <summary>实际身体接触包络。</summary>
    private readonly float body;
    /// <summary>每项分别保存随体高缩放和采用世界单位的部分。</summary>
    private readonly Vector2[] parts;

    /// <summary>先按实际父体和事件来源排序，再统计形状、旋转和运动继承。</summary>
    /// <param name="plan">已完成关系检查的同版本计划。</param>
    internal ConstructionReach(CompiledAbility plan)
    {
        declared = plan.TargetRange; body = plan.Body?.ContactReach ?? 0f;
        targetPlaced = plan.FormCount > 0 && (plan.Binding == null || plan.Binding.Anchor == AbilityAnchor.Target);
        targetPath = plan.NeedsPathTarget;
        parts = new Vector2[plan.FormCount];
        var offsets = new Vector2[plan.FormCount]; var travel = new Vector2[plan.FormCount]; var speeds = new Vector2[plan.FormCount];
        var scales = new float[plan.FormCount]; var radii = new float[plan.FormCount]; var angular = new float[plan.FormCount]; var linear = new float[plan.FormCount];
        var scaled = new bool[plan.FormCount]; var done = new bool[plan.FormCount];
        var motions = plan.Nodes.Select(node => node.EventOperations.Select(operation => operation.Motion).Append(node.Motion).Where(motion => motion != null).Distinct().ToArray()).ToArray();
        for (int i = 0; i < plan.FormCount; i++) Calculate(i);
        Extent = parts.Select(part => part.x + part.y).DefaultIfEmpty(0f).Max();

        void Calculate(int i)
        {
            if (done[i]) return;
            CompiledForm node = plan.Nodes[i]; bool placed = node.Activation?.Placement == ConstructionEventPlacement.Event;
            int source = placed ? node.Activation.Source : node.Parent;
            if (source >= 0) Calculate(source);
            float inherited = placed || node.Parent < 0 ? 1f : scales[node.Parent];
            scaled[i] = placed ? false : node.Parent < 0 ? plan.Binding?.ScaleByBody == true : scaled[node.Parent];
            float growth = node.EventOperations.Where(operation => operation.Kind == ConstructionOperationKind.Grow).Select(operation => operation.TargetScale).DefaultIfEmpty(1f).Max();
            scales[i] = inherited * node.Scale * Mathf.Max(1f, growth);
            float anchor = placed || node.Parent < 0 ? 0f : node.AnchorBlend.RadiusBound(node.Anchor.GetColumn(3), plan.Nodes[node.Parent].Resource.Rig, motions[node.Parent]);
            float layout = node.EventOperations.Where(operation => operation.Gather != null).Select(operation => operation.Gather.Target.magnitude).Append(node.Gather?.Target.magnitude ?? 0f).Append(node.Offset.magnitude).Max();
            offsets[i] = source < 0 ? Vector2.zero : offsets[source];
            travel[i] = source < 0 ? Vector2.zero : travel[source];
            if (source < 0 && (placed || plan.Binding?.Anchor == AbilityAnchor.Hand)) offsets[i].x += body;
            if (placed && source >= 0) offsets[i] += Units(radii[source] * scales[source], scaled[source]);
            offsets[i] += Units((anchor + layout) * inherited, scaled[i]);
            float spin = node.Spin.magnitude + node.EventOperations.Where(operation => operation.Kind == ConstructionOperationKind.Spin).Sum(operation => operation.Vector.magnitude);
            float motionRate = (node.Motion?.MaximumAngularRate ?? 0f) * node.MotionSpeed;
            linear[i] = (node.Motion?.MaximumTranslationRate ?? 0f) * node.MotionSpeed;
            foreach (var operation in node.EventOperations.Where(operation => operation.Motion != null))
            { motionRate += operation.Motion.MaximumAngularRate * operation.MotionSpeed; linear[i] += operation.Motion.MaximumTranslationRate * operation.MotionSpeed; }
            angular[i] = (source < 0 ? 0f : angular[source]) + (spin + motionRate * 1.5f) * Mathf.Deg2Rad;
            var flights = node.EventOperations.Where(operation => operation.Flight != null).Select(operation => (Flight: operation.Flight, At: operation.At)).ToList();
            if (node.Flight != null) flights.Insert(0, (node.Flight, node.FlyAt));
            angular[i] += flights.Sum(flight => flight.Flight.TurnRate + (flight.Flight.Path != null && flight.Flight.Path.Forward.sqrMagnitude > 0f ? flight.Flight.Path.TurnRate : 0f)) * Mathf.Deg2Rad;
            foreach (var contact in node.Resource.Contacts)
                radii[i] = Mathf.Max(radii[i], Mathf.Max(contact.ABlend.RadiusBound(contact.A, node.Resource.Rig, motions[i]), contact.BBlend.RadiusBound(contact.B, node.Resource.Rig, motions[i])) + contact.Radius);
            Vector2 attached = source < 0 ? Vector2.zero : speeds[source];
            if (source >= 0)
                attached += Units((angular[source] * (anchor + layout + (node.FragmentSource >= 0 ? radii[source] : 0f)) + linear[source]) * inherited, scaled[i]);
            foreach (var gather in node.EventOperations.Select(operation => operation.Gather).Append(node.Gather).Where(gather => gather != null))
                attached += Units(3f * layout * inherited / gather.Seconds, scaled[i]);
            if (node.FragmentSource >= 0)
            {
                offsets[i] += Units(radii[source] * inherited, scaled[i]);
                attached.y += plan.Nodes[node.FragmentSource].Fracture.Speed;
            }
            float until = Mathf.Min(node.Presence.y, node.Active.y);
            float totalSpeed = flights.Sum(flight => FlightSpeed(flight.Flight, Mathf.Max(0f, until - Mathf.Max(0f, flight.At))));
            speeds[i] = attached + new Vector2(0f, totalSpeed);
            foreach (var flight in flights)
            {
                float seconds = Mathf.Max(0f, until - Mathf.Max(0f, flight.At));
                float ownSpeed = FlightSpeed(flight.Flight, seconds);
                if (flight.Flight.InheritVelocity)
                    travel[i] += (attached + new Vector2(0f, Mathf.Max(0f, totalSpeed - ownSpeed))) * seconds;
                travel[i].y += flight.Flight.Velocity.magnitude * seconds + flight.Flight.Acceleration.magnitude * seconds * seconds * .5f;
                if (flight.Flight.Path != null) travel[i].y += (float)flight.Flight.Path.TravelBudget(seconds, declared ?? 12f);
            }
            float release = node.Detach;
            foreach (var operation in node.EventOperations.Where(operation => operation.Kind == ConstructionOperationKind.Detach))
                release = release < 0f ? operation.At : Mathf.Min(release, operation.At);
            if (node.FragmentSource >= 0) release = 0f;
            if (release >= 0f)
            {
                float firstFlight = flights.Select(flight => Mathf.Max(0f, flight.At)).DefaultIfEmpty(until).Min();
                travel[i] += attached * Mathf.Max(0f, Mathf.Min(firstFlight, until) - release);
            }
            Vector2 geometry = offsets[i] + Units(radii[i] * scales[i], scaled[i]);
            if (!Finite(scales[i]) || scales[i] <= 0f || !Finite(geometry.x + geometry.y) || !Finite(travel[i].x + travel[i].y) || !Finite(speeds[i].x + speeds[i].y) || !Finite(angular[i]))
                throw new InvalidDataException(node.Path + "：组合缩放、空间或继承运动预算不能表示为有限数");
            if (node.ContactRole != ConstructionContactRole.Decoration && radii[i] > 0f) parts[i] = geometry + travel[i];
            done[i] = true;
        }

        float FlightSpeed(CompiledFlight flight, float seconds) => flight.Velocity.magnitude + flight.Acceleration.magnitude * seconds + (flight.Path?.SpeedBudget(declared ?? 12f) ?? 0f);
    }

    /// <summary>实际体高只缩放对应布局与继承速度，独立世界速度不再次缩放。</summary>
    /// <param name="height">真实体高，定义统计时为一。</param><param name="targetRadius">候选对象实际半径。</param>
    public float Selection(float height = 1f, float targetRadius = 0f)
    {
        if (declared.HasValue) return declared.Value;
        if (targetPlaced || targetPath) return Mathf.Max(body * height + targetRadius, 12f);
        float generated = 0f;
        foreach (Vector2 part in parts) generated = Mathf.Max(generated, part.x * height + part.y);
        return Mathf.Max(body * height, generated) + targetRadius;
    }

    /// <summary>保留随体高与世界单位两种线性组成，避免把整个投送距离一起放大。</summary>
    /// <param name="value">定义层尺度。</param><param name="scaled">是否按体高缩放。</param>
    private static Vector2 Units(float value, bool scaled) => scaled ? new Vector2(value, 0f) : new Vector2(0f, value);
    /// <summary>组合后仍必须能进入实际姿态与空间查询。</summary>
    /// <param name="value">合成后的数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

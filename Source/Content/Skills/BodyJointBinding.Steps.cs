using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>命名足沿自己实际骨长参与移动，空中不制造地面承托。</summary>
internal sealed partial class BodyJointBinding
{
    /// <summary>声明的各足必须完整对应本身体，缺失不借用其他足。</summary>
    /// <param name="names">本次请求的真实名称。</param>
    internal bool HasFeet(IEnumerable<string> names) => names.SequenceEqual(legs.Select(leg => leg.Bone.Part).OrderBy(part => part, StringComparer.Ordinal));

    /// <summary>从实际起势姿态读取每处自然落点，形体和落脚采用相同坐标。</summary>
    /// <param name="frame">同一真实起势。</param><param name="foot">共同脚下。</param>
    /// <param name="forward">起势朝向。</param><param name="height">实际体高。</param>
    internal BodyFootPose[] NaturalFeet(BodyJointFrame frame, Vector3 foot, Vector3 forward, float height)
    {
        Vector3 side = Vector3.Cross(forward, Vector3.forward);
        return legs.OrderBy(leg => leg.Bone.Part, StringComparer.Ordinal).Select(leg =>
        {
            BodyJointSupport support = frame.Supports.First(item => item.Parts.Contains(leg.Bone.Part));
            Vector3 point = support.Motion.Matrix.MultiplyPoint3x4(frame.Root.Matrix.MultiplyPoint3x4(leg.End)) - foot;
            return new BodyFootPose(leg.Bone.Part, new Vector3(Vector3.Dot(point, forward), Vector3.Dot(point, side), point.z) / height,
                true, support.Motion.Matrix * frame.Root.Matrix);
        }).ToArray();
    }

    /// <summary>原骨段按各自真实落脚转动；实际长度、足面、躯干与承托共同限制位移。</summary>
    /// <param name="foot">当前共同身体位置。</param><param name="facing">当前身体朝向。</param>
    /// <param name="terrain">共同地表。</param><param name="support">各足已确认的世界锚点。</param>
    private BodyJointFrame StepFrame(Vector3 foot, Vector3 facing, AbilityHeightField terrain, BodySupportPose support)
        => PlaceFeet(foot, facing, terrain, support, out _);

    /// <summary>先拟合已踩实的足面，再放下新足，最后冻结同一身体与落脚。</summary>
    /// <param name="foot">共同脚下。</param><param name="facing">实际朝向。</param><param name="terrain">共同地表。</param>
    /// <param name="support">各足本步请求及已有落脚。</param><param name="feet">实际确认的落脚。</param>
    /// <param name="seed">上一个数值候选随根点平移后的姿态。</param>
    internal BodyJointFrame PlaceFeet(Vector3 foot, Vector3 facing, AbilityHeightField terrain, BodySupportPose support,
        out BodyFootPose[] feet, BodySurfaceTransform? seed = null)
    {
        // 已踩实的真实足面先决定根高和倾斜，接触边界不随根点滑动。
        Vector3 forward = root.Axes * Vector3.forward; forward.z = 0f;
        Quaternion turn = Quaternion.AngleAxis(Vector3.SignedAngle(forward, facing, Vector3.forward), Vector3.forward);
        var placement = new BodySurfaceTransform(structure.Surface.Foot, foot, turn);
        var desired = support.Parts.ToDictionary(item => item.Part, StringComparer.Ordinal);
        FootPlant[] plants = legs.Where(leg => desired[leg.Bone.Part].Planted && desired[leg.Bone.Part].Plant.HasValue)
            .Select(leg => new FootPlant(leg, desired[leg.Bone.Part].Plant.Value, terrain)).ToArray();
        bool accepted = plants.All(plant => plant.Candidates.Length > 0);
        string rejection = accepted ? null : "原落脚足面已不接地";
        if (plants.Length > 0 && accepted)
        {
            accepted = FitPlants(plants, placement, facing, terrain, seed, out placement);
            if (!accepted) rejection = "真实骨长或足面无法同时满足已落脚位置";
        }
        else if (plants.Length == 0)
        {
            if (support.Rise <= .000001f) placement = Frame(foot, facing, terrain).Root;
            else
            {
                float lift = 0f; int fitted = 0;
                foreach (StandingLeg leg in legs)
                {
                    Vector3 hip = placement.Matrix.MultiplyPoint3x4(leg.Bone.Pivot), target = desired[leg.Bone.Part].Point;
                    float length = Vector3.Distance(leg.End, leg.Bone.Pivot), distance = Vector2.Distance(hip, target);
                    if (distance <= length) { lift += target.z + Mathf.Sqrt(Mathf.Max(0f, length * length - distance * distance)) - hip.z; fitted++; }
                }
                placement = new BodySurfaceTransform(structure.Surface.Foot, foot + Vector3.forward * (fitted > 0 ? lift / fitted : 0f), turn);
            }
        }
        var motions = new List<BodyJointSupport>(); var grounded = new List<Vector2>();
        var resolved = new Dictionary<string, BodyFootPose>(desired, StringComparer.Ordinal);
        Vector3 selectedPivot = placement.Matrix.MultiplyPoint3x4(bone.Pivot);
        BodySurfaceTransform reference = new(selectedPivot, selectedPivot, Quaternion.identity);
        // 各段保持原长度；新落脚由实际足面取得，再加入承托。
        foreach (StandingLeg leg in legs)
        {
            BodyFootPose request = desired[leg.Bone.Part];
            Vector3 pivot = placement.Matrix.MultiplyPoint3x4(leg.Bone.Pivot), original = placement.Matrix.MultiplyPoint3x4(leg.End);
            FootPlant plant = plants.FirstOrDefault(item => item.Leg == leg);
            BodySurfaceTransform motion;
            if (plant != null)
                motion = PlantMotion(plant, placement);
            else if (request.Planted)
            {
                bool landed = FitLanding(leg, placement, request.Point, terrain, out motion);
                if (!landed) Reject("新足在当前骨长下无法触地");
                Matrix4x4 landedMap = motion.Matrix * placement.Matrix;
                resolved[request.Part] = new BodyFootPose(request.Part, landedMap.MultiplyPoint3x4(leg.End), true, landedMap);
            }
            else
            {
                motion = new BodySurfaceTransform(pivot, pivot, BodySurfaceTransform.Between(original - pivot, request.Point - pivot));
                Matrix4x4 trial = motion.Matrix * placement.Matrix;
                // 抬足请求也受实际足面限制；接近落地时停止下压，不让足缘穿地。
                if (leg.Foot.Points.Any(point =>
                    { Vector3 world = trial.MultiplyPoint3x4(point); return world.z < BodyLocomotion.Floor(terrain, world, pivot.z, 0f) - .0001f; }))
                    if (!FitLanding(leg, placement, request.Point, terrain, out motion)) Reject("抬足的原足面无法避开地表");
            }
            Matrix4x4 map = motion.Matrix * placement.Matrix;
            Vector3[] actual = Array.ConvertAll(leg.Foot.Points, map.MultiplyPoint3x4);
            if (actual.Any(point => point.z < BodyLocomotion.Floor(terrain, point, pivot.z, 0f) - .0001f)) Reject("实际足面进入地表");
            if (request.Planted)
            {
                if (plant != null && Vector3.Distance(map.MultiplyPoint3x4(plant.Point), plant.Anchor) > .0001f) Reject("原足缘世界落点无法保持");
                Vector3[] points = actual.Where(point => Mathf.Abs(point.z - BodyLocomotion.Floor(terrain, point, pivot.z, 0f)) < .0001f).ToArray();
                if (points.Length == 0) Reject("足面没有真实接地位置");
                grounded.AddRange(points.Select(point => (Vector2)point));
            }
            motions.Add(new BodyJointSupport(leg.Parts, motion));
            if (leg.Bone.Part == bone.Part) reference = motion;
        }
        // 最后检查真实承托和躯干；腾空不假造接地足。
        if (support.Parts.Any(item => item.Planted))
        { if (!Inside(grounded.ToArray(), placement.Matrix.MultiplyPoint3x4(root.Pivot))) Reject("承重点超出真实接地边界"); }
        else if (support.Rise <= .000001f) Reject("未接地且没有实际腾起");
        foreach (Vector3 original in torso.Points)
        {
            Vector3 point = placement.Matrix.MultiplyPoint3x4(original);
            if (point.z < BodyLocomotion.Floor(terrain, point, placement.Matrix.MultiplyPoint3x4(root.Pivot).z, 0f) - .0001f) Reject("躯干进入地表");
        }
        feet = support.Parts.Select(item => resolved[item.Part]).ToArray();
        return new BodyJointFrame(structure.Surface, contact, limb, parts, bone.Pivot, bone.Axes, root.Pivot,
            placement, terrain, accepted, motions.ToArray(), reference, Natural(foot, facing, turn), skeleton, rejection);

        /// <summary>保留首次真实失败，不用后续连带误差覆盖根因。</summary>
        /// <param name="reason">本候选的实际失败。</param>
        void Reject(string reason) { if (accepted) rejection = reason; accepted = false; }
    }
}

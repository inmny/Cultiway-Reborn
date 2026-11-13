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
            return new BodyFootPose(leg.Bone.Part, new Vector3(Vector3.Dot(point, forward), Vector3.Dot(point, side), point.z) / height, true);
        }).ToArray();
    }

    /// <summary>原骨段按各自真实落脚转动；实际长度、足面、躯干与承托共同限制位移。</summary>
    /// <param name="foot">当前共同身体位置。</param><param name="facing">当前身体朝向。</param>
    /// <param name="terrain">共同地表。</param><param name="support">各足已确认的世界锚点。</param>
    private BodyJointFrame StepFrame(Vector3 foot, Vector3 facing, AbilityHeightField terrain, BodySupportPose support)
    {
        // 先按原骨长求共同根高；已踩实的锚点不随身体平移。
        Vector3 forward = root.Axes * Vector3.forward; forward.z = 0f;
        Quaternion turn = Quaternion.AngleAxis(Vector3.SignedAngle(forward, facing, Vector3.forward), Vector3.forward);
        var placement = new BodySurfaceTransform(structure.Surface.Foot, foot, turn);
        var desired = support.Parts.ToDictionary(item => item.Part, StringComparer.Ordinal);
        float lift = 0f; int fitted = 0; bool accepted = true;
        foreach (StandingLeg leg in legs)
        {
            BodyFootPose request = desired[leg.Bone.Part];
            Vector3 hip = placement.Matrix.MultiplyPoint3x4(leg.Bone.Pivot);
            float length = Vector3.Distance(leg.End, leg.Bone.Pivot), distance = Vector2.Distance(hip, request.Point);
            if (distance > length) { if (request.Planted) accepted = false; continue; }
            if (request.Planted || !support.Parts.Any(item => item.Planted))
            { lift += request.Point.z + Mathf.Sqrt(Mathf.Max(0f, length * length - distance * distance)) - hip.z; fitted++; }
        }
        if (fitted > 0) lift /= fitted;
        placement = new BodySurfaceTransform(structure.Surface.Foot, foot + Vector3.forward * lift, turn);
        var motions = new List<BodyJointSupport>(); var grounded = new List<Vector2>();
        BodySurfaceTransform reference = default;
        // 各段只转动原骨，真实足面继续核对地表和既有锚点。
        foreach (StandingLeg leg in legs)
        {
            BodyFootPose request = desired[leg.Bone.Part];
            Vector3 pivot = placement.Matrix.MultiplyPoint3x4(leg.Bone.Pivot), original = placement.Matrix.MultiplyPoint3x4(leg.End);
            Quaternion rotation = Quaternion.FromToRotation(original - pivot, request.Point - pivot);
            var motion = new BodySurfaceTransform(pivot, pivot, rotation);
            Matrix4x4 map = motion.Matrix * placement.Matrix;
            Vector3 actualEnd = map.MultiplyPoint3x4(leg.End);
            Vector3[] actual = Array.ConvertAll(leg.Foot.Points, map.MultiplyPoint3x4);
            if (actual.Any(point => point.z < BodyLocomotion.Floor(terrain, point, pivot.z, 0f) - .0001f)) accepted = false;
            if (request.Planted)
            {
                if (Vector3.Distance(actualEnd, request.Point) > .0001f) accepted = false;
                Vector3[] points = actual.Where(point => Mathf.Abs(point.z - BodyLocomotion.Floor(terrain, point, pivot.z, 0f)) < .0001f).ToArray();
                if (points.Length == 0) accepted = false;
                if (leg.Bone.Part != bone.Part) grounded.AddRange(points.Select(point => (Vector2)point));
            }
            motions.Add(new BodyJointSupport(leg.Parts, motion));
            if (leg.Bone.Part == bone.Part) reference = motion;
        }
        // 最后检查真实承托和躯干；腾空不假造接地足。
        if (support.Parts.Any(item => item.Planted))
            accepted &= Inside(grounded.ToArray(), placement.Matrix.MultiplyPoint3x4(root.Pivot));
        else accepted &= support.Rise > .000001f;
        foreach (Vector3 original in torso.Points)
        {
            Vector3 point = placement.Matrix.MultiplyPoint3x4(original);
            if (point.z < BodyLocomotion.Floor(terrain, point, placement.Matrix.MultiplyPoint3x4(root.Pivot).z, 0f) - .0001f) accepted = false;
        }
        return new BodyJointFrame(structure.Surface, contact, limb, parts, bone.Pivot, bone.Axes, root.Pivot,
            placement, terrain, accepted, motions.ToArray(), reference, Natural(foot, facing, turn), skeleton);
    }
}

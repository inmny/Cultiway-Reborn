using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>兽足从真实骨段、实际足部和本身体支撑取得动作，不运行人形双足求解。</summary>
internal sealed partial class BodyJointBinding
{
    /// <summary>真实身体采集入口及起势时的不可变结构。</summary>
    internal readonly BodySurfaceBinding Binding;
    /// <summary>同一实际身体。</summary>
    private readonly BodyStructure structure;
    /// <summary>真实作用区域及所选骨段网格。</summary>
    private readonly BodySurfacePose contact;
    /// <summary>起势时冻结一次的原骨段几何。</summary>
    private readonly BodySurfaceMesh limb;
    /// <summary>所选骨段与原身体承接根。</summary>
    private readonly BodyStructureBone bone, root;
    /// <summary>真实骨段和原有子部件。</summary>
    private readonly string[] parts;
    /// <summary>本次原地动作开始位置与原版实际比例。</summary>
    private readonly Vector2 position;
    /// <summary>身体缩放改变后不沿用旧几何。</summary>
    private readonly float scale;

    /// <summary>各足自己的原关节、原足面和原骨长，不固定为两足。</summary>
    private readonly StandingLeg[] legs;
    /// <summary>承重躯干的实际表面，用于拒绝贴地后躯干穿地。</summary>
    private readonly BodySurfaceMesh torso;

    /// <summary>原身体自己的父子关节交接，只建立一次。</summary>
    private readonly BodyJointSkeleton skeleton;

    /// <summary>一个原身体实际提供的足部。</summary>
    private sealed class StandingLeg
    {
        /// <summary>真实关节和子部件。</summary>
        internal BodyStructureBone Bone;
        /// <summary>原骨段选择。</summary>
        internal string[] Parts;
        /// <summary>原足部最低处和完整足面。</summary>
        internal Vector3 End;
        /// <summary>实际足部几何。</summary>
        internal BodySurfaceMesh Foot;
    }

    /// <summary>复制实际来源的数值；实际资源入口只留在主线程绑定中。</summary>
    /// <param name="binding">真实身体。</param><param name="structure">同轮结构。</param>
    /// <param name="contact">原资源实际足部区域。</param><param name="part">实际发力骨段。</param>
    private BodyJointBinding(BodySurfaceBinding binding, BodyStructure structure, BodySurfacePose contact, string part)
    {
        Binding = binding; this.structure = structure; this.contact = contact; bone = structure.Bones[part]; root = bone;
        while (root.Parent != null && structure.Bones.TryGetValue(root.Parent, out var parent)) root = parent;
        parts = structure.Bones.Keys.Where(candidate => structure.IsWithin(candidate, part)).OrderBy(candidate => candidate == part ? 0 : 1).ToArray();
        limb = new BodySurfaceMesh(structure.Surface.SelectParts(parts));
        torso = new BodySurfaceMesh(structure.Surface.SelectParts(new[] { root.Part }));
        skeleton = new BodyJointSkeleton(structure);
        legs = structure.Supports.Select(support =>
        {
            BodyStructureBone actual = structure.Bones.Values.First(candidate => candidate.Part != root.Part && structure.IsWithin(support.Part, candidate.Part));
            BodySurfacePose foot = binding.JointContact(structure, actual.Part);
            return new StandingLeg { Bone = actual, Parts = structure.Bones.Keys.Where(candidate => structure.IsWithin(candidate, actual.Part)).ToArray(),
                End = support.Points.Aggregate(Vector3.zero, (sum, point) => sum + point) / support.Points.Length,
                Foot = new BodySurfaceMesh(foot) };
        }).ToArray();
        position = binding.Actor.current_position; scale = binding.Actor.actor_scale;
    }

    /// <summary>只接受当前有骨段、承接根和明确作用区域的实际身体。</summary>
    /// <param name="actor">实际来源。</param><param name="part">实际部位。</param>
    internal static BodyJointBinding TryCreate(Actor actor, string part)
    {
        var sampled = ConstructedAbilityService.BodyStructureFor(actor);
        if (sampled.Structure == null || !sampled.Structure.Bones.TryGetValue(part, out var bone) ||
            bone.Parent == null || !sampled.Structure.Bones.ContainsKey(bone.Parent)) return null;
        BodySurfacePose contact = sampled.Binding.JointContact(sampled.Structure, part);
        return contact?.Parts.Length > 0 ? new BodyJointBinding(sampled.Binding, sampled.Structure, contact, part) : null;
    }

    /// <summary>原地动作只保留原身体；外部搬移、换比例或换资源时退出。</summary>
    internal bool IsCurrent => Binding.ChangedReason == null && Binding.Actor.actor_scale == scale;

    /// <summary>没有声明步法的动作仍须留在原实际位置。</summary>
    internal bool IsAtStart => Vector2.Distance(Binding.Actor.current_position, position) < .0001f;

    /// <summary>选择包络来自真实骨段，不采用人类臂长。</summary>
    internal float Reach => (bone.Pivot - structure.Surface.Foot).magnitude +
        limb.Points.Max(point => (point - bone.Pivot).magnitude);

    /// <summary>从实际足底和共同地表确定承托，再冻结本步数字。</summary>
    /// <param name="foot">本步共同脚下。</param><param name="facing">锁定身体前方。</param>
    /// <param name="terrain">共同地表。</param>
    /// <param name="support">已经确认的各实际足世界锚点。</param>
    internal BodyJointFrame Frame(Vector3 foot, Vector3 facing, AbilityHeightField terrain, BodySupportPose? support = null)
    {
        if (support?.Parts != null) return StepFrame(foot, facing, terrain, support.Value);
        Vector3 forward = root.Axes * Vector3.forward; forward.z = 0f;
        Quaternion turn = Quaternion.AngleAxis(Vector3.SignedAngle(forward, facing, Vector3.forward), Vector3.forward);
        var placement = new BodySurfaceTransform(structure.Surface.Foot, foot, turn);
        Matrix4x4 map = placement.Matrix;
        float lift = float.PositiveInfinity;
        foreach (StandingLeg leg in legs)
        {
            Vector3 point = map.MultiplyPoint3x4(leg.End);
            lift = Mathf.Min(lift, BodyLocomotion.Floor(terrain, point, map.MultiplyPoint3x4(root.Pivot).z, 0f) - point.z);
        }
        bool supported = legs.Length > 0;
        var supports = new List<BodyJointSupport>();
        var grounded = new List<Vector2>();
        Vector3 actualPivot = map.MultiplyPoint3x4(bone.Pivot);
        BodySurfaceTransform reference = new(actualPivot, actualPivot, Quaternion.identity);
        for (int pass = 0; pass < 4 && supported; pass++)
        {
            placement = new BodySurfaceTransform(structure.Surface.Foot, foot + Vector3.forward * lift, turn);
            supports.Clear(); grounded.Clear(); float drop = 0f;
            foreach (StandingLeg leg in legs)
            {
                bool fitted = Fit(leg, placement, terrain, out BodySurfaceTransform motion, out Vector3[] contactPoints, out float requiredDrop);
                drop = Mathf.Min(drop, requiredDrop);
                if (!fitted && requiredDrop == 0f) { supported = false; break; }
                supports.Add(new BodyJointSupport(leg.Parts, motion));
                if (leg.Bone.Part == bone.Part) reference = motion;
                else grounded.AddRange(contactPoints.Select(point => (Vector2)point));
            }
            if (drop >= 0f) break;
            lift += drop;
            if (pass == 3) supported = false;
        }
        supported &= Inside(grounded.ToArray(), placement.Matrix.MultiplyPoint3x4(root.Pivot));
        foreach (Vector3 original in torso.Points)
        {
            Vector3 point = placement.Matrix.MultiplyPoint3x4(original);
            if (point.z < BodyLocomotion.Floor(terrain, point, placement.Matrix.MultiplyPoint3x4(root.Pivot).z, 0f) - .0001f) { supported = false; break; }
        }
        BodySurfacePose natural = Natural(foot, facing, turn);
        return new BodyJointFrame(structure.Surface, contact, limb, parts, bone.Pivot, bone.Axes, root.Pivot,
            placement, terrain, supported, supports.ToArray(), reference, natural, skeleton);
    }

    /// <summary>同一身体在当前共同位置的自然姿态，原地与移动共用。</summary>
    /// <param name="foot">共同脚下。</param><param name="facing">本步身体朝向。</param><param name="turn">原身体至当前朝向。</param>
    private BodySurfacePose Natural(Vector3 foot, Vector3 facing, Quaternion turn)
    {
        if (BodySurfaceBinding.Provider == null)
            return structure.Surface.Transformed(new BodySurfaceTransform(structure.Surface.Foot, foot, turn));
        BodySurfacePose natural = Binding.To;
        BodyStructure current = Binding.Structure(natural);
        Vector3 naturalForward = current.Bones[root.Part].Axes * Vector3.forward; naturalForward.z = 0f;
        Quaternion naturalTurn = Quaternion.AngleAxis(Vector3.SignedAngle(naturalForward, facing, Vector3.forward), Vector3.forward);
        return natural.Transformed(new BodySurfaceTransform(natural.Foot, foot, naturalTurn));
    }

    /// <summary>单段真实兽腿保持自身骨长转向地面，足垫最低边界继续使用原几何。</summary>
    /// <param name="leg">真实兽足。</param><param name="rootMotion">共同根运动。</param><param name="terrain">共同地表。</param>
    /// <param name="motion">本足关节转动。</param><param name="points">实际接地点。</param><param name="drop">骨长够不到地面时需要降低的身体高度。</param>
    private static bool Fit(StandingLeg leg, BodySurfaceTransform rootMotion, AbilityHeightField terrain,
        out BodySurfaceTransform motion, out Vector3[] points, out float drop)
    {
        Vector3 pivot = rootMotion.Matrix.MultiplyPoint3x4(leg.Bone.Pivot), initial = rootMotion.Matrix.MultiplyPoint3x4(leg.End);
        Vector3 original = initial - pivot, direction = original; direction.z = 0f;
        if (direction.sqrMagnitude < .00000001f) { direction = rootMotion.Rotation * leg.Bone.Axes * Vector3.forward; direction.z = 0f; }
        direction.Normalize(); float length = original.magnitude;
        float z = BodyLocomotion.Floor(terrain, initial, pivot.z, 0f);
        motion = new BodySurfaceTransform(pivot, pivot, Quaternion.identity); points = Array.Empty<Vector3>(); drop = 0f;
        Vector3[] standing = Array.ConvertAll(leg.Foot.Points, rootMotion.Matrix.MultiplyPoint3x4);
        if (Mathf.Abs(standing.Max(point => BodyLocomotion.Floor(terrain, point, pivot.z, 0f) - point.z)) < .0001f)
        {
            points = standing.Where(point => Mathf.Abs(point.z - BodyLocomotion.Floor(terrain, point, pivot.z, 0f)) < .0001f).ToArray();
            return points.Length > 0;
        }
        for (int pass = 0; pass < 12; pass++)
        {
            float vertical = z - pivot.z;
            if (vertical < -length) { drop = vertical + length; return false; }
            if (vertical > length) return false;
            Vector3 target = pivot + direction * Mathf.Sqrt(Mathf.Max(0f, length * length - vertical * vertical)); target.z = z;
            Quaternion rotation = Quaternion.FromToRotation(original, target - pivot);
            motion = new BodySurfaceTransform(pivot, pivot, rotation);
            Matrix4x4 map = motion.Matrix * rootMotion.Matrix;
            Vector3[] actual = Array.ConvertAll(leg.Foot.Points, map.MultiplyPoint3x4);
            float correction = actual.Max(point => BodyLocomotion.Floor(terrain, point, pivot.z, 0f) - point.z);
            if (Mathf.Abs(correction) < .0001f)
            {
                points = actual.Where(point => Mathf.Abs(point.z - BodyLocomotion.Floor(terrain, point, pivot.z, 0f)) < .0001f).ToArray();
                return points.Length > 0;
            }
            z += correction;
        }
        return false;
    }

    /// <summary>由真实承托点形成边界；线或点不补造宽度。</summary>
    /// <param name="points">已经接地的实际点。</param><param name="center">实际承重点投影。</param>
    private static bool Inside(Vector2[] points, Vector2 center)
    {
        var sorted = points.Distinct().OrderBy(point => point.x).ThenBy(point => point.y).ToArray();
        if (sorted.Length == 0) return false;
        if (sorted.Length == 1) return Vector2.Distance(sorted[0], center) <= .0001f;
        var hull = new List<Vector2>();
        foreach (Vector2 point in sorted)
        { while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f) hull.RemoveAt(hull.Count - 1); hull.Add(point); }
        int lower = hull.Count;
        for (int i = sorted.Length - 2; i >= 0; i--)
        { while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], sorted[i]) <= 0f) hull.RemoveAt(hull.Count - 1); hull.Add(sorted[i]); }
        hull.RemoveAt(hull.Count - 1);
        if (hull.Count == 2)
        {
            Vector2 line = hull[1] - hull[0]; float at = Mathf.Clamp01(Vector2.Dot(center - hull[0], line) / line.sqrMagnitude);
            return Vector2.Distance(center, hull[0] + line * at) <= .0001f;
        }
        for (int i = 0; i < hull.Count; i++) if (Cross(hull[i], hull[(i + 1) % hull.Count], center) < -.00001f) return false;
        return true;
    }

    /// <summary>三点在共同地面上的有向面积。</summary>
    /// <param name="a">边首。</param><param name="b">边尾。</param><param name="point">待判断点。</param>
    private static float Cross(Vector2 a, Vector2 b, Vector2 point) => (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
}

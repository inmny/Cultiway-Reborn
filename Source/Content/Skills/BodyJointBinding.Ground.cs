using System;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>身体围绕真实承重点调整，已踩实的足面不平移、不缩放。</summary>
internal sealed partial class BodyJointBinding
{
    /// <summary>一处真实落地足面，只能沿当时确实接地的边界滚动。</summary>
    private sealed class FootPlant
    {
        /// <summary>原骨段及实际足面。</summary>
        internal readonly StandingLeg Leg;
        /// <summary>落地时原足面至世界的固定变换。</summary>
        internal readonly Matrix4x4 Map;
        /// <summary>落地时确实接触共同地表的原几何点。</summary>
        internal readonly Vector3[] Candidates;
        /// <summary>当前承托所用的原几何点。</summary>
        internal Vector3 Point;
        /// <summary>原几何点保持的真实世界位置。</summary>
        internal Vector3 Anchor => Map.MultiplyPoint3x4(Point);
        /// <summary>此关节至当前实际接触点的原长度。</summary>
        internal float Length => Vector3.Distance(Leg.Bone.Pivot, Point);

        /// <summary>只选落地时真实接地的原几何，保留线或点，不补宽度。</summary>
        /// <param name="leg">原骨段。</param><param name="map">落地变换。</param><param name="terrain">共同地表。</param>
        internal FootPlant(StandingLeg leg, Matrix4x4 map, AbilityHeightField terrain)
        {
            Leg = leg; Map = map;
            Candidates = leg.Foot.Points.Distinct().Where(point =>
            {
                Vector3 world = map.MultiplyPoint3x4(point);
                return Mathf.Abs(world.z - BodyLocomotion.Floor(terrain, world, world.z + .0001f, 0f)) < .0001f;
            }).ToArray();
            if (Candidates.Length > 0) Point = Candidates[0];
        }
    }

    /// <summary>关节围绕原骨长转向固定的真实接触点。</summary>
    /// <param name="plant">已踩实的足面。</param><param name="placement">同一身体变换。</param>
    private static BodySurfaceTransform PlantMotion(FootPlant plant, BodySurfaceTransform placement)
    {
        Vector3 pivot = placement.Matrix.MultiplyPoint3x4(plant.Leg.Bone.Pivot);
        return new BodySurfaceTransform(pivot, pivot, BodySurfaceTransform.Between(
            placement.Matrix.MultiplyPoint3x4(plant.Point) - pivot, plant.Anchor - pivot));
    }

    /// <summary>先解身体高度和倾斜，再检查足面是否需要沿原接触边界滚动。</summary>
    /// <param name="plants">实际已落地足面。</param><param name="basis">本步共同根点与转向。</param>
    /// <param name="facing">本步前方。</param><param name="terrain">共同地表。</param>
    /// <param name="seed">最近实际候选。</param><param name="placement">保持骨长的共同身体。</param>
    private bool FitPlants(FootPlant[] plants, BodySurfaceTransform basis, Vector3 facing, AbilityHeightField terrain,
        BodySurfaceTransform? seed, out BodySurfaceTransform placement)
    {
        placement = seed ?? basis;
        // 足缘滚动只更换原落地边界中的点，新的世界位置仍来自原固定变换。
        for (int pass = 0; pass < 4; pass++)
        {
            foreach (FootPlant plant in plants)
            {
                Matrix4x4 map = PlantMotion(plant, placement).Matrix * placement.Matrix;
                plant.Point = plant.Candidates.OrderBy(point =>
                {
                    Vector3 world = map.MultiplyPoint3x4(point);
                    return world.z - BodyLocomotion.Floor(terrain, world, world.z + .0001f, 0f);
                }).First();
            }
            if (!FitRoot(plants, basis, facing, placement, out placement)) return false;
            bool clear = true;
            foreach (FootPlant plant in plants)
            {
                Matrix4x4 map = PlantMotion(plant, placement).Matrix * placement.Matrix;
                foreach (Vector3 point in plant.Leg.Foot.Points)
                {
                    Vector3 world = map.MultiplyPoint3x4(point);
                    if (world.z < BodyLocomotion.Floor(terrain, world, world.z + .0001f, 0f) - .0001f) { clear = false; break; }
                }
            }
            if (clear) return true;
        }
        return false;
    }

    /// <summary>用真实骨长误差调整三个身体自由度，失败就拒绝候选，不伸缩骨段。</summary>
    /// <param name="plants">实际承托。</param><param name="basis">请求的共同位置。</param>
    /// <param name="facing">实际朝向。</param><param name="seed">最近候选。</param><param name="result">实际根运动。</param>
    private bool FitRoot(FootPlant[] plants, BodySurfaceTransform basis, Vector3 facing,
        BodySurfaceTransform seed, out BodySurfaceTransform result)
    {
        Vector3 center = basis.Matrix.MultiplyPoint3x4(root.Pivot), side = Vector3.Cross(facing, Vector3.forward);
        Vector2 balanced = SupportedCenter(plants.Select(plant => (Vector2)plant.Anchor).ToArray(), center);
        if (Vector2.Distance(center, balanced) > plants.Max(plant => plant.Length)) { result = basis; return false; }
        center.x = balanced.x; center.y = balanced.y;
        float lift = seed.Matrix.MultiplyPoint3x4(root.Pivot).z - center.z;
        Quaternion rotation = Align(seed.Rotation);
        var current = Motion(lift, rotation);
        var errors = new float[plants.Length]; var slopes = new Vector3[plants.Length];
        // 约束只来自原关节至真实接触点的距离；数值修正不改变请求的平面承重点。
        for (int iteration = 0; iteration < 16; iteration++)
        {
            float energy = 0f, maximum = 0f;
            for (int i = 0; i < plants.Length; i++)
            { errors[i] = Error(plants[i], current); energy += errors[i] * errors[i]; maximum = Mathf.Max(maximum, Mathf.Abs(errors[i])); }
            if (maximum < .00004f) { result = current; return true; }
            var raised = Motion(lift + .001f, rotation);
            var pitched = Motion(lift, Align(Quaternion.AngleAxis(.01f * Mathf.Rad2Deg, side) * rotation));
            var rolled = Motion(lift, Align(Quaternion.AngleAxis(.01f * Mathf.Rad2Deg, facing) * rotation));
            for (int i = 0; i < plants.Length; i++) slopes[i] = new Vector3(
                (Error(plants[i], raised) - errors[i]) / .001f,
                (Error(plants[i], pitched) - errors[i]) / .01f,
                (Error(plants[i], rolled) - errors[i]) / .01f);
            Matrix4x4 normal = Matrix4x4.zero; normal[3, 3] = 1f; Vector4 gradient = Vector4.zero;
            for (int axis = 0; axis < 3; axis++)
            {
                normal[axis, axis] = .0000001f;
                for (int i = 0; i < plants.Length; i++)
                {
                    gradient[axis] += slopes[i][axis] * errors[i];
                    for (int other = 0; other < 3; other++) normal[axis, other] += slopes[i][axis] * slopes[i][other];
                }
            }
            Vector4 step = -(normal.inverse * gradient);
            float scale = Mathf.Max(1f, Mathf.Abs(step.x) / Mathf.Max(.0001f, plants.Max(plant => plant.Length) * .25f),
                Mathf.Abs(step.y) / .25f, Mathf.Abs(step.z) / .25f);
            step /= scale;
            bool improved = false;
            // 只接受真实误差下降的修正，不用大步翻转身体掩盖不可达落脚。
            for (int trial = 0; trial < 6; trial++)
            {
                float fraction = 1f / (1 << trial), nextLift = lift + step.x * fraction;
                Quaternion nextRotation = Align(Quaternion.AngleAxis(step.y * fraction * Mathf.Rad2Deg, side) *
                    Quaternion.AngleAxis(step.z * fraction * Mathf.Rad2Deg, facing) * rotation);
                if ((nextRotation * Vector3.forward).z <= 0f) continue;
                var candidate = Motion(nextLift, nextRotation); float nextEnergy = 0f;
                foreach (FootPlant plant in plants) { float error = Error(plant, candidate); nextEnergy += error * error; }
                if (!(nextEnergy < energy)) continue;
                lift = nextLift; rotation = nextRotation; current = candidate; improved = true; break;
            }
            if (!improved) break;
        }
        result = current;
        return plants.All(plant => Mathf.Abs(Error(plant, current)) < .00004f);

        /// <summary>保持实际承重点的平面位置，只调整高度和方向。</summary>
        /// <param name="height">本步根高修正。</param><param name="angle">实际整体转动。</param>
        BodySurfaceTransform Motion(float height, Quaternion angle) => new(root.Pivot, center + Vector3.forward * height, angle);
        /// <summary>身体倾斜不暗中改变已声明的平面朝向。</summary>
        /// <param name="angle">候选整体方向。</param>
        Quaternion Align(Quaternion angle)
        {
            Vector3 forward = angle * root.Axes * Vector3.forward; forward.z = 0f;
            return Quaternion.AngleAxis(Vector3.SignedAngle(forward, facing, Vector3.forward), Vector3.forward) * angle;
        }
    }

    /// <summary>读取一条真实骨段到固定世界接触点的长度误差。</summary>
    /// <param name="plant">真实足面。</param><param name="placement">候选身体。</param>
    private static float Error(FootPlant plant, BodySurfaceTransform placement) =>
        Vector3.Distance(placement.Matrix.MultiplyPoint3x4(plant.Leg.Bone.Pivot), plant.Anchor) - plant.Length;

    /// <summary>身体向真实足面内调整承重点，点和线仍保持原边界，不补造支撑宽度。</summary>
    /// <param name="points">当前真实接地点。</param><param name="wanted">请求的承重点。</param>
    private static Vector2 SupportedCenter(Vector2[] points, Vector2 wanted)
    {
        if (Inside(points, wanted)) return wanted;
        Vector2 nearest = points[0]; float distance = Vector2.SqrMagnitude(nearest - wanted);
        // 所有边段包含凸包边，外部最近点必在其中；四足不需要另一份可变支撑对象。
        for (int i = 0; i < points.Length; i++)
            for (int j = i + 1; j < points.Length; j++)
            {
                Vector2 delta = points[j] - points[i];
                if (delta.sqrMagnitude == 0f) continue;
                Vector2 point = points[i] + delta * Mathf.Clamp01(Vector2.Dot(wanted - points[i], delta) / delta.sqrMagnitude);
                float candidate = Vector2.SqrMagnitude(point - wanted);
                if (candidate < distance) { distance = candidate; nearest = point; }
            }
        return nearest;
    }

    /// <summary>新足按实际单段骨长及足面触地，得到可锁定的真实落点。</summary>
    /// <param name="leg">原骨段。</param><param name="placement">已拟合的身体。</param>
    /// <param name="target">请求的落点方向。</param><param name="terrain">共同地表。</param><param name="motion">实际关节转动。</param>
    private static bool FitLanding(StandingLeg leg, BodySurfaceTransform placement, Vector3 target,
        AbilityHeightField terrain, out BodySurfaceTransform motion)
    {
        Vector3 pivot = placement.Matrix.MultiplyPoint3x4(leg.Bone.Pivot), original = placement.Matrix.MultiplyPoint3x4(leg.End) - pivot;
        Quaternion requested = BodySurfaceTransform.Between(original, target - pivot);
        Matrix4x4 aimed = new BodySurfaceTransform(pivot, pivot, requested).Matrix * placement.Matrix;
        // 触地的是实际足缘，足面中心不必落到地面；不能用中心够不到来拒绝真实可达的足缘。
        Vector3 source = leg.Foot.Points.OrderBy(point =>
        { Vector3 world = aimed.MultiplyPoint3x4(point); return world.z - BodyLocomotion.Floor(terrain, world, pivot.z, 0f); }).First();
        original = placement.Matrix.MultiplyPoint3x4(source) - pivot;
        Vector3 direction = aimed.MultiplyPoint3x4(source) - pivot; direction.z = 0f;
        if (direction.sqrMagnitude < .00000001f) { direction = original; direction.z = 0f; }
        direction.Normalize(); float length = original.magnitude, z = BodyLocomotion.Floor(terrain, target, pivot.z, 0f);
        motion = new BodySurfaceTransform(pivot, pivot, Quaternion.identity);
        for (int pass = 0; pass < 12; pass++)
        {
            float vertical = z - pivot.z;
            if (Mathf.Abs(vertical) > length) return false;
            Vector3 point = pivot + direction * Mathf.Sqrt(Mathf.Max(0f, length * length - vertical * vertical)); point.z = z;
            motion = new BodySurfaceTransform(pivot, pivot, BodySurfaceTransform.Between(original, point - pivot));
            Matrix4x4 map = motion.Matrix * placement.Matrix;
            float correction = leg.Foot.Points.Max(vertex =>
            { Vector3 world = map.MultiplyPoint3x4(vertex); return BodyLocomotion.Floor(terrain, world, pivot.z, 0f) - world.z; });
            if (Mathf.Abs(correction) < .0001f) return true;
            z += correction;
        }
        return false;
    }
}

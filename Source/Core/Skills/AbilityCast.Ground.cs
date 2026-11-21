using System;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>用实际作用形状检查共同地表，不把控制原点当作物体底部。</summary>
public sealed partial class AbilityCast
{
    /// <summary>只为可能着地且有内部骨架的部位准备上一姿态缓冲。</summary>
    private readonly FormPose[] groundPoses;

    /// <summary>稳定形状的平移在精确触地时点拆步，避免下落后再向上拉回一整帧。</summary>
    /// <param name="node">正在移动的部位。</param>
    /// <param name="maximum">其他共同边界允许的步长。</param>
    private float GroundStep(int node, float maximum)
    {
        NodeState state = nodes[node]; CompiledForm form = Plan.Nodes[node];
        if (terrain != null && state.Flying && !state.MovementStopped && state.Flight.Ground != ConstructionGroundResponse.Ignore)
        {
            float speed = new Vector2(state.Velocity.x, state.Velocity.y).magnitude;
            if (speed > .00001f) maximum = Mathf.Min(maximum, Mathf.Min(terrain.Size.x / terrain.Width, terrain.Size.y / terrain.Height) * .5f / speed);
        }
        if (terrain != null && (state.Velocity.x != 0 || state.Velocity.y != 0)) return maximum;
        if (!state.Flying || state.MovementStopped || state.Flight.Ground is not (ConstructionGroundResponse.Stop or ConstructionGroundResponse.End or ConstructionGroundResponse.EndWhenActive) || state.Flight.Path != null ||
            state.Flight.TurnRate > 0 || state.Spin.sqrMagnitude > .000001f ||
            (state.Motion?.Motion ?? form.Motion) != null && !state.ShapeHeld && !state.MotionStopped ||
            !state.GrowthStopped && (state.GrowthChanged ? state.GrowthAge < state.GrowthSeconds : form.Growth.x >= 0 && NodeAge(node) < form.Growth.y)) return maximum;
        float height = MinimumHeight(node, state.Transform, poses[node]);
        if (height <= .000001f || float.IsPositiveInfinity(height)) return maximum;
        float velocity = state.Velocity.z, acceleration = state.Acceleration.z, until = float.PositiveInfinity;
        if (Mathf.Abs(acceleration) < .000001f)
        { if (velocity < 0f) until = -height / velocity; }
        else
        {
            float discriminant = velocity * velocity - 2f * acceleration * height;
            if (discriminant >= 0f)
            {
                float root = Mathf.Sqrt(discriminant);
                float first = (-velocity - root) / acceleration, second = (-velocity + root) / acceleration;
                if (first > .000001f) until = first;
                if (second > .000001f) until = Mathf.Min(until, second);
            }
        }
        return Mathf.Min(maximum, until);
    }

    /// <summary>先查每个真实胶囊的前后底部，再保持首次接触姿态并贴齐实际底部。</summary>
    /// <param name="node">本轮已经推进的部位。</param>
    private void ResolveGround(int node)
    {
        if (flows?[node]?.Started == true) return;
        ref NodeState state = ref nodes[node]; CompiledForm form = Plan.Nodes[node];
        if (state.GroundEndPending)
        {
            if (ProcessIsActive(node, includeEnd: false)) EndGroundFlight(node);
            return;
        }
        if (state.Flying && state.Flight.Ground == ConstructionGroundResponse.Follow)
        { FollowGround(node); return; }
        if (!state.Flying || state.MovementStopped && state.Spin.sqrMagnitude < .000001f || state.Flight.Ground is not (ConstructionGroundResponse.Stop or ConstructionGroundResponse.End or ConstructionGroundResponse.EndWhenActive)) return;
        FormPose previousPose = groundPoses[node];
        float beforeRadius = MaximumScale(state.PreviousTransform), radius = MaximumScale(state.Transform), first = 2f;
        // 转移空隙没有实体经过，地面停止或结束只能发生在新落点，不能沿两端插值拉回。
        bool transferred = state.Flight.Transfer != null && state.TransferCount > 0 &&
            Mathf.Abs(Age - (state.FlightStartedAt + state.TransferCount * state.Flight.Transfer.Interval)) <= Mathf.Max(.0000001f, Age * .0000001f);
        if (transferred)
        {
            if (MinimumHeight(node, state.Transform, poses[node]) <= .00001f) first = 1f;
        }
        else
        {
            foreach (FormContactBinding contact in form.Resource.Contacts)
            {
                if (contact.IsPrism)
                {
                    if (!contact.IsFormed(poses[node])) continue;
                    bool existed = contact.IsFormed(previousPose);
                    var from = contact.PrismFrame(previousPose, state.PreviousTransform);
                    var to = contact.PrismFrame(poses[node], state.Transform);
                    for (int i = 0; i < to.VertexCount; i++)
                    {
                        Vector3 q = to.Vertex(i);
                        float height = q.z - TerrainHeight(q);
                        if (!existed)
                        {
                            if (height <= .00001f) first = Mathf.Min(first, 1f);
                            continue;
                        }
                        Vector3 p = from.Vertex(i);
                        Include(p.z - TerrainHeight(p), height);
                    }
                    continue;
                }
                Vector3 beforeA = state.PreviousTransform.MultiplyPoint3x4(contact.ABlend.Point(contact.A, previousPose));
                Vector3 beforeB = state.PreviousTransform.MultiplyPoint3x4(contact.BBlend.Point(contact.B, previousPose));
                Vector3 a = state.Transform.MultiplyPoint3x4(contact.ABlend.Point(contact.A, poses[node]));
                Vector3 b = state.Transform.MultiplyPoint3x4(contact.BBlend.Point(contact.B, poses[node]));
                Include(beforeA.z - TerrainHeight(beforeA) - beforeRadius * contact.Radius, a.z - TerrainHeight(a) - radius * contact.Radius);
                Include(beforeB.z - TerrainHeight(beforeB) - beforeRadius * contact.Radius, b.z - TerrainHeight(b) - radius * contact.Radius);
            }
            if (form.FragmentSource >= 0 && form.Resource.Contacts.Length == 0)
                Include(MinimumHeight(node, state.PreviousTransform, previousPose), MinimumHeight(node, state.Transform, poses[node]));
            else if (terrain != null || form.GroundPoints != null)
                Include(MinimumHeight(node, state.PreviousTransform, previousPose), MinimumHeight(node, state.Transform, poses[node]));
            if (state.LastStep == 0f && MinimumHeight(node, state.Transform, poses[node]) <= 0f) first = 0f;
        }
        if (first > 1f) return;
        StopNode(node, first);
        float lift = -MinimumHeight(node, state.Transform, poses[node]);
        state.Position.z += lift; state.Transform = Matrix4x4.TRS(state.Position, state.Rotation, state.Scale * Mathf.Max(.00001f, state.GrowthValue));
        if (form.FragmentSource >= 0) state.Transform *= Matrix4x4.Translate(-state.Pivot);
        for (int i = node + 1; i < nodes.Length; i++) if (IsAttachedBelow(i, node))
        { Matrix4x4 matrix = nodes[i].Transform; matrix.m23 += lift; nodes[i].Transform = matrix; }
        if (state.Flight.Ground == ConstructionGroundResponse.EndWhenActive && !ProcessIsActive(node, includeEnd: false))
            state.GroundEndPending = true;
        else if (state.Flight.Ground != ConstructionGroundResponse.Stop)
            EndGroundFlight(node);
        void Include(float before, float after)
        {
            if (after <= .00001f && after < before - .000001f)
                first = Mathf.Min(first, float.IsPositiveInfinity(before) ? 1f : before <= 0f ? 0f : Mathf.Clamp01(before / (before - after)));
        }
    }

    /// <summary>在已经取得的落点结束，允许共同接触处理读取最后一段飞行。</summary>
    /// <param name="node">已经实际着地的部位。</param>
    private void EndGroundFlight(int node)
    {
        nodes[node].GroundEndPending = false;
        nodes[node].FlightEnded = true;
        EndNode(node);
    }

    /// <summary>从相同作用形状取得真正最低高度。</summary>
    /// <param name="node">实际部位。</param>
    /// <param name="matrix">共同世界姿态。</param>
    /// <param name="pose">该时点的内部姿态。</param>
    /// <param name="useTerrain">沿地柔性曲面先按平面底部定位，再独立挠曲。</param>
    private float MinimumHeight(int node, Matrix4x4 matrix, FormPose pose, bool useTerrain = true)
    {
        float minimum = float.PositiveInfinity, scale = MaximumScale(matrix);
        foreach (FormContactBinding contact in Plan.Nodes[node].Resource.Contacts)
        {
            if (contact.IsPrism)
            {
                if (!contact.IsFormed(pose)) continue;
                var box = contact.PrismFrame(pose, matrix);
                for (int i = 0; i < box.VertexCount; i++)
                {
                    Vector3 point = box.Vertex(i);
                    minimum = Mathf.Min(minimum, point.z - (useTerrain ? TerrainHeight(point) : 0f));
                }
                continue;
            }
            Vector3 a = matrix.MultiplyPoint3x4(contact.ABlend.Point(contact.A, pose));
            Vector3 b = matrix.MultiplyPoint3x4(contact.BBlend.Point(contact.B, pose));
            if (!useTerrain || terrain == null)
                minimum = Mathf.Min(minimum, Mathf.Min(a.z, b.z) - contact.Radius * scale);
            else
            {
                float distance = Vector2.Distance(a, b), cell = Mathf.Min(terrain.Size.x / terrain.Width, terrain.Size.y / terrain.Height);
                int samples = Mathf.Max(1, Mathf.CeilToInt(distance / cell));
                for (int sample = 0; sample <= samples; sample++)
                { Vector3 point = Vector3.Lerp(a, b, (float)sample / samples); minimum = Mathf.Min(minimum, point.z - TerrainHeight(point) - contact.Radius * scale); }
            }
        }
        if (Plan.Nodes[node].GroundPoints != null)
            foreach (FormSurfaceVertex vertex in Plan.Nodes[node].GroundPoints)
            {
                Vector3 world = matrix.MultiplyPoint3x4(vertex.Sample(pose));
                minimum = Mathf.Min(minimum, world.z - (useTerrain ? TerrainHeight(world) : 0f));
            }
        if (Plan.Nodes[node].FragmentSource >= 0 && Plan.Nodes[node].Resource.Contacts.Length == 0)
        {
            Bounds bounds = fragmentMotion[node].Bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 world = matrix.MultiplyPoint3x4(point);
                minimum = Mathf.Min(minimum, world.z - (useTerrain ? TerrainHeight(world) : 0f));
            }
        }
        return minimum;
    }

    /// <summary>沿用作用形状的最大尺寸半径约定。</summary>
    /// <param name="matrix">实际世界姿态。</param>
    private static float MaximumScale(Matrix4x4 matrix)
    {
        Vector3 scale = matrix.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}

using System;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>用实际作用形状检查地图零高度平面，不把控制原点当作物体底部。</summary>
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
        if (!state.Flying || state.MovementStopped || state.Flight.Ground != ConstructionGroundResponse.Stop || state.Flight.Path != null ||
            state.Flight.TurnRate > 0 || state.Spin.sqrMagnitude > .000001f ||
            (state.Motion?.Motion ?? form.Motion) != null && !state.ShapeHeld && !state.MotionStopped ||
            !state.GrowthStopped && (state.GrowthChanged ? state.GrowthAge < state.GrowthSeconds : form.Growth.x >= 0 && NodeAge(node) < form.Growth.y)) return maximum;
        float height = MinimumHeight(node, state.Transform, poses[node]);
        if (height <= .000001f) return maximum;
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
        ref NodeState state = ref nodes[node]; CompiledForm form = Plan.Nodes[node];
        if (!state.Flying || state.MovementStopped && state.Spin.sqrMagnitude < .000001f || state.Flight.Ground != ConstructionGroundResponse.Stop) return;
        FormPose previousPose = groundPoses[node];
        float beforeRadius = MaximumScale(state.PreviousTransform), radius = MaximumScale(state.Transform), first = 2f;
        foreach (FormContactBinding contact in form.Resource.Contacts)
        {
            Vector3 beforeA = state.PreviousTransform.MultiplyPoint3x4(contact.ABlend.Point(contact.A, previousPose));
            Vector3 beforeB = state.PreviousTransform.MultiplyPoint3x4(contact.BBlend.Point(contact.B, previousPose));
            Vector3 a = state.Transform.MultiplyPoint3x4(contact.ABlend.Point(contact.A, poses[node]));
            Vector3 b = state.Transform.MultiplyPoint3x4(contact.BBlend.Point(contact.B, poses[node]));
            Include(beforeA.z - beforeRadius * contact.Radius, a.z - radius * contact.Radius);
            Include(beforeB.z - beforeRadius * contact.Radius, b.z - radius * contact.Radius);
        }
        if (form.FragmentSource >= 0 && form.Resource.Contacts.Length == 0)
            Include(MinimumHeight(node, state.PreviousTransform, previousPose), MinimumHeight(node, state.Transform, poses[node]));
        if (first > 1f) return;
        StopNode(node, first);
        float lift = -MinimumHeight(node, state.Transform, poses[node]);
        state.Position.z += lift; state.Transform = Matrix4x4.TRS(state.Position, state.Rotation, state.Scale * Mathf.Max(.00001f, state.GrowthValue));
        if (form.FragmentSource >= 0) state.Transform *= Matrix4x4.Translate(-state.Pivot);
        for (int i = node + 1; i < nodes.Length; i++) if (IsAttachedBelow(i, node))
        { Matrix4x4 matrix = nodes[i].Transform; matrix.m23 += lift; nodes[i].Transform = matrix; }
        void Include(float before, float after)
        {
            if (after <= .00001f && after < before - .000001f)
                first = Mathf.Min(first, before <= 0f ? 0f : Mathf.Clamp01(before / (before - after)));
        }
    }

    /// <summary>从相同作用形状取得真正最低高度。</summary>
    /// <param name="node">实际部位。</param>
    /// <param name="matrix">共同世界姿态。</param>
    /// <param name="pose">该时点的内部姿态。</param>
    private float MinimumHeight(int node, Matrix4x4 matrix, FormPose pose)
    {
        float minimum = float.PositiveInfinity, scale = MaximumScale(matrix);
        foreach (FormContactBinding contact in Plan.Nodes[node].Resource.Contacts)
        {
            Vector3 a = matrix.MultiplyPoint3x4(contact.ABlend.Point(contact.A, pose));
            Vector3 b = matrix.MultiplyPoint3x4(contact.BBlend.Point(contact.B, pose));
            minimum = Mathf.Min(minimum, Mathf.Min(a.z, b.z) - contact.Radius * scale);
        }
        if (Plan.Nodes[node].FragmentSource >= 0 && Plan.Nodes[node].Resource.Contacts.Length == 0)
        {
            Bounds bounds = fragmentMotion[node].Bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                minimum = Mathf.Min(minimum, matrix.MultiplyPoint3x4(point).z);
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

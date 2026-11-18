using System;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>射线仍是同一次普通部位，显示与作用共同读取实际伸展骨架。</summary>
public sealed partial class AbilityCast
{
    /// <summary>登记普通时点、条件和命名步骤的共同执行，不建立另一套施放。</summary>
    private static void RegisterRay()
    {
        ConstructionOperations.Register(ConstructionOperationKind.Ray,
            c => new CompiledRay(c.Source.Ray, c.Resource, c.Path),
            (cast, action, parameters) =>
            {
                ref NodeState state = ref cast.nodes[action.Node];
                state.Ray = parameters; state.RayTarget = state.HasTarget ? state.Target : cast.samplingTarget;
                state.RayInitialized = false; state.RayClipped = false; state.MovementStopped = false;
                cast.SampleRay(action.Node, 0f, cast.samplingTarget);
                foreach (int child in cast.Plan.Nodes[action.Node].Children) cast.QueueBirth(child);
                state.Revision++; return true;
            },
            (cast, action, parameters, native) => new ConstructionOperationProgress(
                cast.nodes[action.Node].MovementStopped, !ReferenceEquals(cast.nodes[action.Node].Ray, parameters)));
    }

    /// <summary>当前部位是否正在使用真实射线，身体作用面不借用此规则。</summary>
    /// <param name="node">实际部位。</param>
    internal bool IsRay(int node) => node >= 0 && nodes[node].Ray != null;

    /// <summary>先固定真实接触前缀，再共同更新骨架、作用端点及附属物；保留力量和命中身份。</summary>
    /// <param name="node">同一部位。</param><param name="length">包含厚度的世界长度。</param>
    internal void ClipRay(int node, float length)
    {
        ref NodeState state = ref nodes[node];
        state.RayLength = Mathf.Min(state.RayLength, Mathf.Max(0f, length) / MaximumScale(state.Transform));
        StopNode(node, 1f);
        state.RayClipped = true;
        state.Revision++;
        Refresh(inputs.Target);
    }

    /// <summary>先更新有限指向，再沿原骨架伸展；横截面不随长度膨胀。</summary>
    /// <param name="node">同一实际部位。</param><param name="step">本步世界秒数。</param><param name="target">实际目标。</param>
    private void SampleRay(int node, float step, Vector3 target)
    {
        ref NodeState state = ref nodes[node]; CompiledRay ray = state.Ray;
        if (ray == null) return;
        Vector3 start = state.Transform.GetColumn(3), aim = (ray.FollowTarget ? state.HasTarget ? state.Target : target : state.RayTarget) - start;
        float scale = MaximumScale(state.Transform), minimum = ray.Span.Radius * 2f;
        if (!state.MovementStopped)
        {
            if (aim.sqrMagnitude > .00000001f)
            {
                Quaternion previous = state.RayInitialized ? state.RayRotation : state.Transform.rotation;
                Vector3 direction = previous * ray.Span.Axis;
                Quaternion turn = Quaternion.FromToRotation(direction, aim.normalized);
                if (state.RayInitialized && ray.TurnRate > 0f) turn = Quaternion.RotateTowards(Quaternion.identity, turn, ray.TurnRate * step);
                state.RayRotation = turn * previous;
            }
            else state.RayRotation = state.Transform.rotation;
            state.RayLength = Mathf.Min(ray.Length, aim.magnitude / Mathf.Max(.00001f, scale));
            state.RayInitialized = true;
        }
        state.Transform = Matrix4x4.TRS(start, state.RayRotation, state.Transform.lossyScale);
        if (state.Free) state.Rotation = state.RayRotation;
        var resource = Plan.Nodes[node].Resource; var pose = poses[node];
        resource.Rig.Sample(null, 0f, pose);
        Matrix4x4 shift = Matrix4x4.Translate(ray.Span.Axis * (Mathf.Max(minimum, state.RayLength) - ray.Span.Length));
        pose.World[ray.Span.Control] = shift * pose.World[ray.Span.Control];
        pose.Skin[ray.Span.Control] = shift * pose.Skin[ray.Span.Control];
        resource.Rig.RebuildBounds(pose);
        if (state.RayLength < minimum) state.Visible = false;
    }
}

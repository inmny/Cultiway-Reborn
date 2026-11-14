using System;
using System.IO;
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
                state.Ray = parameters; state.RayTarget = cast.samplingTarget;
                state.RayInitialized = false; state.MovementStopped = false;
                cast.SampleRay(action.Node, 0f, cast.samplingTarget);
                foreach (int child in cast.Plan.Nodes[action.Node].Children) cast.QueueBirth(child);
                state.Revision++; return true;
            },
            (cast, action, parameters, native) => new ConstructionOperationProgress(
                cast.nodes[action.Node].MovementStopped, !ReferenceEquals(cast.nodes[action.Node].Ray, parameters)),
            validate: (form, parameters, path) =>
            {
                if (form.EventOperations.Count(o => o.Ray != null) != 1 || form.FlowSettings != null || form.Fracture != null ||
                    form.Residue != null || form.EventOperations.Any(o => o.Motion != null || o.Propagation != null))
                    throw new InvalidDataException(path + "：同一伸展控制需要一项射线；流散、碎解和其他骨架过程请使用独立后续部位");
                if (form.OnContact != ConstructionContactResponse.Continue)
                    throw new InvalidDataException(path + "：射线接触截断尚未接入，请明确使用穿透或等待截断功能");
            });
    }

    /// <summary>先更新有限指向，再沿原骨架伸展；横截面不随长度膨胀。</summary>
    /// <param name="node">同一实际部位。</param><param name="step">本步世界秒数。</param><param name="target">实际目标。</param>
    private void SampleRay(int node, float step, Vector3 target)
    {
        ref NodeState state = ref nodes[node]; CompiledRay ray = state.Ray;
        if (ray == null) return;
        Vector3 start = state.Transform.GetColumn(3), aim = (ray.FollowTarget ? target : state.RayTarget) - start;
        float scale = MaximumScale(state.Transform), minimum = ray.Span.Radius * 2f;
        if (!state.MovementStopped)
        {
            if (aim.sqrMagnitude > .00000001f)
            {
                Quaternion previous = state.RayInitialized ? state.RayRotation : state.Transform.rotation;
                Vector3 direction = previous * ray.Span.Axis;
                Quaternion turn = Quaternion.FromToRotation(direction, aim.normalized);
                if (state.RayInitialized) turn = Quaternion.RotateTowards(Quaternion.identity, turn, ray.TurnRate * step);
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

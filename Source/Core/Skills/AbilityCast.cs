using System;
using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>同一姿态计算出的世界胶囊，不采用独立的视觉半径。</summary>
public readonly struct AbilityCapsule
{
    /// <summary>所属展开部位。</summary>
    public readonly int Node;
    /// <summary>轴线两端。</summary>
    public readonly Vector3 A, B;
    /// <summary>世界尺寸半径。</summary>
    public readonly float Radius;
    /// <summary>该部位的实际材料，不随显示染色改变。</summary>
    public readonly string Matter;
    /// <summary>从已经确定的姿态记录作用形状。</summary>
    /// <param name="node">展开部位。</param>
    /// <param name="a">轴线起点。</param>
    /// <param name="b">轴线终点。</param>
    /// <param name="radius">实际半径。</param>
    /// <param name="matter">实际材料。</param>
    internal AbilityCapsule(int node, Vector3 a, Vector3 b, float radius, string matter)
    { Node = node; A = a; B = b; Radius = radius; Matter = matter; }

    /// <summary>检查是否与人物或其他对象的球形部位真实相交。</summary>
    /// <param name="centre">目标部位中心。</param>
    /// <param name="radius">目标部位半径。</param>
    public bool Intersects(Vector3 centre, float radius)
    {
        Vector3 axis = B - A;
        float t = axis.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(centre - A, axis) / axis.sqrMagnitude) : 0f;
        float sum = Radius + radius;
        return (centre - A - axis * t).sqrMagnitude <= sum * sum;
    }
}

/// <summary>单次施放独占的形体状态；计算只访问值资料，不访问游戏或网格。</summary>
public sealed class AbilityCast
{
    /// <summary>开始时确定的共享计划。</summary>
    public CompiledAbility Plan { get; }
    /// <summary>当前世界秒数。</summary>
    public float Age { get; private set; }
    /// <summary>达到总持续后不再发布形体。</summary>
    public bool Finished => Age >= Plan.Lifetime;
    /// <summary>本次稳定的根姿态。</summary>
    private readonly Matrix4x4 origin;
    /// <summary>与计划一一对应的本次可变状态。</summary>
    private readonly NodeState[] nodes;

    /// <summary>为一次施放建立状态，目标由后续每步提供。</summary>
    /// <param name="plan">完成编译的计划。</param>
    /// <param name="origin">本次地图空间根姿态。</param>
    public AbilityCast(CompiledAbility plan, Matrix4x4 origin)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan)); this.origin = origin;
        nodes = new NodeState[plan.Nodes.Length];
        for (int i = 0; i < nodes.Length; i++) nodes[i] = new NodeState();
        Sample(0f, Vector3.zero);
    }

    /// <summary>按世界时间推进；显示、挂点与接触形状之后均读取此状态。</summary>
    /// <param name="seconds">逻辑步长，零不推进。</param>
    /// <param name="target">本次目标的当前地图空间位置。</param>
    public void Advance(float seconds, Vector3 target)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (Finished || seconds == 0f) return;
        float remaining = Mathf.Min(seconds, Plan.Lifetime - Age);
        // 在操作时点拆步，脱落和开始飞行不会早执行一整帧。
        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, 1f / 60f);
            foreach (CompiledForm form in Plan.Nodes)
            {
                if (form.Detach > Age && form.Detach < Age + step) step = form.Detach - Age;
                if (form.FlyAt > Age && form.FlyAt < Age + step) step = form.FlyAt - Age;
            }
            Age += step; Sample(step, target); remaining -= step;
        }
    }

    /// <summary>父体先推进，子体依赖同一时刻的父姿态；脱落只发生一次。</summary>
    /// <param name="step">当前世界步长。</param>
    /// <param name="target">当前目标位置。</param>
    private void Sample(float step, Vector3 target)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i]; NodeState state = nodes[i];
            Matrix4x4 previous = state.Transform;
            float growth = form.Growth.x < 0 ? 1f : Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(form.Growth.x, form.Growth.y, Age));
            Quaternion rotation = form.Rotation * Quaternion.Euler(form.Spin * Mathf.Max(0f, Age - form.SpinAt));
            Matrix4x4 attached = (form.Parent < 0 ? origin : nodes[form.Parent].Transform) * form.Anchor *
                Matrix4x4.TRS(form.Offset, rotation, Vector3.one * form.Scale);
            if (!state.Free && (form.Parent < 0 && form.FlyAt >= 0 && Age >= form.FlyAt || form.Detach >= 0 && Age >= form.Detach))
            {
                state.Free = true; state.Position = attached.GetColumn(3); state.Rotation = attached.rotation;
                state.Scale = attached.lossyScale;
                state.Velocity = step > 0f ? ((Vector3)attached.GetColumn(3) - (Vector3)previous.GetColumn(3)) / step : Vector3.zero;
            }
            if (state.Free)
            {
                float releasedAt = form.Parent < 0 ? form.FlyAt : form.Detach;
                state.Rotation *= Quaternion.Euler(form.Spin * Mathf.Min(step, Mathf.Max(0f, Age - Mathf.Max(releasedAt, form.SpinAt))));
                if (!state.Flying && form.FlyAt >= 0 && Age >= form.FlyAt)
                { state.Flying = true; state.Velocity += state.Rotation * form.Velocity; }
                if (state.Flying && form.TurnRate > 0f && state.Velocity.sqrMagnitude > 1e-8f && (target - state.Position).sqrMagnitude > 1e-8f)
                {
                    Vector3 before = state.Velocity;
                    state.Velocity = Vector3.RotateTowards(before, (target - state.Position).normalized * before.magnitude,
                        form.TurnRate * Mathf.Deg2Rad * step, 0f);
                    state.Rotation = Quaternion.FromToRotation(before, state.Velocity) * state.Rotation;
                }
                float freeSince = form.Parent < 0 ? form.FlyAt : form.Detach;
                state.Position += state.Velocity * Mathf.Min(step, Mathf.Max(0f, Age - freeSince));
                state.Transform = Matrix4x4.TRS(state.Position, state.Rotation, state.Scale * Mathf.Max(.00001f, growth));
            }
            else state.Transform = attached * Matrix4x4.Scale(Vector3.one * Mathf.Max(.00001f, growth));
            state.Visible = growth > .0001f;
        }
    }

    /// <summary>发布所有仍存在的形体，绘制方只读同一变换。</summary>
    /// <param name="output">本轮显示集合。</param>
    public void AppendVisuals(List<FormVisualInstance> output)
    {
        if (Finished) return;
        for (int i = 0; i < nodes.Length; i++)
            if (nodes[i].Visible) output.Add(new FormVisualInstance(Plan.Nodes[i].Resource, nodes[i].Transform, surface: Plan.Nodes[i].Surface));
    }

    /// <summary>从相同部位姿态生成有效胶囊；未长出的部分不会提前造成作用。</summary>
    /// <param name="output">本轮作用形状。</param>
    public void AppendContacts(List<AbilityCapsule> output)
    {
        if (Finished) return;
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i]; NodeState state = nodes[i];
            if (!state.Visible || Age < form.Active.x || Age > form.Active.y) continue;
            foreach (FormContactDescription contact in form.Resource.Description.Contacts)
            {
                Matrix4x4 matrix = state.Transform;
                float[] a = contact.A, b = contact.B;
                Vector3 scale = matrix.lossyScale;
                output.Add(new AbilityCapsule(i, matrix.MultiplyPoint3x4(new Vector3(a[0], a[1], a[2])),
                    matrix.MultiplyPoint3x4(new Vector3(b[0], b[1], b[2])),
                    contact.Radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)), form.Matter));
            }
        }
    }

    /// <summary>一个独立形体的必要可变状态。</summary>
    private sealed class NodeState
    {
        /// <summary>当前完整变换。</summary>
        internal Matrix4x4 Transform;
        /// <summary>已解除连接、已开始飞行和是否长成可见。</summary>
        internal bool Free, Flying, Visible;
        /// <summary>脱落后的独立位置、尺寸及速度。</summary>
        internal Vector3 Position, Scale, Velocity;
        /// <summary>脱落后的朝向。</summary>
        internal Quaternion Rotation;
    }
}

using System;
using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>一次施放的实际阶段，接触是否发生另由真实作用结果记录。</summary>
public enum AbilityCastPhase
{
    /// <summary>起势及蓄力。</summary>
    Preparing,
    /// <summary>实际有效过程。</summary>
    Active,
    /// <summary>动作回收。</summary>
    Recovering,
    /// <summary>自然完成。</summary>
    Finished,
    /// <summary>身体或来源失效而中断。</summary>
    Interrupted
}

/// <summary>同一姿态计算出的世界胶囊，不采用独立的视觉半径。</summary>
public readonly struct AbilityCapsule
{
    /// <summary>所属展开部位。</summary>
    public readonly int Node;
    /// <summary>同一部位内的作用形状索引，跨步稳定对应。</summary>
    public readonly int Shape;
    /// <summary>取得形状时部位的变化版本，用于作废接触改变前的旧路径。</summary>
    public readonly int Revision;
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
    /// <param name="shape">部位内的形状索引。</param>
    /// <param name="revision">取得形状时的变化版本。</param>
    internal AbilityCapsule(int node, Vector3 a, Vector3 b, float radius, string matter, int shape = 0, int revision = 0)
    { Node = node; A = a; B = b; Radius = radius; Matter = matter; Shape = shape; Revision = revision; }

    /// <summary>检查是否与人物或其他对象的球形部位真实相交。</summary>
    /// <param name="centre">目标部位中心。</param>
    /// <param name="radius">目标部位半径。</param>
    public bool Intersects(Vector3 centre, float radius)
    {
        float sum = Radius + radius;
        return (centre - ClosestAxisPoint(centre)).sqrMagnitude <= sum * sum;
    }

    /// <summary>取得当前作用轴线上距离接触对象最近的位置。</summary>
    /// <param name="point">对象身体位置。</param>
    public Vector3 ClosestAxisPoint(Vector3 point)
    {
        Vector3 axis = B - A;
        float t = axis.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(point - A, axis) / axis.sqrMagnitude) : 0f;
        return A + axis * t;
    }

    /// <summary>按本步两端的连续位移检查接触，避免手掌或刃部恰好跨过目标而漏算。</summary>
    /// <param name="previous">同一作用部位的上一步形状。</param>
    /// <param name="centre">目标部位中心。</param>
    /// <param name="radius">目标部位半径。</param>
    /// <param name="point">首次接触附近的轴线点。</param>
    public bool SweepFrom(in AbilityCapsule previous, Vector3 centre, float radius, out Vector3 point)
        => SweepFrom(in previous, centre, radius, out point, out _);

    /// <summary>同时返回本步内首次接触的进度，使停止及反馈发生在实际接触处。</summary>
    /// <param name="previous">上一步相同形状。</param>
    /// <param name="centre">目标中心。</param>
    /// <param name="radius">目标半径。</param>
    /// <param name="point">接触附近的轴线点。</param>
    /// <param name="fraction">本步内从零到一的首次接触进度。</param>
    public bool SweepFrom(in AbilityCapsule previous, Vector3 centre, float radius, out Vector3 point, out float fraction)
    {
        Vector3 va = A - previous.A, vb = B - previous.B;
        float speed = Mathf.Max(va.magnitude, vb.magnitude) + Mathf.Abs(Radius - previous.Radius), at = 0f;
        point = A; fraction = 1f;
        // 距离每次最多按两端最大速度缩小，前进到安全边界，不固定抽取几个时点猜命中。
        for (int iteration = 0; iteration < 64; iteration++)
        {
            Vector3 a = previous.A + va * at, b = previous.B + vb * at, axis = b - a;
            float along = axis.sqrMagnitude > 1e-10f ? Mathf.Clamp01(Vector3.Dot(centre - a, axis) / axis.sqrMagnitude) : 0f;
            point = a + axis * along;
            float distance = Vector3.Distance(centre, point) - Mathf.Lerp(previous.Radius, Radius, at) - radius;
            if (distance <= .00001f) { fraction = at; return true; }
            if (speed <= 1e-10f) return false;
            at += distance / speed;
            if (at > 1f) return false;
        }
        return AbilitySweepPolynomial.Find(in previous, in this, centre, radius, out point, out fraction);
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
    public bool Finished => cancelled || Age >= Plan.Lifetime || aliveNodes == 0 &&
        (Plan.Body == null || bodyInterrupted || Age >= Plan.Body.Duration);
    /// <summary>仍存在的生成形体数。</summary>
    private int aliveNodes;
    /// <summary>是否已经明确中断，禁止后续显示或接触恢复运行。</summary>
    private bool cancelled;
    /// <summary>读取同一时钟下的施放阶段。</summary>
    public AbilityCastPhase Phase => cancelled ? AbilityCastPhase.Interrupted : Finished ? AbilityCastPhase.Finished :
        Plan.Body == null || !ControlsBody ? AbilityCastPhase.Active : Age < Plan.Body.ActiveFrom ? AbilityCastPhase.Preparing :
        Age <= Plan.Body.ActiveUntil ? AbilityCastPhase.Active : AbilityCastPhase.Recovering;
    /// <summary>同一动作时钟下的真实身体姿态，非身体能力没有此状态。</summary>
    public BodyPose? BodyPose { get; private set; }
    /// <summary>身体尚在动作中；其结束不强制回收已离手形体。</summary>
    public bool ControlsBody => Plan.Body != null && !bodyInterrupted && !Finished && Age < Plan.Body.Duration;
    /// <summary>身体中断后不再次取得控制。</summary>
    private bool bodyInterrupted;
    /// <summary>本步由来源采集的身体输入，不持有游戏对象。</summary>
    private BodyFrame bodyFrame;
    /// <summary>本次稳定的根姿态。</summary>
    private Matrix4x4 origin;
    /// <summary>本步实际施放输入，不持有来源对象。</summary>
    private AbilityInputs inputs;
    /// <summary>与计划一一对应的本次可变状态。</summary>
    private readonly NodeState[] nodes;
    /// <summary>预测接触时复用的前态，回退不会重新生成整次施放。</summary>
    private readonly NodeState[] savedNodes;
    /// <summary>预测前的共同时间、姿态、连接及结束状态。</summary>
    private float savedAge;
    /// <summary>预测前存在的部位数。</summary>
    private int savedAliveNodes;
    /// <summary>预测前的取消及身体中断状态。</summary>
    private bool savedCancelled, savedBodyInterrupted;
    /// <summary>预测前的身体及连接输入。</summary>
    private BodyFrame savedBodyFrame;
    /// <summary>预测前的外部连接输入。</summary>
    private AbilityInputs savedInputs;
    /// <summary>预测前的共同身体姿态。</summary>
    private BodyPose? savedBodyPose;
    /// <summary>预测前的根姿态。</summary>
    private Matrix4x4 savedOrigin;

    /// <summary>为一次施放建立状态，目标由后续每步提供。</summary>
    /// <param name="plan">完成编译的计划。</param>
    /// <param name="origin">本次地图空间根姿态。</param>
    /// <param name="body">借用身体时必须提供的实际尺寸及脚下位置。</param>
    /// <param name="inputs">采用根连接时的实际施放输入。</param>
    /// <param name="force">来源支付后确定的基础作用量；各部位按强度分配。</param>
    public AbilityCast(CompiledAbility plan, Matrix4x4 origin, BodyFrame? body = null, AbilityInputs? inputs = null, float force = 1f)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan)); this.origin = origin;
        if (plan.Body != null && body == null) throw new ArgumentException("身体动作缺少实际身体输入", nameof(body));
        if (plan.Binding != null && inputs == null) throw new ArgumentException("根连接缺少实际施放输入", nameof(inputs));
        if (float.IsNaN(force) || float.IsInfinity(force) || force < 0f) throw new ArgumentOutOfRangeException(nameof(force));
        bodyFrame = body.GetValueOrDefault();
        this.inputs = inputs.GetValueOrDefault();
        nodes = new NodeState[plan.Nodes.Length];
        savedNodes = new NodeState[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            float strength = force * plan.Nodes[i].Strength;
            if (float.IsInfinity(strength)) throw new ArgumentOutOfRangeException(nameof(force), "部位强度超出有限作用量");
            nodes[i] = new NodeState { Exists = true, Force = strength };
        }
        aliveNodes = nodes.Length;
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
        while (remaining > 0f && !Finished)
        {
            float step = NextStep(remaining);
            Age += step; Sample(step, target); remaining -= step;
        }
    }

    /// <summary>动作及作用时点统一拆步，外部结算与内部姿态不使用两套时间边界。</summary>
    /// <param name="maximum">本步尚可推进的时间。</param>
    public float NextStep(float maximum)
    {
        float step = Mathf.Min(maximum, 1f / 60f, Mathf.Max(0f, Plan.Lifetime - Age));
        foreach (CompiledForm form in Plan.Nodes)
        {
            if (form.Spin.sqrMagnitude > .0001f) step = Mathf.Min(step, 6f / form.Spin.magnitude);
            if (form.TurnRate > 0f) step = Mathf.Min(step, 6f / form.TurnRate);
            Include(form.Detach); Include(form.FlyAt); Include(form.Active.x); Include(form.Active.y);
            Include(form.Growth.x); Include(form.Growth.y);
        }
        if (Plan.Body != null)
        {
            Include(Plan.Body.ActiveFrom); Include(Plan.Body.ActiveUntil);
            Include(Plan.Body.Duration);
            if (Plan.Body.MaximumAngularRate > 0f) step = Mathf.Min(step, 6f / Plan.Body.MaximumAngularRate);
        }
        return step;
        void Include(float marker) { if (marker > Age && marker < Age + step) step = marker - Age; }
    }

    /// <summary>父体先推进，子体依赖同一时刻的父姿态；脱落只发生一次。</summary>
    /// <param name="step">当前世界步长。</param>
    /// <param name="target">当前目标位置。</param>
    private void Sample(float step, Vector3 target)
    {
        if (Plan.Body != null && !bodyInterrupted) BodyPose = Plan.Body.Sample(Mathf.Min(Age, Plan.Body.Duration), in bodyFrame);
        if (Plan.Binding != null && (Age == 0f || Plan.Binding.Follow)) origin = inputs.Resolve(Plan.Binding, BodyPose);
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i]; ref NodeState state = ref nodes[i];
            if (!state.Exists) continue;
            Matrix4x4 previous = state.Transform;
            state.PreviousTransform = previous;
            float growth = form.Growth.x < 0 ? 1f : Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(form.Growth.x, form.Growth.y, Age));
            Quaternion rotation = form.Rotation * Quaternion.Euler(form.Spin * Mathf.Max(0f, Age - form.SpinAt));
            Matrix4x4 attached = (form.Parent < 0 ? origin : nodes[form.Parent].Transform) * form.Anchor *
                Matrix4x4.TRS(form.Offset, rotation, Vector3.one * form.Scale);
            if (!state.Free && form.ReleaseAt >= 0f && Age >= form.ReleaseAt)
            {
                state.Free = true; state.Position = attached.GetColumn(3); state.Rotation = attached.rotation;
                state.ReleasedAt = form.ReleaseAt;
                state.Scale = attached.lossyScale;
                state.Velocity = step > 0f ? ((Vector3)attached.GetColumn(3) - (Vector3)previous.GetColumn(3)) / step : Vector3.zero;
            }
            if (state.Free)
            {
                float freeStep = Mathf.Min(step, Mathf.Max(0f, Age - state.ReleasedAt));
                float flightStep = freeStep;
                if (!state.Stopped)
                {
                    state.Rotation *= Quaternion.Euler(form.Spin * Mathf.Min(freeStep, Mathf.Max(0f, Age - form.SpinAt)));
                    if (!state.Flying && form.FlyAt >= 0 && Age >= form.FlyAt)
                    {
                        flightStep = Mathf.Min(freeStep, Mathf.Max(0f, Age - form.FlyAt));
                        state.Position += state.Velocity * (freeStep - flightStep);
                        state.Flying = true; state.Velocity += state.Rotation * form.Velocity;
                    }
                    if (state.Flying && form.TurnRate > 0f && state.Velocity.sqrMagnitude > 1e-8f && (target - state.Position).sqrMagnitude > 1e-8f)
                    {
                        Vector3 before = state.Velocity;
                        state.Velocity = Vector3.RotateTowards(before, (target - state.Position).normalized * before.magnitude,
                            form.TurnRate * Mathf.Deg2Rad * flightStep, 0f);
                        state.Rotation = Quaternion.FromToRotation(before, state.Velocity) * state.Rotation;
                    }
                    state.Position += state.Velocity * flightStep;
                }
                state.Transform = Matrix4x4.TRS(state.Position, state.Rotation,
                    state.Stopped ? state.Scale : state.Scale * Mathf.Max(.00001f, growth));
            }
            else state.Transform = attached * Matrix4x4.Scale(Vector3.one * Mathf.Max(.00001f, growth));
            state.Visible = state.Stopped || growth > .0001f;
        }
    }

    /// <summary>由来源更新真实身体的脚下位置及尺寸，不在显示回调采集或推进。</summary>
    /// <param name="frame">当前逻辑步的实际身体。</param>
    public void UpdateBody(in BodyFrame frame) => bodyFrame = frame;

    /// <summary>每个逻辑步更新连接输入，显示不会重新采集或推进。</summary>
    /// <param name="frame">本步实际来源及目标。</param>
    public void UpdateInputs(in AbilityInputs frame) => inputs = frame;

    /// <summary>中断身体；已经离手的形体继续，仍依赖手部的形体同时结束。</summary>
    public void InterruptBody()
    {
        bodyInterrupted = true;
        if (nodes.Length == 0 || Plan.Binding?.Anchor == AbilityAnchor.Hand && !nodes[0].Free) Cancel();
    }

    /// <summary>作用部位编译后的共用去重组；负一为身体。</summary>
    /// <param name="node">本次生成形状的部位。</param>
    public int ContactGroup(int node) => node < 0 ? Plan.Nodes.Length : Plan.Nodes[node].ContactGroup;

    /// <summary>只接受当前形体版本的候选，连接或运动改变前的路径不继续结算。</summary>
    /// <param name="capsule">此前采集的形状。</param>
    public bool IsCurrent(in AbilityCapsule capsule) => !cancelled && (capsule.Node < 0 ? !bodyInterrupted :
        nodes[capsule.Node].Exists && nodes[capsule.Node].Revision == capsule.Revision);

    /// <summary>确认真实接触后改变共同状态，末端位置按本步首次相交进度回到接触处。</summary>
    /// <param name="node">实际发生接触的部位。</param>
    /// <param name="fraction">本步内实际接触进度。</param>
    public void ConfirmContact(int node, float fraction)
    {
        if (node < 0) return;
        ref NodeState state = ref nodes[node];
        if (!state.Exists) return;
        switch (Plan.Nodes[node].OnContact)
        {
            case ConstructionContactResponse.Stop:
                if (state.Stopped) return;
                Matrix4x4 old = state.Transform, previous = state.PreviousTransform;
                state.Position = Vector3.Lerp(previous.GetColumn(3), old.GetColumn(3), fraction);
                state.Rotation = Quaternion.Slerp(previous.rotation, old.rotation, fraction);
                Vector3 scale = Vector3.Lerp(previous.lossyScale, old.lossyScale, fraction);
                state.Transform = Matrix4x4.TRS(state.Position, state.Rotation, scale);
                state.Scale = scale;
                state.Stopped = true; state.Free = true; state.ReleasedAt = Age; state.Velocity = Vector3.zero; state.Revision++;
                Matrix4x4 correction = state.Transform * old.inverse;
                for (int i = node + 1; i < nodes.Length; i++)
                    if (IsAttachedBelow(i, node))
                    { nodes[i].Transform = correction * nodes[i].Transform; nodes[i].Revision++; }
                break;
            case ConstructionContactResponse.End:
                EndNode(node);
                break;
        }
    }

    /// <summary>在共同时间步预测之前保存前态；此阶段不调用游戏或发布事件。</summary>
    internal void SaveStep()
    {
        Array.Copy(nodes, savedNodes, nodes.Length);
        savedAge = Age; savedAliveNodes = aliveNodes; savedCancelled = cancelled; savedBodyInterrupted = bodyInterrupted;
        savedBodyFrame = bodyFrame; savedInputs = inputs; savedBodyPose = BodyPose; savedOrigin = origin;
    }

    /// <summary>发现更早接触后恢复，再只推进到真实交锋时点。</summary>
    internal void RestoreStep()
    {
        Array.Copy(savedNodes, nodes, nodes.Length);
        Age = savedAge; aliveNodes = savedAliveNodes; cancelled = savedCancelled; bodyInterrupted = savedBodyInterrupted;
        bodyFrame = savedBodyFrame; inputs = savedInputs; BodyPose = savedBodyPose; origin = savedOrigin;
    }

    /// <summary>部位的明确作用用途；实际身体仍由借用方结算正常近战。</summary>
    /// <param name="node">展开部位，负一为身体。</param>
    internal ConstructionContactRole Role(int node) => node < 0 ? ConstructionContactRole.Attack : Plan.Nodes[node].ContactRole;

    /// <summary>相互消耗后仍可继续传递的实际作用量。</summary>
    /// <param name="node">生成部位。</param>
    public float RemainingForce(int node) => nodes[node].Force;

    /// <summary>真实交锋消耗本部位，归零时连同仍附着的子结构结束。</summary>
    /// <param name="node">实际承受消耗的部位。</param>
    /// <param name="amount">实际吸收或抵消量。</param>
    internal void ConsumeForce(int node, float amount)
    {
        ref NodeState state = ref nodes[node];
        float before = state.Force;
        state.Force = Mathf.Max(0f, before - amount);
        // 浮点乘除产生的百万分之一以下余差不成为新的攻击，更不能被最低伤害放大。
        if (state.Force <= Mathf.Max(before, amount) * .000001f) state.Force = 0f;
        if (state.Force == 0f) EndNode(node);
    }

    /// <summary>结束部位及仍依赖其连接的后代，离开的分支保持存在。</summary>
    /// <param name="node">实际结束的部位。</param>
    private void EndNode(int node)
    {
        if (!nodes[node].Exists) return;
        nodes[node].Exists = false; nodes[node].Visible = false; nodes[node].Revision++; aliveNodes--;
        for (int i = node + 1; i < nodes.Length; i++)
            if (IsAttachedBelow(i, node))
            { nodes[i].Exists = false; nodes[i].Visible = false; nodes[i].Revision++; aliveNodes--; }
    }

    /// <summary>已经脱离的分支不再受原父体结束或停止影响。</summary>
    /// <param name="node">候选后代。</param>
    /// <param name="ancestor">发生变化的祖先。</param>
    private bool IsAttachedBelow(int node, int ancestor)
    {
        if (!nodes[node].Exists || nodes[node].Free) return false;
        for (int parent = Plan.Nodes[node].Parent; parent >= 0; parent = Plan.Nodes[parent].Parent)
        {
            if (parent == ancestor) return true;
            if (nodes[parent].Free) return false;
        }
        return false;
    }

    /// <summary>停止后不再推进、作用或显示；来源负责解除实际借用关系。</summary>
    public void Cancel() => cancelled = true;

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
    /// <param name="includeEnd">刚推进至结束时仍检查最后一段路径；取消不会恢复接触。</param>
    public void AppendContacts(List<AbilityCapsule> output, bool includeEnd = false)
    {
        if (cancelled || Finished && !includeEnd) return;
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i]; NodeState state = nodes[i];
            if (!state.Visible || form.ContactRole == ConstructionContactRole.Decoration || Age < form.Active.x || Age > form.Active.y) continue;
            for (int shape = 0; shape < form.Resource.Description.Contacts.Length; shape++)
            {
                FormContactDescription contact = form.Resource.Description.Contacts[shape];
                Matrix4x4 matrix = state.Transform;
                float[] a = contact.A, b = contact.B;
                Vector3 scale = matrix.lossyScale;
                output.Add(new AbilityCapsule(i, matrix.MultiplyPoint3x4(new Vector3(a[0], a[1], a[2])),
                    matrix.MultiplyPoint3x4(new Vector3(b[0], b[1], b[2])),
                    contact.Radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)), form.Matter, shape, state.Revision));
            }
        }
    }

    /// <summary>一个独立形体的必要可变状态。</summary>
    private struct NodeState
    {
        /// <summary>相互消耗后的剩余作用量。</summary>
        internal float Force;
        /// <summary>当前完整变换。</summary>
        internal Matrix4x4 Transform;
        /// <summary>本次数值步之前的姿态，用于确定首次接触的停止位置。</summary>
        internal Matrix4x4 PreviousTransform;
        /// <summary>实际存在、停止及变化版本。</summary>
        internal bool Exists, Stopped;
        /// <summary>接触改变后作废此前路径。</summary>
        internal int Revision;
        /// <summary>本次实际解除连接的时点。</summary>
        internal float ReleasedAt;
        /// <summary>已解除连接、已开始飞行和是否长成可见。</summary>
        internal bool Free, Flying, Visible;
        /// <summary>脱落后的独立位置、尺寸及速度。</summary>
        internal Vector3 Position, Scale, Velocity;
        /// <summary>脱落后的朝向。</summary>
        internal Quaternion Rotation;
    }
}

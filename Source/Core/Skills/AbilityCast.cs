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
    /// <summary>取得形状时已经确定的完整实际材料及物态，身体边界可为空。</summary>
    public readonly AbilityMatter Material;
    /// <summary>从已经确定的姿态记录作用形状。</summary>
    /// <param name="node">展开部位。</param>
    /// <param name="a">轴线起点。</param>
    /// <param name="b">轴线终点。</param>
    /// <param name="radius">实际半径。</param>
    /// <param name="matter">实际材料。</param>
    /// <param name="shape">部位内的形状索引。</param>
    /// <param name="revision">取得形状时的变化版本。</param>
    /// <param name="material">已经解析的实际材料。</param>
    internal AbilityCapsule(int node, Vector3 a, Vector3 b, float radius, string matter, int shape = 0, int revision = 0, AbilityMatter material = null)
    { Node = node; A = a; B = b; Radius = radius; Matter = matter; Shape = shape; Revision = revision; Material = material; }

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
public sealed partial class AbilityCast
{
    /// <summary>开始时确定的共享计划。</summary>
    public CompiledAbility Plan { get; }
    /// <summary>当前世界秒数。</summary>
    public float Age { get; private set; }
    /// <summary>达到总持续后不再发布形体。</summary>
    public bool Finished => cancelled || Age >= Plan.Lifetime || aliveNodes == 0 &&
        (Plan.Body == null || bodyInterrupted || Age >= Plan.Body.Duration);
    /// <summary>仍有自身显示或作用的部位数；空组织支点不拖延已经结束的施放。</summary>
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
    /// <summary>身体被外部改变后作废此前的接触路径。</summary>
    internal int BodyContactRevision { get; private set; }
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
    /// <summary>各生成形体独占的骨架姿态，刚性形体为空。</summary>
    private readonly FormPose[] poses;
    /// <summary>预测回退使用的独立姿态缓冲。</summary>
    private readonly FormPose[] savedPoses;
    /// <summary>预测前的共同时间、姿态、连接及结束状态。</summary>
    private float savedAge;
    /// <summary>预测前存在的部位数。</summary>
    private int savedAliveNodes;
    /// <summary>预测前的身体路径版本。</summary>
    private int savedBodyRevision;
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
        if (plan.NeedsPathTarget && inputs == null) throw new ArgumentException("目标相对路径缺少实际目标输入", nameof(inputs));
        if (float.IsNaN(force) || float.IsInfinity(force) || force < 0f) throw new ArgumentOutOfRangeException(nameof(force));
        bodyFrame = body.GetValueOrDefault();
        this.inputs = inputs.GetValueOrDefault();
        nodes = new NodeState[plan.Nodes.Length];
        savedNodes = new NodeState[nodes.Length];
        poses = new FormPose[nodes.Length]; savedPoses = new FormPose[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            float strength = force * plan.Nodes[i].Strength;
            if (float.IsInfinity(strength)) throw new ArgumentOutOfRangeException(nameof(force), "部位强度超出有限作用量");
            nodes[i] = new NodeState { Exists = true, Force = strength, Matter = plan.Nodes[i].Matter, ShapeHeld = plan.Nodes[i].Matter.HoldsShape };
            poses[i] = plan.Nodes[i].Resource.Rig.CreatePose(); savedPoses[i] = plan.Nodes[i].Resource.Rig.CreatePose();
            if (HasOwnWork(i)) aliveNodes++;
        }
        Sample(0f, Vector3.zero, true);
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
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i]; NodeState state = nodes[i];
            step = PathStep(form, in state, step);
            Include(form.Presence.x); Include(form.Presence.y);
            foreach (CompiledMatterChange change in form.Changes) Include(change.At);
            if (form.Spin.sqrMagnitude > .0001f) step = Mathf.Min(step, 6f / form.Spin.magnitude);
            if (form.TurnRate > 0f) step = Mathf.Min(step, 6f / form.TurnRate);
            if (form.Motion != null && !state.ShapeHeld && !state.Stopped)
            {
                if (form.Motion.MaximumAngularRate > 0f) step = Mathf.Min(step, 6f / (form.Motion.MaximumAngularRate * form.MotionSpeed));
                IncludeShape(form.MotionAt);
                if (state.ShapeAge >= form.MotionAt)
                {
                    float time = (state.ShapeAge - form.MotionAt) * form.MotionSpeed;
                    if (form.Motion.Loop || time < form.Motion.Duration)
                    {
                        if (form.Motion.Loop) time = Mathf.Repeat(time, form.Motion.Duration);
                        foreach (float marker in form.Motion.Times)
                            if (marker > time + .000001f) { step = Mathf.Min(step, (marker - time) / form.MotionSpeed); break; }
                    }
                }
            }
            Include(form.Detach); Include(form.FlyAt);
            if (form.ShapeActiveFrom) IncludeShape(form.Active.x); else Include(form.Active.x);
            if (form.ShapeActiveUntil) IncludeShape(form.Active.y); else Include(form.Active.y);
            Include(form.Growth.x); Include(form.Growth.y);
            void IncludeShape(float marker)
            { if (!state.ShapeHeld && !state.Stopped && marker > state.ShapeAge && marker < state.ShapeAge + step) step = marker - state.ShapeAge; }
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
    /// <param name="bindInitial">仅在受理时解析首次固定连接，重新采样不会挪动固定根。</param>
    private void Sample(float step, Vector3 target, bool bindInitial = false)
    {
        if (Plan.Body != null && !bodyInterrupted) BodyPose = Plan.Body.Sample(Mathf.Min(Age, Plan.Body.Duration), in bodyFrame);
        if (Plan.Binding != null && (bindInitial || Plan.Binding.Follow)) origin = inputs.Resolve(Plan.Binding, BodyPose);
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i]; ref NodeState state = ref nodes[i];
            if (!state.Exists) continue;
            if (Age > form.Presence.y) { EndNode(i); continue; }
            state.PreviousShapeAge = state.ShapeAge;
            if (!state.ShapeHeld && !state.Stopped) state.ShapeAge += step;
            Matrix4x4 previous = state.Transform;
            state.PreviousTransform = previous;
            float growth = form.Growth.x < 0 ? 1f : Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(form.Growth.x, form.Growth.y, Age));
            Quaternion rotation = form.Rotation * Quaternion.Euler(form.Spin * Mathf.Max(0f, Age - form.SpinAt));
            Matrix4x4 parent = form.Parent < 0 ? origin : nodes[form.Parent].Transform;
            Matrix4x4 anchor = form.Anchor;
            if (form.AnchorBlend.HasControls)
            {
                anchor = form.AnchorBlend.Matrix(poses[form.Parent]) * anchor;
                // 表面点和朝向跟随混合变形，附着物本身不继承骨架混合造成的压缩与剪切。
                anchor = Matrix4x4.TRS(anchor.GetColumn(3), Quaternion.LookRotation(anchor.GetColumn(2), anchor.GetColumn(1)), Vector3.one);
            }
            Matrix4x4 attached = parent * anchor *
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
                        state.Flying = true; state.Velocity = (form.InheritVelocity ? state.Velocity : Vector3.zero) + state.Rotation * form.Velocity;
                        if (form.FlightPath != null) StartPath(form, ref state);
                    }
                    Vector3 aim = state.HasTarget ? state.Target : target;
                    if (state.Flying && form.FlightPath != null) MovePath(form, ref state, flightStep, aim);
                    else
                    {
                        if (state.Flying && form.TurnRate > 0f && state.Velocity.sqrMagnitude > 1e-8f && (aim - state.Position).sqrMagnitude > 1e-8f)
                        {
                            Vector3 before = state.Velocity;
                            state.Velocity = Vector3.RotateTowards(before, (aim - state.Position).normalized * before.magnitude,
                                form.TurnRate * Mathf.Deg2Rad * flightStep, 0f);
                            state.Rotation = Quaternion.FromToRotation(before, state.Velocity) * state.Rotation;
                        }
                        state.Position += state.Velocity * flightStep;
                    }
                }
                state.Transform = Matrix4x4.TRS(state.Position, state.Rotation,
                    state.Stopped ? state.Scale : state.Scale * Mathf.Max(.00001f, growth));
            }
            else state.Transform = attached * Matrix4x4.Scale(Vector3.one * Mathf.Max(.00001f, growth));
            state.Visible = (state.Stopped || growth > .0001f) && Age >= form.Presence.x && Age <= form.Presence.y;
            if (poses[i] != null && !state.Stopped && (bindInitial || !state.ShapeHeld))
            {
                state.PreviousMotionAge = state.MotionAge;
                state.MotionAge = (state.ShapeAge - form.MotionAt) * form.MotionSpeed;
                form.Resource.Rig.Sample(state.ShapeAge < form.MotionAt ? null : form.Motion, state.MotionAge, poses[i]);
            }
            ApplyMatterChanges(i, ConstructionChangeTrigger.Time);
        }
    }

    /// <summary>由来源更新真实身体的脚下位置及尺寸，不在显示回调采集或推进。</summary>
    /// <param name="frame">当前逻辑步的实际身体。</param>
    public void UpdateBody(in BodyFrame frame) => bodyFrame = frame;

    /// <summary>每个逻辑步更新连接输入，显示不会重新采集或推进。</summary>
    /// <param name="frame">本步实际来源及目标。</param>
    public void UpdateInputs(in AbilityInputs frame) => inputs = frame;

    /// <summary>外部输入在当前时点发生改变时重新采样，不推进时间，也不扫过瞬移中间路径。</summary>
    /// <param name="target">当前数值目标。</param>
    internal void Refresh(Vector3 target)
    {
        if (Finished) return;
        Sample(0f, target);
        for (int i = 0; i < nodes.Length; i++)
            if (nodes[i].Exists && nodes[i].Transform != nodes[i].PreviousTransform) nodes[i].Revision++;
        if (ControlsBody) BodyContactRevision++;
    }

    /// <summary>中断身体；已经离手的形体继续，仍依赖手部的形体同时结束。</summary>
    public void InterruptBody()
    {
        bodyInterrupted = true;
        if (nodes.Length == 0) Cancel();
        else if (Plan.Binding?.Anchor == AbilityAnchor.Hand && !nodes[0].Free)
        { EndNode(0); if (aliveNodes == 0) Cancel(); }
    }

    /// <summary>作用部位编译后的共用去重组；负一为身体。</summary>
    /// <param name="node">本次生成形状的部位。</param>
    public int ContactGroup(int node) => node < 0 ? Plan.Nodes.Length : Plan.Nodes[node].ContactGroup;

    /// <summary>总次数耗尽后结束同组实际部位，独立的其他组仍继续。</summary>
    /// <param name="group">已经耗尽的接触组。</param>
    internal void EndContactGroup(int group)
    {
        if (group == nodes.Length) InterruptBody();
        for (int i = 0; i < nodes.Length; i++)
            if (Plan.Nodes[i].ContactGroup == group) EndNode(i);
    }

    /// <summary>只接受当前形体版本的候选，连接或运动改变前的路径不继续结算。</summary>
    /// <param name="capsule">此前采集的形状。</param>
    public bool IsCurrent(in AbilityCapsule capsule) => !cancelled && (capsule.Node < 0 ? !bodyInterrupted && capsule.Revision == BodyContactRevision :
        nodes[capsule.Node].Exists && nodes[capsule.Node].Revision == capsule.Revision);

    /// <summary>确认真实接触后改变共同状态，末端位置按本步首次相交进度回到接触处。</summary>
    /// <param name="node">实际发生接触的部位。</param>
    /// <param name="fraction">本步内实际接触进度。</param>
    public void ConfirmContact(int node, float fraction)
    {
        if (node < 0) return;
        ref NodeState state = ref nodes[node];
        if (!state.Exists) return;
        ApplyMatterChanges(node, ConstructionChangeTrigger.ActorContact);
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
                state.ShapeAge = Mathf.Lerp(state.PreviousShapeAge, state.ShapeAge, fraction);
                if (poses[node] != null && fraction < 1f)
                {
                    state.MotionAge = Mathf.Lerp(state.PreviousMotionAge, state.MotionAge, fraction);
                    Plan.Nodes[node].Resource.Rig.Sample(state.MotionAge < 0f ? null : Plan.Nodes[node].Motion, state.MotionAge, poses[node]);
                }
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
        for (int i = 0; i < poses.Length; i++) poses[i]?.CopyTo(savedPoses[i]);
        savedAge = Age; savedAliveNodes = aliveNodes; savedCancelled = cancelled; savedBodyInterrupted = bodyInterrupted;
        savedBodyRevision = BodyContactRevision;
        savedBodyFrame = bodyFrame; savedInputs = inputs; savedBodyPose = BodyPose; savedOrigin = origin;
    }

    /// <summary>发现更早接触后恢复，再只推进到真实交锋时点。</summary>
    internal void RestoreStep()
    {
        Array.Copy(savedNodes, nodes, nodes.Length);
        for (int i = 0; i < poses.Length; i++) savedPoses[i]?.CopyTo(poses[i]);
        Age = savedAge; aliveNodes = savedAliveNodes; cancelled = savedCancelled; bodyInterrupted = savedBodyInterrupted;
        BodyContactRevision = savedBodyRevision;
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

    /// <summary>空装饰支点只组织后代，其后代全部结束后没有独立显示或作用要继续。</summary>
    /// <param name="node">实际编译部位。</param>
    private bool HasOwnWork(int node) => Plan.Nodes[node].Resource.Meshes.Length != 0 || Plan.Nodes[node].ContactRole != ConstructionContactRole.Decoration;

    /// <summary>结束部位及仍依赖其连接的后代，离开的分支保持存在。</summary>
    /// <param name="node">实际结束的部位。</param>
    internal void EndNode(int node)
    {
        if (!nodes[node].Exists) return;
        nodes[node].Exists = false; nodes[node].Visible = false; nodes[node].Revision++; if (HasOwnWork(node)) aliveNodes--;
        for (int i = node + 1; i < nodes.Length; i++)
            if (IsAttachedBelow(i, node))
            { nodes[i].Exists = false; nodes[i].Visible = false; nodes[i].Revision++; if (HasOwnWork(i)) aliveNodes--; }
    }

    /// <summary>已经脱离的分支不再受原父体结束或停止影响。</summary>
    /// <param name="node">候选后代。</param>
    /// <param name="ancestor">发生变化的祖先。</param>
    internal bool IsAttachedBelow(int node, int ancestor)
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

    /// <summary>本部位已经发生的反射数；身体不参与形体反射。</summary>
    /// <param name="node">生成部位或负一身体。</param>
    public int Reflections(int node) => node < 0 ? 0 : nodes[node].Reflections;

    /// <summary>读取独立形体当前的世界速度。</summary>
    /// <param name="node">生成部位。</param>
    public Vector3 Velocity(int node) => nodes[node].Velocity;

    /// <summary>必须是仍存在、独立运动且尚有次数的部位；附着的身体或器物不会被凭空抛走。</summary>
    /// <param name="node">生成部位。</param>
    internal bool CanReflect(int node) => nodes[node].Exists && nodes[node].Free && !nodes[node].Stopped &&
        nodes[node].Velocity.sqrMagnitude > 1e-8f && nodes[node].Reflections < Plan.Nodes[node].ReflectionLimit;

    /// <summary>反射后继续沿普通飞行操作追踪各自目标，输入仍由主线程采集。</summary>
    /// <param name="node">实际部位。</param>
    /// <param name="target">本轮数值目标。</param>
    internal void SetTarget(int node, Vector3 target)
    { nodes[node].HasTarget = true; nodes[node].Target = target; }

    /// <summary>在真实接触处按表面相对速度反射，保留当时形状及姿态，作废旧路径。</summary>
    /// <param name="node">独立运动部位。</param>
    /// <param name="normal">屏障向外的单位方向。</param>
    /// <param name="surfaceVelocity">接触表面的实际速度。</param>
    /// <param name="target">上一控制者的数值位置。</param>
    internal void Reflect(int node, Vector3 normal, Vector3 surfaceVelocity, Vector3 target)
    {
        ref NodeState state = ref nodes[node];
        state.Velocity = surfaceVelocity + Vector3.Reflect(state.Velocity - surfaceVelocity, normal);
        if (state.Flying && Plan.Nodes[node].FlightPath != null)
        {
            state.PathVelocity = surfaceVelocity + Vector3.Reflect(state.PathVelocity - surfaceVelocity, normal);
            state.PathX = Vector3.Reflect(state.PathX, normal); state.PathY = Vector3.Reflect(state.PathY, normal); state.PathZ = Vector3.Reflect(state.PathZ, normal);
            Plan.Nodes[node].FlightPath.Sample(Mathf.Max(0f, Age - Plan.Nodes[node].FlyAt), out Vector3 offset, out _);
            state.PathCentre = state.Position - PathVector(in state, offset);
        }
        state.Reflections++; state.Revision++; SetTarget(node, target);
        for (int i = node + 1; i < nodes.Length; i++)
            if (IsAttachedBelow(i, node)) { nodes[i].Reflections++; nodes[i].Revision++; SetTarget(i, target); }
    }

    /// <summary>发布所有仍存在的形体，绘制方只读同一变换。</summary>
    /// <param name="output">本轮显示集合。</param>
    public void AppendVisuals(List<FormVisualInstance> output)
    {
        if (Finished) return;
        for (int i = 0; i < nodes.Length; i++)
            if (nodes[i].Visible && Age < Plan.Nodes[i].Presence.y && Plan.Nodes[i].Resource.Meshes.Length > 0) output.Add(new FormVisualInstance(Plan.Nodes[i].Resource, nodes[i].Transform,
                surface: Plan.Nodes[i].Surface, skin: poses[i] == null ? null : (Matrix4x4[])poses[i].Skin.Clone(), poseBounds: poses[i]?.Bounds,
                matterSurface: nodes[i].Matter.Surface, dye: Plan.Nodes[i].Dye, matter: nodes[i].Matter));
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
            float fromAge = form.ShapeActiveFrom ? state.ShapeAge : Age, untilAge = form.ShapeActiveUntil ? state.ShapeAge : Age;
            bool ended = form.ShapeActiveUntil ? untilAge >= form.Active.y &&
                !(includeEnd && state.PreviousShapeAge < form.Active.y && untilAge == form.Active.y) : untilAge > form.Active.y;
            if (!state.Visible || !includeEnd && Age >= form.Presence.y || form.ContactRole == ConstructionContactRole.Decoration || fromAge < form.Active.x || ended) continue;
            for (int shape = 0; shape < form.Resource.Contacts.Length; shape++)
            {
                FormContactBinding contact = form.Resource.Contacts[shape];
                Matrix4x4 matrix = state.Transform;
                Vector3 a = contact.ABlend.Point(contact.A, poses[i]);
                Vector3 b = contact.BBlend.Point(contact.B, poses[i]);
                Vector3 scale = matrix.lossyScale;
                output.Add(new AbilityCapsule(i, matrix.MultiplyPoint3x4(a), matrix.MultiplyPoint3x4(b),
                    contact.Radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)), state.Matter.Id, shape, state.Revision, state.Matter));
            }
        }
    }

    /// <summary>一个独立形体的必要可变状态。</summary>
    private struct NodeState
    {
        /// <summary>本次部位的实际材料及物态。</summary>
        internal AbilityMatter Matter;
        /// <summary>内部形变是否保持当前进度。</summary>
        internal bool ShapeHeld;
        /// <summary>本次形变进度及上一数值步进度。</summary>
        internal float ShapeAge, PreviousShapeAge;
        /// <summary>已经执行的有限物态变化。</summary>
        internal ushort MatterChangesDone;
        /// <summary>共同动作当前与上一步的内部时间，停止时冻结。</summary>
        internal float MotionAge, PreviousMotionAge;
        /// <summary>实际反射次数。</summary>
        internal int Reflections;
        /// <summary>本部位是否拥有反射后独立目标。</summary>
        internal bool HasTarget;
        /// <summary>由主线程更新的反射目标位置。</summary>
        internal Vector3 Target;
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
        /// <summary>曲线中心速度及释放时确定的三个实际参照方向。</summary>
        internal Vector3 PathCentre, PathVelocity, PathX, PathY, PathZ;
        /// <summary>脱落后的朝向。</summary>
        internal Quaternion Rotation;
    }
}

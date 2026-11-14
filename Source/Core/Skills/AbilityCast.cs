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
    Interrupted,

    /// <summary>已经出手，尚未进入实际作用窗口；追加数值以保留既有阶段编号。</summary>
    Releasing
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

    /// <summary>薄层轴对齐箱体的半宽；零值保持胶囊。</summary>
    public readonly Vector3 HalfExtents;

    /// <summary>本次扫掠中最后仍有真实材料的时间比例，普通形状为一。</summary>
    public readonly float Until;

    /// <summary>是否使用有限厚度的箱体。</summary>
    public bool IsBox => HalfExtents.sqrMagnitude > 0f;

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
    /// <param name="halfExtents">薄层箱体半宽，默认仍为胶囊。</param>
    /// <param name="until">此步内材料保持有效的末端。</param>
    internal AbilityCapsule(int node, Vector3 a, Vector3 b, float radius, string matter, int shape = 0,
        int revision = 0, AbilityMatter material = null, Vector3 halfExtents = default, float until = 1f)
    {
        Node = node;
        A = a;
        B = b;
        Radius = radius;
        Matter = matter;
        Shape = shape;
        Revision = revision;
        Material = material;
        HalfExtents = halfExtents;
        Until = until;
    }

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
        if (IsBox) return Vector3.Max(A - HalfExtents, Vector3.Min(A + HalfExtents, point));
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
    public bool SweepFrom(in AbilityCapsule previous, Vector3 centre, float radius, out Vector3 point,
        out float fraction)
    {
        if (IsBox || previous.IsBox)
        {
            var sphere = new AbilityCapsule(0, centre, centre, radius, null);
            return AbilityPairSweep.Find(previous, this, sphere, sphere, out fraction, out point, out _);
        }

        Vector3 va = A - previous.A, vb = B - previous.B;
        float speed = Mathf.Max(va.magnitude, vb.magnitude) + Mathf.Abs(Radius - previous.Radius), at = 0f;
        point = A;
        fraction = 1f;
        // 距离每次最多按两端最大速度缩小，前进到安全边界，不固定抽取几个时点猜命中。
        for (int iteration = 0; iteration < 64; iteration++)
        {
            Vector3 a = previous.A + va * at, b = previous.B + vb * at, axis = b - a;
            float along = axis.sqrMagnitude > 1e-10f
                ? Mathf.Clamp01(Vector3.Dot(centre - a, axis) / axis.sqrMagnitude)
                : 0f;
            point = a + axis * along;
            float distance = Vector3.Distance(centre, point) - Mathf.Lerp(previous.Radius, Radius, at) - radius;
            if (distance <= .00001f)
            {
                fraction = at;
                return true;
            }

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

    /// <summary>独立形体按原寿命结束；身体实际落地及收势完成后结束整次施放。</summary>
    public bool Finished => cancelled || Age >= CastUntil || aliveNodes == 0 &&
        (Plan.Body == null || bodyInterrupted || Age >= BodyUntil);

    /// <summary>仍有自身显示或作用的部位数；空组织支点不拖延已经结束的施放。</summary>
    private int aliveNodes;

    /// <summary>是否已经明确中断，禁止后续显示或接触恢复运行。</summary>
    private bool cancelled;

    /// <summary>读取同一时钟下的施放阶段。</summary>
    public AbilityCastPhase Phase => cancelled ? AbilityCastPhase.Interrupted :
        Finished ? AbilityCastPhase.Finished : ResolveActionPhase();

    /// <summary>同一动作时钟下的真实身体姿态，非身体能力没有此状态。</summary>
    public BodyPose? BodyPose { get; private set; }

    /// <summary>身体尚在动作中；其结束不强制回收已离手形体。</summary>
    public bool ControlsBody => Plan.Body != null && !bodyInterrupted && !Finished && Age < BodyUntil;

    /// <summary>身体中断后不再次取得控制。</summary>
    private bool bodyInterrupted;

    /// <summary>首个真实身体接触时点，落空不启动回弹。</summary>
    private float bodyContactAt = -1f;

    /// <summary>预测前的真实身体接触时点。</summary>
    private float savedBodyContactAt;

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
    /// <param name="terrain">本轮已冻结的绝对高度地表；空值为原版零平面。</param>
    /// <param name="paidResource">来源已实际支付的资源，未支付的数值探针默认不能回能。</param>
    public AbilityCast(CompiledAbility plan, Matrix4x4 origin, BodyFrame? body = null, AbilityInputs? inputs = null,
        float force = 1f, AbilityHeightField terrain = null, double paidResource = 0)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        this.origin = origin;
        if (plan.Body != null && body == null) throw new ArgumentException("Body：缺少施法者的实际身体姿态与尺寸", nameof(body));
        if (plan.Binding != null && inputs == null) throw new ArgumentException("Binding：缺少本次实际施放输入", nameof(inputs));
        if (plan.NeedsPathTarget && inputs == null)
            throw new ArgumentException("Path/TargetLine：缺少本次实际目标位置输入", nameof(inputs));
        if (plan.Binding?.Anchor == AbilityAnchor.Carrier && inputs?.Carrier == null)
            throw new ArgumentException("inputs/Carrier：外部连接需要本次实际取得的载体姿态", nameof(inputs));
        if (inputs.HasValue)
        {
            AbilityInputs value = inputs.Value;
            if ((plan.NeedsPathTarget || plan.Binding?.Anchor == AbilityAnchor.Target) && !FiniteInput(value.Target))
                throw new ArgumentException("inputs/Target：目标位置需要三个有限地图坐标", nameof(inputs));
            if (plan.Binding?.Anchor == AbilityAnchor.Source && !FiniteInput(value.Source))
                throw new ArgumentException("inputs/Source：来源位置需要三个有限地图坐标", nameof(inputs));
            if (plan.Binding is { FaceTarget: true } &&
                plan.Binding.Anchor is not (AbilityAnchor.Hand or AbilityAnchor.Effector) &&
                (!FiniteInput(value.Forward) || value.Forward.sqrMagnitude < 1e-8f))
                throw new ArgumentException("inputs/Forward：朝向连接需要非零且有限的实际出手方向", nameof(inputs));
            if (plan.Binding?.ScaleByBody == true && (float.IsNaN(value.SourceHeight) ||
                                                      float.IsInfinity(value.SourceHeight) || value.SourceHeight <= 0f))
                throw new ArgumentException("inputs/SourceHeight：按体高缩放需要正数的实际体高", nameof(inputs));
        }

        if (float.IsNaN(force) || float.IsInfinity(force) || force < 0f)
            throw new ArgumentOutOfRangeException(nameof(force));
        this.terrain = terrain;
        bool startsTogether = plan.EventListeners.Length == 0 &&
                              Array.TrueForAll(plan.Nodes, form => form.AnchorBuild == 0f);
        bodyFrame = body.GetValueOrDefault();
        this.inputs = inputs.GetValueOrDefault();
        nodes = new NodeState[plan.Nodes.Length];
        savedNodes = new NodeState[nodes.Length];
        if (Array.Exists(plan.Nodes, form => form.FlowSettings != null))
        {
            flows = new AbilityFlow[nodes.Length];
            savedFlows = new AbilityFlow[nodes.Length];
            for (int i = 0; i < flows.Length; i++)
                if (plan.Nodes[i].FlowSettings != null)
                {
                    flows[i] = new AbilityFlow(plan.Nodes[i].FlowSettings);
                    savedFlows[i] = new AbilityFlow(plan.Nodes[i].FlowSettings);
                }
        }

        if (Array.Exists(plan.Nodes, form => form.Fracture != null))
        {
            fractures = new FractureState[nodes.Length];
            savedFractures = new FractureState[nodes.Length];
            fragmentMotion = new FragmentMotion[nodes.Length];
            savedFragmentMotion = new FragmentMotion[nodes.Length];
        }

        if (Array.Exists(plan.Nodes,
                form => form.Gather != null ||
                        Array.Exists(form.EventOperations, operation => operation.Gather != null)))
        {
            gathers = new GatherState[nodes.Length];
            savedGathers = new GatherState[nodes.Length];
        }

        operations = new OperationState[plan.EventOperations.Length];
        savedOperations = new OperationState[operations.Length];
        stepStates = plan.Steps.Length == 0 ? Array.Empty<StepState>() : new StepState[plan.Steps.Length];
        savedStepStates = plan.Steps.Length == 0 ? Array.Empty<StepState>() : new StepState[plan.Steps.Length];
        bool stepFracture = Array.Exists(plan.Steps, step => step.Action.Fracture != null);
        stepParts = stepFracture ? new ConstructionOutputValue[nodes.Length] : Array.Empty<ConstructionOutputValue>();
        savedStepParts =
            stepFracture ? new ConstructionOutputValue[nodes.Length] : Array.Empty<ConstructionOutputValue>();
        conditionEvents = plan.EventListeners.Length == 0
            ? null
            : new Queue<ConditionEvent>(plan.EventOperations.Length + 1);
        birthQueue = startsTogether ? Array.Empty<int>() : new int[nodes.Length];
        poses = new FormPose[nodes.Length];
        savedPoses = new FormPose[nodes.Length];
        transitionPoses = new FormPose[nodes.Length];
        savedTransitionPoses = new FormPose[nodes.Length];
        groundPoses = Array.Exists(plan.Nodes, form => form.HasGround) ? new FormPose[nodes.Length] : null;
        if (Array.Exists(plan.Nodes, form => form.HasGround && form.Resource.Description.ConformToGround))
        {
            groundBase = new FormPose[nodes.Length];
            savedGroundBase = new FormPose[nodes.Length];
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            float strength = plan.Nodes[i].RemainderRoot >= 0 ? 0f : force * plan.Nodes[i].Strength;
            if (float.IsInfinity(strength)) throw new ArgumentOutOfRangeException(nameof(force), "部位强度超出有限作用量");
            nodes[i] = new NodeState
            {
                Exists = true,
                MaterialRetained = 1f,
                Authority = i,
                Force = strength,
                InitialForce = strength,
                Volume = plan.Nodes[i].RemainderRoot >= 0 ? 0 : plan.Nodes[i].Volume * Math.Pow(plan.Nodes[i].Scale, 3),
                Matter = plan.Nodes[i].Matter,
                ShapeHeld = plan.Nodes[i].Matter.HoldsShape,
                Started = plan.Nodes[i].FragmentSource < 0 && startsTogether,
                BirthAt = plan.Nodes[i].FragmentSource < 0 && plan.Nodes[i].Activation == null
                    ? 0f
                    : float.PositiveInfinity,
                Spin = plan.Nodes[i].Spin,
                SpinAt = plan.Nodes[i].SpinAt,
                SpinOffset = Quaternion.identity,
                GrowthValue = 1f
            };
            if (gathers != null)
                gathers[i] = new GatherState { Offset = plan.Nodes[i].Offset, Rotation = plan.Nodes[i].Rotation };
            poses[i] = plan.Nodes[i].Resource.Rig.CreatePose();
            savedPoses[i] = plan.Nodes[i].Resource.Rig.CreatePose();
            if (plan.Nodes[i].HasGround) groundPoses[i] = plan.Nodes[i].Resource.Rig.CreatePose();
            if (plan.Nodes[i].HasGround && plan.Nodes[i].Resource.Description.ConformToGround)
            {
                groundBase[i] = plan.Nodes[i].Resource.Rig.CreatePose();
                savedGroundBase[i] = plan.Nodes[i].Resource.Rig.CreatePose();
            }

            if (plan.Nodes[i].HasMotionChanges)
            {
                transitionPoses[i] = plan.Nodes[i].Resource.Rig.CreatePose();
                savedTransitionPoses[i] = plan.Nodes[i].Resource.Rig.CreatePose();
            }

            if (HasOwnWork(i)) aliveNodes++;
        }

        AllocateResource(paidResource);
        Sample(0f, Vector3.zero, true);
    }

    /// <summary>只在公开施放输入边界检查实际需要的三维坐标，不扫描逐帧内部状态。</summary>
    /// <param name="value">本次实际需要的位置或方向。</param>
    private static bool FiniteInput(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
        !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

    /// <summary>按世界时间推进；显示、挂点与接触形状之后均读取此状态。</summary>
    /// <param name="seconds">逻辑步长，零不推进。</param>
    /// <param name="target">本次目标的当前地图空间位置。</param>
    public void Advance(float seconds, Vector3 target)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (Finished || seconds == 0f) return;
        float until = Mathf.Min(Age + seconds, CastUntil);
        // 在操作时点拆步，脱落和开始飞行不会早执行一整帧。
        while (Age < until && !Finished)
        {
            float step = NextStep(until - Age);
            Age += step;
            Sample(step, target);
            bool burned = AdvanceBurning(step);
            if (burned) Sample(0, target);
            for (int node = 0; node < nodes.Length; node++)
                nodes[node].ShrinkVelocity = burned && !nodes[node].Free && step >= .00001f
                    ? ((Vector3)nodes[node].Transform.GetColumn(3) -
                       (Vector3)nodes[node].PreviousTransform.GetColumn(3)) / step
                    : Vector3.zero;
        }
    }

    /// <summary>动作及作用时点统一拆步，外部结算与内部姿态不使用两套时间边界。</summary>
    /// <param name="maximum">本步尚可推进的时间。</param>
    public float NextStep(float maximum)
    {
        float step = Mathf.Min(maximum, 1f / 60f, Mathf.Max(0f, CastUntil - Age));
        Include(Plan.Lifetime);
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i];
            NodeState state = nodes[i];
            if (!state.Exists) continue;
            if (state.Burning.Lit && state.Burning.Fuel > 0)
                step = Mathf.Min(step,
                    Mathf.Max(Mathf.Max(.00001f, Age * .000001f),
                        (float)(state.Volume * AbilityBurning.Seconds / state.Burning.Fuel)));
            if (!state.Started)
            {
                Include(state.BirthAt);
                continue;
            }

            void IncludeNode(float marker) => Include(state.StartedAt + marker);
            step = PathStep(form, in state, step);
            if (gathers != null && !state.Free)
            {
                GatherState gather = gathers[i];
                if (!gather.InitialStarted && form.Gather != null) IncludeNode(form.Gather.At);
                if (gather.Process != null && Age < gather.StartedAt + gather.Process.Seconds)
                {
                    Include(gather.StartedAt + gather.Process.Seconds);
                    if (gather.AngularRate > 0f) step = Mathf.Min(step, 6f / gather.AngularRate);
                }
            }

            IncludeNode(form.Presence.x);
            IncludeNode(form.Presence.y);
            IncludeNode(form.FractureAt);
            foreach (CompiledMatterChange change in form.Changes) IncludeNode(change.At);
            if (!state.ShapeHeld && (!state.MotionStopped || !state.GrowthStopped) &&
                state.HoldUsed < form.Hold.Seconds)
            {
                if (state.ShapeAge < form.Hold.At) IncludeShape(form.Hold.At);
                else step = Mathf.Min(step, form.Hold.Seconds - state.HoldUsed);
            }

            if (state.Spin.sqrMagnitude > .0001f) step = Mathf.Min(step, 6f / state.Spin.magnitude);
            if (state.Flying && !state.MovementStopped && state.Flight.TurnRate > 0f)
                step = Mathf.Min(step, 6f / state.Flight.TurnRate);
            if (state.Flying && !state.MovementStopped && state.Acceleration.sqrMagnitude > 0f)
                step = Mathf.Min(step, Mathf.Sqrt(.02f / state.Acceleration.magnitude));
            CompiledRange timers = Plan.Program.NodeTimers[i];
            for (int t = timers.Start; t < timers.End; t++)
            {
                var command = Plan.Program.Commands[Plan.Program.EventCommands[Plan.Program.Timers[t]]];
                if (command.Step < 0
                        ? !operations[command.State].Done
                        : stepStates[command.Step].State == ConstructionStepState.Waiting) IncludeNode(command.At);
            }

            if (state.PendingOperations != 0)
            {
                CompiledRange pending = Plan.Program.NodeEvents[i];
                for (int index = pending.Start; index < pending.End; index++)
                    if ((state.PendingOperations & (1 << (index - pending.Start))) != 0)
                        Include(operations[index].Due);
            }

            step = GroundStep(i, step);
            step = SizeThresholdStep(i, step);
            FormMotion motion = state.MotionData?.Motion ?? form.Motion;
            float motionSpeed = state.MotionData?.Speed ?? form.MotionSpeed;
            bool shapeAdvancing = !state.ShapeHeld &&
                                  !(state.HoldUsed < form.Hold.Seconds && state.ShapeAge >= form.Hold.At);
            bool motionHeld = state.Motion != null && state.MotionHoldUsed < state.MotionData.Hold.Seconds &&
                              state.MotionAge / motionSpeed >= state.MotionData.Hold.At;
            if (motion != null && shapeAdvancing && !state.MotionStopped && state.Motion != null &&
                state.MotionHoldUsed < state.MotionData.Hold.Seconds)
            {
                if (motionHeld) step = Mathf.Min(step, state.MotionData.Hold.Seconds - state.MotionHoldUsed);
                else Include(Age + state.MotionData.Hold.At - state.MotionAge / motionSpeed);
            }

            if (motion != null && shapeAdvancing && !state.MotionStopped && !motionHeld)
            {
                float rate = motion.MaximumAngularRate * motionSpeed +
                             (state.Motion != null && state.BlendAge < state.MotionData.Blend
                                 ? state.BlendAngularRate
                                 : 0f);
                if (rate > 0f) step = Mathf.Min(step, 6f / rate);
                if (state.Motion == null) IncludeShape(form.MotionAt);
                float time = state.Motion == null ? (state.ShapeAge - form.MotionAt) * motionSpeed : state.MotionAge;
                if (time >= 0 && (motion.Loop || time < motion.Duration))
                {
                    if (motion.Loop) time = Mathf.Repeat(time, motion.Duration);
                    foreach (float marker in motion.Times)
                        if (marker > time + .000001f)
                        {
                            step = Mathf.Min(step, (marker - time) / motionSpeed);
                            break;
                        }
                }

                if (state.Motion != null)
                {
                    if (state.BlendAge < state.MotionData.Blend)
                        Include(Age + (state.MotionData.Blend - state.BlendAge));
                    if (form.ShapeActiveFrom) IncludeMotion(state.MotionData.From);
                    if (form.ShapeActiveUntil) IncludeMotion(state.MotionData.Until);
                }

                void IncludeMotion(float marker)
                {
                    Include(Age + (marker - state.MotionAge) / motionSpeed);
                }

                if (state.NextActionMarker < motion.StageMarkers.Length)
                    IncludeMotion(motion.StageMarkers[state.NextActionMarker].Value);
            }

            if (state.GrowthChanged && !state.GrowthStopped && shapeAdvancing && state.GrowthAge < state.GrowthSeconds)
                Include(Age + (state.GrowthSeconds - state.GrowthAge));
            IncludeNode(form.Detach);
            IncludeNode(form.FlyAt);
            IncludeNode(form.FlowAt);
            IncludeNode(form.RecoveryAt);
            if (form.Conduction != null)
            {
                if (!state.Conduction.Started) IncludeNode(form.ConductionAt);
                else Include(state.Conduction.NextAt(form.Conduction));
            }

            CompiledRange conductors = Plan.Program.NodeConduction[i];
            for (int c = conductors.Start; c < conductors.End; c++)
            {
                int index = Plan.Program.Conduction[c];
                var command = Plan.Program.Commands[Plan.Program.EventCommands[index]];
                Include(operations[index].Conduction.NextAt(Plan.Program.Parameter<CompiledConduction>(command)));
            }

            IncludeNode(form.LeaveAt);
            if (state.Recovering) Include(state.RecoveryAt + state.Recovery.Seconds);
            if (form.ShapeActiveFrom && state.Motion == null) IncludeShape(form.Active.x);
            else IncludeNode(form.Active.x);
            if (form.ShapeActiveUntil && state.Motion == null) IncludeShape(form.Active.y);
            else IncludeNode(form.Active.y);
            if (!state.GrowthChanged)
            {
                IncludeNode(form.Growth.x);
                IncludeNode(form.Growth.y);
            }

            void IncludeShape(float marker)
            {
                if (!state.ShapeHeld && (!state.MotionStopped || !state.GrowthStopped) &&
                    !(state.HoldUsed < form.Hold.Seconds && state.ShapeAge >= form.Hold.At) &&
                    marker > state.ShapeAge && marker < state.ShapeAge + step) step = marker - state.ShapeAge;
            }
        }

        if (Plan.Body != null)
        {
            Include(Plan.Body.NextStageBoundary(Age));
            Include(Plan.Body.ReleaseAt);
            Include(Plan.Body.ActiveFrom);
            Include(Plan.Body.ActiveUntil);
            Include(Plan.Body.Duration);
            Include(BodyUntil);
            if (bodyResponse.HasValue) Include(bodyResponse.Value.At + bodyResponseDelay + bodyResponse.Value.Seconds);
            Include(Plan.Body.HoldAt);
            Include(Plan.Body.HoldUntil);
            step = Mathf.Min(step, Plan.Body.FootworkStep(Age));
            for (int i = 0; i < Plan.Body.RelationMarkers.Count; i++) Include(Plan.Body.RelationMarkers[i]);
            if (Plan.Body.MaximumAngularRate > 0f && !(Age >= Plan.Body.HoldAt && Age < Plan.Body.HoldUntil))
                step = Mathf.Min(step, 6f / Plan.Body.MaximumAngularRate);
        }

        return step;

        void Include(float marker)
        {
            if (marker > Age && marker < Age + step) step = marker - Age;
        }
    }

    /// <summary>父体先推进，子体依赖同一时刻的父姿态；脱落只发生一次。</summary>
    /// <param name="step">当前世界步长。</param>
    /// <param name="target">当前目标位置。</param>
    /// <param name="bindInitial">仅在受理时解析首次固定连接，重新采样不会挪动固定根。</param>
    private void Sample(float step, Vector3 target, bool bindInitial = false)
    {
        sampleEpoch++;
        sampling = true;
        samplingStep = step;
        samplingTarget = target;
        UpdateBodyStage();
        if (Plan.Body != null && !bodyInterrupted)
            BodyPose = bodyResponse.HasValue
                ? Plan.Body.SampleResponse(Age - bodyResponseDelay, in bodyFrame, bodyResponse.Value, BodyPose, Age)
                : Plan.Body.Sample(Mathf.Min(Age, Plan.Body.Duration), in bodyFrame, bodyContactAt, BodyPose);
        if (Plan.Binding != null && (bindInitial || Plan.Binding.Follow))
            origin = inputs.Resolve(Plan.Binding, BodyPose);
        for (int i = 0; i < nodes.Length; i++) SampleNode(i, step, target, bindInitial);
        if (Plan.Body != null && !bodyInterrupted && !bodyEndSent && Age >= BodyUntil)
        {
            bodyEndSent = true;
            SignalEvent(-1, ConstructionChangeTrigger.End, BodyPose.Value.Hand);
            EndBodyWindows();
        }

        sampling = false;
        FlushBirths(target);
        UpdateSteps();
        RecordActionTimeline();
    }

    /// <summary>按本分支时钟推进一个部位，保持共同姿态、显示和作用区。</summary>
    /// <param name="i">展开部位。</param>
    /// <param name="step">共同世界步长。</param>
    /// <param name="target">实际目标数值。</param>
    /// <param name="bindInitial">是否首次绑定。</param>
    private void SampleNode(int i, float step, Vector3 target, bool bindInitial)
    {
        CompiledForm form = Plan.Nodes[i];
        ref NodeState state = ref nodes[i];
        if (!state.Exists) return;
        if (Age >= Plan.Lifetime) { EndNode(i); return; }
        bool began = !state.Started;
        if (began)
        {
            if (!CanStart(i)) return;
            StartNode(i);
        }
        else if (state.SampleEpoch == sampleEpoch || state.SampleEpoch == -sampleEpoch) return;

        state.SampleEpoch = -sampleEpoch;
        if (state.Leaving)
        {
            state.SampleEpoch = sampleEpoch;
            return;
        }

        if (state.Recovering)
        {
            AdvanceRecovery(i);
            return;
        }

        if (!state.Free && form.Parent >= 0 && nodes[form.Parent].SampleEpoch != sampleEpoch)
            SampleNode(form.Parent, step, target, bindInitial);
        float age = Age - state.StartedAt;
        if (began)
        {
            step = Mathf.Min(step, age);
            state.SpinAt = state.StartedAt + form.SpinAt;
        }

        state.LastStep = step;
        state.PreviousMotionAge = state.MotionAge;
        state.PreviousBlendAge = state.BlendAge;
        if (form.HasGround && groundPoses[i] != null) poses[i].CopyTo(groundPoses[i]);
        RestoreGroundBase(i);
        if (age > form.Presence.y)
        {
            EndNode(i);
            return;
        }

        state.PreviousShapeAge = state.ShapeAge;
        if (!state.ShapeHeld && (!state.MotionStopped || !state.GrowthStopped))
            state.ShapeAge = form.Hold.Advance(state.ShapeAge, ref state.HoldUsed, step);
        float shapeDelta = state.ShapeAge - state.PreviousShapeAge;
        if (fractures != null) CaptureFragmentMotion(i);
        Matrix4x4 previous = state.Transform;
        state.PreviousTransform = previous;
        float growth = AdvanceGrowth(i, age, shapeDelta);
        if (gathers != null) SampleGather(i, age);
        Vector3 localOffset = gathers == null ? form.Offset : gathers[i].Offset;
        Quaternion localRotation = gathers == null ? form.Rotation : gathers[i].Rotation;
        Quaternion rotation = localRotation * state.SpinOffset *
                              Quaternion.Euler(state.Spin * Mathf.Max(0f, Age - state.SpinAt));
        Matrix4x4 parent = state.EventPlaced ? Matrix4x4.TRS(state.EventPosition, state.EventRotation, Vector3.one) :
            form.Parent < 0 ? origin : nodes[form.Parent].Transform;
        Matrix4x4 anchor = state.EventPlaced ? Matrix4x4.identity : form.Anchor;
        if (form.AnchorBlend.HasControls && !state.EventPlaced)
        {
            anchor = form.AnchorBlend.Matrix(poses[form.Parent]) * anchor;
            // 表面点和朝向跟随混合变形，附着物本身不继承骨架混合造成的压缩与剪切。
            anchor = Matrix4x4.TRS(anchor.GetColumn(3),
                Quaternion.LookRotation(anchor.GetColumn(2), anchor.GetColumn(1)), Vector3.one);
        }

        Matrix4x4 attached = parent * anchor *
                             Matrix4x4.TRS(localOffset, rotation, Vector3.one * form.Scale);
        // 材料烧损只收缩本部位，子部位仍跟随真实挂点，但不凭空减少自己的材料和尺寸。
        if (!state.Free && form.Parent >= 0 && nodes[form.Parent].MaterialRetained > 0 &&
            nodes[form.Parent].MaterialRetained < 1)
            attached *= Matrix4x4.Scale(Vector3.one / Mathf.Pow(nodes[form.Parent].MaterialRetained, 1f / 3f));
        if ((bindInitial || began) && form.RemainderRoot < 0 && form.FragmentSource < 0)
            state.Volume = form.Volume * System.Math.Abs(attached.determinant);
        if (began)
        {
            previous = attached;
            state.PreviousTransform = attached;
            if (state.EventPlaced)
            {
                state.Position = attached.GetColumn(3);
                state.Rotation = attached.rotation;
                state.Scale = attached.lossyScale;
                state.ReleasedAt = Age;
            }
        }

        // 极短的边界余步不足以稳定求差，保留最近连续步的点速度。
        if (!state.Free && step >= .00001f)
            state.AttachedVelocity = ((Vector3)attached.GetColumn(3) - (Vector3)previous.GetColumn(3)) / step +
                                     state.ShrinkVelocity;
        if (!state.Free && form.ReleaseAt >= 0f && age >= form.ReleaseAt)
        {
            state.Free = true;
            state.Position = attached.GetColumn(3);
            state.Rotation = attached.rotation;
            state.ReleasedAt = state.StartedAt + form.ReleaseAt;
            state.Scale = attached.lossyScale;
            state.Velocity = state.AttachedVelocity;
            state.ScheduledDetachDone = form.Detach >= 0 && age >= form.Detach;
        }

        if (state.Free)
        {
            float freeStep = Mathf.Min(step, Mathf.Max(0f, Age - state.ReleasedAt));
            float flightStep = freeStep;
            state.Rotation *= Quaternion.Euler(state.Spin * Mathf.Min(freeStep, Mathf.Max(0f, Age - state.SpinAt)));
            if (flows?[i]?.Started != true)
            {
                if (!state.ScheduledFlightDone && form.FlyAt >= 0 && age >= form.FlyAt)
                {
                    flightStep = Mathf.Min(freeStep, Mathf.Max(0f, age - form.FlyAt));
                    state.Position += state.Velocity * (freeStep - flightStep);
                    state.ScheduledFlightDone = true;
                    StartFlight(i, form.Flight, state.StartedAt + form.FlyAt);
                }
            }

            if (!state.MovementStopped && flows?[i]?.Started != true)
            {
                Vector3 aim = state.HasTarget ? state.Target : target;
                if (state.Flying && state.Flight.Path != null) MovePath(form, ref state, flightStep, aim);
                else
                {
                    if (state.Flying && state.Flight.TurnRate > 0f && state.Velocity.sqrMagnitude > 1e-8f &&
                        (aim - state.Position).sqrMagnitude > 1e-8f)
                    {
                        Vector3 before = state.Velocity;
                        state.Velocity = Vector3.RotateTowards(before,
                            (aim - state.Position).normalized * before.magnitude,
                            state.Flight.TurnRate * Mathf.Deg2Rad * flightStep, 0f);
                        state.Rotation = Quaternion.FromToRotation(before, state.Velocity) * state.Rotation;
                    }

                    state.Position += state.Velocity * flightStep +
                                      state.Acceleration * (.5f * flightStep * flightStep);
                    state.Velocity += state.Acceleration * flightStep;
                }
            }

            state.Transform = Matrix4x4.TRS(state.Position, state.Rotation,
                state.Scale * Mathf.Max(.00001f, growth));
            if (form.FragmentSource >= 0) state.Transform *= Matrix4x4.Translate(-state.Pivot);
        }
        else
        {
            state.Scale = attached.lossyScale;
            state.Transform = attached * Matrix4x4.Scale(Vector3.one * Mathf.Max(.00001f, growth));
        }

        if (state.MaterialRetained < 1f)
            state.Transform *= Matrix4x4.Scale(Vector3.one * Mathf.Pow(state.MaterialRetained, 1f / 3f));
        state.Visible = growth > .0001f && age >= form.Presence.x && age <= form.Presence.y;
        if (poses[i] != null && !state.MotionStopped && (bindInitial || began || !state.ShapeHeld))
        {
            if (state.Motion == null) state.MotionAge = (state.ShapeAge - form.MotionAt) * form.MotionSpeed;
            else
            {
                float local = state.MotionAge / state.MotionData.Speed;
                float next = state.MotionData.Hold.Advance(local, ref state.MotionHoldUsed, shapeDelta);
                state.MotionAge = next * state.MotionData.Speed;
                state.BlendAge += next - local;
            }

            SampleMotion(i, state.MotionAge, state.BlendAge);
        }

        state.Visible &= (poses[i]?.BuildProgress ?? 1f) > 0f && AnchorBuilt(i);
        state.SampleEpoch = sampleEpoch;
        if (form.NativeSteps != null) ObserveNativePose(i, age);
        // 先用旧控制走完本步，时点操作只影响从当前时点开始的后续运动。
        bool flowedBefore = flows?[i]?.Started == true;
        if (flowedBefore) AdvanceFlow(i, Mathf.Min(step, Mathf.Max(0f, age - state.FlowAt)), target);
        ResolveGround(i);
        ApplyMatterChanges(i, ConstructionChangeTrigger.Time);
        ApplyPendingOperations(i);
        if (state.Leaving)
        {
            state.SampleEpoch = sampleEpoch;
            return;
        }

        if (state.Recovering)
        {
            AdvanceRecovery(i);
            return;
        }

        if (form.FlowAt >= 0 && age >= form.FlowAt && !flows[i].Started) StartFlow(i);
        if (flows?[i]?.Started == true)
        {
            if (!state.ScheduledFlightDone && form.FlyAt >= 0 && age >= form.FlyAt)
            {
                state.ScheduledFlightDone = true;
                StartFlight(i, form.Flight, state.StartedAt + form.FlyAt);
            }

            if (!flowedBefore) AdvanceFlow(i, 0, target);
        }

        if (form.Conduction != null && age >= form.ConductionAt)
        {
            if (!state.Conduction.Started)
            {
                state.Conduction.Start(state.StartedAt + form.ConductionAt);
                NativeStarted(i, ConstructionOperationKind.Conduct);
            }

            state.Conduction.Tick(Age, form.Conduction);
        }

        CompiledRange conductors = Plan.Program.NodeConduction[i];
        for (int c = conductors.Start; c < conductors.End; c++)
        {
            int index = Plan.Program.Conduction[c];
            if (operations[index].Conduction.Started)
            {
                var command = Plan.Program.Commands[Plan.Program.EventCommands[index]];
                operations[index].Conduction.Tick(Age, Plan.Program.Parameter<CompiledConduction>(command));
            }
        }

        if (form.LeaveAt >= 0f && age >= form.LeaveAt) StartLeave(i);
        if (state.Leaving) return;
        if (form.RecoveryAt >= 0f && age >= form.RecoveryAt && !state.Recovering && !state.RecoveryInterrupted)
            StartRecovery(i, form.Recovery);
        if (state.Recovering)
        {
            AdvanceRecovery(i);
            return;
        }

        if (form.FractureAt >= 0f && age >= form.FractureAt && !fractures[i].Broken)
        {
            NativeStarted(i, ConstructionOperationKind.Fracture);
            RequestFracture(i);
        }

        if (!state.Exists) return;
        if (!began) SignalSizeChange(i);
        if (age >= form.Presence.y) SignalEnd(i, state.Transform.GetColumn(3));
    }

    /// <summary>由来源更新真实身体的脚下位置及尺寸，不在显示回调采集或推进。</summary>
    /// <param name="frame">当前逻辑步的实际身体。</param>
    public void UpdateBody(in BodyFrame frame) => bodyFrame = frame;

    /// <summary>真实连接建立或解除后使旧身体接触候选失效，不推进动作时间。</summary>
    internal void BodyConstraintChanged() => BodyContactRevision++;

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
            if (nodes[i].Exists && nodes[i].Transform != nodes[i].PreviousTransform)
                nodes[i].Revision++;
        if (fractures != null)
            for (int i = 0; i < fractures.Length; i++)
                fractures[i].Captured = false;
        if (ControlsBody) BodyContactRevision++;
    }

    /// <summary>中断身体；已经离手的形体继续，仍依赖手部的形体同时结束。</summary>
    public void InterruptBody()
    {
        if (Plan.Body == null || bodyInterrupted || bodyEndSent) return;
        RecordActionStage(-1, AbilityActionEventKind.Interrupted);
        bodyInterrupted = true;
        CancelAwaiters(-1);
        EndBodyWindows(true);
        if (nodes.Length == 0) Cancel();
        else if ((Plan.Binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector) && !nodes[0].Free)
        {
            EndNode(0, true);
            if (aliveNodes == 0) Cancel();
        }

        FlushBirths(inputs.Target);
    }

    /// <summary>根形体尚未解除本次输入连接，已脱离的作用不再占用载体。</summary>
    internal bool IsInputAttached => Plan.Binding?.Follow == true && nodes.Length > 0 && !nodes[0].Free && !Finished;

    /// <summary>只有明确允许且不再依赖来源连接的部位，才能在控制者失效后继续。</summary>
    /// <param name="node">实际部位。</param>
    internal bool CanContinueWithoutSource(int node)
    {
        if (!nodes[node].Exists || Plan.Nodes[node].SourceLoss != ConstructionSourceLoss.Continue) return false;
        for (int ancestor = node; ancestor >= 0; ancestor = Plan.Nodes[ancestor].Parent)
            if (nodes[ancestor].Free)
                return true;
        return Plan.Binding == null || !Plan.Binding.Follow || Plan.Binding.Anchor == AbilityAnchor.Target;
    }

    /// <summary>实际部位是否仍存在，不用剩余攻击力判断已经消费预算的传播是否有效。</summary>
    /// <param name="node">实际部位。</param>
    internal bool IsNodePresent(int node) => nodes[node].Exists;

    /// <summary>成功产生真实世界收益后，消耗本部位原支付份额，不伪造人物碰撞或重复回能。</summary>
    /// <param name="node">已经提交的实际部位，身体为负一。</param>
    /// <param name="changed">原版世界是否实际改变。</param>
    public void ConfirmWorldEffect(int node, bool changed)
    {
        if (changed && node >= 0) nodes[node].Resource = 0;
    }

    /// <summary>作用部位编译后的共用去重组；负一为身体。</summary>
    /// <param name="node">本次生成形状的部位。</param>
    public int ContactGroup(int node)
        => ContactGroupAtStage(node, bodyStage);

    /// <summary>预测跨作用边界时，先用原实例的接触限额确认边界前的路径。</summary>
    /// <param name="node">实际部位。</param><param name="stage">实际作用实例。</param>
    internal int ContactGroupAtStage(int node, int stage)
    {
        int group = node < 0 ? Plan.Nodes.Length : Plan.Nodes[node].ContactGroup;
        return group == Plan.Nodes.Length && Plan.Body != null ? group + stage : group;
    }

    /// <summary>读取身体或生成部位的完整作用覆盖；未覆盖时继承整招。</summary>
    /// <param name="node">展开部位，负一为身体。</param>
    public CompiledConstructionImpact Impact(int node) =>
        ImpactAtStage(node, bodyStage);

    /// <summary>读取指定实例的实际覆盖，避免新阶段作用提前应用到旧路径。</summary>
    /// <param name="node">实际部位。</param><param name="stage">实际作用实例。</param>
    internal CompiledConstructionImpact ImpactAtStage(int node, int stage) =>
        node < 0 ? Plan.Body.Stages[stage].Impact ?? Plan.Impact : Plan.Nodes[node].Impact ??
            (Plan.Nodes[node].BodyWindow ? Plan.Body.Stages[stage].Impact : null) ?? Plan.Impact;

    /// <summary>取得当前共同接触时点的形体原点，不从显示对象反查。</summary>
    /// <param name="node">展开部位，负一采用真实手部。</param>
    public Vector3 ContactOrigin(int node) =>
        node < 0 ? BodyPose.Value.EffectorPoint : nodes[node].Transform.GetColumn(3);

    /// <summary>总次数耗尽后结束同组实际部位，独立的其他组仍继续。</summary>
    /// <param name="group">已经耗尽的接触组。</param>
    internal void EndContactGroup(int group)
    {
        if (group >= nodes.Length && Plan.Body != null && !bodyResponse.HasValue)
        {
            if (Plan.Body.HasStages) ExhaustBodyStage();
            else InterruptBody();
        }
        for (int i = 0; i < nodes.Length; i++)
            if (ContactGroup(i) == group && !(Plan.Nodes[i].BodyWindow && Plan.Body.HasStages))
                EndNode(i);
        FlushBirths(inputs.Target);
    }

    /// <summary>只接受当前形体版本的候选，连接或运动改变前的路径不继续结算。</summary>
    /// <param name="capsule">此前采集的形状。</param>
    public bool IsCurrent(in AbilityCapsule capsule) => !cancelled && (capsule.Node < 0
        ? BodyContactEnabled && capsule.Revision == BodyContactRevision
        : nodes[capsule.Node].Exists && nodes[capsule.Node].Revision == capsule.Revision &&
          (!Plan.Nodes[capsule.Node].BodyWindow || BodyContactEnabled));

    /// <summary>确认真实接触后改变共同状态，末端位置按本步首次相交进度回到接触处。</summary>
    /// <param name="node">实际发生接触的部位。</param>
    /// <param name="fraction">本步内实际接触进度。</param>
    /// <param name="point">真实接触位置；缺省使用实际部位原点。</param>
    /// <param name="targetId">已经确认的实际人物编号。</param>
    public void ConfirmContact(int node, float fraction, Vector3? point = null, long targetId = 0)
    {
        if (node < 0)
        {
            if (BodyPose.HasValue)
            {
                if (bodyContactAt < 0f) bodyContactAt = Age;
                RecordActionContact(-1, point ?? BodyPose.Value.Hand, AbilityActionContactKind.Actor, targetId);
                SignalEvent(-1, ConstructionChangeTrigger.ActorContact, point ?? BodyPose.Value.Hand);
            }

            FlushBirths(inputs.Target);
            UpdateSteps();
            RecordActionTimeline();
            return;
        }

        // 接触反应可能已经结束该部位；保留已经确认的事实，再决定是否还能改变其状态。
        RecordActionContact(node, point ?? ContactOrigin(node), AbilityActionContactKind.Actor, targetId);
        if (Plan.Nodes[node].BodyWindow && bodyContactAt < 0f) bodyContactAt = Age;
        ref NodeState state = ref nodes[node];
        if (!state.Exists || state.Recovering || state.Leaving) return;
        state.Resource = 0;
        bool flowedBefore = flows?[node]?.Started == true;
        ApplyMatterChanges(node, ConstructionChangeTrigger.ActorContact);
        SignalEvent(node, ConstructionChangeTrigger.ActorContact, point ?? state.Transform.GetColumn(3));
        switch (Plan.Nodes[node].OnContact)
        {
            case ConstructionContactResponse.Stop:
                if (state.Recovering || !flowedBefore && flows?[node]?.Started == true) break;
                if (!state.MotionStopped || !state.GrowthStopped || !state.MovementStopped)
                {
                    StopNode(node, fraction);
                    state.Revision++;
                }

                break;
            case ConstructionContactResponse.End:
                SignalEnd(node, point ?? state.Transform.GetColumn(3));
                EndNode(node);
                break;
        }

        FlushBirths(inputs.Target);
        UpdateSteps();
        RecordActionTimeline();
    }

    /// <summary>在共同时间步预测之前保存前态；此阶段不调用游戏或发布事件。</summary>
    internal void SaveStep()
    {
        savedTerrain = terrain;
        if (flows != null)
            for (int i = 0; i < flows.Length; i++)
                flows[i]?.CopyTo(savedFlows[i]);
        Array.Copy(nodes, savedNodes, nodes.Length);
        if (fractures != null)
        {
            Array.Copy(fractures, savedFractures, fractures.Length);
            Array.Copy(fragmentMotion, savedFragmentMotion, fragmentMotion.Length);
        }

        if (gathers != null) Array.Copy(gathers, savedGathers, gathers.Length);
        Array.Copy(operations, savedOperations, operations.Length);
        Array.Copy(stepStates, savedStepStates, stepStates.Length);
        Array.Copy(stepParts, savedStepParts, stepParts.Length);
        for (int i = 0; i < poses.Length; i++)
        {
            poses[i]?.CopyTo(savedPoses[i]);
            transitionPoses[i]?.CopyTo(savedTransitionPoses[i]);
        }

        if (groundBase != null)
            for (int i = 0; i < groundBase.Length; i++)
                groundBase[i]?.CopyTo(savedGroundBase[i]);
        savedRecoveredResource = RecoveredResource;
        savedRecoveredVolume = RecoveredVolume;
        savedAge = Age;
        savedAliveNodes = aliveNodes;
        savedCancelled = cancelled;
        savedBodyInterrupted = bodyInterrupted;
        savedBodyStage = bodyStage;
        savedExhaustedBodyStage = exhaustedBodyStage;
        savedBodyContactAt = bodyContactAt;
        savedBodyResponse = bodyResponse;
        savedBodyResponseDelay = bodyResponseDelay;
        savedBodyRevision = BodyContactRevision;
        savedBodyEndSent = bodyEndSent;
        savedBodyActionStages = bodyActionStages;
        savedActionEventCount = actionEvents.Count;
        savedBodyFrame = bodyFrame;
        savedInputs = inputs;
        savedBodyPose = BodyPose;
        savedOrigin = origin;
    }

    /// <summary>发现更早接触后恢复，再只推进到真实交锋时点。</summary>
    internal void RestoreStep()
    {
        terrain = savedTerrain;
        if (flows != null)
            for (int i = 0; i < flows.Length; i++)
                savedFlows[i]?.CopyTo(flows[i]);
        Array.Copy(savedNodes, nodes, nodes.Length);
        if (fractures != null)
        {
            Array.Copy(savedFractures, fractures, fractures.Length);
            Array.Copy(savedFragmentMotion, fragmentMotion, fragmentMotion.Length);
        }

        if (gathers != null) Array.Copy(savedGathers, gathers, gathers.Length);
        Array.Copy(savedOperations, operations, operations.Length);
        Array.Copy(savedStepStates, stepStates, stepStates.Length);
        Array.Copy(savedStepParts, stepParts, stepParts.Length);
        for (int i = 0; i < poses.Length; i++)
        {
            savedPoses[i]?.CopyTo(poses[i]);
            savedTransitionPoses[i]?.CopyTo(transitionPoses[i]);
        }

        if (groundBase != null)
            for (int i = 0; i < groundBase.Length; i++)
                savedGroundBase[i]?.CopyTo(groundBase[i]);
        RecoveredResource = savedRecoveredResource;
        RecoveredVolume = savedRecoveredVolume;
        Age = savedAge;
        aliveNodes = savedAliveNodes;
        cancelled = savedCancelled;
        bodyInterrupted = savedBodyInterrupted;
        bodyStage = savedBodyStage;
        exhaustedBodyStage = savedExhaustedBodyStage;
        bodyContactAt = savedBodyContactAt;
        bodyResponse = savedBodyResponse;
        bodyResponseDelay = savedBodyResponseDelay;
        BodyContactRevision = savedBodyRevision;
        bodyEndSent = savedBodyEndSent;
        bodyActionStages = savedBodyActionStages;
        if (actionEvents.Count > savedActionEventCount)
            actionEvents.RemoveRange(savedActionEventCount, actionEvents.Count - savedActionEventCount);
        bodyFrame = savedBodyFrame;
        inputs = savedInputs;
        BodyPose = savedBodyPose;
        origin = savedOrigin;
    }

    /// <summary>部位的明确作用用途；实际身体仍由借用方结算正常近战。</summary>
    /// <param name="node">展开部位，负一为身体。</param>
    internal ConstructionContactRole Role(int node) =>
        node < 0 ? ConstructionContactRole.Attack : Plan.Nodes[node].ContactRole;

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
        if (before > 0) state.Resource *= state.Force / before;
        Vector3 point = state.HasClashPoint ? state.ClashPoint : (Vector3)state.Transform.GetColumn(3);
        state.HasClashPoint = false;
        if (state.Force < before && state.InitialForce > 0)
            SignalEvent(node, ConstructionChangeTrigger.ForceBelow, point, before / state.InitialForce,
                state.Force / state.InitialForce);
        if (fractures != null && Plan.Nodes[node].Fracture != null)
        {
            fractures[node].AwaitingSpend = false;
            FractureNode(node);
        }

        if (state.Force == 0f && (flows?[node]?.Started != true || Plan.Nodes[node].RemainderReceivers.Length > 0))
        {
            SignalEnd(node, point);
            EndNode(node);
        }

        FlushBirths(inputs.Target);
        UpdateSteps();
    }

    /// <summary>空装饰支点只组织后代，其后代全部结束后没有独立显示或作用要继续。</summary>
    /// <param name="node">实际编译部位。</param>
    private bool HasOwnWork(int node) => Plan.Nodes[node].Resource.Meshes.Length != 0 ||
                                         Plan.Nodes[node].ContactRole != ConstructionContactRole.Decoration;

    /// <summary>结束部位及仍依赖其连接的后代，离开的分支保持存在。</summary>
    /// <param name="node">实际结束的部位。</param>
    /// <param name="interrupted">是否因动作或来源中断结束。</param>
    internal void EndNode(int node, bool interrupted = false)
    {
        if (!nodes[node].Exists) return;
        if (nodes[node].Started)
            RecordActionStage(node,
                interrupted ? AbilityActionEventKind.Interrupted : AbilityActionEventKind.Completed);
        SignalEnd(node, nodes[node].Transform.GetColumn(3));
        if (!nodes[node].Exists) return;
        nodes[node].Resource = 0;
        nodes[node].Exists = false;
        nodes[node].Visible = false;
        nodes[node].Revision++;
        if (HasOwnWork(node)) aliveNodes--;
        for (int i = node + 1; i < nodes.Length; i++)
            if (IsAttachedBelow(i, node))
                EndNode(i, interrupted);
        CancelAwaiters(node);
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
    public void Cancel()
    {
        if (cancelled) return;
        if (Plan.Body != null) RecordActionStage(-1, AbilityActionEventKind.Interrupted);
        for (int i = 0; i < nodes.Length; i++)
            if (nodes[i].Exists && nodes[i].Started)
                RecordActionStage(i, AbilityActionEventKind.Interrupted);
        cancelled = true;
    }

    /// <summary>本部位已经发生的反射数；身体不参与形体反射。</summary>
    /// <param name="node">生成部位或负一身体。</param>
    public int Reflections(int node) => node < 0 ? 0 : nodes[node].Reflections;

    /// <summary>读取独立形体当前的世界速度。</summary>
    /// <param name="node">生成部位。</param>
    public Vector3 Velocity(int node) => nodes[node].Velocity;

    /// <summary>必须是仍存在、独立运动且尚有次数的部位；附着的身体或器物不会被凭空抛走。</summary>
    /// <param name="node">生成部位。</param>
    internal bool CanReflect(int node) => nodes[node].Exists && nodes[node].Free && !nodes[node].MovementStopped &&
                                          nodes[node].Velocity.sqrMagnitude > 1e-8f && nodes[node].Reflections <
                                          Plan.Nodes[node].ReflectionLimit;

    /// <summary>反射后继续沿普通飞行操作追踪各自目标，输入仍由主线程采集。</summary>
    /// <param name="node">实际部位。</param>
    /// <param name="target">本轮数值目标。</param>
    internal void SetTarget(int node, Vector3 target)
    {
        nodes[node].HasTarget = true;
        nodes[node].Target = target;
    }

    /// <summary>在真实接触处按表面相对速度反射，保留当时形状及姿态，作废旧路径。</summary>
    /// <param name="node">独立运动部位。</param>
    /// <param name="normal">屏障向外的单位方向。</param>
    /// <param name="surfaceVelocity">接触表面的实际速度。</param>
    /// <param name="target">上一控制者的数值位置。</param>
    internal void Reflect(int node, Vector3 normal, Vector3 surfaceVelocity, Vector3 target)
    {
        if (flows?[node]?.Started == true) flows[node].Reflect(normal, surfaceVelocity);
        ref NodeState state = ref nodes[node];
        Vector3 previousVelocity = state.Velocity;
        state.Velocity = surfaceVelocity + Vector3.Reflect(state.Velocity - surfaceVelocity, normal);
        AdjustFragmentVelocity(node, previousVelocity);
        if (state.Flight != null && !state.Flight.WorldVelocity)
            state.Acceleration = Vector3.Reflect(state.Acceleration, normal);
        if (state.Flying && state.Flight.Path != null)
        {
            state.PathVelocity = surfaceVelocity + Vector3.Reflect(state.PathVelocity - surfaceVelocity, normal);
            state.PathX = Vector3.Reflect(state.PathX, normal);
            state.PathY = Vector3.Reflect(state.PathY, normal);
            state.PathZ = Vector3.Reflect(state.PathZ, normal);
            state.Flight.Path.Sample(Mathf.Max(0f, Age - state.FlightStartedAt), out Vector3 offset, out _);
            state.PathCentre = state.Position - PathVector(in state, offset);
        }

        state.Authority = node;
        state.Reflections++;
        state.Revision++;
        SetTarget(node, target);
        for (int i = node + 1; i < nodes.Length; i++)
            if (IsAttachedBelow(i, node))
            {
                nodes[i].Authority = i;
                nodes[i].Reflections++;
                nodes[i].Revision++;
                SetTarget(i, target);
            }
    }

    /// <summary>发布所有仍存在的形体，绘制方只读同一变换。</summary>
    /// <param name="output">本轮显示集合。</param>
    public void AppendVisuals(List<FormVisualInstance> output)
    {
        if (Finished) return;
        for (int i = 0; i < nodes.Length; i++)
            if (nodes[i].Visible && NodeAge(i) < Plan.Nodes[i].Presence.y && Plan.Nodes[i].Resource.Meshes.Length > 0)
            {
                output.Add(new FormVisualInstance(Plan.Nodes[i].Resource, nodes[i].Transform,
                    surface: Plan.Nodes[i].Surface,
                    tint: nodes[i].Matter.Id == "fire"
                        ? new Color(1f, .31f, .035f,
                            nodes[i].InitialForce > 0 ? Mathf.Clamp01(nodes[i].Force / nodes[i].InitialForce) : 0f)
                        : FormBurningPresentation.Char(nodes[i].Burning, nodes[i].Volume),
                    groundAnchor: Plan.Nodes[i].FragmentSource >= 0 ? (Vector2?)nodes[i].Position : null,
                    skin: poses[i] == null ? null : (Matrix4x4[])poses[i].Skin.Clone(), poseBounds: poses[i]?.Bounds,
                    matterSurface: nodes[i].Matter.Surface, dye: Plan.Nodes[i].Dye, matter: nodes[i].Matter,
                    terrain: terrain, layer: flows?[i]?.Started == true ? ConductingFrame(i) : null,
                    buildProgress: poses[i]?.BuildProgress ?? 1f));
                FormBurningPresentation.Append(output, nodes[i].Transform, nodes[i].Burning, nodes[i].Volume, terrain,
                    Age);
            }
    }

    /// <summary>从相同部位姿态生成有效胶囊；未长出的部分不会提前造成作用。</summary>
    /// <param name="output">本轮作用形状。</param>
    /// <param name="includeEnd">刚推进至结束时仍检查最后一段路径；取消不会恢复接触。</param>
    /// <param name="includeMaterials">是否同时提供无攻击力量或装饰水体，供真实材料反应使用。</param>
    public void AppendContacts(List<AbilityCapsule> output, bool includeEnd = false, bool includeMaterials = false)
    {
        if (cancelled || Finished && !includeEnd) return;
        for (int i = 0; i < nodes.Length; i++)
        {
            CompiledForm form = Plan.Nodes[i];
            NodeState state = nodes[i];
            float age = Age - state.StartedAt;
            bool ending = includeEnd && FinalPathInPrediction(i);
            bool world = form.ContactRole != ConstructionContactRole.Decoration &&
                         Impact(i).WorldEffects.Count > 0;
            bool water = includeMaterials && state.Volume > 0 &&
                         (state.Matter.Id == "water" && state.Matter.State == "liquid" ||
                          AbilityBurning.CanBurn(state.Matter));
            if ((state.Recovering && !water || state.Leaving) && !ending || !state.Visible && !ending ||
                !includeEnd && age >= form.Presence.y ||
                form.ContactRole == ConstructionContactRole.Decoration && !water ||
                !water && !world &&
                (form.FragmentSource >= 0 || form.RemainderRoot >= 0 || flows?[i]?.Started == true) &&
                state.Force <= 0f && !ending || !water && !ProcessIsActive(i, includeEnd)) continue;
            bool flowing = flows?[i]?.Started == true;
            if (flowing)
            {
                AppendFlowContacts(i, output, includeEnd);
                if (!includeEnd || savedFlows[i].Started) continue;
            }

            for (int shape = 0; shape < form.Resource.Contacts.Length; shape++)
            {
                FormContactBinding contact = form.Resource.Contacts[shape];
                Matrix4x4 matrix = flowing ? flows[i].SourceTransform : state.Transform;
                if (!contact.Sample(poses[i], out Vector3 a, out Vector3 b, out float radius)) continue;
                Vector3 scale = matrix.lossyScale;
                output.Add(new AbilityCapsule(i, matrix.MultiplyPoint3x4(a), matrix.MultiplyPoint3x4(b),
                    radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)), state.Matter.Id,
                    shape, state.Revision, state.Matter,
                    until: flowing ? .99999994f : 1f));
            }
        }
    }

    /// <summary>一个独立形体的必要可变状态。</summary>
    private struct NodeState
    {
        /// <summary>当前动作已经经过的阶段；随数值预测一起保存和恢复。</summary>
        internal byte ActionStages;

        /// <summary>当前动作下一处尚未处理的制作标记。</summary>
        internal int NextActionMarker;

        /// <summary>显式重启动作时增加，循环显示不自行重放出手。</summary>
        internal int ActionInstance;

        /// <summary>本部位收束累计的实际请求资格、转换体积、已处理请求和入账。</summary>
        internal double RecoveryEligible, RecoveryConverted, RecoveryRequested, RecoveryGranted;

        /// <summary>已交给命名步骤回执的累计，不在每帧重发。</summary>
        internal double ReportedRecoveryRequest, ReportedRecoveryGranted, ReportedRecoveryVolume;

        /// <summary>真实收束及留置的共同执行来源，原有定时操作为空。</summary>
        internal CompiledEventOperation RecoveryOperation, LeaveOperation;

        /// <summary>本部位有限燃料、热量和点火来源，随预测状态完整恢复。</summary>
        internal AbilityBurning Burning;

        /// <summary>上一完整数值步因父体材料缩损产生的实际挂点运动，脱离时一并继承。</summary>
        internal Vector3 ShrinkVelocity;

        /// <summary>刚性材料局部消耗后的保留体积比例，预测恢复与后续姿态共用。</summary>
        internal float MaterialRetained;

        /// <summary>真实材料受火打断收束后不自动重启同一定时操作。</summary>
        internal bool RecoveryInterrupted;

        /// <summary>当前实际材料体积，与攻击力量分别计量。</summary>
        internal double Volume;

        /// <summary>实际开始流散的部位年龄。</summary>
        internal float FlowAt;

        /// <summary>当前控制身份所在的原部位；交接后只引用已经结束的稳定来源。</summary>
        internal int Authority;

        /// <summary>本次部位的实际材料及物态。</summary>
        internal AbilityMatter Matter;

        /// <summary>已确认的开始时点及实际分支起点，未收到条件时为正无穷。</summary>
        internal float BirthAt, StartedAt;

        /// <summary>本轮采样标记及点运动使用的实际步长。</summary>
        internal long SampleEpoch;

        /// <summary>最近实际步长及当前飞行开始时点。</summary>
        internal float LastStep, FlightStartedAt;

        /// <summary>当前完整飞行与实际地图加速度。</summary>
        internal CompiledFlight Flight;

        /// <summary>实际地图加速度。</summary>
        internal Vector3 Acceleration;

        /// <summary>最近连续步求得的真实附着点速度，不把浮点边界余差当作静止。</summary>
        internal Vector3 AttachedVelocity;

        /// <summary>当前角速度及开始时点。</summary>
        internal Vector3 Spin;

        /// <summary>最近命名或条件旋转的实际执行来源。</summary>
        internal CompiledEventOperation SpinOperation;

        /// <summary>当前旋转段的实际起点。</summary>
        internal float SpinAt;

        /// <summary>已完成旋转段的连续相对朝向。</summary>
        internal Quaternion SpinOffset;

        /// <summary>整体平移是否停止，与内部形状保持分别处理。</summary>
        internal bool MovementStopped;

        /// <summary>开始、排队、独立事件放置和结束通知均随快照恢复。</summary>
        internal bool Started, Queued, EventPlaced, EndSent;

        /// <summary>事件当时的实际位置及来源朝向。</summary>
        internal Vector3 EventPosition;

        /// <summary>独立放置采用的明确朝向。</summary>
        internal Quaternion EventRotation;

        /// <summary>内部形变是否保持当前进度。</summary>
        internal bool ShapeHeld;

        /// <summary>当前骨架是否带有地表挠曲，恢复基姿态后再计算下一轮。</summary>
        internal bool GroundDeformed;

        /// <summary>本次形变进度及上一数值步进度。</summary>
        internal float ShapeAge, PreviousShapeAge;

        /// <summary>已经执行的有限物态变化。</summary>
        internal ushort MatterChangesDone;

        /// <summary>尚未由主线程确认的世界材料转换请求。</summary>
        internal ushort WorldMatterPending;

        /// <summary>共同动作当前与上一步的内部时间，停止时冻结。</summary>
        internal float MotionAge, PreviousMotionAge;

        /// <summary>当前实际制作动作的共享参数，避免每次取样重新检查操作种类。</summary>
        internal CompiledMotionParameters MotionData;

        /// <summary>本部位最多16项条件执行的待处理位，零值直接跳过扫描。</summary>
        internal ushort PendingOperations;

        /// <summary>实际反射次数。</summary>
        internal int Reflections;

        /// <summary>本部位是否拥有反射后独立目标。</summary>
        internal bool HasTarget;

        /// <summary>由主线程更新的反射目标位置。</summary>
        internal Vector3 Target;

        /// <summary>相互消耗后的剩余作用量。</summary>
        internal float Force;

        /// <summary>本次部位最初的作用量，损耗条件始终相对同一基准。</summary>
        internal float InitialForce;

        /// <summary>最近一次确认交锋的实际位置，仅供随后真实扣减使用。</summary>
        internal Vector3 ClashPoint;

        /// <summary>是否已有等待扣减使用的真实交锋位置。</summary>
        internal bool HasClashPoint;

        /// <summary>当前完整变换。</summary>
        internal Matrix4x4 Transform;

        /// <summary>本次数值步之前的姿态，用于确定首次接触的停止位置。</summary>
        internal Matrix4x4 PreviousTransform;

        /// <summary>实际存在、停止及变化版本。</summary>
        internal bool Exists;

        /// <summary>尺寸及内部动作分别停止，不影响独立运动的恢复。</summary>
        internal bool GrowthStopped, MotionStopped;

        /// <summary>条件成形当前倍率及前一时点倍率。</summary>
        internal float GrowthValue, PreviousGrowth;

        /// <summary>当前条件尺寸过程的起止、历时及已运行秒数。</summary>
        internal float GrowthFrom, GrowthTarget, GrowthSeconds, GrowthAge;

        /// <summary>是否已经替换初始生长。</summary>
        internal bool GrowthChanged;

        /// <summary>最近尺寸过程的真实来源，用于判断完成与被替换。</summary>
        internal CompiledEventOperation GrowthOperation;

        /// <summary>当前条件动作及实际连续过渡进度。</summary>
        internal CompiledEventOperation Motion;

        /// <summary>当前及前一步的过渡秒数。</summary>
        internal float BlendAge, PreviousBlendAge;

        /// <summary>从当前姿态过渡到动作的保守角速度上限。</summary>
        internal float BlendAngularRate;

        /// <summary>接触改变后作废此前路径。</summary>
        internal int Revision;

        /// <summary>尚存支付份额及收束开始时固定的份额和体积。</summary>
        internal double Resource, RecoveryResource, RecoveryVolume;

        /// <summary>实际收束过程，开始后不恢复攻击或其他操作。</summary>
        internal bool Recovering;

        /// <summary>已经请求转交世界，或已确认过本次世界材料选择。</summary>
        internal bool Leaving, WorldRecoveryTaken;

        /// <summary>定时导电一次执行及尚待主线程领取的请求。</summary>
        internal ConductionRun Conduction;

        /// <summary>世界时间上的开始时点及已完成比例。</summary>
        internal float RecoveryAt, RecoveryProgress;

        /// <summary>从共享计划读取的已检查收束参数。</summary>
        internal CompiledRecovery Recovery;

        /// <summary>本次实际解除连接的时点。</summary>
        internal float ReleasedAt;

        /// <summary>已完成的形变维持秒数，保存进同一施放快照。</summary>
        internal float HoldUsed;

        /// <summary>当前实际启动动作已用的维持秒数，随共同快照回退，换动作重新开始。</summary>
        internal float MotionHoldUsed;

        /// <summary>已解除连接、已开始飞行和是否长成可见。</summary>
        internal bool Free, Flying, Visible;

        /// <summary>普通定时投送已执行，流散或条件换向后不重复叠加。</summary>
        internal bool ScheduledFlightDone;

        /// <summary>原定时连接确已解除，其他来源脱离不能冒充该操作。</summary>
        internal bool ScheduledDetachDone;

        /// <summary>脱落后的独立位置、尺寸及速度。</summary>
        internal Vector3 Position, Scale, Velocity;

        /// <summary>碎片的模型空间转动中心，几何保持原始坐标。</summary>
        internal Vector3 Pivot;

        /// <summary>曲线中心速度及释放时确定的三个实际参照方向。</summary>
        internal Vector3 PathCentre, PathVelocity, PathX, PathY, PathZ;

        /// <summary>脱落后的朝向。</summary>
        internal Quaternion Rotation;
    }
}

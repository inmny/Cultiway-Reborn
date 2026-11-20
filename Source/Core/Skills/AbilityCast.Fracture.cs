using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实分件在同一施放内继承姿态、余力和运动，不生成另一轮施法。</summary>
public sealed partial class AbilityCast
{
    /// <summary>只有存在碎解操作时才保存的状态与预测前态。</summary>
    private readonly FractureState[] fractures, savedFractures;
    /// <summary>各真实分件上一姿态的数值运动资料，不读取网格。</summary>
    private readonly FragmentMotion[] fragmentMotion, savedFragmentMotion;

    /// <summary>原部位结束时只继承可见姿态，落物不继承作用量、材料或控制关系。</summary>
    private void DropInterruptedPart(int node)
    {
        int index = Plan.Nodes[node].InterruptFall;
        if (index < 0 || !nodes[node].Visible || !nodes[index].Exists) return;
        NodeState source = nodes[node];
        ref NodeState drop = ref nodes[index];
        drop.Started = drop.Free = drop.Visible = true;
        drop.BirthAt = drop.StartedAt = drop.ReleasedAt = Age;
        drop.Transform = drop.PreviousTransform = source.Transform;
        drop.Position = source.Transform.GetColumn(3);
        drop.Rotation = source.Transform.rotation;
        drop.Scale = source.Transform.lossyScale;
        drop.GrowthValue = drop.PreviousGrowth = 1f;
        drop.Matter = source.Matter;
        drop.ShapeHeld = drop.MotionStopped = drop.GrowthStopped = true;
        drop.SampleEpoch = sampleEpoch;
        poses[node]?.CopyTo(poses[index]);
        StartFlight(ref drop, Plan.Nodes[index].Flight, Age);
        drop.Revision++;
    }

    /// <summary>在改变源体姿态前保存各分件真实中心和主要朝向，供脱落继承。</summary>
    /// <param name="node">正在推进的完整主体。</param>
    private void CaptureFragmentMotion(int node)
    {
        CompiledFracture fracture = Plan.Nodes[node].Fracture;
        if (fracture == null) return;
        ref FractureState state = ref fractures[node];
        if (!nodes[node].Visible || nodes[node].LastStep < .00001f) return;
        state.Captured = true; state.MotionStep = nodes[node].LastStep; state.VelocityAdjustment = Vector3.zero;
        foreach (int piece in fracture.Pieces)
        {
            var resource = Plan.Nodes[piece].Resource;
            fragmentMotion[piece] = new FragmentMotion
            {
                Centre = nodes[node].Transform.MultiplyPoint3x4(resource.PosedCentre(poses[node])),
                Rotation = nodes[node].Transform.rotation * resource.PosedRotation(poses[node])
            };
        }
    }

    /// <summary>反射或事件改变整体速度后，点运动立即继承该次变化，不沿用反射前方向。</summary>
    /// <param name="node">已经改变运动的主体。</param>
    /// <param name="previous">改变前的整体速度。</param>
    private void AdjustFragmentVelocity(int node, Vector3 previous)
    {
        if (fractures != null && Plan.Nodes[node].Fracture != null && fractures[node].Captured)
            fractures[node].VelocityAdjustment += nodes[node].Velocity - previous;
    }

    /// <summary>交锋尚未扣减时只登记请求，其他已确认过程可立即碎解。</summary>
    /// <param name="node">实际接受碎解的主体。</param>
    /// <param name="groundImpact">此次碎解是否由实际地面接触确认。</param>
    private void RequestFracture(int node, bool groundImpact = false)
    {
        ref FractureState state = ref fractures[node];
        if (state.Broken || !nodes[node].Exists || !nodes[node].Started) return;
        state.Pending = true; state.GroundImpact = groundImpact;
        if (!state.AwaitingSpend) FractureNode(node);
    }

    /// <summary>先复制真实分件，再交接余力和解除原物；最后只结束原主体与未脱离的依赖。</summary>
    /// <param name="node">已经完成本次真实扣减的主体。</param>
    private void FractureNode(int node)
    {
        ref FractureState execution = ref fractures[node];
        if (!execution.Pending || execution.Broken || !nodes[node].Exists) return;
        execution.Pending = false; execution.Broken = true;
        CompiledFracture fracture = Plan.Nodes[node].Fracture; NodeState source = nodes[node];
        float share = fracture.ActiveCount == 0 ? 0f : source.Force / fracture.ActiveCount;
        float remaining = source.Force; int activeLeft = fracture.ActiveCount;
        Quaternion sourceRotation = source.Transform.rotation;
        Vector3 sourceScale = source.Transform.lossyScale, originPoint = source.Transform.GetColumn(3);
        for (int part = 0; part < fracture.Pieces.Length; part++)
        {
            int index = fracture.Pieces[part]; ref NodeState piece = ref nodes[index];
            var resource = Plan.Nodes[index].Resource;
            Vector3 pivot = resource.PosedCentre(poses[node]), centre = source.Transform.MultiplyPoint3x4(pivot);
            Vector3 velocity = source.Free ? source.Velocity : source.AttachedVelocity;
            Vector3 spin = source.Spin;
            if (execution.Captured)
            {
                velocity = (centre - fragmentMotion[index].Centre) / execution.MotionStep + execution.VelocityAdjustment;
                Quaternion delta = (sourceRotation * resource.PosedRotation(poses[node])) * Quaternion.Inverse(fragmentMotion[index].Rotation);
                delta.ToAngleAxis(out float angle, out Vector3 axis); if (angle > 180f) angle -= 360f;
                spin = Mathf.Abs(angle) < .00001f ? Vector3.zero : Quaternion.Inverse(sourceRotation) * axis * (angle / execution.MotionStep);
            }
            Vector3 direction = centre - originPoint;
            if (direction.sqrMagnitude < .000001f) direction = sourceRotation * new Vector3(Mathf.Cos(part * 2.399963f), Mathf.Sin(part * 2.399963f), .3f);
            piece.Started = true; piece.BirthAt = Age; piece.StartedAt = Age; piece.ReleasedAt = Age;
            piece.Free = true; piece.Visible = source.Visible; piece.Pivot = pivot;
            piece.Position = centre; piece.Rotation = sourceRotation; piece.Scale = sourceScale; piece.GrowthValue = piece.PreviousGrowth = 1f;
            piece.Transform = source.Transform; piece.PreviousTransform = source.Transform; piece.SampleEpoch = sampleEpoch;
            piece.Matter = source.Matter; piece.ShapeHeld = true; piece.MotionStopped = true; piece.GrowthStopped = true;
            piece.Authority = source.Authority; piece.Reflections = source.Reflections; piece.HasTarget = source.HasTarget; piece.Target = source.Target;
            float transferred = fracture.Active[part] ? activeLeft == 1 ? remaining : share : 0f;
            if (fracture.Active[part]) { remaining = Mathf.Max(0f, remaining - transferred); activeLeft--; }
            piece.Force = piece.InitialForce = transferred;
            piece.Guarded = false;
            piece.Volume = source.Volume / fracture.Pieces.Length;
            piece.Burning = source.Burning;
            piece.Burning.Retain(1d / fracture.Pieces.Length);
            piece.Resource = source.Resource / fracture.Pieces.Length;
            float rebound = execution.GroundImpact ? fracture.GroundRebound : 0f;
            piece.Spin = spin * (1f - rebound) + new Vector3(fracture.Spin * .37f, fracture.Spin * (part % 2 == 0 ? 1f : -1f),
                fracture.Spin * .21f);
            piece.SpinAt = Age;
            piece.SpinOffset = Quaternion.identity;
            piece.Velocity = velocity + direction.normalized * fracture.Speed;
            fragmentMotion[index].Bounds = resource.PosedBounds(poses[node]);
            poses[node]?.CopyTo(poses[index]);
            if (rebound > 0f && piece.Velocity.z < 0f)
            {
                piece.Velocity.z *= -rebound;
                // 沿用撞点当时的横向位置；只消除共同接触误差造成的入地，再交给普通飞行。
                float clearance = MinimumHeight(index, piece.Transform, poses[index]);
                if (clearance < 0f)
                {
                    piece.Position.z -= clearance;
                    piece.Transform.m23 -= clearance;
                    piece.PreviousTransform = piece.Transform;
                }
            }
            StartFlight(ref piece, fracture.Flight, Age); piece.Revision++;
        }
        if (fracture.ReleaseChildren)
            for (int child = node + 1; child < nodes.Length; child++)
                if (Plan.Nodes[child].Parent == node && Plan.Nodes[child].FragmentSource < 0 && nodes[child].Started && nodes[child].Exists)
                    ReleaseNode(child);
        nodes[node].Force = 0f; nodes[node].Volume = 0; nodes[node].Resource = 0;
        EndNode(node);
    }

    /// <summary>一次性碎解与交锋等待状态进入同一预测快照。</summary>
    private struct FractureState
    {
        /// <summary>已经登记、真正碎解和本轮运动采样是否可用。</summary>
        internal bool Pending, Broken, Captured;
        /// <summary>正在等待此次交锋的真实扣减。</summary>
        internal bool AwaitingSpend;
        /// <summary>本次碎解来自已经确认的地面撞击。</summary>
        internal bool GroundImpact;
        /// <summary>最近可稳定求差的实际步长，微小边界余步不清除点运动。</summary>
        internal float MotionStep;
        /// <summary>本数值边界之后已确认的整体速度改变。</summary>
        internal Vector3 VelocityAdjustment;
    }

    /// <summary>分件在数值推进前的真实运动输入。</summary>
    private struct FragmentMotion
    {
        /// <summary>当时世界中心。</summary>
        internal Vector3 Centre;
        /// <summary>当时世界主要朝向。</summary>
        internal Quaternion Rotation;
        /// <summary>出生后冻结的装饰运动边界，不用于攻击。</summary>
        internal Bounds Bounds;
    }
}

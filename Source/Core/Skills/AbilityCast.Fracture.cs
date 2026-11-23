using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实分件在同一施放内继承姿态、余力和运动，不生成另一轮施法。</summary>
public sealed partial class AbilityCast
{
    /// <summary>只有存在碎解操作时才保存的状态与预测前态。</summary>
    private readonly FractureState[] fractures, savedFractures;
    /// <summary>每个碎解源体在上一时点的骨架变化。没有骨架的源体不存矩阵。</summary>
    private readonly Matrix4x4[][] fractureSkins, savedFractureSkins;
    /// <summary>分件出生后冻结的装饰运动边界，不用于攻击。</summary>
    private readonly Bounds[] fragmentBounds, savedFragmentBounds;

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

    /// <summary>通过源体部位取得紧凑的碎解资料。</summary>
    /// <param name="node">实际拥有分件的部位。</param>
    private ref FractureState FractureFor(int node) => ref fractures[Plan.Nodes[node].FractureIndex];

    /// <summary>仅保存源体的前一姿态，到实际碎解时才求各分件的点速度。</summary>
    /// <param name="node">正在推进的完整主体。</param>
    /// <param name="index">源体对应的碎解状态位置。</param>
    private void CaptureFragmentMotion(int node, int index)
    {
        ref FractureState state = ref fractures[index];
        if (!nodes[node].Visible || nodes[node].LastStep < .00001f) return;
        state.Captured = true; state.MotionStep = nodes[node].LastStep; state.VelocityAdjustment = Vector3.zero;
        state.PreviousTransform = nodes[node].Transform;
        if (fractureSkins[index] is { } skin) Array.Copy(poses[node].Skin, skin, skin.Length);
    }

    /// <summary>预测及回退只复制真正捕获过的骨架。</summary>
    /// <param name="source">当前保存方向的原姿态。</param>
    /// <param name="destination">当前保存方向的目标缓冲。</param>
    private void CopyFractureSkins(Matrix4x4[][] source, Matrix4x4[][] destination)
    {
        for (int i = 0; i < fractures.Length; i++)
            if (fractures[i].Captured && source[i] is { } skin)
                Array.Copy(skin, destination[i], skin.Length);
    }

    /// <summary>反射或事件改变整体速度后，点运动立即继承该次变化，不沿用反射前方向。</summary>
    /// <param name="node">已经改变运动的主体。</param>
    /// <param name="previous">改变前的整体速度。</param>
    private void AdjustFragmentVelocity(int node, Vector3 previous)
    {
        if (Plan.Nodes[node].FractureIndex >= 0 && FractureFor(node).Captured)
            FractureFor(node).VelocityAdjustment += nodes[node].Velocity - previous;
    }

    /// <summary>交锋尚未扣减时只登记请求，其他已确认过程可立即碎解。</summary>
    /// <param name="node">实际接受碎解的主体。</param>
    /// <param name="groundImpact">此次碎解是否由实际地面接触确认。</param>
    private void RequestFracture(int node, bool groundImpact = false)
    {
        ref FractureState state = ref FractureFor(node);
        if (state.Broken || !nodes[node].Exists || !nodes[node].Started) return;
        state.Pending = true; state.GroundImpact = groundImpact;
        if (!state.AwaitingSpend) FractureNode(node);
    }

    /// <summary>先复制真实分件，再交接余力和解除原物；最后只结束原主体与未脱离的依赖。</summary>
    /// <param name="node">已经完成本次真实扣减的主体。</param>
    private void FractureNode(int node)
    {
        int fractureIndex = Plan.Nodes[node].FractureIndex;
        ref FractureState execution = ref fractures[fractureIndex];
        if (!execution.Pending || execution.Broken || !nodes[node].Exists) return;
        execution.Pending = false; execution.Broken = true;
        CompiledFracture fracture = Plan.Nodes[node].Fracture; NodeState source = nodes[node];
        float share = fracture.ActiveCount == 0 ? 0f : source.Force / fracture.ActiveCount;
        float remaining = source.Force; int activeLeft = fracture.ActiveCount;
        Quaternion sourceRotation = source.Transform.rotation;
        Quaternion priorRotation = execution.Captured ? execution.PreviousTransform.rotation : Quaternion.identity;
        Matrix4x4[] sourceSkin = poses[node]?.Skin, priorSkin = fractureSkins[fractureIndex];
        Vector3 sourceScale = source.Transform.lossyScale, originPoint = source.Transform.GetColumn(3);
        for (int part = 0; part < fracture.Pieces.Length; part++)
        {
            int index = fracture.Pieces[part]; ref NodeState piece = ref nodes[index];
            var resource = Plan.Nodes[index].Resource;
            Vector3 pivot = resource.PosedCentre(sourceSkin), centre = source.Transform.MultiplyPoint3x4(pivot);
            Vector3 velocity = source.Free ? source.Velocity : source.AttachedVelocity;
            Vector3 spin = source.Spin;
            if (execution.Captured)
            {
                Vector3 priorCentre = execution.PreviousTransform.MultiplyPoint3x4(resource.PosedCentre(priorSkin));
                velocity = (centre - priorCentre) / execution.MotionStep + execution.VelocityAdjustment;
                Quaternion delta = (sourceRotation * resource.PosedRotation(sourceSkin)) *
                                   Quaternion.Inverse(priorRotation * resource.PosedRotation(priorSkin));
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
            fragmentBounds[index] = resource.PosedBounds(poses[node]);
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
        /// <summary>上一推进边界的完整源体变换，与同一时点的骨架一起使用。</summary>
        internal Matrix4x4 PreviousTransform;
    }
}

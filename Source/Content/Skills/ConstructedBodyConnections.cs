using System;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>双方关系的真实结果，不把选中人物当成已经抓住。</summary>
public enum ConstructedBodyConnectionState
{
    /// <summary>已经取得双方控制，仍在维持。</summary>
    Active,

    /// <summary>已经归还目标控制。</summary>
    Released,

    /// <summary>确实接触，但对抗或占用等条件不允许建立关系。</summary>
    Rejected
}

/// <summary>双方实际连接点、控制归属和剩余维持预算。</summary>
public readonly struct ConstructedBodyConnection
{
    /// <summary>本次来源施放及双方身份。</summary>
    public readonly long CastId, SourceId, TargetId;

    /// <summary>构造中的关系编号。</summary>
    public readonly string RelationId;

    /// <summary>实际关系方式和当前状态。</summary>
    public readonly BodyRelationKind Kind;

    /// <summary>建立、维持、解除或拒绝的实际状态。</summary>
    public readonly ConstructedBodyConnectionState State;

    /// <summary>双方实际连接点，不读取绘制坐标。</summary>
    public readonly Vector3 SourcePoint, TargetPoint;

    /// <summary>实际世界时点和最晚解除时点。</summary>
    public readonly double WorldTime, Until;

    /// <summary>尚未用掉的维持预算。</summary>
    public readonly float Remaining;

    /// <summary>拒绝或解除原因，维持时为空。</summary>
    public readonly string Reason;

    /// <summary>环境接触有独立真实点和法线，不能把抓点冒充撞击处。</summary>
    public readonly Vector3? EnvironmentPoint, EnvironmentNormal;

    /// <summary>身体真实接触点，保留与环境已有重叠的差值。</summary>
    public readonly Vector3? EnvironmentBodyPoint;

    /// <summary>实际环境身份；地面为零。</summary>
    public readonly long EnvironmentOwner;

    /// <summary>只从已确认的运行状态创建。</summary>
    /// <param name="cast">施放。</param><param name="source">来源。</param><param name="target">目标。</param>
    /// <param name="relation">实际关系。</param><param name="state">实际结果。</param>
    /// <param name="from">来源连接点。</param><param name="to">目标连接点。</param>
    /// <param name="time">当前时点。</param><param name="until">最晚解除时点。</param>
    /// <param name="remaining">剩余预算。</param><param name="reason">明确原因。</param>
    /// <param name="environmentPoint">真实环境接触点。</param><param name="environmentNormal">环境法线。</param>
    /// <param name="environmentOwner">环境身份。</param>
    /// <param name="environmentBodyPoint">身体上的真实位置。</param>
    internal ConstructedBodyConnection(long cast, long source, long target, in CompiledBodyRelation relation,
        ConstructedBodyConnectionState state, Vector3 from, Vector3 to, double time, double until, float remaining,
        string reason = null, Vector3? environmentPoint = null, Vector3? environmentNormal = null, long environmentOwner = 0, Vector3? environmentBodyPoint = null)
    {
        CastId = cast;
        SourceId = source;
        TargetId = target;
        RelationId = relation.Id;
        Kind = relation.Kind;
        State = state;
        SourcePoint = from;
        TargetPoint = to;
        WorldTime = time;
        Until = until;
        Remaining = remaining;
        Reason = reason;
        EnvironmentPoint = environmentPoint;
        EnvironmentNormal = environmentNormal;
        EnvironmentOwner = environmentOwner;
        EnvironmentBodyPoint = environmentBodyPoint;
    }
}

/// <summary>在共同接触时点建立双方控制；绘制和预测不取得或归还凭据。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>实际建立、拒绝或解除后发布。</summary>
    public static event Action<ConstructedBodyConnection> BodyConnectionChanged;

    /// <summary>已确认的一条关系；解除后可短暂保留手部归还，不再占用目标。</summary>
    private sealed class BodyConnection
    {
        /// <summary>本次真实来源和不可变关系。</summary>
        internal RunningCast Owner;

        /// <summary>编译后的双方声明。</summary>
        internal CompiledBodyRelation Relation;

        /// <summary>真实接触时的完整目标表面，维持与显示沿用同一姿态。</summary>
        internal BodySurfacePose Surface;

        /// <summary>抓住时的原表面与抓点，带动始终从这份真实联系计算，不累计图片位移。</summary>
        internal BodySurfacePose OriginalSurface;

        /// <summary>本步纯数值预测，不提前写入人物或替换已确认表面。</summary>
        internal BodySurfacePose PredictedSurface;

        /// <summary>建立时的共同世界抓点。</summary>
        internal Vector3 OriginalPoint;

        /// <summary>真实建立时的发力部位朝向，目标只继承之后发生的相对转动。</summary>
        internal Quaternion OriginalRotation;

        /// <summary>当前预测的共同末端时间。</summary>
        internal double PredictedTime;

        /// <summary>建立时的真实可见几何，携带期间只更新刚性姿态。</summary>
        internal BodySurfaceMesh Geometry;

        /// <summary>仅由确认步骤更新的真实速度及其前次取样时间。</summary>
        internal Vector3 LinearVelocity, AngularVelocity;

        /// <summary>最近速度片段的真实前态；极短尾步不覆盖完整运动片段。</summary>
        internal BodySurfaceTransform VelocityPose;

        /// <summary>速度片段的真实起点。</summary>
        internal double VelocityTime;

        /// <summary>已经取得足够长度的确认片段。</summary>
        internal bool VelocitySampled;

        /// <summary>前次确认的实际刚性变换，不受预测或回退影响。</summary>
        internal BodySurfaceTransform Motion;

        /// <summary>真实速度对应的共同时间。</summary>
        internal double MotionTime;

        /// <summary>已经确认的实际环境接触，普通解除为空。</summary>
        internal BodySurfaceHit? EnvironmentContact;

        /// <summary>真实抓持取得时冻结的撞击来源，离手后沿用。</summary>
        internal BodyImpactSource ImpactSource;

        /// <summary>实际抓到的部件和面内位置。</summary>
        internal BodySurfaceAnchor Anchor;

        /// <summary>接触时的实际身体和装备，新的采集不能掩盖旧关系已经失效。</summary>
        internal BodySurfaceBinding SurfaceBinding;

        /// <summary>原目标及稳定身份。</summary>
        internal Actor Target;

        /// <summary>原目标稳定编号。</summary>
        internal long TargetId;

        /// <summary>目标身体资源，改变后不继续沿旧部位控制。</summary>
        internal ActorAsset SourceAsset, TargetAsset;

        /// <summary>真正取得的目标凭据。</summary>
        internal BodyControlLease TargetLease;

        /// <summary>兵器抵住继续使用目标自己的出招控制，不另取全身凭据。</summary>
        internal RunningCast WeaponTarget;

        /// <summary>没有主动出招的目标只借用右臂，原物品和自然身体仍沿原入口维护。</summary>
        internal PassiveWeaponArm PassiveWeapon;

        /// <summary>兵器关系不能误走整体身体固定或携带路径。</summary>
        internal bool WeaponOnly => WeaponTarget != null || PassiveWeapon != null;

        /// <summary>目标兵器真实接触时的动作阶段和接触点相对发力部位的位置。</summary>
        internal float WeaponTargetAge;

        /// <summary>目标握持臂中保存的真实接触偏移。</summary>
        internal Vector3 WeaponTargetLocal;

        /// <summary>接触时目标实际根点和身体高度。</summary>
        internal Vector3 TargetFoot;

        /// <summary>接触时体高及来源接触面的真实半径。</summary>
        internal float SourceHeight, TargetHeight, Radius;

        /// <summary>同一个真实连接点及其在实际部位的局部位置。</summary>
        internal Vector3 Point, Local, Normal;

        /// <summary>原兵器接触形状，持续转动后仍约束在同一真实形状内。</summary>
        internal int SourceShape;

        /// <summary>其他部位原地维持使用的真实接触阶段。</summary>
        internal float PoseAge;

        /// <summary>自由身体输入供抵住时检查实际收手。</summary>
        internal BodyFrame FreeFrame;

        /// <summary>预算、上次已确认时点、最晚解除时点及归还开始。</summary>
        internal float Remaining;

        /// <summary>只按共同世界秒数更新。</summary>
        internal double LastTime, Until, ReleasedAt;

        /// <summary>目标控制仍实际生效。</summary>
        internal bool Active;

        /// <summary>由同一真实部位姿态换算当前接触点，不以腕点代替肘、足或器官。</summary>
        internal Vector3 SourcePoint
        {
            get
            {
                if (Owner.Cast.BodyPose is not BodyPose pose) return Point;
                Vector3 point = pose.EffectorPoint + pose.EffectorRotation * Local;
                return WeaponOnly && Owner.Body.Weapon != null
                    ? Owner.Body.ClampWeaponContactPoint(in pose, SourceShape, point) : point;
            }
        }

        /// <summary>外部搬移或中断后的目标也报告真正位置，不沿用旧连接点。</summary>
        internal Vector3 TargetPoint =>
            PassiveWeapon != null ? PassiveWeapon.Pose.Hand + PassiveWeapon.ContactOffset :
            WeaponTarget?.Cast.BodyPose is BodyPose pose
                ? pose.EffectorPoint + pose.EffectorRotation * WeaponTargetLocal
                : Target.data?.id == TargetId ? Point + (PhysicsFoot(Target) - TargetFoot) : Point;

        /// <summary>维护或结束后的完整事实。</summary>
        /// <param name="state">实际状态。</param><param name="time">确认时间。</param><param name="reason">结束原因。</param>
        internal ConstructedBodyConnection Fact(ConstructedBodyConnectionState state, double time,
            string reason = null) =>
            new(Owner.Id, Owner.SourceId, TargetId, Relation, state, SourcePoint, TargetPoint, time, Until, Remaining,
                reason, EnvironmentContact?.Point, EnvironmentContact?.Normal, EnvironmentContact?.Owner ?? 0, EnvironmentContact?.BodyPoint);
    }

    /// <summary>按需读取真实维持结果，不为每个渲染帧分配全局关系列表。</summary>
    public static ConstructedBodyConnection[] GetBodyConnections()
    {
        var result = new List<ConstructedBodyConnection>();
        foreach (RunningCast running in casts)
            if (running.Connection?.Active == true)
                result.Add(running.Connection.Fact(ConstructedBodyConnectionState.Active, LogicTime));
        return result.ToArray();
    }

    /// <summary>独立于伤害接触寻找所选身体关系，普通命中次数不会伪装为已抓住。</summary>
    /// <param name="shape">本步实际手部或器物。</param><param name="actor">所选真实目标。</param>
    /// <param name="clock">共同起点。</param>
    /// <param name="step">本段世界秒数。</param><param name="include">加入共同最早接触候选。</param>
    private static void FindBodyConnections(in ShapeMotion shape, in ActorMotion actor, double clock,
        float step, Action<ContactChoice> include)
    {
        RunningCast running = shape.Running;
        if (shape.To.Node >= 0 || running.TargetId != actor.Id || !running.BodyControlled ||
            running.Connection?.Active == true) return;
        var relations = running.Cast.Plan.Body.Construction.Relations;
        for (int i = 0; i < relations.Count; i++)
        {
            var relation = relations[i];
            if (relation.Kind is not (BodyRelationKind.Brace or BodyRelationKind.Grab or BodyRelationKind.Deflect or BodyRelationKind.Disarm) ||
                running.ConnectionAttempts?.Contains(i) == true ||
                !running.Body.UsesRelationShape(relation, shape.To.Shape)) continue;
            float startAge = running.Cast.Age - step;
            if (running.Cast.Age < relation.FromWorldTime || startAge >= relation.UntilWorldTime) continue;
            float earliest = step > 0f ? Mathf.Clamp01((relation.FromWorldTime - startAge) / step) : 0f;
            if (!shape.Continuous) earliest = 1f;
            BodySurfacePose end = BodyContactSurfaceAt(actor.Id, clock + step);
            BodySurfacePose begin = BodyContactSurfaceAt(actor.Id, clock + step * earliest);
            if (begin == null || end == null) continue;
            AbilityCapsule a = Slice(shape.From, shape.To, earliest);
            float at;
            BodySurfaceAnchor anchor;
            Vector3 normal;
            bool touched;
            if (running.Cast.Plan.Body.Part.IsJoint)
            {
                BodySurfacePose sourceEnd = running.Cast.BodyPose.Value.Joint.Contact;
                BodySurfacePose sourceBegin = shape.Continuous ? running.BeforeJoint.Contact : sourceEnd;
                sourceBegin = BodySurfacePose.Lerp(sourceBegin, sourceEnd, earliest);
                touched = BodySurfacePose.FindSurfaces(begin, end, sourceBegin, sourceEnd, relation.To.Part, out at, out anchor, out normal);
            }
            else touched = BodySurfacePose.Find(begin, end, a, shape.To, relation.To.Part, out at, out anchor, out normal);
            if (touched)
            {
                float fraction = Mathf.Lerp(earliest, 1f, at);
                if (startAge + step * fraction >= relation.UntilWorldTime) continue;
                BodySurfacePose surface = BodySurfacePose.Lerp(begin, end, at);
                include(new ContactChoice(shape, actor, null, fraction, anchor.Point(surface), normal, step,
                    bodyRelation: i,
                    bodySurface: surface, bodyAnchor: anchor));
            }
        }
    }

    /// <summary>真实接触后才检查对抗和占用，再取得目标并固定共同时间根点。</summary>
    /// <param name="hit">已经回到真实时点的接触。</param>
    private static void ResolveBodyConnection(in ContactChoice hit)
    {
        RunningCast owner = hit.Left.Running;
        CompiledBodyRelation rule = owner.Cast.Plan.Body.Construction.Relations[hit.BodyRelation];
        (owner.ConnectionAttempts ??= new()).Add(hit.BodyRelation);
        Actor target = hit.Actor.Actor;
        if (rule.Kind is BodyRelationKind.Brace or BodyRelationKind.Deflect or BodyRelationKind.Disarm && rule.To.Part == "item")
        {
            if (rule.Kind == BodyRelationKind.Disarm) ResolveWeaponDisarm(hit, rule);
            else ResolveWeaponConnection(hit, rule);
            return;
        }
        string reason = null;
        if (!owner.BodyControlled || owner.Cast.Finished || target.data?.id != hit.Actor.Id || !target.isAlive() ||
            !target.hasHealth()) reason = "InvalidBody";
        else if (target.isImmovable() || !target.asset.can_be_moved_by_powers) reason = "Immovable";
        else if (target.under_forces && !ConstructedActorImpulse.TryGetSurface(target, out _)) reason = "ExistingForce";
        else if (target.GetExtend().ForceResponseScale(owner.Source, owner.InitialOwner.Damage.AttackerPowerLevel) <=
                 0f) reason = "Resistance";
        BodyControlLease lease = reason == null
            ? BodyControlState.TryAcquire(target.id, BodyOccupancy.Stance,
                owner.Body.Facing, controlsHeight: true)
            : null;
        if (reason == null && lease == null) reason = "Occupied";
        if (reason != null)
        {
            BodyConnectionChanged?.Invoke(new ConstructedBodyConnection(owner.Id, owner.SourceId, hit.Actor.Id,
                rule, ConstructedBodyConnectionState.Rejected, hit.Point, hit.Point, LogicTime,
                owner.StartedAt + rule.UntilWorldTime, 0f, reason));
            return;
        }

        StartingPose(target, out Vector3 foot, out float height);
        target.stopMovement();
        // 原版可能已经推进至帧末；新取得的身体回到本次已确认接触时点，不拉向施术者。
        PublishBodyPosition(target, foot);
        BodyPose pose = owner.Cast.BodyPose.Value;
        owner.Connection = new BodyConnection
        {
            Owner = owner, Relation = rule, Target = target, TargetId = target.id, SourceAsset = owner.Source.asset,
            Surface = hit.BodySurface,
            OriginalSurface = hit.BodySurface, OriginalPoint = hit.Point,
            ImpactSource = new BodyImpactSource(owner.Id, owner.SourceId, target.id, worldGeneration,
                owner.InitialOwner.Damage, Mathf.Max(0f, target.stats["mass"]), rule.CollisionDamageScale),
            Geometry = rule.MoveTarget ? new BodySurfaceMesh(hit.BodySurface) : null,
            OriginalRotation = pose.EffectorRotation,
            Motion = new BodySurfaceTransform(hit.Point, hit.Point, Quaternion.identity), MotionTime = LogicTime,
            VelocityPose = new BodySurfaceTransform(hit.Point, hit.Point, Quaternion.identity), VelocityTime = LogicTime,
            Anchor = hit.BodyAnchor,
            SurfaceBinding = bodySurfaces[target.id],
            TargetAsset = target.asset,
            TargetLease = lease, TargetFoot = foot, TargetHeight = height, Point = hit.Point,
            SourceHeight = pose.Height,
            Local = Quaternion.Inverse(pose.EffectorRotation) * (hit.Point - pose.EffectorPoint),
            PoseAge = owner.Cast.Age,
            Normal = hit.Normal.sqrMagnitude > .000001f ? hit.Normal.normalized : (foot - pose.Foot).normalized,
            Radius = hit.Left.To.Radius, Active = true, LastTime = LogicTime,
            FreeFrame = owner.Body.Frame(pose.Foot, pose.Height,
                owner.Movement?.Sample(owner.Cast.Age, pose.Height, simulation.terrain).Support),
            Until = owner.StartedAt + rule.UntilWorldTime,
            Remaining = Mathf.Max(0f, rule.UntilWorldTime - owner.Cast.Age) * rule.ControlStrength
        };
        if (rule.MoveTarget) carriedBodies.Add(target.id, owner.Connection);
        owner.Cast.ConfirmContact(-1, 1f, hit.Point, target.id);
        ConfirmBodyFeedback(owner, hit.Point);
        owner.Cast.BodyConstraintChanged();
        BodyConnectionChanged?.Invoke(owner.Connection.Fact(ConstructedBodyConnectionState.Active, LogicTime));
    }

    /// <summary>预测只取得纯数值约束，解除时平滑归还自由动作。</summary>
    /// <param name="running">本次来源。</param><param name="frame">本步自由身体。</param><param name="time">本步世界时点。</param>
    private static BodyFrame ApplyBodyConnection(RunningCast running, in BodyFrame frame, double time)
    {
        if (running.WeaponConstraint?.Active == true)
        {
            BodyConnection pressure = running.WeaponConstraint;
            BodyFrame constrained = frame.WithPoseConstraint(new BodyPoseConstraint(pressure.WeaponTargetAge));
            if (pressure.Relation.Kind == BodyRelationKind.Deflect)
            {
                Vector3 point = Vector3.Dot(pressure.SourcePoint - pressure.Point, pressure.Normal) >= 0f
                    ? pressure.SourcePoint : pressure.Point;
                constrained = constrained.WithHandConstraint(new BodyHandConstraint(point, pressure.WeaponTargetLocal));
            }
            return constrained;
        }
        BodyConnection connection = running.Connection;
        if (connection == null) return frame;
        if (connection.Relation.Kind == BodyRelationKind.Deflect) return frame;
        if (connection.WeaponOnly)
            return frame.WithPoseConstraint(new BodyPoseConstraint(connection.PoseAge));
        if (connection.Relation.MoveTarget) return frame.WithRelativeAim(connection.FreeFrame);
        connection.FreeFrame = frame;
        float weight = connection.Active
            ? 1f
            : 1f - Mathf.SmoothStep(0f, 1f, (float)((time - connection.ReleasedAt) / .08d));
        return running.Cast.Plan.Body.Part.Contact == BodyContactPart.Hand
            ? frame.WithHandConstraint(new BodyHandConstraint(connection.Point, connection.Local, weight))
            : frame.WithPoseConstraint(new BodyPoseConstraint(connection.PoseAge, weight));
    }

    /// <summary>持续预算耗尽也构成共同拆步边界，不能在大倍率下多维持一整帧。</summary>
    /// <param name="running">本次来源。</param><param name="step">原步长。</param>
    private static float BodyConnectionStep(RunningCast running, float step)
    {
        BodyConnection connection = running.Connection;
        if (connection?.Active != true) return step;
        float scale = connection.Target.GetExtend()
            .ForceResponseScale(running.Source, running.InitialOwner.Damage.AttackerPowerLevel);
        return scale <= 0f ? step : Mathf.Min(step, Mathf.Max(.000001f, connection.Remaining * scale));
    }

    /// <summary>只在确认后的共同时间检查实际身体、预算和接触点；失效不搬回目标。</summary>
    /// <param name="time">已经确认的共同世界时间。</param>
    private static void UpdateBodyConnections(double time)
    {
        for (int i = 0; i < casts.Count; i++)
        {
            RunningCast owner = casts[i];
            BodyConnection connection = owner.Connection;
            if (connection == null) continue;
            if (connection.WeaponOnly)
            {
                UpdateWeaponConnection(owner, time);
                continue;
            }
            bool sourceLive = owner.Source.data?.id == owner.SourceId && owner.Source.isAlive();
            bool sourceChanged = sourceLive && (owner.Source.asset != connection.SourceAsset ||
                                                Mathf.Abs(SpatialActorPose.Height(owner.Source) -
                                                          connection.SourceHeight) > .00001f);
            if (!connection.Active)
            {
                // 归还过程仍引用原身体；变形后不能继续用旧腕点约束新骨长。
                if (sourceChanged || !sourceLive || time >= connection.ReleasedAt + .08d)
                {
                    owner.Connection = null;
                    if (sourceChanged) owner.Cast.BodyConstraintChanged();
                }

                continue;
            }

            Actor target = connection.Target;
            string reason = null;
            if (!owner.BodyControlled || owner.Cast.Finished || !sourceLive) reason = "OwnerBodyEnded";
            else if (sourceChanged) reason = "BodyChanged";
            else if (target.data?.id != connection.TargetId || !target.isAlive() || !target.hasHealth())
                reason = "TargetLost";
            else if (target.asset != connection.TargetAsset ||
                     Mathf.Abs(SpatialActorPose.Height(target) - connection.TargetHeight) > .00001f)
                reason = "BodyChanged";
            else if (connection.SurfaceBinding.ChangedReason is string changed) reason = changed;
            else if (target.isImmovable()) reason = "Immovable";
            else if (target.under_forces && !ConstructedActorImpulse.TryGetSurface(target, out _))
                reason = "ExternalForce";
            else if (Vector3.Distance(PhysicsFoot(target), connection.TargetFoot) > .0001f) reason = "ExternalMove";
            else if (time >= connection.Until - .0000001d) reason = "PhaseEnded";
            else
            {
                float scale = target.GetExtend()
                    .ForceResponseScale(owner.Source, owner.InitialOwner.Damage.AttackerPowerLevel);
                if (scale <= 0f) reason = "Resistance";
                else
                    connection.Remaining = Mathf.Max(0f,
                        connection.Remaining - (float)Math.Max(0d, time - connection.LastTime) / scale);
                if (reason == null && connection.Remaining <= .000001f) reason = "Exhausted";
                if (reason == null && Vector3.Distance(connection.SourcePoint, connection.Point) >
                    owner.LastSourceHeight * .0001f) reason = "OutOfReach";
                if (reason == null && connection.Relation.Kind == BodyRelationKind.Brace)
                {
                    BodyPose free = owner.Cast.Plan.Body.Sample(owner.Cast.Age, connection.FreeFrame);
                    Vector3 freePoint = free.EffectorPoint + free.EffectorRotation * connection.Local;
                    if (Vector3.Dot(freePoint - connection.Point, connection.Normal) < -connection.Radius)
                        reason = "Withdrawn";
                }
            }

            connection.LastTime = time;
            if (reason != null) ReleaseBodyConnection(owner, reason, fade: !sourceChanged, time: time);
        }
    }

    /// <summary>先归还实际目标凭据，再发布解除；不会释放后来取得的动作。</summary>
    /// <param name="owner">关系的来源。</param><param name="reason">实际原因。</param>
    /// <param name="fade">来源仍占用身体时是否归还手部。</param><param name="time">共同确认时间，空值使用当前逻辑时点。</param>
    private static void ReleaseBodyConnection(RunningCast owner, string reason, bool fade = true, double? time = null)
    {
        BodyConnection connection = owner.Connection;
        if (connection == null) return;
        if (connection.Relation.MoveTarget || connection.WeaponOnly) fade = false;
        if (!connection.Active)
        {
            if (!fade)
            {
                owner.Connection = null;
                owner.Cast.BodyConstraintChanged();
            }
            return;
        }

        connection.Active = false;
        if (connection.PassiveWeapon != null) passiveWeaponBodies.Remove(connection.TargetId);
        if (connection.Relation.MoveTarget) carriedBodies.Remove(connection.TargetId);
        connection.ReleasedAt = time ?? LogicTime;
        Actor target = connection.Target;
        bool inherit = connection.Relation.MoveTarget && connection.EnvironmentContact == null &&
            reason is "PhaseEnded" or "Exhausted" or "OwnerBodyEnded" or "OutOfReach";
        if (inherit && target.data?.id == connection.TargetId && target.isAlive() && target.hasHealth() &&
            !target.under_forces && !target.isImmovable() && connection.SurfaceBinding.ChangedReason == null)
            ConstructedActorImpulse.BeginBody(target, owner.Id, owner.SourceId, connection.TargetLease,
                connection.SurfaceBinding, connection.OriginalSurface, connection.Geometry, connection.Motion,
                connection.LinearVelocity, connection.AngularVelocity, connection.ReleasedAt, simulation.terrain,
                connection.ImpactSource);
        else connection.TargetLease?.Dispose();
        connection.TargetLease = null;
        if (connection.WeaponTarget?.WeaponConstraint == connection) connection.WeaponTarget.WeaponConstraint = null;
        connection.WeaponTarget?.Cast.BodyConstraintChanged();
        if (!connection.WeaponOnly && target.data?.id == connection.TargetId && target.isAlive() && target.hasHealth() &&
            !target.under_forces && !target.isFlying() && target.position_height > 0f && OnSurface(target))
            ConstructedActorImpulse.RestoreSurface(target, PhysicsFoot(target).z, simulation.terrain);
        owner.Cast.BodyConstraintChanged();
        if (!fade || !owner.BodyControlled) owner.Connection = null;
        BodyConnectionChanged?.Invoke(connection.Fact(ConstructedBodyConnectionState.Released, connection.ReleasedAt,
            reason));
    }

    /// <summary>确认目标正在被抓，再按施术归属或有效余力解除这一条关系。</summary>
    /// <param name="running">造成这次真实接触的施放。</param><param name="node">实际接触部位。</param>
    /// <param name="target">确实命中的人物。</param><param name="mode">编译后的解除方式。</param>
    /// <param name="strength">当前接触的实际衰减。</param>
    private static void TryReleaseGrab(RunningCast running, int node, Actor target, ConstructionGrabRelease mode,
        float strength)
    {
        CastAuthority caster = running.Owner(node);
        if (!caster.IsLive || !BodyControlState.HasControl(target.id, BodyOccupancy.Stance)) return;
        if (TryReleaseSupportGrip(running, node, target, mode, strength, caster)) return;
        foreach (RunningCast holder in casts)
        {
            BodyConnection link = holder.Connection;
            if (link?.Active != true || link.Relation.Kind != BodyRelationKind.Grab || link.TargetId != target.id ||
                link.Target != target || link.TargetLease?.IsHeld != true) continue;
            CastAuthority grabber = holder.InitialOwner;
            if (!grabber.IsLive) return;
            if (mode == ConstructionGrabRelease.Own)
            {
                if (grabber.Id != caster.Id || grabber.Actor != caster.Actor) return;
            }
            else
            {
                float force = (node < 0 ? running.Power : running.Cast.RemainingForce(node)) * strength;
                float effective = force * grabber.Actor.GetExtend().ForceResponseScale(caster.Actor,
                    caster.Damage.AttackerPowerLevel);
                if (effective < link.Relation.ControlStrength) return;
            }
            ReleaseBodyConnection(holder, "ReleasedByAbility");
            return;
        }
    }

    /// <summary>人物受力或被停顿时，同时解除其作为来源或目标的实际关系。</summary>
    /// <param name="actor">受到真实作用的人物。</param>
    private static void InterruptBodyConnections(Actor actor)
    {
        for (int i = 0; i < casts.Count; i++)
        {
            RunningCast running = casts[i];
            if (running.Connection?.Active == true &&
                (running.SourceId == actor.id || running.Connection.TargetId == actor.id))
                ReleaseBodyConnection(running, "Interrupted");
        }
    }
}

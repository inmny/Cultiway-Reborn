using System;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实触地后的同一身体，先消散滚动惯性，再连续回到站姿；预测不改变已确认状态。</summary>
internal sealed class BodyGroundRecovery
{
    /// <summary>同一真实身体、可见几何与原脚下。</summary>
    private readonly ConstructedActorImpulse.BodyFlight body;
    /// <summary>实际体高、几何半径、重力及两段历时。</summary>
    private readonly float height, radius, gravity, rolling, rising = .85f;
    /// <summary>同一次写入和完成比较共用已舍入的终点，避免舍入差使控制永不归还。</summary>
    private readonly float duration;
    /// <summary>只在共同时间确认后替换的运动。</summary>
    private State current;

    /// <summary>完整可复制预测，不持有游戏对象。</summary>
    private struct State
    {
        /// <summary>确认世界时间及已经完成的地面过程。</summary>
        internal double Time;
        /// <summary>地面过程进度、原承托高度与实际下落速度。</summary>
        internal float Phase, Support, FallingSpeed;
        /// <summary>同一原几何的刚性变换。</summary>
        internal BodySurfaceTransform Transform;
        /// <summary>实际剩余平面、角向惯性，以及确认小段的中心和角速度。</summary>
        internal Vector3 Drift, Spin, Velocity, Angular;
        /// <summary>开始起身时的脚下与朝向。</summary>
        internal Vector3 Foot;
        /// <summary>起身的实际起点和直立终点。</summary>
        internal Quaternion RiseFrom, RiseTo;
        /// <summary>承托消失后暂停起身，继续真实下落。</summary>
        internal bool Airborne;
    }

    /// <summary>从真实接触接收已经确认的同一姿态和速度。</summary>
    /// <param name="body">仍持有原控制的身体。</param><param name="hit">真实地面或屋面接触。</param>
    /// <param name="time">共同接触时间。</param><param name="stopped">原贴地时长已结束，不能增加剩余位移。</param>
    internal BodyGroundRecovery(ConstructedActorImpulse.BodyFlight body, in BodySurfaceHit hit, double time, bool stopped = false)
    {
        this.body = body; height = body.Basis.Height;
        foreach (Vector3 point in body.Geometry.Points) radius = Mathf.Max(radius, Vector3.Distance(point, body.Geometry.Center));
        gravity = .3f * body.Motion.MassGravity * body.Motion.MassGravity;
        Vector3 velocity = stopped ? Vector3.zero : body.Flight.Arc.Speed(time - body.Motion.StartedAt);
        velocity -= hit.Normal * Vector3.Dot(velocity, hit.Normal);
        Vector3 spin = (stopped ? Vector3.zero : body.Flight.AngularVelocity) + Vector3.Cross(Vector3.forward, velocity) / Mathf.Max(height * .3f, .05f);
        spin = Vector3.ClampMagnitude(spin, 20f);
        rolling = Mathf.Clamp(velocity.magnitude * .06f + spin.magnitude * .012f, .12f, .55f);
        duration = rolling + rising;
        current = new State { Time = time, Support = hit.Point.z, Transform = body.Surface.RigidTransform,
            Drift = velocity, Spin = spin, Velocity = velocity, Angular = spin };
    }

    /// <summary>只在已经贴地且过程完整结束时归还，不能在空中站起。</summary>
    internal bool Finished => !current.Airborne && current.Phase >= duration;
    /// <summary>起身阶段开始以后才报告，空中不冒充起身。</summary>
    internal bool Rising => !current.Airborne && current.Phase >= rolling;
    /// <summary>已经确认的实际中心速度和角速度。</summary>
    internal Vector3 Velocity => current.Velocity;
    /// <summary>已经确认的实际角速度。</summary>
    internal Vector3 Angular => current.Angular;

    /// <summary>读取任一共同时间的实际表面，绘制仍只使用最终确认结果。</summary>
    /// <param name="time">目标世界时间。</param><param name="terrain">本轮共同地表。</param>
    internal BodySurfacePose At(double time, AbilityHeightField terrain) => Surface(Calculate(time, terrain), terrain);

    /// <summary>真实步骤确认以后才推进本次恢复。</summary>
    /// <param name="time">确认世界时间。</param><param name="terrain">本轮共同地表。</param>
    internal BodySurfacePose Commit(double time, AbilityHeightField terrain)
    { current = Calculate(time, terrain); return Surface(current, terrain); }

    /// <summary>同一关节和整体运动生成实际表面，不在绘制时推进动画。</summary>
    /// <param name="value">已确认或预测状态。</param><param name="terrain">同一地表。</param>
    private BodySurfacePose Surface(in State value, AbilityHeightField terrain) => body.Rig == null
        ? body.Basis.Transformed(value.Transform) : body.Rig.Surface(value.Transform, Bones(value, terrain), terrain, value.Support + height * .2f);

    /// <summary>滚动期间保持原肢体，起身才分段建立真实手足支撑。</summary>
    /// <param name="value">同一恢复状态。</param><param name="terrain">实际地表。</param>
    private BodySurfaceTransform[] Bones(in State value, AbilityHeightField terrain) => body.Rig.Sample(value.Transform,
        value.RiseTo, Mathf.Clamp01((value.Phase - rolling) / rising), terrain);

    /// <summary>再受击交接当前关节与实际形状，不恢复旧冻结姿态。</summary>
    /// <param name="surface">刚刚确认的实际身体。</param>
    internal IBodyRecoveryRig Rebase(BodySurfacePose surface) => body.Rig.Rebase(surface, Bones(current, body.Motion.Terrain));

    /// <summary>滚动和起身的保守范围包含中间旋转，不只包住两端。</summary>
    /// <param name="time">预测末端。</param>
    internal Bounds Bounds(double time)
    {
        float seconds = Mathf.Max(0f, (float)(time - current.Time));
        float reach = radius * 3f + current.Drift.magnitude * seconds +
            (current.Airborne ? Mathf.Abs(current.FallingSpeed) * seconds + .5f * gravity * seconds * seconds : 0f);
        return new Bounds(current.Transform.Point, Vector3.one * (reach * 2f + .0001f));
    }

    /// <summary>先计算本段惯性或起身，再保持真实承托并扫过实际环境，最后保存小段速度。</summary>
    /// <param name="time">请求的共同时间。</param><param name="terrain">本轮地表。</param>
    private State Calculate(double time, AbilityHeightField terrain)
    {
        State value = current;
        if (time <= value.Time) return value;
        var obstacles = ConstructedAbilityService.CaptureBodyEnvironment(Bounds(time), terrain);
        while (value.Time < time)
        {
            float seconds = (float)Math.Min(.01d, time - value.Time);
            State next = value;
            next.Time += seconds;
            Vector3 point = value.Transform.Point;
            Quaternion rotation = value.Transform.Rotation;
            if (value.Airborne)
            {
                point += value.Drift * seconds;
                point.z += value.FallingSpeed * seconds - .5f * gravity * seconds * seconds;
                next.FallingSpeed -= gravity * seconds;
            }
            else if (value.Phase < rolling)
            {
                float factor = Mathf.Exp(-6f * seconds);
                point += value.Drift * ((1f - factor) / 6f);
                float angle = value.Spin.magnitude * ((1f - factor) / 6f);
                if (angle > 0f) rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, value.Spin.normalized) * rotation;
                next.Drift *= factor; next.Spin *= factor;
                next.Phase = Mathf.Min(rolling, value.Phase + seconds);

            }
            else
            {
                next.Phase = Mathf.Min(duration, value.Phase + seconds);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((next.Phase - rolling) / (rising * .35f)));
                rotation = Quaternion.Slerp(value.RiseFrom, value.RiseTo, t);
                Vector3 root = value.Foot;
                if (body.Rig != null) root.z = Mathf.Lerp(value.Foot.z, value.Support, t);
                point = root + rotation * (body.Geometry.Center - body.Basis.Foot);
                next.Drift = next.Spin = Vector3.zero;
            }

            var requested = new BodySurfaceTransform(body.Geometry.Center, point, rotation);
            float lift = float.NegativeInfinity, support = value.Support;
            Matrix4x4 map = requested.Matrix;
            if (body.Rig != null)
            {
                State sampled = next; sampled.Transform = requested;
                lift = body.Rig.Lift(Bones(sampled, terrain), terrain, value.Support + height * .2f, out support);
            }
            else foreach (Vector3 original in body.Geometry.Points)
            {
                Vector3 world = map.MultiplyPoint3x4(original);
                float floor = BodyLocomotion.Floor(terrain, world, value.Support + height * .2f, height * .04f);
                if (floor - world.z <= lift) continue;
                lift = floor - world.z; support = floor;
            }
            if (!value.Airborne && support > value.Support + height * .2f)
            {
                requested = value.Transform;
                next.Drift = next.Spin = Vector3.zero;
                if (value.Phase >= rolling) next.Phase = value.Phase;
            }
            else if (!value.Airborne && support < value.Support - height * .2f)
            {
                // 整体离开承托时不吸到低处，也不在空中继续起身。
                next.Airborne = true; next.Phase = value.Phase; next.FallingSpeed = 0f;
                requested = value.Transform;
            }
            else if (!value.Airborne || lift >= 0f)
            {
                point = requested.Point + Vector3.forward * lift;
                requested = new BodySurfaceTransform(body.Geometry.Center, point, requested.Rotation);
                // 手足向真实地面求解后，整体校正会再次改变可达范围，有限次重新核对同一顶点。
                if (body.Rig != null)
                    for (int pass = 0; pass < 3; pass++)
                    {
                        State aligned = next; aligned.Transform = requested;
                        float correction = body.Rig.Lift(Bones(aligned, terrain), terrain, value.Support + height * .2f, out support);
                        if (Mathf.Abs(correction) < .00001f) break;
                        requested = new BodySurfaceTransform(body.Geometry.Center, requested.Point + Vector3.forward * correction, requested.Rotation);
                    }
                next.Airborne = false; next.FallingSpeed = 0f; next.Support = support;
            }

            // 地面已按同一真实顶点维持承托，三角面扫掠这里只检查其余环境。
            State sampledNext = next; sampledNext.Transform = requested;
            bool blocked = body.Rig != null ? body.Rig.Blocked(Bones(value, terrain), Bones(sampledNext, terrain), terrain, obstacles, value.Support + height * .2f)
                : BodySurfaceSweep.Find(body.Geometry, value.Transform, requested, terrain, obstacles, out _, ground: false);
            if (blocked)
            {
                requested = value.Transform;
                next.Drift = next.Spin = Vector3.zero;
                if (value.Phase >= rolling) next.Phase = value.Phase;
            }
            if (value.Phase < rolling && next.Phase >= rolling)
            {
                next.RiseFrom = requested.Rotation;
                next.Foot = requested.Matrix.MultiplyPoint3x4(body.Basis.Foot);
                Vector3 reference = body.Rig?.Forward ?? new Vector3(Mathf.Cos(body.Basis.Heading), Mathf.Sin(body.Basis.Heading), 0f);
                Vector3 forward = requested.Rotation * reference;
                float heading = forward.x * forward.x + forward.y * forward.y > .000001f
                    ? Mathf.Atan2(forward.y, forward.x) - Mathf.Atan2(reference.y, reference.x) : 0f;
                next.RiseTo = Quaternion.AngleAxis(heading * Mathf.Rad2Deg, Vector3.forward);
            }
            next.Transform = requested;
            (next.Velocity, next.Angular) = BodyRigidFlight.Velocity(body.Geometry.Center, value.Transform, next.Transform, seconds);
            value = next;
        }
        return value;
    }
}

using System;
using System.Collections.Concurrent;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>只接管新构造真正施加的受力；原版与其他玩法仍保留各自运动。</summary>
internal static partial class ConstructedActorImpulse
{
    /// <summary>弱引用不延长人物寿命；每个人物的原版更新只访问自己的运动。</summary>
    private static ConcurrentDictionary<long, Motion> motions = new();

    /// <summary>主线程发布的最新不可变地表，人物更新不调用显示采样器。</summary>
    private static volatile AbilityHeightField currentTerrain;

    /// <summary>一次已受理的受力，地表只读取主线程发布的不可变数字。</summary>
    internal sealed class Motion
    {
        /// <summary>只在采集地表时临时取得人物，不用运动记录延长人物寿命。</summary>
        internal WeakReference<Actor> Actor;

        /// <summary>防止对象池换人后沿用上一人的受力。</summary>
        internal long ActorId;

        /// <summary>当前碰撞段的完整轨迹。</summary>
        internal ImpulseArc Arc;

        /// <summary>本次接触确认时的地表。</summary>
        internal AbilityHeightField Terrain;

        /// <summary>当前轨迹起点及最后处理的世界时间。</summary>
        internal double StartedAt, SampledAt;

        /// <summary>贴地运动的结束世界时间，零表示腾起轨迹。</summary>
        internal double SlideUntil;

        /// <summary>上次写入原版的速度、高度及位置，用于识别其他玩法的接管。</summary>
        internal Vector3 ExpectedVelocity;

        /// <summary>原版速度倍率、上次离地高度和计算轨迹时的质量重力。</summary>
        internal float SpeedScale, ExpectedHeight, MassGravity;

        /// <summary>上次写入的地图位置。</summary>
        internal Vector2 ExpectedPosition;

        /// <summary>实际离手身体，空值沿用原直立受力。</summary>
        internal BodyFlight Body;

        /// <summary>已经落在模型表面，沿用同一运动记录维护承托，走出边缘后再下落。</summary>
        internal bool Supported;

        /// <summary>实际承托的世界高度，不随脚下地表变化或普通平移重复叠加。</summary>
        internal float SurfaceZ;
    }

    /// <summary>受力入口明确成功后才登记，不把速度差为零误认为未受理。</summary>
    /// <param name="actor">真实目标。</param><param name="terrain">已发布地表。</param>
    internal static void Begin(Actor actor, AbilityHeightField terrain) => BeginMotion(actor, terrain, 0f);

    /// <summary>已经经过受力抗性后启动贴地运动，不通过微小腾起伪造搬运。</summary>
    /// <param name="actor">已受理的真实目标。</param><param name="seconds">持续世界秒数。</param>
    /// <param name="terrain">共同地表。</param>
    internal static void BeginSlide(Actor actor, float seconds, AbilityHeightField terrain) =>
        BeginMotion(actor, terrain, seconds);

    /// <summary>已经通过抗性与来源检查的世界速度，允许上挑、空中下压及精确贴地推拉。</summary>
    /// <param name="actor">实际人物。</param><param name="velocity">同一世界坐标中的真实速度。</param>
    /// <param name="seconds">贴地持续秒数，零使用重力轨迹。</param><param name="terrain">已冻结地表。</param>
    internal static void BeginDirected(Actor actor, Vector3 velocity, float seconds, AbilityHeightField terrain)
    {
        float massGravity = Mathf.Min(actor.stats["mass"] * SimGlobals.m.gravity, 20f);
        Vector3 position = new(actor.current_position.x, actor.current_position.y,
            Ground(terrain, actor.current_position) + actor.position_height);
        float height = Cultiway.Content.Combat.SpatialActorPose.Height(actor);
        // 空中保持原本的竖直运动，贴地参数不能把人物瞬间吸回地面。
        if (seconds > 0f && (velocity.z != 0f || position.z >
                Floor(terrain, position, position.z + .0001f, height) + .0001f)) seconds = 0f;
        actor.stopMovement();
        actor.velocity_speed = 1f;
        actor.velocity = new Vector3(velocity.x, velocity.y, massGravity == 0f ? 0f : velocity.z / massGravity);
        actor.under_forces = true;
        BeginMotion(actor, terrain, seconds, velocity);
    }

    /// <summary>同一受力的实际世界速度，叠加新作用时不丢掉已经存在的腾起或下落。</summary>
    /// <param name="actor">实际人物。</param>
    internal static Vector3 Velocity(Actor actor)
    {
        if (motions.TryGetValue(actor.id, out Motion motion) && Matches(actor, motion) && !motion.Supported)
            return motion.Body?.RootVelocity(motion.SampledAt) ?? motion.Arc.Speed(motion.SampledAt - motion.StartedAt);
        if (!actor.under_forces) return Vector3.zero;
        float gravity = Mathf.Min(actor.stats["mass"] * SimGlobals.m.gravity, 20f);
        return new Vector3(actor.velocity.x * actor.velocity_speed, actor.velocity.y * actor.velocity_speed,
            actor.velocity.z * gravity);
    }

    /// <summary>先固定真实起点与速度，再一次性发布完整运动。</summary>
    /// <param name="actor">已受理的真实目标。</param><param name="terrain">共同地表。</param>
    /// <param name="seconds">大于零时沿地运动，否则腾起。</param>
    /// <param name="worldVelocity">明确的世界速度；空值沿用原版速度换算。</param>
    private static void BeginMotion(Actor actor, AbilityHeightField terrain, float seconds,
        Vector3? worldVelocity = null)
    {
        actor.stopMovement();
        if (seconds > 0f) actor.velocity_speed = 1f;
        float massGravity = seconds > 0f ? 0f : Mathf.Min(actor.stats["mass"] * SimGlobals.m.gravity, 20f);
        double now = World.world.getCurWorldTime();
        motions.TryGetValue(actor.id, out Motion prior);
        prior?.Body?.Lease.Dispose();
        var motion = new Motion
        {
            Actor = new WeakReference<Actor>(actor), ActorId = actor.id, Terrain = terrain, StartedAt = now,
            SampledAt = now,
            SlideUntil = seconds > 0f ? now + seconds : 0d,
            SpeedScale = actor.velocity_speed, MassGravity = massGravity, ExpectedVelocity = actor.velocity,
            ExpectedHeight = actor.position_height, ExpectedPosition = actor.current_position,
            Arc = new ImpulseArc(new Vector3(actor.current_position.x, actor.current_position.y,
                    Ground(terrain, actor.current_position) + actor.position_height),
                worldVelocity ?? new Vector3(actor.velocity.x * actor.velocity_speed,
                    actor.velocity.y * actor.velocity_speed,
                    actor.velocity.z * massGravity), .3f * massGravity * massGravity)
        };
        motions[actor.id] = motion;
        if (prior?.Body != null) CancelBodyOwnership(prior, "ExternalForce");
    }

    /// <summary>原版下落和受力更新不能对同一目标重复施加重力。</summary>
    /// <param name="actor">正在更新的人物。</param>
    internal static bool Owns(Actor actor) =>
        motions.TryGetValue(actor.id, out Motion motion) && Matches(actor, motion);

    /// <summary>只采集已站稳且仍实际占用的绝对高度，临时受力和速度不进入长期存档。</summary>
    /// <param name="actor">实际人物。</param><param name="z">已确认的世界高度。</param>
    internal static bool TryGetSurface(Actor actor, out float z)
    {
        z = 0;
        if (!motions.TryGetValue(actor.id, out Motion motion) || !motion.Supported ||
            !Matches(actor, motion)) return false;
        z = motion.SurfaceZ;
        return true;
    }

    /// <summary>正式恢复只重建已站稳的承托，不恢复旧动作、速度或实体编号。</summary>
    /// <param name="actor">已经恢复原版身份和位置的人物。</param><param name="z">当前实际表面高度。</param>
    /// <param name="terrain">本轮已经采集的共同地表。</param>
    internal static void RestoreSurface(Actor actor, float z, AbilityHeightField terrain)
    {
        actor.velocity = Vector3.zero;
        actor.under_forces = false;
        actor.position_height = Mathf.Max(0, z - Ground(terrain, actor.current_position));
        motions[actor.id] = new Motion
        {
            Actor = new WeakReference<Actor>(actor), ActorId = actor.id, Terrain = terrain,
            Supported = true, SurfaceZ = z, ExpectedHeight = actor.position_height,
            ExpectedPosition = actor.current_position, ExpectedVelocity = actor.velocity,
            SpeedScale = actor.velocity_speed
        };
    }

    /// <summary>外部重新施力、搬运或对象池换人后，立即交还运动。</summary>
    /// <param name="actor">当前人物。</param><param name="motion">本次写入记录。</param>
    private static bool Matches(Actor actor, Motion motion) =>
        actor.data?.id == motion.ActorId && motion.Actor.TryGetTarget(out Actor expected) &&
        ReferenceEquals(expected, actor) &&
        (motion.Supported ? !actor.under_forces && !actor.isFlying() : actor.under_forces) &&
        actor.velocity == motion.ExpectedVelocity &&
        actor.velocity_speed == motion.SpeedScale &&
        (motion.Supported || actor.current_position == motion.ExpectedPosition) &&
        actor.position_height == motion.ExpectedHeight;

    /// <summary>原版下落之前维护已取得的实际承托；只维护构造落下的人物，不接管全世界人物。</summary>
    /// <param name="actor">当前人物。</param>
    internal static bool UpdateSurface(Actor actor)
    {
        // 先核对原控制，再跟随普通水平移动；表面消失或离开边缘时交回同一受力过程。
        if (!motions.TryGetValue(actor.id, out Motion motion) || !motion.Supported) return false;
        if (!Matches(actor, motion) || !actor.isAlive())
        {
            motions.TryRemove(actor.id, out _);
            CancelBodyOwnership(motion, actor.isAlive() && actor.data?.id == motion.ActorId ? "ExternalControl" : "TargetLost");
            return false;
        }

        if (World.world.isPaused()) return true;
        if (currentTerrain != null) motion.Terrain = currentTerrain;
        float height = Cultiway.Content.Combat.SpatialActorPose.Height(actor);
        float step = height * .2f;
        Vector3 point = new(actor.current_position.x, actor.current_position.y, motion.SurfaceZ);
        float ground = Ground(motion.Terrain, point);
        float floor = BodyLocomotion.Floor(motion.Terrain, point, motion.SurfaceZ + step, height * .16f,
            height * 1.04f);
        if (floor > ground + .0001f && floor >= motion.SurfaceZ - step)
        {
            motion.SurfaceZ = floor;
            actor.position_height = floor - ground;
            motion.ExpectedHeight = actor.position_height;
            motion.ExpectedPosition = actor.current_position;
            actor.dirty_current_tile = true;
            return true;
        }

        actor.position_height = Mathf.Max(0, motion.SurfaceZ - ground);
        actor.velocity = Vector3.zero;
        actor.velocity_speed = 1f;
        actor.under_forces = true;
        Begin(actor, motion.Terrain);
        return true;
    }

    /// <summary>一次世界时点只推进一次；原版倍速循环和重复回调不会多走距离。</summary>
    /// <param name="actor">原版正在推进的人物。</param>
    /// <returns>是否已经处理，应跳过原版受力。</returns>
    internal static bool Update(Actor actor)
    {
        // 先核对当前控制，再处理束缚与时间，最后逐段推进并发布真实位置。
        if (!motions.TryGetValue(actor.id, out Motion motion)) return false;
        if (!Matches(actor, motion) || !actor.isAlive())
        {
            motions.TryRemove(actor.id, out _);
            CancelBodyOwnership(motion, actor.isAlive() && actor.data?.id == motion.ActorId ? "ExternalControl" : "TargetLost");
            return false;
        }

        // 离手身体由共同接触步骤推进；原版回调只让位，不能提前移动到本帧终点。
        if (motion.Body != null) return true;
        if (motion.Supported) return UpdateSurface(actor);

        if (World.world.isPaused()) return true;
        if (currentTerrain != null) motion.Terrain = currentTerrain;
        if (actor.isImmovable() && (motion.Arc.Velocity.x != 0f || motion.Arc.Velocity.y != 0f))
        {
            HoldHorizontal(actor);
            if (!actor.under_forces) return true;
        }

        double now = World.world.getCurWorldTime();
        if (now <= motion.SampledAt) return true;

        // 先按共同时间求位置；穿越地图格时分段检查原版阻挡，下降时截在真实地表。
        bool sliding = motion.SlideUntil > 0d;
        double from = motion.SampledAt - motion.StartedAt,
            until = (sliding ? Math.Min(now, motion.SlideUntil) : now) - motion.StartedAt;
        float cell = motion.Terrain == null
            ? 1f
            : Mathf.Min(1f, motion.Terrain.Size.x / motion.Terrain.Width,
                motion.Terrain.Size.y / motion.Terrain.Height);
        float horizontal = new Vector2(motion.Arc.Velocity.x, motion.Arc.Velocity.y).magnitude;
        double stride = horizontal > 0f ? cell * .25d / horizontal : double.PositiveInfinity;
        if (BodyLocomotion.SurfaceHeight != null) stride = Math.Min(stride, .02d);
        float bodyHeight = Cultiway.Content.Combat.SpatialActorPose.Height(actor);
        while (from < until)
        {
            double next = Math.Min(until, from + stride);
            Vector3 previous = motion.Arc.Position(from);
            bool landed;
            if (sliding) landed = false;
            else if (motion.Terrain == null && BodyLocomotion.SurfaceHeight == null)
            {
                double landing = motion.Arc.LandingAt(0f);
                landed = next >= landing;
                if (landed) next = landing;
            }
            else
            {
                Vector3 end = motion.Arc.Position(next);
                landed = end.z <= Floor(motion.Terrain, end, Mathf.Max(previous.z, end.z) + .0001f, bodyHeight);
                if (landed)
                {
                    double low = from, high = next;
                    for (int i = 0; i < 24; i++)
                    {
                        double mid = (low + high) * .5d;
                        Vector3 point = motion.Arc.Position(mid);
                        if (point.z <= Floor(motion.Terrain, point, Mathf.Max(previous.z, point.z) + .0001f,
                                bodyHeight)) high = mid;
                        else low = mid;
                    }

                    next = high;
                }
            }

            Vector3 position = motion.Arc.Position(next), speed = motion.Arc.Speed(next);
            bool ceilingHit = false;
            if (!sliding && position.z > previous.z && BodyLocomotion.CeilingHeight != null)
            {
                float ceiling = BodyLocomotion.CeilingHeight(position, bodyHeight * .16f,
                    previous.z + bodyHeight * 1.04f);
                if (position.z + bodyHeight * 1.04f >= ceiling)
                {
                    position.z = ceiling - bodyHeight * 1.04f - .00001f;
                    speed.z = 0f;
                    ceilingHit = true;
                }
            }

            if (landed)
                position.z = Mathf.Max(position.z,
                    Floor(motion.Terrain, position, previous.z + .0001f, bodyHeight));
            actor.position_height =
                Mathf.Max(0f, (sliding ? previous.z : position.z) - Ground(motion.Terrain, position));
            actor.velocity.z = motion.MassGravity == 0f ? motion.ExpectedVelocity.z : speed.z / motion.MassGravity;
            Vector2 requested = position, allowed = actor.checkVelocityAgainstBlock(requested);
            if (!sliding && allowed != requested)
            {
                float ground = Ground(motion.Terrain, allowed);
                actor.position_height = Mathf.Max(0f, position.z - ground);
                landed = position.z <= Floor(motion.Terrain, new Vector3(allowed.x, allowed.y, position.z),
                    previous.z + .0001f, bodyHeight);
            }

            actor.current_position = allowed;
            actor.setCurrentTile(Toolbox.getTileAt(allowed.x, allowed.y));
            actor.dirty_current_tile = true;
            bool lostSupport = false, supportChanged = false;
            if (sliding)
            {
                float floor = Floor(motion.Terrain, new Vector3(allowed.x, allowed.y, previous.z),
                    previous.z + bodyHeight * .2f, bodyHeight);
                lostSupport = floor < previous.z - bodyHeight * .2f;
                float actual = lostSupport ? previous.z : floor;
                supportChanged = Mathf.Abs(actual - position.z) > .000001f;
                position.z = actual;
                actor.position_height = Mathf.Max(0f, actual - Ground(motion.Terrain, allowed));
            }

            if (lostSupport)
            {
                // 推出桥面或屋面后沿当前速度下落，不吸回脚下原版地面。
                motion.SlideUntil = 0d;
                motion.MassGravity = Mathf.Min(actor.stats["mass"] * SimGlobals.m.gravity, 20f);
                motion.Arc = new ImpulseArc(new Vector3(allowed.x, allowed.y, position.z),
                    new Vector3(speed.x, speed.y, 0f), .3f * motion.MassGravity * motion.MassGravity);
                motion.StartedAt += next;
                until = now - motion.StartedAt;
                from = 0d;
                sliding = false;
                motion.ExpectedVelocity = actor.velocity;
                continue;
            }

            if (landed)
            {
                actor.stopForce();
                float offset = Mathf.Max(0, position.z - Ground(motion.Terrain, allowed));
                actor.position_height = offset;
                if (offset > .0001f)
                {
                    motion.Supported = true;
                    motion.SurfaceZ = position.z;
                    motion.ExpectedVelocity = actor.velocity;
                    motion.ExpectedHeight = offset;
                    motion.ExpectedPosition = actor.current_position;
                }
                else motions.TryRemove(actor.id, out _);

                return true;
            }

            if (allowed != requested || ceilingHit || supportChanged)
            {
                motion.Arc = new ImpulseArc(
                    new Vector3(allowed.x, allowed.y, position.z),
                    new Vector3(actor.velocity.x * motion.SpeedScale, actor.velocity.y * motion.SpeedScale, speed.z),
                    motion.Arc.Gravity);
                motion.StartedAt += next;
                until -= next;
                from = 0d;
            }
            else from = next;

            motion.ExpectedVelocity = actor.velocity;
        }

        if (sliding && now >= motion.SlideUntil)
        {
            FinishSlide(actor);
            return true;
        }

        motion.SampledAt = now;
        motion.ExpectedVelocity = actor.velocity;
        motion.ExpectedPosition = actor.current_position;
        motion.ExpectedHeight = actor.position_height;
        return true;
    }

    /// <summary>束缚取消水平运动；已离地的人物保留竖直速度，不被凭空拉回地面。</summary>
    /// <param name="actor">真正受束缚的目标。</param>
    internal static void HoldHorizontal(Actor actor)
    {
        bool owned = motions.TryGetValue(actor.id, out Motion motion) && Matches(actor, motion);
        actor.velocity.x = actor.velocity.y = 0f;
        if (!owned) return;
        if (motion.SlideUntil > 0d)
        {
            FinishSlide(actor);
            return;
        }

        double age = motion.SampledAt - motion.StartedAt;
        motion.Arc = new ImpulseArc(new Vector3(actor.current_position.x, actor.current_position.y,
            motion.Arc.Position(age).z), new Vector3(0f, 0f, motion.Arc.Speed(age).z), motion.Arc.Gravity);
        motion.StartedAt = motion.SampledAt;
        motion.ExpectedVelocity = actor.velocity;
    }

    /// <summary>贴地结束只解除运动，不冒充一次腾起后的落地。</summary>
    /// <param name="actor">沿地运动的目标。</param>
    private static void FinishSlide(Actor actor)
    {
        actor.velocity = Vector3.zero;
        actor.velocity_speed = 0f;
        actor.under_forces = false;
        if (actor.position_height > .0001f && motions.TryGetValue(actor.id, out Motion motion))
        {
            motion.Supported = true;
            motion.SurfaceZ = Ground(motion.Terrain, actor.current_position) + actor.position_height;
            motion.ExpectedHeight = actor.position_height;
            motion.ExpectedPosition = actor.current_position;
            motion.ExpectedVelocity = actor.velocity;
            motion.SpeedScale = actor.velocity_speed;
        }
        else motions.TryRemove(actor.id, out _);
    }

    /// <summary>主线程只采集仍实际受力的弱引用，同时移除外部接管、死亡或换人的旧运动。</summary>
    internal static Rect? ActiveBounds()
    {
        Rect? area = null;
        var current = motions;
        foreach (var entry in current)
        {
            Motion motion = entry.Value;
            if (!motion.Actor.TryGetTarget(out Actor actor) || !actor.isAlive() || !Matches(actor, motion))
            {
                current.TryRemove(entry.Key, out _);
                CancelBodyOwnership(motion, "ExternalControl");
                continue;
            }

            Vector2 point = actor.current_position;
            if (motion.Body != null)
            {
                Bounds flight = motion.Body.Ground?.Bounds(Math.Max(motion.SampledAt, World.world.getCurWorldTime())) ??
                    motion.Body.Flight.Bounds(motion.Body.Geometry, motion.SampledAt - motion.StartedAt,
                        Math.Max(motion.SampledAt, World.world.getCurWorldTime()) - motion.StartedAt);
                if (!area.HasValue) area = new Rect(flight.min, flight.size);
                else area = Rect.MinMaxRect(Mathf.Min(area.Value.xMin, flight.min.x), Mathf.Min(area.Value.yMin, flight.min.y),
                    Mathf.Max(area.Value.xMax, flight.max.x), Mathf.Max(area.Value.yMax, flight.max.y));
            }
            if (!area.HasValue) area = new Rect(point, Vector2.zero);
            else
            {
                Rect bounds = area.Value;
                bounds.xMin = Mathf.Min(bounds.xMin, point.x);
                bounds.xMax = Mathf.Max(bounds.xMax, point.x);
                bounds.yMin = Mathf.Min(bounds.yMin, point.y);
                bounds.yMax = Mathf.Max(bounds.yMax, point.y);
                area = bounds;
            }
        }

        return area;
    }

    /// <summary>世界形体采集完成后发布同一份地表数字。</summary>
    /// <param name="terrain">本世界最新地表。</param>
    internal static void PublishTerrain(AbilityHeightField terrain) => currentTerrain = terrain;

    /// <summary>地表数字不会随显示视角变化。</summary>
    /// <param name="terrain">本次受力的地表。</param><param name="position">地图位置。</param>
    private static float Ground(AbilityHeightField terrain, Vector2 position) =>
        terrain?.Sample(position.x, position.y) ?? 0f;

    /// <summary>下落扫掠只读取上一步脚下以下的实际表面，不隔空吸到上方屋顶。</summary>
    /// <param name="terrain">共同地表。</param><param name="point">当前脚下。</param>
    /// <param name="maximum">本段最高可接触表面。</param><param name="height">实际体高。</param>
    private static float Floor(AbilityHeightField terrain, Vector3 point, float maximum, float height) =>
        BodyLocomotion.Floor(terrain, point, maximum, height * .16f, height * 1.04f);

    /// <summary>世界清理后旧人物不能继续取得运动。</summary>
    internal static void Clear()
    {
        ConcurrentDictionary<long, Motion> previous = motions;
        motions = new ConcurrentDictionary<long, Motion>();
        currentTerrain = null;
        foreach (Motion motion in previous.Values) CancelBodyOwnership(motion, "WorldChanged");
    }
}

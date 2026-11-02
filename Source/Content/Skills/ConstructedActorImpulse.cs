using System;
using System.Runtime.CompilerServices;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>只接管新构造真正施加的受力；原版与其他玩法仍保留各自运动。</summary>
internal static class ConstructedActorImpulse
{
    /// <summary>弱引用不延长人物寿命；每个人物的原版更新只访问自己的运动。</summary>
    private static ConditionalWeakTable<Actor, Motion> motions = new();

    /// <summary>一次已受理的受力，地表只读取主线程发布的不可变数字。</summary>
    private sealed class Motion
    {
        /// <summary>防止对象池换人后沿用上一人的受力。</summary>
        internal long ActorId;

        /// <summary>当前碰撞段的完整轨迹。</summary>
        internal ImpulseArc Arc;

        /// <summary>本次接触确认时的地表。</summary>
        internal AbilityHeightField Terrain;

        /// <summary>当前轨迹起点及最后处理的世界时间。</summary>
        internal double StartedAt, SampledAt;

        /// <summary>上次写入原版的速度、高度及位置，用于识别其他玩法的接管。</summary>
        internal Vector3 ExpectedVelocity;

        /// <summary>原版速度倍率、上次离地高度和计算轨迹时的质量重力。</summary>
        internal float SpeedScale, ExpectedHeight, MassGravity;

        /// <summary>上次写入的地图位置。</summary>
        internal Vector2 ExpectedPosition;
    }

    /// <summary>受力入口明确成功后才登记，不把速度差为零误认为未受理。</summary>
    /// <param name="actor">真实目标。</param><param name="terrain">已发布地表。</param>
    internal static void Begin(Actor actor, AbilityHeightField terrain)
    {
        float massGravity = Mathf.Min(actor.stats["mass"] * SimGlobals.m.gravity, 20f);
        double now = World.world.getCurWorldTime();
        var motion = new Motion
        {
            ActorId = actor.id, Terrain = terrain, StartedAt = now, SampledAt = now,
            SpeedScale = actor.velocity_speed, MassGravity = massGravity, ExpectedVelocity = actor.velocity,
            ExpectedHeight = actor.position_height, ExpectedPosition = actor.current_position,
            Arc = new ImpulseArc(new Vector3(actor.current_position.x, actor.current_position.y,
                    Ground(terrain, actor.current_position) + actor.position_height),
                new Vector3(actor.velocity.x * actor.velocity_speed, actor.velocity.y * actor.velocity_speed,
                    actor.velocity.z * massGravity), .3f * massGravity * massGravity)
        };
        motions.Remove(actor);
        motions.Add(actor, motion);
    }

    /// <summary>原版下落和受力更新不能对同一目标重复施加重力。</summary>
    /// <param name="actor">正在更新的人物。</param>
    internal static bool Owns(Actor actor) => motions.TryGetValue(actor, out Motion motion) && Matches(actor, motion);

    /// <summary>外部重新施力、搬运或对象池换人后，立即交还运动。</summary>
    /// <param name="actor">当前人物。</param><param name="motion">本次写入记录。</param>
    private static bool Matches(Actor actor, Motion motion) => actor.data?.id == motion.ActorId && actor.under_forces &&
                                                               actor.velocity == motion.ExpectedVelocity &&
                                                               actor.velocity_speed == motion.SpeedScale &&
                                                               actor.current_position == motion.ExpectedPosition &&
                                                               actor.position_height == motion.ExpectedHeight;

    /// <summary>一次世界时点只推进一次；原版倍速循环和重复回调不会多走距离。</summary>
    /// <param name="actor">原版正在推进的人物。</param>
    /// <returns>是否已经处理，应跳过原版受力。</returns>
    internal static bool Update(Actor actor)
    {
        if (!motions.TryGetValue(actor, out Motion motion)) return false;
        if (!Matches(actor, motion) || !actor.isAlive())
        {
            motions.Remove(actor);
            return false;
        }

        if (World.world.isPaused()) return true;
        double now = World.world.getCurWorldTime();
        if (now <= motion.SampledAt) return true;

        // 先按共同时间求位置；穿越地图格时分段检查原版阻挡，下降时截在真实地表。
        double from = motion.SampledAt - motion.StartedAt, until = now - motion.StartedAt;
        float cell = motion.Terrain == null
            ? 1f
            : Mathf.Min(1f, motion.Terrain.Size.x / motion.Terrain.Width,
                motion.Terrain.Size.y / motion.Terrain.Height);
        float horizontal = new Vector2(motion.Arc.Velocity.x, motion.Arc.Velocity.y).magnitude;
        double stride = horizontal > 0f ? cell * .25d / horizontal : double.PositiveInfinity;
        while (from < until)
        {
            double next = Math.Min(until, from + stride);
            bool landed;
            if (motion.Terrain == null)
            {
                double landing = motion.Arc.LandingAt(0f);
                landed = next >= landing;
                if (landed) next = landing;
            }
            else
            {
                Vector3 end = motion.Arc.Position(next);
                landed = end.z <= Ground(motion.Terrain, end);
                if (landed)
                {
                    double low = from, high = next;
                    for (int i = 0; i < 24; i++)
                    {
                        double mid = (low + high) * .5d;
                        Vector3 point = motion.Arc.Position(mid);
                        if (point.z <= Ground(motion.Terrain, point)) high = mid;
                        else low = mid;
                    }

                    next = high;
                }
            }

            Vector3 position = motion.Arc.Position(next), speed = motion.Arc.Speed(next);
            actor.position_height = Mathf.Max(0f, position.z - Ground(motion.Terrain, position));
            actor.velocity.z = motion.MassGravity == 0f ? motion.ExpectedVelocity.z : speed.z / motion.MassGravity;
            Vector2 requested = position, allowed = actor.checkVelocityAgainstBlock(requested);
            if (allowed != requested)
            {
                float ground = Ground(motion.Terrain, allowed);
                actor.position_height = Mathf.Max(0f, position.z - ground);
                landed = position.z <= ground;
            }

            actor.current_position = allowed;
            actor.setCurrentTile(Toolbox.getTileAt(allowed.x, allowed.y));
            actor.dirty_current_tile = true;
            if (landed)
            {
                actor.stopForce();
                motions.Remove(actor);
                return true;
            }

            if (allowed != requested)
            {
                motion.Arc = new ImpulseArc(new Vector3(allowed.x, allowed.y, position.z),
                    new Vector3(actor.velocity.x * motion.SpeedScale, actor.velocity.y * motion.SpeedScale, speed.z),
                    motion.Arc.Gravity);
                motion.StartedAt += next;
                until -= next;
                from = 0d;
            }
            else from = next;

            motion.ExpectedVelocity = actor.velocity;
        }

        motion.SampledAt = now;
        motion.ExpectedVelocity = actor.velocity;
        motion.ExpectedPosition = actor.current_position;
        motion.ExpectedHeight = actor.position_height;
        return true;
    }

    /// <summary>地表数字不会随显示视角变化。</summary>
    /// <param name="terrain">本次受力的地表。</param><param name="position">地图位置。</param>
    private static float Ground(AbilityHeightField terrain, Vector2 position) =>
        terrain?.Sample(position.x, position.y) ?? 0f;

    /// <summary>世界清理后旧人物不能继续取得运动。</summary>
    internal static void Clear() => motions = new ConditionalWeakTable<Actor, Motion>();
}
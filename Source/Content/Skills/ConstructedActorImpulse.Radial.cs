using System;
using UnityEngine;

namespace Cultiway.Content.Skills;

internal static partial class ConstructedActorImpulse
{
    /// <summary>范围运动在共同时间启动，已受理的上段运动由同一共同步骤先推进。</summary>
    /// <param name="actor">实际人物。</param><param name="velocity">已过抗性的世界速度。</param>
    /// <param name="seconds">最多持续的世界秒数。</param><param name="at">共同确认时间。</param>
    /// <param name="terrain">已经冻结的地表。</param>
    internal static void BeginRadial(Actor actor, Vector3 velocity, float seconds, double at,
        Core.Skills.AbilityHeightField terrain) => BeginDirectedMotion(actor, velocity, seconds, terrain, false, at);

    /// <summary>预测仅取数值轨迹，不发布位置，也不越过该段的实际结束时间。</summary>
    /// <param name="id">实际人物编号。</param><param name="at">待采样时间。</param><param name="foot">实际脚下。</param>
    internal static bool TryRadialFoot(long id, double at, out Vector3 foot)
    {
        foot = default;
        if (!motions.TryGetValue(id, out Motion motion) || !motion.Radial || motion.Supported ||
            !motion.Actor.TryGetTarget(out Actor actor) || !Matches(actor, motion)) return false;
        foot = motion.Arc.Position(Math.Max(0d, Math.Min(at, motion.SlideUntil) - motion.StartedAt));
        return true;
    }

    /// <summary>跟随共同步骤发布有限运动；来源结束后仍只走完已经受理的一段。</summary>
    /// <param name="at">本步已经确认的世界时间。</param>
    internal static void CommitRadial(double at)
    {
        foreach (Motion motion in motions.Values)
            if (motion.Radial && motion.Actor.TryGetTarget(out Actor actor))
            {
                UpdateAt(actor, at);
                ConstructedAbilityService.NotifyActorChanged(actor);
            }
    }
}

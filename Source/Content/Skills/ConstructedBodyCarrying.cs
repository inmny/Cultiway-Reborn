using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实抓持后的带动；预测只改变数字，确认才发布目标位置。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>实际取得目标的带动关系，普通人物取样不遍历全部施放。</summary>
    private static readonly Dictionary<long, BodyConnection> carriedBodies = new();

    /// <summary>查询当前确实取得目标控制的带动关系，不读取选择或图片位置。</summary>
    /// <param name="actor">实际人物编号。</param>
    private static BodyConnection Carrying(long actor) => carriedBodies.TryGetValue(actor, out BodyConnection value)
        ? value : null;

    /// <summary>连续接触读取同一步的前后态，绘制只读取确认后的Surface。</summary>
    /// <param name="connection">实际关系。</param><param name="time">共同取样时间。</param>
    private static BodySurfacePose ConnectionSurface(BodyConnection connection, double time) =>
        connection.Relation.MoveTarget && connection.PredictedSurface != null && time > connection.LastTime &&
        connection.PredictedTime > connection.LastTime
            ? BodySurfacePose.Lerp(connection.Surface, connection.PredictedSurface,
                Mathf.Clamp01((float)((time - connection.LastTime) /
                                     (connection.PredictedTime - connection.LastTime))))
            : connection.Surface;

    /// <summary>来源动作完成本步纯数值推进后，形成目标的同一步预测与候选范围。</summary>
    /// <param name="time">本步共同末端时间。</param>
    private void PredictBodyCarrying(double time)
    {
        foreach (RunningCast running in active)
        {
            BodyConnection connection = running.Connection;
            if (connection?.Active != true || !connection.Relation.MoveTarget) continue;
            Vector3 desired = connection.OriginalSurface.Foot + connection.SourcePoint - connection.OriginalPoint;
            Vector3 allowed = CarryPosition(connection, desired, out bool blocked);
            connection.PredictedSurface = connection.OriginalSurface.Translated(allowed - connection.OriginalSurface.Foot);
            connection.PredictedTime = time;
            connection.MovementBlocked = blocked;
            if (actorIndices.TryGetValue(connection.TargetId, out int index))
                actorIndex.Update(index, AbilityContactIndex.Swept(
                    ActorContact(connection.Surface.Foot, connection.TargetHeight, Vector3.zero),
                    ActorContact(allowed, connection.TargetHeight, Vector3.zero)));
        }
    }

    /// <summary>沿实际小段检查地面和既有阻挡，二分保留最后合法位置，不跨越中间墙格。</summary>
    /// <param name="connection">已经建立的真实关系。</param><param name="desired">同刻抓点要求的位置。</param>
    /// <param name="blocked">实际路径是否被截停。</param>
    private Vector3 CarryPosition(BodyConnection connection, Vector3 desired, out bool blocked)
    {
        Vector3 start = connection.TargetFoot, current = start;
        int count = Math.Max(1, Mathf.CeilToInt(Vector3.Distance(start, desired) /
                                               Mathf.Min(.1f, connection.TargetHeight * .1f)));
        for (int i = 1; i <= count; i++)
        {
            Vector3 next = Vector3.Lerp(start, desired, i / (float)count);
            if (Allowed(current, next))
            {
                current = next;
                continue;
            }

            float low = 0f, high = 1f;
            for (int n = 0; n < 18; n++)
            {
                float middle = (low + high) * .5f;
                if (Allowed(current, Vector3.Lerp(current, next, middle))) low = middle;
                else high = middle;
            }
            blocked = true;
            return Vector3.Lerp(current, next, low);
        }
        blocked = false;
        return current;

        /// <summary>被带动的身体没有自走足底，仍不能进入地面、顶棚或真实阻挡。</summary>
        /// <param name="from">前一合法根点。</param><param name="to">下一候选根点。</param>
        bool Allowed(Vector3 from, Vector3 to)
        {
            if (!BodyLocomotion.CanTranslate(from, to, connection.TargetHeight, terrain)) return false;
            float floor = terrain?.Sample(to.x, to.y) ?? 0f;
            if (to.z < floor - .0001f) return false;
            float ceiling = BodyLocomotion.CeilingHeight?.Invoke(to, connection.TargetHeight * .16f,
                from.z + connection.TargetHeight) ?? float.PositiveInfinity;
            return to.z + connection.TargetHeight <= ceiling + .0001f;
        }
    }

    /// <summary>共同步已经确认后，才将同一目标表面、抓点和世界位置一起发布。</summary>
    /// <param name="time">本步已确认时点。</param>
    private void CommitBodyCarrying(double time)
    {
        foreach (RunningCast running in active)
        {
            BodyConnection connection = running.Connection;
            if (connection?.Active != true || !connection.Relation.MoveTarget || connection.PredictedSurface == null)
                continue;
            BodySurfacePose pose = ConnectionSurface(connection, time);
            PublishBodyPosition(connection.Target, pose.Foot);
            connection.Surface = pose;
            connection.Point = connection.Anchor.Point(pose);
            connection.TargetFoot = pose.Foot;
        }
    }
}

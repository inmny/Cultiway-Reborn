using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实抓持后的带动；预测只改变数字，确认才发布目标位置。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>可选环境冻结实际模型，只在采集边界读取游戏对象。</summary>
    internal static Func<Bounds, AbilityHeightField, BodySurfaceObstacle[]> BodyEnvironmentProvider;

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
            Quaternion rotation = connection.Relation.RotateTarget
                ? running.Cast.BodyPose.Value.EffectorRotation * Quaternion.Inverse(connection.OriginalRotation)
                : Quaternion.identity;
            var motion = new BodySurfaceTransform(connection.OriginalPoint, connection.SourcePoint, rotation);
            connection.PredictedSurface = connection.OriginalSurface.Transformed(motion);
            connection.PredictedTime = time;
            if (actorIndices.TryGetValue(connection.TargetId, out int index))
            {
                Bounds bounds = connection.Surface.Bounds();
                bounds.Encapsulate(connection.PredictedSurface.Bounds());
                actorIndex.Update(index, bounds);
            }
        }
    }

    /// <summary>真实携带环境进入共同候选，来源与目标在最早时点一起回退和确认。</summary>
    /// <param name="clock">共同步起点。</param><param name="step">预测时长。</param><param name="include">共同候选入口。</param>
    private void FindCarryingContacts(double clock, float step, Action<ContactChoice> include)
    {
        foreach (RunningCast running in active)
        {
            BodyConnection connection = running.Connection;
            if (connection?.Active != true || !connection.Relation.MoveTarget || connection.PredictedSurface == null) continue;
            BodySurfaceTransform from = connection.Surface.RigidBasis == null
                ? new BodySurfaceTransform(connection.OriginalPoint, connection.OriginalPoint, Quaternion.identity)
                : connection.Surface.RigidTransform;
            BodySurfaceTransform to = connection.PredictedSurface.RigidTransform;
            Bounds bounds = connection.Surface.Bounds();
            bounds.Encapsulate(connection.PredictedSurface.Bounds());
            float angle = Quaternion.Angle(from.Rotation, to.Rotation) * Mathf.Deg2Rad;
            bounds.Expand(connection.Geometry.Bounds.size.magnitude * angle * angle * .25f + .00004f);
            IReadOnlyList<BodySurfaceObstacle> environment = CaptureCarryingEnvironment(bounds);
            if (!BodySurfaceSweep.Find(connection.Geometry, from, to, terrain, environment, out BodySurfaceHit hit)) continue;
            var before = new AbilityCapsule(-1, connection.SourcePoint, connection.SourcePoint, 0f, null);
            var shape = new ShapeMotion(running, before, before, true);
            ActorMotion actor = actorIndices.TryGetValue(connection.TargetId, out int index) ? actors[index] : default;
            include(new ContactChoice(shape, actor, null, hit.Fraction, hit.Point, hit.Normal, step, carrying: hit));
        }
    }

    /// <summary>模型沿可选入口采集，原版保留明确墙格和地图边界，不从人物包络猜测障碍。</summary>
    /// <param name="bounds">真实扫过范围。</param>
    private IReadOnlyList<BodySurfaceObstacle> CaptureCarryingEnvironment(Bounds bounds)
    {
        var result = new List<BodySurfaceObstacle>();
        if (BodyEnvironmentProvider != null) result.AddRange(BodyEnvironmentProvider(bounds, terrain));
        int left = Mathf.Max(0, Mathf.FloorToInt(bounds.min.x)), right = Mathf.Min(MapBox.width - 1, Mathf.FloorToInt(bounds.max.x));
        int bottom = Mathf.Max(0, Mathf.FloorToInt(bounds.min.y)), top = Mathf.Min(MapBox.height - 1, Mathf.FloorToInt(bounds.max.y));
        for (int y = bottom; y <= top; y++)
        for (int x = left; x <= right; x++)
        {
            WorldTile tile = World.world.GetTileSimple(x, y);
            if (!tile.Type.block || BodyEnvironmentProvider != null && tile.Type.mountains) continue;
            float ground = terrain?.Sample(x + .5f, y + .5f) ?? 0f;
            result.Add(new BodySurfaceObstacle(-1L - x - y * MapBox.width,
                BodySurfaceMesh.Box(new Rect(x, y, 1, 1), ground, ground + tile.Type.block_height), "WallContact"));
        }
        float low = bounds.min.z - 1f, high = bounds.max.z + 1f;
        if (bounds.min.x < 0f) Boundary(new Rect(bounds.min.x - 1f, bounds.min.y - 1f, 1f - bounds.min.x, bounds.size.y + 2f));
        if (bounds.max.x > MapBox.width) Boundary(new Rect(MapBox.width, bounds.min.y - 1f, bounds.max.x - MapBox.width + 1f, bounds.size.y + 2f));
        if (bounds.min.y < 0f) Boundary(new Rect(bounds.min.x - 1f, bounds.min.y - 1f, bounds.size.x + 2f, 1f - bounds.min.y));
        if (bounds.max.y > MapBox.height) Boundary(new Rect(bounds.min.x - 1f, MapBox.height, bounds.size.x + 2f, bounds.max.y - MapBox.height + 1f));
        return result;

        /// <summary>地图外没有可落脚世界，不允许身体从边界穿出。</summary>
        /// <param name="area">实际地图边界外侧。</param>
        void Boundary(Rect area) => result.Add(new BodySurfaceObstacle(long.MinValue, BodySurfaceMesh.Box(area, low, high), "WorldBoundary"));
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

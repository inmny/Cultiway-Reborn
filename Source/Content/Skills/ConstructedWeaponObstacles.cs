using System;
using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>手中兵器和墙格、摆放模型的接触，沿用战斗共同的首次接触时间。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>起势或出招期间已确认的持械姿态，预测结束后与同一件实物比较。</summary>
    private readonly List<(RunningCast Running, BodyPose Pose)> weaponsBefore = new();

    /// <summary>依次核对每件实物时复用前后形状和邻近环境面列表。</summary>
    private readonly List<AbilityCapsule> weaponShapesBefore = new(), weaponShapesAfter = new();

    /// <summary>找实际各段与障碍的首次接触。候选与人物命中一起按时点排序。</summary>
    /// <param name="step">本次共同预测时长。</param>
    /// <param name="include">提交实际障碍候选。</param>
    private void FindWeaponObstacles(float step, Action<ContactChoice> include)
    {
        foreach ((RunningCast running, BodyPose before) in weaponsBefore)
        {
            if (!running.BodyControlled || !running.Cast.ControlsBody) continue;
            BodyPose after = running.Cast.BodyPose.Value;
            if (running.Cast.Plan.Body.WeaponGround && running.Body.FindWeaponGround(before, after, terrain, out var ground))
            {
                var surface = new AbilityCapsule(-1, ground.BodyPoint, ground.BodyPoint, 0f, null, shape: -1);
                var shape = new ShapeMotion(running, surface, surface, true);
                include(new ContactChoice(shape, default, null, ground.Fraction, ground.Point, -ground.Normal, step,
                    environment: ground, heldWeaponObstacle: true));
            }
            if (!running.Cast.Plan.Body.WeaponObstacle) continue;
            running.Body.AppendWeaponObstacleShapes(before, weaponShapesBefore);
            running.Body.AppendWeaponObstacleShapes(after, weaponShapesAfter);
            if (weaponShapesAfter.Count == 0) continue;
            Bounds bounds = AbilityContactIndex.Swept(weaponShapesBefore[0], weaponShapesAfter[0]);
            for (int i = 1; i < weaponShapesAfter.Count; i++)
                bounds.Encapsulate(AbilityContactIndex.Swept(weaponShapesBefore[i], weaponShapesAfter[i]));
            IReadOnlyList<BodySurfaceObstacle> obstacles = CaptureBodyEnvironment(bounds, terrain);
            foreach (BodySurfaceObstacle obstacle in obstacles)
            for (int i = 0; i < weaponShapesAfter.Count; i++)
            {
                var shape = new ShapeMotion(running, weaponShapesBefore[i], weaponShapesAfter[i], true);
                BodySurfaceMesh mesh = obstacle.Mesh;
                if (!shape.Bounds.Intersects(mesh.Bounds)) continue;
                mesh.Query(shape.Bounds, nearby);
                foreach (int face in nearby)
                {
                    int offset = face * 3;
                    Vector3 a = mesh.Points[mesh.Triangles[offset]], b = mesh.Points[mesh.Triangles[offset + 1]],
                        c = mesh.Points[mesh.Triangles[offset + 2]];
                    if (!AbilityTriangleSweep.Find(shape.From, shape.To, a, b, c, a, b, c,
                        out float fraction, out Vector3 point, out Vector3 normal)) continue;
                    var contact = new BodySurfaceHit(fraction, point, -normal, obstacle.Reason, obstacle.Owner);
                    include(new ContactChoice(shape, default, null, fraction, point, -normal, step,
                        environment: contact, heldWeaponObstacle: true));
                }
            }
        }
    }

    /// <summary>使用挡住时的实际姿态转入收势，同时结束该兵器保持的相抵。</summary>
    /// <param name="hit">已经回到实际世界接触时点的兵器。</param>
    private static void ResolveWeaponObstacle(in ContactChoice hit)
    {
        RunningCast running = hit.Left.Running;
        if (!running.Cast.ConfirmWorldContact(-1, hit.Left.To.Shape, hit.Point))
            running.Cast.RecordActionContact(-1, hit.Point, AbilityActionContactKind.World);
        if (hit.Environment.Value.Reason == "GroundContact")
            SpatialCombatFeedback.Sound(CombatContactFeedback.Stone, hit.Point);
        if (running.Connection?.Active == true)
            ReleaseBodyConnection(running, hit.Environment.Value.Reason, fade: false);
        if (!RespondToBodyResult(running, BodyActionResult.Blocked))
            RespondToBodyResult(running, BodyActionResult.Interrupted);
    }
}

using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际持握的器物与地表共用已经确认的姿态。</summary>
internal sealed partial class BodyActionBinding
{
    /// <summary>本次持握的实际器物表面；尺寸相同时反复使用同一份网格。</summary>
    private BodySurfaceMesh weaponGroundMesh;

    /// <summary>采集器物表面所用的实际体高和器物比例。</summary>
    private float weaponGroundScale;

    /// <summary>沿持物手的实际转动，找整件器物首次压到地面的点。</summary>
    /// <param name="before">上一段已确认的身体。</param>
    /// <param name="after">本段预测的身体。</param>
    /// <param name="terrain">共用的原版或 Worldfall 地表。</param>
    /// <param name="hit">实际器物和地面的接触位置。</param>
    internal bool FindWeaponGround(in BodyPose before, in BodyPose after, AbilityHeightField terrain, out BodySurfaceHit hit)
    {
        Matrix4x4 from = WeaponMatrix(before), to = WeaponMatrix(after);
        float scale = action.WeaponSize * before.Height;
        if (weaponGroundMesh == null || scale != weaponGroundScale)
        {
            var points = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Mesh mesh in weapon.Meshes)
            {
                int offset = points.Count;
                foreach (int index in mesh.triangles) triangles.Add(offset + index);
                foreach (Vector3 point in mesh.vertices) points.Add(point * scale);
            }
            weaponGroundMesh = new BodySurfaceMesh(points.ToArray(), triangles.ToArray());
            weaponGroundScale = scale;
        }

        Vector3 wrist = from.inverse.MultiplyPoint3x4(before.Hand);
        var start = new BodySurfaceTransform(wrist * scale, before.Hand, from.rotation);
        var end = new BodySurfaceTransform(wrist * scale, to.MultiplyPoint3x4(wrist), to.rotation);
        return BodySurfaceSweep.Find(weaponGroundMesh, start, end, terrain, Array.Empty<BodySurfaceObstacle>(), out hit);
    }
}

using System;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityMaterial
{
    /// <summary>把同一固定骨架留给连续的刚体移动和触面更新。</summary>
    internal FormPose CaptureSolidPose()
    {
        var pose = Form.Rig.CreatePose();
        if (pose == null) return null;
        Array.Copy(Skin, pose.Skin, Skin.Length);
        pose.BuildProgress = BuildProgress;
        Form.Rig.RebuildBounds(pose);
        return pose;
    }

    /// <summary>移动或拉伸同一实体。体积、金额和骨架保持，碰撞按保存时的同一姿态重建。</summary>
    /// <param name="matrix">新根姿态。</param>
    /// <param name="pose">提前读取的当前固定骨架。</param>
    internal void MoveSolid(in Matrix4x4 matrix, FormPose pose)
    {
        Transform = matrix;
        if (Form.Contacts.Length == 0)
        {
            Shapes[0] = BuildShapes(Form, pose, matrix)[0];
            return;
        }
        for (int i = 0; i < Shapes.Length; i++)
        {
            int shape = Shapes[i].Shape;
            Form.Contacts[shape].World(pose, matrix, 0, shape, 0, Matter, 1, out Shapes[i]);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>自由控制姿态在编译边界转成共同动作，执行复用既有连续过渡。</summary>
public sealed partial class AbilityCast
{
    /// <summary>先核对实际控制，再建立目标局部姿态，由实际开始姿态在指定历时内连续到达。</summary>
    /// <param name="context">当前部位和保存的明确参数。</param>
    private static CompiledMotionParameters CompileDeformation(ConstructionOperationCompileContext context)
    {
        var source = context.Source.Deformation;
        FormResource resource = context.Resource;
        float seconds = context.Source.Seconds;
        string path = context.Path + "/Deformation";
        if (!resource.Deformable) throw new InvalidDataException(path + "：该模型没有可变控制结构，刚性主体可使用整件运动或缩放");
        if (source.Spacing?.Length != 3 || source.Controls == null || source.Controls.Length > resource.Rig.Count ||
            !FormRig.Finite(seconds) || seconds < .05f || seconds > context.Lifetime)
            throw new InvalidDataException(path + "：需要三个控制间距倍率、有限控制列表和0.05秒至存在上限内的历时");
        Vector3 spacing = FormRig.Vector(source.Spacing, 0, path + "/Spacing");
        if (spacing.x < .05f || spacing.y < .05f || spacing.z < .05f || spacing.x > 4f || spacing.y > 4f || spacing.z > 4f)
            throw new InvalidDataException(path + "/Spacing：各方向控制间距须在0.05至4之间");
        if (!string.IsNullOrEmpty(context.Node.ActiveFromMarker) || !string.IsNullOrEmpty(context.Node.ActiveUntilMarker))
            throw new InvalidDataException(path + "：自由形变使用开始和完成步骤，不能引用制作动画的阶段标记");

        FormPose target = resource.Rig.CreatePose();
        resource.Rig.Sample(null, 0f, target);
        for (int i = 0; i < resource.Rig.Count; i++) target.Positions[i] = Vector3.Scale(target.Positions[i], spacing);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (ConstructionControlTarget control in source.Controls)
        {
            if (control == null || string.IsNullOrWhiteSpace(control.Control) || !used.Add(control.Control) ||
                control.Offset?.Length != 3 || control.Rotation?.Length != 3)
                throw new InvalidDataException(path + "/Controls：控制名称缺失、重复或位置角度数量无效");
            int index = ConstructionReferences.Resolve(path + "/Controls/" + control.Control,
                () => resource.Rig.Control(control.Control));
            Vector3 offset = FormRig.Vector(control.Offset, 0, path), angles = FormRig.Vector(control.Rotation, 0, path);
            if (offset.magnitude > 64f || Mathf.Abs(angles.x) > 360f || Mathf.Abs(angles.y) > 360f || Mathf.Abs(angles.z) > 360f)
                throw new InvalidDataException(path + "/Controls：局部偏移最多64模型单位，各角度须在正负360度内");
            target.Positions[index] += offset;
            target.Rotations[index] *= Quaternion.Euler(angles);
        }

        float[] positions = target.Positions.SelectMany(position => new[] { position.x, position.y, position.z }).ToArray();
        float[] rotations = target.Rotations.SelectMany(rotation => new[] { rotation.x, rotation.y, rotation.z, rotation.w }).ToArray();
        var motion = new FormMotion(new FormMotionDescription
        {
            Id = "deformation", Duration = seconds,
            Keys = new[]
            {
                new FormMotionKeyDescription { At = 0, Positions = positions, Rotations = rotations },
                new FormMotionKeyDescription { At = seconds, Positions = positions, Rotations = rotations }
            }
        }, resource.Rig.Count, path);
        return new CompiledMotionParameters(motion, 1f, seconds, default, -1f, -1f, preserveBuild: true);
    }
}

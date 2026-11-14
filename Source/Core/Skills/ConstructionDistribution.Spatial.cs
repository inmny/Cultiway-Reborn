using System;
using System.Collections.Generic;
using System.IO;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>只在编译时沿现有局部曲线放置子结构，不执行一次飞行。</summary>
public sealed class ConstructionDistributionPath
{
    /// <summary>已有曲线资料；关键点参数控制位置分布，不推进世界时间。</summary>
    public ConstructionPath Curve;

    /// <summary>完整路径的候选份数，再共同应用起始、间隔及最多份数。</summary>
    public int Samples = 8;
}

/// <summary>明确区域的形状，仅决定候选出生位置。</summary>
public enum ConstructionDistributionRegion
{
    /// <summary>区域局部三个轴上的方盒，允许零厚的平面或线。</summary>
    Box = 0,

    /// <summary>三个半轴对应尺寸的一半，等尺寸时为球。</summary>
    Ellipsoid = 1
}

/// <summary>区域内各候选的局部朝向，不改变后续独立运动。</summary>
public enum ConstructionDistributionFacing
{
    /// <summary>全部沿区域前向。</summary>
    Fixed = 0,

    /// <summary>沿当前位置背离中心，中心点保持区域前向。</summary>
    Outward = 1,

    /// <summary>沿当前位置朝向中心，中心点保持区域前向。</summary>
    Inward = 2
}

/// <summary>在父模型空间中的有限区域，不扩大任何攻击形状。</summary>
public sealed class ConstructionDistributionSpace
{
    /// <summary>区域形状。</summary>
    public ConstructionDistributionRegion Shape;

    /// <summary>父模型空间中的区域中心。</summary>
    public float[] Centre = new float[3];

    /// <summary>区域局部三个轴的完整宽度。</summary>
    public float[] Size = { 1f, 1f, 1f };

    /// <summary>区域局部轴相对父体的三个旋转角度。</summary>
    public float[] Rotation = new float[3];

    /// <summary>三个轴的格数，取各格中心，零厚轴只能为一格。</summary>
    public int[] Counts = { 1, 1, 1 };

    /// <summary>候选在区域内的方向。</summary>
    public ConstructionDistributionFacing Facing;
}

/// <summary>局部路径及区域只生成有限位置，之后共用已有子结构运行。</summary>
public sealed partial class ConstructionDistribution
{
    /// <summary>复用已有曲线的几何采样，保留真实首尾及切线。</summary>
    /// <param name="path">外部资料位置。</param>
    private FormAnchorBinding[] PathCandidates(string path)
    {
        if (Path?.Curve == null || Path.Samples < 1 || Path.Samples > AbilityCompiler.MaxForms ||
            Path.Curve.Space != ConstructionPathSpace.Local)
            throw new InvalidDataException(path + "/Path：需要局部曲线与1至128个候选，不在编译时读取目标位置");
        var curve = new CompiledConstructionPath(Path.Curve, path + "/Path/Curve", layout: true);
        var result = new FormAnchorBinding[Path.Samples];
        for (int i = 0; i < result.Length; i++)
        {
            float progress = curve.Loop ? (float)i / result.Length : result.Length == 1 ? .5f : (float)i / (result.Length - 1);
            curve.SampleLayout(progress, out Vector3 point, out Vector3 direction);
            Quaternion rotation = Quaternion.identity;
            if (curve.Forward.sqrMagnitude > 0f)
            {
                if (direction.sqrMagnitude == 0f)
                    throw new InvalidDataException(path + "/Path：静止曲段没有朝向，请调整曲线或将Forward设为空保留原朝向");
                rotation = Quaternion.FromToRotation(curve.Forward, direction / Mathf.Sqrt(direction.sqrMagnitude));
            }
            result[i] = new FormAnchorBinding("path." + i, Matrix4x4.TRS(point, rotation, Vector3.one));
        }
        return result;
    }

    /// <summary>按明确区域和格数生成有稳定格位名的候选，椭球只保留内部位置。</summary>
    /// <param name="path">外部资料位置。</param>
    private FormAnchorBinding[] SpaceCandidates(string path)
    {
        if (Space == null || !Enum.IsDefined(typeof(ConstructionDistributionRegion), Space.Shape) ||
            !Enum.IsDefined(typeof(ConstructionDistributionFacing), Space.Facing) || Space.Counts?.Length != 3)
            throw new InvalidDataException(path + "/Space：需要明确区域形状、朝向及三个轴的格数");
        Vector3 centre = SpatialVector(Space.Centre, path + "/Space/Centre"),
            size = SpatialVector(Space.Size, path + "/Space/Size"),
            angles = SpatialVector(Space.Rotation, path + "/Space/Rotation");
        long total = 1;
        for (int axis = 0; axis < 3; axis++)
        {
            int count = Space.Counts[axis];
            if (count < 1 || count > AbilityCompiler.MaxForms || size[axis] < 0f || size[axis] > 10000f ||
                size[axis] == 0f && count != 1)
                throw new InvalidDataException(path + "/Space：尺寸须在0至一万之间，格数须在1至128之间，零厚轴只能取一格");
            total *= count;
        }
        if (size.x == 0f && size.y == 0f && size.z == 0f || total > AbilityCompiler.MaxForms)
            throw new InvalidDataException(path + "/Space：区域须有实际尺寸，三轴候选总量不能超过128份");
        Quaternion orientation = Quaternion.Euler(angles);
        var result = new List<FormAnchorBinding>((int)total);
        for (int z = 0; z < Space.Counts[2]; z++)
        for (int y = 0; y < Space.Counts[1]; y++)
        for (int x = 0; x < Space.Counts[0]; x++)
        {
            double u = (x + .5d) * 2d / Space.Counts[0] - 1d,
                v = (y + .5d) * 2d / Space.Counts[1] - 1d,
                w = (z + .5d) * 2d / Space.Counts[2] - 1d;
            if (Space.Shape == ConstructionDistributionRegion.Ellipsoid && u * u + v * v + w * w > 1d) continue;
            Vector3 local = Vector3.Scale(size * .5f, new Vector3((float)u, (float)v, (float)w));
            Quaternion facing = Quaternion.identity;
            if (Space.Facing != ConstructionDistributionFacing.Fixed && local.sqrMagnitude > 0f)
            {
                Vector3 direction = Space.Facing == ConstructionDistributionFacing.Inward ? -local : local;
                facing = Quaternion.FromToRotation(Vector3.forward, direction / Mathf.Sqrt(direction.sqrMagnitude));
            }
            result.Add(new FormAnchorBinding("space." + x + "." + y + "." + z,
                Matrix4x4.TRS(centre + orientation * local, orientation * facing, Vector3.one)));
        }
        return result.ToArray();
    }

    /// <summary>只在外部排布资料边界读取有限三轴数据。</summary>
    /// <param name="values">外部坐标。</param><param name="path">字段位置。</param>
    private static Vector3 SpatialVector(float[] values, string path)
    {
        if (values?.Length != 3) throw new InvalidDataException(path + "：需要三个有限数值");
        return FormRig.Vector(values, 0, path);
    }
}

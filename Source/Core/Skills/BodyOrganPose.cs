using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>主线程采集的真实器官安装资料，只保留可供并行计算的数值。</summary>
public readonly struct BodyOrganFrame
{
    /// <summary>安装点相对人物脚下的位置。</summary>
    public readonly Vector3 Offset;

    /// <summary>一个图片像素在两个实际轴上的长度，横向包含翻转。</summary>
    public readonly Vector2 PixelScale;

    /// <summary>身体图片平面内的原有倾斜角。</summary>
    public readonly float Tilt;

    /// <summary>复制实际安装与比例。</summary>
    /// <param name="offset">脚下到安装点。</param><param name="pixelScale">两个像素轴的长度。</param>
    /// <param name="tilt">当前真实身体倾斜角。</param>
    public BodyOrganFrame(Vector3 offset, Vector2 pixelScale, float tilt)
    {
        Offset = offset;
        PixelScale = pixelScale;
        Tilt = tilt;
    }
}

/// <summary>器官的完成姿态；原版绘制与接触均使用同一根点、旋转和比例。</summary>
public readonly struct BodyOrganPose
{
    /// <summary>真实根点。</summary>
    public readonly Vector3 Pivot;

    /// <summary>两个实际像素轴，包含比例。</summary>
    public readonly Vector3 X, Y;

    /// <summary>实际器官平面方向。</summary>
    public readonly Quaternion Rotation;

    /// <summary>投影后绘制使用的倾斜角及两个比例。</summary>
    public readonly float Angle;

    /// <summary>接管身体时的原版平面倾斜，与器官自身转角分开保存。</summary>
    public readonly float BodyAngle;

    /// <summary>一个像素的实际横向和高度长度。</summary>
    public readonly Vector2 PixelScale;

    /// <summary>绕实际根点旋转，不带动未接管的身体或肢体。</summary>
    /// <param name="frame">真实安装资料。</param><param name="foot">本步共同脚下。</param>
    /// <param name="angle">相对器官根部的动作角度。</param>
    internal BodyOrganPose(BodyOrganFrame frame, Vector3 foot, float angle)
    {
        Pivot = foot + frame.Offset;
        BodyAngle = frame.Tilt;
        Angle = frame.Tilt + angle * Mathf.Sign(frame.PixelScale.x);
        PixelScale = frame.PixelScale;
        Rotation = Quaternion.AngleAxis(-Angle, Vector3.up);
        X = Rotation * Vector3.right * PixelScale.x;
        Y = Rotation * Vector3.forward * PixelScale.y;
    }

    /// <summary>把相对根点的实际像素位置换到共同世界空间。</summary>
    /// <param name="pixel">实际像素位置。</param>
    public Vector3 Point(Vector2 pixel) => Pivot + X * pixel.x + Y * pixel.y;
}

/// <summary>按实际不透明像素行合并的有限作用形状，稳定索引随本次器官图片保持。</summary>
public sealed class BodyOrganShape
{
    /// <summary>像素行上各段的起点和终点，相对器官根点。</summary>
    private readonly Vector2[] starts, ends;

    /// <summary>实际作用段数量。</summary>
    public int Count => starts.Length;

    /// <summary>复制制作图的实际不透明行，不保留可变输入数组。</summary>
    /// <param name="starts">实际段首。</param><param name="ends">实际段尾。</param>
    public BodyOrganShape(Vector2[] starts, Vector2[] ends)
    {
        if (starts == null || ends == null || starts.Length == 0 || starts.Length != ends.Length)
            throw new System.ArgumentException("器官作用形状需要成对且非空的实际像素段");
        this.starts = (Vector2[])starts.Clone();
        this.ends = (Vector2[])ends.Clone();
    }

    /// <summary>同一姿态生成实际作用段，半径为一个像素厚度的一半。</summary>
    /// <param name="pose">已完成姿态。</param><param name="output">本步共同作用形状。</param>
    public void Append(in BodyOrganPose pose, List<AbilityCapsule> output)
    {
        float radius = Mathf.Max(Mathf.Abs(pose.PixelScale.x), Mathf.Abs(pose.PixelScale.y)) * .5f;
        for (int i = 0; i < starts.Length; i++)
            output.Add(new AbilityCapsule(-1, pose.Point(starts[i]), pose.Point(ends[i]), radius, "physical", i));
    }

    /// <summary>实际安装和旋转可能达到的水平包络，用于候选选择，不扩大实际接触。</summary>
    /// <param name="frame">实际安装与比例。</param>
    public float Reach(in BodyOrganFrame frame)
    {
        float extent = 0f;
        for (int i = 0; i < starts.Length; i++)
            extent = Mathf.Max(extent, Vector2.Scale(starts[i], frame.PixelScale).magnitude,
                Vector2.Scale(ends[i], frame.PixelScale).magnitude);
        return new Vector2(frame.Offset.x, frame.Offset.y).magnitude + extent +
               Mathf.Max(Mathf.Abs(frame.PixelScale.x), Mathf.Abs(frame.PixelScale.y)) * .5f;
    }
}
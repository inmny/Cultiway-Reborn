using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实三维器官的模型安装，不含游戏或绘制对象。</summary>
public readonly struct BodyOrganMount
{
    /// <summary>根点相对同轮脚下的偏移。</summary>
    public readonly Vector3 Offset;

    /// <summary>模型原有方向。</summary>
    public readonly Quaternion Rotation;

    /// <summary>模型单位到地图单位的比例。</summary>
    public readonly float Scale;

    /// <summary>实际部件的绑定逆变换，整器官使用单位矩阵。</summary>
    public readonly Matrix4x4 InverseBind;

    /// <summary>记录已核对的实际安装。</summary>
    /// <param name="offset">根点偏移。</param><param name="rotation">模型方向。</param><param name="scale">实际比例。</param>
    /// <param name="inverseBind">部件原始模型坐标到实际关节的变换。</param>
    public BodyOrganMount(Vector3 offset, Quaternion rotation, float scale, Matrix4x4? inverseBind = null)
    {
        Offset = offset;
        Rotation = rotation;
        Scale = scale;
        InverseBind = inverseBind ?? Matrix4x4.identity;
    }
}

/// <summary>主线程采集的真实器官安装资料，只保留可供并行计算的数值。</summary>
public readonly struct BodyOrganFrame
{
    /// <summary>安装点相对人物脚下的位置。</summary>
    public readonly Vector3 Offset;

    /// <summary>一个图片像素在两个实际轴上的长度，横向包含翻转。</summary>
    public readonly Vector2 PixelScale;

    /// <summary>身体图片平面内的原有倾斜角。</summary>
    public readonly float Tilt;

    /// <summary>原版图片中根点相对枢轴的未倾斜高度。</summary>
    public readonly float NativeHeight;

    /// <summary>真实三维模型安装，空值使用现有原版部件。</summary>
    public readonly BodyOrganMount? Model;

    /// <summary>复制实际安装与比例。</summary>
    /// <param name="offset">脚下到安装点。</param><param name="pixelScale">两个像素轴的长度。</param>
    /// <param name="tilt">当前真实身体倾斜角。</param>
    /// <param name="model">真实三维模型安装。</param><param name="nativeHeight">原版图片根点高度。</param>
    public BodyOrganFrame(Vector3 offset, Vector2 pixelScale, float tilt, BodyOrganMount? model = null,
        float nativeHeight = 0f)
    {
        Offset = offset;
        PixelScale = pixelScale;
        Tilt = tilt;
        Model = model;
        NativeHeight = nativeHeight;
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

    /// <summary>原版真实像素轴的完整空间旋转，绘制和数值表面共用。</summary>
    public readonly Quaternion PixelRotation;

    /// <summary>投影后绘制使用的倾斜角及两个比例。</summary>
    public readonly float Angle;

    /// <summary>接管身体时的原版平面倾斜，与器官自身转角分开保存。</summary>
    public readonly float BodyAngle;

    /// <summary>一个像素的实际横向和高度长度。</summary>
    public readonly Vector2 PixelScale;

    /// <summary>原版图片原有根点，用于把身体图片连接到相同空间安装。</summary>
    public readonly Vector3 NativePivot;

    /// <summary>原版图片中未倾斜的根点高度。</summary>
    public readonly float NativeHeight;

    /// <summary>真实模型的整体姿态，原版部件为空。</summary>
    public readonly Matrix4x4? Model;

    /// <summary>绕实际根点旋转，不带动未接管的身体或肢体。</summary>
    /// <param name="frame">真实安装资料。</param><param name="foot">本步共同脚下。</param>
    /// <param name="angles">相对器官根部的三个局部转角。</param>
    internal BodyOrganPose(BodyOrganFrame frame, Vector3 foot, Vector3 angles)
    {
        NativePivot = foot + frame.Offset;
        NativeHeight = frame.NativeHeight;
        Pivot = frame.Model.HasValue ? foot + frame.Model.Value.Offset : NativePivot;
        BodyAngle = frame.Tilt;
        float mirror = Mathf.Sign(frame.PixelScale.x);
        Angle = frame.Tilt + angles.x * mirror;
        PixelScale = frame.PixelScale;
        Quaternion axes = Quaternion.LookRotation(Vector3.left, Vector3.forward);
        PixelRotation = Quaternion.AngleAxis(-frame.Tilt, Vector3.up) * axes *
            Quaternion.Euler(angles.x * mirror, angles.y * mirror, angles.z) * Quaternion.Inverse(axes);
        Rotation = PixelRotation;
        X = Rotation * Vector3.right * PixelScale.x;
        Y = Rotation * Vector3.forward * PixelScale.y;
        Model = null;
        if (frame.Model.HasValue)
        {
            BodyOrganMount model = frame.Model.Value;
            Rotation = model.Rotation * Quaternion.Euler(angles);
            Model = Matrix4x4.TRS(Pivot, Rotation, Vector3.one * model.Scale) * model.InverseBind;
        }
    }

    /// <summary>原版图片的共同空间变换；有限深度沿不改变画面投影的方向延伸，旋转后也不挪动可见轮廓。</summary>
    /// <param name="pixelsPerUnit">图片一个模型单位包含的像素数。</param>
    /// <param name="pivotFromMount">图片枢轴相对制作根点的像素偏移。</param>
    internal Matrix4x4 NativeMatrix(float pixelsPerUnit, Vector2 pivotFromMount)
    {
        Vector3 x = X * pixelsPerUnit, y = Y * pixelsPerUnit;
        // 数值表面的本地高度是y+z；深度方向在原版投影(x,y+z)中相互抵消。
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetColumn(0, new Vector4(x.x, x.y, x.z, 0f));
        Vector3 depth = y - PixelRotation * new Vector3(0f, -1f, 1f) * Mathf.Abs(PixelScale.y * pixelsPerUnit);
        matrix.SetColumn(1, new Vector4(depth.x, depth.y, depth.z, 0f));
        matrix.SetColumn(2, new Vector4(y.x, y.y, y.z, 0f));
        Vector3 root = Point(pivotFromMount);
        matrix.SetColumn(3, new Vector4(root.x, root.y, root.z, 1f));
        return matrix;
    }

    /// <summary>原版剩余身体沿动作锁定的方向与原身体倾斜放置，不随独立器官转角旋转。</summary>
    /// <param name="foot">本轮实际脚下。</param><param name="pixelsPerUnit">身体图片像素比例。</param>
    internal Matrix4x4 NativeBodyMatrix(Vector3 foot, float pixelsPerUnit)
    {
        float radians = BodyAngle * Mathf.Deg2Rad;
        Vector3 x = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * (PixelScale.x * pixelsPerUnit),
            y = new Vector3(-Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * (PixelScale.y * pixelsPerUnit);
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetColumn(0, new Vector4(x.x, x.y, x.z, 0f));
        Vector3 depth = y - new Vector3(0f, -1f, 1f) * Mathf.Abs(PixelScale.y * pixelsPerUnit);
        matrix.SetColumn(1, new Vector4(depth.x, depth.y, depth.z, 0f));
        matrix.SetColumn(2, new Vector4(y.x, y.y, y.z, 0f));
        matrix.SetColumn(3, new Vector4(foot.x, foot.y, foot.z, 1f));
        return matrix;
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

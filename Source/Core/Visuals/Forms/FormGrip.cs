using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>制作时声明的器物握持帧及真实柄粗，不从名字猜方向。</summary>
public sealed class FormGripDescription
{
    /// <summary>同一资源中的稳定握持编号。</summary>
    public string Id;
    /// <summary>已经存在的真实握点，方向沿柄长轴。</summary>
    public string Anchor;
    /// <summary>确定绕柄朝向的制作方向，不能与柄轴平行。</summary>
    public float[] Up;
    /// <summary>握持处实际最大直径，使用模型单位。</summary>
    public float Diameter;
    /// <summary>器物模型缩放与使用者体高之比的允许范围。</summary>
    public float[] Scale;
}

/// <summary>已经解析的纯数值握持，显示与实际作用共用对齐结果。</summary>
public sealed class FormGrip
{
    /// <summary>真实握持编号及所属挂点。</summary>
    public readonly string Id, Anchor;
    /// <summary>实际柄粗。</summary>
    public readonly float Diameter;
    /// <summary>已声明的相对体高尺寸范围。</summary>
    public readonly Vector2 Scale;
    /// <summary>绑定模型中的完整握持帧。</summary>
    public readonly Matrix4x4 Transform;
    /// <summary>握点和器物表面使用相同的控制。</summary>
    private readonly FormControlBlend blend;

    /// <summary>在资源边界核对尺寸及朝向，一次复制为独立数值。</summary>
    /// <param name="source">外部制作资料。</param><param name="anchor">已解析的实际挂点。</param>
    /// <param name="path">具体资源位置。</param>
    internal FormGrip(FormGripDescription source, FormAnchorBinding anchor, string path)
    {
        if (source.Up?.Length != 3 || source.Scale?.Length != 2 || !FormRig.Finite(source.Diameter) || source.Diameter <= 0f ||
            !FormRig.Finite(source.Scale[0]) || !FormRig.Finite(source.Scale[1]) || source.Scale[0] <= 0f || source.Scale[0] > source.Scale[1])
            throw new InvalidDataException(path + "：握持缺少有效柄粗、尺寸范围或绕轴方向");
        Vector3 up = FormRig.Vector(source.Up, 0, path), forward = anchor.Transform.MultiplyVector(Vector3.forward);
        if (Vector3.Cross(up, forward).sqrMagnitude < .000001f)
            throw new InvalidDataException(path + "：握持绕轴方向不能与柄轴平行");
        Id = source.Id; Anchor = source.Anchor; Diameter = source.Diameter;
        Scale = new Vector2(source.Scale[0], source.Scale[1]); blend = anchor.Blend;
        Transform = Matrix4x4.TRS(anchor.Transform.GetColumn(3), Quaternion.LookRotation(forward, up), Vector3.one);
    }

    /// <summary>身体和器物均按真实尺寸检查，超出握持能力时不能靠穿模接受。</summary>
    /// <param name="scale">器物模型缩放与体高之比。</param>
    /// <param name="minimumOpening">手部最小可闭合尺寸与体高之比。</param>
    /// <param name="maximumOpening">手部最大可闭合尺寸与体高之比。</param>
    public bool Fits(float scale, float minimumOpening, float maximumOpening) =>
        scale >= Scale.x && scale <= Scale.y && Diameter * scale >= minimumOpening && Diameter * scale <= maximumOpening;

    /// <summary>取当前握持帧；骨架器物随实际控制变化，刚性器物保持绑定帧。</summary>
    /// <param name="pose">器物本次姿态，刚性器物不需要。</param>
    public Matrix4x4 Frame(FormPose pose = null) => blend.Frame(Transform, pose);

    /// <summary>位置、柄轴与绕轴方向一起对齐，实际作用随后使用同一器物矩阵。</summary>
    /// <param name="socket">目标的无缩放握持帧。</param><param name="scale">器物实际世界缩放。</param>
    /// <param name="pose">器物本次姿态，刚性器物不需要。</param>
    public Matrix4x4 Align(Matrix4x4 socket, float scale, FormPose pose = null) =>
        socket * Matrix4x4.Scale(Vector3.one * scale) * Frame(pose).inverse;
}

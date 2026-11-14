using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>制作资料中一个命名控制对表面点的影响。</summary>
public sealed class FormInfluenceDescription
{
    /// <summary>已经存在的控制名称。</summary>
    public string Control;
    /// <summary>非负权重，各项合计为一。</summary>
    public float Weight;
}

/// <summary>加载时解析的最多四项控制，计算不再查名称或读取网格。</summary>
public readonly struct FormControlBlend
{
    /// <summary>实际参与的控制索引，空值表示固定模型空间。</summary>
    private readonly int[] indices;
    /// <summary>已经归一的对应权重。</summary>
    private readonly float[] weights;
    /// <summary>是否实际依赖骨架。</summary>
    public bool HasControls => indices != null;

    /// <summary>编译时按实际控制链的最大平移和绑定点计算保守半径，连续旋转不会越过此界。</summary>
    /// <param name="point">绑定空间中的实际点。</param><param name="rig">同一资源骨架。</param><param name="motions">实际采用的动作。</param>
    internal float RadiusBound(Vector3 point, FormRig rig, FormMotion[] motions)
    {
        if (indices == null) return point.magnitude;
        float result = 0f;
        for (int i = 0; i < indices.Length; i++) result += weights[i] * rig.RadiusBound(indices[i], point, motions);
        return result;
    }

    /// <summary>优先读取明确权重，其次使用单个控制或固定空间。</summary>
    /// <param name="source">明确指定的混合权重，空集合不覆盖单控制。</param>
    /// <param name="control">单控制或所属刚性部件的默认控制。</param>
    /// <param name="rig">已解析骨架。</param>
    internal FormControlBlend(FormInfluenceDescription[] source, string control, FormRig rig)
    {
        indices = null; weights = null;
        if (source == null || source.Length == 0)
        {
            int index = rig.Control(control);
            if (index < 0) return;
            indices = new[] { index }; weights = new[] { 1f }; return;
        }
        if (source.Length > 4) throw new InvalidDataException("表面绑定不能超过四项控制");
        indices = new int[source.Length]; weights = new float[source.Length];
        float sum = 0f;
        for (int i = 0; i < source.Length; i++)
        {
            FormInfluenceDescription item = source[i];
            if (item == null || string.IsNullOrEmpty(item.Control) || !FormRig.Finite(item.Weight) || item.Weight < 0f)
                throw new InvalidDataException("表面绑定的控制或权重无效");
            indices[i] = rig.Control(item.Control); weights[i] = item.Weight; sum += item.Weight;
        }
        if (Mathf.Abs(sum - 1f) > .00001f) throw new InvalidDataException("表面绑定权重没有归一");
        for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
    }

    /// <summary>与网格顶点使用相同的加权控制姿态。</summary>
    /// <param name="point">绑定模型空间中的点。</param>
    /// <param name="pose">当前独占姿态；固定点不需要它。</param>
    public Vector3 Point(Vector3 point, FormPose pose)
    {
        if (indices == null) return point;
        Vector3 result = Vector3.zero;
        for (int i = 0; i < indices.Length; i++) result += pose.Skin[indices[i]].MultiplyPoint3x4(point) * weights[i];
        return result;
    }

    /// <summary>挂点的位置与方向一同随相同混合矩阵变化。</summary>
    /// <param name="pose">当前父形体姿态。</param>
    public Matrix4x4 Matrix(FormPose pose)
    {
        if (indices == null) return Matrix4x4.identity;
        if (indices.Length == 1) return pose.Skin[indices[0]];
        Matrix4x4 result = default;
        for (int i = 0; i < indices.Length; i++)
            for (int entry = 0; entry < 16; entry++) result[entry] += pose.Skin[indices[i]][entry] * weights[i];
        return result;
    }

    /// <summary>随表面带动完整接触帧，去掉混合矩阵中的剪切和缩放。</summary>
    /// <param name="rest">绑定模型中的完整坐标系。</param><param name="pose">共同骨架姿态。</param>
    public Matrix4x4 Frame(Matrix4x4 rest, FormPose pose)
    {
        if (!HasControls) return rest;
        Matrix4x4 posed = Matrix(pose) * rest;
        return Matrix4x4.TRS(posed.GetColumn(3), Quaternion.LookRotation(posed.GetColumn(2), posed.GetColumn(1)), Vector3.one);
    }
}

/// <summary>加载时已经确定位置、方向及控制的命名挂点。</summary>
public readonly struct FormAnchorBinding
{
    /// <summary>稳定挂点名称。</summary>
    public readonly string Id;
    /// <summary>该部位实际建立后才可使用的最低成形进度。</summary>
    public readonly float BuildAt;
    /// <summary>绑定模型空间中的位置与方向。</summary>
    public readonly Matrix4x4 Transform;
    /// <summary>实际表面控制。</summary>
    public readonly FormControlBlend Blend;

    /// <summary>从真实三角面取得的独立顶点绑定；普通制作挂点为空。</summary>
    internal readonly FormSurfaceBinding Surface;

    /// <summary>取实际姿态中的位置与方向，不把表面剪切传给接触坐标。</summary>
    /// <param name="pose">与显示及作用共用的骨架姿态。</param>
    public Matrix4x4 Frame(FormPose pose = null) => Surface?.Frame(pose) ?? Blend.Frame(Transform, pose);

    /// <summary>路径及空间生成的完整局部位置朝向，继续普通父体随动。</summary>
    /// <param name="id">稳定生成点名。</param><param name="transform">已经确定的局部坐标系。</param>
    internal FormAnchorBinding(string id, Matrix4x4 transform)
    {
        Id = id; Transform = transform; Blend = default; Surface = null; BuildAt = 0f;
    }

    /// <summary>将实际三角面作为同一挂点通道的数值来源。</summary>
    /// <param name="id">稳定生成点名。</param><param name="surface">独立三顶点绑定。</param>
    internal FormAnchorBinding(string id, FormSurfaceBinding surface)
    {
        Id = id; Surface = surface; Transform = surface.Frame(null); Blend = default; BuildAt = surface.BuildAt;
    }

    /// <summary>在资源加载边界一次解析。</summary>
    /// <param name="source">制作挂点。</param>
    /// <param name="rig">所属骨架。</param>
    /// <param name="control">所属刚性部件的默认控制。</param>
    internal FormAnchorBinding(FormAnchorDescription source, FormRig rig, string control)
    {
        Id = source.Id;
        Surface = null;
        if (!FormRig.Finite(source.BuildAt) || source.BuildAt < 0f || source.BuildAt > 1f)
            throw new InvalidDataException(source.Id + "：挂点成形进度须在0至1之间");
        BuildAt = source.BuildAt;
        Transform = Matrix4x4.TRS(FormRig.Vector(source.Position, 0, Id),
            Quaternion.FromToRotation(Vector3.forward, FormRig.Vector(source.Direction, 0, Id)), Vector3.one);
        Blend = new FormControlBlend(source.Influences, string.IsNullOrEmpty(source.Control) ? control : source.Control, rig);
    }
}

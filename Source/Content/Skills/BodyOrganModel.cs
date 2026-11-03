using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际三维身体提供的器官安装资料，几何和身体姿态在受理时固定。</summary>
internal sealed class BodyOrganModel
{
    /// <summary>真实器官的共用模型及作用形状。</summary>
    internal readonly FormResource Resource;

    /// <summary>安装点、方向和长度，使用三维身体自身的坐标和尺寸。</summary>
    internal readonly Vector3 Mount;

    /// <summary>模型根部沿实际表面朝外的方向。</summary>
    internal readonly Quaternion Rotation;

    /// <summary>实际身体和器官的模型单位到地图单位比例。</summary>
    internal readonly float BodyScale, Scale;

    /// <summary>固定站立身体的各命名部件姿态，不向工作线程传外部模组对象。</summary>
    internal readonly IReadOnlyDictionary<string, Matrix4x4> Parts;

    /// <summary>记录已由对应身体接入核对的安装和数值姿态。</summary>
    /// <param name="resource">实际器官模型。</param><param name="mount">身体模型空间安装点。</param>
    /// <param name="rotation">器官相对身体的方向。</param><param name="bodyScale">实际身体比例。</param>
    /// <param name="scale">器官自身的地图比例。</param><param name="parts">各命名部件的整体变换。</param>
    internal BodyOrganModel(FormResource resource, Vector3 mount, Quaternion rotation, float bodyScale,
        float scale, IReadOnlyDictionary<string, Matrix4x4> parts)
    {
        Resource = resource;
        Mount = mount;
        Rotation = rotation;
        BodyScale = bodyScale;
        Scale = scale;
        Parts = parts;
    }

    /// <summary>沿本次锁定方向生成不可变的三维安装，不按观察镜头改变物理位置。</summary>
    /// <param name="direction">本次身体方向。</param>
    internal BodyOrganMount Frame(Vector3 direction)
    {
        Quaternion body = Quaternion.AngleAxis(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f,
            Vector3.forward);
        return new BodyOrganMount(body * Mount * BodyScale, body * Rotation, Scale);
    }
}
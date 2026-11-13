using System.Collections.Generic;
using System.Linq;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际三维身体提供的器官安装资料，几何和身体姿态在受理时固定。</summary>
internal sealed class BodyOrganModel
{
    /// <summary>真实器官的共用模型及作用形状。</summary>
    internal readonly FormResource Resource;

    /// <summary>实际借用的完整形体或对应真实分件，共享原网格而不复制模型。</summary>
    internal readonly FormResource[] Forms;

    /// <summary>只有实际借用分件的作用形状，稳定顺序随原资源保持。</summary>
    internal readonly FormContactBinding[] Contacts;

    /// <summary>所选关节在原器官中的真实绑定姿态，整器官为空。</summary>
    private readonly Matrix4x4? joint;

    /// <summary>以实际关节为圆心的保守触达，不把整器官其他部件加入所选部件。</summary>
    internal readonly float Reach;

    /// <summary>安装点、方向和长度，使用三维身体自身的坐标和尺寸。</summary>
    internal readonly Vector3 Mount;

    /// <summary>模型根部沿实际表面朝外的方向。</summary>
    internal readonly Quaternion Rotation;

    /// <summary>实际身体和器官的模型单位到地图单位比例。</summary>
    internal readonly float BodyScale, Scale;

    /// <summary>固定站立身体的各命名部件姿态，不向工作线程传外部模组对象。</summary>
    internal readonly IReadOnlyDictionary<string, Matrix4x4> Parts;

    /// <summary>实际承接器官根点的身体部件。</summary>
    internal readonly string Parent;

    /// <summary>固定自然武器所承接的真实身体关节，软器官为空。</summary>
    internal readonly Vector3? DriverPivot;

    /// <summary>该关节在实际身体中的绑定轴。</summary>
    internal readonly Quaternion? DriverAxes;

    /// <summary>记录已由对应身体接入核对的安装和数值姿态。</summary>
    /// <param name="resource">实际器官模型。</param><param name="mount">身体模型空间安装点。</param>
    /// <param name="rotation">器官相对身体的方向。</param><param name="bodyScale">实际身体比例。</param>
    /// <param name="scale">器官自身的地图比例。</param><param name="parts">各命名部件的整体变换。</param>
    /// <param name="parent">实际承接安装的部件。</param>
    /// <param name="forms">实际借用的分件，缺省使用完整器官。</param><param name="joint">所选真实关节。</param>
    /// <param name="driverPivot">固定自然武器承接的身体关节。</param><param name="driverAxes">真实身体关节轴。</param>
    internal BodyOrganModel(FormResource resource, Vector3 mount, Quaternion rotation, float bodyScale,
        float scale, IReadOnlyDictionary<string, Matrix4x4> parts, string parent, FormResource[] forms = null,
        Matrix4x4? joint = null, Vector3? driverPivot = null, Quaternion? driverAxes = null)
    {
        Resource = resource;
        Forms = forms ?? new[] { resource };
        Contacts = forms == null ? resource.Contacts : Forms.SelectMany(form => form.Contacts).ToArray();
        this.joint = joint;
        Mount = mount;
        Rotation = rotation;
        BodyScale = bodyScale;
        Scale = scale;
        Parts = parts;
        Parent = parent;
        DriverPivot = driverPivot;
        DriverAxes = driverAxes;
        Vector3 pivot = joint.HasValue ? (Vector3)joint.Value.GetColumn(3) : Vector3.zero;
        Reach = Mount.magnitude * BodyScale + (pivot.magnitude + Contacts.Max(contact =>
            Mathf.Max((contact.A - pivot).magnitude, (contact.B - pivot).magnitude) + contact.Radius)) * Scale;
        if (DriverPivot.HasValue)
            Reach += (DriverPivot.Value.magnitude + (Mount - DriverPivot.Value).magnitude - Mount.magnitude) * BodyScale;
    }

    /// <summary>沿本次锁定方向生成不可变的三维安装，不按观察镜头改变物理位置。</summary>
    /// <param name="direction">本次身体方向。</param>
    internal BodyOrganMount Frame(Vector3 direction)
    {
        Quaternion body = Quaternion.AngleAxis(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f,
            Vector3.forward);
        Quaternion facing = body * Rotation;
        Vector3 offset = body * Mount * BodyScale;
        return joint.HasValue
            ? new BodyOrganMount(offset + facing * (Vector3)joint.Value.GetColumn(3) * Scale,
                facing * joint.Value.rotation, Scale, joint.Value.inverse)
            : new BodyOrganMount(offset, facing, Scale);
    }

    /// <summary>只允许选择资源明确提供且确实带动几何的命名控制。</summary>
    /// <param name="id">实际部件名称。</param>
    internal bool HasPart(string id) => Resource.Description.Controls.Any(control => control.Id == id) &&
        Resource.Description.Parts.Any(part => part.Control == id);

    /// <summary>在受理时锁定真实部件及关节，运行和显示只读同一数值安装。</summary>
    /// <param name="id">实际部件名称。</param>
    internal BodyOrganModel Select(string id)
    {
        if (!HasPart(id)) return null;
        FormResource[] forms = Resource.Description.Parts.Select((part, index) => (part, index))
            .Where(entry => entry.part.Control == id).Select(entry => Resource.Part(entry.index)).ToArray();
        FormPose pose = Resource.Rig.CreatePose();
        Resource.Rig.Sample(null, 0f, pose);
        return new BodyOrganModel(Resource, Mount, Rotation, BodyScale, Scale, Parts, Parent, forms,
            pose.World[Resource.Rig.Control(id)], DriverPivot, DriverAxes);
    }

    /// <summary>把实际身体关节换到本次共同方向，只向数值计算提供位置与轴。</summary>
    /// <param name="direction">本次身体方向。</param>
    internal (Vector3 Pivot, Quaternion Axes) DriverFrame(Vector3 direction)
    {
        Quaternion body = Quaternion.AngleAxis(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f, Vector3.forward);
        return (body * DriverPivot.Value * BodyScale, body * DriverAxes.Value);
    }
}

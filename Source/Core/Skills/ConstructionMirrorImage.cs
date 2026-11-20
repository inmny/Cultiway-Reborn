using System;
using System.IO;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>用实际镜口承载原人物的活动表面。</summary>
public sealed class ConstructionMirrorImage
{
    /// <summary>映像相对于镜口的占用比例。</summary>
    public float Fill = .82f;
}

/// <summary>编译时已经确认的固定镜口和投影尺寸。</summary>
internal sealed class CompiledMirrorImage
{
    /// <summary>镜口局部中心和有效半径。</summary>
    internal readonly Vector3 Centre;
    internal readonly float Radius;

    /// <summary>映像相对于镜口的占用比例。</summary>
    internal readonly float Fill;

    /// <summary>核对参考挂点确实属于带有作用面的固定圆镜。</summary>
    /// <param name="source">作者所填的映像资料。</param>
    /// <param name="resource">实际镜框、镜面和作用形体。</param>
    /// <param name="path">资料路径。</param>
    internal CompiledMirrorImage(ConstructionMirrorImage source, FormResource resource, string path)
    {
        int face = Array.FindIndex(resource.Description.Parts, part => Array.IndexOf(part.Roles, "mirror.face") >= 0);
        int anchor = Array.FindIndex(resource.Description.Anchors, item => item.Id == "face" && item.Part == face);
        if (!(source.Fill >= .35f && source.Fill <= .94f) || face < 0 || anchor < 0 ||
            resource.PartControls[face] >= 0 || resource.Description.Parts[face].Skin ||
            !Array.Exists(resource.Description.Contacts, contact => contact.Part == face))
            throw new InvalidDataException(path + "：映像需要固定、有实际作用面的镜口和有效占用比例");

        Matrix4x4 frame = resource.Anchors[anchor].Frame();
        Vector3 front = frame.GetColumn(2);
        Centre = frame.GetColumn(3);
        Vector2 lower = Vector2.zero, upper = Vector2.zero;
        Vector3 centre = Centre;
        foreach (FormContactBinding contact in resource.Contacts)
        {
            if (contact.Part != face) continue;
            Vector3 a = contact.A - centre, b = contact.B - centre;
            var extent = Vector2.one * contact.Radius;
            lower = Vector2.Min(lower, new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z)) - extent);
            upper = Vector2.Max(upper, new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)) + extent);
        }
        Radius = Mathf.Min(Mathf.Min(-lower.x, -lower.y), Mathf.Min(upper.x, upper.y));
        if (Vector3.Dot(front, Vector3.down) < .999f || !(Radius > 0f) ||
            (upper + lower).sqrMagnitude > Radius * Radius * .0025f ||
            Mathf.Abs(upper.x - lower.x - (upper.y - lower.y)) > Radius * .08f)
            throw new InvalidDataException(path + "：镜口需要正向的圆形镜面和居中的面挂点");
        Fill = source.Fill;
    }
}

using System;
using System.IO;
using System.Linq;
using Cultiway.Core.Visuals.Forms;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>用实际镜口承载原人物的活动表面。</summary>
public sealed class ConstructionMirrorImage
{
    /// <summary>映像相对于镜口的占用比例。</summary>
    public float Fill = .82f;

    /// <summary>由真实击镜启动的裂势。省略时只显示映像。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionMirrorFracture Fracture;
}

/// <summary>同招部位击中镜子后，允许裂纹传到的明确身体部位。</summary>
public sealed class ConstructionMirrorFracture
{
    /// <summary>负责第一次实际击镜的攻击部位路径。</summary>
    public string Trigger;

    /// <summary>从击镜到裂纹走完整个镜口的世界秒数。</summary>
    public float TravelSeconds = .75f;

    /// <summary>可接收裂势的实际部位名称；每一个名字至多结算一次。</summary>
    public string[] Parts = Array.Empty<string>();
}

/// <summary>编译时已经确认的固定镜口和投影尺寸。</summary>
internal sealed class CompiledMirrorImage
{
    /// <summary>镜口局部中心和有效半径。</summary>
    internal readonly Vector3 Centre;
    internal readonly float Radius;

    /// <summary>映像相对于镜口的占用比例。</summary>
    internal readonly float Fill;

    /// <summary>已核对的击镜和传递规则。</summary>
    internal readonly CompiledMirrorFracture Fracture;

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
        if (source.Fracture != null) Fracture = new CompiledMirrorFracture(source.Fracture, path + "/Fracture");
    }
}

/// <summary>经过检查且脱离编辑数据的裂势许可。</summary>
internal sealed class CompiledMirrorFracture
{
    /// <summary>在完整部位树展开后解析成攻击索引。</summary>
    internal readonly string Trigger;
    internal int TriggerNode = -1;

    /// <summary>整条裂势的时长和归属不同的实际部位。</summary>
    internal readonly float TravelSeconds;
    internal readonly string[] Parts;

    /// <summary>先检查作者明确提供的时间及接收部位。</summary>
    /// <param name="source">原始资料。</param><param name="path">资料路径。</param>
    internal CompiledMirrorFracture(ConstructionMirrorFracture source, string path)
    {
        if (string.IsNullOrWhiteSpace(source.Trigger) || !(source.TravelSeconds >= .22f && source.TravelSeconds <= 4f) ||
            source.Parts == null || source.Parts.Length is < 1 or > 8 ||
            source.Parts.Any(part => string.IsNullOrWhiteSpace(part) || part.Length > 96 || part == "surface") ||
            source.Parts.Distinct(StringComparer.Ordinal).Count() != source.Parts.Length)
            throw new InvalidDataException(path + "：裂势需要明确的击镜部位、有限传播时长和一至八个不同的实际身体部位");
        Trigger = source.Trigger;
        TravelSeconds = source.TravelSeconds;
        Parts = (string[])source.Parts.Clone();
    }
}

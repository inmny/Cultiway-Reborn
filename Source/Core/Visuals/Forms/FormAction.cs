using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>基础动作的制作约定，不代替人物的移动和受力控制。</summary>
public sealed class FormActionDescription
{
    /// <summary>稳定动作编号和已有制作动作。</summary>
    public string Id, Motion;
    /// <summary>模型缩放、播放速度和绕制作上向转动角度的范围。</summary>
    public float[] Scale, Rate, Turn;
    /// <summary>实际支撑部位及保持阶段；悬浮身体可以为空。</summary>
    public FormActionSupportDescription[] Supports;
    /// <summary>双方接触部位及接触和解除阶段。</summary>
    public FormActionContactDescription[] Contacts;
}

/// <summary>同一动作中保持的实际支撑点。</summary>
public sealed class FormActionSupportDescription
{
    /// <summary>实际挂点、开始阶段及解除阶段。</summary>
    public string Anchor, From, Until;
    /// <summary>允许的模型空间位移误差。</summary>
    public float Slack;
}

/// <summary>双方接触的部位用途和可对齐范围。</summary>
public sealed class FormActionContactDescription
{
    /// <summary>关系编号、本方挂点及对方实际部位用途。</summary>
    public string Id, Anchor, PartnerRole;
    /// <summary>接触开始与解除的已有动作标记。</summary>
    public string From, Until;
    /// <summary>允许的世界距离及朝向角度误差。</summary>
    public float Gap, Angle;
    /// <summary>双方表面相对；持物则沿相同握持坐标对齐。</summary>
    public bool Opposed;
}

/// <summary>资源加载时已解析的动作，只保存共同资源和数值约定。</summary>
public sealed class FormAction
{
    /// <summary>动作保留所属资源版本，重载不改写已有动作。</summary>
    public readonly FormResource Resource;
    /// <summary>稳定编号。</summary>
    public readonly string Id;
    /// <summary>同一时间取样的制作动作。</summary>
    public readonly FormMotion Motion;
    /// <summary>实际允许的尺寸、速度和转向范围。</summary>
    public readonly Vector2 Scale, Rate, Turn;
    /// <summary>已经解析的实际支撑。</summary>
    internal readonly Support[] Supports;
    /// <summary>已经解析的接触资料。</summary>
    private readonly Dictionary<string, Contact> contacts = new(StringComparer.Ordinal);
    /// <summary>制作声明的模型上向。</summary>
    internal readonly Vector3 Up;

    /// <summary>一次性解析所有名称、范围和支撑参考姿态。</summary>
    /// <param name="resource">完整资源。</param><param name="source">制作输入。</param>
    internal FormAction(FormResource resource, FormActionDescription source)
    {
        Resource = resource; Id = source.Id;
        Motion = resource.Rig.Motion(source.Motion);
        if (Motion.Loop) throw new InvalidDataException(Id + "：基础接触动作需要明确的结束阶段");
        Scale = Range(source.Scale, true); Rate = Range(source.Rate, true); Turn = Range(source.Turn, false);
        Up = FormRig.Vector(resource.Description.Up, 0, Id).normalized;
        if (source.Supports == null || source.Contacts == null) throw new InvalidDataException(Id + "：缺少支撑或接触集合");
        var pose = resource.Rig.CreatePose();
        var used = new HashSet<int>();
        Supports = source.Supports.Select(s =>
        {
            if (s == null || !FormRig.Finite(s.Slack) || s.Slack < 0) throw new InvalidDataException(Id + "：支撑误差无效");
            int index = Anchor(s.Anchor);
            if (!used.Add(index)) throw new InvalidDataException(Id + "：支撑点重复");
            Vector2 interval = Interval(s.From, s.Until);
            resource.Rig.Sample(Motion, interval.x, pose);
            return new Support(resource.Anchors[index], interval, s.Slack, resource.Anchors[index].Frame(pose).GetColumn(3));
        }).ToArray();
        foreach (var c in source.Contacts)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Id) || contacts.ContainsKey(c.Id) || string.IsNullOrWhiteSpace(c.PartnerRole) ||
                !FormRig.Finite(c.Gap) || c.Gap < 0 || !FormRig.Finite(c.Angle) || c.Angle < 0 || c.Angle > 180)
                throw new InvalidDataException(Id + "：接触编号、对方部位或容差无效");
            contacts.Add(c.Id, new Contact(resource.Anchors[Anchor(c.Anchor)], c.PartnerRole, Interval(c.From, c.Until), c.Gap, c.Angle, c.Opposed));
        }
    }

    /// <summary>资源边界检查声明的调整范围。</summary>
    /// <param name="values">最小及最大值。</param><param name="positive">是否必须为正数。</param>
    private Vector2 Range(float[] values, bool positive)
    {
        if (values?.Length != 2 || !FormRig.Finite(values[0]) || !FormRig.Finite(values[1]) || values[0] > values[1] || positive && values[0] <= 0)
            throw new InvalidDataException(Id + "：动作调整范围无效");
        return new Vector2(values[0], values[1]);
    }

    /// <summary>一次解析动作已有阶段，不维护另一套时钟。</summary>
    /// <param name="from">开始标记。</param><param name="until">解除标记。</param>
    private Vector2 Interval(string from, string until)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(until)) throw new InvalidDataException(Id + "：接触或支撑缺少阶段标记");
        var result = new Vector2(Motion.MarkerTime(from), Motion.MarkerTime(until));
        if (result.x >= result.y) throw new InvalidDataException(Id + "：阶段区间倒置或为空");
        return result;
    }

    /// <summary>在加载时解析真实挂点。</summary>
    /// <param name="id">挂点编号。</param>
    private int Anchor(string id)
    {
        int index = Array.FindIndex(Resource.Anchors, a => a.Id == id);
        return index >= 0 ? index : throw new InvalidDataException(Id + "：动作缺少实际挂点：" + id);
    }

    /// <summary>创建一次可复用的独占姿态，调整请求必须处于制作范围内。</summary>
    /// <param name="scale">实际模型缩放。</param><param name="rate">播放速度。</param><param name="turn">转向角度。</param>
    public FormActionState Create(float scale = 1f, float rate = 1f, float turn = 0f)
    {
        if (!(scale >= Scale.x && scale <= Scale.y && rate >= Rate.x && rate <= Rate.y && turn >= Turn.x && turn <= Turn.y))
            throw new InvalidDataException(Id + "：请求超出动作调整范围");
        return new FormActionState(this, scale, rate, turn);
    }

    /// <summary>按对方实际部位建立一次接触配对，取样时不查询名称。</summary>
    /// <param name="contact">本方关系编号。</param><param name="partner">对方完整资源。</param><param name="anchor">对方接触挂点。</param>
    public FormActionPartner Bind(string contact, FormResource partner, string anchor) => contacts.TryGetValue(contact ?? "", out var found)
        ? new FormActionPartner(this, found, partner, anchor) : throw new InvalidDataException(Id + "：动作缺少接触关系：" + contact);

    /// <summary>实际支撑点及开始时的真实位置。</summary>
    internal readonly struct Support
    {
        /// <summary>实际挂点。</summary>
        internal readonly FormAnchorBinding Anchor;
        /// <summary>有效阶段。</summary>
        internal readonly Vector2 Interval;
        /// <summary>允许位移误差。</summary>
        internal readonly float Slack;
        /// <summary>开始支撑时的位置。</summary>
        internal readonly Vector3 Reference;
        /// <summary>保存已经核对的支撑数值。</summary>
        /// <param name="anchor">实际挂点。</param><param name="interval">阶段。</param><param name="slack">误差。</param><param name="reference">开始位置。</param>
        internal Support(FormAnchorBinding anchor, Vector2 interval, float slack, Vector3 reference)
        { Anchor = anchor; Interval = interval; Slack = slack; Reference = reference; }
    }

    /// <summary>双方几何接触约定，实际人物控制由玩法维护。</summary>
    internal readonly struct Contact
    {
        /// <summary>本方真实接触挂点。</summary>
        internal readonly FormAnchorBinding Anchor;
        /// <summary>对方部位用途。</summary>
        internal readonly string Role;
        /// <summary>接触及解除阶段。</summary>
        internal readonly Vector2 Interval;
        /// <summary>位置及朝向允许误差。</summary>
        internal readonly float Gap, Angle;
        /// <summary>对方接触帧相对本方的旋转。</summary>
        internal readonly Quaternion Facing;
        /// <summary>保存已解析的接触数值。</summary>
        /// <param name="anchor">本方挂点。</param><param name="role">对方部位。</param><param name="interval">阶段。</param>
        /// <param name="gap">位置误差。</param><param name="angle">朝向误差。</param><param name="opposed">是否相对。</param>
        internal Contact(FormAnchorBinding anchor, string role, Vector2 interval, float gap, float angle, bool opposed)
        { Anchor = anchor; Role = role; Interval = interval; Gap = gap; Angle = angle; Facing = opposed ? Quaternion.Euler(0, 180, 0) : Quaternion.identity; }
    }
}

/// <summary>完整资源持有基础动作；分件不继承整身支撑和双方关系。</summary>
public sealed partial class FormResource
{
    /// <summary>加载时建立的稳定动作索引。</summary>
    private readonly Dictionary<string, FormAction> actions = new(StringComparer.Ordinal);
    /// <summary>取得已经解析的动作。</summary>
    /// <param name="id">稳定动作编号。</param>
    public FormAction Action(string id) => actions.TryGetValue(id ?? "", out var found) ? found : throw new InvalidDataException(Description.Id + "：缺少基础动作：" + id);
    /// <summary>完整结构和挂点建立后一次加载。</summary>
    private void LoadActions()
    {
        if (Description.Actions == null) throw new InvalidDataException(Description.Id + "：缺少基础动作集合");
        foreach (var source in Description.Actions)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Id) || actions.ContainsKey(source.Id))
                throw new InvalidDataException(Description.Id + "：基础动作编号为空或重复");
            actions.Add(source.Id, new FormAction(this, source));
        }
    }
}

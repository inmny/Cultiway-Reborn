using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>单次基础动作的姿态与整体调整，显示和作用读取同一结果。</summary>
public sealed class FormActionState
{
    /// <summary>已解析的共同动作及所属资源版本。</summary>
    public readonly FormAction Action;
    /// <summary>独占并复用的骨架姿态。</summary>
    public readonly FormPose Pose;
    /// <summary>模型尺寸及播放速度。</summary>
    public readonly float Scale, Rate;
    /// <summary>含实际转向、缩放及支撑修正的模型世界变换。</summary>
    public Matrix4x4 Transform { get; private set; }
    /// <summary>实际制作动作时间，各阶段共用。</summary>
    public float LocalTime { get; private set; }
    /// <summary>所有有效支撑是否在各自容差内。</summary>
    public bool Supported { get; private set; }
    /// <summary>实际支撑的最大世界位置误差。</summary>
    public float SupportError { get; private set; }
    /// <summary>请求只在建立时解析为旋转和缩放。</summary>
    private readonly Matrix4x4 adjustment;

    /// <summary>为单次动作分配一次姿态。</summary>
    /// <param name="action">已检查动作。</param><param name="scale">实际尺寸。</param><param name="rate">速度。</param><param name="turn">转向。</param>
    internal FormActionState(FormAction action, float scale, float rate, float turn)
    {
        Action = action; Scale = scale; Rate = rate; Pose = action.Resource.Rig.CreatePose();
        adjustment = Matrix4x4.TRS(Vector3.zero, Quaternion.AngleAxis(turn, action.Up), Vector3.one * scale);
    }

    /// <summary>先取共同姿态，再以真实支撑求整体偏移并核对其余支撑。</summary>
    /// <param name="age">单次动作经过时间。</param><param name="root">调用方提供的位置和方向，不包含模型缩放。</param>
    public void Sample(float age, Matrix4x4 root)
    {
        LocalTime = Mathf.Clamp(age * Rate, 0, Action.Motion.Duration);
        Action.Resource.Rig.Sample(Action.Motion, LocalTime, Pose);
        Vector3 offset = Vector3.zero; bool first = true;
        foreach (var support in Action.Supports)
            if (Active(support.Interval))
            { offset = support.Reference - (Vector3)support.Anchor.Frame(Pose).GetColumn(3); first = false; break; }
        Transform = root * adjustment * Matrix4x4.Translate(offset); SupportError = 0; Supported = true;
        if (first) return;
        foreach (var support in Action.Supports)
            if (Active(support.Interval))
            {
                float error = Vector3.Distance(support.Reference, (Vector3)support.Anchor.Frame(Pose).GetColumn(3) + offset);
                SupportError = Mathf.Max(SupportError, error * Scale); Supported &= error <= support.Slack;
            }
    }

    /// <summary>阶段区间包含开始时刻，在解除时刻立即退出。</summary>
    /// <param name="interval">制作动作中的区间。</param>
    internal bool Active(Vector2 interval) => LocalTime >= interval.x && LocalTime < interval.y;
}

/// <summary>两个实际部位的几何关系；候选对齐不会自行移动游戏人物。</summary>
public sealed class FormActionPartner
{
    /// <summary>保留本方动作版本。</summary>
    private readonly FormAction action;
    /// <summary>对方资源保持存活，不跨版本查询新挂点。</summary>
    public readonly FormResource Resource;
    /// <summary>已解析的接触约定和对方挂点。</summary>
    private readonly FormAction.Contact contact;
    /// <summary>对方实际表面帧。</summary>
    private readonly FormAnchorBinding anchor;
    /// <summary>若为握持，采用完整绕柄方向。</summary>
    private readonly FormGrip grip;

    /// <summary>按对方真实挂点所属部件核对用途。</summary>
    /// <param name="action">本方动作。</param><param name="contact">接触约定。</param><param name="resource">对方资源。</param><param name="id">对方挂点。</param>
    internal FormActionPartner(FormAction action, FormAction.Contact contact, FormResource resource, string id)
    {
        this.action = action; this.contact = contact; Resource = resource;
        int index = Array.FindIndex(resource.Anchors, a => a.Id == id);
        if (index < 0 || !resource.Structure.Parts(contact.Role).Contains(resource.Description.Anchors[index].Part))
            throw new InvalidDataException(action.Id + "：对方挂点不属于所需实际部位：" + contact.Role + "/" + id);
        anchor = resource.Anchors[index];
        var source = resource.Description.Grips.FirstOrDefault(g => g.Anchor == id);
        grip = source == null ? null : resource.Grip(source.Id);
    }

    /// <summary>取保留版本中的完整对方接触帧。</summary>
    /// <param name="pose">对方同一姿态。</param>
    private Matrix4x4 Frame(FormPose pose) => grip == null ? anchor.Frame(pose) : grip.Frame(pose);

    /// <summary>本方真实接触帧与对方应有朝向。</summary>
    /// <param name="state">本次动作状态。</param>
    private Matrix4x4 Socket(FormActionState state)
    {
        var posed = state.Transform * contact.Anchor.Frame(state.Pose);
        return Matrix4x4.TRS(posed.GetColumn(3), Quaternion.LookRotation(posed.GetColumn(2), posed.GetColumn(1)) * contact.Facing, Vector3.one);
    }

    /// <summary>从同一动作时间检查接触窗口和支撑。</summary>
    /// <param name="state">属于配对动作的独占状态。</param>
    public bool Active(FormActionState state) => ReferenceEquals(state.Action, action) && state.Supported && state.Active(contact.Interval);

    /// <summary>生成几何对齐候选；人物能否到达和维持由实际玩法决定。</summary>
    /// <param name="state">本方当前状态。</param><param name="scale">对方实际模型尺寸。</param>
    /// <param name="placement">候选模型世界变换。</param><param name="pose">对方同一骨架姿态。</param>
    public bool TryAlign(FormActionState state, float scale, out Matrix4x4 placement, FormPose pose = null)
    {
        placement = default;
        if (!Active(state)) return false;
        placement = Socket(state) * Matrix4x4.Scale(Vector3.one * scale) * Frame(pose).inverse;
        return true;
    }

    /// <summary>以双方实际姿态检查距离与方向，不通过拉伸身体冒充触达。</summary>
    /// <param name="state">本方当前状态。</param><param name="placement">对方实际模型世界变换。</param>
    /// <param name="gap">真实世界间距。</param><param name="angle">完整朝向偏差。</param><param name="pose">对方同一姿态。</param>
    public bool Matches(FormActionState state, Matrix4x4 placement, out float gap, out float angle, FormPose pose = null)
    {
        var expected = Socket(state); var actual = placement * Frame(pose);
        gap = Vector3.Distance(expected.GetColumn(3), actual.GetColumn(3));
        angle = Quaternion.Angle(expected.rotation, Quaternion.LookRotation(actual.GetColumn(2), actual.GetColumn(1)));
        return Active(state) && gap <= contact.Gap && angle <= contact.Angle;
    }
}

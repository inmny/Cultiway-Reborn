using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>实际身体姿态上的接触位置，不另造肢体。</summary>
public enum BodyContactPart
{
    /// <summary>实际手掌。</summary>
    Hand = 0,

    /// <summary>两段实际臂骨连接处。</summary>
    Elbow = 1,

    /// <summary>上臂与躯干连接处。</summary>
    Shoulder = 2,

    /// <summary>真实腿链的膝关节。</summary>
    Knee = 3,

    /// <summary>真实腿链的足部。</summary>
    Foot = 4,

    /// <summary>真实头部关节及头形。</summary>
    Head = 5,

    /// <summary>已拥有且实际可见的非人器官。</summary>
    Organ = 6
}

/// <summary>编译时解析的实际部位；名称、控制占用和接触位置共用一份选择。</summary>
public sealed class BodyPartSelection
{
    /// <summary>实际部位编号。</summary>
    public string Id { get; }

    /// <summary>本方实际左侧肢体。</summary>
    public bool Left { get; }

    /// <summary>身体上的实际接触部位。</summary>
    public BodyContactPart Contact { get; }

    /// <summary>实际需要的肢体占用。</summary>
    public BodyOccupancy Occupancy =>
        IsHead ? BodyOccupancy.Head : !IsArm ? BodyOccupancy.Stance : Left ? BodyOccupancy.LeftArm : BodyOccupancy.RightArm;

    /// <summary>是否驱动完整腿链。</summary>
    public bool IsLeg => Contact is BodyContactPart.Knee or BodyContactPart.Foot;

    /// <summary>是否驱动手臂及其实际掌形。</summary>
    public bool IsArm => Contact is BodyContactPart.Hand or BodyContactPart.Elbow or BodyContactPart.Shoulder;

    /// <summary>是否驱动实际头部。</summary>
    public bool IsHead => Contact == BodyContactPart.Head;

    /// <summary>是否控制已有独立器官。</summary>
    public bool IsOrgan => Contact == BodyContactPart.Organ;

    /// <summary>所选器官的稳定编号，其他部位为空。</summary>
    public string OrganId { get; }

    /// <summary>已有窗口使用的本地化字段。</summary>
    public string Label => Contact switch
    {
        BodyContactPart.Elbow => Left ? "LeftElbow" : "RightElbow",
        BodyContactPart.Shoulder => Left ? "LeftShoulder" : "RightShoulder",
        BodyContactPart.Knee => Left ? "LeftKnee" : "RightKnee",
        BodyContactPart.Foot => Left ? "LeftFoot" : "RightFoot",
        BodyContactPart.Head => "Head",
        BodyContactPart.Organ => "Organ",
        _ => Left ? "LeftHand" : "RightHand"
    };

    /// <summary>实际身体接触形状的名称。</summary>
    public string Shape =>
        Contact switch
        {
            BodyContactPart.Elbow => "elbow", BodyContactPart.Shoulder => "shoulder",
            BodyContactPart.Knee => "knee", BodyContactPart.Foot => "foot", BodyContactPart.Head => "head",
            BodyContactPart.Organ => "organ", _ => "palm"
        };

    /// <summary>只在编译入口解析已提供的身体映射，未提供的部位明确拒绝。</summary>
    /// <param name="id">真实部位编号。</param>
    internal BodyPartSelection(string id)
    {
        Id = id;
        if (id != null && id.StartsWith("organ:", System.StringComparison.Ordinal) && id.Length > 6)
        {
            Contact = BodyContactPart.Organ;
            OrganId = id.Substring(6);
            return;
        }

        (Left, Contact) = id switch
        {
            "hand.right" => (false, BodyContactPart.Hand),
            "hand.left" => (true, BodyContactPart.Hand),
            "elbow.right" => (false, BodyContactPart.Elbow),
            "elbow.left" => (true, BodyContactPart.Elbow),
            "shoulder.right" => (false, BodyContactPart.Shoulder),
            "shoulder.left" => (true, BodyContactPart.Shoulder),
            "knee.right" => (false, BodyContactPart.Knee),
            "knee.left" => (true, BodyContactPart.Knee),
            "foot.right" => (false, BodyContactPart.Foot),
            "foot.left" => (true, BodyContactPart.Foot),
            "head" => (false, BodyContactPart.Head),
            _ => throw new InvalidDataException("Body/Effector：尚无该部位的实际动作映射：" + id)
        };
    }
}

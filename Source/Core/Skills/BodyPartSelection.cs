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
    Organ = 6,

    /// <summary>原身体实际骨段及其已提供作用区域。</summary>
    Joint = 7
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
        IsOrgan ? BodyOccupancy.None : IsHead ? BodyOccupancy.Head : !IsArm ? BodyOccupancy.Stance : Left ? BodyOccupancy.LeftArm : BodyOccupancy.RightArm;

    /// <summary>独立器官使用真实稳定名称取得控制，不挤占人类手臂或全身槽。</summary>
    public string ControlPath => IsOrgan || IsJoint ? Id : null;

    /// <summary>从原身体骨段取得动作，不能生成替代肢体。</summary>
    public bool IsJoint => Contact == BodyContactPart.Joint;

    /// <summary>采用实际根部三轴曲线的器官或原骨段。</summary>
    internal bool HasPartRotation => IsOrgan || IsJoint;

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

    /// <summary>实际器官内的命名部件；空值控制整器官。</summary>
    public string OrganPart { get; }

    /// <summary>真实器官及可选内部部件的完整资源路径。</summary>
    public string OrganPath => IsOrgan ? Id.Substring(6) : null;

    /// <summary>已有窗口使用的本地化字段。</summary>
    public string Label => Contact switch
    {
        BodyContactPart.Elbow => Left ? "LeftElbow" : "RightElbow",
        BodyContactPart.Shoulder => Left ? "LeftShoulder" : "RightShoulder",
        BodyContactPart.Knee => Left ? "LeftKnee" : "RightKnee",
        BodyContactPart.Foot => Left ? "LeftFoot" : "RightFoot",
        BodyContactPart.Head => "Head",
        BodyContactPart.Organ => "Organ",
        BodyContactPart.Joint => "Joint",
        _ => Left ? "LeftHand" : "RightHand"
    };

    /// <summary>实际身体接触形状的名称。</summary>
    public string Shape =>
        Contact switch
        {
            BodyContactPart.Elbow => "elbow", BodyContactPart.Shoulder => "shoulder",
            BodyContactPart.Knee => "knee", BodyContactPart.Foot => "foot", BodyContactPart.Head => "head",
            BodyContactPart.Organ => "organ", BodyContactPart.Joint => "joint", _ => "palm"
        };

    /// <summary>只在编译入口解析已提供的身体映射，未提供的部位明确拒绝。</summary>
    /// <param name="id">真实部位编号。</param>
    internal BodyPartSelection(string id)
    {
        Id = id;
        if (id != null && id.StartsWith("part:", System.StringComparison.Ordinal) && id.Length > 5)
        {
            Contact = BodyContactPart.Joint;
            return;
        }
        if (id != null && id.StartsWith("organ:", System.StringComparison.Ordinal) && id.Length > 6)
        {
            Contact = BodyContactPart.Organ;
            int split = id.IndexOf('/', 6);
            OrganId = split < 0 ? id.Substring(6) : id.Substring(6, split - 6);
            OrganPart = split < 0 ? null : id.Substring(split + 1);
            if (OrganId.Length == 0 || OrganPart != null && (OrganPart.Length == 0 || OrganPart.Contains("/")))
                throw new InvalidDataException("Body/Effector：器官或实际部件名称无效：" + id);
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

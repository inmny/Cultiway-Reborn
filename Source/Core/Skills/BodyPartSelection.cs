using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>同一实际手臂姿态上的接触位置，不另造一只肢体。</summary>
public enum BodyArmContact
{
    /// <summary>实际手掌。</summary>
    Hand = 0,

    /// <summary>两段实际臂骨连接处。</summary>
    Elbow = 1,

    /// <summary>上臂与躯干连接处。</summary>
    Shoulder = 2
}

/// <summary>编译时解析的实际部位；名称、控制占用和接触位置共用一份选择。</summary>
public sealed class BodyPartSelection
{
    /// <summary>实际部位编号。</summary>
    public string Id { get; }

    /// <summary>本方实际左侧手臂。</summary>
    public bool Left { get; }

    /// <summary>同一臂链上的实际接触点。</summary>
    public BodyArmContact Contact { get; }

    /// <summary>实际需要的肢体占用。</summary>
    public BodyOccupancy Occupancy => Left ? BodyOccupancy.LeftArm : BodyOccupancy.RightArm;

    /// <summary>已有窗口使用的本地化字段。</summary>
    public string Label => Contact switch
    {
        BodyArmContact.Elbow => Left ? "LeftElbow" : "RightElbow",
        BodyArmContact.Shoulder => Left ? "LeftShoulder" : "RightShoulder",
        _ => Left ? "LeftHand" : "RightHand"
    };

    /// <summary>实际身体接触形状的名称。</summary>
    public string Shape =>
        Contact switch { BodyArmContact.Elbow => "elbow", BodyArmContact.Shoulder => "shoulder", _ => "palm" };

    /// <summary>只在编译入口解析已提供的身体映射，未提供的部位明确拒绝。</summary>
    /// <param name="id">真实部位编号。</param>
    internal BodyPartSelection(string id)
    {
        Id = id;
        (Left, Contact) = id switch
        {
            "hand.right" => (false, BodyArmContact.Hand),
            "hand.left" => (true, BodyArmContact.Hand),
            "elbow.right" => (false, BodyArmContact.Elbow),
            "elbow.left" => (true, BodyArmContact.Elbow),
            "shoulder.right" => (false, BodyArmContact.Shoulder),
            "shoulder.left" => (true, BodyArmContact.Shoulder),
            _ => throw new InvalidDataException("Body/Effector：尚无该部位的实际动作映射：" + id)
        };
    }
}
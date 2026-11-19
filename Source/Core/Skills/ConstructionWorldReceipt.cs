using System;
using Cultiway.Core.Combat.Tactical;
using System.Collections.Generic;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际世界提交的结果种类，不把数值预测当成提交。</summary>
public enum ConstructionWorldResultKind
{
    /// <summary>真实材料已由世界接受。</summary>
    Transfer,
    /// <summary>实际范围材料选择和转换已经处理。</summary>
    Matter,
    /// <summary>一波真实水路传播及作用已经处理。</summary>
    Conduction,
    /// <summary>原支付通道已经处理资源返还。</summary>
    Recovery,
    /// <summary>真实库存物料已经消耗。</summary>
    Inventory,
    /// <summary>世界材料的独立侵蚀已经得到实际结果。</summary>
    Erosion,
    /// <summary>实际生灵到达经过确认的出口。</summary>
    ActorTransfer,
    /// <summary>本次借用的原法宝实际抵达出口。</summary>
    ArtifactTransfer,
    /// <summary>选中的现有材料过程已改变时间安排。</summary>
    ErosionRetimed,
    /// <summary>指定人物实际收到新讯号。</summary>
    SignalDelivered
}

/// <summary>一次确认的不可变回执，空选择也保留真实的零结果。</summary>
public sealed class ConstructionWorldReceipt
{
    /// <summary>真实处理的类型。</summary>
    public ConstructionWorldResultKind Kind { get; }
    /// <summary>实际部位与同轮序号。</summary>
    public int Node { get; }
    /// <summary>导电波序号，其余为一次确认序号。</summary>
    public int Sequence { get; }
    /// <summary>确认的真实世界时点。</summary>
    public double WorldTime { get; }
    /// <summary>直接送达的内容；其他回执为空。</summary>
    public ConstructionSignalKind? Signal { get; }
    /// <summary>送达时冻结的亲历方位；没有观察内容的讯号留空。</summary>
    public CombatReportedContact? Observation { get; }
    /// <summary>直接送达的实际发送者；其他回执为零。</summary>
    public long SenderId { get; }
    /// <summary>真实转交或转换记录编号，不保存对象引用。</summary>
    public IReadOnlyList<long> Materials { get; }
    /// <summary>整份材料的身份，跨分区和流动时仍可供后段准确选择。</summary>
    public IReadOnlyList<long> MaterialIdentities { get; }
    /// <summary>实际进入伤害接口的目标编号。</summary>
    public IReadOnlyList<long> Targets { get; }
    /// <summary>实际输出材料的体积，侵蚀输出为尚存余量。</summary>
    public double Volume { get; }
    /// <summary>输出材料尚持有的支付份额。</summary>
    public double Resource { get; }
    /// <summary>本次独立侵蚀已经移除的真实体积。</summary>
    public double ErodedVolume { get; }
    /// <summary>本次侵蚀随材料退出的原本金。</summary>
    public double ErodedResource { get; }
    /// <summary>原资源的本次返还请求量及实际入账量。</summary>
    public double Requested { get; }
    /// <summary>原资源通道实际接受的数量。</summary>
    public double Granted { get; }
    /// <summary>实际到达的水格数。</summary>
    public int Cells { get; }
    /// <summary>实际消耗及当前余力。</summary>
    public float Spent { get; }
    /// <summary>确认后的尚存余力。</summary>
    public float Remaining { get; }
    /// <summary>实际消耗或搬运的原物件稳定编号。</summary>
    public IReadOnlyList<string> Items { get; }
    /// <summary>实际转移落点；其他世界操作没有落点。</summary>
    public Vector3? Destination { get; }
    /// <summary>实际消耗的整件数量。</summary>
    public int ItemCount => Items.Count;
    /// <summary>同一步骤的先前确认，仅供内部保留事实链。</summary>
    internal readonly ConstructionWorldReceipt Previous;

    /// <summary>在主线程确认边界复制编号，之后可交给纯数值查询。</summary>
    /// <param name="kind">结果类型。</param><param name="node">实际部位。</param><param name="clock">真实时点。</param>
    /// <param name="sequence">同轮序号。</param><param name="materials">实际世界记录。</param><param name="targets">实际目标。</param>
    /// <param name="volume">实际体积。</param><param name="resource">转交支付份额。</param><param name="requested">返还请求。</param><param name="granted">实际入账。</param>
    /// <param name="cells">到达水格。</param><param name="spent">真实消耗。</param><param name="remaining">当前余力。</param><param name="previous">已确认前项。</param>
    /// <param name="erodedVolume">本段侵蚀的实际体积损失。</param><param name="erodedResource">本段侵蚀退出的本金。</param>
    /// <param name="materialIdentities">整份材料身份，留置时与首次记录编号相同。</param>
    /// <param name="destination">已经确认的实际转移落点。</param>
    /// <param name="signal">实际登记的讯号内容。</param><param name="senderId">实际发信人。</param>
    internal ConstructionWorldReceipt(ConstructionWorldResultKind kind, int node, double clock, int sequence = 1, long[] materials = null, long[] targets = null,
        double volume = 0, double resource = 0, double requested = 0, double granted = 0, int cells = 0, float spent = 0, float remaining = 0, ConstructionWorldReceipt previous = null, long[] materialIdentities = null, string[] items = null, double erodedVolume = 0, double erodedResource = 0, Vector3? destination = null,
        ConstructionSignalKind? signal = null, long senderId = 0, CombatReportedContact? observation = null)
    {
        Kind = kind; Node = node; WorldTime = clock; Sequence = sequence; Destination = destination;
        Signal = signal; SenderId = senderId; Observation = observation;
        Items = Array.AsReadOnly(items == null ? Array.Empty<string>() : (string[])items.Clone());
        Materials = Array.AsReadOnly(materials == null ? Array.Empty<long>() : (long[])materials.Clone());
        MaterialIdentities = Array.AsReadOnly((long[])(materialIdentities ?? materials ?? Array.Empty<long>()).Clone());
        Targets = Array.AsReadOnly(targets == null ? Array.Empty<long>() : (long[])targets.Clone());
        ErodedVolume = erodedVolume; ErodedResource = erodedResource;
        Volume = volume; Resource = resource; Requested = requested; Granted = granted; Cells = cells; Spent = spent; Remaining = remaining; Previous = previous;
    }

    /// <summary>同一步骤串联不可变确认，共用已经复制的编号集合。</summary>
    /// <param name="source">新确认事实。</param><param name="previous">此前已确认事实。</param>
    /// <param name="node">需要补充的实际部位，负一保留原值。</param>
    internal ConstructionWorldReceipt(ConstructionWorldReceipt source, ConstructionWorldReceipt previous, int node = -1)
    {
        Items = source.Items; Destination = source.Destination; ErodedVolume = source.ErodedVolume; ErodedResource = source.ErodedResource;
        Kind = source.Kind; Node = node < 0 ? source.Node : node; WorldTime = source.WorldTime; Sequence = source.Sequence;
        Signal = source.Signal; SenderId = source.SenderId; Observation = source.Observation;
        Materials = source.Materials; MaterialIdentities = source.MaterialIdentities; Targets = source.Targets; Volume = source.Volume; Resource = source.Resource;
        Requested = source.Requested; Granted = source.Granted; Cells = source.Cells; Spent = source.Spent; Remaining = source.Remaining; Previous = previous;
    }
}

using System;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>独立世界材料的正式保存，不包含施放、Unity对象或工作任务。</summary>
public sealed class WorldMaterialData
{
    /// <summary>世界内的稳定编号。</summary>
    public long Id;

    /// <summary>同一材料在不同区域的稳定来源链。</summary>
    public long Lineage;

    /// <summary>是否已经属于共同世界格网。</summary>
    public bool WorldGrid;

    /// <summary>原支付者和当前控制者。</summary>
    public long Provider, Controller;

    /// <summary>原支付资源种类。</summary>
    public string Resource;

    /// <summary>保存时已经确认的世界时间，恢复后不补算读档等待时间。</summary>
    public double Clock;

    /// <summary>有限材料的剩余秒数，零表示持续保留。</summary>
    public double Remaining;

    /// <summary>当前材料的实际数值。</summary>
    public AbilityMaterialData Material;

    /// <summary>共同水层同次回收的账本编号，零为原有单记录收束。</summary>
    public long RecoveryGroup;

    /// <summary>已经独立开始的收束。</summary>
    public bool Recovering;

    /// <summary>收束历时、效率及已经完成比例。</summary>
    public float Seconds, Efficiency, Progress;

    /// <summary>收束起始的真实材料和资源份额。</summary>
    public double InitialVolume, InitialResource;

    /// <summary>已经确认的返还请求与因精度未能表示的余额。</summary>
    public double Confirmed, Fraction;
}

/// <summary>世界材料的纯数值记录，推进时不访问人物和Unity资源。</summary>
internal sealed class WorldMaterialRecord
{
    /// <summary>稳定身份和归属。</summary>
    internal long Id, Provider, Controller;

    /// <summary>跨区域仍保持同一来源。</summary>
    internal long Lineage;

    /// <summary>共同格网由世界通量统一推进。</summary>
    internal bool WorldGrid;

    /// <summary>原资源种类。</summary>
    internal string Resource;

    /// <summary>独占材料状态。</summary>
    internal AbilityMaterial Material;

    /// <summary>已经完成的时间与有限结束时点；零为持续保留。</summary>
    internal double Clock, Expires;

    /// <summary>内部区域共用的回收过程。</summary>
    internal long RecoveryGroup;

    /// <summary>当前收束参数和进度。</summary>
    internal bool Recovering;

    /// <summary>已接受的独立过程历时、效率和进度。</summary>
    internal float Seconds, Efficiency, Progress;

    /// <summary>原始份额及已经入账的累计请求。</summary>
    internal double InitialVolume, InitialResource, Confirmed, Fraction;

    /// <summary>数值过程本轮已经结束，主线程随后移除。</summary>
    internal bool Finished;

    /// <summary>接受一次真正的跨施放回收，不能重新取得同一材料。</summary>
    /// <param name="recovery">普通法术已经检查的收束参数。</param>
    internal void Begin(CompiledRecovery recovery)
    {
        Recovering = true;
        Seconds = recovery.Seconds;
        Efficiency = recovery.Efficiency;
        Progress = 0;
        InitialVolume = Material.Volume;
        InitialResource = Material.Resource;
        Confirmed = Fraction = 0;
    }

    /// <summary>一个有限时间片，纯数值可交给现有工作池。</summary>
    /// <param name="time">本轮目标时间。</param><param name="terrain">冻结的共同地表。</param>
    internal void Step(double time, AbilityHeightField terrain)
    {
        float step = (float)Math.Min(1d / 30, Math.Max(0, time - Clock));
        if (Expires > 0) step = Mathf.Min(step, (float)Math.Max(0, Expires - Clock));
        if (Recovering)
        {
            float next = Mathf.Min(1, Progress + step / Seconds);
            Material.Retain(Progress < 1 ? (1 - next) / (1 - Progress) : 0);
            Progress = next;
            Material.Resource = InitialResource * (1 - Progress);
            if (Material.Flow == null) Material.Volume = InitialVolume * (1 - Progress);
            if (Progress >= 1) Finished = true;
        }
        else Material.AdvanceBounded(step, terrain, Material.Flow?.Settings.OpenBoundary == true);

        Clock += step;
        if (Expires > 0 && Clock >= Expires || !Recovering && Material.Volume < .000001) Finished = true;
    }

    /// <summary>当前数值序列化；已入账部分不在读档时重放。</summary>
    internal WorldMaterialData Capture() => new()
    {
        Id = Id, Lineage = Lineage, WorldGrid = WorldGrid, RecoveryGroup = RecoveryGroup,
        Provider = Provider,
        Controller = Controller,
        Resource = Resource,
        Clock = Clock,
        Remaining = Expires > 0 ? Math.Max(double.Epsilon, Expires - Clock) : 0,
        Material = Material.Capture(),
        Recovering = Recovering,
        Seconds = Seconds,
        Efficiency = Efficiency,
        Progress = Progress,
        InitialVolume = InitialVolume,
        InitialResource = InitialResource,
        Confirmed = Confirmed,
        Fraction = Fraction
    };
}

/// <summary>一次世界材料确认，按稳定编号记录真实体积和原资源入账。</summary>
public readonly struct WorldMaterialChange
{
    /// <summary>真正变化的稳定记录。</summary>
    public readonly long Id;

    /// <summary>本次接受事件对应的施放编号；独立推进事件为零，不进入存档。</summary>
    public readonly long CastId;

    /// <summary>本次接受对应的构造编号。</summary>
    public readonly string AbilityId;

    /// <summary>原支付者。</summary>
    public readonly long Provider;

    /// <summary>接受留置、开始收束、实际回能或结束。</summary>
    public readonly string Kind;

    /// <summary>确认时的实际剩余体积及本次入账。</summary>
    public readonly double Volume, Granted;

    /// <summary>只在主线程确认后构建。</summary>
    /// <param name="row">实际记录。</param><param name="kind">确认种类。</param><param name="granted">本次真实入账。</param>
    /// <param name="castId">接受事件对应的临时施放编号，其余为零。</param>
    /// <param name="abilityId">接受事件对应的实际构造，其余为空。</param>
    internal WorldMaterialChange(WorldMaterialRecord row, string kind, double granted = 0, long castId = 0,
        string abilityId = null)
    {
        CastId = castId;
        AbilityId = abilityId;
        Id = row.Id;
        Provider = row.Provider;
        Kind = kind;
        Volume = row.Material.Volume;
        Granted = granted;
    }
}
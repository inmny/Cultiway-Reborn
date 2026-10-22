using System;
using System.IO;
using System.ComponentModel;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>从真实接触形状进入连通水域的有限放电过程。</summary>
public sealed class ConstructionConduction
{
    /// <summary>总放电波数，含首次；旧资料默认一次。</summary>
    [DefaultValue(1), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Pulses = 1;

    /// <summary>相邻波的世界秒间隔，不使用画面帧数计时。</summary>
    [DefaultValue(.4f), JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public float Interval = .4f;

    /// <summary>沿真实水路最多传播的地图格数。</summary>
    public float Range = 24;

    /// <summary>允许导电的最低真实水深。</summary>
    public float MinimumDepth = .02f;

    /// <summary>每传播一地图格实际消耗的力量。</summary>
    public float TravelCost = .1f;

    /// <summary>每个不同人物消耗的完整力量，再由原有倍率和抗性结算。</summary>
    public float DamagePerTarget = 5;

    /// <summary>最多到达的实际水格数。</summary>
    public int MaximumCells = 512;

    /// <summary>最多作用的不同人物数。</summary>
    public int MaximumTargets = 16;
}

/// <summary>仅在构造资料边界检查的传播参数，计算期间不读取可编辑对象。</summary>
internal sealed class CompiledConduction
{
    /// <summary>已检查的独立参数副本。</summary>
    internal readonly float Range, MinimumDepth, TravelCost, DamagePerTarget;

    /// <summary>总波数和相邻波的世界秒间隔。</summary>
    internal readonly int Pulses;

    /// <summary>相邻波间隔及首波至末波的总跨度。</summary>
    internal readonly float Interval, Span;

    /// <summary>同部位操作的原资料先后顺序。</summary>
    internal readonly int Order;

    /// <summary>明确的本次格数及人物上限。</summary>
    internal readonly int MaximumCells, MaximumTargets;

    /// <summary>复制及检查有效传播、消耗和上限。</summary>
    /// <param name="source">可选普通参数。</param><param name="path">资料错误位置。</param><param name="order">原资料顺序。</param>
    internal CompiledConduction(ConstructionConduction source, string path, int order = 0)
    {
        source ??= new ConstructionConduction();
        bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
        if (source.Pulses < 1 || source.Pulses > 256 || !Positive(source.Interval) ||
            source.Interval < 1f / 60f || source.Interval > 3600)
            throw new InvalidDataException(path + "：导电需要1至256波，间隔须在1/60至3600世界秒内");
        if (!Positive(source.Range) || source.Range > 4096 || !Positive(source.MinimumDepth) ||
            source.MinimumDepth > 100 ||
            !Positive(source.TravelCost) || source.TravelCost > 1000 || !Positive(source.DamagePerTarget) ||
            source.DamagePerTarget < 1 || source.DamagePerTarget > 1000000 ||
            source.MaximumCells < 1 || source.MaximumCells > 262144 || source.MaximumTargets < 1 ||
            source.MaximumTargets > 65536)
            throw new InvalidDataException(path + "：导电需要正数距离、水深和沿途消耗，人物消耗须至少为1，格数须在1至262144，人物数须在1至65536内");
        Pulses = source.Pulses;
        Interval = source.Interval;
        Span = (Pulses - 1) * Interval;
        Order = order;
        Range = source.Range;
        MinimumDepth = source.MinimumDepth;
        TravelCost = source.TravelCost;
        DamagePerTarget = source.DamagePerTarget;
        MaximumCells = source.MaximumCells;
        MaximumTargets = source.MaximumTargets;
    }
}

/// <summary>一轮有限放电的纯数值时序，随所属部位或操作一起预测和回退。</summary>
internal struct ConductionRun
{
    /// <summary>是否已经由定时或真实事件启动。</summary>
    internal bool Started;

    /// <summary>已经发出的波数及尚未领取的确认数。</summary>
    internal int Emitted, Pending;

    /// <summary>首波所在的施放年龄，不逐次累加间隔。</summary>
    internal float StartedAt;

    /// <summary>启动一次有限过程；调用者已经检查此操作尚未启动。</summary>
    /// <param name="at">首次实际执行年龄。</param>
    internal void Start(float at)
    {
        Started = true;
        StartedAt = at;
    }

    /// <summary>下一波的准确年龄；未启动或已结束不建立新时间边界。</summary>
    /// <param name="settings">不可变的本轮配置。</param>
    internal float NextAt(CompiledConduction settings) => !Started || Emitted >= settings.Pulses
        ? float.PositiveInfinity
        : StartedAt + Emitted * settings.Interval;

    /// <summary>记录已经到时的有限波数，领取后不重复，数值回退可完整恢复。</summary>
    /// <param name="age">共同确认前的数值年龄。</param><param name="settings">已检查配置。</param>
    internal void Tick(float age, CompiledConduction settings)
    {
        while (Emitted < settings.Pulses && NextAt(settings) <= age)
        {
            Emitted++;
            Pending++;
        }
    }
}
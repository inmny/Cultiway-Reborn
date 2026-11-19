using System;
using System.Collections.Generic;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>由玩法登记的实际材料及物态，显示和伤害规则各自明确。</summary>
public sealed class AbilityMatter
{
    /// <summary>稳定材料编号。</summary>
    public readonly string Id;
    /// <summary>稳定物态编号。</summary>
    public readonly string State;
    /// <summary>来源玩法提供的实际伤害规则编号。</summary>
    public readonly string DamageKind;
    /// <summary>主体材料表面，空值保留资源自身表面。</summary>
    public readonly string Surface;
    /// <summary>在明确换材的部位使用的表面；兼容旧模型自行提供的默认表面。</summary>
    internal readonly string EditedSurface;
    /// <summary>形成后是否保持当前内部形状。</summary>
    public readonly bool HoldsShape;

    /// <summary>此材料明确提供有限燃烧规则，显示染色不能改变它。</summary>
    public readonly bool Combustible;

    /// <summary>已登记的实体侵蚀系数，零表示尚不提供此规则。</summary>
    public readonly float ErosionFactor;

    /// <summary>是否作为该材料未指定物态时的默认值。</summary>
    internal readonly bool IsDefault;

    /// <summary>登记者明确提供材料规则，计算阶段只读取不可变数值及编号。</summary>
    /// <param name="id">材料编号。</param>
    /// <param name="state">物态编号。</param>
    /// <param name="damageKind">实际伤害规则。</param>
    /// <param name="surface">主体显示表面。</param>
    /// <param name="holdsShape">是否保持内部形状。</param>
    /// <param name="isDefault">是否为材料默认物态。</param>
    /// <param name="combustible">是否明确可燃。</param>
    /// <param name="erosionFactor">已实现的实体侵蚀系数。</param>
    /// <param name="editedSurface">明确换材时的可选表面；缺省沿用材料表面。</param>
    public AbilityMatter(string id, string state, string damageKind, string surface = null, bool holdsShape = false,
        bool isDefault = true, bool combustible = false, float erosionFactor = 0, string editedSurface = null)
    {
        if (float.IsNaN(erosionFactor) || float.IsInfinity(erosionFactor) || erosionFactor < 0 || erosionFactor > 1)
            throw new ArgumentOutOfRangeException(nameof(erosionFactor));
        Id = id;
        State = state;
        DamageKind = damageKind;
        Surface = surface;
        EditedSurface = editedSurface ?? surface;
        HoldsShape = holdsShape;
        IsDefault = isDefault;
        Combustible = combustible;
        ErosionFactor = erosionFactor;
    }
}

/// <summary>材料规则由内容层在初始化时登记，编译时一次解析；工作线程不查表。</summary>
public static class AbilityMatters
{
    /// <summary>当前已经实现的实际材料与物态，供共用改造入口选择。</summary>
    public static IEnumerable<AbilityMatter> All => entries.Values;
    /// <summary>明确提供的材料和物态组合。</summary>
    private static readonly Dictionary<(string, string), AbilityMatter> entries = new();
    /// <summary>材料的默认物态。</summary>
    private static readonly Dictionary<string, string> defaults = new(StringComparer.Ordinal);

    /// <summary>登记已经实现的规则，重复编号直接报告初始化错误。</summary>
    /// <param name="matter">玩法明确实现的组合。</param>
    public static void Register(AbilityMatter matter)
    {
        entries.Add((matter.Id, matter.State), matter);
        if (matter.IsDefault) defaults.Add(matter.Id, matter.State);
    }

    /// <summary>在资料边界解析，缺项不会凭颜色或名称猜测。</summary>
    /// <param name="id">实际材料。</param>
    /// <param name="state">明确物态，空值使用材料默认。</param>
    /// <param name="path">构造中的具体位置。</param>
    public static AbilityMatter Resolve(string id, string state, string path)
    {
        if (TryResolve(id, state, out AbilityMatter value)) return value;
        string key = state == null ? id : id + "/" + state;
        throw ConstructionReferences.Error(path, "未提供实际材料或物态：" + key,
            resourceKind: "Matter", resourceId: key);
    }

    /// <summary>取已登记的实际材料；编辑缺失引用时可保留原名等待修正。</summary>
    /// <param name="id">实际材料。</param><param name="state">明确物态，空值使用默认。</param>
    /// <param name="value">登记的材料及物态。</param>
    internal static bool TryResolve(string id, string state, out AbilityMatter value)
    {
        value = null;
        return !string.IsNullOrEmpty(id) && defaults.TryGetValue(id, out string standard) &&
               entries.TryGetValue((id, state ?? standard), out value);
    }
}

/// <summary>物态变化的实际触发，不将选中目标当成真实接触。</summary>
public enum ConstructionChangeTrigger
{
    /// <summary>到达明确的施放世界时点。</summary>
    Time = 0,

    /// <summary>本部位真正作用于人物之后。</summary>
    ActorContact = 1,

    /// <summary>本部位完成形体相撞或反射之后。</summary>
    Clash = 2,

    /// <summary>部位真正结束，或实际身体自然完成收势；整次取消不触发。</summary>
    End = 3,

    /// <summary>实际材料及物态已改变，不把染色当作变化。</summary>
    MatterChanged = 4,

    /// <summary>真实交锋后，剩余作用量向下越过明确比例。</summary>
    ForceBelow = 5,

    /// <summary>真实长大过程向上达到明确局部尺寸。</summary>
    SizeAbove = 6,

    /// <summary>真实收缩过程向下达到明确局部尺寸。</summary>
    SizeBelow = 7,

    /// <summary>实际作用面触及地块，不要求改变地形或命中人物。</summary>
    WorldContact = 8,

    /// <summary>距本次飞行起点的实际空间距离向外达到阈值。</summary>
    DistanceFromFlightStart = 9,

    /// <summary>普通地波实际抵达满足路径距离的可作用地块。</summary>
    GroundRouteDistance = 10,

    /// <summary>已经确认的导电脉冲实际抵达水格。</summary>
    WaterRouteArrival = 11,

    /// <summary>真实收件人实际登记或更换讯号来源。</summary>
    SignalDelivery = 12,

    /// <summary>明确交接成功，普通到期、取消和耗尽不产生此事件。</summary>
    Handoff = 13
}

/// <summary>已经解析的有限物态变化；每次施放每项最多执行一次。</summary>
internal readonly struct CompiledMatterChange
{
    /// <summary>最早允许发生的施放时点。</summary>
    internal readonly float At;
    /// <summary>实际触发种类。</summary>
    internal readonly ConstructionChangeTrigger Trigger;
    /// <summary>明确输出的材料及物态。</summary>
    internal readonly AbilityMatter Matter;

    /// <summary>可选的世界材料转换规则。</summary>
    internal readonly CompiledWorldMatter WorldMatter;

    /// <summary>记录编译完成的规则。</summary>
    /// <param name="at">最早时点。</param>
    /// <param name="trigger">触发种类。</param>
    /// <param name="matter">输出状态。</param><param name="worldMatter">可选世界材料选择。</param>
    internal CompiledMatterChange(float at, ConstructionChangeTrigger trigger, AbilityMatter matter,
        CompiledWorldMatter worldMatter = null)
    {
        At = at;
        Trigger = trigger;
        Matter = matter;
        WorldMatter = worldMatter;
    }
}

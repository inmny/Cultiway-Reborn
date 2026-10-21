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
    /// <summary>形成后是否保持当前内部形状。</summary>
    public readonly bool HoldsShape;
    /// <summary>是否作为该材料未指定物态时的默认值。</summary>
    internal readonly bool IsDefault;

    /// <summary>登记者明确提供材料规则，计算阶段只读取不可变数值及编号。</summary>
    /// <param name="id">材料编号。</param>
    /// <param name="state">物态编号。</param>
    /// <param name="damageKind">实际伤害规则。</param>
    /// <param name="surface">主体显示表面。</param>
    /// <param name="holdsShape">是否保持内部形状。</param>
    /// <param name="isDefault">是否为材料默认物态。</param>
    public AbilityMatter(string id, string state, string damageKind, string surface = null, bool holdsShape = false, bool isDefault = true)
    { Id = id; State = state; DamageKind = damageKind; Surface = surface; HoldsShape = holdsShape; IsDefault = isDefault; }
}

/// <summary>材料规则由内容层在初始化时登记，编译时一次解析；工作线程不查表。</summary>
public static class AbilityMatters
{
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
        if (string.IsNullOrEmpty(id) || !defaults.TryGetValue(id, out string standard))
            throw new InvalidDataException(path + "：未提供实际材料规则：" + id);
        if (state == null) state = standard;
        if (entries.TryGetValue((id, state), out AbilityMatter value)) return value;
        throw new InvalidDataException(path + "：材料未提供物态：" + id + "/" + state);
    }
}

/// <summary>物态变化的实际触发，不将选中目标当成真实接触。</summary>
public enum ConstructionChangeTrigger
{
    /// <summary>到达明确的施放世界时点。</summary>
    Time,
    /// <summary>本部位真正作用于人物之后。</summary>
    ActorContact,
    /// <summary>本部位完成形体相撞或反射之后。</summary>
    Clash
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
    /// <summary>记录编译完成的规则。</summary>
    /// <param name="at">最早时点。</param>
    /// <param name="trigger">触发种类。</param>
    /// <param name="matter">输出状态。</param>
    internal CompiledMatterChange(float at, ConstructionChangeTrigger trigger, AbilityMatter matter)
    { At = at; Trigger = trigger; Matter = matter; }
}

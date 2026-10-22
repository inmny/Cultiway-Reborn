using System;
using System.IO;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>使一个部位及其普通后代等待真实事件，不另建整招运行类型。</summary>
public sealed class ConstructionActivation
{
    /// <summary>父体用..，身体用body，也可采用相对路径或root开头的完整部位路径。</summary>
    public string Source = "..";
    /// <summary>明确等待的人物接触、交锋、结束或材料变化。</summary>
    public ConstructionChangeTrigger Trigger = ConstructionChangeTrigger.ActorContact;
    /// <summary>确认事件后额外等待的世界秒数。</summary>
    public float Delay;
    /// <summary>继续附着，或从事件位置独立开始。</summary>
    public ConstructionEventPlacement Placement = ConstructionEventPlacement.Event;
    /// <summary>材料变化时可明确筛选输出材料；空值接受所有实际变化。</summary>
    public string Matter;
    /// <summary>明确输出物态，空值采用该材料的默认物态。</summary>
    public string State;
}

/// <summary>真实事件带来的新部位放置。</summary>
public enum ConstructionEventPlacement
{
    /// <summary>仍用真实父体、挂点和尺寸；连接结束时一起结束。</summary>
    Attached,
    /// <summary>事件处独立开始，沿用事件来源朝向及明确的地图尺寸。</summary>
    Event
}

/// <summary>已经复制和检查的事件条件，编译时补齐来源索引。</summary>
internal sealed class CompiledConstructionActivation
{
    /// <summary>只用于编译引用的独立路径。</summary>
    internal readonly string SourcePath;
    /// <summary>已解析的来源，负一表示身体。</summary>
    internal int Source;
    /// <summary>已经检查的事件。</summary>
    internal readonly ConstructionChangeTrigger Trigger;
    /// <summary>事件后的明确延迟。</summary>
    internal readonly float Delay;
    /// <summary>真实放置方式。</summary>
    internal readonly ConstructionEventPlacement Placement;
    /// <summary>材料事件的可选实际输出筛选。</summary>
    internal readonly AbilityMatter Matter;

    /// <summary>在创建、改造和恢复时统一检查条件。</summary>
    /// <param name="source">明确条件。</param>
    /// <param name="lifetime">整次上限。</param>
    /// <param name="path">具体错误位置。</param>
    internal CompiledConstructionActivation(ConstructionActivation source, float lifetime, string path)
    {
        SourcePath = source.Source; Trigger = source.Trigger; Delay = source.Delay; Placement = source.Placement;
        if (string.IsNullOrWhiteSpace(SourcePath) || Trigger is not (ConstructionChangeTrigger.ActorContact or ConstructionChangeTrigger.Clash or ConstructionChangeTrigger.End or ConstructionChangeTrigger.MatterChanged) ||
            float.IsNaN(Delay) || float.IsInfinity(Delay) || Delay < 0f || Delay >= lifetime || !Enum.IsDefined(typeof(ConstructionEventPlacement), Placement) ||
            source.State != null && source.Matter == null || Trigger != ConstructionChangeTrigger.MatterChanged && (source.Matter != null || source.State != null))
            throw new InvalidDataException(path + "：事件来源、种类、延迟、放置或材料筛选无效");
        if (source.Matter != null) Matter = AbilityMatters.Resolve(source.Matter, source.State, path + "/Matter");
    }
}

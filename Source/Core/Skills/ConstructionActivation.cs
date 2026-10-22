using System;
using System.IO;

namespace Cultiway.Core.Skills;

/// <summary>使一个部位及其普通后代等待真实事件，不另建整招运行类型。</summary>
public sealed class ConstructionActivation : ConstructionCondition
{
    /// <summary>开始条件默认来自实际父体。</summary>
    public ConstructionActivation() { Source = ".."; }
    /// <summary>继续附着，或从事件位置独立开始。</summary>
    public ConstructionEventPlacement Placement = ConstructionEventPlacement.Event;
}

/// <summary>真实事件带来的新部位放置。</summary>
public enum ConstructionEventPlacement
{
    /// <summary>仍用真实父体、挂点和尺寸；连接结束时一起结束。</summary>
    Attached,
    /// <summary>事件处独立开始，沿用事件来源朝向及明确的地图尺寸。</summary>
    Event
}

/// <summary>检查后的部位开始条件。</summary>
internal sealed class CompiledConstructionActivation : CompiledConstructionCondition
{
    /// <summary>真实放置方式。</summary>
    internal readonly ConstructionEventPlacement Placement;
    /// <summary>检查共同条件和放置。</summary>
    /// <param name="source">明确条件。</param>
    /// <param name="lifetime">整次上限。</param>
    /// <param name="path">具体错误位置。</param>
    internal CompiledConstructionActivation(ConstructionActivation source, float lifetime, string path) : base(source, lifetime, path)
    {
        Placement = source.Placement;
        if (!Enum.IsDefined(typeof(ConstructionEventPlacement), Placement)) throw new InvalidDataException(path + "：事件放置无效");
    }
}

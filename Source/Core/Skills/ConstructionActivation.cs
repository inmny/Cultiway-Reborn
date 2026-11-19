using System;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>使一个部位及其普通后代等待真实事件，不另建整招运行类型。</summary>
public sealed class ConstructionActivation : ConstructionCondition
{
    /// <summary>开始条件默认来自实际父体。</summary>
    public ConstructionActivation() { Source = ".."; }
    /// <summary>继续附着，或从事件位置独立开始。</summary>
    public ConstructionEventPlacement Placement = ConstructionEventPlacement.Event;
    /// <summary>来源结束时按权重交接实际余力、材料及控制身份，不重新使用初始威力。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool InheritRemainder;
    /// <summary>余量交接后明确开始新的接触组；默认仍继承来源已命中的记录。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ResetContacts;
    /// <summary>人物接触后在其附近选择尚未命中过的目标；空值沿用本次原目标。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactTarget Target;

}

/// <summary>真实事件带来的新部位放置。</summary>
public enum ConstructionEventPlacement
{
    /// <summary>仍用真实父体、挂点和尺寸；连接结束时一起结束。</summary>
    Attached = 0,

    /// <summary>事件处独立开始，沿用事件来源朝向及明确的地图尺寸。</summary>
    Event = 1
}

/// <summary>检查后的部位开始条件。</summary>
internal sealed class CompiledConstructionActivation : CompiledConstructionCondition
{
    /// <summary>真实放置方式。</summary>
    internal readonly ConstructionEventPlacement Placement;
    /// <summary>是否接受来源结束时的真实剩余状态。</summary>
    internal readonly bool InheritRemainder;
    /// <summary>余量交接后是否明确开始新接触组。</summary>
    internal readonly bool ResetContacts;
    /// <summary>可选的真实接触后选敌规则。</summary>
    internal readonly CompiledContactTarget Target;

    /// <summary>检查共同条件和放置。</summary>
    /// <param name="source">明确条件。</param>
    /// <param name="lifetime">整次上限。</param>
    /// <param name="path">具体错误位置。</param>
    internal CompiledConstructionActivation(ConstructionActivation source, float lifetime, string path) : base(source, lifetime, path)
    {
        Placement = source.Placement; InheritRemainder = source.InheritRemainder; ResetContacts = source.ResetContacts;
        Target = source.Target == null ? null : new CompiledContactTarget(source.Target, path + "/Target");
        if (ResetContacts && !InheritRemainder)
            throw new InvalidDataException(path + "：开始新接触组需要明确继承来源剩余力量，不能凭空重建强度");
        if (Target != null && (Trigger != ConstructionChangeTrigger.ActorContact || Placement != ConstructionEventPlacement.Event || InheritRemainder))
            throw new InvalidDataException(path + "：附近选敌需要人物接触及事件处独立开始，不能同时接受结束余量");
        if (!Enum.IsDefined(typeof(ConstructionEventPlacement), Placement)) throw new InvalidDataException(path + "：事件放置无效");
        if (InheritRemainder && (Trigger is not (ConstructionChangeTrigger.End or ConstructionChangeTrigger.Handoff) || Placement != ConstructionEventPlacement.Event))
            throw new InvalidDataException(path + "：余量交接需要结束或明确交接事件，并在事件处独立开始");
        if (Trigger == ConstructionChangeTrigger.Handoff && !InheritRemainder)
            throw new InvalidDataException(path + "：等待交接的部位必须继承实际余量");
    }
}

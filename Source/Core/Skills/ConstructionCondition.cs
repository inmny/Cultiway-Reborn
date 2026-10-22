using System;
using System.IO;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>部位开始和运行中操作共用的真实事件条件。</summary>
public class ConstructionCondition
{
    /// <summary>自身用.，父体用..，身体用body，也可采用明确部位路径。</summary>
    public string Source = ".";
    /// <summary>明确的人物接触、交锋、结束或材料变化。</summary>
    public ConstructionChangeTrigger Trigger = ConstructionChangeTrigger.ActorContact;
    /// <summary>损耗比例或局部尺寸阈值，只用于明确数值条件。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float? Threshold;
    /// <summary>确认事件后额外等待的世界秒数。</summary>
    public float Delay;
    /// <summary>材料变化时的可选实际输出材料筛选。</summary>
    public string Matter;
    /// <summary>明确输出物态，空值采用该材料的默认物态。</summary>
    public string State;
}

/// <summary>复制及检查后的共同条件，编译时补齐来源。</summary>
internal class CompiledConstructionCondition
{
    /// <summary>除按时操作外，实际可订阅的事件种类数。</summary>
    internal const int EventCount = 7;
    /// <summary>明确数值边界，非数值事件不使用。</summary>
    internal readonly float Threshold;
    /// <summary>只用于解析引用的独立路径。</summary>
    internal readonly string SourcePath;
    /// <summary>已解析来源，负一表示身体。</summary>
    internal int Source;
    /// <summary>实际事件种类。</summary>
    internal readonly ConstructionChangeTrigger Trigger;
    /// <summary>明确的世界秒延迟。</summary>
    internal readonly float Delay;
    /// <summary>可选实际输出材料筛选。</summary>
    internal readonly AbilityMatter Matter;

    /// <summary>在创建、编辑和恢复边界统一检查。</summary>
    /// <param name="source">外部条件。</param>
    /// <param name="lifetime">整次上限。</param>
    /// <param name="path">具体错误位置。</param>
    internal CompiledConstructionCondition(ConstructionCondition source, float lifetime, string path)
    {
        SourcePath = source.Source; Trigger = source.Trigger; Delay = source.Delay;
        if (string.IsNullOrWhiteSpace(SourcePath) || Trigger is not (ConstructionChangeTrigger.ActorContact or ConstructionChangeTrigger.Clash or ConstructionChangeTrigger.End or ConstructionChangeTrigger.MatterChanged or ConstructionChangeTrigger.ForceBelow or ConstructionChangeTrigger.SizeAbove or ConstructionChangeTrigger.SizeBelow) ||
            float.IsNaN(Delay) || float.IsInfinity(Delay) || Delay < 0f || Delay >= lifetime ||
            source.State != null && source.Matter == null || Trigger != ConstructionChangeTrigger.MatterChanged && (source.Matter != null || source.State != null))
            throw new InvalidDataException(path + "：事件来源、种类、延迟或材料筛选无效");
        bool numeric = Trigger is ConstructionChangeTrigger.ForceBelow or ConstructionChangeTrigger.SizeAbove or ConstructionChangeTrigger.SizeBelow;
        if (numeric != source.Threshold.HasValue || numeric && (float.IsNaN(source.Threshold.Value) || float.IsInfinity(source.Threshold.Value) ||
            source.Threshold.Value < 0 || source.Threshold.Value > (Trigger == ConstructionChangeTrigger.ForceBelow ? 1f : 64f) ||
            Trigger == ConstructionChangeTrigger.ForceBelow && source.Threshold.Value == 1 || Trigger == ConstructionChangeTrigger.SizeAbove && source.Threshold.Value == 0))
            throw new InvalidDataException(path + "：数值条件需要明确有效阈值，损耗为0至1的剩余比例，成长为0至64的局部尺寸");
        Threshold = source.Threshold ?? 0f;
        if (source.Matter != null) Matter = AbilityMatters.Resolve(source.Matter, source.State, path + "/Matter");
    }
    /// <summary>只接受指定方向的真实越界，边界本身包含在结果一侧。</summary>
    /// <param name="before">事件发生前的真实值。</param>
    /// <param name="after">事件发生后的真实值。</param>
    internal bool Crossed(float before, float after) => Trigger switch
    {
        ConstructionChangeTrigger.SizeAbove => before < Threshold && after >= Threshold,
        ConstructionChangeTrigger.SizeBelow or ConstructionChangeTrigger.ForceBelow => before > Threshold && after <= Threshold,
        _ => true
    };

}

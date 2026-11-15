using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Newtonsoft.Json;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.Skills;

/// <summary>同类状态的合并规则，由状态系统维护，不另建更新循环。</summary>
public enum ConstructionStatusApplication
{
    /// <summary>同类保留最强属性状态，沿用正式首版的缺省含义。</summary>
    Strongest = 0,
    /// <summary>每次真实接触独立加入一份状态。</summary>
    Stack = 1,
    /// <summary>刷新最早加入的同类状态，并采用本次来源及实际时长。</summary>
    Refresh = 2,
    /// <summary>同一来源分别计数，未满上限时独立加入，满层刷新流逝时间最少的一层。</summary>
    SourceStack = 3
}

/// <summary>真实接触附带的已登记属性状态，可与伤害、恢复和其他状态自由组合。</summary>
public sealed class ConstructionStatus
{
    /// <summary>共享状态库中的稳定编号，不使用整招或人物编号。</summary>
    public string Id;
    /// <summary>实际接触后保留的世界秒数。</summary>
    public float Duration;
    /// <summary>沿用同类最强规则比较的强度，属性值单独保存。</summary>
    public float Potency;
    /// <summary>原版或模组已登记的属性编号及本次加成，允许明确负值。</summary>
    public Dictionary<string, float> Stats;
    /// <summary>真实接触时取目标当前非负同名属性的比例，范围正负一；同名固定值作为额外加成，省略固定表时保留模板的其他属性及控制标记。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, float> TargetStatRatios;
    /// <summary>同类合并方式；叠加或刷新时允许省略属性，使用状态自身属性及控制标记。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public ConstructionStatusApplication Application;
    /// <summary>成功施加后等待抗性结算所得的真实时长，供短暂硬直等状态使用。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool WaitForDuration;
    /// <summary>同源有限叠加的最多层数，其他合并方式省略。</summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int StackLimit;
    /// <summary>可选周期伤害的本体量比例及元素构成，由原共享状态维护周期。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionStatusTick Tick;
}

/// <summary>在编译边界解析并复制的状态资料，数值计算线程不施加游戏状态。</summary>
public sealed class CompiledConstructionStatus
{
    /// <summary>已经确认支持属性覆盖的共享状态。</summary>
    public StatusEffectAsset Asset { get; }
    /// <summary>实际持续世界秒数。</summary>
    public float Duration { get; }
    /// <summary>实际比较强度。</summary>
    public float Potency { get; }
    /// <summary>独立只读的属性值，不持有可编辑字典。</summary>
    public IReadOnlyDictionary<string, float> Stats { get; }
    /// <summary>独立只读的目标属性比例，只在主线程确认真实接触后取值。</summary>
    public IReadOnlyDictionary<string, float> TargetStatRatios { get; }
    /// <summary>已经检查的同类合并方式。</summary>
    public ConstructionStatusApplication Application { get; }
    /// <summary>是否使用抗性结算后的真实时长等待。</summary>
    public bool WaitForDuration { get; }
    /// <summary>已检查的同源层数上限。</summary>
    public int StackLimit { get; }
    /// <summary>独立编译的周期伤害声明。</summary>
    public CompiledConstructionStatusTick Tick { get; }

    /// <summary>只在用户资料进入执行前检查编号、范围与状态能力。</summary>
    /// <param name="source">可保存的作用资料。</param><param name="path">资料错误位置。</param>
    internal CompiledConstructionStatus(ConstructionStatus source, string path)
    {
        if (source == null || string.IsNullOrEmpty(source.Id) || !Finite(source.Duration) || source.Duration <= 0f ||
            source.Duration > 3600f || !Finite(source.Potency) || source.Potency < 0f || source.Potency > 1000000f ||
            !Enum.IsDefined(typeof(ConstructionStatusApplication), source.Application) ||
            source.Application == ConstructionStatusApplication.Strongest && (source.Stats == null || source.Stats.Count == 0) && (source.TargetStatRatios == null || source.TargetStatRatios.Count == 0) ||
            (source.Stats?.Count ?? 0) + (source.TargetStatRatios?.Count ?? 0) > 64)
            throw new InvalidDataException(path + "：状态需要稳定编号、0至3600内的正时长、0至一百万的强度、有效合并方式及合计最多64项属性；最强规则必须明确固定值或目标比例");
        Asset = ModClass.L.StatusEffectLibrary.get(source.Id);
        if (Asset == null || source.Application == ConstructionStatusApplication.Strongest && !Asset.SupportsStatOverwrite)
            throw new InvalidDataException(path + "：未登记或不支持所选合并方式的共享状态：" + source.Id);
        var stats = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var pair in source.Stats ?? new Dictionary<string, float>())
        {
            if (string.IsNullOrEmpty(pair.Key) || !Finite(pair.Value) || Math.Abs(pair.Value) > 1000000f ||
                AssetManager.base_stats_library.get(pair.Key) is not { ignore: false })
                throw new InvalidDataException(path + "/Stats：属性需要已登记的有效编号及正负一百万内的有限值：" + pair.Key);
            stats.Add(pair.Key, pair.Value);
        }
        Stats = source.Stats == null ? null : new ReadOnlyDictionary<string, float>(stats);
        if (source.TargetStatRatios != null)
        {
            var ratios = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var pair in source.TargetStatRatios)
            {
                if (string.IsNullOrEmpty(pair.Key) || !Finite(pair.Value) || Math.Abs(pair.Value) > 1f ||
                    AssetManager.base_stats_library.get(pair.Key) is not { ignore: false })
                    throw new InvalidDataException(path + "/TargetStatRatios：比例需要已登记的有效属性及正负一内的有限值：" + pair.Key);
                ratios.Add(pair.Key, pair.Value);
            }
            TargetStatRatios = new ReadOnlyDictionary<string, float>(ratios);
        }
        if (source.Application == ConstructionStatusApplication.SourceStack ? source.StackLimit < 1 || source.StackLimit > 99 : source.StackLimit != 0)
            throw new InvalidDataException(path + "/StackLimit：同源有限叠加需要1至99层，其他方式不声明层数");
        if (source.Tick != null)
        {
            if (source.Application == ConstructionStatusApplication.Strongest || !Asset.SupportsTickValue)
                throw new InvalidDataException(path + "/Tick：周期声明需要已登记的周期状态和明确叠加或刷新方式");
            Tick = new CompiledConstructionStatusTick(source.Tick, path + "/Tick");
        }
        StackLimit = source.StackLimit;
        Application = source.Application;
        WaitForDuration = source.WaitForDuration;
        Duration = source.Duration;
        Potency = source.Potency;
    }

    /// <summary>只在资料边界拒绝非有限数值。</summary><param name="value">实际输入。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}


/// <summary>每秒量取真实接触时本体余力的比例，不提前保存目标伤害结果。</summary>
public sealed class ConstructionStatusTick
{
    /// <summary>本体余力每秒转为持续伤害的比例，包含原参数和来源换算。</summary>
    public float ForcePerSecond;
    /// <summary>本次周期伤害的八类元素权重。</summary>
    public ElementComposition Element;
}

/// <summary>只在资料边界检查和复制的周期伤害。</summary>
public sealed class CompiledConstructionStatusTick
{
    /// <summary>已检查的每秒本体量比例。</summary>
    public float ForcePerSecond { get; }
    /// <summary>独立值类型的伤害元素。</summary>
    public ElementComposition Element { get; }
    /// <summary>核对正每秒比例和有效非负元素，不读取人物或世界。</summary>
    /// <param name="source">可保存声明。</param><param name="path">错误位置。</param>
    internal CompiledConstructionStatusTick(ConstructionStatusTick source, string path)
    {
        if (float.IsNaN(source.ForcePerSecond) || float.IsInfinity(source.ForcePerSecond) || source.ForcePerSecond <= 0f || source.ForcePerSecond > 1000000f)
            throw new InvalidDataException(path + "：每秒本体比例需要正一百万内的有限值");
        float total = 0f;
        for (int i = 0; i < 8; i++)
        {
            float value = source.Element[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 1f)
                throw new InvalidDataException(path + "/Element：元素权重需要零至一内的有限值");
            total += value;
        }
        if (total <= 0f) throw new InvalidDataException(path + "/Element：元素构成不能全为零");
        ForcePerSecond = source.ForcePerSecond;
        var element = source.Element;
        element.Normalize();
        Element = element;
    }
}

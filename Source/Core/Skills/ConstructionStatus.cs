using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.Skills;

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

    /// <summary>只在用户资料进入执行前检查编号、范围与状态能力。</summary>
    /// <param name="source">可保存的作用资料。</param><param name="path">资料错误位置。</param>
    internal CompiledConstructionStatus(ConstructionStatus source, string path)
    {
        if (source == null || string.IsNullOrEmpty(source.Id) || !Finite(source.Duration) || source.Duration <= 0f ||
            source.Duration > 3600f || !Finite(source.Potency) || source.Potency < 0f || source.Potency > 1000000f ||
            source.Stats == null || source.Stats.Count == 0 || source.Stats.Count > 64)
            throw new InvalidDataException(path + "：状态需要稳定编号、0至3600内的正时长、0至一百万的强度及1至64项属性");
        Asset = ModClass.L.StatusEffectLibrary.get(source.Id);
        if (Asset == null || !Asset.SupportsStatOverwrite)
            throw new InvalidDataException(path + "：未登记或不支持属性覆盖的共享状态：" + source.Id);
        var stats = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var pair in source.Stats)
        {
            if (string.IsNullOrEmpty(pair.Key) || !Finite(pair.Value) || Math.Abs(pair.Value) > 1000000f ||
                AssetManager.base_stats_library.get(pair.Key) is not { ignore: false })
                throw new InvalidDataException(path + "/Stats：属性需要已登记的有效编号及正负一百万内的有限值：" + pair.Key);
            stats.Add(pair.Key, pair.Value);
        }
        Stats = new ReadOnlyDictionary<string, float>(stats);
        Duration = source.Duration;
        Potency = source.Potency;
    }

    /// <summary>只在资料边界拒绝非有限数值。</summary><param name="value">实际输入。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

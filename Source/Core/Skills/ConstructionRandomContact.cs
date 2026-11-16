using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Cultiway.Core.Skills;

/// <summary>真实命中时按概率从明确候选中等概率选择一个；候选不再解释旧词条。</summary>
public sealed class ConstructionRandomContact
{
    /// <summary>每次真实命中独立触发的概率。</summary>
    public float Chance = 1f;

    /// <summary>每个候选可同时附带状态、独立元素伤害和击退。</summary>
    public ConstructionContactOutcome[] Outcomes;
}

/// <summary>可组合的单次接触结果；未声明的作用不产生效果。</summary>
public sealed class ConstructionContactOutcome
{
    /// <summary>共用状态服务执行的属性或周期伤害。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionStatus[] Statuses;

    /// <summary>同一次选中结果中按顺序结算的独立元素伤害。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionContactDamage[] Damage;

    /// <summary>选中结果附带的普通击退和腾起。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ConstructionKnockback Knockback;
}

/// <summary>有限且独立复制的候选列表，编译和预测不抽取结果。</summary>
public sealed class CompiledConstructionRandomContact
{
    /// <summary>真实命中时的触发概率。</summary>
    public float Chance { get; }

    /// <summary>已经独立编译的候选结果。</summary>
    public IReadOnlyList<CompiledConstructionContactOutcome> Outcomes { get; }

    /// <summary>是否有可能产生至少一种实际作用。</summary>
    public bool HasEffect { get; }

    /// <summary>在外部资料边界一次检查概率和候选数量。</summary>
    /// <param name="source">可保存的候选资料。</param><param name="path">错误资料位置。</param>
    internal CompiledConstructionRandomContact(ConstructionRandomContact source, string path)
    {
        if (float.IsNaN(source.Chance) || float.IsInfinity(source.Chance) || source.Chance < 0f || source.Chance > 1f ||
            source.Outcomes == null || source.Outcomes.Length == 0 || source.Outcomes.Length > 16)
            throw new InvalidDataException(path + "：概率需要零至一内的有限值，接触候选需要一至十六项");
        Chance = source.Chance;
        Outcomes = Array.AsReadOnly(source.Outcomes.Select((outcome, index) =>
            new CompiledConstructionContactOutcome(outcome, path + "/Outcomes/" + index)).ToArray());
        HasEffect = Chance > 0f && Outcomes.Any(outcome => outcome.HasEffect);
    }
}

/// <summary>单个候选只保存共用状态、伤害和击退，不再创建专用施放或更新器。</summary>
public sealed class CompiledConstructionContactOutcome
{
    /// <summary>已经独立编译的状态。</summary>
    public IReadOnlyList<CompiledConstructionStatus> Statuses { get; }

    /// <summary>已经独立编译的伤害。</summary>
    public IReadOnlyList<CompiledConstructionContactDamage> Damage { get; }

    /// <summary>已经独立编译的击退。</summary>
    public CompiledConstructionKnockback Knockback { get; }

    /// <summary>本候选是否声明实际作用，允许明确的空候选作为落空。</summary>
    public bool HasEffect => Statuses.Count > 0 || Damage.Count > 0 || Knockback?.HasEffect == true;

    /// <summary>候选共享普通作用的编译边界，不允许无限嵌套。</summary>
    /// <param name="source">可保存的单个候选。</param><param name="path">错误资料位置。</param>
    internal CompiledConstructionContactOutcome(ConstructionContactOutcome source, string path)
    {
        if (source == null || source.Statuses?.Length > 16 || source.Damage?.Length > 8)
            throw new InvalidDataException(path + "：候选不能为空，最多十六个状态和八份追加伤害");
        Statuses = source.Statuses == null ? Array.Empty<CompiledConstructionStatus>() :
            Array.AsReadOnly(source.Statuses.Select((status, index) => new CompiledConstructionStatus(status, path + "/Statuses/" + index)).ToArray());
        Damage = source.Damage == null ? Array.Empty<CompiledConstructionContactDamage>() :
            Array.AsReadOnly(source.Damage.Select((damage, index) => new CompiledConstructionContactDamage(damage, path + "/Damage/" + index)).Where(damage => damage.PerForce > 0f).ToArray());
        if (source.Knockback != null) Knockback = new CompiledConstructionKnockback(source.Knockback, path + "/Knockback");
    }
}

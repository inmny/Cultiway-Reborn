using System;
using Cultiway.Core;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.Components;

/// <summary>金丹与元婴共用的组合境界类型。</summary>
public enum CoreFormationRealm : byte
{
    /// <summary>金丹实例。</summary>
    Jindan,

    /// <summary>元婴实例。</summary>
    Yuanying
}

/// <summary>一个组合原子在角色实例中的固化状态。</summary>
public struct CoreFormationAtomState
{
    /// <summary>稳定的原子资产 ID。</summary>
    public string atom_id;

    /// <summary>该原子对最终组合的相对贡献。</summary>
    public float weight;

    /// <summary>达到该金丹转数后生效；0 表示形成时已经生效。</summary>
    public int awakening_stage;

    /// <summary>是否继承自结婴前的金丹。</summary>
    public bool inherited;

    /// <summary>判断该原子是否已经达到显化阶段。</summary>
    public bool IsActive(int stage)
    {
        return awakening_stage <= stage;
    }
}

/// <summary>组合结果提供的一项基础属性系数。</summary>
public struct CoreFormationStatValue
{
    /// <summary>WorldBox 基础属性 ID。</summary>
    public string stat_id;

    /// <summary>强度为 1 时提供的属性值。</summary>
    public float value;

    /// <summary>创建一项由基础属性 ID 和强度系数组成的固化记录。</summary>
    public CoreFormationStatValue(string statId, float value)
    {
        stat_id = statId;
        this.value = value;
    }
}

/// <summary>
/// 单个角色持有的金丹或元婴组合快照。快照只保存有上限的值类型和稳定 ID，
/// 不为角色创建动态 Asset。
/// </summary>
public struct CoreFormationSnapshot
{
    public const int CurrentVersion = 1;

    /// <summary>快照数据版本。</summary>
    public int version;

    /// <summary>组合身份签名；不包含角色 ID、角色名或随机遍历顺序。</summary>
    public string signature;

    /// <summary>由组合签名和原子词干确定的规范名称。</summary>
    public string canonical_name;

    /// <summary>该快照属于金丹还是元婴。</summary>
    public CoreFormationRealm realm;

    /// <summary>当前组合的品质层级，范围为 0-3。</summary>
    public int quality;

    /// <summary>五行、阴阳和混沌的连续组成。</summary>
    public ElementComposition composition;

    /// <summary>已选择的激活与潜在原子，数量由组合器限制。</summary>
    public CoreFormationAtomState[] atoms;

    /// <summary>当前觉醒状态下的最终属性系数。</summary>
    public CoreFormationStatValue[] stats;

    /// <summary>当前觉醒状态下的稳定语义贡献。</summary>
    public SemanticContribution[] semantics;

    /// <summary>与当前组合最匹配、在突破时授予的代表法术实体资产 ID。</summary>
    public string representative_skill_id;

    /// <summary>是否为当前代码能够直接消费的完整快照。</summary>
    public bool IsValid => version == CurrentVersion && !string.IsNullOrEmpty(signature) && atoms is { Length: > 0 };

    /// <summary>复制快照中的数组，避免传承或夺舍后的两个角色共享可变状态。</summary>
    public readonly CoreFormationSnapshot DeepClone()
    {
        var clone = this;
        clone.atoms = atoms == null ? Array.Empty<CoreFormationAtomState>() : (CoreFormationAtomState[])atoms.Clone();
        clone.stats = stats == null ? Array.Empty<CoreFormationStatValue>() : (CoreFormationStatValue[])stats.Clone();
        clone.semantics = semantics == null ? Array.Empty<SemanticContribution>() : (SemanticContribution[])semantics.Clone();
        return clone;
    }
}

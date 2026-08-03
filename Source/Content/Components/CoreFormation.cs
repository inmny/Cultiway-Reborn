using System;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;

namespace Cultiway.Content.Components;

/// <summary>仙道各阶段可独立归档的成果境界。</summary>
public enum CoreFormationRealm : byte
{
    /// <summary>炼气期凝成的命名真气。</summary>
    QiRefinement,

    /// <summary>筑基期熬炼成形的仙基。</summary>
    Foundation,

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

    /// <summary>达到该成果精炼次数后生效；0 表示形成时已经生效。</summary>
    public int awakening_stage;

    /// <summary>是否从上一境界成果继承。</summary>
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
/// 单个角色持有的阶段成果快照。快照只保存有上限的值类型和稳定 ID，
/// 不为角色创建动态 Asset。
/// </summary>
public struct CoreFormationSnapshot
{
    public const int CurrentVersion = 3;

    /// <summary>快照数据版本。</summary>
    public int version;

    /// <summary>组合身份签名；不包含角色 ID、角色名或随机遍历顺序。</summary>
    public string signature;

    /// <summary>由组合签名和原子词干确定的规范名称。</summary>
    public string canonical_name;

    /// <summary>贯穿真气、仙基、金丹与元婴的短命名词干。</summary>
    public string lineage_stem;

    /// <summary>上一阶段成果的稳定签名；炼气成果没有来源时为空。</summary>
    public string source_signature;

    /// <summary>上一阶段成果的规范名称；炼气成果没有来源时为空。</summary>
    public string source_name;

    /// <summary>上一阶段成果在跃迁时已经完成的精炼次数。</summary>
    public int source_refinement;

    /// <summary>该快照所属的仙道成果境界。</summary>
    public CoreFormationRealm realm;

    /// <summary>当前组合形成时确定的黄、玄、地、天四阶九品品阶。</summary>
    public ItemLevel quality;

    /// <summary>当前成果经形成与后续精炼累计得到的连续强度。</summary>
    public float strength;

    /// <summary>本成果已经完成的精炼次数；炼气时即为真气层数。</summary>
    public int refinement;

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

    /// <summary>是否为当前代码能够直接消费的完整成果快照。</summary>
    public bool IsValid => version == CurrentVersion &&
                           !string.IsNullOrEmpty(signature) &&
                           !string.IsNullOrEmpty(canonical_name) &&
                           atoms is { Length: > 0 };

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
